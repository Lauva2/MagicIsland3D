using System;
using System.Collections.Generic;
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
using UnityEngine.EventSystems;

namespace MagicIsland.EditorTools
{
    [InitializeOnLoad]
    public static class StorageShortcutFlowVerification
    {
        const string Key = "MagicIsland.StorageShortcutVerification";
        static StorageShortcutFlowVerification() { if (SessionState.GetBool(Key, false)) EditorApplication.update += Tick; }
        public static void Run()
        {
            PrototypeAddressablesSetup.EnsureSetup(); PrototypeAddressablesSetup.ValidateUiPrefabs();
            const string path = "Assets/Art/Prefabs/UI/StoragePanel.prefab"; var bytes = File.ReadAllBytes(path);
            PrototypeAddressablesSetup.EnsureSetup(); Require(bytes.SequenceEqual(File.ReadAllBytes(path)), "Repeated setup preserves edited storage");
            EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity"); PrototypeAddressablesSetup.BuildContent();
            var settings = AddressableAssetSettingsDefaultObject.Settings; settings.ActivePlayModeDataBuilderIndex = settings.DataBuilders.FindIndex(b => b is BuildScriptPackedPlayMode);
            SessionState.SetBool(Key, true); Phase(0); SessionState.SetFloat(Key + ".Start", (float)EditorApplication.timeSinceStartup);
            EditorApplication.update -= Tick; EditorApplication.update += Tick; EditorApplication.isPlaying = true;
        }
        static void Require(bool value, string why) { if (!value) throw new Exception(why); }
        static void Phase(int p) { SessionState.SetInt(Key + ".Phase", p); Debug.Log("Storage shortcut phase " + p); }
        static StorageView View => UnityEngine.Object.FindObjectOfType<StorageView>();
        static void SaveDirectory(GameManager g, string dir) => g.Saves.Initialize(g.Configuration.Config, g.Configuration.Items, g.Configuration.Crops, dir,
            g.Configuration.Tasks, g.Configuration.Orders, g.Configuration.Seasons, g.Configuration.Gathering, g.Configuration.ToolUpgrades, g.Configuration.Storage,
            g.Configuration.NPCs, g.Configuration.Relationships, g.Configuration.Stories, g.Configuration.NPCSchedules, g.Configuration.Fishing, g.Configuration.Crafting);
        static InventorySlot S(string id, int quantity) => new InventorySlot(id, quantity);
        static void Fixture(GameManager g, bool full = false)
        {
            var bag = new InventorySlot[g.Inventory.State.Capacity]; bag[0] = S("wood", full ? 20 : 50); bag[1] = S(full ? "wood" : "stone", full ? 30 : 12);
            bag[2] = S("egg", 5); bag[3] = S("quality_egg", 7); bag[4] = S(full ? "stone" : "turnip_soup", full ? 1 : 3); bag[5] = S("wood_fence", 2);
            var chest = full ? Enumerable.Repeat(S("stone", 99), 18).ToArray() : new InventorySlot[18]; chest[0] = S("wood", full ? 95 : 90);
            if (full) chest[1] = S("egg", 97); else { chest[1] = S("stone", 99); chest[2] = S("egg", 97); }
            g.Inventory.State.Restore(bag); g.Storage.State.Restore(chest); g.UI.RefreshViews();
        }
        static InventorySlotView Slot(bool bag, int i) => (bag ? View.BagSlots : View.ChestSlots)[i];
        static PointerEventData Begin(bool bag, int index)
        {
            var source = Slot(bag, index); var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left, pointerId = -1,
                pointerDrag = source.gameObject, position = RectTransformUtility.WorldToScreenPoint(null, source.transform.position), eligibleForClick = true };
            ExecuteEvents.ExecuteHierarchy(source.Icon.gameObject, data, ExecuteEvents.beginDragHandler); return data;
        }
        static void End(bool sourceBag, int source, bool targetBag, int target, PointerEventData data)
        {
            data.position = RectTransformUtility.WorldToScreenPoint(null, Slot(targetBag, target).transform.position);
            ExecuteEvents.Execute(Slot(sourceBag, source).gameObject, data, ExecuteEvents.dragHandler);
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(data, hits); Require(hits.All(h => !h.gameObject.transform.IsChildOf(View.DragPreview)), "Preview never intercepts drops");
            ExecuteEvents.ExecuteHierarchy(Slot(targetBag, target).Icon.gameObject, data, ExecuteEvents.dropHandler);
            ExecuteEvents.Execute(Slot(sourceBag, source).gameObject, data, ExecuteEvents.endDragHandler);
        }
        static void Drag(bool bag, int from, int to) => End(bag, from, !bag, to, Begin(bag, from));
        static void Shift(bool bag, int index)
        {
            var input = Slot(bag, index).GetComponent<InventorySlotInput>(); input.ShiftHeld = () => true;
            try { ExecuteEvents.ExecuteHierarchy(Slot(bag, index).Icon.gameObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler); }
            finally { input.ShiftHeld = null; }
        }
        static string Order(GameManager g) => string.Join("|", g.Inventory.State.Slots.Concat(g.Storage.State.Contents.Slots).Select(s => (s.ItemId ?? "") + ":" + s.Quantity));
        static void Capture(GameManager g, string name)
        {
            g.UI.RefreshViews(); Canvas.ForceUpdateCanvases(); foreach (var t in View.GetComponentsInChildren<TMPro.TMP_Text>(true))
            { t.ForceMeshUpdate(true); Require(!t.isTextOverflowing && (t.text ?? "").Where(c => !char.IsControl(c)).All(c => t.font.HasCharacter(c)), "Storage TMP fits: " + t.name + "=" + t.text); }
            g.Players.Player.ViewCamera.GetComponent<MagicIsland.CameraSystem.CameraController>().SnapToTarget(); GameUiPreview.CaptureInventory(g.Players.Player.ViewCamera); File.Copy("Logs/InventoryPreview.png", "Logs/" + name + ".png", true);
        }
        static void Tick()
        {
            if (!SessionState.GetBool(Key, false)) return; int p = SessionState.GetInt(Key + ".Phase", 0);
            if (p == 9 && !EditorApplication.isPlaying) { SessionState.SetBool(Key, false); EditorApplication.Exit(0); return; }
            try
            {
                if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + ".Start", 0) > 180) throw new Exception("Storage shortcut timeout " + p);
                if (!EditorApplication.isPlaying) return; var g = GameManager.Instance; if (g == null) return; Require(g.State != GameFlowState.Failed, "Bootstrap failure");
                string dir = SessionState.GetString(Key + ".Dir", "");
                if (p == 0 && g.State == GameFlowState.MainMenu)
                { dir = Path.GetFullPath("Logs/StorageShortcutSaves/" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir); SessionState.SetString(Key + ".Dir", dir); SaveDirectory(g, dir); g.EnterGame(); Phase(1); }
                else if (p == 1 && g.State == GameFlowState.Playing)
                { Fixture(g); g.Stamina.Restore(50); string before = Order(g); g.QuickTransferStorage(true, 0); g.DepositMatchingStorage(); Require(before == Order(g), "Closed panel guards shortcuts"); g.Players.RestorePosition(g.Storage.Chest.transform.position + new Vector3(0, .1f, -1.4f)); g.OpenStorage(); Phase(2); }
                else if (p == 2 && View != null)
                {
                    Require(View.BagSlots.Length == 30 && View.BagSlots.Count(s => s.SelectButton.interactable) == 10 && View.BagSlots.Skip(10).All(s => s.LockIcon.enabled), "Storage bag shows thirty cells with twenty locked");
                    long gold = g.Economy.Gold; int day = g.Farm.State.Day; string report = JsonUtility.ToJson(g.DailyReports.State.Capture());
                    string before = Order(g); Shift(true, 10); Begin(true, 10); Require(!g.Storage.IsDragging && before == Order(g), "Locked shift/drag source rejected");
                    var pointer = Begin(true, 0); Require(g.Storage.IsDragging && View.DragPreview.gameObject.activeSelf && !View.DepositMatchingButton.interactable && !View.SaveButton.interactable, "Drag preview and competing button guards");
                    g.QuickTransferStorage(true, 0); g.DepositMatchingStorage(); g.TransferStorage(true, 0, 1); g.SaveStorage(); Require(before == Order(g), "Dragging commits no items and blocks other transfers");
                    Capture(g, "StorageDragPreview"); ExecuteEvents.Execute(Slot(true, 0).gameObject, pointer, ExecuteEvents.endDragHandler); Require(!g.Storage.IsDragging && before == Order(g), "Outside release cancels");
                    End(true, 0, true, 1, Begin(true, 0)); Require(before == Order(g), "Same-side drop does not rearrange inventory");
                    Drag(true, 0, 0); Require(g.Inventory.State.Slots[0].Quantity == 41 && g.Storage.State.Contents.Slots[0].Quantity == 99 && View.DetailsText.text.StartsWith("选择"), "Targeted partial merge retains remainder and clears selection");
                    before = Order(g); Drag(true, 3, 2); Require(before == Order(g) && g.Storage.Feedback.Contains("不同物品"), "Premium egg cannot replace ordinary egg target");
                    Shift(true, 1); Require(g.Inventory.GetCount("stone") == 0 && g.Storage.State.Contents.GetCount("stone") == 111, "Shift deposit routes actual pointer click");
                    Shift(false, 0); Require(g.Inventory.GetCount("wood") == 140 && g.Storage.State.Contents.GetCount("wood") == 0 && g.Inventory.State.Slots[0].Quantity == 99, "Shift withdrawal fills existing stacks then empty cells");
                    View.DepositMatchingButton.onClick.Invoke(); Require(g.Inventory.GetCount("egg") == 0 && g.Storage.State.Contents.GetCount("egg") == 102 && g.Inventory.GetCount("quality_egg") == 7 && g.Inventory.GetCount("wood") == 140, "Matching uses exact initial chest IDs and leaves unmatched materials/premium eggs");
                    Fixture(g, true); Shift(true, 0); Require(g.Inventory.State.Slots[0].Quantity == 16 && g.Storage.State.Contents.Slots[0].Quantity == 99 && g.Storage.Feedback.Contains("剩余"), "Shift partially transfers into nearly full chest");
                    Fixture(g, true); string blocked = Path.Combine(dir, "blocked"); File.WriteAllText(blocked, "file"); SaveDirectory(g, blocked);
                    Action observer = () =>
                    {
                        Require(g.Inventory.GetCount("wood") == 46 && g.Inventory.GetCount("egg") == 3 && g.Storage.State.Contents.Slots[0].Quantity == 99 && g.Storage.State.Contents.Slots[1].Quantity == 99, "Batch commits both inventories before observer");
                        Require(!g.BeginStorageDrag(true, 0), "Commit blocks nested drag"); g.DepositMatchingStorage(); g.QuickTransferStorage(true, 1); g.CloseStorage(); g.ReturnToMainMenu(); g.AdvanceDay();
                        Require(g.State == GameFlowState.Playing && g.UI.HasStorage && g.Inventory.GetCount("wood") == 46, "Batch reentry and scene guards");
                    };
                    g.Inventory.State.Changed += observer; View.DepositMatchingButton.onClick.Invoke(); g.Inventory.State.Changed -= observer;
                    Require(g.Storage.SavePending && g.Storage.Feedback.Contains("保存失败") && g.Inventory.GetCount("quality_egg") == 7 && g.Inventory.GetCount("stone") == 1, "Failed batch save retains exact committed counts and unmatched items");
                    SaveDirectory(g, dir); View.SaveButton.onClick.Invoke(); Require(!g.Storage.SavePending && g.Saves.TryLoad(out var saved) && saved.Slots[0].Quantity == 16 && saved.Storage[0].Quantity == 99 && saved.Storage[1].Quantity == 99, "Retry saves both sides without transferring again");
                    Require(g.Economy.Gold == gold && g.Stamina.State.Current == 50 && g.Farm.State.Day == day && report == JsonUtility.ToJson(g.DailyReports.State.Capture()), "Transfers do not sell, harvest, spend stamina or change day");
                    Capture(g, "StorageShortcutPreview"); before = Order(g); pointer = Begin(true, 0); g.CloseStorage(); Require(!g.Storage.IsDragging && before == Order(g), "Closing cancels drag and disposes bindings");
                    g.ReturnToMainMenu(); Phase(3);
                }
                else if (p == 3 && g.State == GameFlowState.MainMenu)
                {
                    Require(g.Saves.TryLoad(out var saved), "Shortcut save exists"); saved.InventoryUpgrade.BaseSlots = 36; saved.InventoryUpgrade.Level = 2;
                    saved.Slots = Enumerable.Range(0, 56).Select(_ => new SavedSlot()).ToArray(); saved.Slots[31] = new SavedSlot { ItemId = "wood", Quantity = 22 }; saved.Slots[55] = new SavedSlot { ItemId = "turnip_soup", Quantity = 3 };
                    saved.Storage = Enumerable.Range(0, 18).Select(_ => new SavedSlot()).ToArray(); saved.Storage[17] = new SavedSlot { ItemId = "wood", Quantity = 95 };
                    string json = JsonUtility.ToJson(saved); File.WriteAllText(g.Saves.SavePath, json); File.WriteAllText(g.Saves.SavePath + ".bak", json); g.ContinueGame(); Phase(4);
                }
                else if (p == 4 && g.State == GameFlowState.Playing) { g.Players.RestorePosition(g.Storage.Chest.transform.position + new Vector3(0, .1f, -1.4f)); g.OpenStorage(); Phase(5); }
                else if (p == 5 && View != null)
                {
                    View.NextPageButton.onClick.Invoke(); Require(View.BagSlots.Count(s => s.SelectButton.interactable) == 26 && View.PageText.text.Contains("2 / 2"), "Legacy paging preserves thirty physical cells and locked tail");
                    Drag(true, 1, 17); Require(g.Inventory.State.Slots[31].Quantity == 18 && g.Storage.State.Contents.Slots[17].Quantity == 99, "Paged source uses real index and partial room");
                    string before = Order(g); End(false, 17, true, 26, Begin(false, 17)); Require(before == Order(g), "Withdraw drop into locked tail cancels");
                    Drag(false, 17, 24); Require(g.Inventory.State.Slots[54].Quantity == 99 && g.Storage.State.Contents.Slots[17].Empty, "Paged destination uses real inventory index");
                    Shift(true, 1); Require(g.Inventory.State.Slots[31].Empty && g.Storage.State.Contents.Slots[0].Quantity == 18 && g.Storage.State.Contents.Slots[17].Empty, "Paged Shift uses real source index and first empty destination");
                    var pointer = Begin(false, 0); View.PreviousPageButton.onClick.Invoke(); Require(View.PageText.text.Contains("2 / 2"), "Page switch blocked during drag"); ExecuteEvents.Execute(Slot(false, 0).gameObject, pointer, ExecuteEvents.endDragHandler);
                    Capture(g, "StorageLegacyPreview"); g.CloseStorage(); g.ReturnToMainMenu(); Phase(6);
                }
                else if (p == 6 && g.State == GameFlowState.MainMenu) { g.ContinueGame(); Phase(7); }
                else if (p == 7 && g.State == GameFlowState.Playing)
                {
                    Require(g.Inventory.State.Capacity == 56 && g.Inventory.State.Slots[54].Quantity == 99 && g.Inventory.State.Slots[55].Quantity == 3 && g.Storage.State.Contents.Slots[0].Quantity == 18 && !g.Storage.IsDragging && !g.Storage.SavePending, "Exact legacy inventory/chest reload without transient drag state");
                    File.WriteAllText("Logs/StorageShortcutVerification.txt", "PASS: packed AA storage UI, preserved setup and Chinese layout, thirty bag cells and locks, pointer drag/cancel/same-side rejection, targeted partial merge and different-item rejection, Shift deposit/withdraw via EventSystem, exact-ID matching and atomic observer state/reentry, partial quick transfer, save failure/retry, no costs/report changes, cleanup and fifty-six-slot legacy page source/destination/Shift/lock mapping with exact reload.");
                    Debug.Log("Storage shortcut flow verification PASSED."); Phase(9); EditorApplication.isPlaying = false;
                }
            }
            catch (Exception error) { Debug.LogException(error); SessionState.SetBool(Key, false); EditorApplication.Exit(1); }
        }
    }
}
