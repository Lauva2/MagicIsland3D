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
    public static class GrasslandCrownWindVerification
    {
        const string Folder="Logs/GrasslandCrownWind",Key="MagicIsland.CrownWindVerification";
        static void Check(bool ok,string why){if(!ok)throw new InvalidOperationException("Crown wind verification: "+why);}
        static string Hash(string p){using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(p)));}
        static Vector4[] UV(Mesh m,int channel){var a=new List<Vector4>();m.GetUVs(channel,a);return a.ToArray();}
        static void Tick(WeatherManager weather,float seconds,bool paused)=>typeof(WeatherManager).GetMethod("TickWind",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(weather,new object[]{seconds,paused});
        public static void BakeAndVerify()
        {
            Check(Application.dataPath.Replace('\\','/').Contains("/Logs/"),"isolated validation project");var root=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);GrasslandCrownWindSetup.Edit[] plan;
            try{plan=GrasslandCrownWindSetup.Plan(root.GetComponent<IslandTerrain>());}finally{PrefabUtility.UnloadPrefabContents(root);}
            var paths=plan.Select(e=>AssetDatabase.GetAssetPath(e.Mesh)).ToArray();var oldV=plan.Select(e=>e.Mesh.vertices).ToArray();var oldN=plan.Select(e=>e.Mesh.normals).ToArray();var oldC=plan.Select(e=>e.Mesh.colors).ToArray();var oldT=plan.Select(e=>e.Mesh.triangles).ToArray();var oldB=plan.Select(e=>e.Mesh.bounds).ToArray();var guids=paths.Select(AssetDatabase.AssetPathToGUID).ToArray();var oldUV=plan.Select(e=>Enumerable.Range(0,8).Select(ch=>UV(e.Mesh,ch)).ToArray()).ToArray();
            var excluded=new HashSet<string>(paths.SelectMany(p=>new[]{p,p+".meta"}));var files=Directory.GetFiles("Assets/Art","*",SearchOption.AllDirectories).Select(p=>p.Replace('\\','/')).Where(p=>!excluded.Contains(p)).ToArray();var hashes=files.Select(Hash).ToArray();
            var importer=AssetImporter.GetAtPath(paths[0]);string metadata=importer.userData;
            try
            {
                importer.userData="Artist.Custom";root=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);bool refused=false;
                try{GrasslandCrownWindSetup.Plan(root.GetComponent<IslandTerrain>());}catch(InvalidOperationException){refused=true;}finally{PrefabUtility.UnloadPrefabContents(root);}Check(refused,"unknown metadata refused before writes");
            }
            finally{importer.userData=metadata;}
            var mesh=plan[0].Mesh;var uv=oldUV[0][0].ToArray();uv[Array.FindIndex(plan[0].Leaves,b=>b)]=Vector4.one;
            try
            {
                mesh.SetUVs(0,uv.ToList());root=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);bool refused=false;
                try{GrasslandCrownWindSetup.Plan(root.GetComponent<IslandTerrain>());}catch(InvalidOperationException){refused=true;}finally{PrefabUtility.UnloadPrefabContents(root);}Check(refused,"artist-authored leaf UV refused before writes");
            }
            finally{mesh.SetUVs(0,oldUV[0][0].ToList());}
            GrasslandCrownWindSetup.Rebuild();int leaves=0;var treePhases=new HashSet<float>();var leafPhases=new HashSet<float>();
            for(int p=0;p<plan.Length;p++)
            {
                var m=plan[p].Mesh;Check(AssetDatabase.AssetPathToGUID(paths[p])==guids[p]&&m.vertices.SequenceEqual(oldV[p])&&m.normals.SequenceEqual(oldN[p])&&m.colors.SequenceEqual(oldC[p])&&m.triangles.SequenceEqual(oldT[p]),"GUID, entire authored geometry/normals/colors/topology retained");
                for(int ch=0;ch<8;ch++)Check(UV(m,ch).SequenceEqual(ch==0?plan[p].UV:oldUV[p][ch]),"planned UV0 only, all other channels retained");
                var data=UV(m,0);for(int i=0;i<data.Length;i++)
                {
                    if(!plan[p].Leaves[i]){Check(data[i]==oldUV[p][0][i],"all trunks, rocks, retired vertices and other biomes stationary");continue;}
                    leaves++;Check(data[i].x>=-3&&data[i].x<=-2&&data[i].w>0&&data[i].w<.25f,"bounded height weight and positive inverse tree height");treePhases.Add(data[i].y);leafPhases.Add(data[i].z);
                    // Maximum shader parameters and wind clamp displace at most .312 m in world space.
                    var b=m.bounds;Check(b.Contains(oldV[p][i]+Vector3.one*.313f)&&b.Contains(oldV[p][i]-Vector3.one*.313f),"conservative complete wind culling envelope");
                }
                Check(m.bounds.center==oldB[p].center&&m.bounds.size==oldB[p].size+Vector3.one*.65f,"only conservative bounds expanded");
            }
            Check(treePhases.Count>=20&&leafPhases.Count>=100,"independent tree and leaf-group phases");for(int i=0;i<files.Length;i++)Check(Hash(files[i])==hashes[i],"entire prefab, collisions, materials, other biomes and ecology retained "+files[i]);
            byte[] original=File.ReadAllBytes(paths[0]);
            try
            {
                var data=UV(mesh,0);data[Array.FindIndex(plan[0].Leaves,b=>b)].y+=.1f;mesh.SetUVs(0,data.ToList());EditorUtility.SetDirty(mesh);AssetDatabase.SaveAssets();var all=Directory.GetFiles("Assets/Art","*",SearchOption.AllDirectories);var saved=all.Select(Hash).ToArray();
                GrasslandCrownWindSetup.Rebuild();GrasslandCrownSetup.Rebuild();PrototypeAddressablesSetup.EnsureSetup();for(int i=0;i<all.Length;i++)Check(Hash(all[i])==saved[i],"repeat/crown entry/normal Setup preserve later manual edits "+all[i]);
            }
            finally{File.WriteAllBytes(paths[0],original);AssetDatabase.ImportAsset(paths[0],ImportAssetOptions.ForceUpdate);}
            Debug.Log("Grassland crown wind bake verification PASSED: "+leaves+" marked vertices; independent tree/leaf phases, stationary trunks, geometry/topology/GUID/UV1-7/art retained, conservative maximum wind envelope, foreign metadata/UV refusal, repeat and hand-edit protection.");
        }
        public static void After()
        {
            SessionState.SetBool(Key,true);SessionState.SetFloat(Key+".Time",Shader.GetGlobalFloat("_IslandWindTime"));SessionState.SetFloat(Key+".Strength",Shader.GetGlobalFloat("_IslandWindStrength"));SessionState.SetVector3(Key+".Direction",Shader.GetGlobalVector("_IslandWindDirection"));BiomeIslandsVerification.Run();
        }
        public static void Inside(GameManager g)
        {
            if(!SessionState.GetBool(Key,false))return;float time=Shader.GetGlobalFloat("_IslandWindTime");float strength=Shader.GetGlobalFloat("_IslandWindStrength");double clock=g.Weather.Wind.Time;
            Tick(g.Weather,4,false);Check(g.Weather.Wind.Time==clock&&Shader.GetGlobalFloat("_IslandWindTime")==time&&Shader.GetGlobalFloat("_IslandWindStrength")==strength,"actual indoor wind frozen");
            var camera=g.Players.Player.ViewCamera;foreach(var f in g.Terrain.Terrain.World.Scenery.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.name.StartsWith("Landscape ",StringComparison.Ordinal)))Check((camera.cullingMask&(1<<f.gameObject.layer))==0,"original indoor culling hides landscape trees");
            Debug.Log("Grassland crown wind indoor verification PASSED: original camera culling and shared wind freeze.");
        }
        public static void Released(GameManager g)
        {
            if(!SessionState.GetBool(Key,false))return;Check(g.Weather.Wind==null&&Shader.GetGlobalFloat("_IslandWindTime")==SessionState.GetFloat(Key+".Time",0)&&Shader.GetGlobalFloat("_IslandWindStrength")==SessionState.GetFloat(Key+".Strength",0),"wind owner and original globals restored on exit");
            Check((Vector3)Shader.GetGlobalVector("_IslandWindDirection")==SessionState.GetVector3(Key+".Direction",Vector3.zero),"original wind direction restored");
        }
        public static void Visit(GameManager g)
        {
            var filters=g.Terrain.Terrain.World.Scenery.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.name.StartsWith("Landscape ",StringComparison.Ordinal)).Where(f=>
            {
                var a=AssetImporter.GetAtPath(BiomeIslandsSetup.Folder+"/"+f.sharedMesh.name+".asset");return a!=null&&GrasslandCrownWindSetup.Done(a.userData);
            }).ToArray();if(filters.Length==0)return;
            Directory.CreateDirectory(Folder);var camera=g.Players.Player.ViewCamera;var pos=camera.transform.position;var rot=camera.transform.rotation;float size=camera.orthographicSize,far=camera.farClipPlane;int mask=camera.cullingMask;var layers=filters.Select(f=>f.gameObject.layer).ToArray();
            float time=Shader.GetGlobalFloat("_IslandWindTime"),strength=Shader.GetGlobalFloat("_IslandWindStrength");var direction=Shader.GetGlobalVector("_IslandWindDirection");
            var state=g.Weather.State;float rain=g.Weather.RainAmount;var stateProperty=typeof(WeatherManager).GetProperty("State");var rainProperty=typeof(WeatherManager).GetProperty("RainAmount");
            try
            {
                foreach(var f in filters){var source=AssetDatabase.LoadAssetAtPath<Mesh>(BiomeIslandsSetup.Folder+"/"+f.sharedMesh.name+".asset");Check(source!=null&&UV(f.sharedMesh,0).SequenceEqual(UV(source,0))&&f.sharedMesh.bounds==source.bounds,"actual Packed wind data and bounds");Check(f.GetComponent<Renderer>().sharedMaterial.shader.isSupported,"Packed wind shader supported");}
                camera.farClipPlane=3200;camera.orthographicSize=7;camera.transform.rotation=Quaternion.Euler(42,0,0);var focus=new Vector3(-115.3122f,20,536.1499f);camera.transform.position=focus-camera.transform.forward*18;g.Weather.Tick(720,true);g.Ocean.Surface.Tick(0,1,false,g.Weather.Sun);
                Shader.SetGlobalFloat("_IslandWindStrength",.65f);Shader.SetGlobalFloat("_IslandWindTime",2);Capture(camera,Folder+"/SunnyA.png");Shader.SetGlobalFloat("_IslandWindTime",6);Capture(camera,Folder+"/SunnyB.png");
                // Isolate the existing landscape renderers, so flower/fish/water animation cannot pass the GPU check.
                foreach(var f in filters)f.gameObject.layer=30;camera.cullingMask=1<<30;
                Shader.SetGlobalFloat("_IslandWindTime",2);var a=Capture(camera,Folder+"/IsolatedA.png");Shader.SetGlobalFloat("_IslandWindTime",6);var b=Capture(camera,Folder+"/IsolatedB.png");Check(FrameChanges(a,b,out _)>64,"actual GPU crown movement without moving grass or animals");
                Shader.SetGlobalFloat("_IslandWindStrength",0);Shader.SetGlobalFloat("_IslandWindTime",2);a=Capture(camera,Folder+"/StillA.png");Shader.SetGlobalFloat("_IslandWindTime",6);b=Capture(camera,Folder+"/StillB.png");Check(StableFrame(a,b),"zero wind leaves all geometry stationary");
                Tick(g.Weather,4,true);a=Capture(camera,Folder+"/PausedA.png");Tick(g.Weather,4,true);b=Capture(camera,Folder+"/PausedB.png");Check(StableFrame(a,b),"actual shared-clock pause stable GPU frame");
                camera.cullingMask=mask;for(int i=0;i<filters.Length;i++)filters[i].gameObject.layer=layers[i];
                stateProperty.SetValue(g.Weather,new WeatherState(state.Seed,0,state.Day,WeatherKind.Rainy));rainProperty.SetValue(g.Weather,1f);g.Weather.Tick(720,true);g.Ocean.Surface.Tick(0,1,true,g.Weather.Sun);
                double rainTime=g.Weather.Wind.Time;Tick(g.Weather,2,false);Capture(camera,Folder+"/RainA.png");Tick(g.Weather,2,false);Capture(camera,Folder+"/RainB.png");
                Check(g.Weather.Wind.Time>rainTime&&Shader.GetGlobalFloat("_IslandWindStrength")== (float)g.Weather.Wind.Strength,"rain still driven by original WindSimulation and AA configuration");
                stateProperty.SetValue(g.Weather,new WeatherState(state.Seed,0,state.Day,WeatherKind.Sunny));rainProperty.SetValue(g.Weather,0f);g.Weather.Tick(720,true);g.Ocean.Surface.Tick(0,1,false,g.Weather.Sun);
                for(int i=0;i<16;i++){Shader.SetGlobalFloat("_IslandWindTime",2+i*.32f);Shader.SetGlobalFloat("_IslandWindStrength",.65f);Capture(camera,Folder+"/Frame"+i.ToString("D2")+".png");}
                Check(!File.Exists(g.Saves.SavePath),"no daytime save");Debug.Log("Grassland crown wind Packed verification PASSED: actual AA UVs/bounds/shader, isolated GPU motion/zero wind/pause, default-camera sequence, no new renderer/material or daytime save.");
            }
            finally
            {
                stateProperty.SetValue(g.Weather,state);rainProperty.SetValue(g.Weather,rain);g.Weather.Tick(720,true);g.Ocean.Surface.Tick(0,1,state.Kind==WeatherKind.Rainy,g.Weather.Sun);for(int i=0;i<filters.Length;i++)filters[i].gameObject.layer=layers[i];camera.cullingMask=mask;camera.transform.SetPositionAndRotation(pos,rot);camera.orthographicSize=size;camera.farClipPlane=far;Shader.SetGlobalFloat("_IslandWindTime",time);Shader.SetGlobalFloat("_IslandWindStrength",strength);Shader.SetGlobalVector("_IslandWindDirection",direction);
            }
        }
        public static bool StableFrame(byte[] a,byte[] b)=>FrameChanges(a,b,out int maximum)<=4&&maximum<=1;
        public static int FrameChanges(byte[] a,byte[] b,out int maximum)
        {
            var first=new Texture2D(2,2,TextureFormat.RGBA32,false);var second=new Texture2D(2,2,TextureFormat.RGBA32,false);maximum=0;
            try
            {
                Check(first.LoadImage(a)&&second.LoadImage(b)&&first.width==second.width&&first.height==second.height,"matching valid GPU image dimensions");
                var x=first.GetPixels32();var y=second.GetPixels32();int count=0;
                for(int i=0;i<x.Length;i++)
                {
                    int difference=Mathf.Max(Mathf.Max(Mathf.Abs(x[i].r-y[i].r),Mathf.Abs(x[i].g-y[i].g)),Mathf.Abs(x[i].b-y[i].b));
                    if(difference>0)count++;if(difference>maximum)maximum=difference;
                }
                return count;
            }
            finally{Object.DestroyImmediate(first);Object.DestroyImmediate(second);}
        }
        static byte[] Capture(Camera camera,string path)
        {
            var rt=new RenderTexture(1280,800,24);var old=camera.targetTexture;var active=RenderTexture.active;var image=new Texture2D(1280,800,TextureFormat.RGB24,false);
            try{camera.targetTexture=rt;camera.Render();camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1280,800),0,0);image.Apply();var bytes=image.EncodeToPNG();File.WriteAllBytes(path,bytes);return bytes;}
            finally{camera.targetTexture=old;RenderTexture.active=active;Object.DestroyImmediate(rt);Object.DestroyImmediate(image);}
        }
    }
}
