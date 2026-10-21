using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MagicIsland.CameraSystem;
using MagicIsland.Interaction;
using MagicIsland.Ocean;
using MagicIsland.TerrainSystem;
using UnityEditor;
using UnityEngine;

namespace MagicIsland.EditorTools
{
    // Offline sample composition. One original root owns the complete construction cut.
    public static class GrasslandEdgeSetup
    {
        public const string Marker="MagicIsland.GrasslandEdge.v1",Pending="MagicIsland.GrasslandEdge.Pending.v1";
        static readonly HashSet<string> Known=new HashSet<string>
        {
            GrasslandRevitalizationSetup.Marker,GrasslandIslandMeadowSetup.Marker,GrasslandGroveSetup.Marker,
            GrasslandFloraSetup.Marker,GrasslandGroundLayersSetup.Marker,GrasslandShrubStoneSetup.Marker,
            GrasslandTouchSetup.Marker,GrasslandReadabilitySetup.Marker,GrasslandPatchSetup.Marker,
            GrasslandBlendSetup.Marker,GrasslandWildflowerSetup.Marker
        };
        [Serializable] public sealed class Site { public Vector2 Root;public float Phase,Scale;public bool Flowers; }
        public sealed class Edit
        {
            public Mesh Mesh;public Vector3[] Vertices,Normals;public Color[] Colors;public Vector2[][] Channels;
            public int[] Indices;public bool[] Changed,Removed;public Bounds Bounds;public string Metadata;
            public Site[] Sites;public int Thinned,Lowered;
        }
        public static bool Done(string data)=>data.Split(';').Contains(Marker);
        public static void Check(bool ok,string why){if(!ok)throw new InvalidOperationException("Grassland edges: "+why);}
        static float Noise(Vector2 p,float scale,float salt)=>GrasslandGroundLayersSetup.Noise(p,scale,salt);
        static bool Grass(float weight)=>weight>=-5&&weight<=-4;
        public static Vector2[] Trees(IslandTerrain t)=>t.GetComponentsInChildren<LandscapeCrownOccluder>(true)
            .SelectMany(c=>c.Crowns.Select(b=>t.transform.InverseTransformPoint(c.transform.TransformPoint(b.Bounds.center))))
            .Select(p=>new Vector2(p.x,p.z)).Distinct().ToArray();

        static Site[] Sites(IslandTerrain t,GrasslandRevitalizationSetup.AuthoringWorld world)
        {
            var s=t.SeasonalDetails.TouchSample;var plants=new List<(Vector2 root,bool grass,float radius)>();
            foreach(var r in t.SeasonalDetails.MeadowBatches)
            {
                var m=r.GetComponent<MeshFilter>().sharedMesh;var v=m.vertices;var roots=m.uv2;var uv=m.uv;
                Check(roots.Length==v.Length&&uv.Length==v.Length,"complete neighboring roots");
                foreach(var g in Enumerable.Range(0,v.Length).Where(i=>Vector2.Distance(roots[i],s.Center)<30).GroupBy(i=>roots[i]))
                    plants.Add((g.Key,g.All(i=>Grass(uv[i].x)),g.Max(i=>Vector2.Distance(new Vector2(v[i].x,v[i].z),g.Key))));
            }
            var ecology=t.World.GetComponentInChildren<WorldWaterEcology>(true);
            var tracks=ecology.RabbitHabitats.SelectMany(h=>h.Track).Select(p=>t.transform.InverseTransformPoint(ecology.transform.TransformPoint(p)))
                .Select(p=>new Vector2(p.x,p.z)).ToArray();
            var random=new System.Random(221047);var candidates=new List<(Site site,double rank)>();
            for(float x=s.Center.x-20;x<s.Center.x+20;x+=1.7f)for(float z=s.Center.y-20;z<s.Center.y+20;z+=1.7f)
            {
                var p=new Vector2(x+(float)random.NextDouble()*1.4f,z+(float)random.NextDouble()*1.4f);
                double rank=random.NextDouble();float phase=(float)random.NextDouble()*Mathf.PI*2;
                if(Vector2.Distance(p,s.Center)+2.8f>s.AreaRadius-s.Feather*.35f
                    ||GrasslandIslandMeadowSetup.RouteDistance(p)<3.65f||!BiomeIslandLayout.Contains(p.x,p.y,20)
                    ||BiomeIslandLayout.At(p.x,p.y)!=IslandBiome.Grassland)continue;
                if(plants.Any(a=>!a.grass&&Vector2.Distance(a.root,p)<a.radius+1.30f)||tracks.Any(q=>Vector2.Distance(q,p)<1.95f))continue;
                float density=plants.Where(a=>a.grass).Sum(a=>Mathf.Exp(-(a.root-p).sqrMagnitude/4));
                if(density<1.8f||density>9||Noise(p,.19f,72)<.36f)continue;
                float floor=world.Floor(p);
                if(!world.Clear(p,floor,1.3f,1.3f)||new[]{Vector2.up,Vector2.down,Vector2.left,Vector2.right}
                    .Any(d=>Mathf.Abs(world.Floor(p+d*1.3f)-floor)>.14f))continue;
                candidates.Add((new Site{Root=p,Phase=phase,Scale=.76f+(float)random.NextDouble()*.24f,Flowers=random.Next(3)!=0},rank));
            }
            var sites=new List<Site>();
            foreach(var c in candidates.OrderBy(c=>c.rank))
            {if(sites.Any(a=>Vector2.Distance(a.Root,c.site.Root)<5.5f))continue;sites.Add(c.site);if(sites.Count==7)break;}
            Check(sites.Count>=4,"bounded small edge groups "+sites.Count);return sites.ToArray();
        }

        public static Edit[] Plan(IslandTerrain t)
        {
            var s=t.SeasonalDetails.TouchSample;
            Check(t.IsValid&&s.ArtVersion==1&&s.Renderers.Length==6&&t.transform.lossyScale==Vector3.one&&t.transform.rotation==Quaternion.identity,"original complete sample");
            var collision=new HashSet<Mesh>(t.GetComponentsInChildren<MeshCollider>(true).Select(c=>c.sharedMesh));
            foreach(var r in s.Renderers)
            {
                var m=r.GetComponent<MeshFilter>().sharedMesh;string p=AssetDatabase.GetAssetPath(m);var metadata=AssetImporter.GetAtPath(p).userData;
                Check(p.StartsWith(BiomeIslandsSetup.Folder+"/",StringComparison.Ordinal)&&!collision.Contains(m)
                    &&r.transform.localToWorldMatrix==t.transform.localToWorldMatrix&&m.subMeshCount==1&&m.tangents.Length==0
                    &&m.blendShapeCount==0&&m.bindposes.Length==0&&m.boneWeights.Length==0,"owned static visual mesh");
                Check(r.sharedMaterials.Length==1&&r.sharedMaterial.shader.name==GrasslandFloraSetup.ShaderName
                    &&GrasslandWildflowerSetup.Done(metadata)&&metadata.Split(';').All(Known.Contains),"preserve custom material / metadata");
                Check(Enumerable.Range(3,5).All(ch=>{var a=new List<Vector4>();m.GetUVs(ch,a);return a.Count==0;}),"preserve custom UV channels");
                Check(Enumerable.Range(0,3).All(ch=>m.GetVertexAttributeDimension((UnityEngine.Rendering.VertexAttribute)
                    ((int)UnityEngine.Rendering.VertexAttribute.TexCoord0+ch))==2),"preserve custom UV dimensions");
            }
            var world=new GrasslandRevitalizationSetup.AuthoringWorld(t,new Vector4(s.Center.x-26,s.Center.y-26,52,52));
            var sites=Sites(t,world);var trees=Trees(t);
            var data=JsonUtility.FromJson<BeachCompositionSetup.Export>(File.ReadAllText(GrasslandCompositionSetup.Source));
            var rocks=data.templates.Single(a=>a.name=="Rocks");var flowers=data.templates.Single(a=>a.name=="Flowers");
            var shrub=AssetDatabase.LoadAssetAtPath<Mesh>(CoastArtSetup.MeshFolder+"/Shrub Cluster.asset");
            Check(shrub!=null&&flowers.positions.Length==1995*3,"original Blender and shrub templates");
            var result=new List<Edit>();
            foreach(var r in s.Renderers)
            {
                var m=r.GetComponent<MeshFilter>().sharedMesh;var v=m.vertices;var n=m.normals;var c=m.colors;
                var roots=m.uv2;var uv=m.uv;var season=m.uv3;var faces=m.triangles;
                Check(new[]{n.Length,c.Length,roots.Length,uv.Length,season.Length}.All(l=>l==v.Length),"complete original channels");
                var points=v.ToList();var normals=n.ToList();var colors=c.ToList();var channels=new[]{uv.ToList(),roots.ToList(),season.ToList()};
                var edit=new Edit{Mesh=m,Changed=new bool[v.Length],Removed=new bool[v.Length],Bounds=m.bounds,Metadata=AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(m)).userData};
                foreach(var g in Enumerable.Range(0,v.Length).GroupBy(i=>roots[i]))
                {
                    var p=g.Key;float d=Vector2.Distance(p,s.Center);if(d>s.AreaRadius-1.5f||!g.All(i=>Grass(uv[i].x)))continue;
                    float edge=sites.Min(a=>Vector2.Distance(a.Root,p));float tree=trees.Select(a=>Vector2.Distance(a,p)).DefaultIfEmpty(100).Min();
                    if(edge<.82f||(tree<3.2f&&Noise(p,.71f,83)<.30f)){foreach(int i in g)edit.Removed[i]=true;edit.Thinned++;continue;}
                    float blend=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.9f,2.6f,edge));
                    blend=Mathf.Max(blend,(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(2,5,tree)))*.6f);
                    blend*=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(s.AreaRadius-s.Feather,s.AreaRadius-1.5f,d));
                    if(blend<=.001f)continue;
                    foreach(int i in g)
                    {
                        float floor=world.Floor(new Vector2(v[i].x,v[i].z))+.018f;float h=Mathf.Max(0,v[i].y-floor);if(h<.001f)continue;
                        float scale=Mathf.Lerp(1,.48f,blend);points[i]=new Vector3(v[i].x,floor+h*scale,v[i].z);
                        normals[i]=new Vector3(n[i].x,n[i].y/scale,n[i].z).normalized;edit.Changed[i]=true;
                    }
                    edit.Lowered++;
                }
                var indices=new List<int>();
                for(int i=0;i<faces.Length;i+=3)
                {
                    int removed=(edit.Removed[faces[i]]?1:0)+(edit.Removed[faces[i+1]]?1:0)+(edit.Removed[faces[i+2]]?1:0);
                    Check(removed==0||removed==3,"whole grass clumps only");if(removed==0)indices.AddRange(new[]{faces[i],faces[i+1],faces[i+2]});
                }
                edit.Sites=r.name.StartsWith("Meadow undergrowth ",StringComparison.Ordinal)?sites.Where(a=>m.name.EndsWith(
                    Mathf.FloorToInt(a.Root.x/128)+" "+Mathf.FloorToInt(a.Root.y/128),StringComparison.Ordinal)).ToArray():Array.Empty<Site>();
                foreach(var site in edit.Sites)
                {
                    float floor=world.Floor(site.Root);var rotation=Quaternion.Euler(0,site.Phase*Mathf.Rad2Deg,0);
                    var weld=new Dictionary<(Vector3,Vector3,Color,Vector2,Vector2),int>();
                    int Vertex(Vector3 q,Vector3 normal,Color color,float weight,float seasonal)
                    {
                        q=rotation*q;normal=rotation*normal;var p=site.Root+new Vector2(q.x,q.z);
                        Check(new Vector2(q.x,q.z).magnitude<=1.16f&&q.y<1.05f&&q.y>-.12f,"complete bounded cluster");
                        var point=new Vector3(p.x,world.Floor(p)+.018f+q.y,p.y);normal.Normalize();color.a=1;
                        var a=new Vector2(weight,site.Phase);var d=new Vector2(floor,seasonal);var key=(point,normal,color,a,d);
                        if(weld.TryGetValue(key,out int id))return id;
                        id=points.Count;weld.Add(key,id);points.Add(point);normals.Add(normal);colors.Add(color);
                        channels[0].Add(a);channels[1].Add(site.Root);channels[2].Add(d);return id;
                    }
                    void Model(float[] positions,float[] sourceNormals,int[] triangles,Vector3 scale,Vector3 offset,int kind)
                    {
                        var map=new int[positions.Length/3];
                        for(int i=0;i<map.Length;i++)
                        {
                            var q=Vector3.Scale(new Vector3(positions[i*3],positions[i*3+1],positions[i*3+2]),scale)+offset;
                            var normal=Vector3.Scale(new Vector3(sourceNormals[i*3],sourceNormals[i*3+1],sourceNormals[i*3+2]),new Vector3(1/scale.x,1/scale.y,1/scale.z)).normalized;
                            float cap=Mathf.Clamp01(q.y/.8f),sample=Noise(site.Root+new Vector2(q.x,q.z),3.7f,12);
                            var color=kind==0?Color.Lerp(new Color(.36f,.49f,.31f),new Color(.59f,.67f,.43f),cap*.75f+sample*.16f)
                                :Color.Lerp(new Color(.55f,.59f,.51f),new Color(.47f,.57f,.37f),Mathf.Clamp01(normal.y)*(.35f+sample*.5f));
                            map[i]=Vertex(q,normal,color,kind==0?Mathf.Clamp01(q.y):0,1);
                        }
                        indices.AddRange(triangles.Select(i=>map[i]));
                    }
                    var sv=shrub.vertices;var sn=shrub.normals;
                    Model(sv.SelectMany(p=>new[]{p.x,p.y,p.z}).ToArray(),sn.SelectMany(p=>new[]{p.x,p.y,p.z}).ToArray(),shrub.triangles,
                        new Vector3(.88f*site.Scale,1.25f*site.Scale,.75f*site.Scale),new Vector3(.24f,0,.18f),0);
                    Model(rocks.positions,rocks.normals,rocks.triangles,new Vector3(.47f,.43f,.43f)*site.Scale,new Vector3(-.32f,-.035f,-.12f),1);
                    for(int blade=0;blade<9;blade++)
                    {
                        float a=blade*2.399963f+site.Phase;var direction=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                        var side=new Vector3(-direction.z,0,direction.x)*.045f;var anchor=direction*(.55f+(blade%3)*.16f);
                        float height=.12f+(blade%4)*.045f;var tip=anchor+direction*.11f+Vector3.up*height;
                        var color=Color.Lerp(new Color(.40f,.52f,.32f),new Color(.52f,.61f,.39f),(blade%3)/2f);
                        int b=Vertex(anchor-side,Vector3.up,color,-4,1),e=Vertex(anchor+side,Vector3.up,color,-4,1);
                        int top=Vertex(tip,Vector3.up,color,-4-height*1.5f,1);indices.AddRange(new[]{b,top,e,b,e,top});
                    }
                    if(site.Flowers)
                    {
                        int branch=((int)(site.Phase*10))%7,start=branch*285;var map=new Dictionary<int,int>();
                        float min=Enumerable.Range(start,285).Min(i=>flowers.positions[i*3+1]);
                        var bottom=Enumerable.Range(start,285).Where(i=>flowers.positions[i*3+1]<min+.001f).ToArray();
                        var anchor=new Vector3(bottom.Average(i=>flowers.positions[i*3]),0,bottom.Average(i=>flowers.positions[i*3+2]));
                        foreach(int i in Enumerable.Range(start,285))
                        {
                            var q=(new Vector3(flowers.positions[i*3],flowers.positions[i*3+1],flowers.positions[i*3+2])-anchor)*.48f+new Vector3(.15f,0,-.69f);
                            var color=new Color(flowers.colors[i*4],flowers.colors[i*4+1],flowers.colors[i*4+2]);
                            bool petal=color.b>color.g||color.r>.95f,pollen=color.r>.85f&&color.g<.7f;
                            if(petal)color=new Color(.96f,.935f,.82f);else if(pollen)color=new Color(.88f,.70f,.36f);
                            map[i]=Vertex(q,new Vector3(flowers.normals[i*3],flowers.normals[i*3+1],flowers.normals[i*3+2]),color,-2-Mathf.Clamp01(q.y),petal||pollen?0:1);
                        }
                        for(int i=0;i<flowers.triangles.Length;i+=3)if(map.ContainsKey(flowers.triangles[i]))
                        {Check(map.ContainsKey(flowers.triangles[i+1])&&map.ContainsKey(flowers.triangles[i+2]),"complete source flower");indices.AddRange(new[]{map[flowers.triangles[i]],map[flowers.triangles[i+1]],map[flowers.triangles[i+2]]});}
                    }
                }
                Check(m.indexFormat==UnityEngine.Rendering.IndexFormat.UInt32||points.Count<=65535,"preserve original index format");
                edit.Vertices=points.ToArray();edit.Normals=normals.ToArray();edit.Colors=colors.ToArray();edit.Channels=channels.Select(a=>a.ToArray()).ToArray();edit.Indices=indices.ToArray();
                foreach(var p in points.Skip(v.Length)){edit.Bounds.Encapsulate(p+Vector3.one*.2f);edit.Bounds.Encapsulate(p-Vector3.one*.2f);}result.Add(edit);
            }
            Check(result.Sum(e=>e.Sites.Length)==sites.Length&&result.Sum(e=>e.Lowered)>0,"assigned edge groups and low transition grass");return result.ToArray();
        }
        static void Clean(string p)=>File.WriteAllText(p,string.Join("\n",File.ReadAllLines(p).Select(l=>l.TrimEnd()))+"\n");
        [MenuItem("Magic Island/Art/Refine Grassland Edge Sample (Explicit)")]
        public static void Rebuild()
        {
            Check(!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling,"Edit Mode required");
            var root=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);var backups=new Dictionary<string,byte[]>();bool saved=false;
            try
            {
                var t=root.GetComponent<IslandTerrain>();Check(t.IsValid&&t.SeasonalDetails.TouchSample.Renderers.Length==6,"complete sample");
                var paths=t.SeasonalDetails.TouchSample.Renderers.Select(r=>AssetDatabase.GetAssetPath(r.GetComponent<MeshFilter>().sharedMesh)).ToArray();var data=paths.Select(p=>AssetImporter.GetAtPath(p).userData).ToArray();
                Check(!data.Any(d=>d.Split(';').Contains(Pending)),"preserve interrupted write");
                if(data.Any(Done)){Check(data.All(Done),"preserve partial migration");saved=true;Debug.Log("Grassland edge sample and later manual edits retained.");return;}
                Physics.SyncTransforms();var plan=Plan(t);
                foreach(string p in paths.SelectMany(p=>new[]{p,p+".meta"}))backups.Add(p,File.ReadAllBytes(p));
                var first=AssetImporter.GetAtPath(paths[0]);first.userData=data[0]+";"+Pending;first.SaveAndReimport();
                foreach(var e in plan)
                {e.Mesh.SetVertices(e.Vertices);e.Mesh.SetNormals(e.Normals);e.Mesh.SetColors(e.Colors);for(int ch=0;ch<3;ch++)e.Mesh.SetUVs(ch,e.Channels[ch]);e.Mesh.SetTriangles(e.Indices,0,false);e.Mesh.bounds=e.Bounds;EditorUtility.SetDirty(e.Mesh);}
                AssetDatabase.SaveAssets();
                foreach(var e in plan.Skip(1).Concat(plan.Take(1)))
                {string p=AssetDatabase.GetAssetPath(e.Mesh);var importer=AssetImporter.GetAtPath(p);importer.userData=e.Metadata+";"+Marker;importer.SaveAndReimport();Clean(p);Clean(p+".meta");}
                saved=true;Debug.Log("Grassland edge sample baked: "+plan.Sum(e=>e.Sites.Length)+" shrub/stone/grass groups, "+plan.Sum(e=>e.Thinned)+" thinned grass roots, "+plan.Sum(e=>e.Lowered)+" lowered transition roots.");
            }
            finally
            {PrefabUtility.UnloadPrefabContents(root);if(!saved){foreach(var p in backups)File.WriteAllBytes(p.Key,p.Value);foreach(string p in backups.Keys.Where(p=>!p.EndsWith(".meta",StringComparison.Ordinal)))AssetDatabase.ImportAsset(p,ImportAssetOptions.ForceUpdate);}}
        }
    }
}
