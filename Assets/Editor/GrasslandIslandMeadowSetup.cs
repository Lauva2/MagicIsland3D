using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MagicIsland.Interaction;
using MagicIsland.Ocean;
using MagicIsland.TerrainSystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    // Explicit island-only authoring. All plans and resource conflicts are checked before writes.
    // Terrain, collisions, construction rules and the original non-grassland road faces are retained.
    public static class GrasslandIslandMeadowSetup
    {
        public const string Group="Grassland island trails and ecology",Marker="MagicIsland.IslandMeadow.v1";
        public const string Folder="Assets/Art/Meshes/Terrain/BiomeIslands";
        // Turn on the flat ancient-tree plateau, beyond the old narrow causeway edge.
        static readonly Vector2[] Spine={new Vector2(-110,335.3333f),new Vector2(-150,370),new Vector2(-100,520),new Vector2(-100,700),new Vector2(-75,700),new Vector2(-26,700),new Vector2(-18,690),new Vector2(-8,690)};
        static readonly Vector2[][] routes=new[]{Round(Spine),new[]{new Vector2(-150,370),new Vector2(-200,420)}};
        public static Vector2[][] Routes()=>routes;
        static Vector2[] Round(Vector2[] source)
        {
            var points=new List<Vector2>{source[0]};
            for(int i=1;i<source.Length-1;i++)
            {
                var a=source[i]-(source[i]-source[i-1]).normalized*4;var b=source[i]+(source[i+1]-source[i]).normalized*4;points.Add(a);
                for(int j=1;j<=12;j++){float t=j/12f;points.Add((1-t)*(1-t)*a+2*(1-t)*t*source[i]+t*t*b);}
            }
            points.Add(source.Last());return points.ToArray();
        }
        public static void Check(bool ok,string why){if(!ok)throw new InvalidOperationException("Island meadow: "+why);}
        public static float RouteDistance(Vector2 p)
        {
            float best=float.MaxValue;foreach(var route in Routes())for(int i=1;i<route.Length;i++)best=Mathf.Min(best,(float)StarterIslandLayout.SegmentDistance(p.x,p.y,route[i-1].x,route[i-1].y,route[i].x,route[i].y));return best;
        }
        public static bool Reserved(Vector2 p,float radius)
        {
            if(BiomeIslandLayout.At(p.x,p.y)!=IslandBiome.Grassland||!BiomeIslandLayout.Contains(p.x,p.y,12))return false;
            for(int x=Mathf.CeilToInt(p.x-radius-.5f);x<=Mathf.FloorToInt(p.x+radius+.5f);x++)
                for(int z=Mathf.CeilToInt(p.y-radius-.5f);z<=Mathf.FloorToInt(p.y+radius+.5f);z++)if(BiomeIslandLayout.CanPlace(x,z))return false;
            return true;
        }
        public sealed class RoadEdit {public Mesh Mesh;public int[][] Indices;}
        public static RoadEdit[] RoadPlan(IslandTerrain terrain)
        {
            var result=new List<RoadEdit>();
            foreach(var f in terrain.World.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.name=="Exploration paths"||f.name==GrassDesertTrailSetup.GroupName||f.name==LakeSnowTrailSetup.GroupName))
            {
                var mesh=f.sharedMesh;var path=AssetDatabase.GetAssetPath(mesh);var importer=AssetImporter.GetAtPath(path);
                Check(path.StartsWith(Folder+"/",StringComparison.Ordinal)&&string.IsNullOrEmpty(importer.userData),"preserve custom road metadata "+path);
                Check(f.transform.localToWorldMatrix==terrain.transform.localToWorldMatrix,"preserve custom road transform");
                var vertices=mesh.vertices;var arrays=new int[mesh.subMeshCount][];bool changed=false;
                for(int sub=0;sub<arrays.Length;sub++)
                {
                    var keep=new List<int>();var indices=mesh.GetTriangles(sub);
                    for(int i=0;i<indices.Length;i+=3)
                    {
                        var a=vertices[indices[i]];var b=vertices[indices[i+1]];var c=vertices[indices[i+2]];
                        bool Select(Vector3 v)=>BiomeIslandLayout.At(v.x,v.z)==IslandBiome.Grassland&&BiomeIslandLayout.Contains(v.x,v.z,25);
                        bool selected=Select(a)&&Select(b)&&Select(c);
                        if(selected)changed=true;else keep.AddRange(indices.Skip(i).Take(3));
                    }
                    arrays[sub]=keep.ToArray();
                }
                if(changed)result.Add(new RoadEdit{Mesh=mesh,Indices=arrays});
            }
            Check(result.Count>0,"retained original island road required");return result.ToArray();
        }
        public sealed class Batch
        {
            public readonly List<Vector3> V=new List<Vector3>(),N=new List<Vector3>();public readonly List<Color>C=new List<Color>();
            public readonly List<Vector2>A=new List<Vector2>(),B=new List<Vector2>(),D=new List<Vector2>();public readonly List<int>T=new List<int>();
            public Mesh Mesh(string name,float envelope=.2f)
            {
                // Weld only exactly equal complete vertices; retain flat normals, root ownership,
                // wind, individual phases and double-sided faces. Shared material stays untouched.
                var cache=new Dictionary<(Vector3,Vector3,Color,Vector2,Vector2,Vector2),int>();var map=new int[V.Count];
                var v=new List<Vector3>();var n=new List<Vector3>();var c=new List<Color>();var a=new List<Vector2>();var b=new List<Vector2>();var d=new List<Vector2>();
                for(int i=0;i<V.Count;i++)
                {
                    var key=(V[i],N[i],C[i],A[i],B[i],D[i]);
                    if(!cache.TryGetValue(key,out int id)){id=v.Count;cache.Add(key,id);v.Add(V[i]);n.Add(N[i]);c.Add(C[i]);a.Add(A[i]);b.Add(B[i]);d.Add(D[i]);}map[i]=id;
                }
                var m=new Mesh{name=name,indexFormat=v.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};m.SetVertices(v);m.SetNormals(n);m.SetColors(c);m.SetUVs(0,a);m.SetUVs(1,b);m.SetUVs(2,d);m.SetTriangles(T.Select(i=>map[i]).ToArray(),0);m.RecalculateBounds();var box=m.bounds;box.Expand(envelope);m.bounds=box;return m;
            }
        }
        public static Mesh Trail(GrasslandRevitalizationSetup.AuthoringWorld world)
        {
            // Half-meter cells divide the baked four-meter ground grid exactly. Match its
            // anti-diagonal so the road never interpolates beneath a terrain triangle.
            const float step=.5f;var routes=Routes();var cells=new HashSet<Vector2Int>();
            foreach(var route in routes)for(int i=1;i<route.Length;i++)
            {
                int count=Mathf.CeilToInt(Vector2.Distance(route[i-1],route[i])/.5f);
                for(int j=0;j<=count;j++)
                {
                    var p=Vector2.Lerp(route[i-1],route[i],j/(float)count);int x=Mathf.FloorToInt(p.x/step),z=Mathf.FloorToInt(p.y/step);
                    for(int dx=-4;dx<=4;dx++)for(int dz=-4;dz<=4;dz++)cells.Add(new Vector2Int(x+dx,z+dz));
                }
            }
            var batch=new Batch();var cache=new Dictionary<Vector2Int,int>();
            float Edge(Vector2 p)=>RouteDistance(p)/(1.65f+.16f*Mathf.Sin(p.x*.19f+p.y*.073f)+.09f*Mathf.Sin(p.y*.39f));
            int Vertex(Vector2Int key)
            {
                if(cache.TryGetValue(key,out int index))return index;var p=new Vector2(key.x*step,key.y*step);
                Check(Reserved(p,0),"trail retains construction grid "+p);float y=world.Floor(p);
                index=batch.V.Count;cache.Add(key,index);batch.V.Add(new Vector3(p.x,y+.025f,p.y));batch.N.Add(Vector3.up);
                batch.C.Add(Color.Lerp(new Color(.77f,.71f,.53f),new Color(.81f,.75f,.57f),Mathf.PerlinNoise(p.x*.11f,p.y*.09f)));
                batch.A.Add(new Vector2(Edge(p),0));batch.B.Add(new Vector2(1,0));batch.D.Add(Vector2.zero);return index;
            }
            foreach(var cell in cells.OrderBy(c=>c.x).ThenBy(c=>c.y))
            {
                var corners=new[]{cell,cell+Vector2Int.right,cell+Vector2Int.one,cell+Vector2Int.up};
                if(corners.All(c=>Edge(new Vector2(c.x*step,c.y*step))>1.12f))continue;
                foreach(var ids in new[]{new[]{0,3,1},new[]{1,3,2}})
                {
                    var p=ids.Select(i=>new Vector2(corners[i].x*step,corners[i].y*step)).ToArray();
                    if(p.Any(q=>!Reserved(q,0))||p.All(q=>Edge(q)>1.12f))continue;
                    var bounds=new Bounds(new Vector3(p[0].x,0,p[0].y),Vector3.zero);foreach(var q in p)bounds.Encapsulate(new Vector3(q.x,0,q.y));
                    bool clear=true;for(int x=Mathf.CeilToInt(bounds.min.x-.5f);x<=Mathf.FloorToInt(bounds.max.x+.5f);x++)for(int z=Mathf.CeilToInt(bounds.min.z-.5f);z<=Mathf.FloorToInt(bounds.max.z+.5f);z++)if(BiomeIslandLayout.CanPlace(x,z))clear=false;
                    if(clear)batch.T.AddRange(ids.Select(i=>Vertex(corners[i])));
                }
            }
            Check(batch.T.Count>1000,"complete union trail");return batch.Mesh("Island meadow trail");
        }
        public static MeadowRabbitHabitat[] Rabbits(IslandTerrain terrain,GrasslandRevitalizationSetup.AuthoringWorld world)
        {
            var ecology=terrain.World.GetComponentInChildren<WorldWaterEcology>(true);Check(ecology.MeadowRabbits==null&&ecology.RabbitHabitats.Length==0,"preserve existing custom population");
            var result=new List<MeadowRabbitHabitat>();
            foreach(float target in new[]{445f,532f,615f,674f})
            {
                MeadowRabbitHabitat habitat=null;
                for(int attempt=0;attempt<100&&habitat==null;attempt++)
                {
                    float z=target+(attempt/2-25)*.5f;var d=z<515?new Vector2(1,3).normalized:Vector2.up;var side=new Vector2(-d.y,d.x);var p=z<515?new Vector2(-150+(z-370)/3,z):new Vector2(-100,z);
                    p+=side*(attempt%2==0?4.35f:-4.35f);var points=new Vector3[25];bool safe=true;
                    for(int i=0;i<points.Length;i++)
                    {
                        var q=p+d*Mathf.Lerp(-1.8f,1.8f,i/24f);if(!Reserved(q,.52f)||RouteDistance(q)<3.35f){safe=false;break;}
                        float y=world.Floor(q);if(!world.Clear(q,y,.55f,1.25f)){safe=false;break;}
                        foreach(var o in new[]{Vector2.left*.5f,Vector2.right*.5f,Vector2.up*.5f,Vector2.down*.5f})if(Mathf.Abs(world.Floor(q+o)-y)>.08f)safe=false;
                        points[i]=ecology.transform.InverseTransformPoint(terrain.transform.TransformPoint(new Vector3(q.x,y+.015f,q.y)));
                    }
                    if(safe)habitat=new MeadowRabbitHabitat{Track=points,Phase=result.Count*2.399963f,WaitSeconds=3.4f+result.Count*.7f,Speed=.78f+result.Count*.035f};
                }
                Check(habitat!=null&&habitat.IsValid,"safe rabbit strip near "+target);result.Add(habitat);
            }
            return result.ToArray();
        }
        public sealed class Site {public Vector2 P;public float Phase,Scale;public int Kind;}
        static Vector2[] Roots(IslandTerrain terrain)=>terrain.SeasonalDetails.MeadowBatches.SelectMany(r=>r.GetComponent<MeshFilter>().sharedMesh.uv2).Distinct().ToArray();
        static bool Near(Vector2 p,IEnumerable<Vector2> points,float radius)=>points.Any(q=>(q-p).sqrMagnitude<radius*radius);
        static float FlowerPocket(Vector2 p)
        {
            float best=0;
            foreach(var bed in new[]{new Vector4(-190,548,28,38),new Vector4(-175,630,22,34),new Vector4(-75,550,28,40),new Vector4(-90,420,24,30),new Vector4(-58,725,22,16)})
            {
                float x=(p.x-bed.x)/bed.z,z=(p.y-bed.y)/bed.w;
                float distance=Mathf.Sqrt(x*x+z*z)+.13f*Mathf.Sin(p.x*.21f+p.y*.17f);
                best=Mathf.Max(best,1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.35f,1.1f,distance)));
            }
            return best;
        }
        public static Site[] Plants(IslandTerrain terrain,GrasslandRevitalizationSetup.AuthoringWorld world,MeadowRabbitHabitat[] rabbits)
        {
            var random=new System.Random(79531);var roots=Roots(terrain);var tracks=rabbits.SelectMany(h=>h.Track).Select(p=>new Vector2(p.x,p.z)).ToArray();var result=new List<Site>();
            // A spatial index keeps the new seed independent from all gameplay random sources.
            var grid=roots.GroupBy(p=>new Vector2Int(Mathf.FloorToInt(p.x/4),Mathf.FloorToInt(p.y/4))).ToDictionary(g=>g.Key,g=>g.ToList());
            bool Occupied(Vector2 p)
            {
                var cell=new Vector2Int(Mathf.FloorToInt(p.x/4),Mathf.FloorToInt(p.y/4));
                for(int x=-1;x<=1;x++)for(int z=-1;z<=1;z++)if(grid.TryGetValue(cell+new Vector2Int(x,z),out var list)&&Near(p,list,1.7f))return true;return false;
            }
            for(float x=-242;x<180;x+=2.4f)for(float z=238;z<805;z+=2.4f)
            {
                var p=new Vector2(x+(float)random.NextDouble()*1.8f,z+(float)random.NextDouble()*1.8f);float phase=(float)random.NextDouble();
                if(BiomeIslandLayout.At(p.x,p.y)!=IslandBiome.Grassland||!BiomeIslandLayout.Contains(p.x,p.y,18)||RouteDistance(p)<3.1f||Near(p,tracks,1.7f)||Occupied(p))continue;
                float field=Mathf.PerlinNoise(p.x*.018f+19,p.y*.017f+7)*.65f+Mathf.PerlinNoise(p.x*.057f-31,p.y*.044f+5)*.35f;
                float route=(float)StarterIslandLayout.RouteDistance(p.x,p.y);float pocket=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(4,15,route));
                float density=Mathf.Lerp(.12f,.86f,Mathf.InverseLerp(.34f,.68f,field));if(phase>Mathf.Max(density,pocket*.65f))continue;
                float bed=FlowerPocket(p);int kind=phase<.025f&&field>.48f||pocket>.3f&&phase<.12f||phase<bed*.38f?1:0;
                // Small collision-free bushes share the same root cuts as low grass on buildable
                // cells; rocks/vegetation disappear together under existing or new construction.
                if(phase<.055f&&Reserved(p,1.4f)&&route>3.8f||bed>.35f&&phase<.032f)kind=2;
                float y=world.Floor(p);if(y<.5f||!world.Clear(p,y,1.35f,kind==0?1.2f:1.9f))continue;
                bool safe=true;foreach(var q in new[]{p+Vector2.one*1.3f,p-Vector2.one*1.3f,p+new Vector2(1.3f,-1.3f),p+new Vector2(-1.3f,1.3f)})
                    if(!BiomeIslandLayout.Contains(q.x,q.y,15)||RouteDistance(q)<2.65f||Mathf.Abs(world.Floor(q)-y)>.24f)safe=false;
                if(safe)result.Add(new Site{P=p,Kind=kind,Phase=phase*6.283185f,Scale=.8f+(float)random.NextDouble()*.55f});
            }
            Check(result.Count>700&&result.Count<12000,"bounded island meadow density "+result.Count);return result.ToArray();
        }
        static void Add(Batch batch,BeachCompositionSetup.Template t,Site site,GrasslandRevitalizationSetup.AuthoringWorld world,float scale,Vector2 offset)
        {
            int first=batch.V.Count;var rotation=Quaternion.Euler(0,site.Phase*Mathf.Rad2Deg,0);float floor=world.Floor(site.P);
            for(int i=0;i<t.positions.Length/3;i++)
            {
                var q=rotation*(new Vector3(t.positions[i*3],t.positions[i*3+1],t.positions[i*3+2])*scale)+new Vector3(offset.x,0,offset.y);var p=site.P+new Vector2(q.x,q.z);
                Check(new Vector2(q.x,q.z).magnitude<=1.18f,"complete plant footprint within existing construction cut envelope");var normal=rotation*new Vector3(t.normals[i*3],t.normals[i*3+1],t.normals[i*3+2]);
                batch.V.Add(new Vector3(p.x,world.Floor(p)+.018f+q.y,p.y));batch.N.Add(normal.sqrMagnitude>.001f?normal:Vector3.up);
                var c=new Color(t.colors[i*4],t.colors[i*4+1],t.colors[i*4+2],1);batch.C.Add(c);batch.A.Add(new Vector2(t.name=="Rocks"?0:Mathf.Clamp01(q.y),site.Phase));batch.B.Add(site.P);batch.D.Add(new Vector2(floor,c.g>c.r?1:0));
            }
            batch.T.AddRange(t.triangles.Select(i=>i+first));
        }
        public static Dictionary<string,Mesh> Cover(GrasslandRevitalizationSetup.AuthoringWorld world,Site[] sites)
        {
            var data=JsonUtility.FromJson<BeachCompositionSetup.Export>(File.ReadAllText(GrasslandCompositionSetup.Source));Check(data?.templates?.Length==3,"existing Blender flower/grass/rock templates");
            var shrub=AssetDatabase.LoadAssetAtPath<Mesh>(CoastArtSetup.MeshFolder+"/Shrub Cluster.asset");Check(shrub!=null,"existing shrub template");
            var batches=new Dictionary<string,Batch>();
            foreach(var s in sites)
            {
                string key="Island meadow cover "+Mathf.FloorToInt(s.P.x/128)+" "+Mathf.FloorToInt(s.P.y/128);
                if(!batches.TryGetValue(key,out var b)){b=new Batch();batches.Add(key,b);}
                if(s.Kind==1)Add(b,data.templates.Single(t=>t.name=="Flowers"),s,world,1.25f*s.Scale,Vector2.zero);
                else if(s.Kind==2)
                {
                    Add(b,data.templates.Single(t=>t.name=="Rocks"),s,world,.72f,new Vector2(-.3f,-.1f));
                    var t=new BeachCompositionSetup.Template{name="Shrub",positions=shrub.vertices.SelectMany(v=>new[]{v.x,v.y,v.z}).ToArray(),normals=shrub.normals.SelectMany(n=>new[]{n.x,n.y,n.z}).ToArray(),colors=shrub.vertices.SelectMany(v=>new[]{.47f,.64f,.35f,1f}).ToArray(),triangles=shrub.triangles};
                    Add(b,t,s,world,.33f,new Vector2(.35f,.22f));
                }
                else
                {
                    // Use the existing living-meadow blade construction, not the high-detail
                    // roadside tuft thousands of times. This is editor-baked shared geometry.
                    float floor=world.Floor(s.P);int count=8+Mathf.FloorToInt(s.Scale*3);
                    for(int blade=0;blade<count;blade++)
                    {
                        float a=s.Phase+blade*2.399963f;var d=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));var side=new Vector3(-d.z,0,d.x);
                        float h=(.35f+(blade%4)*.10f)*s.Scale,r=.12f+(blade%3)*.12f,w=.065f+(blade%2)*.025f;var root=d*r;var tip=root+d*.25f+Vector3.up*h;int first=b.V.Count;
                        var color=Color.Lerp(new Color(.39f,.56f,.30f),new Color(.58f,.68f,.38f),.5f+.5f*Mathf.Sin(s.Phase));
                        var n=new Vector3(-d.x*.3f,.9f,-d.z*.3f).normalized;
                        foreach(var q in new[]{root-side*w,root+side*w,tip,root-side*w,root+side*w,tip})
                        {
                            var p=s.P+new Vector2(q.x,q.z);b.V.Add(new Vector3(p.x,world.Floor(p)+.018f+q.y,p.y));b.N.Add(n);b.C.Add(color);
                            b.A.Add(new Vector2(q.y>0?.55f:0,s.Phase));b.B.Add(s.P);b.D.Add(new Vector2(floor,1));
                        }
                        b.T.AddRange(new[]{first,first+1,first+2,first+3,first+5,first+4});
                    }
                }
            }
            return batches.ToDictionary(e=>e.Key,e=>e.Value.Mesh(e.Key));
        }
        public sealed class Insects {public Dictionary<string,Mesh> Meshes;public Mesh Flowers;public Vector2[] Sites;}
        public static Insects InsectPlan(IslandTerrain terrain,GrasslandRevitalizationSetup.AuthoringWorld world,MeadowRabbitHabitat[] rabbits)
        {
            var ecology=terrain.World.GetComponentInChildren<WorldWaterEcology>(true);var used=new HashSet<int>(ecology.GrasslandAmbient.Where(r=>r.sharedMaterial.shader.name=="MagicIsland/Meadow Insects"&&r.sharedMaterial.GetFloat("_EcologyMode")<.5f)
                .SelectMany(r=>r.GetComponent<MeshFilter>().sharedMesh.uv2).Select(p=>Mathf.FloorToInt((p.y+.0001f)/2.399963f)));
            var ids=Enumerable.Range(0,16).Where(i=>!used.Contains(i)).Take(6).ToArray();Check(ids.Length==6,"six unused original insect phase slots");
            var sites=new List<Vector2>();
            foreach(var h in rabbits)
            {
                var center=h.Track[h.Track.Length/2];var direction=(h.Track.Last()-h.Track[0]).normalized;
                for(int sign=-1;sign<=1&&sites.Count<6;sign+=2)
                {
                    var p=new Vector2(center.x+direction.x*sign*5,center.z+direction.z*sign*5);
                    var inward=p.y<515?new Vector2(-150+(p.y-370)/3,p.y):new Vector2(-100,p.y);p+=(inward-p).normalized*.6f;float y=world.Floor(p);
                    if(Reserved(p,1.15f)&&RouteDistance(p)>3.1f&&world.Clear(p,y,1.3f,1.8f)&&!Near(p,sites,5))sites.Add(p);
                }
            }
            Check(sites.Count==6,"six complete flower habitats");
            var source=JsonUtility.FromJson<GrasslandEcologySetup.Export>(File.ReadAllText(GrasslandEcologySetup.Source));var batches=new Dictionary<string,Mesh>();var flowers=new Batch();
            var flower=JsonUtility.FromJson<BeachCompositionSetup.Export>(File.ReadAllText(GrasslandCompositionSetup.Source)).templates.Single(t=>t.name=="Flowers");
            for(int i=0;i<sites.Count;i++)Add(flowers,flower,new Site{P=sites[i],Phase=sites[i].x*.71f+sites[i].y*.43f},world,1.12f,Vector2.zero);
            // Foliage shader and insect landing share precisely the same rooted wind phase.
            for(int i=0;i<flowers.A.Count;i++)flowers.A[i]=new Vector2(flowers.A[i].x,flowers.B[i].x*.71f+flowers.B[i].y*.43f);
            foreach(string kind in new[]{"Butterfly","Firefly"})
            {
                var b=new Batch();var t=source.templates.Single(a=>a.name==kind);
                for(int site=0;site<sites.Count;site++)for(int member=0;member<(kind=="Butterfly"?2:3);member++)
                {
                    var p=sites[site];float phase=ids[site]*2.399963f+member*1.83f,y=world.Floor(p)+(kind=="Butterfly"?.95f:.58f)+member*.14f;int first=b.V.Count;
                    for(int i=0;i<t.wings.Length;i++)
                    {
                        var q=new Vector3(t.positions[i*3],t.positions[i*3+1],t.positions[i*3+2])*(member==0?1:.82f);
                        b.V.Add(new Vector3(p.x,y,p.y)+q);b.N.Add(new Vector3(t.normals[i*3],t.normals[i*3+1],t.normals[i*3+2]));b.C.Add(new Color(t.colors[i*4],t.colors[i*4+1],t.colors[i*4+2],1));
                        b.A.Add(p);b.B.Add(new Vector2(y,phase));b.D.Add(new Vector2(t.wings[i],phase));
                    }
                    b.T.AddRange(t.triangles.Select(v=>v+first));
                }
                string name="Island meadow "+kind;batches.Add(name,b.Mesh(name,2.1f));
            }
            return new Insects{Meshes=batches,Flowers=flowers.Mesh("Island meadow pollen",.22f),Sites=sites.ToArray()};
        }
        static Renderer Attach(Transform parent,string name,Mesh mesh,Material material,bool shadow=false)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.layer=2;go.transform.SetParent(parent,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;
            var r=go.GetComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=shadow?ShadowCastingMode.On:ShadowCastingMode.Off;return r;
        }
        static void Clean(string path)=>File.WriteAllText(path,string.Join("\n",File.ReadAllLines(path).Select(l=>l.TrimEnd()))+"\n");
        [MenuItem("Magic Island/Art/Refine Grassland Island Trails And Ecology (Explicit)")]
        public static void Rebuild()
        {
            Check(!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling,"Edit Mode required");
            var root=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);var meshes=new Dictionary<string,Mesh>();var created=new List<string>();var backup=new Dictionary<string,byte[]>();bool done=false;
            try
            {
                var terrain=root.GetComponent<IslandTerrain>();Check(terrain.IsValid&&terrain.World.ArtVersion==3,"five authored islands required");var scenery=terrain.World.Scenery;var ecology=terrain.World.GetComponentInChildren<WorldWaterEcology>(true);
                var old=scenery.Find(Group);if(old!=null){Check(old.GetComponentsInChildren<MeshFilter>(true).Length>3&&old.GetComponentsInChildren<MeshFilter>(true).All(f=>f.sharedMesh!=null)&&ecology.IsValid,"preserve custom/incomplete group");Debug.Log("Island meadow trails, ecology and manual edits retained.");return;}
                Check(scenery.localToWorldMatrix==terrain.transform.localToWorldMatrix&&ecology.transform.localToWorldMatrix==terrain.transform.localToWorldMatrix,"preserve custom scenery/ecology root transforms");
                Physics.SyncTransforms();var world=new GrasslandRevitalizationSetup.AuthoringWorld(terrain,new Vector4(-300,200,600,650));var roads=RoadPlan(terrain);
                var rabbits=Rabbits(terrain,world);var insects=InsectPlan(terrain,world,rabbits);meshes.Add("Island meadow pollen",insects.Flowers);foreach(var e in insects.Meshes)meshes.Add(e.Key,e.Value);
                var plants=Plants(terrain,world,rabbits).Where(s=>!Near(s.P,insects.Sites,2)).ToArray();Debug.Log("Island meadow plan: "+plants.Length+" plant sites; preparing lightweight shared batches.");
                var coverMeshes=Cover(world,plants);Check(terrain.SeasonalDetails.MeadowBatches.Length+coverMeshes.Count<=64,"bounded combined cover batch count");foreach(var pair in coverMeshes)meshes.Add(pair.Key,pair.Value);
                meshes.Add("Island meadow trail",Trail(world));meshes.Add("Island meadow rabbits",MeadowRabbitSetup.Prepare(MeadowRabbitSetup.ReadSource(),rabbits));
                var cover=AssetDatabase.LoadAssetAtPath<Material>(GrasslandRevitalizationSetup.MaterialFolder+"Living Meadow Cover.mat");var trail=AssetDatabase.LoadAssetAtPath<Material>(GrasslandRevitalizationSetup.MaterialFolder+"Living Meadow Ribbon.mat");
                var rabbit=AssetDatabase.LoadAssetAtPath<Material>(MeadowRabbitSetup.MaterialPath);var pollen=AssetDatabase.LoadAssetAtPath<Material>(GrasslandEcologySetup.FoliagePath);
                var butterfly=AssetDatabase.LoadAssetAtPath<Material>(GrasslandEcologySetup.MaterialFolder+"Meadow Butterflies.mat");var firefly=AssetDatabase.LoadAssetAtPath<Material>(GrasslandEcologySetup.MaterialFolder+"Meadow Fireflies.mat");
                Check(new[]{cover,trail,rabbit,pollen,butterfly,firefly}.All(m=>m!=null&&!ShaderUtil.ShaderHasError(m.shader)),"existing compiled shared materials");Check(trail.GetFloat("_LegacyRoad")<.5f,"ribbon material UV convention");
                foreach(string key in meshes.Keys){string path=Folder+"/"+key+".asset";Check(!File.Exists(path)&&!File.Exists(path+".meta"),"preserve orphan/custom asset "+path);}
                foreach(string path in roads.Select(r=>AssetDatabase.GetAssetPath(r.Mesh)).SelectMany(p=>new[]{p,p+".meta"}).Concat(new[]{TerrainSetup.PrefabPath}))backup.Add(path,File.ReadAllBytes(path));
                foreach(var pair in meshes){string path=Folder+"/"+pair.Key+".asset";created.Add(path);AssetDatabase.CreateAsset(pair.Value,path);}
                var group=new GameObject(Group);group.layer=2;group.transform.SetParent(scenery,false);var covers=new List<Renderer>();var ambient=ecology.GrasslandAmbient.ToList();
                foreach(var pair in meshes)
                {
                    bool isCover=pair.Key.StartsWith("Island meadow cover",StringComparison.Ordinal),isPollen=pair.Key.EndsWith("pollen",StringComparison.Ordinal),isButterfly=pair.Key.EndsWith("Butterfly",StringComparison.Ordinal),isFirefly=pair.Key.EndsWith("Firefly",StringComparison.Ordinal),isRabbit=pair.Key.EndsWith("rabbits",StringComparison.Ordinal);
                    var parent=isCover||pair.Key.EndsWith("trail",StringComparison.Ordinal)?group.transform:ecology.transform;
                    var renderer=Attach(parent,pair.Key,pair.Value,isCover?cover:isPollen?pollen:isButterfly?butterfly:isFirefly?firefly:isRabbit?rabbit:trail,isRabbit);
                    if(isCover)covers.Add(renderer);if(isPollen||isButterfly||isFirefly)ambient.Add(renderer);if(isRabbit)ecology.MeadowRabbits=renderer;
                }
                ecology.RabbitHabitats=rabbits;ecology.GrasslandAmbient=ambient.ToArray();terrain.SeasonalDetails.MeadowBatches=terrain.SeasonalDetails.MeadowBatches.Concat(covers).ToArray();
                foreach(var edit in roads){for(int i=0;i<edit.Indices.Length;i++)edit.Mesh.SetTriangles(edit.Indices[i],i,false);EditorUtility.SetDirty(edit.Mesh);}
                Check(terrain.IsValid&&ecology.IsValid,"complete AA-owned island ecology");Check(PrefabUtility.SaveAsPrefabAsset(root,TerrainSetup.PrefabPath)!=null,"prefab save");AssetDatabase.SaveAssets();
                foreach(string path in created.Concat(roads.Select(r=>AssetDatabase.GetAssetPath(r.Mesh)))){var importer=AssetImporter.GetAtPath(path);importer.userData=Marker;importer.SaveAndReimport();Clean(path);Clean(path+".meta");}Clean(TerrainSetup.PrefabPath);done=true;
                Debug.Log("Island meadow baked: "+plants.Length+" clumps ("+plants.Count(p=>p.Kind==1)+" flowers, "+plants.Count(p=>p.Kind==2)+" shrub/stone pockets), "+covers.Count+" cover batches; 4 rabbits, 6 flower habitats, 12 butterflies and 18 fireflies; continuous union trail, original ground/collision/construction retained.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
                if(!done){foreach(var pair in backup)File.WriteAllBytes(pair.Key,pair.Value);foreach(string path in created)AssetDatabase.DeleteAsset(path);foreach(string path in backup.Keys.Where(p=>!p.EndsWith(".meta",StringComparison.Ordinal)))AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);}
                foreach(var m in meshes.Values)if(m!=null&&!EditorUtility.IsPersistent(m))Object.DestroyImmediate(m);
            }
        }
    }
}
