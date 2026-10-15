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
    public static class InventoryUpgradeFlowVerification
    {
        const string Key = "MagicIsland.InventoryUpgradeVerification";
        static InventoryState originalBag;
        static InventoryUpgradeFlowVerification() { if (SessionState.GetBool(Key, false)) EditorApplication.update += Tick; }
        public static void Run()
        {
            PrototypeAddressablesSetup.EnsureSetup(); PrototypeAddressablesSetup.ValidateUiPrefabs();
            var paths = new[] { "Assets/Config/InventoryUpgradeConfig.asset", "Assets/Config/GameConfig.asset", "Assets/Art/Prefabs/UI/InventoryUpgradePanel.prefab", "Assets/Art/Prefabs/UI/InventoryPanel.prefab", "Assets/Art/Prefabs/UI/GiftPanel.prefab", "Assets/Art/Prefabs/UI/StoragePanel.prefab", "Assets/Art/Prefabs/UI/ShopPanel.prefab" };
            var bytes = paths.Select(File.ReadAllBytes).ToArray(); PrototypeAddressablesSetup.EnsureSetup();
            for (int i = 0; i < paths.Length; i++) Require(bytes[i].SequenceEqual(File.ReadAllBytes(paths[i])), "Complete backpack resources preserved on repeated setup");
            EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity"); PrototypeAddressablesSetup.BuildContent();
            var settings = AddressableAssetSettingsDefaultObject.Settings; settings.ActivePlayModeDataBuilderIndex = settings.DataBuilders.FindIndex(b => b is BuildScriptPackedPlayMode);
            SessionState.SetBool(Key, true); Phase(0); SessionState.SetFloat(Key + ".Start", (float)EditorApplication.timeSinceStartup);
            EditorApplication.update -= Tick; EditorApplication.update += Tick; EditorApplication.isPlaying = true;
        }
        static void Phase(int p) { SessionState.SetInt(Key + ".Phase", p); Debug.Log("Backpack verification phase " + p); }
        static void Require(bool ok, string why) { if (!ok) throw new Exception(why); }
        static InventoryUpgradeView Upgrade => UnityEngine.Object.FindObjectOfType<InventoryUpgradeView>();
        static InventoryView Bag => UnityEngine.Object.FindObjectOfType<InventoryView>(true);
        static ShopView Shop => UnityEngine.Object.FindObjectOfType<ShopView>();
        static StorageView Chest => UnityEngine.Object.FindObjectOfType<StorageView>();
        static void Market(GameManager g) => g.Players.RestorePosition(g.Shop.Counter.position + new Vector3(0, .1f, -1.5f));
        static void SaveDirectory(GameManager g, string dir) => g.Saves.Initialize(g.Configuration.Config, g.Configuration.Items, g.Configuration.Crops, dir,
            g.Configuration.Tasks, g.Configuration.Orders, g.Configuration.Seasons, g.Configuration.Gathering, g.Configuration.ToolUpgrades, g.Configuration.Storage,
            g.Configuration.NPCs, g.Configuration.Relationships, g.Configuration.Stories, g.Configuration.NPCSchedules, g.Configuration.Fishing, g.Configuration.Crafting);
        static void TextCheck(Component root)
        { foreach (var t in root.GetComponentsInChildren<TMPro.TMP_Text>(true)) { t.ForceMeshUpdate(true); Require(!t.isTextOverflowing && t.text.Where(c => !char.IsControl(c)).All(c => t.font.HasCharacter(c)), "Backpack TMP: " + t.name + "=" + t.text); } }
        static void Capture(GameManager g, string name)
        { g.Players.Player.ViewCamera.GetComponent<MagicIsland.CameraSystem.CameraController>().SnapToTarget(); g.UI.RefreshViews(); Canvas.ForceUpdateCanvases(); GameUiPreview.CaptureInventory(g.Players.Player.ViewCamera); File.Copy("Logs/InventoryPreview.png", "Logs/" + name + ".png", true); }
        static void Write(GameManager g, string json) { File.WriteAllText(g.Saves.SavePath, json); File.WriteAllText(g.Saves.SavePath + ".bak", json); }
        static void Tick()
        {
            if (!SessionState.GetBool(Key, false)) return; int p = SessionState.GetInt(Key + ".Phase", 0);
            if (p == 20 && !EditorApplication.isPlaying) { SessionState.SetBool(Key, false); EditorApplication.Exit(0); return; }
            try
            {
                if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + ".Start", 0) > 240) throw new Exception("Backpack timeout phase " + p);
                if (!EditorApplication.isPlaying) return; var g = GameManager.Instance; if (g == null) return; Require(g.State != GameFlowState.Failed, "Backpack boot failure");
                string dir = SessionState.GetString(Key + ".Dir", "");
                if (p == 0 && g.State == GameFlowState.MainMenu)
                {
                    Require(g.Configuration.Config.InventorySlots == 10 && g.Configuration.InventoryUpgrades.IsValid && g.Configuration.InventoryUpgrades.GoldCosts.SequenceEqual(new[] { 300, 800 }), "AA default ten slots and two costs");
                    dir = Path.GetFullPath("Logs/BackpackSaves/" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir); SessionState.SetString(Key + ".Dir", dir); SaveDirectory(g, dir); g.EnterGame(); Phase(1);
                }
                else if (p == 1 && g.State == GameFlowState.Playing)
                {
                    Require(g.Inventory.State.Capacity == 10 && g.Inventory.UpgradeState.Level == 0 && Bag.Slots.Length == InventoryView.DisplaySlots, "New-game capacity and thirty physical UI slots"); originalBag = g.Inventory.State;
                    var full = Enumerable.Repeat(new InventorySlot("wood", 99), 10).ToArray(); full[1] = default; full[9] = new InventorySlot("quality_egg", 2); g.Inventory.State.Restore(full); Bag.Slots[9].SelectButton.onClick.Invoke();
                    g.Economy.Restore(299); Market(g); g.OpenShop(); Phase(2);
                }
                else if (p == 2 && Shop != null) { Require(Shop.InventoryUpgradeButton != null, "Actual shop entry wired"); Shop.InventoryUpgradeButton.onClick.Invoke(); Phase(3); }
                else if (p == 3 && Upgrade != null)
                {
                    Require(!Upgrade.UpgradeButton.interactable && Upgrade.CapacityText.text.Contains("10") && Upgrade.NextText.text.Contains("20") && Upgrade.CostText.text.Contains("300"), "First offer and insufficient gold state");
                    Upgrade.UpgradeButton.onClick.Invoke(); Require(g.Economy.Gold == 299 && g.Inventory.State.Capacity == 10 && g.Inventory.UpgradeState.Level == 0, "Insufficient upgrade pays nothing"); TextCheck(Upgrade);
                    var position = g.Players.Player.transform.position; g.Economy.Restore(1100); g.Players.RestorePosition(new Vector3(0, .1f, -4)); g.UpgradeInventory(); Require(g.Inventory.State.Capacity == 10, "Upgrade range guard"); g.Players.RestorePosition(position);
                    string blocked = Path.Combine(dir, "blocked"); File.WriteAllText(blocked, "file"); SaveDirectory(g, blocked);
                    Action observer = () => { Require(g.Inventory.State.Capacity == 20 && g.Inventory.UpgradeState.Level == 1 && g.Economy.Gold == 800 && g.DailyReports.State.Capture().Today.Expense == "300", "Capacity, level, cost and report committed before bag observers"); g.UpgradeInventory(); g.CloseInventoryUpgrade(); g.ReturnToMainMenu(); g.AdvanceDay(); g.ToggleInventory(); };
                    g.Inventory.State.Changed += observer; Upgrade.UpgradeButton.onClick.Invoke(); g.Inventory.State.Changed -= observer;
                    Require(object.ReferenceEquals(originalBag, g.Inventory.State) && g.UI.HasInventoryUpgrade && g.State == GameFlowState.Playing && g.Inventory.UpgradeFeedback.Contains("保存失败") && g.Farm.State.Day == 1 && !g.UI.IsInventoryVisible, "In-place upgrade, reentry and failed save retained");
                    Require(g.Inventory.State.Slots[0].Quantity == 99 && g.Inventory.State.Slots[1].Empty && g.Inventory.State.Slots[9].ItemId == "quality_egg" && g.Inventory.State.Slots.Skip(10).All(s => s.Empty) && Bag.DetailTitleText.text == "优质鸡蛋", "Old items, holes, selection and order unchanged");
                    SaveDirectory(g, dir); Upgrade.SaveButton.onClick.Invoke(); Require(g.Saves.TryLoad(out var saved) && saved.InventoryUpgrade.Level == 1 && saved.InventoryUpgrade.BaseSlots == 10 && saved.Slots.Length == 20 && saved.Gold == 800, "Upgrade retry saves complete level once"); TextCheck(Upgrade); Capture(g, "BackpackUpgradePreview");
                    g.CloseInventoryUpgrade(); g.ToggleInventory(); Phase(4);
                }
                else if (p == 4 && g.UI.IsInventoryVisible)
                {
                    Require(Bag.Slots.Count(s => s.gameObject.activeSelf) == 30 && Bag.Slots.Count(s => s.gameObject.activeSelf && s.SelectButton.interactable) == 20 && Bag.CapacityText.text.Contains("20"), "Cached inventory controller unlocks second row");
                    var slots = g.Inventory.State.Slots.ToArray(); slots[19] = new InventorySlot("turnip", 5); g.Inventory.State.Restore(slots); Bag.Slots[19].SelectButton.onClick.Invoke(); Require(Bag.DetailTitleText.text == "芜菁" && Bag.DetailText.text.Contains("数量 5"), "New slot selectable through actual cached binding");
                    g.CloseInventory(); Market(g); g.OpenShop(); Phase(5);
                }
                else if (p == 5 && Shop != null)
                {
                    Shop.SellButton.onClick.Invoke(); Require(g.Inventory.State.Slots[19].Quantity == 4 && g.DailyReports.State.Capture().Today.Income == g.Shop.Crop.SellPrice.ToString(), "Shop transactions keep expanded bag reference");
                    g.CloseShop(); g.Players.RestorePosition(g.Storage.Chest.transform.position + new Vector3(0, .1f, -1.5f)); g.OpenStorage(); Phase(6);
                }
                else if (p == 6 && Chest != null)
                {
                    Require(Chest.BagSlots.Count(s => s.gameObject.activeSelf) == 30 && Chest.BagSlots.Count(s => s.SelectButton.interactable) == 20, "Storage shows second unlocked row"); Chest.BagSlots[19].SelectButton.onClick.Invoke(); Chest.MoveStackButton.onClick.Invoke();
                    Require(g.Inventory.State.Slots[19].Empty && g.Storage.State.Contents.GetCount("turnip") == 4, "Existing storage state transfers new slot correctly"); g.CloseStorage(); Market(g); g.OpenInventoryUpgrade(); Phase(7);
                }
                else if (p == 7 && Upgrade != null)
                {
                    Require(Upgrade.NextText.text.Contains("30") && Upgrade.CostText.text.Contains("800"), "Second offer adds ten"); Upgrade.UpgradeButton.onClick.Invoke(); Require(g.Inventory.State.Capacity == 30 && g.Inventory.UpgradeState.Level == 2 && g.DailyReports.State.Capture().Today.Expense == "1100" && object.ReferenceEquals(originalBag, g.Inventory.State), "Second in-place upgrade and exact total expense");
                    long gold = g.Economy.Gold; Upgrade.UpgradeButton.onClick.Invoke(); Require(g.Economy.Gold == gold && !Upgrade.UpgradeButton.interactable && Upgrade.CapacityText.text.Contains("30"), "Max upgrade cannot charge again"); TextCheck(Upgrade);
                    g.CloseInventoryUpgrade(); var slots = g.Inventory.State.Slots.ToArray(); slots[29] = new InventorySlot("turnip_soup", 2); g.Inventory.State.Restore(slots); g.Stamina.Restore(50); g.ToggleInventory(); Phase(8);
                }
                else if (p == 8 && g.UI.IsInventoryVisible)
                {
                    Require(Bag.Slots.Count(s => s.gameObject.activeSelf) == 30, "Thirty active slots"); Bag.Slots[29].SelectButton.onClick.Invoke(); Bag.UseFoodButton.onClick.Invoke(); Require(g.Inventory.State.Slots[29].Quantity == 1 && g.Stamina.State.Current == 80, "Food use and cached selection work in last added slot");
                    Bag.GetComponentInChildren<UnityEngine.UI.ScrollRect>().verticalNormalizedPosition = 0; g.UI.RefreshViews(); TextCheck(Bag); Capture(g, "ThirtySlotBackpackPreview"); g.CloseInventory(); g.ReturnToMainMenu(); Phase(9);
                }
                else if (p == 9 && g.State == GameFlowState.MainMenu) { Require(g.Inventory.State == null && !g.UI.HasInventoryUpgrade, "Cleanup expanded inventory and upgrade panel"); g.ContinueGame(); Phase(10); }
                else if (p == 10 && g.State == GameFlowState.Playing)
                {
                    Require(g.Inventory.State.Capacity == 30 && g.Inventory.UpgradeState.BaseSlots == 10 && g.Inventory.UpgradeState.Level == 2 && g.Inventory.State.Slots[29].Quantity == 1 && g.Storage.State.Contents.GetCount("turnip") == 4 && g.DailyReports.State.Capture().Today.Expense == "1100", "Expanded save restores items, indices, storage and report");
                    g.AdvanceDay(); Require(g.DailyReports.State.Last.Activity.Expense == "1100", "Upgrade payments appear in actual daily report"); g.StartNewDayFromReport(); g.ReturnToMainMenu(); Phase(11);
                }
                else if (p == 11 && g.State == GameFlowState.MainMenu)
                {
                    Require(g.Saves.TryLoad(out var saved), "Expanded save valid"); string good = JsonUtility.ToJson(saved);
                    saved.InventoryUpgrade.Level = 3; Write(g, JsonUtility.ToJson(saved)); Require(!g.Saves.TryLoad(out _), "Invalid level rejected");
                    saved = JsonUtility.FromJson<SaveData>(good); saved.InventoryUpgrade.BaseSlots = 11; Write(g, JsonUtility.ToJson(saved)); Require(!g.Saves.TryLoad(out _), "Capacity mismatch rejected");
                    saved = JsonUtility.FromJson<SaveData>(good); saved.Version = 24; saved.Slots = saved.Slots.Take(12).ToArray(); saved.Slots[11] = new SavedSlot { ItemId = "turnip_soup", Quantity = 1 };
                    string json = JsonUtility.ToJson(saved), field = "\"InventoryUpgrade\":" + JsonUtility.ToJson(saved.InventoryUpgrade) + ","; Require(json.Contains(field), "Locate real inventory-upgrade field"); Write(g, json.Replace(field, ""));
                    Require(g.Saves.TryLoad(out var old) && old.Version == SaveData.CurrentVersion && old.InventoryUpgrade.BaseSlots == 12 && old.InventoryUpgrade.Level == 0 && old.Slots.Length == 12 && old.Slots[9].Quantity == 2 && old.Slots[11].ItemId == "turnip_soup" && old.Gold == saved.Gold && old.DailyReport.Last.Activity.Expense == "1100", "Real v24 missing-field legacy migration retains twelve slots, exact items, gold and report"); g.ContinueGame(); Phase(12);
                }
                else if (p == 12 && g.State == GameFlowState.Playing)
                { Require(g.Inventory.State.Capacity == 12 && g.Inventory.State.Slots[11].Quantity == 1, "Legacy loaded without shrinking to new default"); g.Economy.Restore(300); Market(g); g.OpenInventoryUpgrade(); Phase(13); }
                else if (p == 13 && Upgrade != null)
                { Require(Upgrade.NextText.text.Contains("22"), "Legacy next capacity still adds ten"); Upgrade.UpgradeButton.onClick.Invoke(); Require(g.Inventory.State.Capacity == 22 && g.Inventory.State.Slots[11].Quantity == 1 && g.Inventory.UpgradeState.BaseSlots == 12, "Legacy upgrade preserves old base and slots"); g.CloseInventoryUpgrade(); g.ReturnToMainMenu(); Phase(14); }
                else if (p == 14 && g.State == GameFlowState.MainMenu) { g.EnterGame(); Phase(15); }
                else if (p == 15 && g.State == GameFlowState.Playing)
                {
                    Require(g.Inventory.State.Capacity == 10 && g.Inventory.UpgradeState.Level == 0 && g.Inventory.GetCount("turnip_soup") == 0 && g.DailyReports.State.Capture().Today.Expense == "0", "New game resets to ten and zero levels/expense");
                    File.WriteAllText("Logs/InventoryUpgradeVerification.txt", "PASS: packed AA ten-slot default and 300/800 costs, shop entry and MVC PopUp, 10/20/30 growth in place, insufficient gold/range/modal/reentry, exact items/holes/selection, save failure and retry, cached bag new-slot bindings, shop/storage/food using added slots, report expenses, current capacity-aware read, actual missing-field v24 twelve-slot migration and +10 legacy upgrade, corrupt level/capacity rejection, cleanup and new-game reset."); Debug.Log("Inventory upgrade flow verification PASSED."); Phase(20); EditorApplication.isPlaying = false;
                }
            }
            catch (Exception e) { Debug.LogException(e); SessionState.SetBool(Key, false); EditorApplication.Exit(1); }
        }
    }
}
