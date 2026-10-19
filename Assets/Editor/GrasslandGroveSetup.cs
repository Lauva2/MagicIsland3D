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
    public static class GrasslandGroveSetup
    {
        public const string Group="Grassland groves and clearings",Marker="MagicIsland.GrasslandGrove.v1";
        public const string Folder=BiomeIslandsSetup.Folder;
        public sealed class CrownEdit {public Mesh Mesh;public Vector3[] Vertices,Normals;public Color[] Colors;public bool[] Changed;public Vector2[] Centers;}
        public sealed class Site {public Vector2 P;public float Phase,Scale;public int Kind;}
        public static void Check(bool ok,string why){if(!ok)throw new InvalidOperationException("Grassland groves: "+why);}
        public static CrownEdit[] Crowns(IslandTerrain terrain)
        {
            var edits=new List<CrownEdit>();var collision=new HashSet<Mesh>(terrain.GetComponentsInChildren<MeshCollider>(true).Select(c=>c.sharedMesh));
            foreach(var f in terrain.World.Scenery.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.name.StartsWith("Landscape ",StringComparison.Ordinal)))
            {
                var mesh=f.sharedMesh;var v=mesh.vertices;var c=mesh.colors;var n=mesh.normals;var change=new bool[v.Length];var centers=new List<Vector2>();
                Check(v.Length==c.Length&&n.Length==v.Length,"complete original crown channels");
                foreach(var part in BiomePolishSetup.Components(mesh))
                {
                    if(part.Length!=70||part.Any(i=>c[i].g-c[i].r<.065f))continue;
                    var bounds=new Bounds(v[part[0]],Vector3.zero);foreach(int i in part)bounds.Encapsulate(v[i]);var p=new Vector2(bounds.center.x,bounds.center.z);
                    if(bounds.size.y<2||p.y<270||p.y>760||part.Any(i=>BiomeIslandLayout.At(v[i].x,v[i].z)!=IslandBiome.Grassland||!BiomeIslandLayout.Contains(v[i].x,v[i].z,35)))continue;
                    Check(f.transform.localToWorldMatrix==terrain.transform.localToWorldMatrix&&!collision.Contains(mesh),"preserve custom transforms / collision mesh");
                    float phase=Mathf.PerlinNoise(p.x*.041f+19,p.y*.037f-8);float width=.98f+.10f*phase,height=.95f+.18f*phase;centers.Add(p);
                    foreach(int i in part)
                    {
                        float cap=Mathf.InverseLerp(bounds.min.y,bounds.max.y,v[i].y);var d=v[i]-bounds.center;
                        v[i]=new Vector3(bounds.center.x+d.x*width,v[i].y+(v[i].y-bounds.min.y)*(height-1),bounds.center.z+d.z*(1.07f-.08f*phase));
                        var tint=Color.Lerp(new Color(.36f,.54f,.32f),new Color(.61f,.72f,.43f),cap*.8f+phase*.2f);tint.a=c[i].a;c[i]=Color.Lerp(c[i],tint,.34f);change[i]=true;
                    }
                }
                if(centers.Count==0)continue;
                var path=AssetDatabase.GetAssetPath(mesh);Check(path.StartsWith(Folder+"/",StringComparison.Ordinal)&&string.IsNullOrEmpty(AssetImporter.GetAtPath(path).userData),"preserve custom crown metadata "+path);
                var copy=Object.Instantiate(mesh);try{copy.vertices=v;copy.RecalculateNormals();var normals=copy.normals;for(int i=0;i<n.Length;i++)if(change[i])n[i]=normals[i];}finally{Object.DestroyImmediate(copy);}
                edits.Add(new CrownEdit{Mesh=mesh,Vertices=v,Normals=n,Colors=c,Changed=change,Centers=centers.ToArray()});
            }
            Check(edits.Count>0,"existing grassland crowns required");return edits.ToArray();
        }
        public static Vector2[] Anchors(CrownEdit[] crowns)
        {
            var result=new List<Vector2>();foreach(var p in crowns.SelectMany(e=>e.Centers).OrderBy(p=>p.y).ThenBy(p=>p.x))if(result.All(q=>Vector2.Distance(q,p)>7))result.Add(p);return result.ToArray();
        }
        public static Site[] Sites(IslandTerrain terrain,GrasslandRevitalizationSetup.AuthoringWorld world,Vector2[] anchors)
        {
            var random=new System.Random(195823);var result=new List<Site>();
            var occupied=terrain.SeasonalDetails.MeadowBatches.SelectMany(r=>r.GetComponent<MeshFilter>().sharedMesh.uv2).Distinct()
                .GroupBy(p=>new Vector2Int(Mathf.FloorToInt(p.x/4),Mathf.FloorToInt(p.y/4))).ToDictionary(g=>g.Key,g=>g.ToList());
            var ecology=terrain.World.GetComponentInChildren<WorldWaterEcology>(true);var tracks=ecology.RabbitHabitats.SelectMany(h=>h.Track).Select(p=>new Vector2(p.x,p.z)).ToArray();
            bool Near(Vector2 p)
            {
                var key=new Vector2Int(Mathf.FloorToInt(p.x/4),Mathf.FloorToInt(p.y/4));
                for(int x=-1;x<=1;x++)for(int z=-1;z<=1;z++)if(occupied.TryGetValue(key+new Vector2Int(x,z),out var list)&&list.Any(q=>(p-q).sqrMagnitude<1.2f*1.2f))return true;return false;
            }
            bool Add(Vector2 p,int kind,float scale)
            {
                if(BiomeIslandLayout.At(p.x,p.y)!=IslandBiome.Grassland||!BiomeIslandLayout.Contains(p.x,p.y,24)||GrasslandIslandMeadowSetup.RouteDistance(p)<3.25f||Near(p)
                    ||tracks.Any(q=>Vector2.Distance(q,p)<1.9f))return false;
                float floor=world.Floor(p);if(!world.Clear(p,floor,1.3f,kind==1?2:1.7f))return false;
                foreach(var d in new[]{Vector2.one,new Vector2(-1,1),new Vector2(1,-1),-Vector2.one})
                    if(!BiomeIslandLayout.Contains(p.x+d.x,p.y+d.y,22)||GrasslandIslandMeadowSetup.RouteDistance(p+d)<2.6f||Mathf.Abs(world.Floor(p+d)-floor)>.22f)return false;
                result.Add(new Site{P=p,Kind=kind,Scale=scale,Phase=(float)random.NextDouble()*6.283185f});
                var key=new Vector2Int(Mathf.FloorToInt(p.x/4),Mathf.FloorToInt(p.y/4));if(!occupied.TryGetValue(key,out var list)){list=new List<Vector2>();occupied.Add(key,list);}list.Add(p);return true;
            }
            foreach(var center in anchors)
                for(int i=0;i<85;i++)
                {
                    float a=(float)random.NextDouble()*6.283185f,r=2+(float)random.NextDouble()*6.5f;var p=center+new Vector2(Mathf.Cos(a)*r,Mathf.Sin(a)*r*.8f);
                    int kind=i%6==0?1:i%5==0?2:i%3==0?3:0;Add(p,kind,.85f+(float)random.NextDouble()*.35f);
                }
            // Recognizable flower slopes and three small path-side moss/flower clearings.
            foreach(var bed in new[]{new Vector4(-176,570,18,25),new Vector4(-79,548,17,22),new Vector4(-87,414,16,20)})
                for(int i=0;i<460;i++)
                {
                    float x=(float)random.NextDouble()*2-1,z=(float)random.NextDouble()*2-1;
                    if(x*x+z*z>1+.12f*Mathf.Sin(x*9+z*7))continue;var p=new Vector2(bed.x+x*bed.z,bed.y+z*bed.w);
                    Add(p,i%8==0?2:i%3==0?0:1,.8f+(float)random.NextDouble()*.4f);
                }
            foreach(var center in new[]{new Vector2(-110,478),new Vector2(-112,537),new Vector2(-88,612)})
                for(int i=0;i<65;i++)
                {
                    float a=(float)random.NextDouble()*6.283185f,r=(float)random.NextDouble()*5.5f;
                    Add(center+new Vector2(Mathf.Cos(a)*r,Mathf.Sin(a)*r),i%5==0?1:i%3==0?2:0,.9f+(float)random.NextDouble()*.25f);
                }
            Check(result.Count>=100&&result.Count<4000,"bounded understory pockets "+result.Count);return result.ToArray();
        }
        static void AddModel(GrasslandIslandMeadowSetup.Batch b,BeachCompositionSetup.Template t,Site site,GrasslandRevitalizationSetup.AuthoringWorld world,Vector3 scale,Vector2 offset,bool shrub=false)
        {
            int first=b.V.Count;float floor=world.Floor(site.P);var rotation=Quaternion.Euler(0,site.Phase*Mathf.Rad2Deg,0);
            for(int i=0;i<t.positions.Length/3;i++)
            {
                var q=rotation*Vector3.Scale(new Vector3(t.positions[i*3],t.positions[i*3+1],t.positions[i*3+2]),scale)+new Vector3(offset.x,0,offset.y);var p=site.P+new Vector2(q.x,q.z);
                Check(new Vector2(q.x,q.z).magnitude<=1.18f,"full root-cut footprint");var n=rotation*Vector3.Scale(new Vector3(t.normals[i*3],t.normals[i*3+1],t.normals[i*3+2]),new Vector3(1/scale.x,1/scale.y,1/scale.z));
                var c=new Color(t.colors[i*4],t.colors[i*4+1],t.colors[i*4+2],1);
                if(shrub)c=Color.Lerp(new Color(.33f,.50f,.29f),new Color(.57f,.67f,.38f),Mathf.Clamp01(q.y/1.4f));
                else if(t.name=="Rocks")c=Color.Lerp(c,new Color(.44f,.55f,.35f),.24f);
                b.V.Add(new Vector3(p.x,world.Floor(p)+.018f+q.y,p.y));b.N.Add(n.sqrMagnitude>.001f?n.normalized:Vector3.up);b.C.Add(c);
                b.A.Add(new Vector2(t.name=="Rocks"?0:Mathf.Clamp01(q.y),site.Phase));b.B.Add(site.P);b.D.Add(new Vector2(floor,c.g>c.r?1:0));
            }
            b.T.AddRange(t.triangles.Select(i=>i+first));
        }
        public static Dictionary<string,Mesh> Prepare(GrasslandRevitalizationSetup.AuthoringWorld world,Site[] sites)
        {
            var data=JsonUtility.FromJson<BeachCompositionSetup.Export>(File.ReadAllText(GrasslandCompositionSetup.Source));Check(data?.templates?.Length==3,"Blender templates");
            var shrub=AssetDatabase.LoadAssetAtPath<Mesh>(CoastArtSetup.MeshFolder+"/Shrub Cluster.asset");Check(shrub!=null,"shared shrub template");
            var shrubData=new BeachCompositionSetup.Template{name="Shrub",positions=shrub.vertices.SelectMany(p=>new[]{p.x,p.y,p.z}).ToArray(),normals=shrub.normals.SelectMany(n=>new[]{n.x,n.y,n.z}).ToArray(),colors=shrub.vertices.SelectMany(_=>new[]{.4f,.6f,.3f,1f}).ToArray(),triangles=shrub.triangles};
            var batches=new Dictionary<string,GrasslandIslandMeadowSetup.Batch>();
            foreach(var s in sites)
            {
                string key="Grove understory "+Mathf.FloorToInt(s.P.x/128)+" "+Mathf.FloorToInt(s.P.y/128);
                if(!batches.TryGetValue(key,out var b)){b=new GrasslandIslandMeadowSetup.Batch();batches.Add(key,b);}
                if(s.Kind==1)AddModel(b,data.templates.Single(t=>t.name=="Flowers"),s,world,Vector3.one*(1.25f*s.Scale),Vector2.zero);
                else if(s.Kind==2)
                {
                    AddModel(b,data.templates.Single(t=>t.name=="Rocks"),s,world,Vector3.one*.72f,new Vector2(-.30f,-.1f));
                    AddModel(b,shrubData,s,world,new Vector3(.33f,.85f*s.Scale,.33f),new Vector2(.35f,.22f),true);
                }
                else if(s.Kind==3)
                {
                    for(int leaf=0;leaf<11;leaf++)
                    {
                        float a=s.Phase+leaf*2.399963f,r=.15f+(leaf%4)*.22f;var p=s.P+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*r;float y=world.Floor(p)+.022f;
                        var d=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*(.10f+(leaf%3)*.025f);var side=new Vector3(-d.z,0,d.x)*.46f;var center=new Vector3(p.x,y,p.y);int start=b.V.Count;
                        foreach(var v in new[]{center-d,center+side,center+d+Vector3.up*.018f,center-side})
                        {b.V.Add(v);b.N.Add(Vector3.up);b.C.Add(Color.Lerp(new Color(.45f,.48f,.29f),new Color(.65f,.60f,.36f),(leaf%4)/3f));b.A.Add(Vector2.zero);b.B.Add(s.P);b.D.Add(new Vector2(world.Floor(s.P),0));}
                        b.T.AddRange(new[]{start,start+1,start+2,start,start+2,start+3});
                    }
                }
                else
                {
                    // Taller grass at tree edges, with the same bounded root metadata and wind.
                    for(int blade=0;blade<13;blade++)
                    {
                        float a=s.Phase+blade*2.399963f;var d=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));var side=new Vector3(-d.z,0,d.x)*(.065f+(blade%3)*.015f);var root=d*(.14f+(blade%4)*.12f);var tip=root+d*.28f+Vector3.up*(.48f+(blade%4)*.12f)*s.Scale;int start=b.V.Count;
                        foreach(var q in new[]{root-side,root+side,tip})
                        {var p=s.P+new Vector2(q.x,q.z);b.V.Add(new Vector3(p.x,world.Floor(p)+q.y+.018f,p.y));b.N.Add(new Vector3(-d.x*.3f,.9f,-d.z*.3f).normalized);b.C.Add(Color.Lerp(new Color(.32f,.50f,.28f),new Color(.54f,.65f,.35f),(blade%4)/3f));b.A.Add(new Vector2(q.y>0?.7f:0,s.Phase));b.B.Add(s.P);b.D.Add(new Vector2(world.Floor(s.P),1));}
                        b.T.AddRange(new[]{start,start+1,start+2,start,start+2,start+1});
                    }
                }
            }
            return batches.ToDictionary(p=>p.Key,p=>p.Value.Mesh(p.Key));
        }
        static void Clean(string p)=>File.WriteAllText(p,string.Join("\n",File.ReadAllLines(p).Select(l=>l.TrimEnd()))+"\n");
        [MenuItem("Magic Island/Art/Refine Grassland Groves And Clearings (Explicit)")]
        public static void Rebuild()
        {
            Check(!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling,"Edit Mode required");
            var root=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);var meshes=new Dictionary<string,Mesh>();var backups=new Dictionary<string,byte[]>();var created=new List<string>();bool saved=false;
            try
            {
                var terrain=root.GetComponent<IslandTerrain>();Check(terrain.IsValid&&terrain.World.ArtVersion==3,"authored islands");var parent=terrain.World.Scenery;var old=parent.Find(Group);
                if(old!=null){Check(old.GetComponentsInChildren<MeshFilter>(true).Length>0&&old.GetComponentsInChildren<MeshFilter>(true).All(f=>f.sharedMesh!=null),"preserve incomplete/custom grove");Debug.Log("Grassland groves and manual edits retained.");return;}
                Check(parent.localToWorldMatrix==terrain.transform.localToWorldMatrix,"preserve custom scenery transform");Physics.SyncTransforms();
                var crowns=Crowns(terrain);var world=new GrasslandRevitalizationSetup.AuthoringWorld(terrain,new Vector4(-300,230,600,600));var anchors=Anchors(crowns);var sites=Sites(terrain,world,anchors);meshes=Prepare(world,sites);
                Check(terrain.SeasonalDetails.MeadowBatches.Length+meshes.Count<=64,"bounded ground cover batches");var material=AssetDatabase.LoadAssetAtPath<Material>(GrasslandRevitalizationSetup.MaterialFolder+"Living Meadow Cover.mat");Check(material!=null&&!ShaderUtil.ShaderHasError(material.shader),"compiled existing shared material");
                foreach(string key in meshes.Keys){string p=Folder+"/"+key+".asset";Check(!File.Exists(p)&&!File.Exists(p+".meta"),"preserve orphan asset "+p);}
                foreach(string p in crowns.Select(e=>AssetDatabase.GetAssetPath(e.Mesh)).SelectMany(p=>new[]{p,p+".meta"}).Concat(new[]{TerrainSetup.PrefabPath}))backups.Add(p,File.ReadAllBytes(p));
                foreach(var pair in meshes){string p=Folder+"/"+pair.Key+".asset";created.Add(p);AssetDatabase.CreateAsset(pair.Value,p);}
                var group=new GameObject(Group);group.layer=2;group.transform.SetParent(parent,false);var renderers=terrain.SeasonalDetails.MeadowBatches.ToList();
                foreach(var pair in meshes)
                {
                    var go=new GameObject(pair.Key,typeof(MeshFilter),typeof(MeshRenderer));go.layer=2;go.transform.SetParent(group.transform,false);go.GetComponent<MeshFilter>().sharedMesh=pair.Value;var r=go.GetComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;renderers.Add(r);
                }
                terrain.SeasonalDetails.MeadowBatches=renderers.ToArray();foreach(var e in crowns){e.Mesh.vertices=e.Vertices;e.Mesh.normals=e.Normals;e.Mesh.colors=e.Colors;e.Mesh.RecalculateBounds();EditorUtility.SetDirty(e.Mesh);}
                Check(terrain.IsValid,"valid complete owner");Check(PrefabUtility.SaveAsPrefabAsset(root,TerrainSetup.PrefabPath)!=null,"prefab save");AssetDatabase.SaveAssets();
                foreach(string p in crowns.Select(e=>AssetDatabase.GetAssetPath(e.Mesh)).Concat(created)){var importer=AssetImporter.GetAtPath(p);importer.userData=Marker;importer.SaveAndReimport();Clean(p);Clean(p+".meta");}Clean(TerrainSetup.PrefabPath);saved=true;
                Directory.CreateDirectory("Logs/GrasslandGrove");File.WriteAllLines("Logs/GrasslandGrove/Anchors.txt",anchors.Select(p=>p.ToString("F2")));
                Debug.Log("Grassland groves baked: "+crowns.Sum(e=>e.Centers.Length)+" existing crowns, "+anchors.Length+" tree groups, "+sites.Length+" understory pockets (flowers "+sites.Count(s=>s.Kind==1)+", shrubs "+sites.Count(s=>s.Kind==2)+", leaf litter "+sites.Count(s=>s.Kind==3)+"), "+meshes.Count+" shared batches.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);if(!saved){foreach(var p in backups)File.WriteAllBytes(p.Key,p.Value);foreach(string p in created)AssetDatabase.DeleteAsset(p);foreach(string p in backups.Keys.Where(p=>!p.EndsWith(".meta",StringComparison.Ordinal)))AssetDatabase.ImportAsset(p,ImportAssetOptions.ForceUpdate);}
                foreach(var m in meshes.Values)if(m!=null&&!EditorUtility.IsPersistent(m))Object.DestroyImmediate(m);
            }
        }
    }
}
