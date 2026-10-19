using System;
using System.IO;
using System.Linq;
using System.Reflection;
using MagicIsland.Interaction;
using MagicIsland.Managers;
using MagicIsland.TerrainSystem;
using MagicIsland.UI.Views;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    [InitializeOnLoad] public static class StarterIslandWorldFlowVerification
    {
        const string Key="MagicIsland.StarterWorldFlow";
        static StarterIslandWorldFlowVerification(){if(SessionState.GetBool(Key,false))EditorApplication.update+=Tick;}
        static void Check(bool b,string why){if(!b)throw new InvalidOperationException("Starter world flow: "+why);}
        static void Phase(int n){SessionState.SetInt(Key+".Phase",n);Debug.Log("Starter world phase "+n);}
        public static void Run()
        {
            if(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(TerrainSetup.PrefabPath).GetComponent<IslandTerrain>().World?.ArtVersion==3)
            {BiomeIslandsVerification.Run();return;}
            StarterIslandWorldVerification.Run();
            StarterIslandNatureVerification.Run();
            string[] paths={TerrainSetup.PrefabPath,OceanSetup.PrefabPath,MapPanelSetup.Path,"Assets/Art/Materials/Terrain/Lake Water.mat"};
            var bytes=paths.Select(File.ReadAllBytes).ToArray();PrototypeAddressablesSetup.EnsureSetup();
            for(int i=0;i<paths.Length;i++)Check(bytes[i].SequenceEqual(File.ReadAllBytes(paths[i])),"normal Setup preserves authored world "+paths[i]);
            EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");PrototypeAddressablesSetup.BuildContent();
            var settings=AddressableAssetSettingsDefaultObject.Settings;settings.ActivePlayModeDataBuilderIndex=settings.DataBuilders.FindIndex(b=>b is BuildScriptPackedPlayMode);
            SessionState.SetBool(Key,true);SessionState.SetFloat(Key+".Start",(float)EditorApplication.timeSinceStartup);Phase(0);
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;EditorApplication.isPlaying=true;
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;int phase=SessionState.GetInt(Key+".Phase",0);
            if(phase==14&&!EditorApplication.isPlaying){SessionState.SetBool(Key,false);EditorApplication.Exit(0);return;}
            try
            {
                Check(EditorApplication.timeSinceStartup-SessionState.GetFloat(Key+".Start",0)<420,"timeout "+phase);
                if(!EditorApplication.isPlaying)return;var g=GameManager.Instance;if(g==null)return;Check(g.State!=GameFlowState.Failed,"initialization");
                if(phase==0&&g.State==GameFlowState.MainMenu)
                {
                    string dir=Path.GetFullPath("Logs/StarterWorld/Saves/"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
                    g.Saves.Initialize(g.Configuration.Config,g.Configuration.Items,g.Configuration.Crops,dir,g.Configuration.Tasks,g.Configuration.Orders,g.Configuration.Seasons,g.Configuration.Gathering,g.Configuration.ToolUpgrades,g.Configuration.Storage,g.Configuration.NPCs,g.Configuration.Relationships,g.Configuration.Stories,g.Configuration.NPCSchedules,g.Configuration.Fishing,g.Configuration.Crafting,g.Configuration.Animals,g.Configuration.Orchards);
                    g.Settings.Initialize(dir+"/Settings");g.EnterGame();Phase(1);
                }
                else if(phase==1&&g.State==GameFlowState.Playing)
                {
                    Directory.CreateDirectory("Logs/StarterWorld");Check(!File.Exists(g.Saves.SavePath),"new game does not save");
                    Check(Vector3.Distance(g.Players.Player.transform.position,g.Terrain.Terrain.Biomes.NewGameSpawn)<.2f,"beach spawn preserved");
                    VerifyCollision(g);VerifyWeather(g);VerifyWalk(g);StarterIslandNatureVerification.VerifyRuntime(g);CaptureWorld(g);g.ToggleMap();Phase(2);
                }
                else if(phase==2&&Object.FindObjectOfType<MapView>()!=null)
                {
                    var view=Object.FindObjectOfType<MapView>();Check(!g.Map.Model.WorldOverview&&g.Map.Model.Locations.Count==12,"village map readable");view.ScaleButton.onClick.Invoke();
                    Check(g.Map.Model.WorldOverview&&g.Map.Model.Locations.Count==11,"world map and actual landmarks");
                    Vector3 before=g.Players.Player.transform.position;
                    foreach(var entry in g.Map.Model.Locations.Select((p,i)=>new{p,i}).ToArray())
                    {view.LocationButtons[entry.i].onClick.Invoke();view.DetailText.ForceMeshUpdate(true);Check(view.DetailText.text.All(c=>char.IsControl(c)||view.DetailText.font.HasCharacter(c)),"Chinese glyph "+entry.p.Name);Check(!view.DetailText.isTextOverflowing,"map detail layout "+entry.p.Name);}
                    Check(before==g.Players.Player.transform.position&&!g.Players.Player.enabled,"map pauses without teleport");
                    GameUiPreview.CaptureInventory(g.Players.Player.ViewCamera);File.Copy("Logs/InventoryPreview.png","Logs/StarterWorld/Map.png",true);
                    g.CloseMap();g.Players.RestorePosition(g.Housing.Entrance.Position+new Vector3(0,.1f,-1.4f));g.EnterHome();Phase(3);
                }
                else if(phase==3&&g.State==GameFlowState.Playing&&g.Housing.Inside&&!g.Ocean.Surface.WorldEcology.gameObject.activeSelf)
                {Check(!g.Ocean.Surface.WorldEcology.gameObject.activeSelf&&!g.Ocean.Surface.WorldEcology.AncientTree.enabled,"indoor hides world fish and ancient tree");g.Players.RestorePosition(g.Configuration.Housing.InteriorOrigin+new Vector3(-1.5f,.1f,2));g.RequestRest();Phase(4);}
                else if(phase==4&&Object.FindObjectOfType<SleepView>()!=null){Object.FindObjectOfType<SleepView>().ConfirmButton.onClick.Invoke();Phase(5);}
                else if(phase==5&&g.UI.HasDailyReport&&!g.Saves.SleepSavePending)
                {Check(g.Saves.TryLoad(out var save)&&save.Day==2,"bed sleep saves complete next morning");g.StartNewDayFromReport();g.ReturnToMainMenu();Phase(6);}
                else if(phase==6&&Object.FindObjectOfType<PauseView>()!=null){Object.FindObjectOfType<PauseView>().ReturnMenuButton.onClick.Invoke();Phase(7);}
                else if(phase==7&&g.State==GameFlowState.MainMenu)
                {
                    Check(!g.Terrain.OwnsTerrain&&Object.FindObjectsOfType<StarterIslandWorld>(true).Length==0,"AA world cleanup");
                    Check(Object.FindObjectsOfType<MagicIsland.Ocean.WorldWaterEcology>(true).Length==0,"world ecology cleanup");
                    Check(g.Saves.TryLoad(out var save),"load sleep checkpoint");
                    save.PlayerArea=HousingState.Island;save.PlayerX=0;save.PlayerY=.05f;save.PlayerZ=-4.6f;
                    File.WriteAllText(g.Saves.SavePath,JsonUtility.ToJson(save,true));g.Saves.RefreshAvailability();g.ContinueGame();Phase(8);
                }
                else if(phase==8&&g.State==GameFlowState.Playing)
                {
                    Check(Vector2.Distance(new Vector2(g.Players.Player.transform.position.x,g.Players.Player.transform.position.z),new Vector2(0,-4.6f))<.05f,"old village checkpoint retains coordinates");g.ReturnToMainMenu();Phase(9);
                }
                else if(phase==9&&Object.FindObjectOfType<PauseView>()!=null){Object.FindObjectOfType<PauseView>().ReturnMenuButton.onClick.Invoke();Phase(10);}
                else if(phase==10&&g.State==GameFlowState.MainMenu)
                {
                    Check(g.Saves.TryLoad(out var save),"checkpoint fixture");save.PlayerX=-120;save.PlayerZ=1000;save.PlayerY=(float)StarterIslandLayout.Ground(-120,1000)+.05f;
                    File.WriteAllText(g.Saves.SavePath,JsonUtility.ToJson(save,true));SessionState.SetString(Key+".Save",File.ReadAllText(g.Saves.SavePath));g.Saves.RefreshAvailability();g.ContinueGame();Phase(11);
                }
                else if(phase==11&&g.State==GameFlowState.Playing)
                {
                    var p=g.Players.Player.transform.position;Check(Vector2.Distance(new Vector2(p.x,p.z),new Vector2(-120,1000))<.05f&&p.y>30,"new mountainous checkpoint resolves real terrain");
                    Check(File.ReadAllText(g.Saves.SavePath)==SessionState.GetString(Key+".Save",""),"continue does not rewrite checkpoint");
                    g.ReturnToMainMenu();Phase(12);
                }
                else if(phase==12&&Object.FindObjectOfType<PauseView>()!=null){Object.FindObjectOfType<PauseView>().ReturnMenuButton.onClick.Invoke();Phase(13);}
                else if(phase==13&&g.State==GameFlowState.MainMenu)
                {
                    Check(!g.Terrain.OwnsTerrain&&!g.Ocean.OwnsOcean&&Object.FindObjectsOfType<StarterIslandWorld>(true).Length==0,"re-entry releases terrain/water dependencies");
                    File.WriteAllText("Logs/StarterWorld/Flow.txt","PASS: packed AA, beach spawn, actual ground support and connected routes, capsule walking, lake/river blocking and bridge, rendered overview and landmarks, map scale/Chinese/pause, real bed sleep, old coordinates and mountainous continue, menu/re-entry and cleanup.\n");
                    Debug.Log("Starter island world flow verification PASSED.");Phase(14);EditorApplication.isPlaying=false;
                }
            }
            catch(Exception e){Debug.LogException(e);SessionState.SetBool(Key,false);EditorApplication.isPlaying=false;EditorApplication.Exit(1);}
        }
        static void VerifyCollision(GameManager g)
        {
            var r=StarterIslandLayout.Route;var terrain=g.Terrain.Terrain;
            for(int i=1;i<r.GetLength(0);i++)
            {
                var a=new Vector2((float)r[i-1,0],(float)r[i-1,1]);var b=new Vector2((float)r[i,0],(float)r[i,1]);int n=Mathf.CeilToInt(Vector2.Distance(a,b)/2);
                for(int j=0;j<=n;j++)
                {
                    var p=Vector2.Lerp(a,b,j/(float)n);Check(g.Terrain.TryResolve(new Vector3(p.x,0,p.y),out var supported),"route ground at "+p);
                    var controller=g.Players.Player.GetComponent<CharacterController>();var center=supported+controller.center;float half=controller.height*.5f-controller.radius;
                    var blockers=Physics.OverlapCapsule(center-Vector3.up*half,center+Vector3.up*half,controller.radius*.9f,~0,QueryTriggerInteraction.Ignore);
                    Check(blockers.All(c=>terrain.IsGround(c)||c.transform.IsChildOf(g.Players.Player.transform)),"solid route obstruction at "+p);
                }
            }
            Check(!g.Terrain.TryResolve(new Vector3(160,12,630),out _),"lake prevents unsafe restore");
            Check(!g.Terrain.TryResolve(new Vector3(420,12,477),out _),"river prevents unsafe restore");
            Check(g.Ocean.Surface.WorldWaters.Length==3,"freshwater uses unified ocean clock");
        }
        static void VerifyWalk(GameManager g)
        {
            var movement=g.Players.Player;var method=movement.GetType().GetMethod("TickMovement",BindingFlags.Instance|BindingFlags.NonPublic);
            var camera=movement.ViewCamera;Quaternion before=camera.transform.rotation;camera.transform.rotation=Quaternion.Euler(45,0,0);
            try
            {
                // Real CharacterController crosses the opened old coastline and climbs the new approach.
                Check(g.Terrain.TryResolve(new Vector3(0,0,20),out var start),"boardwalk start");g.Players.RestorePosition(start);
                for(int i=0;i<1000;i++)method.Invoke(movement,new object[]{Vector2.up,.05f});
                Check(movement.transform.position.z>195&&movement.transform.position.y>9,"actual movement from village to mainland");
                Check(g.Terrain.TryResolve(new Vector3(-90,0,630),out start),"lake bank start");g.Players.RestorePosition(start);
                for(int i=0;i<350;i++)method.Invoke(movement,new object[]{Vector2.right,.05f});
                Check(StarterIslandLayout.Contains(movement.transform.position.x,movement.transform.position.z,.3),"walk cannot enter lake");
                Check(g.Terrain.TryResolve(new Vector3(504,0,459),out start),"bridge start");g.Players.RestorePosition(start);
                for(int i=0;i<170;i++)method.Invoke(movement,new object[]{new Vector2(-.6f,-.8f),.05f});
                Check(movement.transform.position.z<435,"actual bridge crossing");
                Check(g.Terrain.TryResolve(new Vector3(0,0,205),out start),"first trail approach");g.Players.RestorePosition(start);
                foreach(var target in new[]{new Vector2(0,240),new Vector2(-150,370),new Vector2(-100,520),new Vector2(-100,680),new Vector2(-100,700),new Vector2(-75,700),new Vector2(-32,700),new Vector2(-18,690),new Vector2(-8,690)})
                {
                    int steps=0;
                    while(Vector2.Distance(new Vector2(movement.transform.position.x,movement.transform.position.z),target)>.6f&&steps++<1800)
                    {
                        var direction=target-new Vector2(movement.transform.position.x,movement.transform.position.z);
                        method.Invoke(movement,new object[]{direction.normalized,.05f});
                    }
                    Check(steps<1800,"actual walk from mainland entrance to lake blocked near "+target+" at "+movement.transform.position);
                    Check(g.Terrain.Contains(movement.transform.position,.3f),"first exploration walk stays on dry ground");
                }
            }
            finally{camera.transform.rotation=before;}
        }
        static void VerifyWeather(GameManager g)
        {
            Check(g.Terrain.TryResolve(new Vector3(-120,0,1000),out var snow),"snow weather fixture");g.Players.RestorePosition(snow);
            g.Weather.Tick(750,false);
            Check(Vector3.Distance(g.Weather.Rain.transform.position,snow+Vector3.up*7.5f)<.01f,"bounded rain field follows high mainland ground");
            Check(Vector3.Distance(g.Weather.Clouds.transform.position,snow)<.01f,"existing clouds cover mainland altitude");
            var time=g.Weather.Clouds.AnimationTime;var position=g.Weather.Clouds.transform.position;g.Weather.Tick(750,true);
            Check(g.Weather.Clouds.AnimationTime==time&&g.Weather.Clouds.transform.position==position,"modal pauses mainland weather");
        }
        static void CaptureWorld(GameManager g)
        {
            LightingFlowVerification.SetTime(g,750);g.Ocean.Surface.Tick(0,g.Ocean.Surface.Daylight,false,g.Weather.Sun);
            var camera=g.Players.Player.ViewCamera;float far=camera.farClipPlane;camera.farClipPlane=4000;
            try
            {
                bool clouds=g.Weather.Clouds.gameObject.activeSelf;g.Weather.Clouds.gameObject.SetActive(false);
                try{LightingFlowVerification.Capture(g,"StarterWorld/Overview",new Vector3(0,20,620),740,50);}
                finally{g.Weather.Clouds.gameObject.SetActive(clouds);}
                foreach(var entry in new[]{new{n="Amber",p=new Vector3(-420,0,590),s=25f},new{n="AncientTree",p=new Vector3(-8,0,700),s=30f},new{n="Moonflowers",p=new Vector3(480,0,650),s=28f},new{n="Frost",p=new Vector3(-40,0,1000),s=25f}})
                {
                    var p=entry.p;p.y=(float)StarterIslandLayout.Ground(p.x,p.z)+6;LightingFlowVerification.Capture(g,"StarterWorld/"+entry.n,p,entry.s,50);
                }
                foreach(var entry in new[]{new{n="RouteBoardwalk",p=new Vector3(0,0,174),s=27f},new{n="RouteMeadow",p=new Vector3(-80,0,315),s=38f},new{n="RouteLake",p=new Vector3(-100,0,620),s=32f},new{n="RouteHeadland",p=new Vector3(-430,0,135),s=60f}})
                {
                    var p=entry.p;p.y=(float)StarterIslandLayout.Ground(p.x,p.z)+2;LightingFlowVerification.Capture(g,"StarterWorld/"+entry.n,p,entry.s,70);
                }
            }
            finally{camera.farClipPlane=far;}
            g.Players.RestorePosition(g.Terrain.Terrain.Biomes.NewGameSpawn);
        }
    }
}
