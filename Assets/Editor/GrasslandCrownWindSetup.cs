using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MagicIsland.Interaction;
using MagicIsland.TerrainSystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MagicIsland.EditorTools
{
    public static class GrasslandCrownWindSetup
    {
        public const string Marker="MagicIsland.GrasslandCrownWind.v1";
        public const string Required=GrasslandGroveSetup.Marker+";"+GrasslandCrownSetup.Marker;
        public sealed class Edit{public Mesh Mesh;public Vector4[] UV;public bool[] Leaves;public int Lobes;}
        public static bool Done(string metadata)=>metadata.Split(';').Contains(Marker);
        public static void Check(bool ok,string why){if(!ok)throw new InvalidOperationException("Grassland crown wind: "+why);}
        public static MeshFilter[] Filters(IslandTerrain t)=>t.World.Scenery.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.name.StartsWith("Landscape ",StringComparison.Ordinal))
            .Where(f=>{var a=AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(f.sharedMesh));return a!=null&&a.userData.Split(';').Contains(GrasslandCrownSetup.Marker);}).ToArray();
        static Bounds Box(int[] part,Vector3[] v){var b=new Bounds(v[part[0]],Vector3.zero);foreach(int i in part)b.Encapsulate(v[i]);return b;}
        public static Edit[] Plan(IslandTerrain t)
        {
            var result=new List<Edit>();var collisions=new HashSet<Mesh>(t.GetComponentsInChildren<MeshCollider>(true).Select(c=>c.sharedMesh));
            // Inspect all landscapes too, so replacing required metadata cannot silently omit a mesh.
            foreach(var f in t.World.Scenery.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.name.StartsWith("Landscape ",StringComparison.Ordinal)))
            {
                var m=f.sharedMesh;var v=m.vertices;var c=m.colors;var parts=BiomePolishSetup.Components(m);
                var lobes=parts.Where(p=>p.Length==260&&p.All(i=>c[i].g-c[i].r>.065f)).ToArray();if(lobes.Length==0)continue;
                string path=AssetDatabase.GetAssetPath(m);var importer=AssetImporter.GetAtPath(path);Check(importer!=null&&importer.userData==Required,"preserve unknown/incomplete crown metadata "+path);
                Check(path.StartsWith(BiomeIslandsSetup.Folder+"/Art Landscape ",StringComparison.Ordinal)&&!collisions.Contains(m)&&f.transform.localToWorldMatrix==t.transform.localToWorldMatrix,"original visual-only ownership and transform");
                var renderer=f.GetComponent<Renderer>();Check(renderer!=null&&renderer.sharedMaterials.Length==1&&renderer.sharedMaterial.shader.name=="MagicIsland/Island Biome Details","original shared detail material");
                Check(m.GetVertexAttributeDimension(VertexAttribute.TexCoord0)==4&&m.subMeshCount==1,"existing four-component UV0 and static mesh");
                var uv=new List<Vector4>();m.GetUVs(0,uv);Check(uv.Count==v.Length,"complete existing UV0");
                var trunks=parts.Where(p=>p.Length==22&&p.All(i=>c[i].r>c[i].g+.04f)).Select(p=>Box(p,v)).Where(b=>b.size.y>4&&b.size.x<1.1f&&b.size.z<1.1f).ToArray();Check(trunks.Length>0,"original stationary trunks");
                var anchors=lobes.Select(p=>trunks.OrderBy(b=>(new Vector2(b.center.x,b.center.z)-new Vector2(Box(p,v).center.x,Box(p,v).center.z)).sqrMagnitude).First()).ToArray();
                var edit=new Edit{Mesh=m,UV=uv.ToArray(),Leaves=new bool[v.Length],Lobes=lobes.Length};
                for(int l=0;l<lobes.Length;l++)
                {
                    var b=Box(lobes[l],v);var trunk=anchors[l];var root=new Vector3(trunk.center.x,trunk.min.y,trunk.center.z);
                    float height=Enumerable.Range(0,lobes.Length).Where(i=>anchors[i]==trunk).Max(i=>Box(lobes[i],v).max.y)-root.y;Check(height>4&&height<20,"safe original tree height");
                    Check(new Vector2(b.center.x-root.x,b.center.z-root.z).magnitude<height&&lobes[l].All(i=>BiomeIslandLayout.At(v[i].x,v[i].z)==IslandBiome.Grassland),"known grassland crown and trunk association");
                    float tree=Mathf.Repeat(root.x*.71f+root.z*.43f+root.y*.29f,Mathf.PI*2),leaf=Mathf.Repeat(b.center.x*1.19f+b.center.z*.83f+b.center.y*.61f,Mathf.PI*2);
                    foreach(int i in lobes[l])
                    {
                        Check(uv[i]==Vector4.zero,"preserve artist UV0 on refined leaves");float weight=Mathf.Clamp01((v[i].y-root.y)/height);
                        edit.UV[i]=new Vector4(-2-weight,tree,leaf,1/height);edit.Leaves[i]=true;
                    }
                }
                result.Add(edit);
            }
            Check(result.Count>0&&result.Count==Filters(t).Length,"all refined crown meshes planned before writes");return result.ToArray();
        }
        static void Clean(string p)=>File.WriteAllText(p,string.Join("\n",File.ReadAllLines(p).Select(l=>l.TrimEnd()))+"\n");
        [MenuItem("Magic Island/Art/Prepare Grassland Crown Wind (Explicit)")]
        public static void Rebuild()
        {
            Check(!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling,"Edit Mode required");var root=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);var backup=new Dictionary<string,byte[]>();bool saved=false;
            try
            {
                var t=root.GetComponent<IslandTerrain>();Check(t.IsValid&&t.World.ArtVersion==3,"authored islands");var filters=Filters(t);Check(filters.Length>0,"refined crown meshes required");var done=filters.Select(f=>Done(AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(f.sharedMesh)).userData)).ToArray();
                if(done.Any(v=>v)){Check(done.All(v=>v),"preserve incomplete migration");Debug.Log("Grassland crown wind and later hand edits retained.");return;}
                var plan=Plan(t);foreach(string p in plan.Select(e=>AssetDatabase.GetAssetPath(e.Mesh)).SelectMany(p=>new[]{p,p+".meta"}))backup.Add(p,File.ReadAllBytes(p));
                foreach(var e in plan){e.Mesh.SetUVs(0,e.UV.ToList());var bounds=e.Mesh.bounds;bounds.Expand(.65f);e.Mesh.bounds=bounds;EditorUtility.SetDirty(e.Mesh);}AssetDatabase.SaveAssets();
                foreach(var e in plan){string p=AssetDatabase.GetAssetPath(e.Mesh);var importer=AssetImporter.GetAtPath(p);importer.userData=Required+";"+Marker;importer.SaveAndReimport();Clean(p);Clean(p+".meta");}saved=true;
                Directory.CreateDirectory("Logs/GrasslandCrownWind");File.WriteAllText("Logs/GrasslandCrownWind/Stats.json",JsonUtility.ToJson(new Report{Meshes=plan.Length,Lobes=plan.Sum(e=>e.Lobes),Vertices=plan.Sum(e=>e.Leaves.Count(v=>v))}));
                Debug.Log("Grassland crown wind baked: "+plan.Sum(e=>e.Lobes)+" original leaf clusters, existing UV0 only; no geometry, material, prefab or collision changes.");
            }
            finally{PrefabUtility.UnloadPrefabContents(root);if(!saved){foreach(var p in backup)File.WriteAllBytes(p.Key,p.Value);foreach(string p in backup.Keys.Where(p=>!p.EndsWith(".meta",StringComparison.Ordinal)))AssetDatabase.ImportAsset(p,ImportAssetOptions.ForceUpdate);}}
        }
        [Serializable]sealed class Report{public int Meshes,Lobes,Vertices;}
    }
}
