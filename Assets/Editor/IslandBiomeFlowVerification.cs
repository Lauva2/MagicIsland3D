using System;
using System.Collections.Generic;
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
using Object = UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    [InitializeOnLoad] public static class IslandBiomeFlowVerification
    {
        const string Key = "MagicIsland.IslandBiomeFlow";
        static IslandBiomeFlowVerification() { if (SessionState.GetBool(Key,false)) EditorApplication.update += Tick; }
        static void Require(bool b,string why) { if(!b)throw new InvalidOperationException("Biome flow: "+why); }
        static void Phase(int p) { SessionState.SetInt(Key+".Phase",p);Debug.Log("Island biome phase "+p); }
        public static void Run()
        {
            if(AssetDatabase.LoadAssetAtPath<GameObject>(TerrainSetup.PrefabPath).GetComponent<IslandTerrain>().World!=null){StarterIslandWorldFlowVerification.Run();return;}
            IslandBiomeVerification.Run();EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");PrototypeAddressablesSetup.BuildContent();
            var settings=AddressableAssetSettingsDefaultObject.Settings;settings.ActivePlayModeDataBuilderIndex=settings.DataBuilders.FindIndex(b=>b is BuildScriptPackedPlayMode);
            SessionState.SetBool(Key,true);SessionState.SetFloat(Key+".Start",(float)EditorApplication.timeSinceStartup);Phase(0);
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;EditorApplication.isPlaying=true;
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;int p=SessionState.GetInt(Key+".Phase",0);
            if(p==15&&!EditorApplication.isPlaying){SessionState.SetBool(Key,false);EditorApplication.Exit(0);return;}
            try
            {
                Require(EditorApplication.timeSinceStartup-SessionState.GetFloat(Key+".Start",0)<300,"timeout phase "+p);
                if(!EditorApplication.isPlaying)return;var g=GameManager.Instance;if(g==null)return;Require(g.State!=GameFlowState.Failed,"world initialization");
                if(p==0&&g.State==GameFlowState.MainMenu)
                {
                    string dir=Path.GetFullPath("Logs/IslandBiomes/Saves/"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
                    g.Saves.Initialize(g.Configuration.Config,g.Configuration.Items,g.Configuration.Crops,dir,g.Configuration.Tasks,g.Configuration.Orders,g.Configuration.Seasons,g.Configuration.Gathering,g.Configuration.ToolUpgrades,g.Configuration.Storage,g.Configuration.NPCs,g.Configuration.Relationships,g.Configuration.Stories,g.Configuration.NPCSchedules,g.Configuration.Fishing,g.Configuration.Crafting,g.Configuration.Animals,g.Configuration.Orchards);
                    g.Settings.Initialize(dir+"/Settings");g.EnterGame();Phase(1);
                }
                else if(p==1&&g.State==GameFlowState.Playing)
                {
                    BeachStart(g);Require(!File.Exists(g.Saves.SavePath),"new game creates no progress file");
                    g.Time.Clock.Restore(750);g.Weather.Tick(750,true);VerifyRuntime(g);g.ToggleMap();Phase(2);
                }
                else if(p==2&&Object.FindObjectOfType<MapView>()!=null)
                {
                    Require(g.Map.Model.Biomes==g.Terrain.Terrain.Biomes&&g.Map.Model.Region.Contains("沙滩"),"map uses AA regions and beach location");
                    var map=Object.FindObjectOfType<MapView>();map.Render(g.Map.Model);map.RegionText.ForceMeshUpdate(true);
                    Require(!map.RegionText.isTextOverflowing&&map.RegionText.text.All(c=>char.IsControl(c)||map.RegionText.font.HasCharacter(c)),"actual Chinese map label fits and has glyphs");
                    GameUiPreview.CaptureInventory(g.Players.Player.ViewCamera);File.Copy("Logs/InventoryPreview.png","Logs/IslandBiomesMap.png",true);
                    g.CloseMap();string item=SeasonalGroundVerification.Placeable(g);Require(g.Inventory.TryAdd(item,1),"placement fixture item");
                    g.Placements.StartCoroutine(g.Placements.Begin(item));Phase(3);
                }
                else if(p==3&&g.Placements.Ready)
                {
                    VerifyPlacement(g);Require(!File.Exists(g.Saves.SavePath),"daytime scenery and placement do not save");
                    g.Players.RestorePosition(g.Housing.Entrance.Position+new Vector3(0,.1f,-1.4f));g.EnterHome();Phase(4);
                }
                else if(p==4&&g.State==GameFlowState.Playing&&g.Housing.Inside)
                {
                    Require(!g.Terrain.Terrain.SeasonalDetails.gameObject.activeSelf,"interior hides all biome scenery with original cover");
                    g.Players.RestorePosition(g.Configuration.Housing.InteriorOrigin+new Vector3(-1.5f,.1f,2));g.RequestRest();Phase(5);
                }
                else if(p==5&&Object.FindObjectOfType<SleepView>()!=null){Object.FindObjectOfType<SleepView>().ConfirmButton.onClick.Invoke();Phase(6);}
                else if(p==6&&g.UI.HasDailyReport&&!g.Saves.SleepSavePending)
                {
                    Require(g.Saves.TryLoad(out var saved)&&saved.Day==2&&saved.PlayerArea==HousingState.Home,"only actual sleep writes a compatible checkpoint");
                    g.StartNewDayFromReport();g.ReturnToMainMenu();Phase(7);
                }
                else if(p==7&&Object.FindObjectOfType<PauseView>()!=null){Object.FindObjectOfType<PauseView>().ReturnMenuButton.onClick.Invoke();Phase(8);}
                else if(p==8&&g.State==GameFlowState.MainMenu)
                {
                    // Derive an old outdoor-position fixture from the real, fully validated sleep snapshot.
                    Require(g.Saves.TryLoad(out var saved),"load completed checkpoint fixture");saved.PlayerArea=HousingState.Island;saved.PlayerX=0;saved.PlayerY=.05f;saved.PlayerZ=-4.6f;
                    string json=JsonUtility.ToJson(saved,true);File.WriteAllText(g.Saves.SavePath,json);SessionState.SetString(Key+".Save",json);
                    g.Saves.RefreshAvailability();g.ContinueGame();Phase(9);
                }
                else if(p==9&&g.State==GameFlowState.Playing)
                {
                    Require(Vector2.Distance(new Vector2(g.Players.Player.transform.position.x,g.Players.Player.transform.position.z),new Vector2(0,-4.6f))<.05f,"continue restores saved outdoor position instead of beach start");
                    Require(File.ReadAllText(g.Saves.SavePath)==SessionState.GetString(Key+".Save",""),"load does not rewrite checkpoint");g.ReturnToMainMenu();Phase(10);
                }
                else if(p==10&&Object.FindObjectOfType<PauseView>()!=null){Object.FindObjectOfType<PauseView>().ReturnMenuButton.onClick.Invoke();Phase(11);}
                else if(p==11&&g.State==GameFlowState.MainMenu){g.EnterGame();Phase(12);}
                else if(p==12&&g.State==GameFlowState.Playing)
                {
                    BeachStart(g);Require(File.ReadAllText(g.Saves.SavePath)==SessionState.GetString(Key+".Save",""),"another new game preserves previous successful save");
                    Require(Object.FindObjectsOfType<IslandTerrain>().Length==1,"re-entry owns exactly one terrain");g.ReturnToMainMenu();Phase(13);
                }
                else if(p==13&&Object.FindObjectOfType<PauseView>()!=null){Object.FindObjectOfType<PauseView>().ReturnMenuButton.onClick.Invoke();Phase(14);}
                else if(p==14&&g.State==GameFlowState.MainMenu)
                {
                    Require(!g.Terrain.OwnsTerrain&&g.Terrain.Terrain==null&&Object.FindObjectsOfType<SeasonalGroundCover>(true).Length==0,"menu releases terrain, biome resources and cover subscriptions");
                    File.WriteAllText("Logs/IslandBiomes/Flow.txt","PASS: actual Packed AA; beach start and camera; connected grass/desert/snow; shared baked scenery; GPU grass-only seasonal color and fixed-clock image; map regions/Chinese label; actual construction hides/restores biome scenery; interior and sleep; saved outdoor position preserved; new game preserves old checkpoint; return/re-entry and cleanup.\n");
                    Debug.Log("Island biome flow verification PASSED.");Phase(15);EditorApplication.isPlaying=false;
                }
            }
            catch(Exception e){Debug.LogException(e);SessionState.SetBool(Key,false);EditorApplication.isPlaying=false;EditorApplication.Exit(1);}
        }
        static void BeachStart(GameManager g)
        {
            var terrain=g.Terrain.Terrain;var point=terrain.transform.InverseTransformPoint(g.Players.Player.transform.position);var spawn=terrain.Biomes.NewGameSpawn;
            Require(Vector2.Distance(new Vector2(point.x,point.z),new Vector2(spawn.x,spawn.z))<.08f&&terrain.Biomes.At(point.x,point.z)==IslandBiome.Beach,"new player starts at authored dry beach point");
            Require(g.Terrain.TryResolve(g.Players.Player.transform.position,out var ground)&&Mathf.Abs(ground.y-g.Players.Player.transform.position.y)<.2f,"beach is supported by actual collision");
            Require(Vector3.Distance(g.Players.Player.transform.position,g.Players.Player.ViewCamera.GetComponent<MagicIsland.CameraSystem.CameraController>().Target.position)<.01f,"camera follows beach player");
        }
        static void VerifyRuntime(GameManager g)
        {
            var terrain=g.Terrain.Terrain;var cover=terrain.SeasonalDetails;var patches=terrain.BiomeScenery.GetComponentsInChildren<SeasonalGroundPatch>(true);
            Require(patches.Count(p=>p.Surface.enabled&&p.Biome==IslandBiome.Desert)>=4&&patches.Count(p=>p.Surface.enabled&&p.Biome==IslandBiome.Snow)>=4,"meaningful visible cactus/rock and snowy pine/rock populations");
            Require(cover.Patches.Where(p=>p.Surface.enabled&&p.Biome==IslandBiome.Grassland).All(p=>{var local=terrain.transform.InverseTransformPoint(p.transform.position);return terrain.Biomes.Weights(local.x,local.z).x>.5f;}),"seasonal flowers stay in grassland");
            foreach(MagicIsland.CalendarSystem.Season s in Enum.GetValues(typeof(MagicIsland.CalendarSystem.Season)))
            {cover.Present(s,true);Require(patches.All(p=>p.Filter.sharedMesh==p.Seasons[0]),"desert and snow scenery persists in "+s);}
            cover.Present(g.Calendar.Date.Season,true);Connectivity(g);GroundPixels(g);
            LightingFlowVerification.Capture(g,"IslandBiomesOverview",new Vector3(0,0,1),25);
            LightingFlowVerification.Capture(g,"IslandBiomesDesert",new Vector3(-13,.6f,0),6.5f);
            LightingFlowVerification.Capture(g,"IslandBiomesSnow",new Vector3(2,.6f,13),6.5f);
            LightingFlowVerification.Capture(g,"IslandBiomesBeach",terrain.Biomes.NewGameSpawn,7);
        }
        static void Connectivity(GameManager g)
        {
            var c=g.Players.Player.GetComponent<CharacterController>();var visited=new HashSet<Vector2Int>();var queue=new Queue<Vector2Int>();var start=g.Terrain.Terrain.Biomes.NewGameSpawn;
            queue.Enqueue(new Vector2Int(Mathf.RoundToInt(start.x),Mathf.RoundToInt(start.z)));var reached=new HashSet<IslandBiome>();var buffer=new Collider[48];
            while(queue.Count>0)
            {
                var at=queue.Dequeue();if(!visited.Add(at))continue;
                if(!g.Terrain.TryResolve(new Vector3(at.x,0,at.y),out var point))continue;
                var center=point+c.center;float radius=c.radius+.025f,half=c.height*.5f-radius;
                int count=Physics.OverlapCapsuleNonAlloc(center-Vector3.up*half,center+Vector3.up*half,radius,buffer,~0,QueryTriggerInteraction.Ignore);
                if(count==buffer.Length||Enumerable.Range(0,count).Any(i=>!buffer[i].transform.IsChildOf(g.Players.Player.transform)&&buffer[i].bounds.max.y>point.y+.05f))continue;
                reached.Add(g.Terrain.Terrain.Biomes.At(at.x,at.y));
                foreach(var d in new[]{Vector2Int.up,Vector2Int.down,Vector2Int.left,Vector2Int.right})if(!visited.Contains(at+d))queue.Enqueue(at+d);
            }
            Require(reached.Contains(IslandBiome.Beach)&&reached.Contains(IslandBiome.Grassland)&&reached.Contains(IslandBiome.Desert)&&reached.Contains(IslandBiome.Snow),"actual full-player footprint can reach all regions from beach around facilities");
        }
        static void VerifyPlacement(GameManager g)
        {
            SeasonalGroundPatch target=null;
            foreach(var patch in g.Terrain.Terrain.BiomeScenery.GetComponentsInChildren<SeasonalGroundPatch>().Where(p=>p.Surface.enabled))
            {
                var p=g.Terrain.Terrain.transform.InverseTransformPoint(patch.transform.position);g.Players.RestorePosition(patch.transform.position+new Vector3(0,.1f,-1.2f));
                g.Placements.SetCandidate(Mathf.RoundToInt(p.x),Mathf.RoundToInt(p.z));if(g.Placements.CheckCandidate()==PlacementResult.Success){target=patch;break;}
            }
            Require(target!=null&&g.Placements.Confirm()&&!target.Surface.enabled,"successful actual construction hides new biome decoration");
            Require(g.Placements.Recover(g.Placements.Objects.Values.Single())&&target.Surface.enabled,"recovery restores same baked decoration without deleting player state");g.Placements.Cancel();
        }
        static void GroundPixels(GameManager g)
        {
            var renderer=g.Terrain.Terrain.Ground.GetComponent<MeshRenderer>();int layer=renderer.gameObject.layer;var old=Shader.GetGlobalVector("_IslandSeasonGrass");
            var go=new GameObject("Biome ground inspection",typeof(Camera));var camera=go.GetComponent<Camera>();camera.enabled=false;camera.orthographic=true;camera.orthographicSize=22;
            camera.transform.SetPositionAndRotation(new Vector3(0,40,0),Quaternion.Euler(90,0,0));camera.cullingMask=1<<31;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.aspect=960f/540;
            try
            {
                renderer.gameObject.layer=31;Shader.SetGlobalVector("_IslandSeasonGrass",new Vector4(.3f,.8f,.25f,1));var a=Pixels(camera,"GroundGreen");
                Shader.SetGlobalVector("_IslandSeasonGrass",new Vector4(.9f,.3f,.1f,1));var b=Pixels(camera,"GroundWarm");
                Func<Vector3,int> index=p=>{var screen=camera.WorldToViewportPoint(p);return Mathf.RoundToInt(screen.y*540)*960+Mathf.RoundToInt(screen.x*960);};
                Func<int,int> delta=i=>Math.Abs(a[i].r-b[i].r)+Math.Abs(a[i].g-b[i].g)+Math.Abs(a[i].b-b[i].b);
                Require(delta(index(Vector3.zero))>15&&delta(index(new Vector3(-13,0,0)))<3&&delta(index(new Vector3(2,0,14)))<3,"GPU seasonal recoloring affects grass while desert and permanent snow retain their palette");
                // Independent GPU readbacks may round a few channels by one 8-bit level.
                var paused=Pixels(camera,"GroundPaused");
                Require(b.Zip(paused,(x,y)=>Math.Max(Math.Abs(x.r-y.r),Math.Max(Math.Abs(x.g-y.g),Math.Abs(x.b-y.b)))).Max()<=1,"paused surface is unchanged within one-level GPU quantization");
            }
            finally{renderer.gameObject.layer=layer;Shader.SetGlobalVector("_IslandSeasonGrass",old);Object.DestroyImmediate(go);}
        }
        static Color32[] Pixels(Camera camera,string name)
        {
            var rt=new RenderTexture(960,540,24);var texture=new Texture2D(960,540,TextureFormat.RGB24,false);var old=RenderTexture.active;
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,960,540),0,0);texture.Apply();File.WriteAllBytes("Logs/IslandBiomes/"+name+".png",texture.EncodeToPNG());return texture.GetPixels32();}
            finally{camera.targetTexture=null;RenderTexture.active=old;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(texture);}
        }
    }
}
