using System;
using System.IO;
using System.Linq;
using MagicIsland.Inventory;
using MagicIsland.Managers;
using MagicIsland.Saving;
using MagicIsland.UI.Views;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace MagicIsland.EditorTools
{
    [InitializeOnLoad] public static class PauseSleepFlowVerification
    {
        const string Key="MagicIsland.PauseSleepVerification";
        static PauseSleepFlowVerification() { if(SessionState.GetBool(Key,false)) EditorApplication.update+=Tick; }
        public static void Run()
        {
            PrototypeAddressablesSetup.EnsureSetup(); PrototypeAddressablesSetup.ValidateUiPrefabs();
            string[] paths=Directory.GetFiles(PrototypeUiPrefabBuilder.Folder,"*.prefab"); var bytes=paths.Select(File.ReadAllBytes).ToArray();PrototypeAddressablesSetup.EnsureSetup();
            for(int i=0;i<paths.Length;i++) Require(bytes[i].SequenceEqual(File.ReadAllBytes(paths[i])),"Repeated setup preserves all edited UI");
            EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");PrototypeAddressablesSetup.BuildContent();
            var settings=AddressableAssetSettingsDefaultObject.Settings;settings.ActivePlayModeDataBuilderIndex=settings.DataBuilders.FindIndex(b=>b is BuildScriptPackedPlayMode);
            SessionState.SetBool(Key,true);Phase(0);SessionState.SetFloat(Key+".Start",(float)EditorApplication.timeSinceStartup);
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;EditorApplication.isPlaying=true;
        }
        static void Require(bool b,string why) { if(!b) throw new Exception(why); }
        static void Phase(int p) { SessionState.SetInt(Key+".Phase",p);Debug.Log("Pause sleep phase "+p); }
        static T View<T>() where T:Component => UnityEngine.Object.FindObjectOfType<T>();
        static string Dir=>SessionState.GetString(Key+".Dir","");
        static void SaveDirectory(GameManager g,string dir)=>g.Saves.Initialize(g.Configuration.Config,g.Configuration.Items,g.Configuration.Crops,dir,g.Configuration.Tasks,g.Configuration.Orders,g.Configuration.Seasons,g.Configuration.Gathering,g.Configuration.ToolUpgrades,g.Configuration.Storage,g.Configuration.NPCs,g.Configuration.Relationships,g.Configuration.Stories,g.Configuration.NPCSchedules,g.Configuration.Fishing,g.Configuration.Crafting,g.Configuration.Animals);
        static void NoSave(GameManager g)=>Require(!File.Exists(g.Saves.SavePath)&&!File.Exists(g.Saves.SavePath+".bak"),"Daytime operation must not create progress save or backup");
        static void SameSave(GameManager g)=>Require(File.ReadAllText(g.Saves.SavePath)==SessionState.GetString(Key+".Saved",""),"Daytime operation must not replace sleep checkpoint");
        static int Index(GameManager g,string id)=>Enumerable.Range(0,g.Inventory.State.Capacity).First(i=>g.Inventory.State.Slots[i].ItemId==id);
        static void TextCheck(Component root) { foreach(var t in root.GetComponentsInChildren<TMPro.TMP_Text>(true)) { t.ForceMeshUpdate(true);Require(!t.isTextOverflowing&&t.text.Where(c=>!char.IsControl(c)).All(c=>t.font.HasCharacter(c)),"Pause/settings TMP "+t.name+"="+t.text); } }
        static void Capture(GameManager g,string name) { g.UI.RefreshViews();Canvas.ForceUpdateCanvases();g.Players.Player.ViewCamera.GetComponent<MagicIsland.CameraSystem.CameraController>().SnapToTarget();GameUiPreview.CaptureInventory(g.Players.Player.ViewCamera);File.Copy("Logs/InventoryPreview.png","Logs/"+name+".png",true); }
        static void EnterHome(GameManager g) { g.Players.RestorePosition(g.Housing.Entrance.Position+new Vector3(0,.1f,-1.4f));g.EnterHome(); }
        static void Rest(GameManager g) { g.Players.RestorePosition(g.Configuration.Housing.InteriorOrigin+new Vector3(-1.5f,.1f,2));g.RequestRest(); }
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;int p=SessionState.GetInt(Key+".Phase",0);
            if(p==23&&!EditorApplication.isPlaying){SessionState.SetBool(Key,false);EditorApplication.Exit(0);return;}
            try
            {
                if(EditorApplication.timeSinceStartup-SessionState.GetFloat(Key+".Start",0)>240)throw new Exception("Pause sleep timeout "+p);
                if(!EditorApplication.isPlaying)return;var g=GameManager.Instance;if(g==null)return;Require(g.State!=GameFlowState.Failed,"Pause boot failure");
                if(p==0&&g.State==GameFlowState.MainMenu)
                {
                    string dir=Path.GetFullPath("Logs/PauseSleepSaves/"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);SessionState.SetString(Key+".Dir",dir);SaveDirectory(g,dir);g.Settings.Initialize(dir+"/Settings");
                    g.Settings.SetVolume(.62f);Require(g.Settings.Flush(),"Persist first settings");g.Settings.SetVolume(.2f);Require(g.Settings.Flush(),"Create settings backup");File.WriteAllText(g.Settings.SettingsPath,"bad settings");g.Settings.Initialize(dir+"/Settings");Require(g.Settings.Volume==.62f&&g.Settings.Feedback.Contains("备份"),"Corrupt settings use valid independent backup");
                    string blocked=dir+"_settings_blocked";File.WriteAllText(blocked,"blocked");g.Settings.Initialize(blocked);g.Settings.SetVolume(.81f);Require(!g.Settings.Flush()&&g.Settings.Volume==.81f&&g.Settings.Feedback.Contains("保存失败"),"Settings save failure keeps live value and feedback");g.Settings.Initialize(dir+"/Settings");NoSave(g);g.EnterGame();Phase(1);
                }
                else if(p==1&&g.State==GameFlowState.Playing)
                { NoSave(g);g.HandleEscape();Phase(2); }
                else if(p==2&&View<PauseView>()!=null)
                {
                    var view=View<PauseView>();Require(view.Layer==UILayer.PopUp&&view.transform.parent==g.UI.GetLayerRoot(UILayer.PopUp)&&!g.Players.Player.enabled&&!g.Interactions.Controller.enabled,"AA MVC pause stops input");
                    SessionState.SetFloat(Key+".Clock",(float)g.Time.Clock.Minute);TextCheck(view);Capture(g,"PausePreview");view.SettingsButton.onClick.Invoke();Phase(3);
                }
                else if(p==3&&View<SettingsView>()!=null)
                {
                    var view=View<SettingsView>();Require(g.UI.HasPause&&(float)g.Time.Clock.Minute==SessionState.GetFloat(Key+".Clock",-1),"Clock stays paused beneath settings");
                    view.VolumeSlider.value=.37f;view.FullscreenToggle.isOn=true;g.UI.RefreshViews();TextCheck(view);Capture(g,"PauseSettingsPreview");view.CloseButton.onClick.Invoke();NoSave(g);
                    Require(File.Exists(g.Settings.SettingsPath)&&g.Settings.Volume==.37f&&g.Settings.Fullscreen,"Independent settings persisted on close");g.Settings.Initialize(Dir+"/Settings");Require(g.Settings.Volume==.37f&&g.Settings.Fullscreen,"Settings reload restores volume and fullscreen");Phase(4);
                }
                else if(p==4&&View<PauseView>()!=null)
                {
                    Require(!g.UI.HasSettings&&!g.Players.Player.enabled,"Settings close keeps pause");var view=View<PauseView>();view.ReturnMenuButton.onClick.Invoke();Require(g.ReturnConfirmation,"Return requires in-game discard confirmation");TextCheck(view);Capture(g,"PauseReturnPreview");
                    view.ContinueButton.onClick.Invoke();Require(!g.ReturnConfirmation&&g.State==GameFlowState.Playing&&g.UI.HasPause,"Cancel return preserves current game");view.ReturnMenuButton.onClick.Invoke();view.ReturnMenuButton.onClick.Invoke();Phase(5);
                }
                else if(p==5&&g.State==GameFlowState.MainMenu)
                { NoSave(g);Require(!g.Saves.HasSave&&!g.UI.HasPause&&!g.UI.HasSettings,"Unslept new game leaves no save and releases panels");g.EnterGame();Phase(6); }
                else if(p==6&&g.State==GameFlowState.Playing)
                {
                    g.AdvanceDay();Require(g.Farm.State.Day==1,"Public debug advance cannot bypass bed confirmation");Require(!g.SaveProgress(),"Manual save entry disabled");g.Economy.Restore(1000);g.Inventory.TryAdd("wood",10);g.Inventory.TryAdd("turnip",5);g.Inventory.TryAdd("quality_egg",2);
                    g.Players.RestorePosition(g.Shop.Counter.position+new Vector3(0,.1f,-1.5f));g.OpenShop();Phase(7);
                }
                else if(p==7&&View<ShopView>()!=null)
                {
                    var view=View<ShopView>();view.BuyButton.onClick.Invoke();Require(!view.SaveButton.gameObject.activeSelf,"Shop has no manual save button");g.SaveShop();NoSave(g);view.InventoryUpgradeButton.onClick.Invoke();Phase(8);
                }
                else if(p==8&&View<InventoryUpgradeView>()!=null)
                {
                    View<InventoryUpgradeView>().UpgradeButton.onClick.Invoke();Require(g.Inventory.State.Capacity==20&&!View<InventoryUpgradeView>().SaveButton.gameObject.activeSelf,"Upgrade commits in memory without save");NoSave(g);g.CloseInventoryUpgrade();
                    g.Players.RestorePosition(g.Storage.Chest.transform.position+new Vector3(0,.1f,-1.5f));g.OpenStorage();Phase(9);
                }
                else if(p==9&&View<StorageView>()!=null)
                {
                    g.TransferStorage(true,Index(g,"wood"),4);Require(g.Storage.State.Contents.GetCount("wood")==4&&!View<StorageView>().SaveButton.gameObject.activeSelf&&!g.Storage.SavePending,"Storage is live only, no false save failure");NoSave(g);g.CloseStorage();
                    g.Players.RestorePosition(g.Shipping.Box.transform.position+new Vector3(0,.1f,-1.5f));g.OpenShipping();Phase(10);
                }
                else if(p==10&&View<ShippingView>()!=null)
                {
                    g.TransferShipping(true,Index(g,"turnip"),5);g.TransferShipping(true,Index(g,"quality_egg"),2);Require(g.Shipping.State.ExpectedIncome==205&&!View<ShippingView>().SaveButton.gameObject.activeSelf&&!g.Shipping.SavePending,"Shipping queued without daytime save");NoSave(g);g.CloseShipping();EnterHome(g);Phase(11);
                }
                else if(p==11&&g.State==GameFlowState.Playing&&g.Housing.Inside) { NoSave(g);Rest(g);Phase(12); }
                else if(p==12&&View<SleepView>()!=null)
                { View<SleepView>().CancelButton.onClick.Invoke();NoSave(g);Require(g.Farm.State.Day==1,"Cancelled sleep does not advance or save");Rest(g);Phase(13); }
                else if(p==13&&View<SleepView>()!=null)
                {
                    View<SleepView>().ConfirmButton.onClick.Invoke();Require(g.Farm.State.Day==2&&g.DailyReports.State.Pending&&File.Exists(g.Saves.SavePath),"Actual bed sleep creates first progress checkpoint");
                    Require(g.Saves.TryLoad(out var saved)&&saved.Day==2&&saved.ClockMinute==360&&!saved.DailyReport.Pending&&saved.Slots.Length==20&&saved.Storage.Sum(s=>s.Quantity)==4&&saved.Shipping.Slots.All(s=>s.Quantity==0)&&saved.Gold==895,"Sleep snapshot includes entire world and next morning without pending report");
                    SessionState.SetString(Key+".Saved",File.ReadAllText(g.Saves.SavePath));Require(!View<DailyReportView>().SaveButton.gameObject.activeSelf,"Successful sleep hides retry save button");g.StartNewDayFromReport();SameSave(g);g.Inventory.TryAdd("turnip",1);g.Economy.State.TryEarn(11);g.SaveDailyReport();g.SaveFood();SameSave(g);
                    g.HandleEscape();Phase(14);
                }
                else if(p==14&&View<PauseView>()!=null)
                { View<PauseView>().ReturnMenuButton.onClick.Invoke();View<PauseView>().ReturnMenuButton.onClick.Invoke();Phase(15); }
                else if(p==15&&g.State==GameFlowState.MainMenu) { SameSave(g);g.ContinueGame();Phase(16); }
                else if(p==16&&g.State==GameFlowState.Playing)
                {
                    Require(g.Farm.State.Day==2&&g.Time.Clock.Minute>=360&&g.Time.Clock.Minute<361&&g.Economy.Gold==895&&g.Inventory.State.GetCount("turnip")==0&&!g.UI.HasDailyReport&&g.Inventory.State.Capacity==20&&g.Storage.State.Contents.GetCount("wood")==4,"Reload restores last sleep morning and discards later daytime changes");
                    SameSave(g);Rest(g);Phase(17);
                }
                else if(p==17&&View<SleepView>()!=null)
                {
                    string blocked=Dir+"_blocked";File.WriteAllText(blocked,"blocked");SaveDirectory(g,blocked);View<SleepView>().ConfirmButton.onClick.Invoke();
                    Require(g.Farm.State.Day==3&&g.Saves.SleepSavePending&&!g.CanStartSavedMorning&&View<DailyReportView>().SaveButton.gameObject.activeSelf&&!View<DailyReportView>().ContinueButton.interactable,"Sleep failure blocks next day and offers checkpoint-only retry");
                    g.StartNewDayFromReport();g.ConfirmSleep();Require(g.Farm.State.Day==3&&g.DailyReports.State.Pending,"Failed sleep cannot continue or settle again");TextCheck(View<DailyReportView>());Capture(g,"SleepSaveFailurePreview");
                    string pending=(string)typeof(SaveManager).GetField("sleepCheckpointJson",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(g.Saves);
                    g.Players.RestorePosition(g.Configuration.Housing.InteriorOrigin+new Vector3(2,.1f,0));SaveDirectory(g,Dir);g.SaveDailyReport();
                    Require(File.ReadAllText(g.Saves.SavePath)==pending,"Retry writes the immutable sleep snapshot, including original position and timestamp");
                    Require(g.Farm.State.Day==3&&!g.Saves.SleepSavePending&&g.CanStartSavedMorning&&g.Saves.TryLoad(out var saved)&&saved.Day==3&&saved.Gold==895,"Retry persists same sleep without duplicate payout/day");
                    SessionState.SetString(Key+".Saved",File.ReadAllText(g.Saves.SavePath));g.StartNewDayFromReport();SameSave(g);g.ReturnToMainMenu();Phase(18);
                }
                else if(p==18&&View<PauseView>()!=null) { Require(g.ReturnConfirmation,"Report/day return asks before discard");View<PauseView>().ReturnMenuButton.onClick.Invoke();Phase(19); }
                else if(p==19&&g.State==GameFlowState.MainMenu) { SameSave(g);g.ContinueGame();Phase(20); }
                else if(p==20&&g.State==GameFlowState.Playing)
                {
                    Require(g.Farm.State.Day==3&&!g.UI.HasDailyReport&&g.Economy.Gold==895,"Retried checkpoint reloads once");g.HandleEscape();Phase(21);
                }
                else if(p==21&&View<PauseView>()!=null)
                { g.HandleEscape();Require(!g.UI.HasPause&&g.Players.Player.enabled,"ESC resumes input");g.ToggleInventory();g.HandleEscape();Require(!g.UI.IsInventoryVisible&&!g.UI.HasPause,"ESC closes existing panel before opening pause");g.HandleEscape();Phase(22); }
                else if(p==22&&View<PauseView>()!=null)
                {
                    Require(!g.SaveProgress(),"No manual save after sleeping");SameSave(g);
                    File.WriteAllText("Logs/PauseSleepVerification.txt","PASS: packed AA MVC pause/settings, ESC priority, clock and input pause, independent persistent settings, confirmation/cancel/discard, no daytime saves, disabled manual/debug bypass, real sleep whole-world checkpoint, report acknowledgement without write, exact morning reload, sleep-failure immutable retry without duplicate payout and cleanup.\n");Debug.Log("Pause sleep flow verification PASSED.");Phase(23);EditorApplication.isPlaying=false;
                }
            }
            catch(Exception error){Debug.LogException(error);SessionState.SetBool(Key,false);EditorApplication.Exit(1);}
        }
    }
}
