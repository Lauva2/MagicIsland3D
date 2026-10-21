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

namespace MagicIsland.EditorTools
{
    // Baked appearance only: no changes to the road geometry, collision or construction rules.
    public static class GrasslandPathEdgeSetup
    {
        public const string Marker="MagicIsland.GrasslandPathEdge.v1",Pending="MagicIsland.GrasslandPathEdge.Pending.v1";
        public const string RoadName="Island meadow trail";
        public const float StaticRadius=.32f,MotionRadius=.72f,ClearCenter=.85f;
        static readonly HashSet<string> Known=new HashSet<string>
        {
            GrasslandGroundLayersSetup.Marker,GrasslandTouchSetup.Marker,GrasslandReadabilitySetup.Marker,
            GrasslandPatchSetup.Marker,GrasslandBlendSetup.Marker,GrasslandWildflowerSetup.Marker,GrasslandEdgeSetup.Marker
        };
        [Serializable] public sealed class Site { public Vector2 Root;public float Phase,Height;public int Blades;public bool Pebbles; }
        public sealed class Edit
        {
            public Mesh Mesh;public bool Road;public Vector3[] Vertices,Normals;public Color[] Colors;public Vector2[][] Channels;
            public int[] Indices;public bool[] Changed;public Bounds Bounds;public string Metadata;public Site[] Sites;
        }
        public static bool Done(string metadata)=>metadata.Split(';').Contains(Marker);
        public static void Check(bool ok,string why){if(!ok)throw new InvalidOperationException("Grassland path edges: "+why);}
        static float Noise(Vector2 p,float scale,float salt)=>GrasslandGroundLayersSetup.Noise(p,scale,salt);
        static Vector2 XZ(Vector3 p)=>new Vector2(p.x,p.z);
        static float Feather(Vector2 p,MeadowTouchSettings s)=>1-Mathf.SmoothStep(0,1,
            Mathf.InverseLerp(s.AreaRadius-s.Feather-1,s.AreaRadius-1.5f,Vector2.Distance(p,s.Center)));
        public static MeshFilter Road(IslandTerrain t)=>t.World.GetComponentsInChildren<MeshFilter>(true).Single(f=>f.sharedMesh!=null&&f.sharedMesh.name==RoadName);
        public static MeshFilter[] Filters(IslandTerrain t)=>new[]{Road(t)}.Concat(GrasslandPatchSetup.Batches(t).Select(r=>r.GetComponent<MeshFilter>())).ToArray();

        public static bool[] ProtectedRoadVertices(Mesh mesh,MeadowTouchSettings s)
        {
            var v=mesh.vertices;var faces=mesh.triangles;var protect=new bool[v.Length];var edges=new Dictionary<(int,int),int>();
            void Edge(int a,int b){var key=(Mathf.Min(a,b),Mathf.Max(a,b));edges.TryGetValue(key,out int n);edges[key]=n+1;}
            for(int i=0;i<faces.Length;i+=3)
            {
                int a=faces[i],b=faces[i+1],c=faces[i+2];Edge(a,b);Edge(b,c);Edge(c,a);
                Check(Vector2.Distance(XZ(v[a]),XZ(v[b]))<.72f&&Vector2.Distance(XZ(v[b]),XZ(v[c]))<.72f
                    &&Vector2.Distance(XZ(v[c]),XZ(v[a]))<.72f,"original half-meter horizontal road cells");
                // A changed vertex cannot influence any triangle touching the central clear strip or leaving the sample.
                if(new[]{a,b,c}.Any(j=>Vector2.Distance(XZ(v[j]),s.Center)>=s.AreaRadius-.75f
                    ||GrasslandIslandMeadowSetup.RouteDistance(XZ(v[j]))<ClearCenter))protect[a]=protect[b]=protect[c]=true;
            }
            foreach(var e in edges.Where(e=>e.Value==1)){protect[e.Key.Item1]=true;protect[e.Key.Item2]=true;}
            Check(edges.Values.All(n=>n==1||n==2),"original single-layer manifold road");return protect;
        }

        static Site[] Sites(IslandTerrain t,GrasslandRevitalizationSetup.AuthoringWorld world)
        {
            var s=t.SeasonalDetails.TouchSample;var plants=new List<(Vector2 root,bool grass,float radius)>();
            foreach(var r in t.SeasonalDetails.MeadowBatches)
            {
                var m=r.GetComponent<MeshFilter>().sharedMesh;var v=m.vertices;var roots=m.uv2;var uv=m.uv;
                Check(roots.Length==v.Length&&uv.Length==v.Length,"complete neighboring plant data");
                foreach(var g in Enumerable.Range(0,v.Length).Where(i=>Vector2.Distance(roots[i],s.Center)<30).GroupBy(i=>roots[i]))
                    plants.Add((g.Key,g.All(i=>uv[i].x>=-5&&uv[i].x<=-4),g.Max(i=>Vector2.Distance(XZ(v[i]),g.Key))));
            }
            var ecology=t.World.GetComponentInChildren<WorldWaterEcology>(true);
            var tracks=ecology.RabbitHabitats.SelectMany(h=>h.Track).Select(p=>t.transform.InverseTransformPoint(ecology.transform.TransformPoint(p))).Select(XZ).ToArray();
            var random=new System.Random(224871);var candidates=new List<(Site site,double rank)>();
            for(float x=s.Center.x-22;x<s.Center.x+22;x+=.7f)for(float z=s.Center.y-22;z<s.Center.y+22;z+=.7f)
            {
                var p=new Vector2(x+(float)random.NextDouble()*.6f,z+(float)random.NextDouble()*.6f);
                float draw=(float)random.NextDouble(),phase=(float)random.NextDouble()*Mathf.PI*2;double rank=random.NextDouble();
                float road=GrasslandIslandMeadowSetup.RouteDistance(p),fade=Feather(p,s);
                if(road<2.2f||road>3.05f||fade<.15f||Vector2.Distance(p,s.Center)+MotionRadius>s.AreaRadius-1
                    ||!BiomeIslandLayout.Contains(p.x,p.y,20)||BiomeIslandLayout.At(p.x,p.y)!=IslandBiome.Grassland)continue;
                if(draw>Mathf.SmoothStep(0,.82f,Mathf.InverseLerp(.32f,.69f,Noise(p,.31f,62)))*fade
                    ||plants.Any(a=>Vector2.Distance(a.root,p)<(a.grass?.44f:a.radius+MotionRadius))
                    ||tracks.Any(q=>Vector2.Distance(q,p)<1.95f))continue;
                float floor=world.Floor(p);
                if(!world.Clear(p,floor,MotionRadius,.7f)||new[]{Vector2.up,Vector2.down,Vector2.left,Vector2.right}
                    .Any(d=>Mathf.Abs(world.Floor(p+d*MotionRadius)-floor)>.12f))continue;
                candidates.Add((new Site{Root=p,Phase=phase,Height=(.14f+(float)random.NextDouble()*.10f)*Mathf.Lerp(.6f,1,fade),
                    Blades=4+random.Next(3),Pebbles=random.Next(4)==0},rank));
            }
            var sites=new List<Site>();
            foreach(var candidate in candidates.OrderBy(c=>c.rank))
            {if(sites.Any(a=>Vector2.Distance(a.Root,candidate.site.Root)<.82f))continue;sites.Add(candidate.site);if(sites.Count==36)break;}
            Check(sites.Count>=16&&sites.Any(a=>a.Pebbles),"bounded interrupted low shoulders "+sites.Count);return sites.ToArray();
        }

        public static Edit[] Plan(IslandTerrain t)
        {
            var s=t.SeasonalDetails.TouchSample;var filters=Filters(t);
            Check(t.IsValid&&s.ArtVersion==1&&s.IsValid&&s.Renderers.Length==6&&filters.Length==3
                &&t.transform.position==Vector3.zero&&t.transform.rotation==Quaternion.identity&&t.transform.lossyScale==Vector3.one,"original prepared sample");
            var collision=new HashSet<Mesh>(t.GetComponentsInChildren<MeshCollider>(true).Select(c=>c.sharedMesh));
            foreach(var f in filters)
            {
                var m=f.sharedMesh;string p=AssetDatabase.GetAssetPath(m),metadata=AssetImporter.GetAtPath(p).userData;var r=f.GetComponent<Renderer>();bool road=f==filters[0];
                Check(p.StartsWith(BiomeIslandsSetup.Folder+"/",StringComparison.Ordinal)&&!collision.Contains(m)
                    &&f.transform.localToWorldMatrix==t.transform.localToWorldMatrix&&m.subMeshCount==1&&m.tangents.Length==0
                    &&m.blendShapeCount==0&&m.bindposes.Length==0&&m.boneWeights.Length==0,"original visual-only static mesh");
                Check(r.sharedMaterials.Length==1&&r.sharedMaterial.shader.name==(road?"MagicIsland/Meadow Trail":GrasslandFloraSetup.ShaderName),"preserve custom material");
                if(road)Check(AssetDatabase.GetAssetPath(r.sharedMaterial.shader)=="Assets/Art/Shaders/MeadowTrail.shader"
                    &&!ShaderUtil.ShaderHasError(r.sharedMaterial.shader),"original supported road shader");
                Check(road?metadata==GrasslandIslandMeadowSetup.Marker:GrasslandEdgeSetup.Done(metadata)&&metadata.Split(';').All(Known.Contains),"preserve custom metadata / partial migration");
                Check(Enumerable.Range(0,3).All(ch=>m.GetVertexAttributeDimension((VertexAttribute)((int)VertexAttribute.TexCoord0+ch))==2)
                    &&Enumerable.Range(3,5).All(ch=>{var a=new List<Vector4>();m.GetUVs(ch,a);return a.Count==0;}),"preserve custom channels");
            }
            var world=new GrasslandRevitalizationSetup.AuthoringWorld(t,new Vector4(s.Center.x-25,s.Center.y-25,50,50));
            var sites=Sites(t,world);var result=new List<Edit>();
            var rocks=JsonUtility.FromJson<BeachCompositionSetup.Export>(File.ReadAllText(GrasslandCompositionSetup.Source)).templates.Single(a=>a.name=="Rocks");
            for(int which=0;which<filters.Length;which++)
            {
                var m=filters[which].sharedMesh;var v=m.vertices;var n=m.normals;var c=m.colors;var uv=m.uv;var roots=m.uv2;var season=m.uv3;
                Check(new[]{n.Length,c.Length,uv.Length,roots.Length,season.Length}.All(l=>l==v.Length),"complete original vertex channels");
                var e=new Edit{Mesh=m,Road=which==0,Changed=new bool[v.Length],Bounds=m.bounds,Indices=m.triangles,
                    Metadata=AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(m)).userData,Sites=Array.Empty<Site>()};
                if(e.Road)
                {
                    var protect=ProtectedRoadVertices(m,s);
                    for(int i=0;i<v.Length;i++)
                    {
                        var p=XZ(v[i]);float fade=Feather(p,s);if(protect[i]||fade<=0)continue;
                        Check(uv[i].x>=0&&uv[i].y==0&&roots[i]==new Vector2(1,0)&&season[i]==Vector2.zero,"original trail weights");
                        float edge=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.60f,1.02f,uv[i].x));
                        float patch=Noise(p,.39f,48);
                        // Decode before rasterization, then sample continuous noise per pixel, avoiding grid-shaped steps.
                        roots[i].y=-913-fade;
                        var tint=Color.Lerp(new Color(.77f,.72f,.54f),new Color(.61f,.65f,.43f),Mathf.Clamp01((patch-.35f)*1.9f));
                        tint.a=c[i].a;c[i]=Color.Lerp(c[i],tint,edge*fade*.43f);e.Changed[i]=true;
                    }
                    e.Vertices=v;e.Normals=n;e.Colors=c;e.Channels=new[]{uv,roots,season};
                }
                else
                {
                    var points=v.ToList();var normals=n.ToList();var colors=c.ToList();var channels=new[]{uv.ToList(),roots.ToList(),season.ToList()};var indices=e.Indices.ToList();
                    string suffix=m.name.Substring("Meadow undergrowth ".Length);
                    e.Sites=sites.Where(a=>Mathf.FloorToInt(a.Root.x/128)+" "+Mathf.FloorToInt(a.Root.y/128)==suffix).ToArray();
                    foreach(var site in e.Sites)
                    {
                        float floor=world.Floor(site.Root);var rotation=Quaternion.Euler(0,site.Phase*Mathf.Rad2Deg,0);
                        int Vertex(Vector3 q,Vector3 normal,Color color,float weight,float seasonal)
                        {
                            q=rotation*q;normal=rotation*normal;var p=site.Root+XZ(q);
                            Check(XZ(q).magnitude<=StaticRadius&&q.y<.30f&&q.y>-.06f,"complete low shoulder footprint");
                            int id=points.Count;points.Add(new Vector3(p.x,world.Floor(p)+.018f+q.y,p.y));normals.Add(normal.normalized);color.a=1;colors.Add(color);
                            channels[0].Add(new Vector2(weight,site.Phase));channels[1].Add(site.Root);channels[2].Add(new Vector2(floor,seasonal));return id;
                        }
                        for(int blade=0;blade<site.Blades;blade++)
                        {
                            float a=blade*2.399963f;var d=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));var side=new Vector3(-d.z,0,d.x);
                            var anchor=d*(.055f+blade%3*.035f);float h=site.Height*(.75f+blade%3*.10f),width=.025f+blade%3*.008f;
                            var tint=Color.Lerp(new Color(.40f,.52f,.31f),new Color(.51f,.61f,.38f),Noise(site.Root,.47f,19));
                            int first=points.Count;
                            void V(float t,float lateral)
                            {var q=anchor+d*(.13f*t*t)+side*lateral+Vector3.up*(h*t*(1-.18f*t));var color=tint*Mathf.Lerp(.95f,1.08f,t);color.a=1;
                                Vertex(q,new Vector3(-d.x*.2f,1,-d.z*.2f),color,-4-Mathf.Clamp01(q.y*1.5f),1);}
                            V(0,-width);V(0,width);V(.55f,-width*.65f);V(.55f,width*.65f);V(1,0);
                            var faces=new[]{0,2,1,1,2,3,2,4,3};for(int i=0;i<faces.Length;i+=3)
                                indices.AddRange(new[]{first+faces[i],first+faces[i+1],first+faces[i+2],first+faces[i],first+faces[i+2],first+faces[i+1]});
                        }
                        if(site.Pebbles)
                        {
                            int first=points.Count;float scale=.08f+.04f*Noise(site.Root,.63f,17);
                            for(int i=0;i<rocks.positions.Length/3;i++)
                            {
                                var q=new Vector3(rocks.positions[i*3],rocks.positions[i*3+1],rocks.positions[i*3+2])*scale+new Vector3(-.08f,-.012f,.05f);
                                Vertex(q,new Vector3(rocks.normals[i*3],rocks.normals[i*3+1],rocks.normals[i*3+2]),new Color(.65f,.65f,.54f),0,0);
                            }
                            indices.AddRange(rocks.triangles.Select(i=>i+first));
                        }
                    }
                    Check(m.indexFormat==IndexFormat.UInt32||points.Count<=65535,"preserve original index format");
                    e.Vertices=points.ToArray();e.Normals=normals.ToArray();e.Colors=colors.ToArray();e.Channels=channels.Select(a=>a.ToArray()).ToArray();e.Indices=indices.ToArray();
                    foreach(var p in points.Skip(v.Length)){e.Bounds.Encapsulate(p+Vector3.one*MotionRadius);e.Bounds.Encapsulate(p-Vector3.one*MotionRadius);}
                }
                result.Add(e);
            }
            Check(result[0].Changed.Count(b=>b)>40&&result.Sum(e=>e.Sites.Length)==sites.Length&&result.Skip(1).All(e=>e.Sites.Length>0),"local road edges and both original shoulder batches");
            return result.ToArray();
        }

        static void Clean(string p)=>File.WriteAllText(p,string.Join("\n",File.ReadAllLines(p).Select(l=>l.TrimEnd()))+"\n");
        [MenuItem("Magic Island/Art/Refine Grassland Path Edge Sample (Explicit)")]
        public static void Rebuild()
        {
            Check(!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling,"Edit Mode required");
            var root=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);var backups=new Dictionary<string,byte[]>();bool saved=false;
            try
            {
                var t=root.GetComponent<IslandTerrain>();Check(t.IsValid&&t.SeasonalDetails.TouchSample.ArtVersion==1,"prepared sample");
                var paths=Filters(t).Select(f=>AssetDatabase.GetAssetPath(f.sharedMesh)).ToArray();var data=paths.Select(p=>AssetImporter.GetAtPath(p).userData).ToArray();
                Check(!data.Any(d=>d.Split(';').Contains(Pending)),"preserve interrupted write");
                if(data.Any(Done)){Check(data.All(Done),"preserve partial migration");saved=true;Debug.Log("Grassland path edges and later manual edits retained.");return;}
                Physics.SyncTransforms();var plan=Plan(t);
                foreach(string p in paths.SelectMany(p=>new[]{p,p+".meta"}))backups.Add(p,File.ReadAllBytes(p));
                var first=AssetImporter.GetAtPath(paths[0]);first.userData=data[0]+";"+Pending;first.SaveAndReimport();
                foreach(var e in plan)
                {
                    if(!e.Road){e.Mesh.SetVertices(e.Vertices);e.Mesh.SetNormals(e.Normals);for(int ch=1;ch<3;ch++)e.Mesh.SetUVs(ch,e.Channels[ch]);e.Mesh.SetTriangles(e.Indices,0,false);e.Mesh.bounds=e.Bounds;}
                    e.Mesh.SetColors(e.Colors);e.Mesh.SetUVs(0,e.Channels[0]);if(e.Road)e.Mesh.SetUVs(1,e.Channels[1]);EditorUtility.SetDirty(e.Mesh);
                }
                AssetDatabase.SaveAssets();
                foreach(var e in plan.Skip(1).Concat(plan.Take(1)))
                {string p=AssetDatabase.GetAssetPath(e.Mesh);var importer=AssetImporter.GetAtPath(p);importer.userData=e.Metadata+";"+Marker;importer.SaveAndReimport();Clean(p);Clean(p+".meta");}
                saved=true;Debug.Log("Grassland path edges baked: "+plan[0].Changed.Count(b=>b)+" edge vertices, "+plan.Sum(e=>e.Sites.Length)+" low grass groups; original road geometry and central triangles retained.");
            }
            finally
            {PrefabUtility.UnloadPrefabContents(root);if(!saved){foreach(var p in backups)File.WriteAllBytes(p.Key,p.Value);foreach(string p in backups.Keys.Where(p=>!p.EndsWith(".meta",StringComparison.Ordinal)))AssetDatabase.ImportAsset(p,ImportAssetOptions.ForceUpdate);}}
        }
    }
}
