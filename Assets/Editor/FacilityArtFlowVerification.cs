using System;
using System.IO;
using System.Linq;
using MagicIsland.CameraSystem;
using MagicIsland.Farming;
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
    [InitializeOnLoad] public static class FacilityArtFlowVerification
    {
        const string Key="MagicIsland.FacilityArtVerification";
        static ShopManager fixture;
        static FacilityArtFlowVerification(){if(SessionState.GetBool(Key,false))EditorApplication.update+=Tick;}
        public static void Run()
        {
            PrototypeAddressablesSetup.EnsureSetup();
            var paths=new[]{FacilityArtSetup.ShopPath,"Assets/Art/Prefabs/Buildings/ToolWorkbench.prefab","Assets/Art/Prefabs/Buildings/CookingStation.prefab","Assets/Art/Prefabs/Buildings/StorageChest.prefab","Assets/Config/GameConfig.asset"}
                .Concat(Directory.GetFiles(FacilityArtSetup.MaterialFolder,"*.mat")).Concat(Directory.GetFiles(FacilityArtSetup.MeshFolder,"*.asset")).ToArray();
            var bytes=paths.Select(File.ReadAllBytes).ToArray();PrototypeAddressablesSetup.EnsureSetup();for(int i=0;i<paths.Length;i++)Require(bytes[i].SequenceEqual(File.ReadAllBytes(paths[i])),"Setup preserves facility art and configuration");
            EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");PrototypeAddressablesSetup.BuildContent();var settings=AddressableAssetSettingsDefaultObject.Settings;
            settings.ActivePlayModeDataBuilderIndex=settings.DataBuilders.FindIndex(b=>b is BuildScriptPackedPlayMode);
            SessionState.SetBool(Key,true);Phase(0);SessionState.SetFloat(Key+".Start",(float)EditorApplication.timeSinceStartup);EditorApplication.update-=Tick;EditorApplication.update+=Tick;EditorApplication.isPlaying=true;
        }
        static void Require(bool ok,string why){if(!ok)throw new Exception(why);}
        static void Phase(int p){SessionState.SetInt(Key+".Phase",p);Debug.Log("Facility art phase "+p);}
        static T View<T>() where T:Component=>Object.FindObjectOfType<T>();
        static void Move(GameManager g,Vector3 p){g.Players.RestorePosition(p);g.Players.Player.ViewCamera.GetComponent<CameraController>().SnapToTarget();Physics.SyncTransforms();}
        static void SaveDirectory(GameManager g,string dir)=>g.Saves.Initialize(g.Configuration.Config,g.Configuration.Items,g.Configuration.Crops,dir,g.Configuration.Tasks,g.Configuration.Orders,g.Configuration.Seasons,g.Configuration.Gathering,g.Configuration.ToolUpgrades,g.Configuration.Storage,g.Configuration.NPCs,g.Configuration.Relationships,g.Configuration.Stories,g.Configuration.NPCSchedules,g.Configuration.Fishing,g.Configuration.Crafting,g.Configuration.Animals);
        static void NoSave(GameManager g)=>Require(!File.Exists(g.Saves.SavePath),"Facilities cannot save daytime operations");
        static void SameSave(GameManager g)=>Require(File.ReadAllText(g.Saves.SavePath)==SessionState.GetString(Key+".Saved",""),"Daytime exit and menu cannot replace sleep checkpoint");
        static int BagIndex(GameManager g,string id)=>Enumerable.Range(0,g.Inventory.State.Capacity).First(i=>g.Inventory.State.Slots[i].ItemId==id);
        static void Click(GameManager g,Component actor,InteractionKind kind)
        {
            Move(g,actor.transform.position+new Vector3(0,.1f-actor.transform.position.y,-1.5f));
            foreach(var c in actor.GetComponentsInChildren<Collider>())
            {
                var target=g.Interactions.RefreshPointer(new Ray(c.bounds.center+Vector3.up*7,Vector3.down),false);
                if(target.Kind!=kind)continue;Require(target.InRange&&g.Interactions.TryInteract(),"Real mouse dispatch to "+kind);return;
            }
            throw new Exception("Cannot target facility "+kind);
        }
        static void Capture(GameManager g,string name,float size,Vector3 focus)
        {
            var view=g.Players.Player.ViewCamera;float before=view.orthographicSize;view.orthographicSize=size;view.GetComponent<CameraController>().SnapToTarget();view.transform.position+=focus-(g.Players.Player.transform.position+Vector3.up*.6f);
            foreach(var text in Object.FindObjectsOfType<TMPro.TMP_Text>())text.ForceMeshUpdate(true);
            var canvases=Object.FindObjectsOfType<Canvas>();var enabled=canvases.Select(c=>c.enabled).ToArray();g.Cameras.SetOcclusionEnabled(false);
            try{foreach(var c in canvases)c.enabled=false;GameUiPreview.CaptureInventory(view);File.Copy("Logs/InventoryPreview.png","Logs/"+name+".png",true);}
            finally{for(int i=0;i<canvases.Length;i++)canvases[i].enabled=enabled[i];view.orthographicSize=before;g.Cameras.SetOcclusionEnabled(true);}
        }
        static void CheckWorld(GameManager g)
        {
            Require(g.Shop.OwnsCounter&&g.Shop.Actor!=null&&g.Shop.Counter==g.Shop.Actor.transform,"ShopManager owns AA stand");
            Require(g.Shop.Counter.localPosition==new Vector3(g.Configuration.Config.FarmSize*.6f+1.5f,.6f,-4.6f)&&g.Shop.Counter.localScale==Vector3.one,"Original shop position retained with unit prefab scale");
            var box=g.Shop.Actor.GetComponent<BoxCollider>();Require(box.center==Vector3.zero&&box.size==new Vector3(1.4f,1.2f,1)&&g.Shop.Actor.GetComponentsInChildren<Collider>().Length==1,"Old counter body footprint retained without low canopy obstruction");
            Require(Object.FindObjectOfType<MagicIsland.Prototype.FarmPrototype>().transform.Find("Island Shop")==null,"Runtime placeholder shop removed");
            foreach(var actor in new Component[]{g.Shop.Actor,g.ToolUpgrades.Workbench,g.Cooking.Station,g.Storage.Chest})
            {
                Require(actor.GetComponent<FacilityVisual>().ArtVersion==FacilityArtSetup.Version&&actor.GetComponent<CameraOccluder>()!=null,"Facility AA art and occlusion configured");
                var details=actor.transform.Find("Facility details");if(details!=null)Require(details.GetComponentsInChildren<Collider>().Length==0,"Detail cannot change facility collision footprint");
            }
            Require(g.NPCSchedules.Bound,"New stand and details preserve NPC routes");
            Capture(g,"FacilitiesOverview",11,new Vector3(5.8f,.7f,-2.6f));
            Move(g,g.Shop.Counter.position+new Vector3(-2,-.5f,-1.5f));Capture(g,"MarketStandPreview",3.5f,g.Shop.Counter.position+Vector3.up*.3f);
            Move(g,g.Cooking.Station.transform.position+new Vector3(-2,.1f,-1.5f));Capture(g,"KitchenStationPreview",3,g.Cooking.Station.transform.position+Vector3.up*.65f);
            Move(g,g.Storage.Chest.transform.position+new Vector3(-2,.1f,-1.5f));Capture(g,"StorageChestArtPreview",2.8f,g.Storage.Chest.transform.position+Vector3.up*.4f);
            Move(g,g.ToolUpgrades.Workbench.transform.position+new Vector3(-2,.1f,-1.5f));Capture(g,"ToolWorkbenchArtPreview",3,g.ToolUpgrades.Workbench.transform.position+Vector3.up*.75f);
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;int p=SessionState.GetInt(Key+".Phase",0);
            if(p==21&&!EditorApplication.isPlaying){SessionState.SetBool(Key,false);EditorApplication.Exit(0);return;}
            try
            {
                if(EditorApplication.timeSinceStartup-SessionState.GetFloat(Key+".Start",0)>240)throw new Exception("Facility timeout "+p);
                if(!EditorApplication.isPlaying)return;var g=GameManager.Instance;if(g==null)return;Require(g.State!=GameFlowState.Failed,"Facility startup");
                if(p==1&&g.State==GameFlowState.MainMenu)throw new Exception("Facility world initialization returned to menu; inspect manager error");
                if(p==0&&g.State==GameFlowState.MainMenu)
                {string dir=Path.GetFullPath("Logs/FacilitySaves/"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);SaveDirectory(g,dir);g.EnterGame();Phase(1);}
                else if(p==1&&g.State==GameFlowState.Playing)
                {
                    CheckWorld(g);g.Economy.Restore(1000);Require(g.Inventory.TryAdd("turnip",5)&&g.Inventory.TryAdd("wood",12)&&g.Inventory.TryAdd("stone",6),"Prepare facility fixtures");g.Shop.SelectOffer(0);Click(g,g.Shop.Actor,InteractionKind.Shop);Phase(2);
                }
                else if(p==2&&View<ShopView>()!=null)
                {
                    var seed=g.Shop.Seed;var crop=g.Shop.Crop;long gold=g.Economy.Gold;long count=g.Inventory.GetCount(seed.Id);View<ShopView>().BuyButton.onClick.Invoke();
                    Require(g.Inventory.GetCount(seed.Id)==count+1&&g.Economy.Gold==gold-seed.BuyPrice,"Stand UI buys one seed");gold=g.Economy.Gold;count=g.Inventory.GetCount(crop.Id);View<ShopView>().SellButton.onClick.Invoke();
                    Require(g.Inventory.GetCount(crop.Id)==count-1&&g.Economy.Gold==gold+crop.SellPrice,"Stand UI sells one crop");
                    gold=g.Economy.Gold;Move(g,g.Shop.Counter.position+Vector3.right*5);g.TradeShop(true);Require(g.Economy.Gold==gold,"Distant stand confirmation cannot trade");NoSave(g);g.CloseShop();Click(g,g.ToolUpgrades.Workbench,InteractionKind.Workbench);Phase(3);
                }
                else if(p==3&&View<CraftingView>()!=null)
                {View<CraftingView>().UpgradeButton.onClick.Invoke();Phase(19);}
                else if(p==19&&View<ToolUpgradeView>()!=null)
                {
                    View<ToolUpgradeView>().UpgradeButtons[0].onClick.Invoke();Require(g.ToolUpgrades.State.Level(FarmTool.Axe)==1,"Styled tool station preserves upgrade");NoSave(g);View<ToolUpgradeView>().CraftingButton.onClick.Invoke();Phase(4);
                }
                else if(p==4&&View<CraftingView>()!=null)
                {
                    View<CraftingView>().CraftButtons[0].onClick.Invoke();Require(g.Inventory.GetCount("wood_fence")==1,"Workbench MVC crafts one decoration");NoSave(g);g.CloseCrafting();Click(g,g.Cooking.Station,InteractionKind.Cooking);Phase(5);
                }
                else if(p==5&&View<CookingView>()!=null)
                {
                    long before=g.Inventory.GetCount("turnip");View<CookingView>().CookButtons[0].onClick.Invoke();Require(g.Inventory.GetCount("turnip_soup")==1&&g.Inventory.GetCount("turnip")==before-2,"Styled kitchen MVC cooks one soup");NoSave(g);g.CloseCooking();Click(g,g.Storage.Chest,InteractionKind.Storage);Phase(6);
                }
                else if(p==6&&View<StorageView>()!=null)
                {
                    g.TransferStorage(true,BagIndex(g,"wood"),3);Require(g.Storage.State.Contents.GetCount("wood")==3,"Styled chest deposits material");int slot=Enumerable.Range(0,g.Storage.State.Contents.Capacity).First(i=>g.Storage.State.Contents.Slots[i].ItemId=="wood");
                    g.TransferStorage(false,slot,1);Require(g.Storage.State.Contents.GetCount("wood")==2,"Styled chest withdraws material");NoSave(g);g.CloseStorage();Move(g,g.Housing.Entrance.Position+new Vector3(0,.1f,-1.4f));g.EnterHome();Phase(7);
                }
                else if(p==7&&g.State==GameFlowState.Playing&&g.Housing.Inside)
                {Move(g,g.Configuration.Housing.InteriorOrigin+new Vector3(-1.5f,.1f,2));g.RequestRest();Phase(8);}
                else if(p==8&&View<SleepView>()!=null)
                {
                    View<SleepView>().ConfirmButton.onClick.Invoke();Require(g.Saves.TryLoad(out var save)&&save.Day==2&&save.Storage.Sum(s=>s.Quantity)==2&&save.Slots.Any(s=>s.ItemId=="turnip_soup"&&s.Quantity==1)&&save.Slots.Any(s=>s.ItemId=="wood_fence"&&s.Quantity==1),"Actual sleep saves whole facility operation results");
                    SessionState.SetString(Key+".Saved",File.ReadAllText(g.Saves.SavePath));g.StartNewDayFromReport();g.ReturnToMainMenu();Phase(9);
                }
                else if(p==9&&View<PauseView>()!=null){View<PauseView>().ReturnMenuButton.onClick.Invoke();Phase(10);}
                else if(p==10&&g.State==GameFlowState.MainMenu)
                {
                    SameSave(g);Require(!g.Shop.OwnsCounter&&g.Shop.Counter==null&&Object.FindObjectsOfType<ShopCounter>().Length==0,"Menu releases AA shop instance");
                    fixture=new GameObject("Shop load fixture").AddComponent<ShopManager>();fixture.StartCoroutine(fixture.LoadCounter("Shop_Verification_Missing",fixture.transform,5));Phase(11);
                }
                else if(p==11&&fixture.LastError!=null)
                {Require(!fixture.OwnsCounter&&fixture.Actor==null,"Failed shop AA load releases handle");fixture.StartCoroutine(fixture.LoadCounter(g.Configuration.Config.ShopPrefabAddress,fixture.transform,5));fixture.Clear();Phase(12);}
                else if(p==12)Phase(13);
                else if(p==13)
                {Require(!fixture.OwnsCounter&&fixture.Counter==null&&fixture.GetComponentsInChildren<ShopCounter>().Length==0,"Cancelled shop load releases late instance");Object.Destroy(fixture.gameObject);g.ContinueGame();Phase(14);}
                else if(p==14&&g.State==GameFlowState.Playing)
                {
                    Require(g.Housing.Inside&&g.Shop.OwnsCounter&&Object.FindObjectsOfType<ShopCounter>().Length==1&&g.Inventory.GetCount("turnip_soup")==1&&g.Storage.State.Contents.GetCount("wood")==2&&g.ToolUpgrades.State.Level(FarmTool.Axe)==1,"Checkpoint reload restores facility results and one shop");SameSave(g);
                    Move(g,g.Configuration.Housing.InteriorOrigin+new Vector3(0,.1f,-3));g.ExitHome();Phase(15);
                }
                else if(p==15&&g.State==GameFlowState.Playing&&!g.Housing.Inside)
                {SameSave(g);g.ReturnToMainMenu();Phase(16);}
                else if(p==16&&View<PauseView>()!=null){View<PauseView>().ReturnMenuButton.onClick.Invoke();Phase(17);}
                else if(p==17&&g.State==GameFlowState.MainMenu){g.EnterGame();Phase(18);}
                else if(p==18&&g.State==GameFlowState.Playing)
                {
                    Require(g.Shop.OwnsCounter&&Object.FindObjectsOfType<ShopCounter>().Length==1&&g.Inventory.GetCount("turnip_soup")==0,"Fresh game owns one stand and resets live results");SameSave(g);
                    File.WriteAllText("Logs/FacilityArtVerification.txt","PASS: packed AA facility art and artist preservation, original positions/body footprint, mouse dispatch, stand buy/sell/range guards, tool upgrade/crafting/cooking/storage, no daytime save, real sleep checkpoint/reload, AA unload/failure/cancel and fresh-game re-entry.\n");Debug.Log("Facility art flow verification PASSED.");Phase(21);EditorApplication.isPlaying=false;
                }
            }
            catch(Exception error){Debug.LogException(error);SessionState.SetBool(Key,false);EditorApplication.isPlaying=false;EditorApplication.Exit(1);}
        }
    }
}
