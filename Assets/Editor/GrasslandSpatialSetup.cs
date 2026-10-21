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
    public static class GrasslandSpatialSetup
    {
        public const string Marker="MagicIsland.GrasslandSpatial.v1",Pending="MagicIsland.GrasslandSpatial.Pending.v1";
        static readonly HashSet<string> Known=new HashSet<string>
        {
            GrasslandRevitalizationSetup.Marker,GrasslandIslandMeadowSetup.Marker,GrasslandGroveSetup.Marker,
            GrasslandFloraSetup.Marker,GrasslandGroundLayersSetup.Marker,GrasslandShrubStoneSetup.Marker,
            GrasslandTouchSetup.Marker,GrasslandReadabilitySetup.Marker,GrasslandPatchSetup.Marker,
            GrasslandBlendSetup.Marker,GrasslandWildflowerSetup.Marker,GrasslandEdgeSetup.Marker,GrasslandPathEdgeSetup.Marker,GrasslandSampleFinishSetup.Marker
        };
        [Serializable] public sealed class Site { public Vector2 Root;public float Phase,Scale;public bool Flowers;public int Kind,Band; }
        public sealed class Edit
        {
            public Mesh Mesh;public Vector3[] Vertices,Normals;public Color[] Colors;public Vector2[][] Channels;
            public int[] Indices;public bool[] Changed,Removed;public Bounds Bounds;public string Metadata;
            public Site[] Sites;public int Thinned,Lowered,Raised;
        }
        public static bool Done(string data)=>data.Split(';').Contains(Marker);
        public static void Check(bool ok,string why){if(!ok)throw new InvalidOperationException("Grassland spatial: "+why);}
        static float Noise(Vector2 p,float scale,float salt)=>GrasslandGroundLayersSetup.Noise(p,scale,salt);
        static bool Grass(float weight)=>weight>=-5&&weight<=-4;
        public static Vector2[] Trees(IslandTerrain t)=>t.GetComponentsInChildren<LandscapeCrownOccluder>(true)
            .SelectMany(c=>c.Crowns.Select(b=>t.transform.InverseTransformPoint(c.transform.TransformPoint(b.Bounds.center))))
            .Select(p=>new Vector2(p.x,p.z)).Distinct().ToArray();

        // Broad art direction zones; actual roots are jittered, seeded and checked against authored scenery.
        static readonly Vector4[] Zones={new Vector4(-10.5f,1,6,4.5f),new Vector4(12,-2,6,4),new Vector4(-7,14.5f,4.6f,3.8f)};
        public static float Field(Vector2 p,Vector2 center,out int band)
        {
            float best=100;band=0;
            for(int i=0;i<Zones.Length;i++)
            {
                var a=Zones[i];var d=p-center-new Vector2(a.x,a.y);
                float distance=new Vector2(d.x/a.z,d.y/a.w).magnitude+(Noise(p,.25f,132)-.5f)*.24f;
                if(distance<best){best=distance;band=i;}
            }
            return best;
        }
        public static Vector2 ZoneCenter(Vector2 center,int band)=>center+new Vector2(Zones[band].x,Zones[band].y);
        static Site[] Sites(IslandTerrain t,GrasslandRevitalizationSetup.AuthoringWorld world)
        {
            var s=t.SeasonalDetails.TouchSample;var plants=new List<(Vector2 root,bool grass,float radius)>();
            foreach(var r in t.SeasonalDetails.MeadowBatches)
            {
                var m=r.GetComponent<MeshFilter>().sharedMesh;var v=m.vertices;var roots=m.uv2;var uv=m.uv;
                Check(roots.Length==v.Length&&uv.Length==v.Length,"complete neighboring roots");
                foreach(var g in m.triangles.Distinct().Where(i=>Vector2.Distance(roots[i],s.Center)<30).GroupBy(i=>roots[i]))
                    plants.Add((g.Key,g.All(i=>Grass(uv[i].x)),g.Max(i=>Vector2.Distance(new Vector2(v[i].x,v[i].z),g.Key))));
            }
            var ecology=t.World.GetComponentInChildren<WorldWaterEcology>(true);
            var tracks=ecology.RabbitHabitats.SelectMany(h=>h.Track).Select(p=>t.transform.InverseTransformPoint(ecology.transform.TransformPoint(p)))
                .Select(p=>new Vector2(p.x,p.z)).ToArray();
            var random=new System.Random(231017);var candidates=new List<(Site site,double rank)>();
            for(float x=s.Center.x-22;x<s.Center.x+22;x+=.82f)for(float z=s.Center.y-22;z<s.Center.y+22;z+=.82f)
            {
                var p=new Vector2(x+(float)random.NextDouble()*.72f,z+(float)random.NextDouble()*.72f);
                double rank=random.NextDouble();float phase=(float)random.NextDouble()*Mathf.PI*2;
                float field=Field(p,s.Center,out int band);
                if(Vector2.Distance(p,s.Center)+2.3f>s.AreaRadius-s.Feather*.25f
                    ||GrasslandIslandMeadowSetup.RouteDistance(p)<3.65f||!BiomeIslandLayout.Contains(p.x,p.y,20)
                    ||BiomeIslandLayout.At(p.x,p.y)!=IslandBiome.Grassland||field>1.4f)continue;
                if(plants.Any(a=>!a.grass&&Vector2.Distance(a.root,p)<a.radius+1.3f)||tracks.Any(q=>Vector2.Distance(q,p)<1.95f))continue;
                float floor=world.Floor(p);
                if(!world.Clear(p,floor,1.3f,1.65f)||new[]{Vector2.up,Vector2.down,Vector2.left,Vector2.right}
                    .Any(d=>Mathf.Abs(world.Floor(p+d*1.3f)-floor)>.14f))continue;
                int kind=field<.78f?0:field<1.18f&&p.y<ZoneCenter(s.Center,band).y+1.5f?1:2;
                if(kind!=0&&plants.Any(a=>a.grass&&Vector2.Distance(a.root,p)<.42f))continue;
                candidates.Add((new Site{Root=p,Phase=phase,Scale=.83f+(float)random.NextDouble()*.24f,Flowers=kind==1,Kind=kind,Band=band},rank));
            }
            var sites=new List<Site>();
            foreach(int kind in new[]{0,1,2})foreach(var c in candidates.Where(c=>c.site.Kind==kind).OrderBy(c=>c.rank))
            {
                if(sites.Count(a=>a.Kind==kind&&a.Band==c.site.Band)>=(kind==0?2:kind==1?12:22))continue;
                if(sites.Any(a=>Vector2.Distance(a.Root,c.site.Root)<(a.Kind==0||kind==0?2.6f:kind==1||a.Kind==1?1.15f:.62f)))continue;
                sites.Add(c.site);
            }
            Check(sites.Count(a=>a.Kind==0)>=2&&sites.Count(a=>a.Kind==1)>=6&&sites.Count(a=>a.Kind==2)>=12,"safe spatial groups "+string.Join(",",sites.GroupBy(a=>a.Kind).Select(g=>g.Key+":"+g.Count())));
            return sites.ToArray();
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
                    &&GrasslandSampleFinishSetup.Done(metadata)&&metadata.Split(';').All(Known.Contains),"preserve custom material / metadata");
                Check(Enumerable.Range(3,5).All(ch=>{var a=new List<Vector4>();m.GetUVs(ch,a);return a.Count==0;}),"preserve custom UV channels");
                Check(Enumerable.Range(0,3).All(ch=>m.GetVertexAttributeDimension((UnityEngine.Rendering.VertexAttribute)
                    ((int)UnityEngine.Rendering.VertexAttribute.TexCoord0+ch))==2),"preserve custom UV dimensions");
            }
            var world=new GrasslandRevitalizationSetup.AuthoringWorld(t,new Vector4(s.Center.x-26,s.Center.y-26,52,52));
            var sites=Sites(t,world);
            var data=JsonUtility.FromJson<BeachCompositionSetup.Export>(File.ReadAllText(GrasslandCompositionSetup.Source));
            var flowers=data.templates.Single(a=>a.name=="Flowers");
            var shrub=AssetDatabase.LoadAssetAtPath<Mesh>(CoastArtSetup.MeshFolder+"/Shrub Cluster.asset");
            Check(shrub!=null&&flowers.positions.Length==1995*3,"original Blender and shrub templates");
            Check(shrub.vertexCount==3234,"original seven-lobe shrub template");
            // A three-lobe variant keeps the middle layer light and preserves the original UInt16 batch budget.
            var sv=shrub.vertices;var sn=shrub.normals;
            var picked=Enumerable.Range(0,sv.Length).Where(i=>i/462==0||i/462==2||i/462==4).ToArray();
            var remap=picked.Select((id,index)=>(id,index)).ToDictionary(a=>a.id,a=>a.index);var shrubFaces=new List<int>();
            var sourceFaces=shrub.triangles;
            for(int i=0;i<sourceFaces.Length;i+=3)if(remap.ContainsKey(sourceFaces[i]))
            {Check(remap.ContainsKey(sourceFaces[i+1])&&remap.ContainsKey(sourceFaces[i+2]),"whole source lobes");shrubFaces.AddRange(new[]{remap[sourceFaces[i]],remap[sourceFaces[i+1]],remap[sourceFaces[i+2]]});}
            var result=new List<Edit>();
            foreach(var r in s.Renderers)
            {
                var m=r.GetComponent<MeshFilter>().sharedMesh;var v=m.vertices;var n=m.normals;var c=m.colors;
                var roots=m.uv2;var uv=m.uv;var season=m.uv3;var faces=m.triangles;
                Check(new[]{n.Length,c.Length,roots.Length,uv.Length,season.Length}.All(l=>l==v.Length),"complete original channels");
                var points=v.ToList();var normals=n.ToList();var colors=c.ToList();var channels=new[]{uv.ToList(),roots.ToList(),season.ToList()};
                var edit=new Edit{Mesh=m,Changed=new bool[v.Length],Removed=new bool[v.Length],Bounds=m.bounds,Metadata=AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(m)).userData};
                foreach(var g in faces.Distinct().GroupBy(i=>roots[i]))
                {
                    var p=g.Key;float d=Vector2.Distance(p,s.Center);if(d>s.AreaRadius-1.5f)continue;
                    float feather=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(s.AreaRadius-s.Feather,s.AreaRadius-1.5f,d));
                    float field=Field(p,s.Center,out int band);bool grass=g.All(i=>Grass(uv[i].x));
                    if(grass&&GrasslandIslandMeadowSetup.RouteDistance(p)<3.65f)continue;
                    // Preserve road-shoulder grass and the normal travel corridor; thin whole roots only in intervening glades.
                    if(grass&&GrasslandIslandMeadowSetup.RouteDistance(p)>3.65f&&feather>.98f&&field>1.4f&&Noise(p,.91f,140)<.23f)
                    {foreach(int i in g)edit.Removed[i]=true;edit.Thinned++;continue;}
                    float core=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.68f,1.4f,field));
                    bool mixed=g.Any(i=>Grass(uv[i].x))&&g.Any(i=>uv[i].x>0);
                    if(!grass&&!mixed)continue;bool changed=false;
                    if(mixed&&core<=.001f||!world.Clear(p,world.Floor(p),1.3f,1.65f))continue;
                    foreach(int i in g)
                    {
                        bool leaf=mixed&&uv[i].x>0;
                        if(!Grass(uv[i].x)&&!leaf)continue;
                        float floor=world.Floor(new Vector2(v[i].x,v[i].z))+.018f;float h=Mathf.Max(0,v[i].y-floor);
                        if(h<.005f)continue;
                        float scale=leaf?Mathf.Lerp(1,1.7f,core*feather):Mathf.Lerp(1,field<1.2f?1.2f:.64f,(core>.01f?core:.65f)*feather);
                        float next=leaf?Mathf.Max(h,Mathf.Min(1.3f,h*scale)):Mathf.Min(.62f,h*scale);if(Mathf.Abs(next-h)<.0001f)continue;
                        points[i]=new Vector3(v[i].x,floor+next,v[i].z);normals[i]=new Vector3(n[i].x,n[i].y/(next/h),n[i].z).normalized;
                        edit.Changed[i]=true;changed=true;
                    }
                    if(changed){if(mixed)edit.Raised++;else edit.Lowered++;}
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
                        Check(new Vector2(q.x,q.z).magnitude<=1.16f&&q.y<1.65f&&q.y>-.12f,"complete bounded cluster");
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
                    if(site.Kind==0)
                        Model(picked.SelectMany(i=>new[]{sv[i].x,sv[i].y,sv[i].z}).ToArray(),picked.SelectMany(i=>new[]{sn[i].x,sn[i].y,sn[i].z}).ToArray(),shrubFaces.ToArray(),
                            new Vector3(.98f*site.Scale,(1.9f+.3f*Mathf.Sin(site.Phase))*site.Scale,.91f*site.Scale),Vector3.zero,0);
                    int blades=site.Kind==2?11:site.Kind==0?7:3;
                    for(int blade=0;blade<blades;blade++)
                    {
                        float a=blade*2.399963f+site.Phase;var direction=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                        var side=new Vector3(-direction.z,0,direction.x)*.045f;var anchor=direction*(site.Kind==0?.68f:.12f+(blade%3)*.13f);
                        float height=site.Kind==2?.20f+(blade%4)*.075f:.11f+(blade%3)*.04f;
                        var middle=anchor+direction*.07f+Vector3.up*height*.58f;var tip=anchor+direction*.22f+Vector3.up*height;
                        var color=Color.Lerp(new Color(.43f,.55f,.35f),new Color(.58f,.66f,.43f),(blade%3)/2f);
                        int b=Vertex(anchor-side,Vector3.up,color,-4,1),e=Vertex(anchor+side,Vector3.up,color,-4,1);
                        int l=Vertex(middle-side*.65f,Vector3.up,color,-4-height*.6f,1),right=Vertex(middle+side*.65f,Vector3.up,color,-4-height*.6f,1);
                        int top=Vertex(tip,Vector3.up,color,-4-height*1.5f,1);
                        indices.AddRange(new[]{b,l,e,e,l,right,l,top,right});
                    }
                    if(site.Flowers)
                    {
                        int branch=((int)(site.Phase*10))%7,start=branch*285;var map=new Dictionary<int,int>();
                        float min=Enumerable.Range(start,285).Min(i=>flowers.positions[i*3+1]);
                        var bottom=Enumerable.Range(start,285).Where(i=>flowers.positions[i*3+1]<min+.001f).ToArray();
                        var anchor=new Vector3(bottom.Average(i=>flowers.positions[i*3]),0,bottom.Average(i=>flowers.positions[i*3+2]));
                        foreach(int i in Enumerable.Range(start,285))
                        {
                            var q=(new Vector3(flowers.positions[i*3],flowers.positions[i*3+1],flowers.positions[i*3+2])-anchor)*.48f+new Vector3(.05f,0,-.12f);
                            var color=new Color(flowers.colors[i*4],flowers.colors[i*4+1],flowers.colors[i*4+2]);
                            bool petal=color.b>color.g||color.r>.95f,pollen=color.r>.85f&&color.g<.7f;
                            if(petal)color=site.Band==2?new Color(.79f,.74f,.84f):site.Band==0?new Color(.94f,.92f,.81f):new Color(.92f,.85f,.60f);else if(pollen)color=new Color(.88f,.70f,.36f);
                            map[i]=Vertex(q,new Vector3(flowers.normals[i*3],flowers.normals[i*3+1],flowers.normals[i*3+2]),color,-2-Mathf.Clamp01(q.y),petal||pollen?0:1);
                        }
                        for(int i=0;i<flowers.triangles.Length;i+=3)if(map.ContainsKey(flowers.triangles[i]))
                        {Check(map.ContainsKey(flowers.triangles[i+1])&&map.ContainsKey(flowers.triangles[i+2]),"complete source flower");indices.AddRange(new[]{map[flowers.triangles[i]],map[flowers.triangles[i+1]],map[flowers.triangles[i+2]]});}
                    }
                }
                Check(m.indexFormat==UnityEngine.Rendering.IndexFormat.UInt32||points.Count<=65535,"preserve original index format "+m.name+" "+points.Count);
                edit.Vertices=points.ToArray();edit.Normals=normals.ToArray();edit.Colors=colors.ToArray();edit.Channels=channels.Select(a=>a.ToArray()).ToArray();edit.Indices=indices.ToArray();
                foreach(var p in points.Where((p,i)=>i>=v.Length||edit.Changed[i])){edit.Bounds.Encapsulate(p+Vector3.one*.2f);edit.Bounds.Encapsulate(p-Vector3.one*.2f);}result.Add(edit);
            }
            Check(result.Sum(e=>e.Sites.Length)==sites.Length&&result.Sum(e=>e.Raised)>0,"assigned spatial groups and raised original shrub layer");return result.ToArray();
        }
        static void Clean(string p)=>File.WriteAllText(p,string.Join("\n",File.ReadAllLines(p).Select(l=>l.TrimEnd()))+"\n");
        [MenuItem("Magic Island/Art/Compose Grassland Spatial Sample (Explicit)")]
        public static void Rebuild()
        {
            Check(!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling,"Edit Mode required");
            var root=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);var backups=new Dictionary<string,byte[]>();bool saved=false;
            try
            {
                var t=root.GetComponent<IslandTerrain>();Check(t.IsValid&&t.SeasonalDetails.TouchSample.Renderers.Length==6,"complete sample");
                var paths=t.SeasonalDetails.TouchSample.Renderers.Select(r=>AssetDatabase.GetAssetPath(r.GetComponent<MeshFilter>().sharedMesh)).ToArray();var data=paths.Select(p=>AssetImporter.GetAtPath(p).userData).ToArray();
                Check(!data.Any(d=>d.Split(';').Contains(Pending)),"preserve interrupted write");
                if(data.Any(Done)){Check(data.All(Done),"preserve partial migration");saved=true;Debug.Log("Grassland spatial sample and later manual edits retained.");return;}
                Physics.SyncTransforms();var plan=Plan(t);
                foreach(string p in paths.SelectMany(p=>new[]{p,p+".meta"}))backups.Add(p,File.ReadAllBytes(p));
                var first=AssetImporter.GetAtPath(paths[0]);first.userData=data[0]+";"+Pending;first.SaveAndReimport();
                foreach(var e in plan)
                {e.Mesh.SetVertices(e.Vertices);e.Mesh.SetNormals(e.Normals);e.Mesh.SetColors(e.Colors);for(int ch=0;ch<3;ch++)e.Mesh.SetUVs(ch,e.Channels[ch]);e.Mesh.SetTriangles(e.Indices,0,false);e.Mesh.bounds=e.Bounds;EditorUtility.SetDirty(e.Mesh);}
                AssetDatabase.SaveAssets();
                foreach(var e in plan.Skip(1).Concat(plan.Take(1)))
                {string p=AssetDatabase.GetAssetPath(e.Mesh);var importer=AssetImporter.GetAtPath(p);importer.userData=e.Metadata+";"+Marker;importer.SaveAndReimport();Clean(p);Clean(p+".meta");}
                saved=true;Debug.Log("Grassland spatial sample baked: "+plan.Sum(e=>e.Sites.Length)+" clustered shrub/flower/grass groups, "+plan.Sum(e=>e.Thinned)+" thinned grass roots, "+plan.Sum(e=>e.Lowered)+" grass transitions, "+plan.Sum(e=>e.Raised)+" raised shrub roots.");
            }
            finally
            {PrefabUtility.UnloadPrefabContents(root);if(!saved){foreach(var p in backups)File.WriteAllBytes(p.Key,p.Value);foreach(string p in backups.Keys.Where(p=>!p.EndsWith(".meta",StringComparison.Ordinal)))AssetDatabase.ImportAsset(p,ImportAssetOptions.ForceUpdate);}}
        }
    }
}
