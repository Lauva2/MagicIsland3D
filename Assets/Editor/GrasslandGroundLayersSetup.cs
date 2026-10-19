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
    public static class GrasslandGroundLayersSetup
    {
        public const string Group="Grassland ground layers",Marker="MagicIsland.GrasslandGroundLayers.v1";
        public const string MapPath="Assets/Art/Textures/Terrain/MeadowUnderlay.png";
        public const string MaterialPath="Assets/Art/Materials/Terrain/Meadow Underlay.mat",ShaderName="MagicIsland/Meadow Underlay";
        public static readonly Vector4 Rect=new Vector4(-300,230,600,620);
        static readonly Vector4[] Beds={new Vector4(-176,570,18,25),new Vector4(-79,548,17,22),new Vector4(-87,414,16,20)};
        public sealed class Site {public Vector2 P;public float Phase,Scale;public bool Flower;public int Branch;}
        public static void Check(bool ok,string why){if(!ok)throw new InvalidOperationException("Grassland ground layers: "+why);}
        public static float Noise(Vector2 p,float scale,float salt)=>Mathf.Clamp01(Mathf.PerlinNoise(p.x*scale+salt,p.y*scale-salt));
        public static float BedDistance(Vector2 p)
        {
            float best=float.MaxValue;
            foreach(var b in Beds){var d=new Vector2((p.x-b.x)/b.z,(p.y-b.y)/b.w);best=Mathf.Min(best,d.magnitude+(Noise(p,.12f,29)-.5f)*.26f);}return best;
        }
        public static Vector2[] Trees(IslandTerrain terrain)
        {
            var result=new List<Vector2>();
            foreach(var f in terrain.World.Scenery.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.name.StartsWith("Landscape ",StringComparison.Ordinal)))
            {
                var m=f.sharedMesh;var v=m.vertices;var c=m.colors;
                foreach(var part in BiomePolishSetup.Components(m))
                {
                    if(part.Length!=70||part.Any(i=>c[i].g-c[i].r<.065f))continue;
                    var b=new Bounds(v[part[0]],Vector3.zero);foreach(int i in part)b.Encapsulate(v[i]);var p=new Vector2(b.center.x,b.center.z);
                    if(b.size.y>2&&BiomeIslandLayout.At(p.x,p.y)==IslandBiome.Grassland&&BiomeIslandLayout.Contains(p.x,p.y,25)&&result.All(q=>Vector2.Distance(q,p)>3))result.Add(p);
                }
            }
            Check(result.Count>0,"existing tree canopy anchors");return result.ToArray();
        }
        public static Color Field(Vector2 p,Vector2[] trees)
        {
            float coast=(float)BiomeIslandLayout.Distance(IslandBiome.Grassland,p.x,p.y);
            float mask=Mathf.SmoothStep(0,1,Mathf.InverseLerp(13,27,coast));
            float broad=Noise(p,.061f,47),fine=Noise(p,.19f,18);
            float bed=BedDistance(p),transition=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.05f,2.35f,bed));
            float green=Mathf.Clamp01((broad-.28f)*1.7f+fine*.25f+transition*.20f);
            float road=GrasslandIslandMeadowSetup.RouteDistance(p);
            float shoulder=(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(2,5.5f,road)))*(.4f+.6f*fine);
            float dry=Mathf.Clamp01(shoulder*.65f+Mathf.SmoothStep(0,1,Mathf.InverseLerp(.55f,.82f,Noise(p,.085f,77)))*.38f);
            float tree=0;foreach(var q in trees)tree=Mathf.Max(tree,1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.5f,6.5f,Vector2.Distance(p,q))));
            return new Color(green,dry,tree*(.6f+fine*.4f),mask);
        }
        public static MeshRenderer[] Grounds(IslandTerrain t)
            =>t.World.Grounds.Where(c=>c.transform.parent.name=="Grassland island").Select(c=>c.GetComponent<MeshRenderer>()).ToArray();
        public static Site[] Sites(IslandTerrain terrain,GrasslandRevitalizationSetup.AuthoringWorld world,Vector2[] trees)
        {
            var random=new System.Random(208731);var result=new List<Site>();
            var occupied=terrain.SeasonalDetails.MeadowBatches.SelectMany(r=>r.GetComponent<MeshFilter>().sharedMesh.uv2).Distinct()
                .GroupBy(p=>new Vector2Int(Mathf.FloorToInt(p.x/3),Mathf.FloorToInt(p.y/3))).ToDictionary(g=>g.Key,g=>g.ToList());
            var ecology=terrain.World.GetComponentInChildren<WorldWaterEcology>(true);var tracks=ecology.RabbitHabitats.SelectMany(h=>h.Track).Select(v=>new Vector2(v.x,v.z)).ToArray();
            bool Near(Vector2 p,float distance)
            {
                var key=new Vector2Int(Mathf.FloorToInt(p.x/3),Mathf.FloorToInt(p.y/3));
                for(int x=-1;x<=1;x++)for(int z=-1;z<=1;z++)if(occupied.TryGetValue(key+new Vector2Int(x,z),out var list)&&list.Any(q=>(p-q).sqrMagnitude<distance*distance))return true;return false;
            }
            for(float x=-275;x<240;x+=1.7f)for(float z=260;z<795;z+=1.7f)
            {
                var p=new Vector2(x+(float)random.NextDouble()*1.35f,z+(float)random.NextDouble()*1.35f);
                float draw=(float)random.NextDouble(),phase=(float)random.NextDouble()*6.283185f,bed=BedDistance(p);
                if(!BiomeIslandLayout.Contains(p.x,p.y,22)||BiomeIslandLayout.At(p.x,p.y)!=IslandBiome.Grassland||GrasslandIslandMeadowSetup.RouteDistance(p)<2.8f)continue;
                var field=Field(p,trees);float density=.12f+field.r*.67f+field.b*.12f;
                // Flower beds dissolve into low grass, with sparse two-stem flowers on irregular outskirts.
                bool flower=bed>1&&bed<2.2f&&draw<.075f*(1-Mathf.InverseLerp(1,2.2f,bed));
                if(!flower&&draw>density)continue;
                if(Near(p,flower?1.25f:.75f)||tracks.Any(q=>Vector2.Distance(q,p)<1.8f))continue;
                float floor=world.Floor(p);if(!world.Clear(p,floor,.86f,.95f))continue;
                bool safe=true;foreach(var d in new[]{new Vector2(.8f,.8f),new Vector2(-.8f,.8f),new Vector2(.8f,-.8f),new Vector2(-.8f,-.8f)})
                    if(!BiomeIslandLayout.Contains(p.x+d.x,p.y+d.y,20)||GrasslandIslandMeadowSetup.RouteDistance(p+d)<2.1f||Mathf.Abs(world.Floor(p+d)-floor)>.15f)safe=false;
                if(!safe)continue;
                result.Add(new Site{P=p,Flower=flower,Phase=phase,Scale=.8f+(float)random.NextDouble()*.4f,Branch=random.Next(7)});
                var key=new Vector2Int(Mathf.FloorToInt(p.x/3),Mathf.FloorToInt(p.y/3));if(!occupied.TryGetValue(key,out var list)){list=new List<Vector2>();occupied.Add(key,list);}list.Add(p);
            }
            Check(result.Count>2000&&result.Count<40000&&result.Any(s=>s.Flower),"bounded grass/edge flower population "+result.Count);return result.ToArray();
        }
        public static Dictionary<string,Mesh> Prepare(GrasslandRevitalizationSetup.AuthoringWorld world,Site[] sites)
        {
            var batches=new Dictionary<string,GrasslandIslandMeadowSetup.Batch>();var floors=new Dictionary<Vector2,float>();
            float Floor(Vector2 p){if(!floors.TryGetValue(p,out float y)){y=world.Floor(p);floors.Add(p,y);}return y;}
            var export=JsonUtility.FromJson<BeachCompositionSetup.Export>(File.ReadAllText(GrasslandCompositionSetup.Source));var flower=export.templates.Single(t=>t.name=="Flowers");
            Check(flower.positions.Length/3==1995&&flower.normals.Length==flower.positions.Length&&flower.colors.Length==1995*4,"original seven-branch Blender flower source");
            foreach(var site in sites)
            {
                string name="Meadow undergrowth "+Mathf.FloorToInt(site.P.x/128)+" "+Mathf.FloorToInt(site.P.y/128);
                if(!batches.TryGetValue(name,out var b)){b=new GrasslandIslandMeadowSetup.Batch();batches.Add(name,b);}
                float floor=Floor(site.P);var rotation=Quaternion.Euler(0,site.Phase*Mathf.Rad2Deg,0);
                void Add(Vector3 q,Vector3 n,Color color,float weight,bool petal=false)
                {
                    var p=site.P+new Vector2(q.x,q.z);Check(new Vector2(q.x,q.z).magnitude<.86f,"bounded new root envelope");
                    b.V.Add(new Vector3(p.x,Floor(p)+.018f+q.y,p.y));b.N.Add(n.normalized);b.C.Add(color);b.A.Add(new Vector2(-(site.Flower?2:4)-weight,site.Phase));b.B.Add(site.P);b.D.Add(new Vector2(floor,petal?0:1));
                }
                if(site.Flower)
                {
                    var chosen=new HashSet<int>(new[]{site.Branch,(site.Branch+3)%7});var map=new Dictionary<int,int>();var tint=GrasslandFloraSetup.PetalColor(site.P);
                    for(int i=0;i<1995;i++)
                    {
                        if(!chosen.Contains(i/285))continue;var q=rotation*new Vector3(flower.positions[i*3],flower.positions[i*3+1],flower.positions[i*3+2])*(.72f*site.Scale);
                        var c=new Color(flower.colors[i*4],flower.colors[i*4+1],flower.colors[i*4+2],1);bool petal=c.b>c.g||c.r>.95f;bool pollen=c.r>.85f&&c.g<.7f;
                        if(petal)c=tint;else if(pollen)c=new Color(.88f,.70f,.36f,1);
                        map.Add(i,b.V.Count);Add(q,rotation*new Vector3(flower.normals[i*3],flower.normals[i*3+1],flower.normals[i*3+2]),c,Mathf.Clamp01(q.y),petal||pollen);
                    }
                    for(int i=0;i<flower.triangles.Length;i+=3)
                    {
                        int a=flower.triangles[i],c=flower.triangles[i+1],d=flower.triangles[i+2];
                        if(!map.ContainsKey(a))continue;Check(map.ContainsKey(c)&&map.ContainsKey(d),"complete separate source branches");b.T.AddRange(new[]{map[a],map[c],map[d]});
                    }
                }
                else
                {
                    int blades=6+site.Branch%4;
                    for(int j=0;j<blades;j++)
                    {
                        float a=site.Phase+j*2.399963f,h=(.12f+(j%4)*.035f)*site.Scale,w=.035f+(j%3)*.015f;var d=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));var side=new Vector3(-d.z,0,d.x);var root=d*(.08f+(j%4)*.105f);int first=b.V.Count;
                        var mid=root+d*.12f+Vector3.up*(h*.64f);var tip=root+d*.25f+Vector3.up*h;var n=new Vector3(-d.x*.18f,1,-d.z*.18f).normalized;
                        var color=Color.Lerp(new Color(.49f,.63f,.35f),new Color(.64f,.73f,.42f),Noise(site.P,.17f,34)*.7f+(j%3)*.1f);
                        foreach(var q in new[]{root-side*w,root+side*w,mid-side*w*.58f,mid+side*w*.58f,tip})Add(q,n,color,Mathf.Clamp01(q.y*1.5f));
                        b.T.AddRange(new[]{first,first+2,first+1,first+1,first+2,first+3,first+2,first+4,first+3,first,first+1,first+2,first+1,first+3,first+2,first+2,first+3,first+4});
                    }
                }
            }
            return batches.ToDictionary(p=>p.Key,p=>p.Value.Mesh(p.Key));
        }
        public static Texture2D Map(Vector2[] trees)
        {
            const int size=512;var texture=new Texture2D(size,size,TextureFormat.RGBA32,false,true);var pixels=new Color[size*size];
            for(int z=0;z<size;z++)for(int x=0;x<size;x++)pixels[z*size+x]=Field(new Vector2(Rect.x+x/(size-1f)*Rect.z,Rect.y+z/(size-1f)*Rect.w),trees);
            texture.SetPixels(pixels);texture.Apply();return texture;
        }
        static void Clean(string path)=>File.WriteAllText(path,string.Join("\n",File.ReadAllLines(path).Select(l=>l.TrimEnd()))+"\n");
        [MenuItem("Magic Island/Art/Refine Grassland Ground Layers (Explicit)")]
        public static void Rebuild()
        {
            Check(!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling,"Edit Mode required");
            var root=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);var meshes=new Dictionary<string,Mesh>();var created=new List<string>();var prefab=File.ReadAllBytes(TerrainSetup.PrefabPath);bool saved=false;Texture2D field=null;
            try
            {
                var t=root.GetComponent<IslandTerrain>();Check(t.IsValid&&t.World.ArtVersion==3,"authored islands");var parent=t.World.Scenery;var old=parent.Find(Group);
                if(old!=null){Check(old.GetComponentsInChildren<MeshFilter>(true).Length>0&&old.GetComponentsInChildren<MeshFilter>(true).All(f=>f.sharedMesh!=null)&&File.Exists(MapPath)&&File.Exists(MaterialPath),"preserve incomplete/custom ground layers");Debug.Log("Grassland ground layers and later hand edits retained.");return;}
                Check(parent.localToWorldMatrix==t.transform.localToWorldMatrix,"preserve custom scenery transform");
                var grounds=Grounds(t);var basis=AssetDatabase.LoadAssetAtPath<Material>(GrasslandRevitalizationSetup.MaterialFolder+"Living Meadow Ground.mat");var flora=AssetDatabase.LoadAssetAtPath<Material>(GrasslandFloraSetup.MaterialPath);var shader=Shader.Find(ShaderName);
                Check(grounds.Length>0&&grounds.All(r=>r.sharedMaterials.Length==1&&r.sharedMaterial==basis)&&basis!=null&&flora!=null&&shader!=null&&!ShaderUtil.ShaderHasError(shader),"preserve ground materials / compiled shared shaders");
                foreach(string p in new[]{MapPath,MaterialPath})Check(!File.Exists(p)&&!File.Exists(p+".meta"),"preserve orphan asset "+p);
                Physics.SyncTransforms();var world=new GrasslandRevitalizationSetup.AuthoringWorld(t,Rect);var trees=Trees(t);var sites=Sites(t,world,trees);meshes=Prepare(world,sites);field=Map(trees);
                Check(t.SeasonalDetails.MeadowBatches.Length+meshes.Count<=64,"bounded total cover batches");
                foreach(string name in meshes.Keys){string p=BiomeIslandsSetup.Folder+"/"+name+".asset";Check(!File.Exists(p)&&!File.Exists(p+".meta"),"preserve orphan mesh "+p);}
                created.Add(MapPath);File.WriteAllBytes(MapPath,field.EncodeToPNG());AssetDatabase.ImportAsset(MapPath);var importer=(TextureImporter)AssetImporter.GetAtPath(MapPath);importer.sRGBTexture=false;importer.mipmapEnabled=false;importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Bilinear;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.userData=Marker;importer.SaveAndReimport();
                var material=new Material(basis){name="Meadow Underlay",shader=shader};material.SetTexture("_UnderlayMap",AssetDatabase.LoadAssetAtPath<Texture2D>(MapPath));material.SetVector("_UnderlayRect",Rect);created.Add(MaterialPath);AssetDatabase.CreateAsset(material,MaterialPath);
                foreach(var pair in meshes){string p=BiomeIslandsSetup.Folder+"/"+pair.Key+".asset";created.Add(p);AssetDatabase.CreateAsset(pair.Value,p);}
                var group=new GameObject(Group);group.layer=2;group.transform.SetParent(parent,false);var batches=t.SeasonalDetails.MeadowBatches.ToList();
                foreach(var pair in meshes)
                {
                    var go=new GameObject(pair.Key,typeof(MeshFilter),typeof(MeshRenderer));go.layer=2;go.transform.SetParent(group.transform,false);go.GetComponent<MeshFilter>().sharedMesh=pair.Value;var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=flora;renderer.shadowCastingMode=ShadowCastingMode.Off;batches.Add(renderer);
                }
                foreach(var renderer in grounds)renderer.sharedMaterial=material;t.SeasonalDetails.MeadowBatches=batches.ToArray();Check(t.IsValid,"complete AA owner");Check(PrefabUtility.SaveAsPrefabAsset(root,TerrainSetup.PrefabPath)!=null,"prefab save");AssetDatabase.SaveAssets();
                foreach(string p in created.Where(p=>p!=MapPath)){var assetImporter=AssetImporter.GetAtPath(p);assetImporter.userData=Marker;assetImporter.SaveAndReimport();Clean(p);Clean(p+".meta");}Clean(MapPath+".meta");Clean(TerrainSetup.PrefabPath);saved=true;
                Directory.CreateDirectory("Logs/GrasslandGroundLayers");File.WriteAllText("Logs/GrasslandGroundLayers/Sites.json",JsonUtility.ToJson(new SiteReport{Grass=sites.Count(s=>!s.Flower),Flowers=sites.Count(s=>s.Flower),Batches=meshes.Count,Trees=trees.Length}));
                Debug.Log("Grassland ground layers baked: "+sites.Count(s=>!s.Flower)+" low-grass clumps, "+sites.Count(s=>s.Flower)+" two-stem flowers, "+meshes.Count+" merged batches, "+grounds.Length+" ground material references; original geometry retained.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);if(!saved&&created.Count>0){File.WriteAllBytes(TerrainSetup.PrefabPath,prefab);foreach(string p in created)AssetDatabase.DeleteAsset(p);AssetDatabase.ImportAsset(TerrainSetup.PrefabPath,ImportAssetOptions.ForceUpdate);}
                foreach(var mesh in meshes.Values)if(mesh!=null&&!EditorUtility.IsPersistent(mesh))Object.DestroyImmediate(mesh);if(field!=null)Object.DestroyImmediate(field);
            }
        }
        [Serializable]sealed class SiteReport {public int Grass,Flowers,Batches,Trees;}
    }
}
