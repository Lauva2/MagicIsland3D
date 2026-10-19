using System;
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
    public static class GrasslandGroundLayersVerification
    {
        const string Folder="Logs/GrasslandGroundLayers";
        static void Check(bool ok,string why){if(!ok)throw new InvalidOperationException("Ground layers verification: "+why);}
        static string Hash(string p){using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(p)));}
        public static void BakeAndVerify()
        {
            Check(Application.dataPath.Replace('\\','/').Contains("/Logs/"),"isolated validation project");
            var files=Directory.GetFiles("Assets/Art","*",SearchOption.AllDirectories).Select(p=>p.Replace('\\','/')).Where(p=>p!=TerrainSetup.PrefabPath).ToArray();var hashes=files.Select(Hash).ToArray();
            // Custom ground materials are detected before any publishing or resource creation.
            byte[] originalPrefab=File.ReadAllBytes(TerrainSetup.PrefabPath);var root=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);
            try
            {
                var t=root.GetComponent<IslandTerrain>();var grounds=GrasslandGroundLayersSetup.Grounds(t);Check(grounds.Length>0,"grassland-only ground ownership");
                grounds[0].sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(GrasslandFloraSetup.MaterialPath);Check(PrefabUtility.SaveAsPrefabAsset(root,TerrainSetup.PrefabPath)!=null,"custom-material fixture");
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            try
            {
                bool refused=false;try{GrasslandGroundLayersSetup.Rebuild();}catch(InvalidOperationException){refused=true;}
                Check(refused&&!File.Exists(GrasslandGroundLayersSetup.MapPath)&&!File.Exists(GrasslandGroundLayersSetup.MaterialPath),"foreign ground material refused before writes");
            }
            finally{File.WriteAllBytes(TerrainSetup.PrefabPath,originalPrefab);AssetDatabase.ImportAsset(TerrainSetup.PrefabPath,ImportAssetOptions.ForceUpdate);}
            GrasslandGroundLayersSetup.Rebuild();
            for(int i=0;i<files.Length;i++)Check(Hash(files[i])==hashes[i],"existing ground, collisions, roads, flora, ecology, other biomes and art retained "+files[i]);
            VerifyAssets();var bytes=File.ReadAllBytes(GrasslandGroundLayersSetup.MaterialPath);
            try
            {
                var material=AssetDatabase.LoadAssetAtPath<Material>(GrasslandGroundLayersSetup.MaterialPath);material.SetFloat("_UnderlayStrength",.71f);EditorUtility.SetDirty(material);AssetDatabase.SaveAssets();
                var all=Directory.GetFiles("Assets/Art","*",SearchOption.AllDirectories);var saved=all.Select(Hash).ToArray();
                GrasslandGroundLayersSetup.Rebuild();GrasslandFloraSetup.Rebuild();PrototypeAddressablesSetup.EnsureSetup();for(int i=0;i<all.Length;i++)Check(saved[i]==Hash(all[i]),"repeat/previous entry/ordinary Setup preserves later hand edits "+all[i]);
            }
            finally{File.WriteAllBytes(GrasslandGroundLayersSetup.MaterialPath,bytes);AssetDatabase.ImportAsset(GrasslandGroundLayersSetup.MaterialPath,ImportAssetOptions.ForceUpdate);}
            Debug.Log("Grassland ground layers bake verification PASSED: existing art/geometry/collision/UV/GUID/other biomes protected, new roots and normals bounded, shared materials, repeat/manual preservation.");
        }
        public static void VerifyAssets()
        {
            var root=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);
            try
            {
                var t=root.GetComponent<IslandTerrain>();var group=t.World.Scenery.Find(GrasslandGroundLayersSetup.Group);Check(t.IsValid&&group!=null,"AA owner references");Check(group.GetComponentsInChildren<Collider>(true).Length==0&&group.GetComponentsInChildren<MonoBehaviour>(true).Length==0,"combined visual-only subtree");
                var grounds=GrasslandGroundLayersSetup.Grounds(t);Check(grounds.Length>0&&grounds.All(r=>r.sharedMaterial.shader.name==GrasslandGroundLayersSetup.ShaderName),"grassland ground shader only");
                var map=(TextureImporter)AssetImporter.GetAtPath(GrasslandGroundLayersSetup.MapPath);Check(!map.sRGBTexture&&!map.mipmapEnabled&&map.wrapMode==TextureWrapMode.Clamp&&map.textureCompression==TextureImporterCompression.Uncompressed,"linear clamped distribution field");
                foreach(var r in group.GetComponentsInChildren<Renderer>(true))
                {
                    Check(t.SeasonalDetails.MeadowBatches.Contains(r)&&r.sharedMaterial==AssetDatabase.LoadAssetAtPath<Material>(GrasslandFloraSetup.MaterialPath),"same seasonal, cut and wind owner");
                    var m=r.GetComponent<MeshFilter>().sharedMesh;var v=m.vertices;var a=m.uv;var b=m.uv2;var d=m.uv3;
                    Check(m.normals.All(n=>n.sqrMagnitude>.99f)&&v.Length==a.Length&&v.Length==b.Length&&v.Length==d.Length,"authored normals and complete root channels");
                    for(int i=0;i<v.Length;i++)Check(Vector2.Distance(new Vector2(v[i].x,v[i].z),b[i])<.861f&&a[i].x<=-2&&a[i].x>=-5&&GrasslandIslandMeadowSetup.RouteDistance(b[i])>=2.8f,"wind, root envelope and road clearance");
                }
                Check(t.SeasonalDetails.MeadowBatches.Length<=64,"bounded batch count");
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        public static void After(){VerifyAssets();BiomeIslandsVerification.Run();}
        public static void Visit(GameManager g)
        {
            var t=g.Terrain.Terrain;var group=t.World.Scenery.Find(GrasslandGroundLayersSetup.Group);if(group==null)return;
            Directory.CreateDirectory(Folder);var renderers=group.GetComponentsInChildren<Renderer>(true);var enabled=renderers.Select(r=>r.enabled).ToArray();var grounds=GrasslandGroundLayersSetup.Grounds(t);var materials=grounds.Select(r=>r.sharedMaterial).ToArray();
            var camera=g.Players.Player.ViewCamera;var pos=camera.transform.position;var rotation=camera.transform.rotation;float size=camera.orthographicSize,far=camera.farClipPlane;
            var state=g.Weather.State;float rain=g.Weather.RainAmount,time=Shader.GetGlobalFloat("_IslandWindTime"),strength=Shader.GetGlobalFloat("_IslandWindStrength");
            var stateProperty=typeof(WeatherManager).GetProperty("State");var rainProperty=typeof(WeatherManager).GetProperty("RainAmount");
            try
            {
                camera.farClipPlane=3200;
                foreach(var r in renderers)
                {
                    var m=r.GetComponent<MeshFilter>().sharedMesh;var source=AssetDatabase.LoadAssetAtPath<Mesh>(BiomeIslandsSetup.Folder+"/"+m.name+".asset");Check(source!=null&&source.vertices.SequenceEqual(m.vertices)&&source.colors.SequenceEqual(m.colors)&&source.uv.SequenceEqual(m.uv),"actual Packed low-grass and edge-flower data");
                }
                Check(grounds.Length>0&&grounds.All(r=>r.sharedMaterial.shader.name==GrasslandGroundLayersSetup.ShaderName&&r.sharedMaterial.GetTexture("_UnderlayMap")!=null),"actual AA ground shader and texture dependency");
                foreach(string light in new[]{"Day","Dusk","Rain"})
                {
                    bool wet=light=="Rain";stateProperty.SetValue(g.Weather,new WeatherState(state.Seed,0,state.Day,wet?WeatherKind.Rainy:WeatherKind.Sunny));rainProperty.SetValue(g.Weather,wet?1f:0f);
                    g.Weather.Tick(light=="Dusk"?1100:720,true);g.Ocean.Surface.Tick(0,light=="Dusk"?.4f:1,wet,g.Weather.Sun);Shader.SetGlobalFloat("_IslandWindTime",12);Shader.SetGlobalFloat("_IslandWindStrength",wet?1.1f:.65f);
                    foreach(var r in renderers)r.enabled=false;foreach(var r in grounds)r.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(GrasslandRevitalizationSetup.MaterialFolder+"Living Meadow Ground.mat");Views(camera,Folder+"/Before/"+light);
                    for(int i=0;i<renderers.Length;i++)renderers[i].enabled=enabled[i];for(int i=0;i<grounds.Length;i++)grounds[i].sharedMaterial=materials[i];Views(camera,Folder+"/After/"+light);
                }
                t.SeasonalDetails.Present(g.Calendar.Date.Season,false);Check(renderers.All(r=>!r.enabled),"new cover hidden indoors");t.SeasonalDetails.Present(g.Calendar.Date.Season,true);
                g.Weather.Tick(720,true);Frame(camera,new Vector3(-176,13,570));Shader.SetGlobalFloat("_IslandWindStrength",1);Shader.SetGlobalFloat("_IslandWindTime",2);var a=Capture(camera,Folder+"/WindA.png");Shader.SetGlobalFloat("_IslandWindTime",6);var b=Capture(camera,Folder+"/WindB.png");Check(!a.SequenceEqual(b),"actual GPU shared wind changes");
                var tick=typeof(WeatherManager).GetMethod("TickWind",BindingFlags.Instance|BindingFlags.NonPublic);tick.Invoke(g.Weather,new object[]{3f,true});var c=Capture(camera,Folder+"/PausedA.png");tick.Invoke(g.Weather,new object[]{3f,true});var d=Capture(camera,Folder+"/PausedB.png");Check(c.SequenceEqual(d),"shared-clock pause stable frame");
                Check(!File.Exists(g.Saves.SavePath),"no daytime save");Debug.Log("Grassland ground layers Packed verification PASSED: actual meshes/material/field, default day/dusk/rain comparisons, indoor visibility, GPU wind/pause, no daytime save.");
            }
            finally
            {
                for(int i=0;i<renderers.Length;i++)renderers[i].enabled=enabled[i];for(int i=0;i<grounds.Length;i++)grounds[i].sharedMaterial=materials[i];stateProperty.SetValue(g.Weather,state);rainProperty.SetValue(g.Weather,rain);g.Weather.Tick(720,true);g.Ocean.Surface.Tick(0,1,false,g.Weather.Sun);Shader.SetGlobalFloat("_IslandWindTime",time);Shader.SetGlobalFloat("_IslandWindStrength",strength);camera.transform.SetPositionAndRotation(pos,rotation);camera.orthographicSize=size;camera.farClipPlane=far;
            }
        }
        static void Views(Camera camera,string folder)
        {
            Directory.CreateDirectory(folder);Frame(camera,new Vector3(-176,13,570));Capture(camera,folder+"/FlowerSlope.png");Frame(camera,new Vector3(-126,13,532));Capture(camera,folder+"/Grove.png");Frame(camera,new Vector3(-115,13,445));Capture(camera,folder+"/Arrival.png");Frame(camera,new Vector3(-108,13,600));Capture(camera,folder+"/Meadow.png");
        }
        static void Frame(Camera camera,Vector3 focus){camera.transform.rotation=Quaternion.Euler(42,0,0);camera.transform.position=focus-camera.transform.forward*18;camera.orthographicSize=7;}
        static byte[] Capture(Camera camera,string path)
        {
            var rt=new RenderTexture(1280,800,24);var old=camera.targetTexture;var active=RenderTexture.active;var image=new Texture2D(1280,800,TextureFormat.RGB24,false);
            try{camera.targetTexture=rt;camera.Render();camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1280,800),0,0);image.Apply();var data=image.EncodeToPNG();File.WriteAllBytes(path,data);return data;}
            finally{camera.targetTexture=old;RenderTexture.active=active;Object.DestroyImmediate(rt);Object.DestroyImmediate(image);}
        }
    }
}
