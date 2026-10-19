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
    // Four ordered, explicit art steps. All authoring plans precede writes; no extra original UV channel.
    public static class GrasslandRevitalizationSetup
    {
        public const string Group="Grassland living meadow",Marker="MagicIsland.GrasslandLiving.v1";
        public const string MapPath="Assets/Art/Textures/Terrain/MeadowField.png";
        public const string MaterialFolder="Assets/Art/Materials/Terrain/";
        public static readonly Vector4 Rect=new Vector4(-180,180,660,680);
        public sealed class Site {public Vector2 Point;public float Phase;public int Kind;}
        public sealed class Edit {public Mesh Mesh;public Vector3[] Vertices,Normals;public Color[] Colors;public int[] Triangles;public bool[] Changed;}
        public static void Check(bool ok,string why){if(!ok)throw new InvalidOperationException("Living grassland: "+why);}
        public static float Mask(float x,float z)=>GrasslandFinishSetup.Mask(x,z);
        static float Field(float x,float z)=>Mathf.PerlinNoise(x*.031f+62,z*.025f+19);
        public static Edit[] Plan(IslandTerrain terrain)
        {
            var edits=new List<Edit>();
            var homes=GrasslandEcologySetup.Sites();
            foreach(var f in terrain.World.Scenery.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.name.StartsWith("Landscape ",StringComparison.Ordinal)||f.name.StartsWith("Route verge ",StringComparison.Ordinal)))
            {
                var mesh=f.sharedMesh;var source=mesh.vertices;if(!source.Any(p=>Mask(p.x,p.z)>.05f))continue;
                var importer=AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(mesh));
                if(importer.userData.StartsWith(Marker,StringComparison.Ordinal))continue;
                Check(string.IsNullOrEmpty(importer.userData)||importer.userData==DesertCompositionSetup.Marker,"preserve custom importer metadata "+mesh.name);
                var v=(Vector3[])source.Clone();var n=mesh.normals;var c=mesh.colors;var changed=new bool[v.Length];var triangles=mesh.triangles;
                Check(c.Length==v.Length&&n.Length==v.Length,"original authored channels");
                var parts=BiomePolishSetup.Components(mesh);
                if(f.name.StartsWith("Landscape ",StringComparison.Ordinal))
                {
                    foreach(var part in parts)
                    {
                        if(part.Length!=70||part.Any(i=>Mask(source[i].x,source[i].z)<.25f||c[i].g-c[i].r<.065f))continue;
                        var box=new Bounds(source[part[0]],Vector3.zero);foreach(int i in part)box.Encapsulate(source[i]);
                        if(box.size.y<2)continue;float phase=Field(box.center.x,box.center.z);
                        foreach(int i in part)
                        {
                            var d=source[i]-box.center;float cap=Mathf.InverseLerp(box.min.y,box.max.y,source[i].y);
                            float w=Mask(source[i].x,source[i].z);float pinch=1-w*(.025f+.035f*Mathf.Sin(cap*Mathf.PI));
                            v[i]=box.center+new Vector3(d.x*pinch,d.y*(1+.06f*w*phase),d.z*pinch);
                            var tint=Color.Lerp(new Color(.34f,.50f,.31f),new Color(.69f,.77f,.44f),cap*.85f+phase*.15f);tint.a=c[i].a;
                            c[i]=Color.Lerp(c[i],tint,.42f*w);changed[i]=true;
                        }
                    }
                    if(changed.Any(b=>b))
                    {var temp=Object.Instantiate(mesh);try{temp.vertices=v;temp.RecalculateNormals();var normals=temp.normals;for(int i=0;i<n.Length;i++)if(changed[i])n[i]=normals[i];}finally{Object.DestroyImmediate(temp);}}
                }
                else
                {
                    // Remove only complete, still-recognizable original plants. Retain habitat anchors and artist edits.
                    var byFirst=parts.ToDictionary(p=>p.Min());var petals=new[]{new Color(.96f,.89f,.63f),new Color(.88f,.87f,.77f),new Color(.70f,.77f,.97f)};
                    bool Part(int first,int count,Color color)=>byFirst.TryGetValue(first,out var p)&&p.Length==count&&p.OrderBy(i=>i).SequenceEqual(Enumerable.Range(first,count))&&p.All(i=>c[i]==color);
                    void Remove(int[] plant,Vector3 root)
                    {
                        if(plant.Any(i=>Mask(source[i].x,source[i].z)<.4f)||homes.Any(h=>Vector2.Distance(h,new Vector2(root.x,root.z))<3)||Field(root.x*1.3f,root.z*1.3f)>.68f)return;
                        foreach(int i in plant)changed[i]=true;
                    }
                    foreach(var part in parts)
                    {
                        int first=part.Min();
                        if(Part(first,4,new Color(.44f,.63f,.32f)))Remove(part,source[first]);
                        else if(first+112<=source.Length&&Part(first,22,new Color(.39f,.57f,.30f))&&petals.Contains(c[first+22])
                            &&Enumerable.Range(0,5).All(k=>Part(first+22+k*4,4,c[first+22])&&source[first+22+k*4]==source[first+1])&&Part(first+42,70,new Color(.95f,.75f,.34f)))
                            Remove(Enumerable.Range(first,112).ToArray(),source[first]);
                    }
                    var kept=new List<int>();for(int i=0;i<triangles.Length;i+=3)if(!changed[triangles[i]])kept.AddRange(triangles.Skip(i).Take(3));triangles=kept.ToArray();
                }
                if(changed.Any(b=>b))edits.Add(new Edit{Mesh=mesh,Vertices=v,Normals=n,Colors=c,Triangles=triangles,Changed=changed});
            }
            return edits.ToArray();
        }
        // Index actual baked terrain/scenery once, instead of scanning the entire island per plant.
        public sealed class AuthoringWorld
        {
            readonly IslandTerrain terrain;
            readonly Dictionary<Vector2Int,List<Bounds>> obstacles=new Dictionary<Vector2Int,List<Bounds>>();
            readonly Dictionary<Vector2Int,List<MeshCollider>> grounds=new Dictionary<Vector2Int,List<MeshCollider>>();
            static Vector2Int Cell(float x,float z)=>new Vector2Int(Mathf.FloorToInt(x/16),Mathf.FloorToInt(z/16));
            public AuthoringWorld(IslandTerrain value,Vector4? area=null)
            {
                terrain=value;var rect=area??Rect;
                void Add<T>(Dictionary<Vector2Int,List<T>> grid,Bounds box,T item)
                {
                    var a=Cell(box.min.x,box.min.z);var b=Cell(box.max.x,box.max.z);
                    for(int x=a.x;x<=b.x;x++)for(int z=a.y;z<=b.y;z++)
                    {var key=new Vector2Int(x,z);if(!grid.TryGetValue(key,out var list)){list=new List<T>();grid.Add(key,list);}list.Add(item);}
                }
                foreach(var c in terrain.World.Grounds)Add(grounds,c.bounds,c);
                foreach(var b in MeadowRabbitSetup.Obstacles(terrain))
                    if(b.max.x>rect.x-8&&b.min.x<rect.x+rect.z+8&&b.max.z>rect.y-8&&b.min.z<rect.y+rect.w+8)Add(obstacles,b,b);
            }
            public float Floor(Vector2 p)
            {
                var q=terrain.transform.TransformPoint(new Vector3(p.x,0,p.y));
                Check(grounds.TryGetValue(Cell(q.x,q.z),out var list),"baked ground cell "+p);bool found=false;float height=float.MinValue;
                foreach(var c in list)if(c.Raycast(new Ray(new Vector3(q.x,300,q.z),Vector3.down),out var hit,350)&&hit.point.y>height){height=hit.point.y;found=true;}
                Check(found,"actual baked floor "+p);return terrain.transform.InverseTransformPoint(new Vector3(q.x,height,q.z)).y;
            }
            public bool Clear(Vector2 p,float y,float radius,float height)
            {
                var center=terrain.transform.TransformPoint(new Vector3(p.x,y+height*.5f+.025f,p.y));var box=new Bounds(center,new Vector3(radius*2,height,radius*2));
                var a=Cell(box.min.x,box.min.z);var b=Cell(box.max.x,box.max.z);
                for(int x=a.x;x<=b.x;x++)for(int z=a.y;z<=b.y;z++)if(obstacles.TryGetValue(new Vector2Int(x,z),out var list)&&list.Any(o=>o.Intersects(box)))return false;
                return true;
            }
        }
        public static Site[] Sites(IslandTerrain terrain,AuthoringWorld world)
        {
            var result=new List<Site>();var random=new System.Random(102619);var homes=GrasslandEcologySetup.Sites();
            for(float x=-148;x<435;x+=3.1f)for(float z=218;z<818;z+=3.1f)
            {
                var p=new Vector2(x+(float)random.NextDouble()*2.6f-1.3f,z+(float)random.NextDouble()*2.6f-1.3f);
                float phase=(float)random.NextDouble(),field=Field(p.x,p.y),route=(float)StarterIslandLayout.RouteDistance(p.x,p.y);
                if(Mask(p.x,p.y)<.55f||route<3.2f||!StarterIslandLayout.Contains(p.x,p.y,1.3f))continue;
                float density=Mathf.Lerp(.18f,.88f,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.27f,.67f,field)));
                if(phase>density)continue;
                int kind=phase<.032f&&field>.46f?1:0;
                // Flowers concentrate around a few broad patches and existing insect habitats.
                if(homes.Any(h=>Vector2.Distance(h,p)<8)&&phase<.22f)kind=1;
                float y=world.Floor(p);if(!world.Clear(p,y,kind==1?1.35f:.85f,kind==1?1.8f:.8f))continue;
                bool safe=true;foreach(float dx in new[]{-1.15f,1.15f})foreach(float dz in new[]{-1.15f,1.15f})
                    if(Mask(p.x+dx,p.y+dz)<.25f||StarterIslandLayout.RouteDistance(p.x+dx,p.y+dz)<2.5f||Mathf.Abs(world.Floor(p+new Vector2(dx,dz))-y)>.32f)safe=false;
                if(!safe)continue;result.Add(new Site{Point=p,Phase=phase,Kind=kind});
            }
            // Low shrubs and moss stones occupy reserved shoulders, never construction cells.
            foreach(var p in GrasslandCompositionSetup.Sites().Concat(GrasslandFinishSetup.Sites().Select(s=>s.Point)))
            {
                var q=p+new Vector2(0,3.0f);
                if(!GrasslandCompositionSetup.Clear(q)||Mask(q.x,q.y)<.5f||result.Any(s=>Vector2.Distance(s.Point,q)<2.2f))continue;
                float y=world.Floor(q);if(world.Clear(q,y,1.3f,1.6f))result.Add(new Site{Point=q,Phase=(result.Count*.618f)%1,Kind=2});
            }
            Check(result.Count>1200&&result.Count<12000,"bounded meadow density "+result.Count);return result.ToArray();
        }
        sealed class Batch
        {
            public readonly List<Vector3> V=new List<Vector3>(),N=new List<Vector3>();public readonly List<Color>C=new List<Color>();public readonly List<int>T=new List<int>();
            public readonly List<Vector2>A=new List<Vector2>(),B=new List<Vector2>(),D=new List<Vector2>();
            public Mesh Mesh(string name)
            {var m=new Mesh{name=name,indexFormat=V.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};m.SetVertices(V);m.SetNormals(N);m.SetColors(C);m.SetUVs(0,A);m.SetUVs(1,B);m.SetUVs(2,D);var valid=new List<int>();for(int i=0;i<T.Count;i+=3)if(Vector3.Cross(V[T[i+1]]-V[T[i]],V[T[i+2]]-V[T[i]]).sqrMagnitude>1e-14f)valid.AddRange(new[]{T[i],T[i+1],T[i+2]});m.SetTriangles(valid,0);m.RecalculateBounds();var bounds=m.bounds;bounds.Expand(.16f);m.bounds=bounds;return m;}
        }
        public static Dictionary<string,Mesh> Prepare(IslandTerrain terrain,AuthoringWorld world,Site[] sites)
        {
            var export=JsonUtility.FromJson<BeachCompositionSetup.Export>(File.ReadAllText(GrasslandCompositionSetup.Source));Check(export?.templates?.Length==3,"existing Blender templates");
            var shrub=AssetDatabase.LoadAssetAtPath<Mesh>(CoastArtSetup.MeshFolder+"/Shrub Cluster.asset");Check(shrub!=null,"original shrub template");
            var batches=new Dictionary<string,Batch>();
            foreach(var site in sites)
            {
                var p=site.Point;float floor=world.Floor(p);float phase=site.Phase*Mathf.PI*2;bool low=site.Kind!=2;
                string key=(low?"Living meadow ":"Meadow shrubs ")+Mathf.FloorToInt(p.x/128)+" "+Mathf.FloorToInt(p.y/128);
                if(!batches.TryGetValue(key,out var batch)){batch=new Batch();batches.Add(key,batch);}
                void Vertex(Vector3 q,Vector3 normal,Color color,float weight)
                {
                    Check(new Vector2(q.x,q.z).magnitude<=1.18f,"full clump envelope");var xz=p+new Vector2(q.x,q.z);
                    Check(Mask(xz.x,xz.y)>.1f&&StarterIslandLayout.RouteDistance(xz.x,xz.y)>2.45f,"full road/water clearance");
                    batch.V.Add(new Vector3(xz.x,world.Floor(xz)+.018f+q.y,xz.y));batch.N.Add(normal.sqrMagnitude>.001f?normal.normalized:Vector3.up);batch.C.Add(color);
                    batch.A.Add(new Vector2(weight,phase+p.x*.17f));batch.B.Add(p);batch.D.Add(new Vector2(floor,color.g>color.r?1:0));
                }
                void Add(BeachCompositionSetup.Template t,float scale,Vector2 offset)
                {
                    Check(t.positions.Length>0&&t.positions.Length%3==0&&t.normals.Length==t.positions.Length&&t.colors.Length==t.positions.Length/3*4&&t.triangles.All(i=>i>=0&&i<t.positions.Length/3),"template channels");
                    int first=batch.V.Count;var rotation=Quaternion.Euler(0,site.Phase*360,0);
                    for(int i=0;i<t.positions.Length/3;i++)
                    {
                        var q=rotation*(new Vector3(t.positions[i*3],t.positions[i*3+1],t.positions[i*3+2])*scale)+new Vector3(offset.x,0,offset.y);
                        var color=new Color(t.colors[i*4],t.colors[i*4+1],t.colors[i*4+2],1);float height=Mathf.Clamp01(q.y/1.2f);
                        Vertex(q,rotation*new Vector3(t.normals[i*3],t.normals[i*3+1],t.normals[i*3+2]),color,t.name=="Rocks"?0:height);
                    }
                    batch.T.AddRange(t.triangles.Select(i=>first+i));
                }
                if(site.Kind==1)Add(export.templates.Single(t=>t.name=="Flowers"),1.25f,new Vector2(0,0));
                else if(site.Kind==2)
                {
                    Add(export.templates.Single(t=>t.name=="Rocks"),.85f,new Vector2(-.22f,-.12f));
                    var v=shrub.vertices;var n=shrub.normals;int first=batch.V.Count;var rotation=Quaternion.Euler(0,phase*Mathf.Rad2Deg,0);
                    for(int i=0;i<v.Length;i++){var q=rotation*(v[i]*.34f)+new Vector3(.36f,0,.27f);Vertex(q,rotation*n[i],Color.Lerp(new Color(.40f,.57f,.32f),new Color(.61f,.72f,.40f),Mathf.Clamp01(q.y)),Mathf.Clamp01(q.y));}
                    batch.T.AddRange(shrub.triangles.Select(i=>first+i));
                }
                else
                {
                    int count=8+(int)(site.Phase*6);
                    for(int blade=0;blade<count;blade++)
                    {
                        float a=phase+blade*2.399963f;var d=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));var side=new Vector3(-d.z,0,d.x);
                        float h=.24f+(blade%4)*.08f+site.Phase*.12f,r=.12f+(blade%3)*.13f,w=.052f+(blade%2)*.025f;
                        var root=d*r;var tip=root+d*.26f+Vector3.up*h;int first=batch.V.Count;
                        var tint=Color.Lerp(new Color(.43f,.60f,.32f),new Color(.63f,.73f,.41f),site.Phase);
                        var normal=new Vector3(-d.x*.3f,.9f,-d.z*.3f).normalized;
                        Vertex(root-side*w,normal,tint,0);Vertex(root+side*w,normal,tint,0);Vertex(tip,normal,tint,.55f);
                        Vertex(root-side*w,normal,tint,0);Vertex(root+side*w,normal,tint,0);Vertex(tip,normal,tint,.55f);
                        batch.T.AddRange(new[]{first,first+1,first+2,first+3,first+5,first+4});
                    }
                }
            }
            return batches.ToDictionary(e=>e.Key,e=>e.Value.Mesh(e.Key));
        }
        public sealed class RoadPlan {public Mesh Mesh;public MeshRenderer Renderer;public int[] Original,Keep,Meadow;}
        public static RoadPlan PlanRoad(IslandTerrain terrain)
        {
            var renderer=terrain.World.transform.Find("Exploration paths").GetComponent<MeshRenderer>();var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;
            var importer=AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(mesh));
            if(mesh.subMeshCount==2&&importer.userData==Marker+".Road")return null;
            Check(mesh.subMeshCount==1&&string.IsNullOrEmpty(importer.userData),"preserve custom road partitions/metadata");
            var v=mesh.vertices;var indices=mesh.triangles;Check(v.Length%4==0&&indices.Length%6==0,"original quad road topology");
            var keep=new List<int>();var meadow=new List<int>();int last=-1;
            for(int cursor=0;cursor<indices.Length;cursor+=6)
            {
                int a=indices[cursor];Check(a%4==0&&a>last&&a+3<v.Length&&indices.Skip(cursor).Take(6).SequenceEqual(new[]{a,a+1,a+2,a,a+2,a+3}),"original sparse road face order");last=a;
                bool grass=Enumerable.Range(a,4).All(i=>Mask(v[i].x,v[i].z)>.15f);
                (grass?meadow:keep).AddRange(indices.Skip(cursor).Take(6));
            }
            Check(keep.Count>0&&meadow.Count>0,"both original and grassland road ranges");return new RoadPlan{Mesh=mesh,Renderer=renderer,Original=indices,Keep=keep.ToArray(),Meadow=meadow.ToArray()};
        }
        // Explicit authoring only. Partition faces without changing a single original vertex/channel.
        public static string PartitionRoad(IslandTerrain terrain,Material material)
        {
            var plan=PlanRoad(terrain);if(plan==null)return AssetDatabase.GetAssetPath(terrain.World.transform.Find("Exploration paths").GetComponent<MeshFilter>().sharedMesh);
            var original=AssetDatabase.LoadAssetAtPath<Material>(BeachCompositionSetup.MaterialPath);
            Check(plan.Renderer.sharedMaterials.Length==1&&(plan.Renderer.sharedMaterial==original||plan.Renderer.sharedMaterial==material),"preserve custom road material");
            var mesh=plan.Mesh;var v=mesh.vertices;var normals=mesh.normals;var colors=mesh.colors;var bounds=mesh.bounds;var uv=new List<Vector4[]>();
            for(int ch=0;ch<8;ch++){var channel=new List<Vector4>();mesh.GetUVs(ch,channel);uv.Add(channel.ToArray());}
            mesh.subMeshCount=2;mesh.SetTriangles(plan.Keep,0,false);mesh.SetTriangles(plan.Meadow,1,false);plan.Renderer.sharedMaterials=new[]{original,material};EditorUtility.SetDirty(mesh);
            Check(v.SequenceEqual(mesh.vertices)&&normals.SequenceEqual(mesh.normals)&&colors.SequenceEqual(mesh.colors)&&bounds==mesh.bounds,"road geometry/channels/bounds retained");
            for(int ch=0;ch<8;ch++){var channel=new List<Vector4>();mesh.GetUVs(ch,channel);Check(uv[ch].SequenceEqual(channel),"road UV retained");}
            Func<int[],string[]> faces=a=>Enumerable.Range(0,a.Length/3).Select(i=>a[i*3]+","+a[i*3+1]+","+a[i*3+2]).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
            Check(faces(plan.Original).SequenceEqual(faces(mesh.triangles)),"exact original road triangle multiset retained");
            AssetDatabase.SaveAssets();string path=AssetDatabase.GetAssetPath(mesh);var importer=AssetImporter.GetAtPath(path);importer.userData=Marker+".Road";importer.SaveAndReimport();Clean(path);Clean(path+".meta");
            Debug.Log("Grassland road partition verification PASSED: "+plan.Meadow.Length/3+" grass triangles, original opaque material outside; all original geometry, faces, UV and collision retained.");return path;
        }
        static Texture2D FieldMap()
        {
            const int size=512;var t=new Texture2D(size,size,TextureFormat.RGBA32,false,true);var pixels=new Color[size*size];
            for(int z=0;z<size;z++)for(int x=0;x<size;x++)
            {
                float px=Rect.x+x/(size-1f)*Rect.z,pz=Rect.y+z/(size-1f)*Rect.w;
                pixels[z*size+x]=new Color(Mask(px,pz),Mathf.SmoothStep(0,1,Mathf.InverseLerp(.27f,.69f,Field(px,pz))),Mathf.Clamp01((float)StarterIslandLayout.RouteDistance(px,pz)/4),Mathf.PerlinNoise(px*.11f+5,pz*.095f+71));
            }
            t.SetPixels(pixels);t.Apply();return t;
        }
        static Material MakeMaterial(string name,string shader,Material basis,Texture map)
        {
            string path=MaterialFolder+name+".mat";Check(!File.Exists(path)&&!File.Exists(path+".meta"),"preserve orphan/custom material "+name);
            var s=Shader.Find(shader);Check(s!=null&&!ShaderUtil.ShaderHasError(s),"shader "+shader);
            var m=basis!=null?new Material(basis):new Material(s);m.name=name;m.shader=s;if(m.HasProperty("_MeadowMap")){m.SetTexture("_MeadowMap",map);m.SetVector("_MeadowRect",Rect);}AssetDatabase.CreateAsset(m,path);return m;
        }
        public static MeshFilter[] Grounds(IslandTerrain terrain)=>terrain.World.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.name.StartsWith("Land ",StringComparison.Ordinal)&&f.sharedMesh.vertices.Any(p=>Mask(p.x,p.z)>.05f)).ToArray();
        static void Clean(string path)=>File.WriteAllText(path,string.Join("\n",File.ReadAllLines(path).Select(s=>s.TrimEnd()))+"\n");
        [MenuItem("Magic Island/Art/Revitalize Grassland In Order (Explicit)")]
        public static void Rebuild()
        {
            Check(!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling,"Edit Mode required");
            var root=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);Dictionary<string,Mesh> meshes=null;Texture2D texture=null;
            try
            {
                var terrain=root.GetComponent<IslandTerrain>();var old=terrain.World.Scenery.Find(Group);
                if(old!=null){Check(old.GetComponentsInChildren<MeshFilter>(true).All(f=>f.sharedMesh!=null)&&terrain.SeasonalDetails.MeadowBatches.Length>0,"preserve incomplete/custom meadow");Debug.Log("Living meadow and manual edits retained.");return;}
                var edits=Plan(terrain);var roadPlan=PlanRoad(terrain);Check(roadPlan!=null,"unpartitioned baseline road");var world=new AuthoringWorld(terrain);var sites=Sites(terrain,world);meshes=Prepare(terrain,world,sites);Check(meshes.Count<=64,"bounded batch count before writes");
                var grounds=Grounds(terrain);var basis=AssetDatabase.LoadAssetAtPath<Material>(IslandBiomeSetup.MaterialFolder+"/Biome Ground.mat");
                Check(grounds.Length>0&&grounds.All(f=>f.GetComponent<Renderer>().sharedMaterial==basis),"preserve custom ground material");
                var paths=terrain.World.transform.Find("Exploration paths");var ribbon=terrain.World.transform.Find(GrassDesertTrailSetup.GroupName);
                Check(paths!=null&&ribbon!=null&&paths.GetComponent<Renderer>().sharedMaterial==AssetDatabase.LoadAssetAtPath<Material>(BeachCompositionSetup.MaterialPath)
                    &&ribbon.GetComponent<Renderer>().sharedMaterial==AssetDatabase.LoadAssetAtPath<Material>(GrassDesertTrailSetup.MaterialPath),"preserve custom road materials");
                foreach(string key in meshes.Keys)Check(!File.Exists(StarterIslandWorldSetup.Folder+"/"+key+".asset")&&!File.Exists(StarterIslandWorldSetup.Folder+"/"+key+".asset.meta"),"preserve orphan mesh "+key);
                foreach(string name in new[]{"Living Meadow Ground","Living Meadow Cover","Living Meadow Road","Living Meadow Ribbon"})Check(!File.Exists(MaterialFolder+name+".mat")&&!File.Exists(MaterialFolder+name+".mat.meta"),"preserve orphan material");
                Check(!File.Exists(MapPath)&&!File.Exists(MapPath+".meta"),"preserve orphan field texture");
                foreach(string name in new[]{"MagicIsland/Meadow Ground","MagicIsland/Meadow Cover","MagicIsland/Meadow Trail"})Check(Shader.Find(name)!=null&&!ShaderUtil.ShaderHasError(Shader.Find(name)),"compiled shader "+name);
                texture=FieldMap();
                // 1. Ground. 2. Paths. 3. Clump composition. 4. Original canopies and habitat context.
                Directory.CreateDirectory(Path.GetDirectoryName(MapPath));File.WriteAllBytes(MapPath,texture.EncodeToPNG());AssetDatabase.ImportAsset(MapPath,ImportAssetOptions.ForceSynchronousImport);
                var importer=(TextureImporter)AssetImporter.GetAtPath(MapPath);importer.sRGBTexture=false;importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Bilinear;importer.SaveAndReimport();
                var map=AssetDatabase.LoadAssetAtPath<Texture2D>(MapPath);var ground=MakeMaterial("Living Meadow Ground","MagicIsland/Meadow Ground",basis,map);
                var cover=MakeMaterial("Living Meadow Cover","MagicIsland/Meadow Cover",null,null);
                var road=MakeMaterial("Living Meadow Road","MagicIsland/Meadow Trail",null,map);road.SetFloat("_LegacyRoad",1);
                var trail=MakeMaterial("Living Meadow Ribbon","MagicIsland/Meadow Trail",ribbon.GetComponent<Renderer>().sharedMaterial,map);
                foreach(var f in grounds)f.GetComponent<Renderer>().sharedMaterial=ground;PartitionRoad(terrain,road);ribbon.GetComponent<Renderer>().sharedMaterial=trail;
                var group=new GameObject(Group);group.layer=2;group.transform.SetParent(terrain.World.Scenery,false);var renderers=new List<Renderer>();
                foreach(var e in meshes)
                {
                    AssetDatabase.CreateAsset(e.Value,StarterIslandWorldSetup.Folder+"/"+e.Key+".asset");var go=new GameObject(e.Key,typeof(MeshFilter),typeof(MeshRenderer));go.layer=2;go.transform.SetParent(group.transform,false);
                    go.GetComponent<MeshFilter>().sharedMesh=e.Value;var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=cover;renderer.shadowCastingMode=ShadowCastingMode.Off;renderers.Add(renderer);
                }
                Check(renderers.Count<=64,"bounded batch count");terrain.SeasonalDetails.MeadowBatches=renderers.ToArray();
                foreach(var e in edits){e.Mesh.vertices=e.Vertices;e.Mesh.normals=e.Normals;e.Mesh.colors=e.Colors;e.Mesh.SetTriangles(e.Triangles,0,false);e.Mesh.RecalculateBounds();EditorUtility.SetDirty(e.Mesh);}
                PrefabUtility.SaveAsPrefabAsset(root,TerrainSetup.PrefabPath);AssetDatabase.SaveAssets();
                foreach(var e in edits){string path=AssetDatabase.GetAssetPath(e.Mesh);var meta=AssetImporter.GetAtPath(path);meta.userData=Marker+(string.IsNullOrEmpty(meta.userData)?"":";previous="+meta.userData);meta.SaveAndReimport();Clean(path);Clean(path+".meta");}
                foreach(string key in meshes.Keys)Clean(StarterIslandWorldSetup.Folder+"/"+key+".asset");Clean(TerrainSetup.PrefabPath);
                foreach(string name in new[]{"Living Meadow Ground","Living Meadow Cover","Living Meadow Road","Living Meadow Ribbon"})Clean(MaterialFolder+name+".mat");
                Debug.Log("Living grassland baked: "+sites.Length+" clumps ("+sites.Count(s=>s.Kind==1)+" flower pockets), "+meshes.Count+" batches, "+edits.Length+" original canopy/verge edits.");
            }
            finally{if(texture!=null)Object.DestroyImmediate(texture);if(meshes!=null)foreach(var m in meshes.Values)if(!AssetDatabase.Contains(m))Object.DestroyImmediate(m);PrefabUtility.UnloadPrefabContents(root);}
        }
    }
}
