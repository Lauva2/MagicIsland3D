using System;
using System.IO;
using System.Linq;
using System.Reflection;
using MagicIsland.CalendarSystem;
using MagicIsland.CameraSystem;
using MagicIsland.Configuration;
using MagicIsland.Managers;
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
    [InitializeOnLoad] public static class SeasonalFlowVerification
    {
        const string Key="MagicIsland.SeasonalVerification";
        static SeasonalFlowVerification(){if(SessionState.GetBool(Key,false))EditorApplication.update+=Tick;}
        [MenuItem("Magic Island/Verify Seasonal Presentation Flow")]
        public static void Run()
        {
            VerifyConfiguration();PrototypeAddressablesSetup.EnsureSetup();AssetDatabase.SaveAssets();
            var paths=VegetationWindSetup.Prefabs.Concat(Directory.GetFiles(VegetationWindSetup.MaterialFolder,"*.mat"))
                .Concat(new[]{"Assets/Config/SeasonConfig.asset",TerrainSetup.PrefabPath,OceanSetup.PrefabPath,OceanSetup.MeshFolder+"/Island Shore.asset",FarmArtSetup.MaterialFolder+"/Plot Grass.mat",FarmArtSetup.MaterialFolder+"/Grass Tips.mat",SeasonalDetailsSetup.PrefabPath,SeasonalDetailsSetup.MaterialPath})
                .Concat(Directory.GetFiles(SeasonalDetailsSetup.Folder,"*.asset")).ToArray();
            var bytes=paths.Select(File.ReadAllBytes).ToArray();PrototypeAddressablesSetup.EnsureSetup();AssetDatabase.SaveAssets();
            for(int i=0;i<paths.Length;i++)Require(bytes[i].SequenceEqual(File.ReadAllBytes(paths[i])),"Setup preserves authored season/environment data "+paths[i]);
            SeasonalGroundVerification.VerifyAssets();
            foreach(string shader in new[]{"MagicIsland/Island Shore","MagicIsland/Vegetation Wind","MagicIsland/Vegetation Wind Fade","MagicIsland/Seasonal Grass"})
                Require(Shader.Find(shader)!=null&&!ShaderUtil.ShaderHasError(Shader.Find(shader)),"Shader compiles "+shader);
            EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");PrototypeAddressablesSetup.BuildContent();
            var settings=AddressableAssetSettingsDefaultObject.Settings;
            settings.ActivePlayModeDataBuilderIndex=settings.DataBuilders.FindIndex(b=>b is BuildScriptPackedPlayMode);
            foreach(string suffix in new[]{"Grass","Foliage","Evergreen"})
            {
                var value=Shader.GetGlobalVector("_IslandSeason"+suffix);
                SessionState.SetVector3(Key+suffix,value);SessionState.SetFloat(Key+suffix+".W",value.w);
            }
            SessionState.SetBool(Key,true);SessionState.SetFloat(Key+".Start",(float)EditorApplication.timeSinceStartup);Phase(0);
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;EditorApplication.isPlaying=true;
        }
        static void Require(bool ok,string why){if(!ok)throw new Exception("Seasonal presentation: "+why);}
        public static void VerifyConfiguration()
        {
            var config=ScriptableObject.CreateInstance<SeasonConfig>();
            try
            {
                Require(config.IsValid,"Default palettes valid");
                foreach(Season season in Enum.GetValues(typeof(Season)))
                {
                    var p=config.Palette(season);var color=p.Foliage;float strength=p.Strength;
                    foreach(float value in new[]{float.NaN,float.PositiveInfinity,-.01f,1.01f})
                    {
                        p.Foliage=new Color(value,.5f,.5f);Require(!config.IsValid,"Reject non-finite/out-of-range palette");p.Foliage=color;
                        p.Strength=value;Require(!config.IsValid,"Reject invalid blend strength");p.Strength=strength;
                    }
                    p.Strength=0;Require(config.IsValid,"Zero strength allows authored look");p.Strength=1;Require(config.IsValid,"Full strength valid");p.Strength=strength;
                }
                var saved=config.Winter;config.Winter=null;Require(!config.IsValid,"Reject missing season");config.Winter=saved;
                bool rejected=false;try{config.Palette((Season)4);}catch(ArgumentOutOfRangeException){rejected=true;}Require(rejected,"Reject unknown season");
                var original=Shader.GetGlobalVector("_IslandSeasonFoliage");var scope=new SeasonalPresentation();
                scope.Apply(config,Season.Autumn);scope.Clear();scope.Clear();Require(Shader.GetGlobalVector("_IslandSeasonFoliage")==original,"Idempotent global restoration");
            }
            finally{Object.DestroyImmediate(config);}
        }
        static void Phase(int p){SessionState.SetInt(Key+".Phase",p);SessionState.SetFloat(Key+".Time",(float)EditorApplication.timeSinceStartup);Debug.Log("Seasonal phase "+p);}
        static T View<T>()where T:Component=>Object.FindObjectOfType<T>();
        static void CheckSeason(GameManager g)
        {
            var p=g.Configuration.Seasons.Palette(g.Calendar.Date.Season);Color expected=p.Foliage.linear;
            var actual=Shader.GetGlobalVector("_IslandSeasonFoliage");
            Require(Vector3.Distance(new Vector3(expected.r,expected.g,expected.b),actual)<.0001f&&Mathf.Abs(actual.w-p.Strength)<.0001f,"Date-derived global palette "+g.Calendar.Date.Season);
        }
        static void InitializeSaves(GameManager g,string dir)=>g.Saves.Initialize(g.Configuration.Config,g.Configuration.Items,g.Configuration.Crops,dir,
            tasks:g.Configuration.Tasks,orders:g.Configuration.Orders,seasons:g.Configuration.Seasons,gathering:g.Configuration.Gathering,
            toolUpgrades:g.Configuration.ToolUpgrades,storage:g.Configuration.Storage,npcs:g.Configuration.NPCs,relationships:g.Configuration.Relationships,
            stories:g.Configuration.Stories,schedules:g.Configuration.NPCSchedules,fishing:g.Configuration.Fishing,crafting:g.Configuration.Crafting,
            animals:g.Configuration.Animals,orchards:g.Configuration.Orchards);
        static void EnterHome(GameManager g){g.Players.RestorePosition(g.Housing.Entrance.Position+new Vector3(0,.1f,-1.4f));g.EnterHome();}
        static void Rest(GameManager g){g.Players.RestorePosition(g.Configuration.Housing.InteriorOrigin+new Vector3(-1.5f,.1f,2));g.RequestRest();}
        static Color[] Capture(Camera camera,string name)
        {
            var target=new RenderTexture(1280,720,24);var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);
            var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;
            try
            {
                camera.targetTexture=target;camera.Render();RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();
                Directory.CreateDirectory("Logs");File.WriteAllBytes("Logs/Season"+name+".png",texture.EncodeToPNG());return texture.GetPixels();
            }
            finally{camera.targetTexture=oldTarget;RenderTexture.active=oldActive;target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(texture);}
        }
        static void RenderCheck(GameManager g)
        {
            var camera=g.Players.Player.ViewCamera;var position=camera.transform.position;float size=camera.orthographicSize;
            var canvases=Object.FindObjectsOfType<Canvas>();var visible=canvases.Select(c=>c.enabled).ToArray();
            var transforms=Object.FindObjectsOfType<VegetationWind>().Select(w=>new{Wind=w,Matrix=w.transform.localToWorldMatrix,Material=w.Target.sharedMaterial,Color=w.Target.sharedMaterial.color}).ToArray();
            var colliders=Object.FindObjectsOfType<Collider>().Select(c=>new{Collider=c,Bounds=c.bounds}).ToArray();
            var scope=new SeasonalPresentation();g.Cameras.SetOcclusionEnabled(false);
            try
            {
                foreach(var c in canvases)c.enabled=false;
                camera.orthographicSize=18;camera.transform.position=new Vector3(0,1,0)-camera.transform.forward*45;
                Color[] spring=null;
                foreach(Season season in Enum.GetValues(typeof(Season)))
                {
                    scope.Apply(g.Configuration.Seasons,season);g.Terrain.Terrain.SeasonalDetails.Present(season,true);var pixels=Capture(camera,season.ToString());
                    var overview=camera.transform.position;camera.orthographicSize=5;
                    camera.transform.position=new Vector3(-7,.6f,6)-camera.transform.forward*25;
                    Capture(camera,"Details"+season);camera.transform.position=overview;camera.orthographicSize=18;
                    if(spring==null)spring=pixels;
                    else Require(pixels.Zip(spring,(a,b)=>Mathf.Abs(a.r-b.r)+Mathf.Abs(a.g-b.g)+Mathf.Abs(a.b-b.b)>.04f?1:0).Sum()>2000,"Actual seasonal grass/foliage render "+season);
                }
                scope.Apply(g.Configuration.Seasons,Season.Autumn);
                var palm=transforms.First(t=>t.Wind.Evergreen);var tree=transforms.First(t=>!t.Wind.Evergreen);
                var block=new MaterialPropertyBlock();palm.Wind.Target.GetPropertyBlock(block);Require(block.GetFloat("_SeasonEvergreen")==1&&block.GetFloat("_WindBend")>0,"Evergreen classification coexists with wind MPB");
                tree.Wind.Target.GetPropertyBlock(block);Require(block.GetFloat("_SeasonEvergreen")==0,"Deciduous foliage uses autumn palette");
                var occluder=tree.Wind.GetComponentInParent<CameraOccluder>();
                try
                {
                    typeof(CameraOccluder).GetMethod("Fade",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(occluder,new object[]{true,.22f,.15f,1f});
                    Require(occluder.RuntimeMaterialCount>0&&tree.Wind.Target.sharedMaterial.shader.name=="MagicIsland/Vegetation Wind Fade","Seasonal foliage retains fade shader");
                    Capture(camera,"AutumnFade");
                }
                finally{occluder.Restore();}
                Require(transforms.All(t=>t.Wind.transform.localToWorldMatrix==t.Matrix&&t.Wind.Target.sharedMaterial==t.Material&&t.Material.color==t.Color),"No transform or shared material changes");
                Require(colliders.All(c=>c.Collider.bounds==c.Bounds),"Season changes keep collision geometry fixed");
            }
            finally{scope.Clear();g.Terrain.Terrain.SeasonalDetails.Present(g.Calendar.Date.Season,true);for(int i=0;i<canvases.Length;i++)canvases[i].enabled=visible[i];camera.transform.position=position;camera.orthographicSize=size;g.Cameras.SetOcclusionEnabled(true);}
            CheckSeason(g);
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;int p=SessionState.GetInt(Key+".Phase",0);
            if(p==20&&!EditorApplication.isPlaying){SessionState.SetBool(Key,false);EditorApplication.Exit(0);return;}
            if(EditorApplication.timeSinceStartup-SessionState.GetFloat(Key+".Time",0)<.3)return;
            try
            {
                Require(EditorApplication.timeSinceStartup-SessionState.GetFloat(Key+".Start",0)<240,"Timeout phase "+p);
                if(!EditorApplication.isPlaying)return;var g=GameManager.Instance;if(g==null)return;Require(g.State!=GameFlowState.Failed,"Startup");
                if(p==0&&g.State==GameFlowState.MainMenu)
                {
                    InitializeSaves(g,Path.GetFullPath("Logs/SeasonalSaves/"+Guid.NewGuid().ToString("N")));
                    g.Configuration.Seasons.DaysPerSeason=1;g.EnterGame();Phase(1);
                }
                else if(p==1&&g.State==GameFlowState.Playing)
                {
                    CheckSeason(g);Require(!File.Exists(g.Saves.SavePath),"Seasonal startup never saves daytime progress");
                    g.Time.Clock.Restore(750);g.Weather.Tick(750,true);g.OpenPause();Phase(2);
                }
                else if(p==2&&View<PauseView>()!=null)
                {
                    var before=Shader.GetGlobalVector("_IslandSeasonFoliage");RenderCheck(g);g.Weather.Tick(1320,true);CheckSeason(g);
                    SeasonalGroundVerification.VerifyRuntime(g);
                    Require(Shader.GetGlobalVector("_IslandSeasonFoliage")==before,"Pause/night never change calendar palette");
                    g.ResumeFromPause();g.Inventory.TryAdd(SeasonalGroundVerification.Placeable(g),1);
                    g.Placements.StartCoroutine(g.Placements.Begin(SeasonalGroundVerification.Placeable(g)));Phase(18);
                }
                else if(p==18&&g.Placements.Ready){SeasonalGroundVerification.VerifyPlacement(g);EnterHome(g);Phase(3);}
                else if(p==3&&g.State==GameFlowState.Playing&&g.Housing.Inside){CheckSeason(g);Require(!g.Terrain.Terrain.SeasonalDetails.gameObject.activeSelf,"Indoor hides ground details");Rest(g);Phase(4);}
                else if(p==4&&View<SleepView>()!=null){View<SleepView>().ConfirmButton.onClick.Invoke();Phase(5);}
                else if(p==5&&View<DailyReportView>()!=null)
                {
                    Require(!g.Saves.SleepSavePending&&g.Saves.TryLoad(out var saved)&&saved.Day==g.Farm.State.Day,"Real sleep persists next season checkpoint");CheckSeason(g);
                    g.StartNewDayFromReport();
                    if(g.Farm.State.Day==4){g.ReturnToMainMenu();Phase(6);}
                    else if(g.Farm.State.Day<5){Rest(g);Phase(4);}
                    else{g.ReturnToMainMenu();Phase(10);}
                }
                else if((p==6||p==10)&&View<PauseView>()!=null){View<PauseView>().ReturnMenuButton.onClick.Invoke();Phase(p+1);}
                else if((p==7||p==11)&&g.State==GameFlowState.MainMenu)
                {
                    foreach(string suffix in new[]{"Grass","Foliage","Evergreen"})
                    {
                        var value=Shader.GetGlobalVector("_IslandSeason"+suffix);
                        Require(Vector3.Distance(value,SessionState.GetVector3(Key+suffix,Vector3.zero))<.0001f
                            &&Mathf.Abs(value.w-SessionState.GetFloat(Key+suffix+".W",0))<.0001f,"Menu restores previous "+suffix+" globals");
                    }
                    if(p==7){g.ContinueGame();Phase(8);}else{g.EnterGame();Phase(12);}
                }
                else if(p==8&&g.State==GameFlowState.Playing)
                {
                    Require(g.Farm.State.Day==4&&g.Calendar.Date.Season==Season.Winter&&g.Housing.Inside,"Load restores winter before input");CheckSeason(g);Rest(g);Phase(4);
                }
                else if(p==12&&g.State==GameFlowState.Playing)
                {
                    Require(g.Farm.State.Day==1&&g.Calendar.Date.Season==Season.Spring,"New game resets spring after winter/year transition");CheckSeason(g);
                    File.WriteAllText("Logs/SeasonalVerification.txt","PASS: packed-AA palette/ground assets and setup preservation; four seasonal overview/detail renders; bounded flowers/grass/leaves/frost; GPU leaf motion/pause and chopped canopy; maximum farm/routes and old construction; actual placement hiding/recovery; evergreen/wind/fade; unchanged transforms/collisions/shared materials; pause/night/indoor; four real bed sleeps across seasons/year; winter load; cleanup/re-entry; no new save fields.\n");
                    Debug.Log("Seasonal presentation flow verification PASSED.");Phase(20);EditorApplication.isPlaying=false;
                }
            }
            catch(Exception error){Debug.LogException(error);SessionState.SetBool(Key,false);EditorApplication.isPlaying=false;EditorApplication.Exit(1);}
        }
    }
}
