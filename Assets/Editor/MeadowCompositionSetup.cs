using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using MagicIsland.Ocean;
using MagicIsland.TerrainSystem;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    // One explicit, preflighted relocation. No changes to flower/insect geometry or world rules.
    public static class MeadowCompositionSetup
    {
        public const string Marker="MagicIsland.MeadowComposition.v1";
        static void Require(bool ok,string why)=>MeadowRabbitSetup.Require(ok,why);
        public static Vector2[] Flowers(IslandTerrain terrain)
        {
            var owner=terrain.World.GetComponentInChildren<WorldWaterEcology>(true);
            var centers=new HashSet<Vector2>();
            foreach(var r in owner.GrasslandAmbient.Where(r=>r.sharedMaterial.shader.name=="MagicIsland/Meadow Insects"&&r.name.Contains("Butterfly")))
            {
                var mesh=r.GetComponent<MeshFilter>().sharedMesh;var a=mesh.uv;var b=mesh.uv2;
                Require(a.Length==mesh.vertexCount&&b.Length==a.Length,"preserve incomplete insect metadata");
                for(int i=0;i<a.Length;i++)
                {
                    var p=terrain.transform.InverseTransformPoint(r.transform.TransformPoint(new Vector3(a[i].x,b[i].x,a[i].y)));
                    centers.Add(new Vector2(p.x,p.z));
                }
            }
            Require(centers.Count==15,"preserve custom insect distribution");return centers.ToArray();
        }
        public static MeadowRabbitHabitat[] Plan(IslandTerrain terrain)
        {
            var owner=terrain.World.GetComponentInChildren<WorldWaterEcology>(true);var obstacles=MeadowRabbitSetup.Obstacles(terrain);var flowers=Flowers(terrain);
            var allFlowers=GrasslandCompositionSetup.Sites().Concat(GrasslandFinishSetup.Sites().Select(s=>s.Point)).Concat(BiomePolishSetup.Sites(1).Select(s=>s.Point)).Concat(flowers).Distinct().ToArray();
            var result=new List<MeadowRabbitHabitat>();
            foreach(var original in owner.RabbitHabitats)
            {
                var h=JsonUtility.FromJson<MeadowRabbitHabitat>(JsonUtility.ToJson(original));
                var old=terrain.transform.InverseTransformPoint(owner.transform.TransformPoint(original.Track[original.Track.Length/2]));
                var d3=terrain.transform.InverseTransformDirection(owner.transform.TransformDirection(original.Track.Last()-original.Track[0]));var direction=new Vector2(d3.x,d3.z).normalized;
                Vector3[] chosen=null;
                foreach(var flower in flowers.OrderBy(f=>Vector2.Distance(f,new Vector2(old.x,old.z))))
                {
                    if(Vector2.Distance(flower,new Vector2(old.x,old.z))>70)continue;
                    foreach(float offset in new[]{3.6f,-3.6f,4.1f,-4.1f,4.8f,-4.8f,5.5f,-5.5f})
                    {
                        var center=flower+direction*offset;
                        if(result.Any(r=>Vector3.Distance(r.Track[r.Track.Length/2],owner.transform.InverseTransformPoint(terrain.transform.TransformPoint(new Vector3(center.x,old.y,center.y))))<12))continue;
                        chosen=MeadowRabbitSetup.SafeTrack(terrain,center,direction,obstacles,allFlowers,1.7f);
                        if(chosen!=null)break;
                    }
                    if(chosen!=null)break;
                }
                Require(chosen!=null,"no safe composition near existing flowers for rabbit "+result.Count);
                h.Track=chosen;Require(h.IsValid,"valid composition track");result.Add(h);
            }
            return result.ToArray();
        }
        public static bool SameShape(Mesh a,Mesh b)
        {
            if(a.vertexCount!=b.vertexCount||a.subMeshCount!=b.subMeshCount||!a.triangles.SequenceEqual(b.triangles)||!a.colors.SequenceEqual(b.colors)
                ||Vector3.Distance(a.bounds.center,b.bounds.center)>.0001f||Vector3.Distance(a.bounds.size,b.bounds.size)>.0001f)return false;
            bool Near(Vector3[] x,Vector3[] y)=>x.Length==y.Length&&x.Zip(y,(p,q)=>(p-q).sqrMagnitude<.00000001f).All(v=>v);
            if(!Near(a.vertices,b.vertices)||!Near(a.normals,b.normals))return false;
            for(int channel=0;channel<8;channel++)
            {
                var x=new List<Vector4>();var y=new List<Vector4>();a.GetUVs(channel,x);b.GetUVs(channel,y);
                if(x.Count!=y.Count||x.Zip(y,(p,q)=>(p-q).sqrMagnitude<.00000001f).Any(v=>!v))return false;
            }
            return true;
        }
        [MenuItem("Magic Island/Art/Refine Meadow Ecology Composition (Explicit)")]
        public static void Refine()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling,"Edit Mode required");
            var importer=AssetImporter.GetAtPath(MeadowRabbitSetup.MeshPath);Require(importer!=null,"existing rabbit mesh");
            if(importer.userData==Marker){Debug.Log("Meadow composition and later manual edits retained.");return;}
            Require(string.IsNullOrEmpty(importer.userData),"preserve custom rabbit metadata");
            var root=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);Mesh expected=null,prepared=null;
            try
            {
                var terrain=root.GetComponent<IslandTerrain>();var owner=terrain.World.GetComponentInChildren<WorldWaterEcology>(true);
                Require(owner.IsValid&&owner.RabbitHabitats.Length==4,"existing four rabbits");
                var transform=owner.MeadowRabbits.transform;
                Require(transform.parent==owner.transform&&transform.localPosition==Vector3.zero&&transform.localRotation==Quaternion.identity&&transform.localScale==Vector3.one,"preserve manually transformed rabbit batch");
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(MeadowRabbitSetup.MeshPath);
                Require(owner.MeadowRabbits.GetComponent<MeshFilter>().sharedMesh==mesh,"preserve custom rabbit mesh reference");
                expected=MeadowRabbitSetup.Prepare(MeadowRabbitSetup.ReadSource(),owner.RabbitHabitats);
                Require(SameShape(mesh,expected),"preserve manually edited rabbit shape/channels");
                // Preserve manually moved habitats too, before calculating any new distribution.
                var old=MeadowRabbitSetup.Plan(terrain);
                Require(old.Select((h,i)=>h.Track.SequenceEqual(owner.RabbitHabitats[i].Track)).All(v=>v),"preserve manually edited rabbit tracks");
                var habitats=Plan(terrain);prepared=MeadowRabbitSetup.Prepare(MeadowRabbitSetup.ReadSource(),habitats);
                EditorUtility.CopySerialized(prepared,mesh);owner.RabbitHabitats=habitats;EditorUtility.SetDirty(mesh);
                PrefabUtility.SaveAsPrefabAsset(root,TerrainSetup.PrefabPath);AssetDatabase.SaveAssets();importer.userData=Marker;importer.SaveAndReimport();
                foreach(var path in new[]{TerrainSetup.PrefabPath,MeadowRabbitSetup.MeshPath})File.WriteAllText(path,string.Join("\n",File.ReadAllLines(path).Select(l=>l.TrimEnd()))+"\n");
                Debug.Log("Meadow ecology composed: four rabbit strips near existing flower/butterfly pockets; original mesh GUID, materials, world art and collision preserved.");
            }
            finally{if(expected!=null)Object.DestroyImmediate(expected);if(prepared!=null)Object.DestroyImmediate(prepared);PrefabUtility.UnloadPrefabContents(root);}
        }
    }
}
