using System;
using System.IO;
using System.Linq;
using System.Reflection;
using MagicIsland.CameraSystem;
using MagicIsland.Farming;
using MagicIsland.Gathering;
using MagicIsland.Interaction;
using MagicIsland.Inventory;
using MagicIsland.Managers;
using MagicIsland.Saving;
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
    [InitializeOnLoad] public static class BeachCloudFlowVerification
    {
        const string Key="MagicIsland.BeachCloudVerification";
        static string pickedId,pickedItem,savedJson;
        static Vector3[] remainingPositions;
        static string[] remainingIds;
        static long gold;
        static int stamina;
        static BeachCloudFlowVerification(){if(SessionState.GetBool(Key,false))EditorApplication.update+=Tick;}
        public static void Run()
        {
            PrototypeAddressablesSetup.EnsureSetup();CloudFlowVerification.VerifyAssets();
            var glass=AssetDatabase.LoadAssetAtPath<Mesh>(BeachForageSetup.Meshes+"/BeachSeaGlass.asset");
            Require(glass.normals.All(n=>n.y>0),"Glass surfaces face the overhead gameplay camera");
            var paths=new[]{CloudSetup.PrefabPath,CloudSetup.MeshPath,CloudSetup.MaterialPath,CloudSetup.CookiePath,BeachForageSetup.MaterialPath,"Assets/Config/ItemCatalog.asset","Assets/Config/GatheringCatalog.asset","Assets/Art/Prefabs/UI/ShopPanel.prefab"}
                .Concat(BeachPickup.Items.Select(id=>BeachForageSetup.Folder+"/"+BeachPickup.Address(id)+".prefab")).Concat(Directory.GetFiles(BeachForageSetup.Meshes,"*.asset")).ToArray();
            var bytes=paths.Select(File.ReadAllBytes).ToArray();PrototypeAddressablesSetup.EnsureSetup();
            for(int i=0;i<paths.Length;i++)Require(bytes[i].SequenceEqual(File.ReadAllBytes(paths[i])),"Setup preserves authored resources "+paths[i]);
            EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");PrototypeAddressablesSetup.BuildContent();
            var settings=AddressableAssetSettingsDefaultObject.Settings;settings.ActivePlayModeDataBuilderIndex=settings.DataBuilders.FindIndex(b=>b is BuildScriptPackedPlayMode);
            SessionState.SetBool(Key,true);SessionState.SetFloat(Key+".Start",(float)EditorApplication.timeSinceStartup);Phase(0);
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;EditorApplication.isPlaying=true;
        }
        static void Require(bool ok,string why){if(!ok)throw new Exception("Beach/cloud verification: "+why);}
        static void Phase(int value){SessionState.SetInt(Key+".Phase",value);SessionState.SetFloat(Key+".Time",(float)EditorApplication.timeSinceStartup);Debug.Log("Beach/cloud phase "+value);}
        static T View<T>()where T:Component=>Object.FindObjectOfType<T>();
        static void Move(GameManager g,Vector3 p){g.Players.RestorePosition(p);g.Players.Player.ViewCamera.GetComponent<CameraController>().SnapToTarget();Physics.SyncTransforms();}
        static void Approach(GameManager g,BeachPickup node){var p=node.transform.position;Move(g,p-new Vector3(p.x,0,p.z).normalized*1.1f+Vector3.up*.15f);}
        static void NoSave(GameManager g){Require(savedJson==null?!File.Exists(g.Saves.SavePath):File.ReadAllText(g.Saves.SavePath)==savedJson,"Only sleep writes progress");}
        static void Capture(GameManager g,string name,bool ui)
        {
            var camera=g.Players.Player.ViewCamera;float size=camera.orthographicSize;var canvases=Object.FindObjectsOfType<Canvas>();var enabled=canvases.Select(c=>c.enabled).ToArray();
            try{if(!ui){foreach(var c in canvases)c.enabled=false;camera.orthographicSize=4.3f;}typeof(CameraManager).GetMethod("UpdateOcclusion",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(g.Cameras,new object[]{1f});g.UI.RefreshViews();GameUiPreview.CaptureInventory(camera);File.Copy("Logs/InventoryPreview.png","Logs/"+name+".png",true);}
            finally{camera.orthographicSize=size;for(int i=0;i<canvases.Length;i++)canvases[i].enabled=enabled[i];}
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;int p=SessionState.GetInt(Key+".Phase",0);
            if(p==30&&!EditorApplication.isPlaying){SessionState.SetBool(Key,false);EditorApplication.Exit(0);return;}
            if(EditorApplication.timeSinceStartup-SessionState.GetFloat(Key+".Time",0)<.6)return;
            try
            {
                Require(EditorApplication.timeSinceStartup-SessionState.GetFloat(Key+".Start",0)<240,"Flow timeout phase "+p);
                if(!EditorApplication.isPlaying)return;var g=GameManager.Instance;if(g==null)return;Require(g.State!=GameFlowState.Failed,"World initialization");
                if(p==1&&g.State==GameFlowState.MainMenu)throw new Exception("World returned to menu while loading beach/clouds");
                if(p==0&&g.State==GameFlowState.MainMenu)
                {
                    string dir=Path.GetFullPath("Logs/BeachCloudSaves/"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
                    g.Saves.Initialize(g.Configuration.Config,g.Configuration.Items,g.Configuration.Crops,dir,g.Configuration.Tasks,g.Configuration.Orders,g.Configuration.Seasons,g.Configuration.Gathering,g.Configuration.ToolUpgrades,g.Configuration.Storage,g.Configuration.NPCs,g.Configuration.Relationships,g.Configuration.Stories,g.Configuration.NPCSchedules,g.Configuration.Fishing,g.Configuration.Crafting,g.Configuration.Animals,g.Configuration.Orchards);
                    savedJson=null;g.EnterGame();Phase(1);
                }
                else if(p==1&&g.State==GameFlowState.Playing)
                {
                    g.Time.Clock.Restore(750);g.Weather.Tick(750,true);
                    var lighting=g.Configuration.Weather;g.Ocean.Surface.Tick(0,(float)DaylightCycle.Evaluate(750,lighting.DawnMinute,lighting.DaylightMinute,lighting.DuskMinute,lighting.NightMinute),false);
                    Require(g.Gathering.BeachState.Count==10&&g.Gathering.BeachNodes.Count==10,"Ten new-game AA beach pickups");
                    Require(g.Gathering.Nodes.Count==g.Configuration.Gathering.Nodes.Count,"Legacy trees/rocks intact");
                    foreach(var node in g.Gathering.BeachNodes)
                    {
                        var pos=node.transform.position;
                        Require(IslandLayout.ContainsLand(pos.x,pos.z,.32)&&!IslandLayout.PierReserved(pos.x,pos.z)&&pos.y>g.Ocean.Surface.SeaLevel,"Reachable dry beach outside pier");
                        Require(node.Target.isTrigger&&node.GetComponent<MeshFilter>().sharedMesh!=null&&node.GetComponent<Renderer>().sharedMaterial!=null,"Authored art and non-blocking pickup collider");
                        Approach(g,node);var target=g.Interactions.RefreshPointer(new Ray(node.Target.bounds.center+Vector3.up*3,Vector3.down),false);
                        Require(target.Kind==InteractionKind.BeachForage&&target.Ready&&target.InRange,"Actual mouse ray resolves each beach pickup");
                    }
                    CloudFlowVerification.VerifyRuntime(g);CloudFlowVerification.CapturePreviews(g);
                    var first=g.Gathering.BeachNodes[0];pickedId=first.Id;pickedItem=first.ItemId;Approach(g,first);Capture(g,"BeachForagePreview",false);
                    var bag=g.Inventory.State;var original=bag.Slots.ToArray();bag.Restore(new InventorySlot[bag.Capacity]);Require(bag.TryAdd("stone",99*bag.Capacity),"Full bag fixture");
                    Require(!g.Tools.TryBeachPickup(first,out _)&&g.Gathering.Owns(first)&&g.Gathering.BeachState.Count==10&&bag.GetCount(pickedItem)==0,"Full bag preserves item/model and pays nothing");bag.Restore(original);
                    Move(g,Vector3.up*.1f);var far=g.Interactions.RefreshPointer(new Ray(first.Target.bounds.center+Vector3.up*3,Vector3.down),false);Require(!far.InRange&&!g.Interactions.TryInteract(),"Distance guard");
                    Approach(g,first);Require(g.Tools.SelectTool(FarmTool.Axe),"Keep a selected axe while picking up");stamina=g.Stamina.State.Current;
                    var near=g.Interactions.RefreshPointer(new Ray(first.Target.bounds.center+Vector3.up*3,Vector3.down),false);
                    Require(near.Ready&&g.Interactions.TryInteract(),"Mouse pickup action");
                    Require(g.Inventory.GetCount(pickedItem)==1&&g.Gathering.BeachState.Count==9&&g.Gathering.BeachNodes.Count==9&&!first.gameObject.activeSelf&&g.Stamina.State.Current==stamina,"One atomic free pickup, removes visual immediately");
                    Require(!g.Gathering.Collect(first,out _)&&g.Inventory.GetCount(pickedItem)==1,"Duplicate pickup cannot pay twice");NoSave(g);Phase(2);
                }
                else if(p==2&&!g.Tools.Busy)
                {
                    Require(g.Tools.SelectedTool==FarmTool.Axe&&g.Stamina.State.Current==stamina,"Pickup preserves selected tool and stamina");
                    Capture(g,"BeachPickupPreview",true);Move(g,g.Shop.Counter.position+new Vector3(0,.1f,-1.4f));g.OpenShop();Phase(3);
                }
                else if(p==3&&View<ShopView>()!=null)
                {
                    var view=View<ShopView>();Require(view.CropButtons.Length>=g.Shop.SaleItems.Count,"All beach sale options visible in AA MVC panel");
                    int index=Enumerable.Range(0,g.Shop.SaleItems.Count).Single(i=>g.Shop.SaleItems[i].Id==pickedItem);view.CropButtons[index].onClick.Invoke();
                    Require(!view.BuyButton.interactable&&view.SellButton.interactable,"Forage is only sold");gold=g.Economy.Gold;view.SellButton.onClick.Invoke();
                    Require(g.Inventory.GetCount(pickedItem)==0&&g.Economy.Gold==gold+g.Configuration.Items.Find(pickedItem).SellPrice,"Real MVC sale pays configured price");
                    foreach(var text in view.GetComponentsInChildren<TMPro.TMP_Text>(true)){text.ForceMeshUpdate(true);Require(!text.isTextOverflowing&&text.text.Where(c=>!char.IsControl(c)).All(c=>text.font.HasCharacter(c)),"Chinese shop text fits: "+text.name);}
                    Capture(g,"BeachShopPreview",true);NoSave(g);g.CloseShop();
                    remainingIds=g.Gathering.BeachNodes.Select(n=>n.Id).ToArray();remainingPositions=g.Gathering.BeachNodes.Select(n=>n.transform.position).ToArray();
                    Move(g,g.Housing.Entrance.Position+new Vector3(0,.1f,-1.4f));g.EnterHome();Phase(4);
                }
                else if(p==4&&g.State==GameFlowState.Playing&&g.Housing.Inside)
                {
                    g.Weather.Tick(g.Time.Clock.Minute,true);Require(!g.Weather.Clouds.gameObject.activeSelf&&!g.Weather.Clouds.ShadowsActive&&g.Weather.Sun.cookie==null,"Actual indoor transition disables clouds/shadows");
                    Move(g,g.Configuration.Housing.InteriorOrigin+new Vector3(-1.5f,.1f,2));g.RequestRest();Phase(5);
                }
                else if(p==5&&View<SleepView>()!=null)
                {
                    NoSave(g);View<SleepView>().ConfirmButton.onClick.Invoke();Require(g.Saves.TryLoad(out var save),"Sleep checkpoint loads: "+g.Saves.LastError);
                    Require(save.Version==SaveData.CurrentVersion&&save.Day==2&&save.BeachForage.Entries.Length==14&&!save.BeachForage.Entries.Any(e=>e.Id==pickedId),"Sleep replenishes five, retains collection and saves version 29");
                    for(int i=0;i<remainingIds.Length;i++)Require(g.Gathering.BeachNodes.Single(n=>n.Id==remainingIds[i]).transform.position==remainingPositions[i],"Unpicked items retain stable location overnight");
                    var old=JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(save));old.Version=28;old.BeachForage=null;old.Upgrade(100);
                    Require(old.Version==SaveData.CurrentVersion&&old.BeachForage.Day==save.Day&&old.BeachForage.Entries.Length==10&&old.Gold==save.Gold&&old.Gathering.Length==save.Gathering.Length,"Version 28 migration initializes beach without changing resources/gold");
                    savedJson=File.ReadAllText(g.Saves.SavePath);g.StartNewDayFromReport();g.ReturnToMainMenu();Phase(6);
                }
                else if(p==6&&View<PauseView>()!=null){View<PauseView>().ReturnMenuButton.onClick.Invoke();Phase(7);}
                else if(p==7&&g.State==GameFlowState.MainMenu)
                {
                    Require(g.Gathering.BeachState==null&&g.Gathering.BeachNodes.Count==0&&g.Weather.Clouds==null&&!g.Weather.OwnsClouds&&Object.FindObjectsOfType<CloudField>(true).Length==0&&Object.FindObjectsOfType<BeachPickup>(true).Length==0,"Return to menu releases cloud/pickup instances and AA ownership");NoSave(g);g.ContinueGame();Phase(8);
                }
                else if(p==8&&g.State==GameFlowState.Playing)
                {
                    Require(g.Gathering.BeachState.Count==14&&g.Gathering.BeachNodes.Count==14&&g.Gathering.BeachState.Find(pickedId)==null,"Loading restores saved beach without refill");
                    for(int i=0;i<remainingIds.Length;i++)Require(g.Gathering.BeachNodes.Single(n=>n.Id==remainingIds[i]).transform.position==remainingPositions[i],"Reload keeps beach locations");
                    Require(g.Weather.OwnsClouds&&g.Weather.Clouds!=null,"Clouds reinitialize once on load");NoSave(g);g.ReturnToMainMenu();Phase(9);
                }
                else if(p==9&&View<PauseView>()!=null){View<PauseView>().ReturnMenuButton.onClick.Invoke();Phase(10);}
                else if(p==10&&g.State==GameFlowState.MainMenu)
                {
                    NoSave(g);File.WriteAllText("Logs/BeachCloudVerification.txt","PASS: preserved authored assets; packed-AA beach/cloud loading; ten reachable dry shoreline pickups; mouse/range/full bag/duplicate/stamina/tool guards; MVC sale and Chinese layout; five overnight spawns and retained sites; sleep-only v29 save; v28 migration; reload without refill; menu/indoor ownership cleanup; sparse moving cloud shadows, pause/rain/night and rendered sunlight comparison.\n");
                    Debug.Log("Beach/cloud flow verification PASSED.");Phase(30);EditorApplication.isPlaying=false;
                }
            }
            catch(Exception error){Debug.LogException(error);SessionState.SetBool(Key,false);EditorApplication.isPlaying=false;EditorApplication.Exit(1);}
        }
    }
}
