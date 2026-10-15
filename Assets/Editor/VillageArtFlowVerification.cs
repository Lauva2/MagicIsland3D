using System;
using System.IO;
using System.Linq;
using System.Reflection;
using MagicIsland.CameraSystem;
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
    [InitializeOnLoad] public static class VillageArtFlowVerification
    {
        const string Key="MagicIsland.VillageArtVerification";
        static int chair;
        static VillageArtFlowVerification(){if(SessionState.GetBool(Key,false))EditorApplication.update+=Tick;}
        public static void Run()
        {
            PrototypeAddressablesSetup.EnsureSetup();
            var paths=new[]{"Assets/Art/Prefabs/Buildings/PlayerHouse.prefab","Assets/Art/Prefabs/Buildings/PlayerHouseExpansion.prefab","Assets/Art/Prefabs/Buildings/OrderHall.prefab",TerrainSetup.PrefabPath,VillageArtSetup.PathsPath,"Assets/Config/HousingConfig.asset","Assets/Config/GameConfig.asset"}
                .Concat(Directory.GetFiles(VillageArtSetup.MeshFolder,"*.asset")).Concat(Directory.GetFiles(VillageArtSetup.MaterialFolder,"*.mat")).ToArray();
            var bytes=paths.Select(File.ReadAllBytes).ToArray();PrototypeAddressablesSetup.EnsureSetup();
            for(int i=0;i<paths.Length;i++)Require(bytes[i].SequenceEqual(File.ReadAllBytes(paths[i])),"Setup preserves building art, path layout and configuration");
            EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");PrototypeAddressablesSetup.BuildContent();var settings=AddressableAssetSettingsDefaultObject.Settings;
            settings.ActivePlayModeDataBuilderIndex=settings.DataBuilders.FindIndex(b=>b is BuildScriptPackedPlayMode);
            SessionState.SetBool(Key,true);Phase(0);SessionState.SetFloat(Key+".Start",(float)EditorApplication.timeSinceStartup);EditorApplication.update-=Tick;EditorApplication.update+=Tick;EditorApplication.isPlaying=true;
        }
        static void Require(bool ok,string why){if(!ok)throw new Exception(why);}
        static void Phase(int p){SessionState.SetInt(Key+".Phase",p);Debug.Log("Village art phase "+p);}
        static T View<T>() where T:Component=>Object.FindObjectOfType<T>();
        static void Move(GameManager g,Vector3 p){g.Players.RestorePosition(p);g.Players.Player.ViewCamera.GetComponent<CameraController>().SnapToTarget();Physics.SyncTransforms();}
        static void SaveDirectory(GameManager g,string dir)=>g.Saves.Initialize(g.Configuration.Config,g.Configuration.Items,g.Configuration.Crops,dir,g.Configuration.Tasks,g.Configuration.Orders,g.Configuration.Seasons,g.Configuration.Gathering,g.Configuration.ToolUpgrades,g.Configuration.Storage,g.Configuration.NPCs,g.Configuration.Relationships,g.Configuration.Stories,g.Configuration.NPCSchedules,g.Configuration.Fishing,g.Configuration.Crafting,g.Configuration.Animals);
        static void NoSave(GameManager g)=>Require(!File.Exists(g.Saves.SavePath),"Building/board/placement actions cannot save before sleeping");
        static void SameSave(GameManager g)=>Require(File.ReadAllText(g.Saves.SavePath)==SessionState.GetString(Key+".Saved",""),"Daytime transitions and menu cannot replace sleep checkpoint");
        static void Capture(GameManager g,string name,float size,Vector3? focus=null)
        {
            var camera=g.Players.Player.ViewCamera;float oldSize=camera.orthographicSize;camera.orthographicSize=size;camera.GetComponent<CameraController>().SnapToTarget();g.UI.RefreshViews();
            if(focus.HasValue)camera.transform.position+=focus.Value-(g.Players.Player.transform.position+Vector3.up*.6f);
            foreach(var text in Object.FindObjectsOfType<TMPro.TMP_Text>())text.ForceMeshUpdate(true);
            var canvases=Object.FindObjectsOfType<Canvas>();var enabled=canvases.Select(c=>c.enabled).ToArray();g.Cameras.SetOcclusionEnabled(false);
            try{foreach(var c in canvases)c.enabled=false;GameUiPreview.CaptureInventory(camera);File.Copy("Logs/InventoryPreview.png","Logs/"+name+".png",true);}
            finally{for(int i=0;i<canvases.Length;i++)canvases[i].enabled=enabled[i];camera.orthographicSize=oldSize;g.Cameras.SetOcclusionEnabled(true);}
        }
        static InteractionTarget Point(GameManager g,Component actor,InteractionKind kind)
        {
            foreach(var c in actor.GetComponentsInChildren<Collider>())
            {var target=g.Interactions.RefreshPointer(new Ray(c.bounds.center+Vector3.up*8,Vector3.down),false);if(target.Kind==kind)return target;}
            throw new Exception("Cannot target styled "+kind);
        }
        static void Walk(GameManager g,Vector2 direction,int frames)
        {for(int i=0;i<frames;i++)typeof(MagicIsland.Player.PlayerMovement).GetMethod("TickMovement",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(g.Players.Player,new object[]{direction,.04f});}
        static void CheckWorld(GameManager g)
        {
            var home=g.Housing.Entrance;var box=home.GetComponent<BoxCollider>();var board=g.Orders.Board;
            Require(home.GetComponent<VillageBuildingVisual>().ArtVersion==VillageArtSetup.Version&&g.Orders.Hall.GetComponent<VillageBuildingVisual>().ArtVersion==VillageArtSetup.Version,"Packed AA house and hall art owned by existing managers");
            Require(box.center==new Vector3(0,1.15f,0)&&box.size==new Vector3(1.3f,2.3f,2)&&home.GetComponentsInChildren<Collider>().Length==1,"Original house physical footprint retained");
            Require(home.ReachPoint.localPosition==new Vector3(0,0,-1.08f)&&home.ArrivalPoint.localPosition==new Vector3(0,.1f,-2.08f),"Original doorway reach/arrival retained");
            Require(board.transform.localPosition==new Vector3(1.28f,0,-2.40f)&&g.Orders.Hall.GetComponentsInChildren<OrderBoard>().Length==1,"Noticeboard remains in front of same building");
            Require(g.Orders.Hall.transform.Find("Village details").GetComponentsInChildren<Collider>().Length==0&&home.transform.Find("Village details").GetComponentsInChildren<Collider>().Length==0,"New visual detail adds no invisible interaction blockers");
            var fade=g.Orders.Hall.GetComponent<CameraOccluder>();Require(fade.Renderers.Length>25&&fade.Renderers.All(r=>r.GetComponentInParent<OrderBoard>()==null),"Hall fade includes new detail and excludes notices");
            var paths=g.Terrain.Terrain.Paths;Require(paths!=null&&paths.IsValid&&paths.GetComponentsInChildren<Transform>().All(t=>t.gameObject.layer==2),"AA nested paths are passable and ignore pointer");
            for(int farm=1;farm<=12;farm++)
            {
                paths.Configure(farm,g.Configuration.Housing);float half=farm*.6f+.5f;
                foreach(var segment in paths.Segments.Where(s=>s.gameObject.activeSelf))foreach(var v in segment.GetComponent<MeshFilter>().sharedMesh.vertices)
                {
                    var p=segment.TransformPoint(v);Require(IslandLayout.ContainsLand(p.x,p.z,.3),"Every configurable path stays on supported land");
                    Require(!(Mathf.Abs(p.x)<half&&Mathf.Abs(p.z)<half),"Every farm size keeps roads outside farm reservation");
                }
            }
            paths.Configure(g.Configuration.Config.FarmSize,g.Configuration.Housing);
            Move(g,new Vector3(0,.1f,-7.2f));Walk(g,Vector2.right,12);Require(g.Players.Player.transform.position.x>.8f&&g.Players.Player.transform.position.y<.2f,"Actual movement crosses sand path without collision ridge");
            Require(!g.Interactions.RefreshPointer(new Ray(paths.Segments[1].position+Vector3.up*8,Vector3.down),false).Visible,"Road mesh cannot intercept click target");
            Require(g.NPCSchedules.Bound,"Existing NPC route passes styled buildings and paths");
            Move(g,new Vector3(5.8f,.1f,-2.6f));Capture(g,"VillageOverview",11);
            Move(g,home.Position+new Vector3(0,.1f,-1.4f));Capture(g,"VillageCottagePreview",3.8f,home.transform.position+Vector3.up);
            Move(g,board.transform.position+new Vector3(-.8f,.1f,-.55f));Capture(g,"VillageHallPreview",4.8f,g.Orders.Hall.transform.position+new Vector3(0,.8f,-.65f));
            Move(g,board.transform.position+new Vector3(2.4f,.1f,-.5f));Walk(g,Vector2.left,Mathf.CeilToInt(3.0f/(g.Players.Player.Speed*.04f)));
            Require(g.Players.Player.transform.position.x<board.transform.position.x-.1f&&g.Orders.InRange(g.Players.Player.transform.position,g.Configuration.Config.InteractionReach),"Actual road approach passes NPC and reaches board without entering its collision");
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;int p=SessionState.GetInt(Key+".Phase",0);
            if(p==15&&!EditorApplication.isPlaying){SessionState.SetBool(Key,false);EditorApplication.Exit(0);return;}
            try
            {
                if(EditorApplication.timeSinceStartup-SessionState.GetFloat(Key+".Start",0)>240)throw new Exception("Village timeout "+p);
                if(!EditorApplication.isPlaying)return;var g=GameManager.Instance;if(g==null)return;Require(g.State!=GameFlowState.Failed,"Village startup");
                if(p==1&&g.State==GameFlowState.MainMenu)throw new Exception("Village initialization returned to menu; inspect preceding manager error");
                if(p==0&&g.State==GameFlowState.MainMenu)
                {string dir=Path.GetFullPath("Logs/VillageSaves/"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);SaveDirectory(g,dir);g.EnterGame();Phase(1);}
                else if(p==1&&g.State==GameFlowState.Playing)
                {
                    CheckWorld(g);g.Economy.Restore(1000);Require(g.Inventory.TryAdd(g.Orders.State.ItemId,g.Orders.State.Quantity),"Prepare order fixture");
                    var target=Point(g,g.Orders.Board,InteractionKind.Orders);Require(target.InRange&&g.Interactions.TryInteract(),"Real mouse click on styled board opens order panel");NoSave(g);Phase(2);
                }
                else if(p==2&&View<OrderView>()!=null)
                {
                    long before=g.Economy.Gold,reward=g.Orders.State.Reward;View<OrderView>().DeliverButton.onClick.Invoke();Require(g.Orders.State.Fulfilled&&g.Economy.Gold==before+reward,"Styled board preserves order settlement");
                    View<OrderView>().DeliverButton.onClick.Invoke();Require(g.Economy.Gold==before+reward,"Repeated order delivery grants nothing twice");NoSave(g);
                    Require(g.Inventory.TryAdd("wood",g.Configuration.Housing.UpgradeWood)&&g.Inventory.TryAdd("stone",g.Configuration.Housing.UpgradeStone)&&g.Inventory.TryAdd("wood_chair",1),"Prepare upgrade and furniture fixtures");
                    View<OrderView>().HouseUpgradeButton.onClick.Invoke();Phase(3);
                }
                else if(p==3&&View<HouseUpgradeView>()!=null)
                {
                    View<HouseUpgradeView>().ApplyButton.onClick.Invoke();Require(g.Housing.Upgrade.Building&&g.Housing.Upgrade.Level==0,"Upgrade request does not complete exterior early");NoSave(g);g.CloseHouseUpgrade();
                    Move(g,g.Housing.Entrance.Position+new Vector3(0,.1f,-1.4f));Require(Point(g,g.Housing.Entrance,InteractionKind.Home).InRange&&g.Interactions.TryInteract(),"Styled doorway mouse interaction enters home");Phase(4);
                }
                else if(p==4&&g.State==GameFlowState.Playing&&g.Housing.Inside)
                {
                    Require(g.Housing.Interior.OriginalWalls.All(w=>w.activeSelf)&&(g.Players.Player.ViewCamera.cullingMask&(1<<g.Terrain.Terrain.Paths.gameObject.layer))==0,"Original room retained and outdoor paths excluded indoors");
                    Move(g,g.Configuration.Housing.InteriorOrigin+new Vector3(2,.1f,.3f));g.ToggleInventory();int slot=Enumerable.Range(0,g.Inventory.State.Capacity).First(i=>g.Inventory.State.Slots[i].ItemId=="wood_chair");g.BeginPlacementFromBag(slot);Phase(5);
                }
                else if(p==5&&g.Furniture.Ready&&View<PlacementView>()!=null)
                {
                    g.Furniture.SetCandidate(2,2);g.ConfirmPlacement();Require(g.Furniture.State.Count==1,"Furniture fixture placed before upgrade");chair=g.Furniture.Objects.Values.Single().GetInstanceID();NoSave(g);
                    Move(g,g.Configuration.Housing.InteriorOrigin+new Vector3(-1.5f,.1f,2));g.RequestRest();Phase(6);
                }
                else if(p==6&&View<SleepView>()!=null)
                {
                    View<SleepView>().ConfirmButton.onClick.Invoke();Require(g.Housing.Upgrade.Level==1&&g.Farm.State.Day==2&&g.Saves.TryLoad(out var saved)&&saved.HouseUpgrade.Level==1&&saved.Furniture.Length==1,"Actual sleep completes extension and saves whole-world checkpoint");
                    Require(g.Housing.Interior.OriginalWalls.All(w=>!w.activeSelf)&&g.Furniture.Objects.Values.Single().GetInstanceID()==chair,"In-place expansion preserves furniture instance");
                    SessionState.SetString(Key+".Saved",File.ReadAllText(g.Saves.SavePath));g.StartNewDayFromReport();Move(g,g.Configuration.Housing.InteriorOrigin+new Vector3(0,.1f,-3));g.ExitHome();Phase(7);
                }
                else if(p==7&&g.State==GameFlowState.Playing&&!g.Housing.Inside)
                {
                    Require(g.Housing.Entrance.GetComponentsInChildren<VillageBuildingVisual>().Length==2&&g.Housing.Entrance.NameText.text=="舒适小屋","AA completed exterior displays upgraded art once");
                    Require(g.Housing.Entrance.GetComponentsInChildren<Collider>().Length==1,"Upgrade detail retains original collision footprint");
                    Move(g,g.Housing.Entrance.Position+new Vector3(0,.1f,-1.4f));Capture(g,"VillageUpgradedCottagePreview",3.8f,g.Housing.Entrance.transform.position+Vector3.up);SameSave(g);g.ReturnToMainMenu();Phase(8);
                }
                else if(p==8&&View<PauseView>()!=null){View<PauseView>().ReturnMenuButton.onClick.Invoke();Phase(9);}
                else if(p==9&&g.State==GameFlowState.MainMenu)
                {
                    SameSave(g);Require(Object.FindObjectsOfType<VillagePaths>().Length==0&&Object.FindObjectsOfType<VillageBuildingVisual>().Length==0,"Menu releases nested path and building instances");g.ContinueGame();Phase(10);
                }
                else if(p==10&&g.State==GameFlowState.Playing)
                {
                    Require(g.Housing.Inside&&g.Housing.Upgrade.Level==1&&g.Furniture.State.Count==1&&g.Housing.Entrance.GetComponentsInChildren<VillageBuildingVisual>().Length==2,"Sleep checkpoint reloads upgraded building and furniture");
                    Require(Object.FindObjectsOfType<VillagePaths>().Length==1,"Reload owns one path network");SameSave(g);Move(g,g.Configuration.Housing.InteriorOrigin+new Vector3(0,.1f,-3));g.ExitHome();Phase(11);
                }
                else if(p==11&&g.State==GameFlowState.Playing&&!g.Housing.Inside)
                {SameSave(g);g.ReturnToMainMenu();Phase(12);}
                else if(p==12&&View<PauseView>()!=null){View<PauseView>().ReturnMenuButton.onClick.Invoke();Phase(13);}
                else if(p==13&&g.State==GameFlowState.MainMenu)
                {
                    File.WriteAllText("Logs/VillageArtVerification.txt","PASS: packed AA house/hall/upgrade/path art and artist preservation, original door/body/board collision footprint, roads outside all 12 farm sizes, actual road movement and pointer filtering, real board/order/upgrade/door UI, no daytime save, real sleep completion retaining furniture, upgraded exterior, checkpoint reload, menu cleanup and one path network.\n");
                    Debug.Log("Village art flow verification PASSED.");Phase(15);EditorApplication.isPlaying=false;
                }
            }
            catch(Exception error){Debug.LogException(error);SessionState.SetBool(Key,false);EditorApplication.isPlaying=false;EditorApplication.Exit(1);}
        }
    }
}
