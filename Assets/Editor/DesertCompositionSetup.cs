using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MagicIsland.Interaction;
using MagicIsland.TerrainSystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    // Explicit editor bake. Original geography, channels, collision and resource identities stay intact.
    public static class DesertCompositionSetup
    {
        public const string Group = "Desert route composition";
        public const string Marker = "MagicIsland.DesertComposition.v1";
        public const float Radius = 1.05f;
        public static readonly Vector2[] Route = { new Vector2(-180,400), new Vector2(-280,500), new Vector2(-390,565), new Vector2(-420,760), new Vector2(-360,816) };
        public sealed class Edit { public Mesh Mesh; public Color[] Colors; public bool[] Changed; }
        public sealed class Site { public Vector2 Point; public float Yaw; public int Kind; }
        public static void Require(bool ok, string why) { if (!ok) throw new InvalidOperationException("Desert composition: " + why); }
        static float Smooth(float a, float b, float x) => Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,x));
        public static float Distance(Vector2 p) => Enumerable.Range(0,Route.Length-1).Min(i => (float)StarterIslandLayout.SegmentDistance(p.x,p.y,Route[i].x,Route[i].y,Route[i+1].x,Route[i+1].y));
        public static float Mask(Vector3 p)
        {
            float w = (1-Smooth(24,48,Distance(new Vector2(p.x,p.z)))) * Smooth(387,417,p.z) * (1-Smooth(795,830,p.z));
            w *= Smooth(.04f,.65f,(float)StarterIslandLayout.DesertWeight(p.x,p.z));
            w *= Smooth(30,55,(float)StarterIslandLayout.CoastDistance(p.x,p.z));
            w *= 1-Smooth(.02f,.16f,(float)StarterIslandLayout.SnowWeight(p.x,p.z));
            foreach(var q in new[]{new Vector2(-420,590),new Vector2(-8,700),new Vector2(-40,1000)}) w *= Smooth(45,70,Vector2.Distance(new Vector2(p.x,p.z),q));
            return w;
        }
        public static Edit[] Plan(IslandTerrain terrain)
        {
            var result = new List<Edit>();
            foreach(var f in terrain.World.GetComponentsInChildren<MeshFilter>(true).Where(f => f.name.StartsWith("Land ",StringComparison.Ordinal)||f.name.StartsWith("Landscape ",StringComparison.Ordinal)))
            {
                var mesh=f.sharedMesh; var v=mesh.vertices;
                if(!v.Any(p => Mask(p)>0)) continue;
                var importer=AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(mesh));
                Require(importer!=null,"mesh importer");
                if(importer.userData==Marker) continue;
                Require(string.IsNullOrEmpty(importer.userData),"preserve custom importer metadata: "+mesh.name);
                var colors=mesh.colors; Require(colors.Length==v.Length,"authored colors"); var changed=new bool[v.Length];
                if(f.name.StartsWith("Land ",StringComparison.Ordinal))
                {
                    for(int i=0;i<v.Length;i++)
                    {
                        float w=Mask(v[i]); if(w<=0) continue;
                        float desert=(float)StarterIslandLayout.DesertWeight(v[i].x,v[i].z);
                        float noise=Mathf.PerlinNoise(v[i].x*.017f+17,v[i].z*.024f+39);
                        var dry=new Color(.72f,.73f,.51f); var sand=Color.Lerp(new Color(.84f,.72f,.54f),new Color(.91f,.79f,.61f),noise);
                        var target=Color.Lerp(dry,sand,Smooth(.12f,.9f,desert)); target.a=colors[i].a;
                        colors[i]=Color.Lerp(colors[i],target,.30f*w); changed[i]=true;
                    }
                }
                else foreach(var component in BiomePolishSetup.Components(mesh))
                {
                    // Only the original rounded sandstone lobes; brown cylindrical trunks never qualify.
                    if(component.Length!=70||component.Any(i => colors[i].r-colors[i].g<.075f||colors[i].g-colors[i].b<.075f)) continue;
                    var bounds=new Bounds(v[component[0]],Vector3.zero); foreach(int i in component) bounds.Encapsulate(v[i]);
                    if(bounds.size.y>7||Mask(bounds.center)<=0) continue;
                    foreach(int i in component)
                    {
                        float w=Mask(v[i]); if(w<=0) continue;
                        float cap=Mathf.InverseLerp(bounds.min.y,bounds.max.y,v[i].y);
                        float band=.5f+.5f*Mathf.Sin(cap*13+bounds.center.x*.035f);
                        var tint=Color.Lerp(new Color(.66f,.52f,.40f),new Color(.86f,.73f,.55f),cap*.75f+band*.25f); tint.a=colors[i].a;
                        colors[i]=Color.Lerp(colors[i],tint,.32f*w); changed[i]=true;
                    }
                }
                if(changed.Any(b => b)) result.Add(new Edit{Mesh=mesh,Colors=colors,Changed=changed});
            }
            return result.ToArray();
        }
        public static bool Clear(Vector2 p)
        {
            if(!BiomePolishSetup.Clear(p,Radius)||Distance(p)>5.5f) return false;
            for(int x=Mathf.FloorToInt(p.x-Radius-.5f);x<=Mathf.CeilToInt(p.x+Radius+.5f);x++)
                for(int z=Mathf.FloorToInt(p.y-Radius-.5f);z<=Mathf.CeilToInt(p.y+Radius+.5f);z++)
                    if(Mathf.Abs(x-p.x)<=Radius+.5f&&Mathf.Abs(z-p.y)<=Radius+.5f&&StarterIslandLayout.CanPlace(x,z)) return false;
            return true;
        }
        public static Site[] Sites(IslandTerrain terrain)
        {
            var sites=new List<Site>();
            var old=DesertFrontSetup.PlanSites().Select(s=>s.Point).Concat(BiomePolishSetup.Sites(2).Select(s=>s.Point)).ToArray();
            // Existing baked details, not a re-roll of the base desert population.
            var obstacles=MeadowRabbitSetup.Obstacles(terrain);
            for(int segment=0;segment<Route.Length-1;segment++)
            {
                var a=Route[segment];var b=Route[segment+1];var d=(b-a).normalized;var side=new Vector2(-d.y,d.x);float length=Vector2.Distance(a,b);
                int count=Mathf.FloorToInt(length/15.5f);
                for(int i=0;i<count;i++)
                {
                    float t=(i+.43f+(i%3)*.12f)/count;
                    foreach(int sign in new[]{(i+segment)%2==0?1:-1,(i+segment)%2==0?-1:1})
                    {
                        var p=Vector2.Lerp(a,b,t)+side*(sign*4.18f);
                        if(!Clear(p)||Mask(new Vector3(p.x,0,p.y))<.08f||old.Any(q=>Vector2.Distance(p,q)<2.5f)||sites.Any(s=>Vector2.Distance(s.Point,p)<7)) continue;
                        float y=Ground(terrain,p);var box=new Bounds(terrain.transform.TransformPoint(new Vector3(p.x,y+.9f,p.y)),new Vector3(Radius*2,1.9f,Radius*2));
                        if(obstacles.Any(o=>o.Intersects(box))) continue;
                        sites.Add(new Site{Point=p,Yaw=(i*137.5f+segment*41)%360,Kind=(i+segment)%4});break;
                    }
                }
            }
            Require(sites.Count>=10,"insufficient complete safe shoulder pockets: "+sites.Count);return sites.ToArray();
        }
        public static float Ground(IslandTerrain terrain,Vector2 p)
        {
            Require(terrain.World.Raycast(new Ray(terrain.transform.TransformPoint(new Vector3(p.x,300,p.y)),Vector3.down),out var hit,350),"actual ground at "+p);
            return terrain.transform.InverseTransformPoint(hit.point).y;
        }
        sealed class Batch
        {
            public readonly List<Vector3> V=new List<Vector3>(),N=new List<Vector3>();public readonly List<Color> C=new List<Color>();public readonly List<int> T=new List<int>();
            public Mesh Mesh(string name) {var m=new Mesh{name=name,indexFormat=V.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};m.SetVertices(V);m.SetNormals(N);m.SetColors(C);m.SetTriangles(T,0);m.RecalculateBounds();return m;}
        }
        public static Dictionary<string,Mesh> Prepare(IslandTerrain terrain,Site[] sites)
        {
            var rock=AssetDatabase.LoadAssetAtPath<Mesh>(IslandBiomeSetup.Folder+"/Sandstone.asset");var cactus=AssetDatabase.LoadAssetAtPath<Mesh>(IslandBiomeSetup.Folder+"/Cactus.asset");
            Require(rock!=null&&cactus!=null,"existing authored desert templates");
            var export=JsonUtility.FromJson<BeachCompositionSetup.Export>(File.ReadAllText(GrasslandCompositionSetup.Source));
            Require(export!=null&&export.version==1&&export.templates!=null,"existing Blender grass library");var grass=export.templates.Single(t=>t.name=="Grass");
            var batches=new Dictionary<string,Batch>();
            for(int index=0;index<sites.Length;index++)
            {
                var site=sites[index];var p=site.Point;string key="Desert pocket "+Mathf.FloorToInt(p.x/128)+" "+Mathf.FloorToInt(p.y/128);
                if(!batches.TryGetValue(key,out var batch)){batch=new Batch();batches.Add(key,batch);}
                var yaw=Quaternion.Euler(0,site.Yaw,0);
                void Append(Vector3[] v,Vector3[] n,Color[] c,int[] triangles,Vector3 scale,Vector3 offset,int kind)
                {
                    Require(v.Length>0&&v.Length==n.Length&&v.Length==c.Length,"source channels");int start=batch.V.Count;
                    float min=v.Min(q=>q.y),height=Mathf.Max(.001f,v.Max(q=>q.y)-min);
                    var normalMatrix=Matrix4x4.Scale(scale).inverse.transpose;
                    for(int j=0;j<v.Length;j++)
                    {
                        var q=Vector3.Scale(v[j],scale);float cap=(v[j].y-min)/height;
                        // Uneven weathered stone silhouettes, all kept within the validated pocket.
                        if(kind==0){q.x*=.90f+.08f*Mathf.Sin(cap*5+index);q.z*=.91f+.07f*Mathf.Cos(cap*4+index);}
                        q=yaw*(q+offset);Require(new Vector2(q.x,q.z).magnitude<=Radius,"full template footprint");
                        var xz=p+new Vector2(q.x,q.z);float floor=Ground(terrain,xz);
                        batch.V.Add(new Vector3(xz.x,floor+.015f+q.y,xz.y));batch.N.Add((yaw*normalMatrix.MultiplyVector(n[j])).normalized);
                        Color tint;
                        if(kind==0)tint=Color.Lerp(new Color(.65f,.50f,.38f),new Color(.86f,.73f,.57f),cap*.78f+(.5f+.5f*Mathf.Sin(cap*15+index))*.22f);
                        else if(kind==1)tint=c[j].r>.8f?new Color(.91f,.76f,.66f):Color.Lerp(new Color(.39f,.53f,.40f),new Color(.57f,.65f,.45f),cap);
                        else {float dry=Smooth(.15f,.95f,(float)StarterIslandLayout.DesertWeight(p.x,p.y));tint=Color.Lerp(new Color(.57f,.65f,.41f),new Color(.73f,.67f,.47f),dry);tint*=.93f+.07f*cap;tint.a=1;}
                        batch.C.Add(tint);
                    }
                    for(int j=0;j<triangles.Length;j+=3)
                    {
                        int a=start+triangles[j],b=start+triangles[j+1],c0=start+triangles[j+2];
                        if(Vector3.Cross(batch.V[b]-batch.V[a],batch.V[c0]-batch.V[a]).sqrMagnitude>1e-14f)batch.T.AddRange(new[]{a,b,c0});
                    }
                }
                void Stone(float s,Vector3 offset)=>Append(rock.vertices,rock.normals,rock.colors,rock.triangles,new Vector3(s,site.Kind==0?s*1.28f:s*.80f,s*.86f),offset,0);
                void Cactus(float s,Vector3 offset)=>Append(cactus.vertices,cactus.normals,cactus.colors,cactus.triangles,new Vector3(s,s*(.9f+index%3*.11f),s),offset,1);
                void Grass(float s,Vector3 offset)
                {
                    int count=grass.positions.Length/3;
                    var v=Enumerable.Range(0,count).Select(i=>new Vector3(grass.positions[i*3],grass.positions[i*3+1],grass.positions[i*3+2])).ToArray();
                    var n=Enumerable.Range(0,count).Select(i=>new Vector3(grass.normals[i*3],grass.normals[i*3+1],grass.normals[i*3+2])).ToArray();
                    Append(v,n,Enumerable.Repeat(Color.white,count).ToArray(),grass.triangles,new Vector3(s,s*.72f,s),offset,2);
                }
                if(site.Kind==0){Stone(1.35f,new Vector3(-.28f,0,-.04f));Stone(.62f,new Vector3(.44f,0,.26f));Grass(.60f,new Vector3(.22f,0,-.43f));}
                else if(site.Kind==1){Cactus(.94f,new Vector3(-.24f,0,-.08f));Cactus(.48f,new Vector3(.38f,0,.25f));Grass(.56f,new Vector3(.25f,0,-.38f));}
                else if(site.Kind==2){Stone(1.10f,new Vector3(-.30f,0,.06f));Grass(.83f,new Vector3(.36f,0,-.17f));}
                else {Grass(.91f,new Vector3(-.25f,0,.06f));Grass(.57f,new Vector3(.37f,0,.23f));Stone(.56f,new Vector3(.32f,0,-.37f));}
            }
            return batches.ToDictionary(e=>e.Key,e=>e.Value.Mesh(e.Key));
        }
        static void Clean(string path)=>File.WriteAllText(path,string.Join("\n",File.ReadAllLines(path).Select(s=>s.TrimEnd()))+"\n");
        [MenuItem("Magic Island/Art/Refine Desert Route Composition (Explicit)")]
        public static void Refine()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling,"Edit Mode required");
            var root=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);Dictionary<string,Mesh> meshes=null;
            try
            {
                var terrain=root.GetComponent<IslandTerrain>();var existing=terrain.World.Scenery.Find(Group);
                if(existing!=null)
                {
                    var filters=existing.GetComponentsInChildren<MeshFilter>(true);
                    Require(filters.Length>0&&filters.All(f=>f.sharedMesh!=null)&&existing.GetComponentsInChildren<Collider>(true).Length==0,"preserve incomplete/custom group");
                    Debug.Log("Desert composition and subsequent manual edits retained.");return;
                }
                var edits=Plan(terrain);Require(edits.Length>0,"unrefined original ground");
                meshes=Prepare(terrain,Sites(terrain));
                foreach(string key in meshes.Keys)Require(!File.Exists(StarterIslandWorldSetup.Folder+"/"+key+".asset")&&!File.Exists(StarterIslandWorldSetup.Folder+"/"+key+".asset.meta"),"preserve orphan/custom batch "+key);
                var material=AssetDatabase.LoadAssetAtPath<Material>(BeachCompositionSetup.MaterialPath);Require(material!=null,"shared AA detail material");
                // Complete plans and guards precede all asset writes. No new UV channel is allocated.
                var group=new GameObject(Group);group.layer=2;group.transform.SetParent(terrain.World.Scenery,false);
                foreach(var e in meshes)
                {
                    string path=StarterIslandWorldSetup.Folder+"/"+e.Key+".asset";AssetDatabase.CreateAsset(e.Value,path);
                    var go=new GameObject(e.Key,typeof(MeshFilter),typeof(MeshRenderer));go.layer=2;go.transform.SetParent(group.transform,false);
                    go.GetComponent<MeshFilter>().sharedMesh=e.Value;go.GetComponent<MeshRenderer>().sharedMaterial=material;
                }
                foreach(var e in edits){e.Mesh.colors=e.Colors;EditorUtility.SetDirty(e.Mesh);}
                PrefabUtility.SaveAsPrefabAsset(root,TerrainSetup.PrefabPath);AssetDatabase.SaveAssets();
                foreach(var e in edits){string path=AssetDatabase.GetAssetPath(e.Mesh);var importer=AssetImporter.GetAtPath(path);importer.userData=Marker;importer.SaveAndReimport();Clean(path);}
                foreach(string key in meshes.Keys)Clean(StarterIslandWorldSetup.Folder+"/"+key+".asset");Clean(TerrainSetup.PrefabPath);
                Debug.Log("Desert composition baked: "+Sites(terrain).Length+" pockets, "+meshes.Count+" batches, "+edits.Length+" color meshes; ground geometry, UV0-7 and collision retained.");
            }
            finally{if(meshes!=null)foreach(var m in meshes.Values)if(!AssetDatabase.Contains(m))Object.DestroyImmediate(m);PrefabUtility.UnloadPrefabContents(root);}
        }
    }
}
