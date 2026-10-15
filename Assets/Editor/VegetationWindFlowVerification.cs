using System;
using System.IO;
using System.Linq;
using System.Reflection;
using MagicIsland.CameraSystem;
using MagicIsland.Configuration;
using MagicIsland.Managers;
using MagicIsland.TerrainSystem;
using MagicIsland.UI.Views;
using MagicIsland.WeatherSystem;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    [InitializeOnLoad] public static class VegetationWindFlowVerification
    {
        const string Key="MagicIsland.VegetationWindVerification";
        static VegetationWindFlowVerification(){if(SessionState.GetBool(Key,false))EditorApplication.update+=Tick;}
        public static void Run()
        {
            VegetationWindSetup.Prepare();
            var paths=VegetationWindSetup.Prefabs.Concat(Directory.GetFiles(VegetationWindSetup.MaterialFolder,"*.mat")).Concat(new[]{"Assets/Config/WeatherConfig.asset"}).ToArray();
            var bytes=paths.Select(File.ReadAllBytes).ToArray();VegetationWindSetup.Prepare();
            for(int i=0;i<paths.Length;i++)Require(bytes[i].SequenceEqual(File.ReadAllBytes(paths[i])),"Preserve authored wind prefabs/materials/config "+paths[i]);
            foreach(var path in VegetationWindSetup.Prefabs)
            {
                var wind=AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentsInChildren<VegetationWind>(true);
                Require(wind.Length>0&&wind.All(w=>w.Target!=null&&w.Target.sharedMaterial.shader.name=="MagicIsland/Vegetation Wind"),"Authored wind dependency "+path);
            }
            EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");PrototypeAddressablesSetup.BuildContent();var settings=AddressableAssetSettingsDefaultObject.Settings;
            settings.ActivePlayModeDataBuilderIndex=settings.DataBuilders.FindIndex(b=>b is BuildScriptPackedPlayMode);
            SessionState.SetFloat(Key+".OldTime",Shader.GetGlobalFloat("_IslandWindTime"));SessionState.SetFloat(Key+".OldStrength",Shader.GetGlobalFloat("_IslandWindStrength"));
            SessionState.SetBool(Key,true);SessionState.SetFloat(Key+".Start",(float)EditorApplication.timeSinceStartup);Phase(0);EditorApplication.update-=Tick;EditorApplication.update+=Tick;EditorApplication.isPlaying=true;
        }
        static void Require(bool ok,string why){if(!ok)throw new Exception("Vegetation wind: "+why);}
        static void Phase(int p){SessionState.SetInt(Key+".Phase",p);SessionState.SetFloat(Key+".Time",(float)EditorApplication.timeSinceStartup);Debug.Log("Vegetation wind phase "+p);}
        static T View<T>()where T:Component=>Object.FindObjectOfType<T>();
        static void Wind(GameManager g,float dt,bool paused=false)=>typeof(WeatherManager).GetMethod("TickWind",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(g.Weather,new object[]{dt,paused});
        static void Mix(GameManager g)=>typeof(AudioManager).GetMethod("TickAmbience",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(g.Audio,new object[]{4f});
        static void Move(GameManager g,Vector3 p){g.Players.RestorePosition(p);g.Players.Player.ViewCamera.GetComponent<CameraController>().SnapToTarget();Physics.SyncTransforms();}
        static void NoSave(GameManager g)=>Require(!File.Exists(g.Saves.SavePath),"Wind never writes daytime progress");
        static Color[] Capture(Camera camera,string name)
        {
            var target=new RenderTexture(1280,720,24);var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;
            try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();Directory.CreateDirectory("Logs");File.WriteAllBytes("Logs/"+name+".png",texture.EncodeToPNG());return texture.GetPixels();}
            finally{camera.targetTexture=oldTarget;RenderTexture.active=oldActive;target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(texture);}
        }
        static int Changed(Color[] a,Color[] b)=>a.Zip(b,(x,y)=>Mathf.Abs(x.r-y.r)+Mathf.Abs(x.g-y.g)+Mathf.Abs(x.b-y.b)>.04f?1:0).Sum();
        static void RenderCheck(GameManager g)
        {
            var palm=Object.FindObjectsOfType<VegetationWind>().First(w=>w.Radial&&w.name=="Palm crown");var root=palm.GetComponentInParent<CoastalSceneryPatch>();
            var camera=g.Players.Player.ViewCamera;var position=camera.transform.position;var rotation=camera.transform.rotation;float size=camera.orthographicSize;int mask=camera.cullingMask;Color background=camera.backgroundColor;
            var canvases=Object.FindObjectsOfType<Canvas>();var enabled=canvases.Select(c=>c.enabled).ToArray();var colliderBounds=Object.FindObjectsOfType<Collider>().Select(c=>new{Collider=c,Bounds=c.bounds,Matrix=c.transform.localToWorldMatrix}).ToArray();
            var transforms=Object.FindObjectsOfType<VegetationWind>().Select(w=>new{Wind=w,Matrix=w.transform.localToWorldMatrix}).ToArray();
            g.Cameras.SetOcclusionEnabled(false);
            try
            {
                foreach(var canvas in canvases)canvas.enabled=false;
                camera.orthographicSize=4.3f;camera.transform.position=root.transform.position+Vector3.up*1.5f-camera.transform.forward*25;
                Wind(g,1.2f);var first=Capture(camera,"WindSunnyPreviewA");Wind(g,2.4f);var second=Capture(camera,"WindSunnyPreviewB");
                Require(Changed(first,second)>500,"Real GPU wind changes rendered leaf silhouettes and shadows");
                foreach(var entry in colliderBounds)Require(entry.Collider.bounds==entry.Bounds&&entry.Collider.transform.localToWorldMatrix==entry.Matrix,"All collision geometry stays fixed during wind");
                Require(transforms.All(e=>e.Wind.transform.localToWorldMatrix==e.Matrix),"GPU wind does not move authored transforms or conflict with hit animation");
                // Isolate a custom-shader leaf, so opaque trunk fading cannot conceal an alpha regression.
                int layer=palm.gameObject.layer;palm.gameObject.layer=31;camera.cullingMask=1<<31;camera.backgroundColor=Color.black;camera.orthographicSize=2.3f;camera.transform.position=palm.Target.bounds.center-camera.transform.forward*20;
                var occluder=root.GetComponent<CameraOccluder>();
                try
                {
                    var opaque=Capture(camera,"WindOpaqueLeafPreview");
                    typeof(CameraOccluder).GetMethod("Fade",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(occluder,new object[]{true,.22f,.15f,1f});
                    Require(occluder.RuntimeMaterialCount>0&&palm.Target.sharedMaterial.color.a<.3f,"Wind materials opt into owned camera fade copies");
                    var faded=Capture(camera,"WindFadedLeafPreview");float a=opaque.Sum(p=>p.g),b=faded.Sum(p=>p.g);Require(a>500&&b<a*.80f,"Wind shader alpha actually blends in gameplay render "+a+"/"+b);
                    occluder.Restore();Require(palm.Target.sharedMaterial.shader.name=="MagicIsland/Vegetation Wind"&&occluder.RuntimeMaterialCount==0,"Fade restores authored wind materials");
                }
                finally{palm.gameObject.layer=layer;occluder.Restore();}
            }
            finally{for(int i=0;i<canvases.Length;i++)canvases[i].enabled=enabled[i];camera.transform.SetPositionAndRotation(position,rotation);camera.orthographicSize=size;camera.cullingMask=mask;camera.backgroundColor=background;g.Cameras.SetOcclusionEnabled(true);}
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;int p=SessionState.GetInt(Key+".Phase",0);
            if(p==20&&!EditorApplication.isPlaying){SessionState.SetBool(Key,false);EditorApplication.Exit(0);return;}
            if(EditorApplication.timeSinceStartup-SessionState.GetFloat(Key+".Time",0)<.4)return;
            try
            {
                Require(EditorApplication.timeSinceStartup-SessionState.GetFloat(Key+".Start",0)<240,"Timeout phase "+p);
                if(!EditorApplication.isPlaying)return;var g=GameManager.Instance;if(g==null)return;Require(g.State!=GameFlowState.Failed,"Startup");
                if(p==1&&g.State==GameFlowState.MainMenu)throw new Exception("Wind startup returned to menu");
                if(p==0&&g.State==GameFlowState.MainMenu)
                {
                    string dir=Path.GetFullPath("Logs/VegetationWindSaves/"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
                    g.Saves.Initialize(g.Configuration.Config,g.Configuration.Items,g.Configuration.Crops,dir,g.Configuration.Tasks,g.Configuration.Orders,g.Configuration.Seasons,g.Configuration.Gathering,g.Configuration.ToolUpgrades,g.Configuration.Storage,g.Configuration.NPCs,g.Configuration.Relationships,g.Configuration.Stories,g.Configuration.NPCSchedules,g.Configuration.Fishing,g.Configuration.Crafting,g.Configuration.Animals,g.Configuration.Orchards);
                    g.EnterGame();Phase(1);
                }
                else if(p==1&&g.State==GameFlowState.Playing){g.Time.Clock.Restore(750);g.Weather.Tick(750,true);g.OpenPause();Phase(2);}
                else if(p==2&&View<PauseView>()!=null)
                {
                    Require(g.Weather.Wind!=null&&Object.FindObjectsOfType<VegetationWind>().Length>60,"Packed AA coast and gathering foliage bound");
                    double time=g.Weather.Wind.Time,strength=g.Weather.Wind.Strength;Wind(g,20,true);Require(g.Weather.Wind.Time==time&&g.Weather.Wind.Strength==strength,"Pause freezes wind clock and gusts");
                    RenderCheck(g);Wind(g,12);float sunny=g.Weather.WindFactor;
                    var clouds=g.Weather.Clouds;var initial=clouds.Clouds[0].localPosition;clouds.Tick(0,1,false,true,.4f,g.Configuration.Weather.WindDirection);clouds.Tick(0,1,false,true,2.2f,g.Configuration.Weather.WindDirection);
                    Require(initial==clouds.Clouds[0].localPosition,"Changing cloud speed never teleports existing clouds");
                    clouds.Tick(3,1,false,true,g.Weather.WindFactor,g.Configuration.Weather.WindDirection);
                    Require(Mathf.Abs(Vector3.Distance(initial,clouds.Clouds[0].localPosition)-clouds.Wind.magnitude*g.Weather.WindFactor*3)<.01f,"Cloud velocity uses shared wind strength and direction");
                    g.Weather.State.NextDay(g.Weather.State.Day+1,1);Wind(g,0);Require(g.Weather.WindFactor==sunny,"Weather change has no sudden wind jump");Wind(g,18);
                    Require(g.Weather.WindFactor>sunny*1.25f,"Rain builds stronger wind smoothly");
                    g.Audio.TickEnvironment(g,0,false);Mix(g);float expected=g.Configuration.Audio.EffectsVolume*g.Configuration.Audio.WindVolume*g.Configuration.Audio.Find(AudioCue.Wind).Gain*Mathf.Clamp(g.Weather.WindFactor,0,3);
                    Require(Mathf.Abs(g.Audio.AmbientVolume(AudioCue.Wind)-expected)<.001f,"Wind sound uses exact shared wind factor");
                    g.Weather.Tick(750,true);g.ResumeFromPause();Move(g,g.Housing.Entrance.Position+new Vector3(0,.1f,-1.4f));g.EnterHome();Phase(3);
                }
                else if(p==3&&g.State==GameFlowState.Playing&&g.Housing.Inside)
                {
                    double time=g.Weather.Wind.Time;Wind(g,25);Require(time==g.Weather.Wind.Time&&!g.Weather.Clouds.gameObject.activeSelf,"Indoors freezes outdoor wind and hides clouds");NoSave(g);g.ReturnToMainMenu();Phase(4);
                }
                else if(p==4&&View<PauseView>()!=null){View<PauseView>().ReturnMenuButton.onClick.Invoke();Phase(5);}
                else if(p==5&&g.State==GameFlowState.MainMenu)
                {
                    Require(g.Weather.Wind==null&&Object.FindObjectsOfType<VegetationWind>(true).Length==0,"Menu clears wind foliage instances");
                    Require(Shader.GetGlobalFloat("_IslandWindTime")==SessionState.GetFloat(Key+".OldTime",0)&&Shader.GetGlobalFloat("_IslandWindStrength")==SessionState.GetFloat(Key+".OldStrength",0),"Wind restores previous shader globals");NoSave(g);g.EnterGame();Phase(6);
                }
                else if(p==6&&g.State==GameFlowState.Playing)
                {
                    Require(g.Weather.Wind.Time<2&&Object.FindObjectsOfType<VegetationWind>().Length>60,"Re-entry starts one fresh wind simulation");NoSave(g);
                    File.WriteAllText("Logs/VegetationWindVerification.txt","PASS: packed-AA authored foliage/materials/config preservation; shared sunny/rain wind and gusts; actual GPU deformation and fixed transforms/colliders; real custom-shader camera alpha/fade restoration; continuous cloud drift and shared sound gain; pause/interior freeze; global cleanup and re-entry; no daytime progress writes.\n");
                    Debug.Log("Vegetation wind flow verification PASSED.");Phase(20);EditorApplication.isPlaying=false;
                }
            }
            catch(Exception error){Debug.LogException(error);SessionState.SetBool(Key,false);EditorApplication.isPlaying=false;EditorApplication.Exit(1);}
        }
    }
}
