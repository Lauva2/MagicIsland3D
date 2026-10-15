using System;
using System.IO;
using System.Linq;
using MagicIsland.Inventory;
using MagicIsland.Managers;
using MagicIsland.UI.Views;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MagicIsland.EditorTools
{
    [InitializeOnLoad]
    public static class InventorySortFlowVerification
    {
        const string Key = "MagicIsland.InventorySortVerification";
        static InventorySlot[] savedSlots;
        static InventorySortFlowVerification() { if (SessionState.GetBool(Key, false)) EditorApplication.update += Tick; }
        public static void Run()
        {
            PrototypeAddressablesSetup.EnsureSetup(); PrototypeAddressablesSetup.ValidateUiPrefabs();
            const string path = "Assets/Art/Prefabs/UI/InventoryPanel.prefab";
            var bytes = File.ReadAllBytes(path); PrototypeAddressablesSetup.EnsureSetup();
            Require(bytes.SequenceEqual(File.ReadAllBytes(path)), "Repeated setup preserves complete edited prefab");
            EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity"); PrototypeAddressablesSetup.BuildContent();
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            settings.ActivePlayModeDataBuilderIndex = settings.DataBuilders.FindIndex(b => b is BuildScriptPackedPlayMode);
            SessionState.SetBool(Key, true); Phase(0); SessionState.SetFloat(Key + ".Start", (float)EditorApplication.timeSinceStartup);
            EditorApplication.update -= Tick; EditorApplication.update += Tick; EditorApplication.isPlaying = true;
        }
        static void Require(bool ok, string reason) { if (!ok) throw new Exception(reason); }
        static void Phase(int value) { SessionState.SetInt(Key + ".Phase", value); Debug.Log("Inventory sort phase " + value); }
        static InventoryView Bag => UnityEngine.Object.FindObjectOfType<InventoryView>(true);
        static void SaveDirectory(GameManager g, string dir) => g.Saves.Initialize(g.Configuration.Config, g.Configuration.Items, g.Configuration.Crops, dir,
            g.Configuration.Tasks, g.Configuration.Orders, g.Configuration.Seasons, g.Configuration.Gathering, g.Configuration.ToolUpgrades, g.Configuration.Storage,
            g.Configuration.NPCs, g.Configuration.Relationships, g.Configuration.Stories, g.Configuration.NPCSchedules, g.Configuration.Fishing, g.Configuration.Crafting);
        static InventorySlot[] Fixture(int capacity)
        {
            var values = new InventorySlot[capacity];
            values[0] = new InventorySlot("quality_egg", 2); values[2] = new InventorySlot("wood", 50);
            values[3] = new InventorySlot("egg", 3); values[4] = new InventorySlot("turnip_soup", 2);
            values[5] = new InventorySlot("wood", 60); values[6] = new InventorySlot("turnip_seed", 7);
            values[7] = new InventorySlot("wood_fence", 1); values[8] = new InventorySlot("wood", 10);
            return values;
        }
        static void CheckSorted(GameManager g)
        {
            var ids = g.Inventory.State.Slots.Where(s => !s.Empty).Select(s => s.ItemId);
            Require(ids.SequenceEqual(new[] { "turnip_seed", "wood", "wood", "turnip_soup", "wood_fence", "egg", "quality_egg" }), "Actual sort type order and distinct egg qualities");
            Require(g.Inventory.State.Slots[1].Quantity == 99 && g.Inventory.State.Slots[2].Quantity == 21, "Fragmented wood merged without loss");
            Require(Bag.DetailTitleText.text == "选择物品" && !Bag.UseSeedButton.interactable && !Bag.UseFoodButton.gameObject.activeSelf && !Bag.PlaceButton.gameObject.activeSelf, "Selected slot cleared before wrong-item actions");
        }
        static void Tick()
        {
            if (!SessionState.GetBool(Key, false)) return;
            int phase = SessionState.GetInt(Key + ".Phase", 0);
            if (phase == 9 && !EditorApplication.isPlaying) { SessionState.SetBool(Key, false); EditorApplication.Exit(0); return; }
            try
            {
                if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + ".Start", 0) > 180) throw new Exception("Sort timeout phase " + phase);
                if (!EditorApplication.isPlaying) return;
                var g = GameManager.Instance; if (g == null) return;
                Require(g.State != GameFlowState.Failed, "Game bootstrap failed");
                string dir = SessionState.GetString(Key + ".Dir", "");
                if (phase == 0 && g.State == GameFlowState.MainMenu)
                {
                    dir = Path.GetFullPath("Logs/InventorySortSaves/" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
                    SessionState.SetString(Key + ".Dir", dir); SaveDirectory(g, dir); g.EnterGame(); Phase(1);
                }
                else if (phase == 1 && g.State == GameFlowState.Playing)
                {
                    Require(Bag.SortButton != null && g.Inventory.State.Capacity == 10, "Packed AA sort button and default capacity");
                    g.Inventory.State.Restore(Fixture(10)); var before = g.Inventory.State.Slots.ToArray();
                    g.SortInventory(); Require(before.SequenceEqual(g.Inventory.State.Slots), "Hidden bag cannot sort");
                    g.Stamina.Restore(50); g.ToggleInventory(); Phase(2);
                }
                else if (phase == 2 && g.UI.IsInventoryVisible)
                {
                    Bag.Slots[4].SelectButton.onClick.Invoke(); Require(Bag.UseFoodButton.interactable, "Food selection before sort");
                    string report = JsonUtility.ToJson(g.DailyReports.State.Capture()); long gold = g.Economy.Gold; int day = g.Farm.State.Day;
                    var original = g.Inventory.State;
                    string blocked = Path.Combine(dir, "blocked"); File.WriteAllText(blocked, "file"); SaveDirectory(g, blocked);
                    uint version = original.ReorderVersion;
                    Action observer = () =>
                    {
                        CheckSorted(g); g.SortInventory(); g.UseFood(3); g.UseSeed("turnip_seed"); g.BeginPlacementFromBag(4);
                        g.CloseInventory(); g.ToggleInventory(); g.ReturnToMainMenu(); g.AdvanceDay();
                        Require(g.UI.IsInventoryVisible && g.State == GameFlowState.Playing && original.ReorderVersion == version + 1, "Sort commit blocks nested operations");
                    };
                    original.Changed += observer; Bag.SortButton.onClick.Invoke(); original.Changed -= observer;
                    Require(ReferenceEquals(original, g.Inventory.State) && g.Inventory.SortSavePending && Bag.FeedbackText.text.Contains("保存失败"), "Failed save retains in-place sort and retry feedback");
                    Require(g.Economy.Gold == gold && g.Farm.State.Day == day && g.Stamina.State.Current == 50 && report == JsonUtility.ToJson(g.DailyReports.State.Capture()), "Sorting costs nothing and records no economic or harvest event");
                    Bag.UseFoodButton.onClick.Invoke(); Bag.PlaceButton.onClick.Invoke(); Bag.UseSeedButton.onClick.Invoke();
                    Require(g.Stamina.State.Current == 50 && g.Inventory.GetCount("turnip_soup") == 2 && g.UI.IsInventoryVisible, "Cleared buttons cannot consume, place or close");
                    SaveDirectory(g, dir); Bag.SaveButton.onClick.Invoke();
                    Require(!g.Inventory.SortSavePending && g.Saves.TryLoad(out var saved) && saved.Slots[1].Quantity == 99 && saved.Slots[2].Quantity == 21 && saved.InventoryUpgrade.Level == 0, "Existing save button retries sorted slots without upgrading");
                    foreach (int index in new[] { 0, 4 }) { Bag.Slots[index].SelectButton.onClick.Invoke(); Bag.SortButton.onClick.Invoke(); CheckSorted(g); }
                    g.UI.RefreshViews(); Canvas.ForceUpdateCanvases();
                    foreach (var text in Bag.GetComponentsInChildren<TMPro.TMP_Text>(true))
                    { text.ForceMeshUpdate(true); Require(!text.isTextOverflowing && text.text.Where(c => !char.IsControl(c)).All(c => text.font.HasCharacter(c)), "TMP fits and contains Chinese glyphs: " + text.name); }
                    g.Players.Player.ViewCamera.GetComponent<MagicIsland.CameraSystem.CameraController>().SnapToTarget();
                    GameUiPreview.CaptureInventory(g.Players.Player.ViewCamera); File.Copy("Logs/InventoryPreview.png", "Logs/InventorySortPreview.png", true);
                    g.CloseInventory(); g.Economy.Restore(1100);
                    Require(g.Inventory.Upgrade(g.Economy) && g.Inventory.Upgrade(g.Economy), "Expanded capacity fixture");
                    g.Inventory.State.Restore(Fixture(30)); g.ToggleInventory(); Phase(3);
                }
                else if (phase == 3 && g.UI.IsInventoryVisible)
                {
                    Bag.Slots[6].SelectButton.onClick.Invoke(); Bag.SortButton.onClick.Invoke(); CheckSorted(g);
                    Require(g.Inventory.State.Capacity == 30 && g.Inventory.UpgradeState.Level == 2 && Bag.Slots.Count(s => s.gameObject.activeSelf) == 30, "Sort preserves upgraded capacity and pooled bindings");
                    savedSlots = g.Inventory.State.Slots.ToArray(); g.CloseInventory(); g.ReturnToMainMenu(); Phase(4);
                }
                else if (phase == 4 && g.State == GameFlowState.MainMenu) { g.ContinueGame(); Phase(5); }
                else if (phase == 5 && g.State == GameFlowState.Playing)
                {
                    // JsonUtility writes empty null strings as ""; both represent an empty slot.
                    Require(savedSlots.Select(s => (s.ItemId ?? "") + ":" + s.Quantity).SequenceEqual(g.Inventory.State.Slots.Select(s => (s.ItemId ?? "") + ":" + s.Quantity)) && g.Inventory.State.Capacity == 30 && g.Inventory.UpgradeState.Level == 2 && g.Inventory.SortFeedback == null, "Exact sorted order and upgrade restore without transient feedback");
                    g.ToggleInventory(); Phase(6);
                }
                else if (phase == 6 && g.UI.IsInventoryVisible)
                {
                    g.Inventory.SetSortSaveFailed();
                    Bag.Slots[3].SelectButton.onClick.Invoke(); Bag.UseFoodButton.onClick.Invoke();
                    Require(g.Inventory.GetCount("turnip_soup") == 1 && g.Stamina.State.Current == 80 && !g.Inventory.SortSavePending, "Reselect after sorting consumes correct food and successful autosave clears pending failure");
                    g.Inventory.State.Restore(new InventorySlot[30]); Bag.Slots[29].SelectButton.onClick.Invoke(); Bag.SortButton.onClick.Invoke();
                    Require(g.Inventory.State.Slots.All(s => s.Empty) && Bag.DetailTitleText.text == "选择物品" && g.Inventory.State.Capacity == 30 && g.Saves.TryLoad(out var empty) && empty.Slots.All(s => s.Quantity == 0), "Empty sort clears selection and saves unchanged capacity");
                    File.WriteAllText("Logs/InventorySortVerification.txt", "PASS: packed AA button and missing-only setup, exact type ordering and merges, separate egg quality, immediate selection invalidation, guarded reentry, no costs/report changes, failed save retaining sort and retry, upgraded thirty-slot bag, exact save/load order, reselected food use, empty/idempotent sort, Chinese TMP layout.");
                    Debug.Log("Inventory sort flow verification PASSED."); Phase(9); EditorApplication.isPlaying = false;
                }
            }
            catch (Exception error) { Debug.LogException(error); SessionState.SetBool(Key, false); EditorApplication.Exit(1); }
        }
    }
}
