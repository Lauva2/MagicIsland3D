using System;
using System.Collections.Generic;
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
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MagicIsland.EditorTools
{
    [InitializeOnLoad]
    public static class InventoryEditFlowVerification
    {
        const string Key = "MagicIsland.InventoryEditVerification";
        static InventorySlot[] savedSlots;
        static InventoryEditFlowVerification() { if (SessionState.GetBool(Key, false)) EditorApplication.update += Tick; }
        public static void Run()
        {
            PrototypeAddressablesSetup.EnsureSetup(); PrototypeAddressablesSetup.ValidateUiPrefabs();
            string path = "Assets/Art/Prefabs/UI/InventoryPanel.prefab"; var bytes = File.ReadAllBytes(path);
            PrototypeAddressablesSetup.EnsureSetup(); Require(bytes.SequenceEqual(File.ReadAllBytes(path)), "Repeated setup preserves edited prefab");
            EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity"); PrototypeAddressablesSetup.BuildContent();
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            settings.ActivePlayModeDataBuilderIndex = settings.DataBuilders.FindIndex(b => b is BuildScriptPackedPlayMode);
            SessionState.SetBool(Key, true); Phase(0); SessionState.SetFloat(Key + ".Start", (float)EditorApplication.timeSinceStartup);
            EditorApplication.update -= Tick; EditorApplication.update += Tick; EditorApplication.isPlaying = true;
        }
        static void Require(bool value, string why) { if (!value) throw new Exception(why); }
        static void Phase(int value) { SessionState.SetInt(Key + ".Phase", value); Debug.Log("Inventory edit phase " + value); }
        static InventoryView Bag => UnityEngine.Object.FindObjectOfType<InventoryView>(true);
        static void SaveDirectory(GameManager g, string dir) => g.Saves.Initialize(g.Configuration.Config, g.Configuration.Items, g.Configuration.Crops, dir,
            g.Configuration.Tasks, g.Configuration.Orders, g.Configuration.Seasons, g.Configuration.Gathering, g.Configuration.ToolUpgrades, g.Configuration.Storage,
            g.Configuration.NPCs, g.Configuration.Relationships, g.Configuration.Stories, g.Configuration.NPCSchedules, g.Configuration.Fishing, g.Configuration.Crafting);
        static InventorySlot[] Fixture(int capacity)
        {
            var slots = new InventorySlot[capacity];
            slots[0] = new InventorySlot("wood", 40); slots[1] = new InventorySlot("wood", 80); slots[2] = new InventorySlot("turnip_seed", 5);
            slots[3] = new InventorySlot("turnip_soup", 3); slots[4] = new InventorySlot("quality_egg", 7); slots[5] = new InventorySlot("egg", 9); slots[6] = new InventorySlot("wood_fence", 2);
            return slots;
        }
        static PointerEventData Begin(GameManager g, int source)
        {
            var slot = Bag.Slots[source];
            var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left, pointerId = -1,
                pointerDrag = slot.gameObject, position = RectTransformUtility.WorldToScreenPoint(null, slot.transform.position), eligibleForClick = true };
            Require(ExecuteEvents.ExecuteHierarchy(slot.Icon.gameObject, data, ExecuteEvents.initializePotentialDrag) == slot.gameObject, "Slot owns drag initialization before ScrollRect");
            ExecuteEvents.ExecuteHierarchy(slot.Icon.gameObject, data, ExecuteEvents.beginDragHandler);
            ExecuteEvents.Execute(slot.gameObject, data, ExecuteEvents.dragHandler);
            Require(g.Inventory.IsDragging && Bag.DragPreview.gameObject.activeSelf && !data.eligibleForClick, "Pointer drag accepted and suppresses click selection");
            return data;
        }
        static void Drop(int source, int target, PointerEventData data)
        {
            data.position = RectTransformUtility.WorldToScreenPoint(null, Bag.Slots[target].transform.position);
            ExecuteEvents.Execute(Bag.Slots[source].gameObject, data, ExecuteEvents.dragHandler);
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(data, hits);
            Require(hits.All(hit => !hit.gameObject.transform.IsChildOf(Bag.DragPreview)), "Ghost never intercepts raycasts");
            ExecuteEvents.ExecuteHierarchy(Bag.Slots[target].Icon.gameObject, data, ExecuteEvents.dropHandler);
            ExecuteEvents.Execute(Bag.Slots[source].gameObject, data, ExecuteEvents.endDragHandler);
        }
        static void Move(GameManager g, int from, int to) => Drop(from, to, Begin(g, from));
        static void Right(int source) => ExecuteEvents.ExecuteHierarchy(Bag.Slots[source].Icon.gameObject,
            new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Right, pointerId = -2 }, ExecuteEvents.pointerClickHandler);
        static void ClearSelection() => Require(Bag.DetailTitleText.text == "选择物品" && !Bag.UseSeedButton.interactable && !Bag.UseFoodButton.gameObject.activeSelf && !Bag.PlaceButton.gameObject.activeSelf, "Successful edit clears selected-slot actions");
        static void Capture(GameManager g, string name)
        {
            g.UI.RefreshViews(); Canvas.ForceUpdateCanvases();
            foreach (var text in Bag.GetComponentsInChildren<TMPro.TMP_Text>(true))
            { text.ForceMeshUpdate(true); Require(!text.isTextOverflowing && text.text.Where(c => !char.IsControl(c)).All(c => text.font.HasCharacter(c)), "Chinese TMP fits: " + text.name + "=" + text.text); }
            g.Players.Player.ViewCamera.GetComponent<MagicIsland.CameraSystem.CameraController>().SnapToTarget();
            GameUiPreview.CaptureInventory(g.Players.Player.ViewCamera); File.Copy("Logs/InventoryPreview.png", "Logs/" + name + ".png", true);
        }
        static string Order(IEnumerable<InventorySlot> slots) => string.Join("|", slots.Select(s => (s.ItemId ?? "") + ":" + s.Quantity));
        static void Tick()
        {
            if (!SessionState.GetBool(Key, false)) return; int phase = SessionState.GetInt(Key + ".Phase", 0);
            if (phase == 9 && !EditorApplication.isPlaying) { SessionState.SetBool(Key, false); EditorApplication.Exit(0); return; }
            try
            {
                if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + ".Start", 0) > 180) throw new Exception("Inventory edit timeout " + phase);
                if (!EditorApplication.isPlaying) return; var g = GameManager.Instance; if (g == null) return;
                Require(g.State != GameFlowState.Failed, "Game bootstrap failed"); string dir = SessionState.GetString(Key + ".Dir", "");
                if (phase == 0 && g.State == GameFlowState.MainMenu)
                {
                    dir = Path.GetFullPath("Logs/InventoryEditSaves/" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
                    SessionState.SetString(Key + ".Dir", dir); SaveDirectory(g, dir); g.EnterGame(); Phase(1);
                }
                else if (phase == 1 && g.State == GameFlowState.Playing)
                {
                    Require(Bag.Slots.All(s => s.GetComponent<InventorySlotInput>() != null) && !Bag.DragPreview.gameObject.activeSelf, "Packed AA input components and initially hidden preview");
                    g.Inventory.State.Restore(Fixture(10)); var before = Order(g.Inventory.State.Slots);
                    Require(!g.BeginInventoryDrag(0), "Hidden inventory refuses drag"); g.SplitInventoryItem(0); Require(before == Order(g.Inventory.State.Slots), "Hidden inventory refuses split");
                    g.Stamina.Restore(50); g.ToggleInventory(); Phase(2);
                }
                else if (phase == 2 && g.UI.IsInventoryVisible)
                {
                    var original = g.Inventory.State; var wrapper = original.Slots;
                    long gold = g.Economy.Gold; int day = g.Farm.State.Day; string report = JsonUtility.ToJson(g.DailyReports.State.Capture());
                    Bag.Slots[3].SelectButton.onClick.Invoke(); string before = Order(original.Slots);
                    var rightDrag = new PointerEventData(EventSystem.current) { pointerId = -2, button = PointerEventData.InputButton.Right, eligibleForClick = true };
                    ExecuteEvents.Execute(Bag.Slots[0].gameObject, rightDrag, ExecuteEvents.beginDragHandler);
                    Require(!rightDrag.eligibleForClick && !g.Inventory.IsDragging && before == Order(original.Slots), "Right-button drag suppresses accidental split click");
                    var pointer = Begin(g, 0);
                    Require(!Bag.SortButton.interactable && !Bag.SaveButton.interactable && !Bag.UseFoodButton.interactable, "Drag disables competing inventory actions");
                    g.SortInventory(); g.UseFood(3); g.UseSeed("turnip_seed"); g.BeginPlacementFromBag(6); g.SplitInventoryItem(0); g.SaveFood();
                    Require(before == Order(original.Slots) && g.UI.IsInventoryVisible && g.Stamina.State.Current == 50, "Dragging changes no state and protects food/seed/placement/split/sort");
                    var wrongPointer = new PointerEventData(EventSystem.current) { pointerId = -2, button = PointerEventData.InputButton.Left };
                    ExecuteEvents.Execute(Bag.Slots[0].gameObject, wrongPointer, ExecuteEvents.endDragHandler); Require(g.Inventory.IsDragging, "Different pointer cannot end owned drag");
                    Capture(g, "InventoryDragPreview");
                    ExecuteEvents.Execute(Bag.Slots[0].gameObject, pointer, ExecuteEvents.endDragHandler);
                    Require(!g.Inventory.IsDragging && !Bag.DragPreview.gameObject.activeSelf && before == Order(original.Slots) && Bag.UseFoodButton.interactable, "Outside release cancels with original selection/items intact");
                    Move(g, 0, 7); Require(original.Slots[0].Empty && original.Slots[7].Quantity == 40, "Whole stack moved to empty target"); ClearSelection();
                    Move(g, 7, 1); Require(original.Slots[1].Quantity == 99 && original.Slots[7].Quantity == 21, "Partial merge retains overflow");
                    Move(g, 7, 2); Require(original.Slots[2].ItemId == "wood" && original.Slots[7].ItemId == "turnip_seed", "Different-item swap");
                    Move(g, 4, 5); Require(original.Slots[4].ItemId == "egg" && original.Slots[5].ItemId == "quality_egg" && g.Inventory.GetCount("egg") == 9 && g.Inventory.GetCount("quality_egg") == 7, "Quality eggs swap without merging");
                    Bag.Slots[3].SelectButton.onClick.Invoke(); before = Order(original.Slots); Move(g, 3, 3);
                    Require(before == Order(original.Slots) && Bag.DetailTitleText.text == "芜菁汤", "Same-slot drop changes neither order nor selection");
                    Move(g, 2, 1); Require(before == Order(original.Slots) && Bag.FeedbackText.text.Contains("堆叠已满"), "Full stack drop fails without losing source");
                    Right(2); Require(original.Slots[2].Quantity == 11 && original.Slots[0].Quantity == 10 && original.Slots[0].ItemId == "wood", "Right click splits odd stack into first empty"); ClearSelection();
                    Right(3); Require(original.Slots[3].Quantity == 2 && original.Slots[8].Quantity == 1, "Right click splits food exactly");
                    before = Order(original.Slots); Right(8); Require(before == Order(original.Slots) && Bag.FeedbackText.text.Contains("至少两个"), "One-item split is refused");
                    var full = original.Slots.ToArray(); full[9] = new InventorySlot("turnip_seed", 1); original.Restore(full);
                    before = Order(original.Slots); Right(2); Require(before == Order(original.Slots) && Bag.FeedbackText.text.Contains("没有空格"), "Full bag split fails even with mergeable stacks");
                    string blocked = Path.Combine(dir, "blocked"); File.WriteAllText(blocked, "file"); SaveDirectory(g, blocked);
                    uint revision = original.ReorderVersion;
                    Action observer = () =>
                    {
                        ClearSelection(); Require(!g.BeginInventoryDrag(6), "Nested begin blocked"); g.SplitInventoryItem(6); g.SortInventory(); g.CloseInventory(); g.ReturnToMainMenu(); g.AdvanceDay(); g.BeginPlacementFromBag(1);
                        Require(original.ReorderVersion == revision + 1 && g.UI.IsInventoryVisible && g.State == GameFlowState.Playing, "Commit guards scene/selection/other operations");
                    };
                    original.Changed += observer; Move(g, 1, 6); original.Changed -= observer;
                    Require(original.Slots[6].Quantity == 99 && original.Slots[1].ItemId == "wood_fence" && g.Inventory.EditSavePending && Bag.FeedbackText.text.Contains("保存失败"), "Failed save retains exact swap and retry feedback");
                    SaveDirectory(g, dir); Bag.SaveButton.onClick.Invoke(); Require(!g.Inventory.EditSavePending && g.Saves.TryLoad(out var saved) && saved.Slots[6].Quantity == 99 && saved.Slots[1].ItemId == "wood_fence", "Save retry persists edits exactly once");
                    Require(ReferenceEquals(original, g.Inventory.State) && ReferenceEquals(wrapper, original.Slots) && gold == g.Economy.Gold && day == g.Farm.State.Day && g.Stamina.State.Current == 50 && report == JsonUtility.ToJson(g.DailyReports.State.Capture()), "All edits preserve bag identity/capacity, money/stamina/day/report");
                    Capture(g, "InventorySplitPreview");
                    before = Order(original.Slots); pointer = Begin(g, 0); g.CloseInventory();
                    Require(!g.Inventory.IsDragging && !Bag.DragPreview.gameObject.activeSelf && before == Order(original.Slots), "Closing cancels drag and hides cached preview");
                    g.ToggleInventory(); pointer = Begin(g, 0); original.TryRemoveSlot(0, 1);
                    Require(!g.Inventory.IsDragging && !Bag.DragPreview.gameObject.activeSelf, "Changed source automatically invalidates stale drag");
                    before = Order(original.Slots); Drop(0, 9, pointer); Require(before == Order(original.Slots), "Stale pointer cannot move changed source");
                    pointer = Begin(g, 0); pointer.pointerDrag = Bag.CloseButton.gameObject; before = Order(original.Slots); Drop(0, 9, pointer);
                    Require(before == Order(original.Slots) && !g.Inventory.IsDragging, "Unowned drag ignored and canceled");
                    g.CloseInventory(); g.Economy.Restore(1100); Require(g.Inventory.Upgrade(g.Economy) && g.Inventory.Upgrade(g.Economy), "Thirty-slot fixture");
                    var expanded = Fixture(30); expanded[29] = new InventorySlot("turnip_seed", 12); original.Restore(expanded); g.ToggleInventory(); Phase(3);
                }
                else if (phase == 3 && g.UI.IsInventoryVisible)
                {
                    Bag.GetComponentInChildren<ScrollRect>().verticalNormalizedPosition = 0; Canvas.ForceUpdateCanvases();
                    Move(g, 29, 28); Right(28);
                    Require(g.Inventory.State.Slots[28].Quantity == 6 && g.Inventory.State.Slots[7].Quantity == 6 && g.Inventory.State.Slots[29].Empty && g.Inventory.State.Capacity == 30 && g.Inventory.UpgradeState.Level == 2, "Cached bindings work on expanded slots with unchanged level");
                    savedSlots = g.Inventory.State.Slots.ToArray(); g.CloseInventory(); g.ReturnToMainMenu(); Phase(4);
                }
                else if (phase == 4 && g.State == GameFlowState.MainMenu) { g.ContinueGame(); Phase(5); }
                else if (phase == 5 && g.State == GameFlowState.Playing)
                {
                    Require(Order(savedSlots) == Order(g.Inventory.State.Slots) && g.Inventory.State.Capacity == 30 && g.Inventory.UpgradeState.Level == 2 && !g.Inventory.IsDragging && g.Inventory.EditFeedback == null && !Bag.DragPreview.gameObject.activeSelf, "Exact edit order restored without transient drag state");
                    g.ToggleInventory(); Phase(6);
                }
                else if (phase == 6 && g.UI.IsInventoryVisible)
                {
                    Bag.Slots[3].SelectButton.onClick.Invoke(); Bag.UseFoodButton.onClick.Invoke(); Require(g.Inventory.GetCount("turnip_soup") == 2 && g.Stamina.State.Current == 80, "Reselect food after editing/load uses intended stack");
                    Begin(g, 0); g.ReturnToMainMenu(); Phase(7);
                }
                else if (phase == 7 && g.State == GameFlowState.MainMenu)
                {
                    Require(g.Inventory.State == null && !g.Inventory.IsDragging, "Returning disposes inputs and clears drag state"); g.EnterGame(); Phase(8);
                }
                else if (phase == 8 && g.State == GameFlowState.Playing)
                {
                    Require(g.Inventory.State.Capacity == 10 && g.Inventory.EditFeedback == null && !g.Inventory.IsDragging, "New game resets editing state and default ten slots");
                    File.WriteAllText("Logs/InventoryEditVerification.txt", "PASS: packed AA input/preview and missing-only setup, EventSystem child routing before ScrollRect, no ghost raycast interception, drag cancel and pointer ownership, competing action guards, exact move/partial merge/swap, distinct egg quality, right-click odd/even/single/full-bag splitting, selection invalidation and commit reentry, save failure/retry, stale source and foreign drag rejection, thirty-slot cached bindings, exact order/level save-load, food use, cleanup/new game and Chinese TMP layout.");
                    Debug.Log("Inventory edit flow verification PASSED."); Phase(9); EditorApplication.isPlaying = false;
                }
            }
            catch (Exception error) { Debug.LogException(error); SessionState.SetBool(Key, false); EditorApplication.Exit(1); }
        }
    }
}
