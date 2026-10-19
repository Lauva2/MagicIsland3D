using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MagicIsland.Interaction;
using MagicIsland.TerrainSystem;
using UnityEditor;
using UnityEngine;

namespace MagicIsland.EditorTools
{
    // Opt-in art migration of existing combined cover, never a normal Setup operation.
    public static class GrasslandFloraSetup
    {
        public const string Marker="MagicIsland.GrasslandFlora.v1";
        public const string MaterialPath="Assets/Art/Materials/Terrain/Natural Meadow Cover.mat";
        public const string ShaderName="MagicIsland/Meadow Flora";
        public sealed class Edit
        {
            public Mesh Mesh;public Vector3[] Vertices,Normals;public Color[] Colors;public Vector2[] Wind;
            public bool[] Changed;public int Flowers,Grass;public string Metadata;
        }
        static readonly Color Cream=new Color(.99f,.91f,.65f),Lilac=new Color(.68f,.60f,.86f),Pollen=new Color(.90f,.59f,.23f);
        public static void Check(bool ok,string why){if(!ok)throw new InvalidOperationException("Grassland flora: "+why);}
        static bool Same(Color a,Color b)=>Mathf.Abs(a.r-b.r)+Mathf.Abs(a.g-b.g)+Mathf.Abs(a.b-b.b)<.0001f;
        static float Noise(Vector2 p,float salt)=>Mathf.Clamp01(Mathf.PerlinNoise(p.x*.173f+salt,p.y*.157f-salt));
        public static bool Finished(string data)=>data.Split(';').Contains(Marker);
        public static Color PetalColor(Vector2 p)
        {
            var anchors=new[]{new Vector2(-176,570),new Vector2(-79,548),new Vector2(-87,414)};
            int nearest=Enumerable.Range(0,3).OrderBy(i=>Vector2.SqrMagnitude(p-anchors[i])).First();
            // Large, irregular color communities, with a few contrasting clumps at their edges.
            float distance=Vector2.Distance(p,anchors[nearest]);
            int kind=distance<34+Noise(p*.13f,38)*11?nearest:Mathf.Min(2,Mathf.FloorToInt(Noise(p*.21f,71)*3));
            if(Noise(p,94)>.74f)kind=(kind+1)%3;
            var colors=new[]{new Color(.96f,.935f,.82f),new Color(.79f,.73f,.88f),new Color(.95f,.87f,.60f)};
            return colors[kind]*Mathf.Lerp(.96f,1,Noise(p,19));
        }
        public static Edit[] Plan(IslandTerrain terrain)
        {
            var cover=terrain.SeasonalDetails.MeadowBatches;
            Check(cover.Length>0&&cover.Length<=64,"existing bounded cover required");
            var colliders=new HashSet<Mesh>(terrain.GetComponentsInChildren<MeshCollider>(true).Select(c=>c.sharedMesh));
            var world=new GrasslandRevitalizationSetup.AuthoringWorld(terrain,new Vector4(-300,230,600,600));
            var floorCache=new Dictionary<Vector2,float>();
            float Floor(Vector2 p){if(!floorCache.TryGetValue(p,out float y)){y=world.Floor(p);floorCache.Add(p,y);}return y;}
            var edits=new List<Edit>();
            foreach(var renderer in cover)
            {
                var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;string path=AssetDatabase.GetAssetPath(mesh);
                Check(path.StartsWith(BiomeIslandsSetup.Folder+"/",StringComparison.Ordinal)&&!colliders.Contains(mesh),"original owned visual mesh "+path);
                Check(renderer.transform.localToWorldMatrix==terrain.transform.localToWorldMatrix&&renderer.sharedMaterial.shader.name=="MagicIsland/Meadow Cover","preserve custom material/transform");
                string metadata=AssetImporter.GetAtPath(path).userData;
                Check(string.IsNullOrEmpty(metadata)||metadata==GrasslandIslandMeadowSetup.Marker||metadata==GrasslandGroveSetup.Marker,"preserve custom metadata "+path);
                var v=mesh.vertices;var c=mesh.colors;var n=mesh.normals;var uv=mesh.uv;var roots=mesh.uv2;var changed=new bool[v.Length];int flowers=0,grass=0;
                Check(v.Length==c.Length&&v.Length==n.Length&&v.Length==uv.Length&&v.Length==roots.Length,"complete root/visual channels");
                foreach(var group in Enumerable.Range(0,v.Length).GroupBy(i=>roots[i]))
                {
                    var p=group.Key;var ids=group.ToArray();
                    if(BiomeIslandLayout.At(p.x,p.y)!=IslandBiome.Grassland||!BiomeIslandLayout.Contains(p.x,p.y,20))continue;
                    bool flower=ids.Any(i=>Same(c[i],Pollen))&&ids.Count(i=>Same(c[i],Cream)||Same(c[i],Lilac))>100;
                    bool blade=!flower&&ids.Length<=650&&ids.All(i=>c[i].g-c[i].r>.08f)&&ids.Any(i=>uv[i].x>.1f);
                    if(!flower&&!blade)continue;
                    float radius=ids.Max(i=>Vector2.Distance(new Vector2(v[i].x,v[i].z),p));
                    Check(radius<=1.181f&&uv[ids[0]].x>=0,"bounded original clump");
                    float phase=uv[ids[0]].y-(mesh.name.StartsWith("Art Living meadow",StringComparison.Ordinal)?p.x*.17f:0);
                    var rotation=Quaternion.Euler(0,phase*Mathf.Rad2Deg,0);var inverse=Quaternion.Inverse(rotation);
                    float scale=radius/.62500047f,shrink=.85f+Noise(p,12)*.15f,height=.84f+Noise(p,31)*.16f;
                    var tint=PetalColor(p);var heads=new Vector3[7];
                    for(int k=0;k<7;k++){float a=k*2.399963f,r=.06f+(k%3)*.15f;heads[k]=new Vector3(Mathf.Cos(a)*r,.56f+(k%4)*.13f,Mathf.Sin(a)*r);}
                    foreach(int i in ids)
                    {
                        var q=v[i];var old=new Vector2(q.x,q.z);float baseY=Floor(old)+.018f;float y=q.y-baseY;
                        var offset=new Vector3(q.x-p.x,y,q.z-p.y);
                        bool petal=flower&&(Same(c[i],Cream)||Same(c[i],Lilac));float petalWidth=1;
                        if(petal)
                        {
                            var canonical=inverse*offset/scale;int head=Enumerable.Range(0,7).OrderBy(k=>
                                (new Vector2(canonical.x-heads[k].x,canonical.z-heads[k].z)).sqrMagnitude+16*Mathf.Pow(canonical.y-heads[k].y,2)).First();
                            var delta=canonical-heads[head];
                            Check(Mathf.Abs(delta.y)<.09f&&new Vector2(delta.x,delta.z).magnitude<.29f,"recognizable Blender petal; preserve custom geometry "+mesh.name);
                            // Shorten only the outer tip; preserve the broad petal shoulders and stem attachment.
                            float round=.80f+.14f*Noise(p+Vector2.one*head,53);
                            float radial=new Vector2(delta.x,delta.z).magnitude;
                            petalWidth=Mathf.Lerp(1,round,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.12f,.27f,radial)));
                            canonical=heads[head]+new Vector3(delta.x*petalWidth,delta.y,delta.z*petalWidth);offset=rotation*canonical*scale;
                            float edge=Mathf.Clamp01(new Vector2(delta.x,delta.z).magnitude/.27f);c[i]=Color.Lerp(tint*.94f,tint,edge);c[i].a=1;
                        }
                        var next=p+new Vector2(offset.x,offset.z)*shrink;
                        v[i]=new Vector3(next.x,Floor(next)+.018f+offset.y*height,next.y);
                        if(!petal&&flower&&Same(c[i],Pollen))c[i]=new Color(.88f,.70f,.36f,1);
                        uv[i].x=-(flower?2:4)-uv[i].x;changed[i]=true;
                        // Keep the authored double-sided shading; recalculation flips the overlapping reverse faces.
                        n[i]=new Vector3(n[i].x/(shrink*petalWidth),n[i].y/height,n[i].z/(shrink*petalWidth)).normalized;
                        Check(Vector2.Distance(next,p)<=radius+.0002f,"original root footprint retained "+mesh.name+" at "+p+", radius="+radius+", next="+Vector2.Distance(next,p)+", flower="+flower);
                    }
                    if(flower)flowers++;else grass++;
                }
                edits.Add(new Edit{Mesh=mesh,Vertices=v,Colors=c,Normals=n,Wind=uv,Changed=changed,Flowers=flowers,Grass=grass,Metadata=metadata});
            }
            Check(edits.Select(e=>e.Mesh).Distinct().Count()==edits.Count&&edits.Sum(e=>e.Flowers)>0&&edits.Sum(e=>e.Grass)>0,"unique meshes / recognizable flowers and grass");return edits.ToArray();
        }
        static void Clean(string p)=>File.WriteAllText(p,string.Join("\n",File.ReadAllLines(p).Select(l=>l.TrimEnd()))+"\n");
        [MenuItem("Magic Island/Art/Refine Grassland Flowers And Grass (Explicit)")]
        public static void Rebuild()
        {
            Check(!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling,"Edit Mode required");
            var root=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);var backups=new Dictionary<string,byte[]>();bool saved=false,created=false;
            try
            {
                var terrain=root.GetComponent<IslandTerrain>();Check(terrain.IsValid&&terrain.World.ArtVersion==3,"authored islands");
                var batches=terrain.SeasonalDetails.MeadowBatches.Where(r=>!AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(r.GetComponent<MeshFilter>().sharedMesh)).userData.Split(';').Contains(GrasslandGroundLayersSetup.Marker)).ToArray();var done=batches.Select(r=>Finished(AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(r.GetComponent<MeshFilter>().sharedMesh)).userData)).ToArray();
                if(done.Any(b=>b))
                {Check(done.All(b=>b)&&batches.All(r=>r.sharedMaterial!=null&&r.sharedMaterial.shader.name==ShaderName),"preserve incomplete flora migration");Debug.Log("Grassland flora and later manual edits retained.");return;}
                Check(!File.Exists(MaterialPath)&&!File.Exists(MaterialPath+".meta"),"preserve orphan material");var shader=Shader.Find(ShaderName);Check(shader!=null&&!ShaderUtil.ShaderHasError(shader),"compiled flora shader");Physics.SyncTransforms();var plan=Plan(terrain);
                foreach(string p in plan.Select(e=>AssetDatabase.GetAssetPath(e.Mesh)).SelectMany(p=>new[]{p,p+".meta"}).Concat(new[]{TerrainSetup.PrefabPath}))backups.Add(p,File.ReadAllBytes(p));
                var material=new Material(shader){name="Natural Meadow Cover"};AssetDatabase.CreateAsset(material,MaterialPath);created=true;
                foreach(var e in plan){e.Mesh.vertices=e.Vertices;e.Mesh.normals=e.Normals;e.Mesh.colors=e.Colors;e.Mesh.uv=e.Wind;e.Mesh.RecalculateBounds();e.Mesh.bounds=new Bounds(e.Mesh.bounds.center,e.Mesh.bounds.size+Vector3.one*.16f);EditorUtility.SetDirty(e.Mesh);}
                foreach(var r in batches)r.sharedMaterial=material;
                Check(PrefabUtility.SaveAsPrefabAsset(root,TerrainSetup.PrefabPath)!=null,"prefab save");AssetDatabase.SaveAssets();
                foreach(var e in plan){string p=AssetDatabase.GetAssetPath(e.Mesh);var importer=AssetImporter.GetAtPath(p);importer.userData=(string.IsNullOrEmpty(e.Metadata)?"":e.Metadata+";")+Marker;importer.SaveAndReimport();Clean(p);Clean(p+".meta");}
                Clean(MaterialPath);Clean(MaterialPath+".meta");Clean(TerrainSetup.PrefabPath);saved=true;
                Debug.Log("Grassland flora baked: "+plan.Sum(e=>e.Flowers)+" flower clumps, "+plan.Sum(e=>e.Grass)+" grass clumps in "+plan.Length+" existing batches; no new objects.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
                if(!saved){foreach(var p in backups)File.WriteAllBytes(p.Key,p.Value);if(created)AssetDatabase.DeleteAsset(MaterialPath);foreach(string p in backups.Keys.Where(p=>!p.EndsWith(".meta",StringComparison.Ordinal)))AssetDatabase.ImportAsset(p,ImportAssetOptions.ForceUpdate);}
            }
        }
    }
}
