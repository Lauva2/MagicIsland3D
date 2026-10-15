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
    [InitializeOnLoad]
    public static class ShippingFlowVerification
    {
        const string Key = "MagicIsland.ShippingVerification";
        static ShippingFlowVerification() { if (SessionState.GetBool(Key,false)) EditorApplication.update += Tick; }
        public static void Run()
        {
            PrototypeAddressablesSetup.EnsureSetup(); PrototypeAddressablesSetup.ValidateUiPrefabs();
            string[] paths = {"Assets/Art/Prefabs/UI/ShippingPanel.prefab","Assets/Art/Prefabs/UI/DailyReportPanel.prefab","Assets/Art/Prefabs/Buildings/ShippingBox.prefab"};
            var bytes = paths.Select(File.ReadAllBytes).ToArray(); PrototypeAddressablesSetup.EnsureSetup();
            for(int i=0;i<paths.Length;i++) Require(bytes[i].SequenceEqual(File.ReadAllBytes(paths[i])),"Repeated setup preserves shipping resources");
            EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity"); PrototypeAddressablesSetup.BuildContent();
            var settings=AddressableAssetSettingsDefaultObject.Settings; settings.ActivePlayModeDataBuilderIndex=settings.DataBuilders.FindIndex(b=>b is BuildScriptPackedPlayMode);
            SessionState.SetBool(Key,true); Phase(0); SessionState.SetFloat(Key+".Start",(float)EditorApplication.timeSinceStartup);
            EditorApplication.update-=Tick; EditorApplication.update+=Tick; EditorApplication.isPlaying=true;
        }
        static void Require(bool b,string why) { if(!b) throw new Exception(why); }
        static void Phase(int p) { SessionState.SetInt(Key+".Phase",p); Debug.Log("Shipping phase "+p); }
        static ShippingView View => UnityEngine.Object.FindObjectOfType<ShippingView>();
        static DailyReportView Report => UnityEngine.Object.FindObjectOfType<DailyReportView>(true);
        static void SaveDirectory(GameManager g,string dir) => g.Saves.Initialize(g.Configuration.Config,g.Configuration.Items,g.Configuration.Crops,dir,
            g.Configuration.Tasks,g.Configuration.Orders,g.Configuration.Seasons,g.Configuration.Gathering,g.Configuration.ToolUpgrades,g.Configuration.Storage,
            g.Configuration.NPCs,g.Configuration.Relationships,g.Configuration.Stories,g.Configuration.NPCSchedules,g.Configuration.Fishing,g.Configuration.Crafting,g.Configuration.Animals);
        static void Near(GameManager g) => g.Players.RestorePosition(g.Shipping.Box.transform.position+new Vector3(0,.1f,-1.5f));
        static void CheckText(Component root)
        { foreach(var text in root.GetComponentsInChildren<TMPro.TMP_Text>(true)) { text.ForceMeshUpdate(true); Require(!text.isTextOverflowing && text.text.Where(c=>!char.IsControl(c)).All(c=>text.font.HasCharacter(c)),"Shipping TMP "+text.name+"="+text.text); } }
        static void Capture(GameManager g,string name)
        { g.UI.RefreshViews(); Canvas.ForceUpdateCanvases(); g.Players.Player.ViewCamera.GetComponent<MagicIsland.CameraSystem.CameraController>().SnapToTarget(); GameUiPreview.CaptureInventory(g.Players.Player.ViewCamera); File.Copy("Logs/InventoryPreview.png","Logs/"+name+".png",true); }
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false)) return; int p=SessionState.GetInt(Key+".Phase",0);
            if(p==12 && !EditorApplication.isPlaying) { SessionState.SetBool(Key,false); EditorApplication.Exit(0); return; }
            try
            {
                if(EditorApplication.timeSinceStartup-SessionState.GetFloat(Key+".Start",0)>240) throw new Exception("Shipping timeout "+p);
                if(!EditorApplication.isPlaying) return; var g=GameManager.Instance; if(g==null) return; Require(g.State!=GameFlowState.Failed,"Shipping boot failure");
                string dir=SessionState.GetString(Key+".Dir","");
                if(p==0 && g.State==GameFlowState.MainMenu)
                { dir=Path.GetFullPath("Logs/ShippingSaves/"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir); SessionState.SetString(Key+".Dir",dir); SaveDirectory(g,dir); g.EnterGame(); Phase(1); }
                else if(p==1 && g.State==GameFlowState.Playing)
                {
                    Require(g.Configuration.Shipping!=null && g.Shipping.Box!=null && g.Shipping.State.ExpectedIncome==0,"AA config and box initialized empty");
                    var config=g.Configuration.Shipping; Require(!g.Configuration.LoadConfiguration().MoveNext() && g.Configuration.Shipping==config,"Loaded configurations reuse their AA assets");
                    Require(g.Shipping.Box.transform.localPosition==g.Configuration.Housing.ExteriorPosition(g.Configuration.Config.FarmSize)+g.Configuration.Shipping.HouseOffset,"Box placed beside home");
                    g.Players.RestorePosition(new Vector3(5,.1f,5)); g.OpenShipping(); Require(!g.UI.HasShipping,"Out-of-range entry rejected"); Near(g);
                    Physics.SyncTransforms(); var ray=new Ray(g.Shipping.Box.transform.position+new Vector3(0,4,0),Vector3.down);
                    Require(g.Interactions.Controller.RefreshShippingHover(ray,false) && !g.Interactions.Controller.RefreshShippingHover(ray,true),"World marker raycast and UI interception");
                    Require(g.Inventory.TryAdd("turnip",5) && g.Inventory.TryAdd("sardine",2) && g.Inventory.TryAdd("egg",2) && g.Inventory.TryAdd("quality_egg",3),"Prepare eligible crops, fish, both egg qualities");
                    g.OpenShipping(); Phase(2);
                }
                else if(p==2 && View!=null)
                {
                    Require(View.Layer==UILayer.PopUp && View.transform.parent==g.UI.GetLayerRoot(UILayer.PopUp) && View.BagSlots.Count(s=>s.SelectButton.interactable)==10 && View.BagSlots.Skip(10).All(s=>s.LockIcon.enabled),"AA MVC layer and thirty bag cells with locks");
                    double clock=g.Time.Clock.Minute; g.AdvanceDay(); Require(g.Farm.State.Day==1 && !g.Players.Player.enabled && !g.Interactions.Controller.enabled,"Open panel blocks day and inputs");
                    View.BagSlots[0].SelectButton.onClick.Invoke(); Require(!View.MoveOneButton.interactable,"Seed cannot be shipped");
                    View.BagSlots[1].SelectButton.onClick.Invoke(); View.MoveStackButton.onClick.Invoke(); Require(g.Inventory.State.GetCount("turnip")==0 && g.Shipping.State.ExpectedIncome==125 && g.Economy.Gold==100,"Deposit whole stack without immediate payment");
                    View.BoxSlots[0].SelectButton.onClick.Invoke(); View.MoveOneButton.onClick.Invoke(); Require(g.Inventory.State.GetCount("turnip")==1 && g.Shipping.State.ExpectedIncome==100,"Retrieve one before sleep");
                    for(int i=2;i<=4;i++) { View.BagSlots[i].SelectButton.onClick.Invoke(); View.MoveStackButton.onClick.Invoke(); }
                    Require(g.Shipping.State.ExpectedIncome==340 && g.Time.Clock.Minute==clock && g.Stamina.State.Current==100,"Quote and paused clock, no stamina charge");
                    Require(g.Tasks.State.Capture().Single(t=>t.Id=="first_sell").Progress==0,"Queued crops do not count as sales yet");
                    CheckText(View); Capture(g,"ShippingPreview"); Require(g.Saves.TryLoad(out var saved) && saved.Version==26 && saved.Shipping.Slots.Sum(s=>s.Quantity)==11 && saved.Gold==100,"Actual JSON preserves queued contents");
                    g.CloseShipping(); g.ReturnToMainMenu(); Phase(3);
                }
                else if(p==3 && g.State==GameFlowState.MainMenu) { Require(g.Shipping.State==null && g.Shipping.Box==null,"Return releases box and state"); g.ContinueGame(); Phase(4); }
                else if(p==4 && g.State==GameFlowState.Playing)
                {
                    Require(g.Shipping.State.ExpectedIncome==340 && g.Economy.Gold==100 && g.Shipping.State.LastSettlementDay==0,"Unsettled load does not pay"); Near(g); g.OpenShipping(); Phase(5);
                }
                else if(p==5 && View!=null)
                {
                    string blocked=dir+"_blocked"; File.WriteAllText(blocked,"blocked"); SaveDirectory(g,blocked);
                    View.BoxSlots[0].SelectButton.onClick.Invoke(); View.MoveOneButton.onClick.Invoke(); Require(g.Shipping.SavePending && g.Shipping.State.ExpectedIncome==315 && g.Inventory.State.GetCount("turnip")==2,"Failed transfer save retains both inventories");
                    SaveDirectory(g,dir); View.SaveButton.onClick.Invoke(); Require(!g.Shipping.SavePending && g.Shipping.State.ExpectedIncome==315,"Retry only saves, no duplicate transfer"); g.CloseShipping();
                    g.Players.RestorePosition(g.Housing.Entrance.Position+new Vector3(0,.1f,-1.4f)); g.EnterHome(); Phase(13);
                }
                else if(p==13 && g.State==GameFlowState.Playing && g.Housing.Inside)
                { g.Players.RestorePosition(g.Time.Bed.position+new Vector3(0,.1f,-1.2f)); g.RequestRest(); Phase(14); }
                else if(p==14 && UnityEngine.Object.FindObjectOfType<SleepView>()!=null)
                {
                    SaveDirectory(g,dir+"_blocked"); UnityEngine.Object.FindObjectOfType<SleepView>().ConfirmButton.onClick.Invoke();
                    Require(g.Farm.State.Day==2 && g.Economy.Gold==415 && g.Shipping.State.ExpectedIncome==0 && g.DailyReports.State.Pending,"Actual indoor sleep with failed save retains live payout and cleared queue");
                    Require(g.Tasks.State.Capture().Single(t=>t.Id=="first_sell").Progress==1,"Successful crop settlement advances sale task");
                    Require(g.DailyReports.State.Last.Activity.Income=="315" && g.DailyReports.State.Last.Shipping.Length==4 && g.DailyReports.State.Last.Shipping.Sum(s=>(long)s.Revenue)==315,"Ended-day report includes distinct crop, fish and egg lines");
                    g.AdvanceDay(); g.OpenShipping(); g.TransferShipping(true,1,1); Require(g.Farm.State.Day==2 && g.Economy.Gold==415 && !g.UI.HasShipping,"Pending report blocks repeats and shipment actions");
                    SaveDirectory(g,dir); g.SaveDailyReport(); Require(g.Saves.TryLoad(out var settled) && settled.Shipping.LastSettlementDay==1 && settled.Shipping.Slots.All(s=>s.Quantity==0),"Successful retry persists settled marker and no queue");
                    CheckText(Report); Canvas.ForceUpdateCanvases(); Require(((RectTransform)Report.ShippingText.transform.parent).rect.width>600 && ((RectTransform)Report.ShippingText.transform.parent).rect.height>100,"Report shipping viewport has visible area");
                    Capture(g,"ShippingReportPreview"); g.ReturnToMainMenu(); Phase(6);
                }
                else if(p==6 && g.State==GameFlowState.MainMenu) { g.ContinueGame(); Phase(7); }
                else if(p==7 && g.State==GameFlowState.Playing)
                {
                    Require(g.Economy.Gold==415 && g.Shipping.State.ExpectedIncome==0 && g.DailyReports.State.Pending && Report.ShippingText.text.Contains("优质鸡蛋"),"Settled pending report reloads without duplicate payout");
                    g.StartNewDayFromReport(); g.AdvanceDay(); Require(g.Farm.State.Day==3 && g.Economy.Gold==415 && g.DailyReports.State.Last.Shipping.Length==0,"Following empty day earns nothing");
                    g.StartNewDayFromReport(); g.ReturnToMainMenu(); Phase(8);
                }
                else if(p==8 && g.State==GameFlowState.MainMenu)
                {
                    Require(g.Saves.TryLoad(out var legacy),"Read migration fixture"); legacy.Version=25; legacy.Shipping=null; legacy.DailyReport.Last.Shipping=null;
                    var json=JsonUtility.ToJson(legacy,true); File.WriteAllText(g.Saves.SavePath,json); File.WriteAllText(g.Saves.SavePath+".bak",json); g.ContinueGame(); Phase(9);
                }
                else if(p==9 && g.State==GameFlowState.Playing)
                {
                    Require(g.Economy.Gold==415 && g.Shipping.State.ExpectedIncome==0 && g.Shipping.State.LastSettlementDay==0,"Legacy save gains empty box without rewards");
                    Require(g.SaveProgress(),"Save migrated game"); Require(g.Saves.TryLoad(out var current) && current.Version==26,"Legacy saves upgrade on next save");
                    foreach(Action<SaveData> mutate in new Action<SaveData>[] {s=>s.Shipping.LastSettlementDay=s.Day,s=>s.Shipping.Slots=Array.Empty<MagicIsland.Shipping.ShippingSlot>(),s=> {s.Shipping.Slots[0].ItemId="turnip_seed";s.Shipping.Slots[0].Quantity=1;},s=> {s.Shipping.Slots[0].ItemId="turnip";s.Shipping.Slots[0].Quantity=100;},s=> {s.DailyReport.Pending=true;s.Shipping.Slots[0].ItemId="turnip";s.Shipping.Slots[0].Quantity=1;} })
                    {
                        var bad=JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(current)); mutate(bad); jsonWrite(g,bad); Require(!g.Saves.TryLoad(out _),"Corrupt shipping records rejected");
                    }
                    jsonWrite(g,current); g.ReturnToMainMenu(); Phase(10);
                }
                else if(p==10 && g.State==GameFlowState.MainMenu) { g.EnterGame(); Phase(11); }
                else if(p==11 && g.State==GameFlowState.Playing)
                {
                    Require(g.Economy.Gold==100 && g.Shipping.State.ExpectedIncome==0 && g.Shipping.State.LastSettlementDay==0 && g.DailyReports.State.Last==null,"New game clears shipment and report history");
                    File.WriteAllText("Logs/ShippingVerification.txt","PASS: packed AA, MVC, locks, range, quote, retrieval, save retry, overnight report, no duplicate payout, migration, corruption, cleanup and new game.\n");
                    Phase(12); EditorApplication.isPlaying=false;
                }
            }
            catch(Exception error) { Debug.LogException(error); SessionState.SetBool(Key,false); EditorApplication.Exit(1); }
        }
        static void jsonWrite(GameManager g,SaveData data) { var json=JsonUtility.ToJson(data,true); File.WriteAllText(g.Saves.SavePath,json); File.WriteAllText(g.Saves.SavePath+".bak",json); }
    }
}
