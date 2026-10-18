using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MagicIsland.Interaction;
using MagicIsland.TerrainSystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    // Explicit authoring only: retain the existing walkable world and original tree trunks.
    public static class GrasslandCompositionSetup
    {
        public const string Group="Grassland route composition";
        public const string Source="SourceArt/GrasslandComposition/GrasslandComposition.meshdata.json";
        public const string Folder="Assets/Art/Meshes/Terrain/StarterWorld";
        public const float Radius=1.05f;
        public static readonly Vector2[] Route={new Vector2(0,185),new Vector2(0,240),new Vector2(-150,370),new Vector2(-100,520),new Vector2(-100,635)};
        public sealed class Edit {public Mesh Mesh;public Vector3[] Vertices,Normals;public Color[] Colors;public Vector2[] Stamp;}
        static void Require(bool ok,string why){if(!ok)throw new InvalidOperationException("Grassland composition: "+why);}
        static float Distance(Vector2 p)=>Enumerable.Range(0,Route.Length-1).Min(i=>(float)StarterIslandLayout.SegmentDistance(p.x,p.y,Route[i].x,Route[i].y,Route[i+1].x,Route[i+1].y));
        public static bool Clear(Vector2 p)
        {
            if(!BiomePolishSetup.Clear(p,Radius)||StarterIslandLayout.OriginalRegion(p.x,p.y)!=0)return false;
            for(int x=Mathf.RoundToInt(p.x-Radius);x<=Mathf.RoundToInt(p.x+Radius);x++)
                for(int z=Mathf.RoundToInt(p.y-Radius);z<=Mathf.RoundToInt(p.y+Radius);z++)if(StarterIslandLayout.CanPlace(x,z))return false;
            return new[]{new Vector2(4.2f,205),new Vector2(-3.5f,235),new Vector2(-145.8f,371),new Vector2(-95.5f,610)}.All(q=>Vector2.Distance(p,q)>2.7f);
        }
        public static Vector2[] Sites()
        {
            var points=new List<Vector2>();
            for(int segment=0;segment<Route.Length-1;segment++)
            {
                var a=Route[segment];var b=Route[segment+1];var d=(b-a).normalized;var side=new Vector2(-d.y,d.x);
                int count=Mathf.FloorToInt(Vector2.Distance(a,b)/22);
                for(int i=0;i<count;i++)
                {
                    float t=(i+.46f+(i%3)*.13f)/count;int sign=(i+segment)%2==0?1:-1;
                    var p=Vector2.Lerp(a,b,t)+side*(sign*(4.08f+(i%2)*.04f));
                    if(Clear(p))points.Add(p);
                    // A short companion pocket at selected bends; deliberately avoid two parallel rows.
                    var q=p+d*4.8f;if(i%3==1&&Clear(q))points.Add(q);
                }
            }
            Require(points.Count>=12,"insufficient reserved shoulder pockets: "+points.Count);return points.ToArray();
        }
        public static Edit[] PlanCrowns(IslandTerrain terrain)
        {
            var edits=new List<Edit>();
            foreach(var filter in terrain.World.Scenery.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.name.StartsWith("Landscape ",StringComparison.Ordinal)))
            {
                var mesh=filter.sharedMesh;var source=mesh.vertices;var v=(Vector3[])source.Clone();var colors=mesh.colors;
                var stamp=new List<Vector2>();mesh.GetUVs(6,stamp);
                if(!source.Any(p=>Distance(new Vector2(p.x,p.z))<32&&p.z>205&&p.z<610))continue;
                if(stamp.Count>0){Require(stamp.Count==v.Length&&stamp.All(s=>s.y==-742&&(s.x==0||s.x==1)),"custom UV6 on "+mesh.name);continue;}
                bool[] changed=new bool[v.Length];
                Require(colors.Length==v.Length&&mesh.normals.Length==v.Length,"authored crown channels");
                foreach(var component in BiomePolishSetup.Components(mesh))
                {
                    var bounds=new Bounds(source[component[0]],Vector3.zero);foreach(int i in component)bounds.Encapsulate(source[i]);
                    var center=bounds.center;float distance=Distance(new Vector2(center.x,center.z));
                    if(distance>29||center.z<215||center.z>600||bounds.size.y<2||component.Length!=70)continue;
                    if(component.Any(i=>colors[i].g-colors[i].r<.065f||DesertFrontSetup.Mask(source[i].x,source[i].z)>0||SnowFrontSetup.Mask(source[i].x,source[i].z)>0||ForestEdgeSetup.Mask(source[i].x,source[i].z)>0))continue;
                    float w=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(18,29,distance));
                    float phase=.5f+.5f*Mathf.Sin(center.z*.065f+center.x*.017f);
                    var scale=Vector3.Lerp(Vector3.one,new Vector3(.83f+phase*.14f,1.03f+phase*.10f,.97f-phase*.13f),w);
                    foreach(int i in component)
                    {
                        v[i]=center+Vector3.Scale(source[i]-center,scale);
                        float cap=Mathf.InverseLerp(bounds.min.y,bounds.max.y,source[i].y);
                        var tint=Color.Lerp(new Color(.41f,.59f,.31f),new Color(.68f,.77f,.41f),cap);
                        tint.a=colors[i].a;colors[i]=Color.Lerp(colors[i],tint,.22f*w);changed[i]=true;
                    }
                }
                if(!changed.Any(c=>c))continue;
                var temp=Object.Instantiate(mesh);Vector3[] normals=mesh.normals;
                try{temp.vertices=v;temp.RecalculateNormals();var n=temp.normals;for(int i=0;i<n.Length;i++)if(changed[i])normals[i]=n[i];}
                finally{Object.DestroyImmediate(temp);}
                edits.Add(new Edit{Mesh=mesh,Vertices=v,Normals=normals,Colors=colors,Stamp=changed.Select(c=>new Vector2(c?1:0,-742)).ToArray()});
            }
            return edits.ToArray();
        }
        sealed class Batch
        {
            public readonly List<Vector3> V=new List<Vector3>(),N=new List<Vector3>();public readonly List<Color> C=new List<Color>();public readonly List<int> T=new List<int>();
            public Mesh Mesh(string name){var m=new Mesh{name=name,indexFormat=V.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};m.SetVertices(V);m.SetNormals(N);m.SetColors(C);m.SetTriangles(T,0);m.RecalculateBounds();return m;}
        }
        public static Dictionary<string,Mesh> Prepare(IslandTerrain terrain)
        {
            Require(File.Exists(Source),"missing Blender source");var data=JsonUtility.FromJson<BeachCompositionSetup.Export>(File.ReadAllText(Source));
            Require(data!=null&&data.version==1&&data.templates!=null&&data.templates.Length==3,"source format");
            foreach(string name in new[]{"Rocks","Grass","Flowers"})Require(data.templates.Count(t=>t.name==name)==1,"unique template "+name);
            foreach(var t in data.templates)Require(t.positions.Length>0&&t.positions.Length%3==0&&t.normals.Length==t.positions.Length&&t.colors.Length==t.positions.Length/3*4
                &&t.positions.Concat(t.normals).Concat(t.colors).All(f=>!float.IsNaN(f)&&!float.IsInfinity(f))&&t.triangles.Length%3==0&&t.triangles.All(i=>i>=0&&i<t.positions.Length/3),"source channels");
            var batches=new Dictionary<string,Batch>();var sites=Sites();
            for(int site=0;site<sites.Length;site++)
            {
                var p=sites[site];string key="Grassland pocket "+Mathf.FloorToInt(p.x/128)+" "+Mathf.FloorToInt(p.y/128);
                if(!batches.TryGetValue(key,out var batch)){batch=new Batch();batches.Add(key,batch);}
                void Add(string name,float scale,Vector2 offset,float yaw)
                {
                    var t=data.templates.Single(a=>a.name==name);int first=batch.V.Count;var rotation=Quaternion.Euler(0,yaw,0);
                    for(int i=0;i<t.positions.Length/3;i++)
                    {
                        var q=rotation*(new Vector3(t.positions[i*3],t.positions[i*3+1],t.positions[i*3+2])*scale)+new Vector3(offset.x,0,offset.y);
                        Require(new Vector2(q.x,q.z).magnitude<=Radius,"whole model footprint");
                        var xz=p+new Vector2(q.x,q.z);var ray=new Ray(terrain.transform.TransformPoint(new Vector3(xz.x,300,xz.y)),Vector3.down);
                        Require(terrain.World.Raycast(ray,out var hit,350),"actual baked ground");var floor=terrain.transform.InverseTransformPoint(hit.point);
                        batch.V.Add(new Vector3(xz.x,floor.y+.015f+q.y,xz.y));batch.N.Add((rotation*new Vector3(t.normals[i*3],t.normals[i*3+1],t.normals[i*3+2])).normalized);
                        batch.C.Add(new Color(t.colors[i*4],t.colors[i*4+1],t.colors[i*4+2],1));
                    }
                    batch.T.AddRange(t.triangles.Select(i=>first+i));
                }
                float yaw=site*137.5f;
                Vector2 Offset(float x,float z){var q=Quaternion.Euler(0,yaw,0)*new Vector3(x,0,z);return new Vector2(q.x,q.z);}
                if(site%3==0)
                {
                    // Stems grow beside the stone, outside its entire horizontal envelope.
                    Add("Rocks",.68f,Offset(-.35f,0),yaw);
                    Add("Grass",.82f,Offset(-.08f,.40f),yaw+32);
                    Add("Flowers",.87f,Offset(.45f,-.12f),yaw-43);
                }
                else
                {
                    Add("Grass",1.15f,new Vector2(-.29f,.10f),yaw+32);
                    Add("Flowers",1.2f,new Vector2(.23f,-.18f),yaw-43);
                }
            }
            return batches.ToDictionary(k=>k.Key,k=>k.Value.Mesh(k.Key));
        }
        static void Clean(string path)=>File.WriteAllText(path,string.Join("\n",File.ReadAllLines(path).Select(s=>s.TrimEnd()))+"\n");
        [MenuItem("Magic Island/Art/Refine Grassland Route Composition (Explicit)")]
        public static void Rebuild()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling,"use Edit Mode");
            var root=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);Dictionary<string,Mesh> meshes=null;
            try
            {
                var terrain=root.GetComponent<IslandTerrain>();var existing=terrain.World.Scenery.Find(Group);
                if(existing!=null){var filters=existing.GetComponentsInChildren<MeshFilter>(true);Require(filters.Length>0&&filters.All(f=>f.sharedMesh!=null)&&existing.GetComponentsInChildren<Collider>(true).Length==0,"preserve incomplete/custom group");Debug.Log("Grassland composition and manual edits retained.");return;}
                var edits=PlanCrowns(terrain);meshes=Prepare(terrain);
                foreach(string key in meshes.Keys)Require(!File.Exists(Folder+"/"+key+".asset")&&!File.Exists(Folder+"/"+key+".asset.meta"),"preserve orphan/custom mesh "+key);
                var material=AssetDatabase.LoadAssetAtPath<Material>(BeachCompositionSetup.MaterialPath);Require(material!=null,"shared detail material");
                var group=new GameObject(Group);group.layer=2;group.transform.SetParent(terrain.World.Scenery,false);
                foreach(var entry in meshes)
                {
                    string path=Folder+"/"+entry.Key+".asset";AssetDatabase.CreateAsset(entry.Value,path);
                    var go=new GameObject(entry.Key,typeof(MeshFilter),typeof(MeshRenderer));go.layer=2;go.transform.SetParent(group.transform,false);
                    go.GetComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);go.GetComponent<MeshRenderer>().sharedMaterial=material;
                }
                foreach(var e in edits){e.Mesh.vertices=e.Vertices;e.Mesh.normals=e.Normals;e.Mesh.colors=e.Colors;e.Mesh.SetUVs(6,e.Stamp);e.Mesh.RecalculateBounds();EditorUtility.SetDirty(e.Mesh);}
                PrefabUtility.SaveAsPrefabAsset(root,TerrainSetup.PrefabPath);AssetDatabase.SaveAssets();
                foreach(var e in edits)Clean(AssetDatabase.GetAssetPath(e.Mesh));foreach(string key in meshes.Keys)Clean(Folder+"/"+key+".asset");Clean(TerrainSetup.PrefabPath);
                Debug.Log("Grassland composition baked: "+Sites().Length+" pockets, "+meshes.Count+" shared-material batches, "+edits.Length+" crown meshes; original trunks and ground retained.");
            }
            finally{if(meshes!=null)foreach(var m in meshes.Values)if(!AssetDatabase.Contains(m))Object.DestroyImmediate(m);PrefabUtility.UnloadPrefabContents(root);}
        }
    }
}
