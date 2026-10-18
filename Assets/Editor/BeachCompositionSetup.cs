using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MagicIsland.Configuration;
using MagicIsland.Interaction;
using MagicIsland.Ocean;
using MagicIsland.TerrainSystem;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    // Authored Blender templates become one AA mesh. Never called by ordinary Setup.
    public static class BeachCompositionSetup
    {
        public const string Group="Beach arrival composition";
        public const string MeshPath=CoastArtSetup.MeshFolder+"/Beach Arrival Composition.asset";
        public const string SourcePath="SourceArt/BeachComposition/BeachComposition.meshdata.json";
        public const string MaterialPath="Assets/Art/Materials/Terrain/Biome Details.mat";
        public const float Radius=.48f;
        public static readonly Vector2[] Sites={new Vector2(-1.27687f,-17.85201f),new Vector2(7.15319f,-16.53507f),
            new Vector2(11.89117f,-15.48171f),new Vector2(15.98996f,-10.48583f)};
        [Serializable] public sealed class Template{public string name;public float[] positions,normals,colors;public int[] triangles;}
        [Serializable] public sealed class Export{public int version;public Template[] templates;}
        static void Require(bool ok,string why){if(!ok)throw new InvalidOperationException("Beach composition: "+why);}
        static bool Finite(float f)=>!float.IsNaN(f)&&!float.IsInfinity(f);
        static void Clean(string path)=>File.WriteAllText(path,string.Join("\n",File.ReadAllLines(path).Select(s=>s.TrimEnd()))+"\n");

        public static Vector2 PickupPoint(int site,float jitter,int count)
        {
            double angle=(site+.5+jitter)*Math.PI*2/count*(1-.42/(Math.PI*2));if(angle>=5.60)angle+=.42;
            double radius=IslandLayout.CoastRadius(angle)-1.02;
            return new Vector2((float)(Math.Cos(angle)*radius),(float)(Math.Sin(angle)*radius));
        }
        public static void ValidateSites()
        {
            var catalog=AssetDatabase.LoadAssetAtPath<GatheringCatalog>("Assets/Config/GatheringCatalog.asset");Require(catalog!=null,"missing AA gathering configuration");
            int count=catalog.BeachDefinition().Sites;
            // Cover every seed's jitter interval, including a conservative margin between samples.
            float pickupRadius=0;
            foreach(string item in BeachPickup.Items)
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(BeachForageSetup.Folder+"/"+BeachPickup.Address(item)+".prefab");
                var box=prefab!=null?prefab.GetComponent<BoxCollider>():null;
                Require(box!=null&&box.center.x==0&&box.center.z==0&&prefab.transform.localScale==Vector3.one,"preserve custom pickup envelope");
                pickupRadius=Mathf.Max(pickupRadius,new Vector2(box.size.x,box.size.z).magnitude*.5f);
            }
            foreach(var p in Sites)
            {
                Require(Vector2.Distance(p,new Vector2(1,-14))>2.5f,"spawn clearance");
                Require(!(p.x>10&&Mathf.Abs(p.y-(float)IslandLayout.PierZ)<1.75f+Radius),"dock approach clearance");
                for(int x=0;x<=24;x++)for(int z=0;z<=24;z++)
                {
                    var q=p+new Vector2(Mathf.Lerp(-Radius,Radius,x/24f),Mathf.Lerp(-Radius,Radius,z/24f));
                    Require(!IslandLayout.CanPlace(Mathf.RoundToInt(q.x),Mathf.RoundToInt(q.y)),"preserve complete buildable cells at "+q);
                }
                for(int site=0;site<count;site++)for(int j=0;j<=256;j++)
                    Require(Vector2.Distance(p,PickupPoint(site,Mathf.Lerp(-.29f,.29f,j/256f),count))>Radius+pickupRadius+.05f,"preserve every seeded pickup footprint");
            }
        }
        public static Mesh Prepare(IslandTerrain terrain,MeshCollider shore)
        {
            ValidateSites();Require(File.Exists(SourcePath),"missing Blender export");
            var data=JsonUtility.FromJson<Export>(File.ReadAllText(SourcePath));
            Require(data!=null&&data.version==1&&data.templates!=null&&data.templates.Length==3,"source format");
            foreach(string name in new[]{"Rocks","Driftwood","Grass"})Require(data.templates.Count(t=>t!=null&&t.name==name)==1,"unique template "+name);
            foreach(var t in data.templates)
            {
                Require(t.positions!=null&&t.positions.Length>0&&t.positions.Length%3==0&&t.normals!=null&&t.normals.Length==t.positions.Length
                    &&t.colors!=null&&t.colors.Length==t.positions.Length/3*4&&t.triangles!=null&&t.triangles.Length%3==0,"template channels");
                Require(t.positions.Concat(t.normals).Concat(t.colors).All(Finite)&&t.colors.All(c=>c>=0&&c<=1)
                    &&t.triangles.All(i=>i>=0&&i<t.positions.Length/3),"finite template values/indices");
            }
            var v=new List<Vector3>();var normals=new List<Vector3>();var colors=new List<Color>();var triangles=new List<int>();
            var uv=new List<Vector2>();
            void Add(string name,int site,float scale,Vector3 offset,float yaw)
            {
                var t=data.templates.Single(a=>a.name==name);int first=v.Count;var rotate=Quaternion.Euler(0,yaw,0);var p=Sites[site];
                for(int i=0;i<t.positions.Length/3;i++)
                {
                    var q=rotate*(new Vector3(t.positions[i*3],t.positions[i*3+1],t.positions[i*3+2])*scale)+offset;
                    Require(new Vector2(q.x,q.z).magnitude<=Radius,"whole composition radius");
                    // Each base follows the existing baked shore; no ground or collision edits.
                    var basePoint=p+new Vector2(q.x,q.z);
                    var ray=new Ray(terrain.transform.TransformPoint(new Vector3(basePoint.x,300,basePoint.y)),Vector3.down);
                    bool found=terrain.Ground.Raycast(ray,out var hit,350);
                    if(shore.Raycast(ray,out var sand,350)&&(!found||sand.point.y>hit.point.y)){hit=sand;found=true;}
                    Require(found,"actual visible shore surface");
                    var floor=terrain.transform.InverseTransformPoint(hit.point);
                    v.Add(new Vector3(basePoint.x,floor.y+.008f+q.y,basePoint.y));
                    var n=rotate*new Vector3(t.normals[i*3],t.normals[i*3+1],t.normals[i*3+2]);Require(n.sqrMagnitude>.9f,"source normals");
                    var floorNormal=terrain.transform.InverseTransformDirection(hit.normal);
                    n=new Vector3(n.x+floorNormal.x/Mathf.Max(.1f,floorNormal.y)*n.y,n.y,n.z+floorNormal.z/Mathf.Max(.1f,floorNormal.y)*n.y);normals.Add(n.normalized);
                    colors.Add(new Color(t.colors[i*4],t.colors[i*4+1],t.colors[i*4+2],1));uv.Add(new Vector2(site,0));
                }
                triangles.AddRange(t.triangles.Select(i=>i+first));
            }
            for(int site=0;site<Sites.Length;site++)
            {
                float yaw=-Mathf.Atan2(Sites[site].y,Sites[site].x)*Mathf.Rad2Deg;
                Add(site%2==0?"Rocks":"Driftwood",site,site==2?.92f:1,Vector3.zero,yaw+24+site*19);
                var inward=-Sites[site].normalized;var offset=inward*.12f+new Vector2(-inward.y,inward.x)*.19f;
                Add("Grass",site,.9f,new Vector3(offset.x,0,offset.y),yaw+site*37);
            }
            var mesh=new Mesh{name="Beach Arrival Composition"};mesh.SetVertices(v);mesh.SetNormals(normals);mesh.SetColors(colors);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();
            return mesh;
        }
        [MenuItem("Magic Island/Art/Refine Beach Arrival Composition (Explicit)")]
        public static void Rebuild()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling,"use Edit Mode");
            var root=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);Mesh prepared=null;GameObject query=null;
            try
            {
                var group=root.transform.Find(Group);
                if(group!=null)
                {
                    ValidateSites();
                    var filters=group.GetComponentsInChildren<MeshFilter>(true);
                    Require(group.GetComponentsInChildren<Collider>(true).Length==0&&filters.Length>0&&filters.All(f=>f.sharedMesh!=null),"preserve incomplete/manual composition");
                    Debug.Log("Existing beach composition and manual edits retained.");return;
                }
                Require(!File.Exists(MeshPath)&&!File.Exists(MeshPath+".meta"),"preserve orphan/custom mesh");
                var material=AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);Require(material!=null,"shared detail material");
                var ocean=AssetDatabase.LoadAssetAtPath<GameObject>(OceanSetup.PrefabPath).GetComponent<OceanSurface>();
                Require(ocean!=null&&ocean.Shore!=null&&ocean.Shore.transform.localToWorldMatrix==Matrix4x4.identity,"authored shore coordinate space");
                // Outer wet sand belongs to the ocean's visible mesh, beyond the walkable collider.
                // This temporary editor query never enters a prefab or the runtime collision set.
                query=new GameObject("Beach source surface query"){hideFlags=HideFlags.HideAndDontSave};
                query.transform.SetPositionAndRotation(root.transform.position,root.transform.rotation);query.transform.localScale=root.transform.lossyScale;
                var shore=query.AddComponent<MeshCollider>();shore.sharedMesh=ocean.Shore.GetComponent<MeshFilter>().sharedMesh;
                prepared=Prepare(root.GetComponent<IslandTerrain>(),shore);Object.DestroyImmediate(query);query=null;
                AssetDatabase.CreateAsset(prepared,MeshPath);prepared=null;
                var go=new GameObject(Group,typeof(MeshFilter),typeof(MeshRenderer));go.layer=2;go.transform.SetParent(root.transform,false);
                go.GetComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);go.GetComponent<MeshRenderer>().sharedMaterial=material;
                PrefabUtility.SaveAsPrefabAsset(root,TerrainSetup.PrefabPath);AssetDatabase.SaveAssets();Clean(MeshPath);Clean(TerrainSetup.PrefabPath);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                Debug.Log("Beach composition: four shore pockets, one combined mesh, shared material; original terrain and scenery preserved.");
            }
            finally{if(query!=null)Object.DestroyImmediate(query);if(prepared!=null)Object.DestroyImmediate(prepared);PrefabUtility.UnloadPrefabContents(root);}
        }
    }
}
