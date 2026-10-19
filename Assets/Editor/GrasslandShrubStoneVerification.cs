using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using MagicIsland.Managers;
using MagicIsland.TerrainSystem;
using MagicIsland.WeatherSystem;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    public static class GrasslandShrubStoneVerification
    {
        const string Folder="Logs/GrasslandShrubStone",Baseline="Assets/Editor/ShrubStoneBaseline";
        static void Check(bool ok,string why){if(!ok)throw new InvalidOperationException("Shrub/stone verification: "+why);}
        static string Hash(string p){using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(p)));}
        static Vector4[] UV(Mesh m,int channel){var a=new List<Vector4>();m.GetUVs(channel,a);return a.ToArray();}
        public static bool Authored(Renderer r)
        {
            var mesh=r.GetComponent<MeshFilter>().sharedMesh;if(!GrasslandShrubStoneSetup.Candidate(mesh.name))return false;
            var importer=AssetImporter.GetAtPath(BiomeIslandsSetup.Folder+"/"+mesh.name+".asset");return importer!=null&&GrasslandShrubStoneSetup.Done(importer.userData);
        }
        public static Vector2[] PlacementRoots(Renderer r)
        {
            var m=r.GetComponent<MeshFilter>().sharedMesh;var root=m.uv2;var weight=m.uv;var colors=m.colors;
            bool RefinedLeaf(Color c)
            {
                float t=(c.r-.36f)/.27f;
                return t>=0&&t<=1&&Mathf.Abs(c.g-(.51f+.22f*t))+Mathf.Abs(c.b-(.30f+.14f*t))<.0003f;
            }
            // Metadata also marks unchanged portions of a combined mesh; select an actual edited lobe.
            return Enumerable.Range(0,root.Length).GroupBy(i=>root[i]).Where(g=>g.All(i=>weight[i].x>=0)&&g.Any(i=>weight[i].x>.01f)&&g.Any(i=>weight[i].x==0)&&g.Any(i=>RefinedLeaf(colors[i]))).Select(g=>g.Key).ToArray();
        }
        public static void BakeAndVerify()
        {
            Check(Application.dataPath.Replace('\\','/').Contains("/Logs/"),"isolated validation project");var root=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);GrasslandShrubStoneSetup.Edit[] plan;
            try{Physics.SyncTransforms();plan=GrasslandShrubStoneSetup.Plan(root.GetComponent<IslandTerrain>());}finally{PrefabUtility.UnloadPrefabContents(root);}
            var paths=plan.Select(e=>AssetDatabase.GetAssetPath(e.Mesh)).ToArray();var oldV=plan.Select(e=>e.Mesh.vertices).ToArray();var oldN=plan.Select(e=>e.Mesh.normals).ToArray();var oldC=plan.Select(e=>e.Mesh.colors).ToArray();var oldT=plan.Select(e=>e.Mesh.triangles).ToArray();var guids=paths.Select(AssetDatabase.AssetPathToGUID).ToArray();
            var oldUV=plan.Select(e=>Enumerable.Range(0,8).Select(ch=>UV(e.Mesh,ch)).ToArray()).ToArray();
            Check(!Directory.Exists(Baseline),"fresh pre-refinement fixtures");AssetDatabase.CreateFolder("Assets/Editor","ShrubStoneBaseline");foreach(string p in paths)Check(AssetDatabase.CopyAsset(p,Baseline+"/"+Path.GetFileName(p)),"original-mesh comparison fixture");
            var exceptions=new HashSet<string>(paths.SelectMany(p=>new[]{p,p+".meta"}));var protect=Directory.GetFiles("Assets/Art","*",SearchOption.AllDirectories).Select(p=>p.Replace('\\','/')).Where(p=>!exceptions.Contains(p)).ToArray();var hashes=protect.Select(Hash).ToArray();
            var importer=AssetImporter.GetAtPath(paths[0]);string metadata=importer.userData;
            try
            {
                importer.userData="Artist.Custom";root=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);bool refused=false;
                try{GrasslandShrubStoneSetup.Plan(root.GetComponent<IslandTerrain>());}catch(InvalidOperationException){refused=true;}finally{PrefabUtility.UnloadPrefabContents(root);}Check(refused,"foreign metadata refused before writes");
            }
            finally{importer.userData=metadata;}
            GrasslandShrubStoneSetup.Rebuild();int changed=0;
            for(int p=0;p<paths.Length;p++)
            {
                var m=AssetDatabase.LoadAssetAtPath<Mesh>(paths[p]);Check(AssetDatabase.AssetPathToGUID(paths[p])==guids[p]&&m.triangles.SequenceEqual(oldT[p]),"original GUID, topology and root ownership");
                for(int ch=0;ch<8;ch++)Check(UV(m,ch).SequenceEqual(oldUV[p][ch]),"all original UV0-7, wind, roots and season retained");
                var v=m.vertices;var n=m.normals;var c=m.colors;
                for(int i=0;i<v.Length;i++)
                {
                    if(plan[p].Changed[i]){changed++;Check(v[i]==plan[p].Vertices[i]&&n[i]==plan[p].Normals[i]&&c[i]==plan[p].Colors[i],"planned existing stones and leaf lobes only");Check(n[i].sqrMagnitude>.99f&&Vector3.Dot(n[i],oldN[p][i])>.65f,"valid outward authored shading");}
                    else Check(v[i]==oldV[p][i]&&n[i]==oldN[p][i]&&c[i]==oldC[p][i],"existing flowers, grass, litter and unselected art retained");
                }
            }
            for(int i=0;i<protect.Length;i++)Check(Hash(protect[i])==hashes[i],"prefab, all ground/collision, other biomes, ecology, materials and art retained "+protect[i]);
            byte[] bytes=File.ReadAllBytes(paths[0]);
            try
            {
                var m=AssetDatabase.LoadAssetAtPath<Mesh>(paths[0]);var c=m.colors;c[0].r*=.97f;m.colors=c;EditorUtility.SetDirty(m);AssetDatabase.SaveAssets();var all=Directory.GetFiles("Assets/Art","*",SearchOption.AllDirectories);var saved=all.Select(Hash).ToArray();
                GrasslandShrubStoneSetup.Rebuild();GrasslandFloraSetup.Rebuild();GrasslandGroundLayersSetup.Rebuild();PrototypeAddressablesSetup.EnsureSetup();for(int i=0;i<all.Length;i++)Check(saved[i]==Hash(all[i]),"repeat/previous entries/normal Setup preserves later hand edits "+all[i]);
            }
            finally{File.WriteAllBytes(paths[0],bytes);AssetDatabase.ImportAsset(paths[0],ImportAssetOptions.ForceUpdate);}
            Debug.Log("Grassland shrubs/stones bake verification PASSED: "+changed+" vertices; topology/GUID/all UVs, unselected flowers/grass, complete prefab, ground/collision/ecology/other biomes retained; custom metadata and later manual edits protected.");
        }
        public static void After(){BiomeIslandsVerification.Run();}
        public static void Visit(GameManager g)
        {
            var renderers=g.Terrain.Terrain.SeasonalDetails.MeadowBatches.Where(Authored).ToArray();if(renderers.Length==0)return;
            var filters=renderers.Select(r=>r.GetComponent<MeshFilter>()).ToArray();var meshes=filters.Select(f=>f.sharedMesh).ToArray();var roots=renderers.SelectMany(PlacementRoots).Distinct().ToArray();Check(roots.Length>0,"existing composition roots");
            var camera=g.Players.Player.ViewCamera;var position=camera.transform.position;var rotation=camera.transform.rotation;float size=camera.orthographicSize,far=camera.farClipPlane;
            var state=g.Weather.State;float rain=g.Weather.RainAmount,time=Shader.GetGlobalFloat("_IslandWindTime"),strength=Shader.GetGlobalFloat("_IslandWindStrength");var stateProperty=typeof(WeatherManager).GetProperty("State");var rainProperty=typeof(WeatherManager).GetProperty("RainAmount");
            try
            {
                camera.farClipPlane=3200;
                foreach(var m in meshes){var source=AssetDatabase.LoadAssetAtPath<Mesh>(BiomeIslandsSetup.Folder+"/"+m.name+".asset");Check(source!=null&&source.vertices.SequenceEqual(m.vertices)&&source.normals.SequenceEqual(m.normals)&&source.colors.SequenceEqual(m.colors),"actual Packed refined stone/shrub data");}
                foreach(string light in new[]{"Day","Dusk","Rain"})
                {
                    bool wet=light=="Rain";stateProperty.SetValue(g.Weather,new WeatherState(state.Seed,0,state.Day,wet?WeatherKind.Rainy:WeatherKind.Sunny));rainProperty.SetValue(g.Weather,wet?1f:0f);g.Weather.Tick(light=="Dusk"?1100:720,true);g.Ocean.Surface.Tick(0,light=="Dusk"?.4f:1,wet,g.Weather.Sun);Shader.SetGlobalFloat("_IslandWindTime",12);Shader.SetGlobalFloat("_IslandWindStrength",wet?1.1f:.65f);
                    if(Directory.Exists(Baseline))
                    {
                        for(int i=0;i<filters.Length;i++){filters[i].sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Baseline+"/"+meshes[i].name+".asset");Check(filters[i].sharedMesh!=null,"original geometry fixtures");}Views(g,camera,roots,Folder+"/Before/"+light);for(int i=0;i<filters.Length;i++)filters[i].sharedMesh=meshes[i];
                    }
                    Views(g,camera,roots,Folder+"/After/"+light);
                }
                g.Terrain.Terrain.SeasonalDetails.Present(g.Calendar.Date.Season,false);Check(renderers.All(r=>!r.enabled),"indoor visibility owner retained");g.Terrain.Terrain.SeasonalDetails.Present(g.Calendar.Date.Season,true);
                Check(!File.Exists(g.Saves.SavePath),"no daytime save");Debug.Log("Grassland shrubs/stones Packed verification PASSED: actual mesh/color/normal data, same-frame default and close day/dusk/rain comparison, original roots/material/season/cut owner, indoor visibility, no daytime save.");
            }
            finally
            {
                for(int i=0;i<filters.Length;i++)filters[i].sharedMesh=meshes[i];stateProperty.SetValue(g.Weather,state);rainProperty.SetValue(g.Weather,rain);g.Weather.Tick(720,true);g.Ocean.Surface.Tick(0,1,false,g.Weather.Sun);Shader.SetGlobalFloat("_IslandWindTime",time);Shader.SetGlobalFloat("_IslandWindStrength",strength);camera.transform.SetPositionAndRotation(position,rotation);camera.orthographicSize=size;camera.farClipPlane=far;
            }
        }
        static void Views(GameManager g,Camera camera,Vector2[] roots,string folder)
        {
            Directory.CreateDirectory(folder);int number=0;
            foreach(var target in new[]{new Vector2(-110,478),new Vector2(-112,537),new Vector2(-88,612)})
            {
                var p=roots.OrderBy(q=>(q-target).sqrMagnitude).First();Check(g.Terrain.TryResolve(new Vector3(p.x,0,p.y),out var point),"actual original composition floor");
                Frame(camera,point+Vector3.up*.35f,3.1f);Capture(camera,folder+"/Detail"+number+".png");Frame(camera,point+Vector3.up*.6f,7);Capture(camera,folder+"/Game"+number+".png");number++;
            }
        }
        static void Frame(Camera camera,Vector3 focus,float size){camera.transform.rotation=Quaternion.Euler(42,0,0);camera.transform.position=focus-camera.transform.forward*18;camera.orthographicSize=size;}
        static void Capture(Camera camera,string path)
        {
            var rt=new RenderTexture(1280,800,24);var old=camera.targetTexture;var active=RenderTexture.active;var image=new Texture2D(1280,800,TextureFormat.RGB24,false);
            try{camera.targetTexture=rt;camera.Render();camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1280,800),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());}
            finally{camera.targetTexture=old;RenderTexture.active=active;Object.DestroyImmediate(rt);Object.DestroyImmediate(image);}
        }
    }
}
