using System;
using System.IO;
using System.Linq;
using MagicIsland.Managers;
using MagicIsland.Saving;
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
    public static class InventoryGridFlowVerification
    {
        const string Key = "MagicIsland.InventoryGridVerification";
        static InventoryGridFlowVerification() { if (SessionState.GetBool(Key, false)) EditorApplication.update += Tick; }
        public static void Run()
        {
            PrototypeAddressablesSetup.EnsureSetup(); PrototypeAddressablesSetup.ValidateUiPrefabs();
            const string path = "Assets/Art/Prefabs/UI/InventoryPanel.prefab"; var bytes = File.ReadAllBytes(path);
            PrototypeAddressablesSetup.EnsureSetup(); Require(bytes.SequenceEqual(File.ReadAllBytes(path)), "Repeated setup preserves edited grid");
            EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity"); PrototypeAddressablesSetup.BuildContent();
            var settings = AddressableAssetSettingsDefaultObject.Settings; settings.ActivePlayModeDataBuilderIndex = settings.DataBuilders.FindIndex(b => b is BuildScriptPackedPlayMode);
            SessionState.SetBool(Key, true); Phase(0); SessionState.SetFloat(Key + ".Start", (float)EditorApplication.timeSinceStartup);
            EditorApplication.update -= Tick; EditorApplication.update += Tick; EditorApplication.isPlaying = true;
        }
        static void Require(bool condition, string why) { if (!condition) throw new Exception(why); }
        static void Phase(int phase) { SessionState.SetInt(Key + ".Phase", phase); Debug.Log("Inventory grid phase " + phase); }
        static InventoryView Bag => UnityEngine.Object.FindObjectOfType<InventoryView>(true);
        static void SaveDirectory(GameManager g, string dir) => g.Saves.Initialize(g.Configuration.Config, g.Configuration.Items, g.Configuration.Crops, dir,
            g.Configuration.Tasks, g.Configuration.Orders, g.Configuration.Seasons, g.Configuration.Gathering, g.Configuration.ToolUpgrades, g.Configuration.Storage,
            g.Configuration.NPCs, g.Configuration.Relationships, g.Configuration.Stories, g.Configuration.NPCSchedules, g.Configuration.Fishing, g.Configuration.Crafting);
        static void Layout(int unlocked)
        {
            Canvas.ForceUpdateCanvases(); var scroll = Bag.GetComponentInChildren<ScrollRect>(true); var grid = scroll.content.GetComponent<GridLayoutGroup>();
            Require(grid.constraintCount == 10 && !scroll.vertical && !scroll.horizontal, "Ten columns and fixed viewport");
            var slots = Bag.Slots.Where(slot => slot.gameObject.activeSelf).ToArray();
            Require(slots.Length == 30 && slots.Count(slot => slot.SelectButton.interactable) == unlocked, "Thirty visible slots and exact unlocked count");
            Require(slots.Select(s => Mathf.RoundToInt(((RectTransform)s.transform).anchoredPosition.y)).Distinct().Count() == 3
                && slots.Take(10).Select(s => Mathf.RoundToInt(((RectTransform)s.transform).anchoredPosition.x)).Distinct().Count() == 10, "Three rows of ten");
            foreach (var slot in slots)
            {
                var corners = new Vector3[4]; ((RectTransform)slot.transform).GetWorldCorners(corners);
                foreach (var corner in corners) { var local = scroll.viewport.InverseTransformPoint(corner); Require(scroll.viewport.rect.xMin - .1f <= local.x && local.x <= scroll.viewport.rect.xMax + .1f && scroll.viewport.rect.yMin - .1f <= local.y && local.y <= scroll.viewport.rect.yMax + .1f, "All thirty cells fit viewport"); }
                if (!slot.SelectButton.interactable) Require(slot.LockIcon.enabled && !slot.Icon.enabled && slot.NameText.text == "未解锁", "Locked slot has lock and no item icon");
                else Require(!slot.LockIcon.enabled, "Unlocked slot has no lock");
            }
        }
        static PointerEventData Begin(int source)
        {
            var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left, pointerId = -1,
                pointerDrag = Bag.Slots[source].gameObject, position = RectTransformUtility.WorldToScreenPoint(null, Bag.Slots[source].transform.position), eligibleForClick = true };
            ExecuteEvents.Execute(Bag.Slots[source].gameObject, data, ExecuteEvents.beginDragHandler); return data;
        }
        static void End(int source, int target, PointerEventData data)
        {
            ExecuteEvents.Execute(Bag.Slots[target].gameObject, data, ExecuteEvents.dropHandler);
            ExecuteEvents.Execute(Bag.Slots[source].gameObject, data, ExecuteEvents.endDragHandler);
        }
        static void Right(int slot) => ExecuteEvents.Execute(Bag.Slots[slot].gameObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Right, pointerId = -2 }, ExecuteEvents.pointerClickHandler);
        static string Order(GameManager g) => string.Join("|", g.Inventory.State.Slots.Select(slot => (slot.ItemId ?? "") + ":" + slot.Quantity));
        static void Capture(GameManager g, string name)
        {
            g.UI.RefreshViews(); Canvas.ForceUpdateCanvases();
            foreach (var text in Bag.GetComponentsInChildren<TMPro.TMP_Text>(true))
            { text.ForceMeshUpdate(true); Require(!text.isTextOverflowing && (text.text ?? "").Where(c => !char.IsControl(c)).All(c => text.font.HasCharacter(c)), "Grid Chinese TMP: " + text.name + "=" + text.text); }
            g.Players.Player.ViewCamera.GetComponent<MagicIsland.CameraSystem.CameraController>().SnapToTarget();
            GameUiPreview.CaptureInventory(g.Players.Player.ViewCamera); File.Copy("Logs/InventoryPreview.png", "Logs/" + name + ".png", true);
        }
        static void Tick()
        {
            if (!SessionState.GetBool(Key, false)) return; int phase = SessionState.GetInt(Key + ".Phase", 0);
            if (phase == 9 && !EditorApplication.isPlaying) { SessionState.SetBool(Key, false); EditorApplication.Exit(0); return; }
            try
            {
                if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + ".Start", 0) > 180) throw new Exception("Inventory grid timeout " + phase);
                if (!EditorApplication.isPlaying) return; var g = GameManager.Instance; if (g == null) return;
                Require(g.State != GameFlowState.Failed, "Game bootstrap failed"); string dir = SessionState.GetString(Key + ".Dir", "");
                if (phase == 0 && g.State == GameFlowState.MainMenu)
                {
                    dir = Path.GetFullPath("Logs/InventoryGridSaves/" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
                    SessionState.SetString(Key + ".Dir", dir); SaveDirectory(g, dir); g.EnterGame(); Phase(1);
                }
                else if (phase == 1 && g.State == GameFlowState.Playing) { g.ToggleInventory(); Phase(2); }
                else if (phase == 2 && g.UI.IsInventoryVisible)
                {
                    Layout(10); Require(g.Inventory.State.Capacity == 10 && !Bag.PageControls.gameObject.activeSelf, "New game retains ten actual slots, no pager");
                    Bag.Slots[0].SelectButton.onClick.Invoke(); string title = Bag.DetailTitleText.text, before = Order(g); uint revision = g.Inventory.State.ReorderVersion;
                    Bag.Slots[10].SelectButton.onClick.Invoke(); Right(10); Begin(10);
                    Require(!g.Inventory.IsDragging && before == Order(g) && Bag.DetailTitleText.text == title && revision == g.Inventory.State.ReorderVersion, "Locked click/right/drag preserves items and selection");
                    var pointer = Begin(0); Require(g.Inventory.IsDragging, "Unlocked source can drag"); End(0, 10, pointer);
                    Require(!g.Inventory.IsDragging && before == Order(g) && revision == g.Inventory.State.ReorderVersion, "Drop on locked cell cancels without change");
                    Capture(g, "InventoryTenUnlockedPreview"); g.Economy.Restore(1100); Require(g.Inventory.Upgrade(g.Economy), "First expansion");
                    Layout(20); Require(g.Inventory.State.Capacity == 20 && !Bag.Slots[19].LockIcon.enabled && Bag.Slots[20].LockIcon.enabled, "Second row unlocks live"); Capture(g, "InventoryTwentyUnlockedPreview");
                    var slots = g.Inventory.State.Slots.ToArray(); slots[19] = new MagicIsland.Inventory.InventorySlot("turnip_soup", 3); g.Inventory.State.Restore(slots);
                    Bag.Slots[19].SelectButton.onClick.Invoke(); Require(Bag.DetailTitleText.text == "芜菁汤", "Newly unlocked cell is selectable");
                    Require(g.Inventory.Upgrade(g.Economy), "Second expansion"); Layout(30); Require(g.Inventory.State.Capacity == 30 && g.Inventory.UpgradeState.Level == 2, "All three rows unlock");
                    Capture(g, "InventoryThirtyUnlockedPreview"); g.CloseInventory(); g.ReturnToMainMenu(); Phase(3);
                }
                else if (phase == 3 && g.State == GameFlowState.MainMenu)
                {
                    Require(g.Saves.TryLoad(out var saved), "Current save valid"); saved.InventoryUpgrade.BaseSlots = 36; saved.InventoryUpgrade.Level = 2;
                    saved.Slots = Enumerable.Range(0, 56).Select(_ => new SavedSlot()).ToArray();
                    saved.Slots[31] = new SavedSlot { ItemId = "turnip_soup", Quantity = 3 }; saved.Slots[55] = new SavedSlot { ItemId = "quality_egg", Quantity = 9 };
                    string json = JsonUtility.ToJson(saved); File.WriteAllText(g.Saves.SavePath, json); File.WriteAllText(g.Saves.SavePath + ".bak", json);
                    g.ContinueGame(); Phase(4);
                }
                else if (phase == 4 && g.State == GameFlowState.Playing) { g.Stamina.Restore(50); g.ToggleInventory(); Phase(5); }
                else if (phase == 5 && g.UI.IsInventoryVisible)
                {
                    Layout(30); Require(g.Inventory.State.Capacity == 56 && Bag.PageControls.gameObject.activeSelf && Bag.NextPageButton.interactable, "Large legacy save preserved behind thirty-slot paging");
                    Bag.NextPageButton.onClick.Invoke(); Layout(26); Require(Bag.PageText.text.Contains("2 / 2") && Bag.DetailTitleText.text == "选择物品", "Legacy second page and selection reset");
                    Bag.Slots[1].SelectButton.onClick.Invoke(); Bag.UseFoodButton.onClick.Invoke(); Require(g.Inventory.State.Slots[31].Quantity == 2 && g.Stamina.State.Current == 80, "Paged food selection resolves real index");
                    Right(25); Require(g.Inventory.State.Slots[55].Quantity == 5 && g.Inventory.State.Slots[0].ItemId == "quality_egg" && g.Inventory.State.Slots[0].Quantity == 4, "Paged split keeps quality and uses actual first empty");
                    Bag.Slots[25].SelectButton.onClick.Invoke(); string before = Order(g); Bag.Slots[26].SelectButton.onClick.Invoke(); Right(26); End(25, 26, Begin(25));
                    Require(before == Order(g) && !g.Inventory.IsDragging && Bag.DetailTitleText.text == "优质鸡蛋", "Legacy locked tail rejects click/split/drop");
                    End(1, 24, Begin(1)); Require(g.Inventory.State.Slots[31].Empty && g.Inventory.State.Slots[54].ItemId == "turnip_soup" && g.Inventory.State.Slots[54].Quantity == 2, "Paged drop resolves real destination");
                    var data = Begin(24); Bag.PreviousPageButton.onClick.Invoke(); Require(Bag.PageText.text.Contains("2 / 2"), "Page switch blocked during drag"); ExecuteEvents.Execute(Bag.Slots[24].gameObject, data, ExecuteEvents.endDragHandler);
                    Capture(g, "InventoryLegacyPagePreview"); Bag.PreviousPageButton.onClick.Invoke(); Layout(30); Require(Bag.Slots[0].NameText.text == "优质鸡蛋", "First page shows exact split output");
                    g.CloseInventory(); g.ReturnToMainMenu(); Phase(6);
                }
                else if (phase == 6 && g.State == GameFlowState.MainMenu) { g.ContinueGame(); Phase(7); }
                else if (phase == 7 && g.State == GameFlowState.Playing)
                {
                    Require(g.Inventory.State.Capacity == 56 && g.Inventory.State.Slots[54].Quantity == 2 && g.Inventory.State.Slots[55].Quantity == 5 && g.Inventory.State.Slots[0].Quantity == 4 && g.Inventory.UpgradeState.Level == 2, "Legacy page edits save/load without truncation or changed level");
                    File.WriteAllText("Logs/InventoryGridVerification.txt", "PASS: packed AA and preserved repeated setup, exactly three rows of ten fitting the viewport, 10/20/30 live unlocks with visible lock graphics, locked click/right/drag/drop rejection, newly unlocked bindings, Chinese TMP and previews, fifty-six-slot legacy paging, real-index food/split/drop on later page, page-switch drag guard, exact legacy order/quality/level save-load without truncation.");
                    Debug.Log("Inventory grid flow verification PASSED."); Phase(9); EditorApplication.isPlaying = false;
                }
            }
            catch (Exception error) { Debug.LogException(error); SessionState.SetBool(Key, false); EditorApplication.Exit(1); }
        }
    }
}
