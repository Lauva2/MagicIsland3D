using System;
using System.IO;
using System.Linq;
using System.Reflection;
using MagicIsland.CameraSystem;
using MagicIsland.Farming;
using MagicIsland.FishingSystem;
using MagicIsland.Interaction;
using MagicIsland.Managers;
using MagicIsland.Ocean;
using MagicIsland.Player;
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
    [InitializeOnLoad]public static class TerrainCoastFlowVerification
    {
        const string Key="MagicIsland.TerrainCoastVerification";
        static TerrainManager fixture;
        static byte[] saveBefore;
        static int stamina;
        static long fish;
        static float wave;
        static int splashes;
        static int effectEmissions;
        static TerrainCoastFlowVerification(){if(SessionState.GetBool(Key,false))EditorApplication.update+=Tick;}
        public static void Run()
        {
            PrototypeAddressablesSetup.EnsureSetup();
            var paths=new[]{TerrainSetup.PrefabPath,TerrainSetup.MeshFolder+"/Island Land.asset",TerrainSetup.MeshFolder+"/Sea Boundary.asset","Assets/Art/Prefabs/Fishing/CoastalFishingSpot.prefab",OceanSetup.PrefabPath,OceanSetup.MeshFolder+"/Island Shore.asset","Assets/Config/FishingCatalog.asset","Assets/Config/GameConfig.asset"}
                .Concat(Directory.GetFiles(CoastArtSetup.MeshFolder,"*.asset")).Concat(Directory.GetFiles(CoastArtSetup.MaterialFolder,"*.mat")).Concat(Directory.GetFiles(CoastArtSetup.PrefabFolder,"*.prefab")).ToArray();
            var bytes=paths.Select(File.ReadAllBytes).ToArray();PrototypeAddressablesSetup.EnsureSetup();for(int i=0;i<paths.Length;i++)Require(bytes[i].SequenceEqual(File.ReadAllBytes(paths[i])),"Setup preserves terrain/pier artist edits");
            EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");PrototypeAddressablesSetup.BuildContent();var settings=AddressableAssetSettingsDefaultObject.Settings;
            settings.ActivePlayModeDataBuilderIndex=settings.DataBuilders.FindIndex(b=>b is BuildScriptPackedPlayMode);
            SessionState.SetBool(Key,true);Phase(0);SessionState.SetFloat(Key+".Start",(float)EditorApplication.timeSinceStartup);EditorApplication.update-=Tick;EditorApplication.update+=Tick;EditorApplication.isPlaying=true;
        }
        static void Require(bool yes,string why){if(!yes)throw new Exception(why);}
        static void Phase(int n){SessionState.SetInt(Key+".Phase",n);Debug.Log("Terrain coast phase "+n);}
        static void Move(GameManager g,Vector3 position){g.Players.RestorePosition(position);g.Players.Player.ViewCamera.GetComponent<CameraController>().SnapToTarget();Physics.SyncTransforms();}
        static void Walk(GameManager g,Vector2 input,int frames)
        {for(int i=0;i<frames;i++)typeof(PlayerMovement).GetMethod("TickMovement",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(g.Players.Player,new object[]{input,.04f});}
        static long Fish(GameManager g)=>g.Inventory.GetCount("sardine")+g.Inventory.GetCount("sea_bass");
        static void NoSave(GameManager g)
        {Require(saveBefore==null?!File.Exists(g.Saves.SavePath):File.ReadAllBytes(g.Saves.SavePath).SequenceEqual(saveBefore),"Coast actions never write progress outside sleep");}
        static void Capture(GameManager g,string name,bool ui=true)
        {
            var view=g.Players.Player.ViewCamera;view.GetComponent<CameraController>().SnapToTarget();g.UI.RefreshViews();
            var canvases=Object.FindObjectsOfType<Canvas>();var enabled=canvases.Select(c=>c.enabled).ToArray();
            try{if(!ui)foreach(var c in canvases)c.enabled=false;GameUiPreview.CaptureInventory(view);File.Copy("Logs/InventoryPreview.png","Logs/"+name+".png",true);}
            finally{for(int i=0;i<canvases.Length;i++)canvases[i].enabled=enabled[i];}
        }
        static void Setup(GameManager g)
        {
            Debug.Log("Coast setup: ownership");
            Require(g.Terrain.OwnsTerrain&&g.Terrain.Terrain.IsValid&&g.Fishing.Spot.Deck!=null&&g.Fishing.Spot.ReachPoint!=null,"AA terrain/pier bound");
            var world=Object.FindObjectOfType<MagicIsland.Prototype.FarmPrototype>();Require(world.transform.Find("Island ground")==null&&world.transform.Find("East boundary")==null,"Legacy square ground/walls replaced");
            VerifyCoastArt(g);
            CoastalPalmVerification.VerifyRuntime(g);
            Debug.Log("Coast setup: legacy landing");
            foreach(var p in new[]{new Vector3(11.3f,8,11.3f),new Vector3(-11.3f,3,-11.3f),new Vector3(0,2,-4.6f)})
            {Require(g.Housing.ApplyArea(HousingState.Island,p),"Legacy island position resolves safely");Require(Mathf.Abs(g.Players.Player.transform.position.y-.05f)<.025f,"Restored foot is on terrain instead of air");}
            Debug.Log("Coast setup: beach movement");Move(g,new Vector3(-10,.1f,0));Walk(g,Vector2.left,80);var beach=g.Players.Player.transform.position;
            Require(beach.x<-14&&beach.y>g.Ocean.Surface.SeaLevel+.05f&&g.Terrain.Contains(beach,.28f),"WASD crosses former wall, stands on beach, stops before sea");Capture(g,"WalkableBeachPreview",false);
            Debug.Log("Coast setup: dock movement");Move(g,new Vector3(12.2f,.1f,(float)IslandLayout.PierZ));Walk(g,Vector2.right,70);
            var deck=g.Players.Player.transform.position;Require(deck.x>16.5f&&deck.x<18.3f&&deck.y>.06f,"WASD enters shore gate, climbs dock and stops at its end");
            Walk(g,Vector2.up,40);Require(Mathf.Abs(g.Players.Player.transform.position.z-(float)IslandLayout.PierZ)<1.1f,"Dock side barrier blocks sea exit");
            Debug.Log("Coast setup: NPC progress");var defs=g.NPCSchedules.State.Definitions;var legacy=defs.Select(d=>new NPCScheduleDefinition(d.Id,d.Speed,d.CoastMinute,d.ReturnMinute,new[]{d.Points[0],d.Points[1],new SchedulePoint(d.Points[2].X,-6.8),new SchedulePoint(9.15,-6.8)})).ToArray();
            g.NPCSchedules.State.Restore(new NPCScheduleState(legacy,780).Capture());g.NPCSchedules.Tick(780,2,false);
            Require(g.NPCSchedules.Bound&&defs.All(d=>g.Terrain.Contains(g.NPCs.Actors[d.Id].transform.position,.3f)),"Old NPC route progress restores and reaches safe coast");
            Require(g.Placements.State.CheckLocation(g.Configuration.Crafting.Recipes.First(r=>r.IsPlaceable&&!r.IsFurniture).OutputItemId,13,-8,0)==PlacementResult.RouteReserved,"Dock approach reserved from construction");
            Move(g,g.Fishing.Spot.Position+new Vector3(-.4f,.05f,0));g.Tools.SelectTool(FarmTool.FishingRod);
            var ray=new Ray(g.Fishing.Spot.Deck.bounds.center+Vector3.up*6,Vector3.down);var target=g.Interactions.RefreshPointer(ray,false);
            Require(target.Kind==InteractionKind.Fishing&&target.InRange&&target.Ready,"Unified interaction targets real deck");Capture(g,"CoastPierPreview",false);
            stamina=g.Stamina.State.Current;fish=Fish(g);splashes=g.Ocean.Surface.SplashCount;effectEmissions=g.Ocean.Surface.Effects.EmissionCount;Require(g.Interactions.TryInteract(),"Mouse dispatch opens fishing");NoSave(g);
        }
        static void VerifyCoastArt(GameManager g)
        {
            var terrain=g.Terrain.Terrain;var ocean=g.Ocean.Surface;
            Require(terrain.CoastArtVersion==CoastArtSetup.Version&&ocean.CoastArtVersion==CoastArtSetup.Version&&g.Fishing.Spot.CoastArtVersion==CoastArtSetup.Version,"AA terrain/ocean/pier all contain coast art");
            var palms=terrain.GetComponentsInChildren<CoastalSceneryPatch>();Require(palms.Length>=8,"Palm scenery loaded with terrain dependencies");
            Require(terrain.CoastalScenery.GetComponentsInChildren<MeshFilter>().Length>40&&ocean.GetComponentsInChildren<OceanHabitatPatch>().Sum(p=>p.Population)>=1000,"Beach and merged submerged habitat meshes loaded");
            Require(ocean.transform.Find("Shallow reef gardens").GetComponentsInChildren<Renderer>().All(r=>r.bounds.max.y<=ocean.Water.transform.position.y-.07f),"Reef plants and stones stay below wave troughs");
            foreach(var filter in terrain.GetComponentsInChildren<MeshFilter>().Concat(ocean.GetComponentsInChildren<MeshFilter>()))
            {
                Require(filter.sharedMesh!=null&&filter.sharedMesh.vertices.All(v=>!float.IsNaN(v.x)&&!float.IsNaN(v.y)&&!float.IsNaN(v.z)&&!float.IsInfinity(v.x)&&!float.IsInfinity(v.y)&&!float.IsInfinity(v.z)),"All baked scenery vertices are finite");
                var bounds=filter.sharedMesh.bounds;Require(bounds.size.sqrMagnitude>.001f&&!float.IsNaN(bounds.center.x)&&!float.IsInfinity(bounds.size.x),"Baked mesh culling bounds are usable");
            }
            Require(palms.All(p=>p.Obstacle!=null&&p.Obstacle.enabled&&!p.Obstacle.isTrigger&&p.Obstacle.gameObject.layer==2),"Solid palm trunks ignore pointer rays");
            var palm=palms[0];var p=palm.transform.position;
            var trunk=palm.Obstacle.bounds.center;trunk.y=p.y;
            Move(g,trunk+new Vector3(-1.3f,.08f,0));Walk(g,Vector2.right,18);
            Require(g.Players.Player.transform.position.x<trunk.x-.3f,"Actual movement aimed at the collider center stops at palm trunk");
            Move(g,p+new Vector3(0,.08f,2.4f));g.Cameras.SetOcclusionEnabled(true);
            typeof(CameraManager).GetMethod("UpdateOcclusion",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(g.Cameras,new object[]{1f});
            var occluder=palm.GetComponent<CameraOccluder>();Require(occluder.Opacity<.3f&&occluder.RuntimeMaterialCount>0&&palm.Obstacle.enabled,"Palm fades without disabling solid trunk");Capture(g,"CoastPalmFadePreview",false);
            var crown=occluder.Renderers[0].bounds;foreach(var renderer in occluder.Renderers)crown.Encapsulate(renderer.bounds);
            float clearX=p.x>0?crown.min.x-.8f:crown.max.x+.8f;
            Move(g,new Vector3(clearX,p.y+.08f,p.z+1.6f));typeof(CameraManager).GetMethod("UpdateOcclusion",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(g.Cameras,new object[]{1f});
            Require(occluder.Opacity==1&&occluder.RuntimeMaterialCount==0,"Palm restores original AA materials after moving aside");
            Require(!g.Interactions.RefreshPointer(new Ray(p+Vector3.up*6,Vector3.down),false).Visible,"Palm decoration cannot intercept a mouse interaction");
            var saved=new[]{new SavedPlacedObject{Id=1,ItemId="fixture",GridX=Mathf.RoundToInt(p.x),GridZ=Mathf.RoundToInt(p.z)}};
            g.Terrain.RespectSavedPlacement(saved);Require(!palm.gameObject.activeSelf&&!palm.Obstacle.gameObject.activeInHierarchy,"Saved construction takes priority over new palm scenery");palm.gameObject.SetActive(true);Physics.SyncTransforms();
            Move(g,new Vector3(13.2f,.1f,-10.5f));float size=g.Players.Player.ViewCamera.orthographicSize;g.Players.Player.ViewCamera.orthographicSize=9;
            typeof(CameraManager).GetMethod("UpdateOcclusion",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(g.Cameras,new object[]{1f});
            Capture(g,"CoastArtOverview",false);g.Players.Player.ViewCamera.orthographicSize=size;
            NoSave(g);
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;int p=SessionState.GetInt(Key+".Phase",0);
            if(p==15&&!EditorApplication.isPlaying){SessionState.SetBool(Key,false);EditorApplication.Exit(0);return;}
            try
            {
                if(EditorApplication.timeSinceStartup-SessionState.GetFloat(Key+".Start",0)>240)throw new Exception("Coast timeout "+p);
                if(!EditorApplication.isPlaying)return;var g=GameManager.Instance;if(g==null)return;Require(g.State!=GameFlowState.Failed,"Coast startup");
                if(p==1&&g.State==GameFlowState.MainMenu)throw new Exception("World initialization returned to menu; inspect the preceding manager error");
                if(p==0&&g.State==GameFlowState.MainMenu){saveBefore=File.Exists(g.Saves.SavePath)?File.ReadAllBytes(g.Saves.SavePath):null;g.EnterGame();Phase(1);}
                else if(p==1&&g.State==GameFlowState.Playing){Setup(g);Phase(2);}
                else if(p==2&&Object.FindObjectOfType<FishingView>()!=null&&g.Fishing.State.Active)
                {
                    Require(Object.FindObjectOfType<FishingView>().CoastLayout&&Object.FindObjectOfType<FishingView>().StatusText.transform.parent.GetComponent<RectTransform>().anchoredPosition==new Vector2(20,-350),"Coast fishing panel keeps water and float visible");Require(g.Fishing.Spot.Bobber.gameObject.activeSelf&&!g.Players.Player.enabled,"Fishing UI freezes movement and displays float");
                    Require(g.Ocean.Surface.SplashCount==splashes+1,"Successful bobber cast notifies ocean wildlife exactly once");
                    Require(g.Ocean.Surface.Effects.EmissionCount>=effectEmissions+10&&g.Ocean.Surface.Effects.Particles.Length==84,"Real bobber cast creates water effects from the AA pool");
                    int emitted=g.Ocean.Surface.Effects.EmissionCount;string feedback=g.Fishing.Feedback;Require(!g.Fishing.Cast()&&g.Ocean.Surface.SplashCount==splashes+1&&g.Ocean.Surface.Effects.EmissionCount==emitted,"Rejected repeated cast cannot create another wildlife disturbance or splash effect");g.Fishing.SetFeedback(feedback);
                    wave=g.Ocean.Surface.AnimationTime;Phase(3);
                }
                else if(p==3)
                {
                    Require(g.Ocean.Surface.AnimationTime>wave,"Ocean continues flowing during timed fishing session");
                    var point=g.Fishing.Spot.Bobber.position;Require(Mathf.Abs(point.y-g.Ocean.Surface.HeightAt(point)-.055f)<.005f,"Float follows shader wave height");
                    var line=g.Fishing.Spot.GetComponentInChildren<LineRenderer>();Require(line.enabled&&(line.GetPosition(2)-point).sqrMagnitude<.00001f&&(line.GetPosition(0)-g.Fishing.Rod.Tip.position).sqrMagnitude<.00001f,"Fish line joins rod and float");
                    Capture(g,"CoastFishingPreview");if(g.Fishing.State.Phase==FishingPhase.Waiting)g.Fishing.Tick((float)g.Fishing.State.Remaining+.001f);Require(g.Fishing.State.Phase==FishingPhase.Bite,"Advance fixture to bite once");
                    point=g.Fishing.Spot.Bobber.position;Require(Mathf.Abs(point.y-g.Ocean.Surface.HeightAt(point)+.045f)<.005f,"Bite submerges float relative to actual water");
                    g.ReelFishing();Require(Fish(g)==fish+1&&g.Stamina.State.Current==stamina-g.Configuration.Fishing.StaminaCost,"Catch settles exactly one fish and one stamina cost");g.ReelFishing();Require(Fish(g)==fish+1,"Repeat reel cannot settle twice");NoSave(g);g.CloseFishing();
                    g.OpenFishing();Phase(4);
                }
                else if(p==4&&Object.FindObjectOfType<FishingView>()!=null&&g.Fishing.State.Active)
                {g.CloseFishing();Require(!g.Fishing.Spot.Bobber.gameObject.activeSelf&&!g.Fishing.Spot.GetComponentInChildren<LineRenderer>().enabled,"Cancel clears float and line");NoSave(g);
                    Move(g,g.Housing.Entrance.Position+new Vector3(0,.1f,-1.4f));g.EnterHome();Phase(5);}
                else if(p==5&&g.State==GameFlowState.Playing&&g.Housing.Inside)
                {Require((g.Players.Player.ViewCamera.cullingMask&(1<<g.Terrain.Terrain.gameObject.layer))==0,"Indoor camera excludes island terrain");Move(g,g.Housing.Interior.Exit.Position+new Vector3(0,.1f,1.2f));g.ExitHome();Phase(6);}
                else if(p==6&&g.State==GameFlowState.Playing&&!g.Housing.Inside){Require(g.Terrain.Terrain!=null,"Scene transition preserves terrain");g.ReturnToMainMenu();Phase(7);}
                else if(p==7&&Object.FindObjectOfType<PauseView>()!=null){Object.FindObjectOfType<PauseView>().ReturnMenuButton.onClick.Invoke();Phase(8);}
                else if(p==8&&g.State==GameFlowState.MainMenu)
                {NoSave(g);Require(!g.Terrain.OwnsTerrain&&g.Terrain.Terrain==null&&Object.FindObjectsOfType<IslandTerrain>().Length==0,"Menu releases terrain and collider instance");fixture=new GameObject("Terrain load fixture").AddComponent<TerrainManager>();fixture.StartCoroutine(fixture.Load("Terrain_Verification_Missing",fixture.transform));Phase(9);}
                else if(p==9&&fixture.LastError!=null)
                {Require(!fixture.OwnsTerrain,"Failed load releases AA handle");fixture.StartCoroutine(fixture.Load(g.Configuration.Config.TerrainPrefabAddress,fixture.transform));fixture.Clear();Phase(10);}
                else if(p==10)Phase(11);
                else if(p==11)
                {Require(!fixture.OwnsTerrain&&fixture.Terrain==null&&fixture.GetComponentsInChildren<IslandTerrain>().Length==0,"Cancelled load releases late collision mesh");Object.Destroy(fixture.gameObject);g.EnterGame();Phase(12);}
                else if(p==12&&g.State==GameFlowState.Playing)
                {Require(Object.FindObjectsOfType<IslandTerrain>().Length==1&&Object.FindObjectsOfType<FishingSpot>().Length==1,"Re-entry owns one fresh terrain and pier");NoSave(g);Phase(13);}
                else if(p==13)
                {File.WriteAllText("Logs/TerrainCoastVerification.txt","PASS: packed AA scenery dependencies, artist preservation, finite meshes, palm collision/pointer filtering/camera fade/material cleanup, saved-construction priority, real WASD beach/dock movement and water barriers, old island positions/NPC routes, pier reservation, mouse fishing, matching shader wave/float/line, bite/catch/cancel, no daytime save, home transition, unload/failure/cancel and re-entry.\n");Debug.Log("Terrain coast flow verification PASSED.");Phase(15);EditorApplication.isPlaying=false;}
            }
            catch(Exception e){Debug.LogException(e);SessionState.SetBool(Key,false);EditorApplication.isPlaying=false;EditorApplication.Exit(1);}
        }
    }
}
