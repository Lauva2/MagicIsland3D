using System;
using System.IO;
using System.Linq;
using MagicIsland.Animals;
using MagicIsland.Farming;
using MagicIsland.Inventory;
using MagicIsland.Managers;
using MagicIsland.Reports;
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
    public static class DailyReportFlowVerification
    {
        const string Key = "MagicIsland.DailyReportVerification";
        static DailyReportFlowVerification() { if (SessionState.GetBool(Key, false)) EditorApplication.update += Tick; }
        public static void Run()
        {
            PrototypeAddressablesSetup.EnsureSetup(); PrototypeAddressablesSetup.ValidateUiPrefabs();
            string path = "Assets/Art/Prefabs/UI/DailyReportPanel.prefab"; byte[] original = File.ReadAllBytes(path); PrototypeAddressablesSetup.EnsureSetup();
            Require(original.SequenceEqual(File.ReadAllBytes(path)), "Repeated setup preserves complete report prefab");
            EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity"); PrototypeAddressablesSetup.BuildContent();
            var settings = AddressableAssetSettingsDefaultObject.Settings; settings.ActivePlayModeDataBuilderIndex = settings.DataBuilders.FindIndex(b => b is BuildScriptPackedPlayMode);
            SessionState.SetBool(Key, true); Phase(0); SessionState.SetFloat(Key + ".Start", (float)EditorApplication.timeSinceStartup);
            EditorApplication.update -= Tick; EditorApplication.update += Tick; EditorApplication.isPlaying = true;
        }
        static void Phase(int p) { SessionState.SetInt(Key + ".Phase", p); Debug.Log("Report verification phase " + p); }
        static void Require(bool ok, string why) { if (!ok) throw new Exception(why); }
        static DailyReportView Report => UnityEngine.Object.FindObjectOfType<DailyReportView>(true);
        static CoopView Coop => UnityEngine.Object.FindObjectOfType<CoopView>();
        static AnimalShopView Animals => UnityEngine.Object.FindObjectOfType<AnimalShopView>();
        static ShopView Shop => UnityEngine.Object.FindObjectOfType<ShopView>();
        static OrderView Order => UnityEngine.Object.FindObjectOfType<OrderView>();
        static SleepView Sleep => UnityEngine.Object.FindObjectOfType<SleepView>();
        static int Income => SessionState.GetInt(Key + ".Income", 0);
        static int Expense => SessionState.GetInt(Key + ".Expense", 0);
        static void AddIncome(int value) => SessionState.SetInt(Key + ".Income", Income + value);
        static void AddExpense(int value) => SessionState.SetInt(Key + ".Expense", Expense + value);
        static void Office(GameManager g) => g.Players.RestorePosition(g.Orders.Board.transform.position + new Vector3(0, .1f, -1.5f));
        static void Market(GameManager g) => g.Players.RestorePosition(g.Shop.Counter.position + new Vector3(0, .1f, -1.5f));
        static void Care(GameManager g) => g.Players.RestorePosition(g.Animals.Coop.transform.position + new Vector3(0, .1f, -1.7f));
        static void SaveDirectory(GameManager g, string dir) => g.Saves.Initialize(g.Configuration.Config, g.Configuration.Items, g.Configuration.Crops, dir,
            g.Configuration.Tasks, g.Configuration.Orders, g.Configuration.Seasons, g.Configuration.Gathering, g.Configuration.ToolUpgrades, g.Configuration.Storage,
            g.Configuration.NPCs, g.Configuration.Relationships, g.Configuration.Stories, g.Configuration.NPCSchedules, g.Configuration.Fishing, g.Configuration.Crafting);
        static void TextCheck(Component root)
        { foreach (var t in root.GetComponentsInChildren<TMPro.TMP_Text>(true)) { t.ForceMeshUpdate(true); Require(!t.isTextOverflowing && t.text.Where(c => !char.IsControl(c)).All(c => t.font.HasCharacter(c)), "Report TMP: " + t.name + "=" + t.text); } }
        static void Capture(GameManager g, string name)
        { g.Players.Player.ViewCamera.GetComponent<MagicIsland.CameraSystem.CameraController>().SnapToTarget(); g.UI.RefreshViews(); Canvas.ForceUpdateCanvases(); GameUiPreview.CaptureInventory(g.Players.Player.ViewCamera); File.Copy("Logs/InventoryPreview.png", "Logs/" + name + ".png", true); }
        static void Write(GameManager g, string json) { File.WriteAllText(g.Saves.SavePath, json); File.WriteAllText(g.Saves.SavePath + ".bak", json); }
        static void CheckFirstSummary(GameManager g)
        {
            var s = g.DailyReports.State; var last = s.Last;
            Require(s.Day == 2 && s.Pending && last.Day == 1 && last.Activity.Income == Income.ToString() && last.Activity.Expense == Expense.ToString() && last.Activity.Harvested == "1", "Exact first-day successful transaction and harvest summary");
            Require(last.NewMatureCrops == 1 && last.Eggs == 1 && last.QualityEggs == 1 && g.Animals.State.Eggs == 3 && g.Animals.State.QualityEggs == 2, "Report contains only new production, keeps existing pending eggs");
            Require(g.UI.HasDailyReport && Report.Layer == UILayer.PopUp && Report.transform.parent == g.UI.GetLayerRoot(UILayer.PopUp) && !g.Players.Player.enabled && !g.Interactions.Controller.enabled, "AA MVC PopUp pauses input");
        }
        static void Tick()
        {
            if (!SessionState.GetBool(Key, false)) return; int p = SessionState.GetInt(Key + ".Phase", 0);
            if (p == 22 && !EditorApplication.isPlaying) { SessionState.SetBool(Key, false); EditorApplication.Exit(0); return; }
            try
            {
                if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + ".Start", 0) > 240) throw new Exception("Report timeout phase " + p);
                if (!EditorApplication.isPlaying) return; var g = GameManager.Instance; if (g == null) return; Require(g.State != GameFlowState.Failed, "Report boot failure");
                string dir = SessionState.GetString(Key + ".Dir", "");
                if (p == 0 && g.State == GameFlowState.MainMenu)
                {
                    dir = Path.GetFullPath("Logs/DailyReportSaves/" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir); SessionState.SetString(Key + ".Dir", dir);
                    SessionState.SetInt(Key + ".Income", 0); SessionState.SetInt(Key + ".Expense", 0); SaveDirectory(g, dir); g.EnterGame(); Phase(1);
                }
                else if (p == 1 && g.State == GameFlowState.Playing)
                {
                    Require(Report != null && !g.UI.HasDailyReport && !g.DailyReports.State.Pending && g.DailyReports.State.Last == null, "AA report preloaded and hidden on new game");
                    g.Economy.Restore(1000); Require(g.DailyReports.State.Capture().Today.Income == "0", "Balance restoration excluded from income");
                    for (int i = 0; i < 4; i++) { var c = g.Farm.State.Cells[i]; c.Tilled = c.Planted = true; c.CropId = g.Farm.State.DefaultCropId; c.Growth = i == 0 || i == 2 ? c.RequiredGrowthDays : c.RequiredGrowthDays - 1; c.Watered = i != 3; }
                    Action observer = () => Require(g.DailyReports.State.Capture().Today.Harvested == "1", "Harvest recorded before bag observers");
                    g.Inventory.State.Changed += observer; Require(g.Farm.TryAct(0, FarmTool.Hoe, out _), "Actual successful harvest"); g.Inventory.State.Changed -= observer;
                    for (int i = 0; i < g.Configuration.Tasks.Tasks[0].Required; i++) g.Farm.TryAct(4 + i, FarmTool.Hoe, out _);
                    Require(g.Tasks.State.Ready, "Prepare real first task reward"); g.ClaimTaskReward(); AddIncome(g.Configuration.Tasks.Tasks[0].GoldReward);
                    g.Inventory.TryAdd("wood", 15); g.Inventory.TryAdd("stone", 5); g.Inventory.TryAdd("animal_feed", 2); Market(g); g.OpenShop(); Phase(2);
                }
                else if (p == 2 && Shop != null)
                {
                    Shop.BuyButton.onClick.Invoke(); AddExpense(g.Shop.Seed.BuyPrice); Shop.SellButton.onClick.Invoke(); AddIncome(g.Shop.Crop.SellPrice); Shop.SellButton.onClick.Invoke();
                    Require(g.DailyReports.State.Capture().Today.Income == Income.ToString() && g.DailyReports.State.Capture().Today.Expense == Expense.ToString(), "Actual seed purchase, crop sale, task reward and failed sale counted correctly");
                    g.CloseShop(); Office(g); g.OpenCoop(true); Phase(3);
                }
                else if (p == 3 && Coop != null)
                { Coop.BuildButton.onClick.Invoke(); AddExpense(g.Configuration.Animals.BuildGold); Require(g.Animals.State.Built, "Build coop transaction"); g.CloseAnimalPanel(); Market(g); g.OpenAnimalShop(); Phase(4); }
                else if (p == 4 && Animals != null)
                {
                    Animals.ChickenButton.onClick.Invoke(); Animals.ChickenButton.onClick.Invoke(); AddExpense(2 * g.Configuration.Animals.ChickenPrice);
                    Animals.FeedButton.onClick.Invoke(); Animals.FeedButton.onClick.Invoke(); AddExpense(2 * g.Configuration.Animals.FeedPrice);
                    g.Inventory.TryAdd("egg", 1); g.Inventory.TryAdd("quality_egg", 1); Animals.SellButton.onClick.Invoke(); Animals.QualitySellButton.onClick.Invoke(); AddIncome(g.Configuration.Animals.EggPrice + g.Configuration.Animals.QualityEggPrice);
                    var saved = g.Animals.State.Capture(); saved.Eggs = 2; saved.QualityEggs = 1; saved.Chickens[0].Affinity = 68;
                    for (int seed = 1; seed < 1000; seed++) { saved.QualitySeed = seed; var probe = new AnimalState(g.Animals.State.Definition); probe.Restore(saved); var bag = new InventoryState(1, g.Configuration.Items.Definitions()); bag.TryAdd("animal_feed", 2); probe.Feed(bag); probe.NextDay(2); if (probe.QualityEggs == 2) break; }
                    g.Animals.State.Restore(saved); g.CloseAnimalPanel(); Care(g); g.OpenCoop(); Phase(5);
                }
                else if (p == 5 && Coop != null)
                { Coop.FeedButton.onClick.Invoke(); g.CloseAnimalPanel(); g.Inventory.TryAdd(g.Orders.State.ItemId, g.Orders.State.Quantity); Office(g); g.OpenOrder(); Phase(6); }
                else if (p == 6 && Order != null)
                {
                    int reward = (int)g.Orders.State.Reward; Order.DeliverButton.onClick.Invoke(); Require(g.Orders.State.Fulfilled, "Actual daily order delivery"); AddIncome(reward); Order.DeliverButton.onClick.Invoke();
                    Require(g.DailyReports.State.Capture().Today.Income == Income.ToString() && g.DailyReports.State.Capture().Today.Expense == Expense.ToString(), "All actual trade, building, livestock and order silent payments tracked exactly once");
                    g.CloseOrder(); g.ReturnToMainMenu(); Phase(7);
                }
                else if (p == 7 && g.State == GameFlowState.MainMenu) { Require(g.Saves.TryLoad(out _), "Midday save loads: " + g.Saves.LastError); g.ContinueGame(); Phase(8); }
                else if (p == 8 && g.State == GameFlowState.Playing)
                {
                    Require(!g.UI.HasDailyReport && g.DailyReports.State.Capture().Today.Income == Income.ToString() && g.DailyReports.State.Capture().Today.Expense == Expense.ToString() && g.DailyReports.State.Capture().Today.Harvested == "1", "Midday save/resume preserves active ledger without counting restored gold or bag");
                    g.Players.RestorePosition(g.Housing.Entrance.Position + new Vector3(0, .1f, -1.4f)); g.EnterHome(); Phase(9);
                }
                else if (p == 9 && g.State == GameFlowState.Playing && g.Housing.Inside)
                { g.Players.RestorePosition(g.Configuration.Housing.InteriorOrigin + new Vector3(-1.5f, .1f, 2)); g.RequestRest(); Phase(10); }
                else if (p == 10 && Sleep != null)
                {
                    string blocked = Path.Combine(dir, "blocked"); File.WriteAllText(blocked, "file"); SaveDirectory(g, blocked);
                    Sleep.ConfirmButton.onClick.Invoke(); g.ConfirmSleep(); CheckFirstSummary(g); Require(g.DailyReports.Feedback.Contains("保存失败"), "Settlement save failure retains report"); TextCheck(Report); Capture(g, "DailyReportPreview");
                    long gold = g.Economy.Gold; double minute = g.Time.Clock.Minute;
                    g.AdvanceDay(); g.ToggleInventory(); g.OpenShop(); g.ClaimTaskReward(); g.OpenCoop(); g.ExitHome(); g.CloseSleep();
                    Require(g.Farm.State.Day == 2 && g.Economy.Gold == gold && !g.UI.IsInventoryVisible && g.Housing.Inside && g.UI.HasDailyReport && !g.Players.Player.enabled, "Pending report blocks date, rewards, bag, shops, residence and input restoration");
                    g.Time.Tick(30, g.UI.HasDailyReport); Require(g.Time.Clock.Minute == minute && g.Stamina.State.Current == g.Configuration.Config.MaxStamina, "Report pauses morning clock after refill");
                    Report.ContinueButton.onClick.Invoke(); Require(g.DailyReports.State.Pending && g.UI.HasDailyReport && g.Farm.State.Day == 2, "Acknowledgement save failure keeps same report pending");
                    SaveDirectory(g, dir); Report.SaveButton.onClick.Invoke(); Require(g.Saves.TryLoad(out var saved) && saved.DailyReport.Pending && saved.DailyReport.Last.Activity.Income == Income.ToString() && saved.Day == 2, "Report save retry captures whole settlement once");
                    SessionState.SetString(Key + ".Report", JsonUtility.ToJson(g.DailyReports.State.Capture())); Report.ReturnMenuButton.onClick.Invoke(); Phase(11);
                }
                else if (p == 11 && g.State == GameFlowState.MainMenu)
                { Require(Report == null && g.DailyReports.State == null, "Report cached AA instance and tracking cleared on menu"); g.ContinueGame(); Phase(12); }
                else if (p == 12 && g.State == GameFlowState.Playing)
                {
                    CheckFirstSummary(g); Require(JsonUtility.ToJson(g.DailyReports.State.Capture()) == SessionState.GetString(Key + ".Report", ""), "Pending report resumes exact saved data in correct residence"); TextCheck(Report); Capture(g, "DailyReportResumePreview");
                    long gold = g.Economy.Gold; Report.ContinueButton.onClick.Invoke(); Report.ContinueButton.onClick.Invoke();
                    Require(!g.DailyReports.State.Pending && !g.UI.HasDailyReport && g.Players.Player.enabled && g.Farm.State.Day == 2 && g.Economy.Gold == gold && g.Saves.TryLoad(out var saved) && !saved.DailyReport.Pending, "Continue only acknowledges and saves, never repeats day or money");
                    g.Economy.TryEarn(60); g.Economy.TrySpend(7); Require(g.DailyReports.State.Capture().Today.Income == "60" && g.DailyReports.State.Capture().Today.Expense == "7", "Fresh morning ledger starts independently");
                    g.RequestRest(); Phase(13);
                }
                else if (p == 13 && Sleep != null)
                {
                    Sleep.ConfirmButton.onClick.Invoke(); var last = g.DailyReports.State.Last;
                    Require(last.Day == 2 && last.Activity.Income == "60" && last.Activity.Expense == "7" && last.Activity.Harvested == "0" && last.Eggs == 0 && last.QualityEggs == 0 && g.Animals.State.TotalEggs == 5, "Next report excludes prior income, harvest and old pending eggs");
                    Require(Report.NetText.text.Contains("+53"), "Positive net formatted"); TextCheck(Report); Capture(g, "DailyReportPositivePreview"); Report.ContinueButton.onClick.Invoke(); g.ReturnToMainMenu(); Phase(14);
                }
                else if (p == 14 && g.State == GameFlowState.MainMenu)
                {
                    Require(g.Saves.TryLoad(out var saved) && saved.Version == SaveData.CurrentVersion && saved.Day == 3, "Current report save loads"); string good = JsonUtility.ToJson(saved);
                    saved.DailyReport.Today.Income = "-1"; Write(g, JsonUtility.ToJson(saved)); Require(!g.Saves.TryLoad(out _), "Negative ledger rejected before world");
                    saved = JsonUtility.FromJson<SaveData>(good); saved.DailyReport.Last.Day = 3; Write(g, JsonUtility.ToJson(saved)); Require(!g.Saves.TryLoad(out _), "Wrong completed date rejected");
                    saved = JsonUtility.FromJson<SaveData>(good); saved.DailyReport.Pending = true; saved.DailyReport.Today.Harvested = "1"; Write(g, JsonUtility.ToJson(saved)); Require(!g.Saves.TryLoad(out _), "Pending report with active-day activity rejected");
                    saved = JsonUtility.FromJson<SaveData>(good); saved.Version = 23; string json = JsonUtility.ToJson(saved), field = "\"DailyReport\":" + JsonUtility.ToJson(saved.DailyReport) + ","; Require(json.Contains(field), "Real daily report JSON field located"); Write(g, json.Replace(field, ""));
                    Require(g.Saves.TryLoad(out var old) && old.Version == SaveData.CurrentVersion && old.DailyReport.Day == 3 && !old.DailyReport.Pending && old.DailyReport.Last.Day == 0 && old.DailyReport.Today.Income == "0" && old.Gold == saved.Gold && old.Animals.QualityEggs == 2 && old.Animals.Chickens[0].Affinity == 70, "Real v23 missing-field migration preserves gold and quality husbandry without invented daily activity"); g.ContinueGame(); Phase(15);
                }
                else if (p == 15 && g.State == GameFlowState.Playing)
                { Require(!g.UI.HasDailyReport && g.Players.Player.enabled && g.DailyReports.State.Day == 3 && g.DailyReports.State.Last == null, "Legacy game playable with hidden report"); g.ReturnToMainMenu(); Phase(16); }
                else if (p == 16 && g.State == GameFlowState.MainMenu) { g.EnterGame(); Phase(17); }
                else if (p == 17 && g.State == GameFlowState.Playing)
                {
                    Require(g.DailyReports.State.Day == 1 && g.DailyReports.State.Capture().Today.Income == "0" && !g.DailyReports.State.Pending && !g.UI.HasDailyReport && g.Animals.State.TotalEggs == 0, "New game resets ledger and pending report");
                    g.AdvanceDay(); var last = g.DailyReports.State.Last; Require(last.Activity.Income == "0" && last.Activity.Expense == "0" && last.Activity.Harvested == "0" && last.NewMatureCrops == 0 && last.Eggs == 0 && last.QualityEggs == 0, "Empty day report has no fabricated activity"); TextCheck(Report); Capture(g, "DailyReportEmptyPreview");
                    Report.ContinueButton.onClick.Invoke(); g.ReturnToMainMenu(); Phase(18);
                }
                else if (p == 18 && g.State == GameFlowState.MainMenu)
                { g.ContinueGame(); Phase(19); }
                else if (p == 19 && g.State == GameFlowState.Playing)
                { Require(!g.UI.HasDailyReport && !g.DailyReports.State.Pending && g.DailyReports.State.Last.Day == 1, "Acknowledged report never reopens on read"); g.ReturnToMainMenu(); Phase(20); }
                else if (p == 20 && g.State == GameFlowState.MainMenu)
                { File.WriteAllText("Logs/DailyReportVerification.txt", "PASS: packed AA hidden preload and MVC PopUp; actual seed/crop/task/order/coop/chicken/feed/two-egg transactions and harvest before observers; midday reload, mature and production deltas, indoor sleep, paused clock/input/modal/reentry, settlement/ack save failure and retry, pending report/menu/reload exactness, acknowledge once without advancing or rewards, independent next ledger, empty day, real missing-field v23 migration and corruption rejection, cleanup/new game and acknowledged-read behavior."); Debug.Log("Daily report flow verification PASSED."); Phase(22); EditorApplication.isPlaying = false; }
            }
            catch (Exception e) { Debug.LogException(e); SessionState.SetBool(Key, false); EditorApplication.Exit(1); }
        }
    }
}
