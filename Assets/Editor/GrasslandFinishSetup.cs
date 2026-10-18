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
    // Explicit finishing pass; geography, physical trunks and construction eligibility remain authoritative.
    public static class GrasslandFinishSetup
    {
        public const string Group="Grassland meadow finish";
        public const float Marker=-744,Radius=1.05f;
        public sealed class Edit
        {
            public Mesh Mesh;public Vector3[] Vertices,Normals;public Color[] Colors;public int[] Triangles;
            public Vector2[] Stamp;public bool Ground,Verge;public bool[] Changed;
        }
        public sealed class Site{public Vector2 Point;public int Kind;public float Yaw;}
        static void Require(bool ok,string why){if(!ok)throw new InvalidOperationException("Grassland finish: "+why);}
        static float Smooth(float a,float b,float v)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,v));
        public static float Mask(float x,float z)
        {
            if(!StarterIslandLayout.Contains(x,z)||DesertFrontSetup.Mask(x,z)>0||SnowFrontSetup.Mask(x,z)>0||ForestEdgeSetup.Mask(x,z)>0)return 0;
            float w=Smooth(185,235,z)*(1-Smooth(785,840,z))*Smooth(16,45,(float)StarterIslandLayout.CoastDistance(x,z))
                *Smooth(2,20,(float)StarterIslandLayout.LakeDistance(x,z))*Smooth(6,25,(float)StarterIslandLayout.OutletDistance(x,z));
            w*=1-Smooth(.001f,.25f,(float)StarterIslandLayout.DesertWeight(x,z));
            w*=1-Smooth(.001f,.25f,(float)StarterIslandLayout.ForestWeight(x,z));
            w*=1-Smooth(.001f,.25f,(float)StarterIslandLayout.SnowWeight(x,z));
            // Feather to the already authored approaches without repainting them.
            float west=Smooth(275,300,z)*(1-Smooth(790,815,z));w*=1-west*(1-Smooth(-150,-115,x));
            w*=1-Smooth(20,40,x)*Smooth(715,740,z);
            foreach(var p in new[]{new Vector2(-8,700),new Vector2(480,650),new Vector2(-420,590),new Vector2(-40,1000)})w*=Smooth(60,82,Vector2.Distance(new Vector2(x,z),p));
            return w;
        }
        static bool Done(Mesh mesh)
        {
            var uv=new List<Vector2>();mesh.GetUVs(7,uv);if(uv.Count==0)return false;
            Require(uv.Count==mesh.vertexCount&&uv.All(p=>p.y==Marker&&(p.x==0||p.x==1)),"preserve custom UV7: "+mesh.name);return true;
        }
        static bool Leaf(Color c)=>c.g-c.r>.065f&&c.g-c.b>.05f;
        static float Field(float x,float z)=>Mathf.PerlinNoise(x*.017f+26,z*.014f+57);
        public static Edit[] Plan(IslandTerrain terrain)
        {
            var edits=new List<Edit>();var anchors=GrasslandCompositionSetup.Sites().Concat(new[]{new Vector2(4.2f,205),new Vector2(-3.5f,235),new Vector2(-145.8f,371),new Vector2(-95.5f,610)}).ToArray();
            foreach(var f in terrain.World.Scenery.parent.GetComponentsInChildren<MeshFilter>(true))
            {
                bool ground=f.name.StartsWith("Land ",StringComparison.Ordinal),landscape=f.name.StartsWith("Landscape ",StringComparison.Ordinal),verge=f.name.StartsWith("Route verge ",StringComparison.Ordinal);
                if(!ground&&!landscape&&!verge)continue;var mesh=f.sharedMesh;var source=mesh.vertices;
                if(!source.Any(p=>Mask(p.x,p.z)>.001f)||Done(mesh))continue;
                var vertices=(Vector3[])source.Clone();var normals=mesh.normals;var colors=mesh.colors;var changed=new bool[source.Length];var triangles=mesh.triangles;
                Require(colors.Length==source.Length&&normals.Length==source.Length,"authored channels: "+mesh.name);
                if(ground)
                {
                    for(int i=0;i<source.Length;i++)
                    {
                        var p=source[i];float mask=Mask(p.x,p.z);if(mask<=.001f)continue;
                        float patch=Smooth(.32f,.69f,Field(p.x,p.z));
                        float damp=1-Smooth(22,95,(float)StarterIslandLayout.LakeDistance(p.x,p.z));
                        float grove=Smooth(.48f,.72f,Mathf.PerlinNoise(p.x*.009f+7,p.z*.013f+33));
                        var target=Color.Lerp(new Color(.48f,.64f,.35f),new Color(.73f,.79f,.48f),patch);
                        target=Color.Lerp(target,new Color(.43f,.62f,.42f),damp*.66f+grove*.12f);
                        target.a=colors[i].a;colors[i]=Color.Lerp(colors[i],target,mask*.55f);changed[i]=true;
                    }
                }
                else if(landscape)
                {
                    var previous=new List<Vector2>();mesh.GetUVs(6,previous);
                    foreach(var component in BiomePolishSetup.Components(mesh))
                    {
                        if(component.Length!=70||component.Any(i=>Mask(source[i].x,source[i].z)<=.01f||!Leaf(colors[i])||previous.Count==source.Length&&previous[i].y==-742&&previous[i].x==1))continue;
                        var bounds=new Bounds(source[component[0]],Vector3.zero);foreach(int i in component)bounds.Encapsulate(source[i]);
                        if(bounds.size.y<2)continue;var center=bounds.center;float w=component.Min(i=>Mask(source[i].x,source[i].z));
                        float phase=Field(center.x+83,center.z+37);var scale=Vector3.Lerp(Vector3.one,new Vector3(.82f+phase*.16f,.93f+phase*.15f,.98f-phase*.14f),w);
                        foreach(int i in component)
                        {
                            vertices[i]=center+Vector3.Scale(source[i]-center,scale);
                            float cap=Mathf.InverseLerp(bounds.min.y,bounds.max.y,source[i].y);
                            var tint=Color.Lerp(new Color(.31f,.49f,.29f),new Color(.65f,.75f,.39f),cap*.8f+phase*.2f);tint.a=colors[i].a;
                            colors[i]=Color.Lerp(colors[i],tint,w*.32f);changed[i]=true;
                        }
                    }
                    if(changed.Any(b=>b))
                    {
                        var temp=Object.Instantiate(mesh);
                        try{temp.vertices=vertices;temp.RecalculateNormals();var n=temp.normals;for(int i=0;i<n.Length;i++)if(changed[i])normals[i]=n[i];}
                        finally{Object.DestroyImmediate(temp);}
                    }
                }
                else
                {
                    // Generated flowers have disconnected petals/pollen: make one decision for the entire plant.
                    var petals=new[]{new Color(.96f,.89f,.63f),new Color(.88f,.87f,.77f),new Color(.70f,.77f,.97f)};
                    var components=BiomePolishSetup.Components(mesh);var byFirst=components.ToDictionary(a=>a.Min());
                    bool Remove(int[] indices,Vector3 root)
                    {
                        var p=new Vector2(root.x,root.z);
                        return indices.All(i=>Mask(source[i].x,source[i].z)>.01f)&&!anchors.Any(a=>Vector2.Distance(a,p)<5.8f)&&Field(p.x*2.1f,p.y*2.1f)<=.67f;
                    }
                    bool Part(int first,int count,Color color)=>byFirst.TryGetValue(first,out var component)&&component.Length==count
                        &&component.OrderBy(i=>i).SequenceEqual(Enumerable.Range(first,count))&&component.All(i=>colors[i]==color);
                    foreach(var component in components)
                    {
                        int first=component.Min();
                        if(Part(first,4,new Color(.44f,.63f,.32f)))
                        {
                            var center=component.Aggregate(Vector3.zero,(a,i)=>a+source[i])/component.Length;
                            if(Remove(component,center))foreach(int i in component)changed[i]=true;
                        }
                        else if(Part(first,22,new Color(.39f,.57f,.30f))&&first+112<=source.Length)
                        {
                            var color=colors[first+22];if(!petals.Contains(color))continue;
                            if(!Enumerable.Range(0,5).All(k=>Part(first+22+k*4,4,color)&&source[first+22+k*4]==source[first+1])
                                ||!Part(first+42,70,new Color(.95f,.75f,.34f)))continue;
                            var flower=Enumerable.Range(first,112).ToArray();
                            if(Remove(flower,source[first]))foreach(int i in flower)changed[i]=true;
                        }
                    }
                    var kept=new List<int>();for(int i=0;i<triangles.Length;i+=3)if(!changed[triangles[i]])kept.AddRange(new[]{triangles[i],triangles[i+1],triangles[i+2]});triangles=kept.ToArray();
                }
                if(!changed.Any(b=>b))continue;
                edits.Add(new Edit{Mesh=mesh,Vertices=vertices,Normals=normals,Colors=colors,Triangles=triangles,Ground=ground,Verge=verge,Changed=changed,Stamp=changed.Select(b=>new Vector2(b?1:0,Marker)).ToArray()});
            }
            return edits.ToArray();
        }
        public static bool Clear(Vector2 p)
        {
            if(Mask(p.x,p.y)<.35f||!BiomePolishSetup.Clear(p,Radius))return false;
            for(int x=Mathf.RoundToInt(p.x-Radius);x<=Mathf.RoundToInt(p.x+Radius);x++)for(int z=Mathf.RoundToInt(p.y-Radius);z<=Mathf.RoundToInt(p.y+Radius);z++)if(StarterIslandLayout.CanPlace(x,z))return false;
            return GrasslandCompositionSetup.Sites().All(a=>Vector2.Distance(a,p)>6);
        }
        public static Site[] Sites()
        {
            var sites=new List<Site>();
            var segments=new[]{new[]{new Vector2(0,240),new Vector2(300,340)},new[]{new Vector2(300,340),new Vector2(350,363)},new[]{new Vector2(-100,642),new Vector2(-100,690)}};
            for(int segment=0;segment<segments.Length;segment++)
            {
                var a=segments[segment][0];var b=segments[segment][1];var d=(b-a).normalized;var side=new Vector2(-d.y,d.x);float length=Vector2.Distance(a,b);
                for(float along=13;along<length-9;along+=19.7f)
                {
                    int n=sites.Count;var p=a+d*along+side*(n%2==0?4.08f:-4.08f);if(!Clear(p))continue;
                    sites.Add(new Site{Point=p,Kind=n%3,Yaw=n*137.5f+segment*27});
                }
            }
            Require(sites.Count>=10,"insufficient existing reserved shoulder sites");return sites.ToArray();
        }
        sealed class Batch
        {
            public readonly List<Vector3> V=new List<Vector3>(),N=new List<Vector3>();public readonly List<Color> C=new List<Color>();public readonly List<int> T=new List<int>();
            public Mesh Mesh(string name){var m=new Mesh{name=name,indexFormat=V.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};m.SetVertices(V);m.SetNormals(N);m.SetColors(C);m.SetTriangles(T,0);m.RecalculateBounds();return m;}
        }
        public static Dictionary<string,Mesh> Prepare(IslandTerrain terrain)
        {
            var data=JsonUtility.FromJson<BeachCompositionSetup.Export>(File.ReadAllText(GrasslandCompositionSetup.Source));
            Require(data!=null&&data.version==1&&data.templates!=null,"Blender templates");
            var batches=new Dictionary<string,Batch>();
            foreach(var site in Sites())
            {
                var p=site.Point;string key="Meadow finish "+Mathf.FloorToInt(p.x/128)+" "+Mathf.FloorToInt(p.y/128);
                if(!batches.TryGetValue(key,out var batch)){batch=new Batch();batches.Add(key,batch);}
                Vector2 Offset(float x,float z){var q=Quaternion.Euler(0,site.Yaw,0)*new Vector3(x,0,z);return new Vector2(q.x,q.z);}
                void Add(string name,float scale,Vector2 offset,float yaw)
                {
                    var t=data.templates.Single(a=>a.name==name);Require(t.positions.Length>0&&t.positions.Length%3==0&&t.normals.Length==t.positions.Length&&t.colors.Length==t.positions.Length/3*4
                        &&t.positions.Concat(t.normals).Concat(t.colors).All(f=>!float.IsNaN(f)&&!float.IsInfinity(f))&&t.triangles.Length%3==0&&t.triangles.All(i=>i>=0&&i<t.positions.Length/3),"template channels");
                    int first=batch.V.Count;var rotate=Quaternion.Euler(0,yaw,0);
                    for(int i=0;i<t.positions.Length/3;i++)
                    {
                        var q=rotate*(new Vector3(t.positions[i*3],t.positions[i*3+1],t.positions[i*3+2])*scale)+new Vector3(offset.x,0,offset.y);Require(new Vector2(q.x,q.z).magnitude<=Radius,"complete model envelope");
                        var xz=p+new Vector2(q.x,q.z);var ray=new Ray(terrain.transform.TransformPoint(new Vector3(xz.x,300,xz.y)),Vector3.down);
                        Require(terrain.World.Raycast(ray,out var hit,350),"actual baked ground");var floor=terrain.transform.InverseTransformPoint(hit.point);
                        batch.V.Add(new Vector3(xz.x,floor.y+.015f+q.y,xz.y));batch.N.Add((rotate*new Vector3(t.normals[i*3],t.normals[i*3+1],t.normals[i*3+2])).normalized);
                        batch.C.Add(new Color(t.colors[i*4],t.colors[i*4+1],t.colors[i*4+2],1));
                    }
                    batch.T.AddRange(t.triangles.Select(i=>first+i));
                }
                if(site.Kind==0)Add("Rocks",.68f,Offset(-.35f,0),site.Yaw);
                Add("Grass",site.Kind==2?1.3f:.82f,Offset(-.08f,.40f),site.Yaw+32);
                if(site.Kind!=2)Add("Flowers",.87f,Offset(.45f,-.12f),site.Yaw-43);
            }
            return batches.ToDictionary(p=>p.Key,p=>p.Value.Mesh(p.Key));
        }
        static void Clean(string path)=>File.WriteAllText(path,string.Join("\n",File.ReadAllLines(path).Select(s=>s.TrimEnd()))+"\n");
        [MenuItem("Magic Island/Art/Finish Grassland Biome (Explicit)")]
        public static void Rebuild()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling,"use Edit Mode");
            var root=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);Dictionary<string,Mesh> meshes=null;
            try
            {
                var terrain=root.GetComponent<IslandTerrain>();var existing=terrain.World.Scenery.Find(Group);
                if(existing!=null){var filters=existing.GetComponentsInChildren<MeshFilter>(true);Require(filters.Length>0&&filters.All(f=>f.sharedMesh!=null)&&existing.GetComponentsInChildren<Collider>(true).Length==0,"preserve incomplete/custom finish");Debug.Log("Grassland finish and manual edits retained.");return;}
                var edits=Plan(terrain);meshes=Prepare(terrain);var material=AssetDatabase.LoadAssetAtPath<Material>(BeachCompositionSetup.MaterialPath);Require(material!=null,"shared material");
                foreach(string key in meshes.Keys)Require(!File.Exists(GrasslandCompositionSetup.Folder+"/"+key+".asset")&&!File.Exists(GrasslandCompositionSetup.Folder+"/"+key+".asset.meta"),"preserve orphan mesh "+key);
                var group=new GameObject(Group);group.layer=2;group.transform.SetParent(terrain.World.Scenery,false);
                foreach(var pair in meshes)
                {
                    string path=GrasslandCompositionSetup.Folder+"/"+pair.Key+".asset";AssetDatabase.CreateAsset(pair.Value,path);
                    var go=new GameObject(pair.Key,typeof(MeshFilter),typeof(MeshRenderer));go.layer=2;go.transform.SetParent(group.transform,false);go.GetComponent<MeshFilter>().sharedMesh=pair.Value;go.GetComponent<MeshRenderer>().sharedMaterial=material;
                }
                foreach(var e in edits)
                {
                    if(!e.Ground&&!e.Verge){e.Mesh.vertices=e.Vertices;e.Mesh.normals=e.Normals;e.Mesh.RecalculateBounds();}
                    e.Mesh.colors=e.Colors;if(e.Verge)e.Mesh.SetTriangles(e.Triangles,0,false);e.Mesh.SetUVs(7,e.Stamp);EditorUtility.SetDirty(e.Mesh);
                }
                PrefabUtility.SaveAsPrefabAsset(root,TerrainSetup.PrefabPath);AssetDatabase.SaveAssets();Clean(TerrainSetup.PrefabPath);
                foreach(var e in edits)Clean(AssetDatabase.GetAssetPath(e.Mesh));foreach(string key in meshes.Keys)Clean(GrasslandCompositionSetup.Folder+"/"+key+".asset");
                Debug.Log("Grassland finish baked: "+edits.Length+" meshes, "+Sites().Length+" pockets / "+meshes.Count+" batches.");
            }
            finally{if(meshes!=null)foreach(var m in meshes.Values)if(!AssetDatabase.Contains(m))Object.DestroyImmediate(m);PrefabUtility.UnloadPrefabContents(root);}
        }
    }
}
