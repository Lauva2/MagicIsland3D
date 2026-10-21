using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MagicIsland.TerrainSystem;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    public static class GrasslandGroundDetailSetup
    {
        public const string Marker="MagicIsland.GrasslandGroundDetail.v1",Pending="MagicIsland.GrasslandGroundDetail.Pending.v1";
        public const string MapPath="Assets/Art/Textures/Terrain/MeadowSampleGround.png";
        public const string MaterialPath=GrasslandGroundLayersSetup.MaterialPath;
        static readonly HashSet<string> Known=new HashSet<string>{GrasslandRevitalizationSetup.Marker,GrasslandIslandMeadowSetup.Marker,GrasslandGroveSetup.Marker,
            GrasslandFloraSetup.Marker,GrasslandGroundLayersSetup.Marker,GrasslandShrubStoneSetup.Marker,GrasslandTouchSetup.Marker,GrasslandReadabilitySetup.Marker,
            GrasslandPatchSetup.Marker,GrasslandBlendSetup.Marker,GrasslandWildflowerSetup.Marker,GrasslandEdgeSetup.Marker,GrasslandPathEdgeSetup.Marker,
            GrasslandSampleFinishSetup.Marker,GrasslandSpatialSetup.Marker,GrasslandShrubDetailSetup.Marker,GrasslandShrubWindSetup.Marker};
        public sealed class Plant{public Vector2 Root;public bool Grass,Flower,Shrub;public float Radius;}
        public sealed class Edit{public Mesh Mesh;public Vector3[] Vertices,Normals;public Color[] Colors;public bool[] Changed;public string Metadata;public int Clumps;}
        public sealed class PlanData{public Edit[] Meshes;public Color32[] Pixels;public Vector4 Rect;public int Leaves;public Plant[] Plants;}
        struct Leaf{public Vector2 Center,Direction;public float Length,Width,Opacity;}
        public static bool Done(string data)=>data.Split(';').Contains(Marker);
        public static void Check(bool ok,string why){if(!ok)throw new InvalidOperationException("Ground detail: "+why);}
        public static float Mask(Vector2 p,MeadowTouchSettings s)
            =>1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(s.AreaRadius-s.Feather,s.AreaRadius-1,Vector2.Distance(p,s.Center)));
        static float Noise(Vector2 p,float scale,float salt)=>GrasslandGroundLayersSetup.Noise(p,scale,salt);
        public static PlanData Plan(IslandTerrain t)
        {
            var s=t.SeasonalDetails.TouchSample;Check(t.IsValid&&s.ArtVersion==1&&s.Renderers.Length==6&&t.transform.localToWorldMatrix==Matrix4x4.identity,"prepared original sample");
            var material=AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);var grounds=GrasslandGroundLayersSetup.Grounds(t);
            Check(material!=null&&material.shader.name==GrasslandGroundLayersSetup.ShaderName&&grounds.Length>0&&grounds.All(r=>r.sharedMaterial==material)
                &&AssetDatabase.GetAssetPath(material.GetTexture("_UnderlayMap"))==GrasslandGroundLayersSetup.MapPath
                &&AssetImporter.GetAtPath(MaterialPath).userData==GrasslandGroundLayersSetup.Marker,"preserve custom ground material/reference/metadata");
            Check(string.IsNullOrEmpty(AssetDatabase.GetAssetPath(material.GetTexture("_GroundDetailMap")))&&material.GetFloat("_GroundDetailStrength")==0
                &&material.GetVector("_GroundDetailRect")==Vector4.zero,"preserve preexisting detail settings");
            Check(!File.Exists(MapPath)&&!File.Exists(MapPath+".meta"),"preserve orphan/custom sample map");
            var plants=new List<Plant>();
            foreach(var r in t.SeasonalDetails.MeadowBatches)
            {
                var m=r.GetComponent<MeshFilter>().sharedMesh;var v=m.vertices;var uv=m.uv;var roots=m.uv2;
                Check(v.Length==uv.Length&&v.Length==roots.Length,"complete neighboring root metadata");
                foreach(var g in m.triangles.Distinct().Where(i=>Vector2.Distance(roots[i],s.Center)<s.AreaRadius+4).GroupBy(i=>roots[i]))
                {
                    var ids=g.ToArray();plants.Add(new Plant{Root=g.Key,Grass=ids.All(i=>uv[i].x>=-5&&uv[i].x<=-4),Flower=ids.Any(i=>uv[i].x>=-3&&uv[i].x<=-2),
                        Shrub=ids.Any(i=>GrasslandShrubWindSetup.Marked(uv[i])),Radius=ids.Max(i=>Vector2.Distance(new Vector2(v[i].x,v[i].z),g.Key))});
                }
            }
            var grass=plants.Where(p=>p.Grass).ToArray();var accents=plants.Where(p=>!p.Grass).ToArray();Check(grass.Length>100&&accents.Any(p=>p.Shrub),"actual grass/flower/shrub distribution");
            var leaves=new List<Leaf>();var random=new System.Random(231021);
            foreach(var plant in accents.Where(p=>p.Shrub||p.Flower&&p.Radius>.3f).OrderBy(p=>p.Root.x).ThenBy(p=>p.Root.y))
            {
                int count=plant.Shrub?7:random.Next(1,3);
                for(int i=0;i<count;i++)
                {
                    float angle=(float)random.NextDouble()*Mathf.PI*2,reach=.25f+(float)random.NextDouble()*(plant.Shrub?1.1f:.55f);
                    var center=plant.Root+new Vector2(Mathf.Sin(angle),Mathf.Cos(angle))*reach;
                    if(Mask(center,s)<.25f||GrasslandIslandMeadowSetup.RouteDistance(center)<2.7f)continue;
                    float yaw=(float)random.NextDouble()*Mathf.PI*2;
                    leaves.Add(new Leaf{Center=center,Direction=new Vector2(Mathf.Cos(yaw),Mathf.Sin(yaw)),Length=.12f+(float)random.NextDouble()*.09f,
                        Width=.075f+(float)random.NextDouble()*.035f,Opacity=.45f+(float)random.NextDouble()*.3f});
                }
            }
            const int size=512;var rect=new Vector4(s.Center.x-25,s.Center.y-25,50,50);var pixels=new Color32[size*size];float texelReach=GrasslandBlendSetup.TexelReach(size,size,rect);
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                var p=GrasslandBlendSetup.PixelWorld(x,y,size,size,rect);float mask=Mask(p,s);
                if(Vector2.Distance(p,s.Center)+texelReach>=s.AreaRadius||GrasslandIslandMeadowSetup.RouteDistance(p)-texelReach<=2.1f||mask<=0)continue;
                float density=0,moss=0;
                foreach(var plant in grass){float d=(p-plant.Root).sqrMagnitude;if(d<16)density+=Mathf.Exp(-d/2.2f);}
                foreach(var plant in accents)
                {
                    float d=(p-plant.Root).sqrMagnitude,reach=Mathf.Clamp(plant.Radius,.3f,1.15f)+.35f;
                    if(d<9)moss=Mathf.Max(moss,Mathf.Exp(-d/(reach*reach))*(plant.Shrub?.82f:.42f));
                }
                float broad=Noise(p,.17f,61),fine=Noise(p,.61f,38),clumps=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.7f,4.0f,density));
                float soil=(1-clumps*.70f)*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.32f,.74f,broad))*(.60f+fine*.30f);
                moss=Mathf.Clamp01(moss+clumps*.35f)*(.62f+fine*.38f);float litter=0;
                foreach(var leaf in leaves)
                {
                    var d=p-leaf.Center;if(d.sqrMagnitude>.08f)continue;
                    float u=Vector2.Dot(d,leaf.Direction)/leaf.Length,w=Vector2.Dot(d,new Vector2(-leaf.Direction.y,leaf.Direction.x))/leaf.Width;
                    float shape=Mathf.Abs(u)+w*w;
                    litter=Mathf.Max(litter,(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.30f,1,shape)))*leaf.Opacity);
                }
                pixels[y*size+x]=(Color32)new Color(soil,moss,litter,mask);
            }
            var world=new GrasslandRevitalizationSetup.AuthoringWorld(t,new Vector4(s.Center.x-25,s.Center.y-25,50,50));var floorCache=new Dictionary<Vector2,float>();
            float Floor(Vector2 p){if(!floorCache.TryGetValue(p,out float f)){f=world.Floor(p);floorCache.Add(p,f);}return f;}
            var edits=new List<Edit>();
            foreach(var r in s.Renderers)
            {
                var m=r.GetComponent<MeshFilter>().sharedMesh;string path=AssetDatabase.GetAssetPath(m),data=AssetImporter.GetAtPath(path).userData;
                Check(path.StartsWith(BiomeIslandsSetup.Folder+"/",StringComparison.Ordinal)&&r.transform.localToWorldMatrix==t.transform.localToWorldMatrix
                    &&r.sharedMaterial.shader.name==GrasslandFloraSetup.ShaderName&&GrasslandShrubWindSetup.Done(data)&&data.Split(';').All(Known.Contains)
                    &&!t.GetComponentsInChildren<MeshCollider>(true).Any(c=>c.sharedMesh==m),"preserve custom mesh/material/metadata or collision");
                var v=m.vertices;var n=m.normals;var colors=m.colors;var uv=m.uv;var roots=m.uv2;
                Check(v.Length==n.Length&&v.Length==colors.Length&&v.Length==uv.Length&&v.Length==roots.Length&&m.subMeshCount==1,"complete visual attributes");
                var e=new Edit{Mesh=m,Vertices=v.ToArray(),Normals=n.ToArray(),Colors=colors.ToArray(),Changed=new bool[v.Length],Metadata=data};edits.Add(e);
                foreach(var group in m.triangles.Distinct().GroupBy(i=>roots[i]))
                {
                    var ids=group.ToArray();var root=group.Key;float blend=Mask(root,s);
                    if(blend<=0||Vector2.Distance(root,s.Center)+1.3f>=s.AreaRadius||GrasslandIslandMeadowSetup.RouteDistance(root)<3.65f||ids.Any(i=>uv[i].x< -5||uv[i].x> -4))continue;
                    float density=0;foreach(var p in grass){float d=(p.Root-root).sqrMagnitude;if(d<9)density+=Mathf.Exp(-d/1.6f);}
                    if(density>2.3f)continue;
                    float factor=Mathf.Lerp(1,Mathf.Lerp(.64f,.94f,Mathf.InverseLerp(1,2.3f,density)),blend);
                    e.Clumps++;
                    foreach(int i in ids)
                    {
                        float floor=Floor(new Vector2(v[i].x,v[i].z)),height=Mathf.Max(0,v[i].y-floor-.018f);
                        if(height<=.025f)continue;
                        e.Vertices[i].y=floor+.018f+height*factor;e.Normals[i]=new Vector3(n[i].x,n[i].y/factor,n[i].z).normalized;
                        var tint=Color.Lerp(colors[i],new Color(.57f,.65f,.40f,colors[i].a),.16f*blend);tint.a=colors[i].a;e.Colors[i]=tint;e.Changed[i]=true;
                    }
                }
            }
            Check(edits.Sum(e=>e.Changed.Count(b=>b))>500&&leaves.Count>30&&pixels.Count(p=>p.b>10)>100,"visible bounded ground and edge plan");
            return new PlanData{Meshes=edits.ToArray(),Pixels=pixels,Rect=rect,Leaves=leaves.Count,Plants=plants.ToArray()};
        }
        static void Clean(string p)=>File.WriteAllText(p,string.Join("\n",File.ReadAllLines(p).Select(l=>l.TrimEnd()))+"\n");
        [MenuItem("Magic Island/Art/Refine Grassland Sample Ground Detail (Explicit)")]
        public static void Rebuild()
        {
            Check(!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling,"Edit Mode required");
            var root=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);var backups=new Dictionary<string,byte[]>();bool saved=false,created=false;
            try
            {
                var t=root.GetComponent<IslandTerrain>();var meshPaths=t.SeasonalDetails.TouchSample.Renderers.Select(r=>AssetDatabase.GetAssetPath(r.GetComponent<MeshFilter>().sharedMesh)).ToArray();
                var paths=meshPaths.Concat(new[]{MaterialPath}).ToArray();var metadata=paths.Select(p=>AssetImporter.GetAtPath(p).userData).ToArray();
                Check(!metadata.Any(d=>d.Split(';').Contains(Pending)),"preserve interrupted write");
                if(metadata.Any(Done))
                {
                    Check(metadata.All(Done)&&File.Exists(MapPath)&&Done(AssetImporter.GetAtPath(MapPath).userData),"preserve partial/custom completed data");
                    saved=true;Debug.Log("Grassland ground detail and manual art retained.");return;
                }
                var plan=Plan(t);
                foreach(var p in paths.SelectMany(p=>new[]{p,p+".meta"}))backups.Add(p,File.ReadAllBytes(p));
                var materialImporter=AssetImporter.GetAtPath(MaterialPath);materialImporter.userData=metadata.Last()+";"+Pending;materialImporter.SaveAndReimport();
                var texture=new Texture2D(512,512,TextureFormat.RGBA32,false,true);
                try{texture.SetPixels32(plan.Pixels);texture.Apply();created=true;File.WriteAllBytes(MapPath,texture.EncodeToPNG());}
                finally{Object.DestroyImmediate(texture);}
                AssetDatabase.ImportAsset(MapPath,ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);var importer=(TextureImporter)AssetImporter.GetAtPath(MapPath);
                importer.sRGBTexture=false;importer.mipmapEnabled=false;importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Bilinear;
                importer.textureCompression=TextureImporterCompression.Uncompressed;importer.alphaIsTransparency=false;importer.userData=Marker;importer.SaveAndReimport();
                foreach(var e in plan.Meshes){var bounds=e.Mesh.bounds;e.Mesh.vertices=e.Vertices;e.Mesh.normals=e.Normals;e.Mesh.colors=e.Colors;e.Mesh.bounds=bounds;EditorUtility.SetDirty(e.Mesh);}
                AssetDatabase.ImportAsset(MapPath,ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);
                var material=AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
                var map=AssetDatabase.LoadAssetAtPath<Texture2D>(MapPath);Check(map!=null&&AssetDatabase.Contains(map),"imported persistent detail map required before material write");
                // Stage the newly added shader properties before updating the original material asset.
                // A material imported before the shader gained this texture can retain an old property layout.
                var staged=new Material(material){name=material.name};
                try
                {
                    staged.SetTexture("_GroundDetailMap",map);staged.SetVector("_GroundDetailRect",plan.Rect);staged.SetFloat("_GroundDetailStrength",.86f);
                    Check(staged.GetTexture("_GroundDetailMap")!=null,"staged persistent texture reference");
                    EditorUtility.CopySerialized(staged,material);EditorUtility.SetDirty(material);AssetDatabase.SaveAssets();
                }
                finally{Object.DestroyImmediate(staged);}
                string mapGuid=AssetDatabase.AssetPathToGUID(MapPath);
                Check(File.ReadAllText(MaterialPath).Contains("guid: "+mapGuid),"material save must serialize the persistent texture GUID; map="+mapGuid+", memory="+AssetDatabase.GetAssetPath(material.GetTexture("_GroundDetailMap")));
                for(int i=0;i<paths.Length;i++)
                {
                    var imp=AssetImporter.GetAtPath(paths[i]);imp.userData=metadata[i]+";"+Marker;imp.SaveAndReimport();Clean(paths[i]);Clean(paths[i]+".meta");
                    Check(File.ReadAllText(MaterialPath).Contains("guid: "+mapGuid),"preserve texture GUID after importing "+paths[i]);
                }
                Clean(MapPath+".meta");
                AssetDatabase.ImportAsset(MaterialPath,ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);
                Check(AssetDatabase.GetAssetPath(AssetDatabase.LoadAssetAtPath<Material>(MaterialPath).GetTexture("_GroundDetailMap"))==MapPath,"persistent material dependency after reload");
                saved=true;
                Debug.Log("Grassland sample ground prepared: "+plan.Leaves+" baked leaf marks, "+plan.Meshes.Sum(e=>e.Clumps)+" low edge clumps; no new geometry or objects.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
                if(!saved)
                {
                    foreach(var p in backups)File.WriteAllBytes(p.Key,p.Value);
                    if(created)AssetDatabase.DeleteAsset(MapPath);
                    foreach(var p in backups.Keys.Where(p=>!p.EndsWith(".meta",StringComparison.Ordinal)))AssetDatabase.ImportAsset(p,ImportAssetOptions.ForceUpdate);
                }
            }
        }
    }
}
