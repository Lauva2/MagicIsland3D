using System;
using System.IO;
using System.Linq;
using MagicIsland.Interaction;
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
    public static class FurnitureFlowVerification
    {
        const string Key = "MagicIsland.FurnitureVerification";
        static FurnitureFlowVerification() { if (SessionState.GetBool(Key, false)) EditorApplication.update += Tick; }
        public static void Run()
        {
            PrototypeAddressablesSetup.EnsureSetup(); PrototypeAddressablesSetup.ValidateUiPrefabs(); EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity"); PrototypeAddressablesSetup.BuildContent();
            var settings = AddressableAssetSettingsDefaultObject.Settings; settings.ActivePlayModeDataBuilderIndex = settings.DataBuilders.FindIndex(b => b is BuildScriptPackedPlayMode);
            SessionState.SetBool(Key, true); Phase(0); SessionState.SetFloat(Key + ".Start", (float)EditorApplication.timeSinceStartup);
            EditorApplication.update -= Tick; EditorApplication.update += Tick; EditorApplication.isPlaying = true;
        }
        static void Phase(int value) => SessionState.SetInt(Key + ".Phase", value);
        static void Require(bool ok, string message) { if (!ok) throw new Exception(message); }
        static CraftingView Craft => UnityEngine.Object.FindObjectOfType<CraftingView>();
        static InventoryView Bag => UnityEngine.Object.FindObjectOfType<InventoryView>();
        static PlacementView Preview => UnityEngine.Object.FindObjectOfType<PlacementView>();
        static void SaveDirectory(GameManager g, string dir) => g.Saves.Initialize(g.Configuration.Config, g.Configuration.Items, g.Configuration.Crops, dir, g.Configuration.Tasks,
            g.Configuration.Orders, g.Configuration.Seasons, g.Configuration.Gathering, g.Configuration.ToolUpgrades, g.Configuration.Storage, g.Configuration.NPCs, g.Configuration.Relationships,
            g.Configuration.Stories, g.Configuration.NPCSchedules, g.Configuration.Fishing, g.Configuration.Crafting);
        static void At(GameManager g, float x, float z) => g.Players.RestorePosition(g.Configuration.Housing.InteriorOrigin + new Vector3(x, .1f, z));
        static void Door(GameManager g) => g.Players.RestorePosition(g.Housing.Entrance.Position + new Vector3(0, .1f, -1.4f));
        static int Slot(GameManager g, string id) => Enumerable.Range(0, g.Inventory.State.Capacity).First(i => g.Inventory.State.Slots[i].ItemId == id);
        static void Begin(GameManager g, string id)
        { if (!g.UI.IsInventoryVisible) g.ToggleInventory(); Bag.Slots[Slot(g, id)].SelectButton.onClick.Invoke(); Require(Bag.PlaceButton.interactable, "Furniture bag placement action"); Bag.PlaceButton.onClick.Invoke(); }
        static void TextCheck(Component view)
        { foreach (var t in view.GetComponentsInChildren<TMPro.TMP_Text>()) { t.ForceMeshUpdate(); Require(!t.isTextOverflowing && t.text.Where(c => !char.IsControl(c)).All(c => t.font.HasCharacter(c)), "Furniture Chinese/layout: " + t.name + "=" + t.text); } }
        static void Capture(GameManager g, string name)
        { g.Players.Player.ViewCamera.GetComponent<MagicIsland.CameraSystem.CameraController>().SnapToTarget(); g.UI.RefreshViews(); GameUiPreview.CaptureInventory(g.Players.Player.ViewCamera); File.Copy("Logs/InventoryPreview.png", "Logs/" + name + ".png", true); }
        static void Write(GameManager g, string json) { File.WriteAllText(g.Saves.SavePath, json); File.WriteAllText(g.Saves.SavePath + ".bak", json); }
        static void Tick()
        {
            if (!SessionState.GetBool(Key, false)) return; int phase = SessionState.GetInt(Key + ".Phase", 0);
            if (phase == 21 && !EditorApplication.isPlaying) { SessionState.SetBool(Key, false); EditorApplication.Exit(0); return; }
            try
            {
                if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + ".Start", 0) > 240) throw new Exception("Furniture timeout phase " + phase);
                if (!EditorApplication.isPlaying) return; var g = GameManager.Instance; if (g == null) return; Require(g.State != GameFlowState.Failed, "Furniture configuration failed");
                if (phase == 0 && g.State == GameFlowState.MainMenu)
                {
                    Require(g.Configuration.Crafting.Recipes.Count == 6 && g.Configuration.Crafting.Recipes.Count(r => r.IsFurniture) == 3, "Three outdoor recipes and three indoor recipes");
                    string dir = Path.GetFullPath("Logs/FurnitureSaves/" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir); SessionState.SetString(Key + ".Dir", dir); SaveDirectory(g, dir); g.EnterGame(); Phase(1);
                }
                else if (phase == 1 && g.State == GameFlowState.Playing)
                {
                    Require(g.Furniture.State.Count == 0 && !g.Furniture.Bound && !g.Scenes.HomeScene.IsValid(), "No furniture grants or eager indoor scene");
                    g.Inventory.TryAdd("wood", 30); g.Inventory.TryAdd("stone", 10); g.Farm.TryAct(0, MagicIsland.Farming.FarmTool.Hoe, out _);
                    g.Players.RestorePosition(g.ToolUpgrades.Workbench.transform.position + new Vector3(0, .1f, -1.7f)); g.OpenCrafting(); Phase(2);
                }
                else if (phase == 2 && Craft != null)
                {
                    Require(!Craft.PreviousButton.interactable && Craft.NextButton.interactable, "Crafting first page"); Craft.NextButton.onClick.Invoke();
                    Require(Craft.NameTexts[0].text == "木桌" && Craft.NameTexts[1].text == "木椅" && Craft.NameTexts[2].text == "小台灯", "Second page furniture mapping");
                    Require(Craft.PreviousButton.interactable && !Craft.NextButton.interactable, "Crafting page bounds"); TextCheck(Craft); Capture(g, "FurnitureCraftingPreview");
                    foreach (var button in Craft.CraftButtons) button.onClick.Invoke();
                    Require(g.Inventory.GetCount("wood") == 21 && g.Inventory.GetCount("stone") == 9 && g.Inventory.GetCount("wood_table") == 1 && g.Inventory.GetCount("wood_chair") == 1 && g.Inventory.GetCount("small_lamp") == 1, "Furniture recipe costs and yields");
                    Craft.PreviousButton.onClick.Invoke(); Require(Craft.NameTexts[0].text == "木栅栏", "Outdoor page preserved"); g.CloseCrafting();
                    g.ToggleInventory(); int slot = Slot(g, "wood_table"); Bag.Slots[slot].SelectButton.onClick.Invoke(); Require(!Bag.PlaceButton.interactable, "Indoor furniture disabled outdoors");
                    g.BeginPlacementFromBag(slot); Require(!g.Furniture.Active && g.UI.IsInventoryVisible, "Wrong area guard"); g.CloseInventory(); Door(g); g.EnterHome(); Phase(3);
                }
                else if (phase == 3 && g.State == GameFlowState.Playing && g.Housing.Inside)
                { Require(g.Furniture.Bound && g.Farm.State.Cells[0].Tilled, "Room binds shared world"); At(g, 2, .2f); Begin(g, "wood_table"); Phase(4); }
                else if (phase == 4 && g.Furniture.Ready && g.UI.HasPlacement && Preview != null)
                {
                    Require(Preview.Layer == UILayer.Common && !g.Tools.InputEnabled && g.Players.Player.enabled && !g.UI.IsInventoryVisible, "Furniture preview input and MVC layer");
                    foreach (var point in new[] { new Vector2Int(6, 0), new Vector2Int(0, -3), new Vector2Int(-1, 2), new Vector2Int(-3, 2), new Vector2Int(5, 4), new Vector2Int(2, 0) })
                    { g.Furniture.SetCandidate(point.x, point.y); Require(g.Furniture.CheckCandidate() != PlacementResult.Success, "Blocked wall/spawn/bed/shelf/player " + point); }
                    At(g, -3, .2f); g.Furniture.SetCandidate(-3, 2); Require(g.Furniture.CheckCandidate() == PlacementResult.Blocked, "Actual fixed bed collision in range");
                    At(g, 5, 2.2f); g.Furniture.SetCandidate(5, 4); Require(g.Furniture.CheckCandidate() == PlacementResult.Blocked, "Actual shelf collision in range"); At(g, 2, .2f);
                    g.Furniture.SetCandidate(2, 2); Require(g.Furniture.CheckCandidate() == PlacementResult.Success, "Valid floor cell"); g.RotatePlacement(); g.UI.RefreshViews(); TextCheck(Preview); Capture(g, "FurniturePlacementPreview");
                    double minute = g.Time.Clock.Minute; SessionState.SetString(Key + ".Minute", minute.ToString("R", System.Globalization.CultureInfo.InvariantCulture)); Phase(5);
                }
                else if (phase == 5 && g.Furniture.Ready)
                {
                    Require(g.Time.Clock.Minute == double.Parse(SessionState.GetString(Key + ".Minute", "0"), System.Globalization.CultureInfo.InvariantCulture), "Furniture preview pauses world");
                    g.Furniture.SetCandidate(2, 2); Preview.ConfirmButton.onClick.Invoke(); g.ConfirmPlacement();
                    Require(g.Furniture.State.Count == 1 && g.Furniture.Objects.Count == 1 && g.Inventory.GetCount("wood_table") == 0 && !g.UI.HasPlacement, "Single confirm consumes once");
                    Require(g.Saves.TryLoad(out var save) && save.Furniture.Length == 1 && save.Furniture[0].QuarterTurns == 1 && save.PlayerArea == HousingState.Home, "Furniture local position and rotation autosave");
                    At(g, 3, .2f); Begin(g, "wood_chair"); Phase(6);
                }
                else if (phase == 6 && g.Furniture.Ready && g.UI.HasPlacement && Preview != null)
                {
                    g.Furniture.SetCandidate(2, 2); Require(g.Furniture.CheckCandidate() == PlacementResult.Occupied, "Furniture overlap rejected");
                    g.Furniture.SetCandidate(3, 2); g.RotatePlacement(); g.RotatePlacement(); g.ConfirmPlacement(); Require(g.Furniture.State.Count == 2, "Chair placed at 180 degrees");
                    At(g, 3, -1.8f); Begin(g, "small_lamp"); Phase(7);
                }
                else if (phase == 7 && g.Furniture.Ready && g.UI.HasPlacement && Preview != null)
                {
                    g.Furniture.SetCandidate(3, 0); string dir = SessionState.GetString(Key + ".Dir", ""), blocked = Path.Combine(dir, "blocked"); File.WriteAllText(blocked, "file"); SaveDirectory(g, blocked);
                    g.ConfirmPlacement(); Require(g.Furniture.State.Count == 3 && g.Furniture.Feedback.Contains("保存失败") && g.Inventory.GetCount("small_lamp") == 0, "Failed save retains atomic furniture result");
                    var light = g.Furniture.Objects.Values.SelectMany(o => o.GetComponentsInChildren<Light>()).Single(); Require(light.enabled && light.cullingMask == 1 << g.Configuration.Housing.InteriorLayer, "Indoor lamp affects interior only");
                    SaveDirectory(g, dir); g.ToggleInventory(); Bag.SaveButton.onClick.Invoke(); Require(g.Saves.TryLoad(out var save) && save.Furniture.Length == 3, "Bag save retries furniture snapshot"); g.CloseInventory();
                    Capture(g, "FurnishedHomePreview"); At(g, -1.5f, 2); g.RequestRest(); Phase(8);
                }
                else if (phase == 8 && UnityEngine.Object.FindObjectOfType<SleepView>() != null)
                { UnityEngine.Object.FindObjectOfType<SleepView>().ConfirmButton.onClick.Invoke(); Require(g.Farm.State.Day == 2 && g.Furniture.State.Count == 3, "Bed remains accessible and sleep preserves furniture"); At(g, 0, -3); g.ExitHome(); Phase(9); }
                else if (phase == 9 && g.State == GameFlowState.Playing && !g.Housing.Inside)
                { Require(g.Furniture.Bound && g.Furniture.Objects.Count == 3 && g.Placements.State.Count == 0, "Exit retains indoor objects and independent outdoor state"); g.ReturnToMainMenu(); Phase(10); }
                else if (phase == 10 && g.State == GameFlowState.MainMenu)
                { Require(g.Furniture.State == null && !g.Furniture.Bound && g.Furniture.Objects.Count == 0, "Menu releases furniture state/instances/assets"); g.ContinueGame(); Phase(11); }
                else if (phase == 11 && g.State == GameFlowState.Playing)
                { Require(!g.Housing.Inside && g.Furniture.State.Count == 3 && !g.Furniture.Bound && !g.Scenes.HomeScene.IsValid(), "Outdoor load keeps indoor records lazy"); Door(g); g.EnterHome(); Phase(12); }
                else if (phase == 12 && g.State == GameFlowState.Playing && g.Housing.Inside)
                {
                    Require(g.Furniture.Objects.Count == 3 && g.Furniture.State.Capture().Single(r => r.ItemId == "wood_chair").QuarterTurns == 2, "AA furniture restoration exact");
                    var chair = g.Furniture.Objects.Values.Single(o => g.Furniture.State.Find(o.Id).ItemId == "wood_chair"); At(g, 3, .2f); Physics.SyncTransforms();
                    var interaction = UnityEngine.Object.FindObjectOfType<InteractionController>(); Require(interaction.RefreshDecorationHover(new Ray(chair.transform.position + Vector3.up * 4, Vector3.down), false) && interaction.DecorationHover == chair, "Indoor right-click furniture hover");
                    var slots = g.Inventory.State.Slots.ToArray(); g.Inventory.State.Restore(Enumerable.Repeat(new MagicIsland.Inventory.InventorySlot("wood", 99), g.Inventory.State.Capacity).ToArray()); g.RecoverDecoration(chair);
                    Require(g.Furniture.State.Count == 3 && g.Furniture.Objects.Count == 3, "Full bag recovery leaves object intact"); g.Inventory.State.Restore(slots);
                    g.RecoverDecoration(chair); g.RecoverDecoration(chair); Require(g.Furniture.State.Count == 2 && g.Inventory.GetCount("wood_chair") == 1, "Recovery returns exactly one and saves");
                    Begin(g, "wood_chair"); g.CancelPlacement(); Require(!g.Furniture.Active && g.Inventory.GetCount("wood_chair") == 1 && g.Furniture.State.Count == 2, "Cancel during load consumes nothing"); Phase(13);
                }
                else if (phase == 13 && g.State == GameFlowState.Playing && !g.UI.HasPlacement)
                { var recipe = g.Configuration.Crafting.FindItem("wood_chair"); recipe.PrefabAddress = "MissingFurniturePreview"; Begin(g, "wood_chair"); Phase(14); }
                else if (phase == 14 && g.State == GameFlowState.Playing && !g.Furniture.Active && !g.UI.HasPlacement)
                { Require(g.Inventory.GetCount("wood_chair") == 1 && g.Players.Player.enabled, "Missing AA preview recovers input and item"); g.Configuration.Crafting.FindItem("wood_chair").PrefabAddress = "WoodChair"; At(g, 0, -3); g.ExitHome(); Phase(26); }
                else if (phase == 26 && g.State == GameFlowState.Playing && !g.Housing.Inside)
                { Door(g); g.EnterHome(); Phase(27); }
                else if (phase == 27 && g.State == GameFlowState.Playing && g.Housing.Inside)
                { Require(g.Furniture.LastError == null && g.Furniture.Objects.Count == 2, "Room reentry clears prior preview error and retains furniture"); At(g, 3, .2f); Begin(g, "wood_chair"); Phase(15); }
                else if (phase == 15 && g.Furniture.Ready && g.UI.HasPlacement && Preview != null)
                { g.Furniture.SetCandidate(3, 2); g.ConfirmPlacement(); Require(g.Furniture.State.Count == 3, "AA preview retry succeeds"); At(g, 1, -1); Require(g.SaveProgress(), "Indoor position saved"); g.ReturnToMainMenu(); Phase(16); }
                else if (phase == 16 && g.State == GameFlowState.MainMenu)
                {
                    Require(g.Saves.TryLoad(out var save), "Current furniture save available"); SessionState.SetString(Key + ".Good", JsonUtility.ToJson(save));
                    save.Furniture[0].GridX = -3; save.Furniture[0].GridZ = 2; Write(g, JsonUtility.ToJson(save)); g.ContinueGame(); Phase(17);
                }
                else if (phase == 17 && g.State == GameFlowState.MainMenu)
                { Require(!g.Scenes.HomeScene.IsValid() && g.Furniture.State == null, "Saved furniture overlap rejects safely and unloads room"); Write(g, SessionState.GetString(Key + ".Good", "")); g.ContinueGame(); Phase(18); }
                else if (phase == 18 && g.State == GameFlowState.Playing)
                {
                    Require(g.Housing.Inside && g.Furniture.Objects.Count == 3 && (g.Housing.SavedPosition - new Vector3(1, .1f, -1)).sqrMagnitude < .05f, "Indoor load restores area, furniture and position");
                    g.ReturnToMainMenu(); Phase(19);
                }
                else if (phase == 19 && g.State == GameFlowState.MainMenu)
                { var old = JsonUtility.FromJson<SaveData>(SessionState.GetString(Key + ".Good", "")); old.Version = 17; old.Furniture = null; Write(g, JsonUtility.ToJson(old)); g.ContinueGame(); Phase(20); }
                else if (phase == 20 && g.State == GameFlowState.Playing)
                {
                    Require(g.Housing.Inside && g.Furniture.State.Count == 0 && g.Furniture.Objects.Count == 0 && g.Farm.State.Day == 2 && g.Inventory.GetCount("wood") == 21 && g.Farm.State.Cells[0].Tilled, "v17 migration adds empty furniture and preserves world/inventory/indoor area");
                    File.WriteAllText("Logs/FurnitureVerification.txt", "PASS: packed AA furniture crafting/pagination, placement, protected routes, collision, rotation, save/retry, sleep, lazy restore, recovery, cancellation, AA failure, overlap recovery, v17 migration and Chinese TMP.");
                    Debug.Log("Furniture flow verification PASSED."); Phase(21); EditorApplication.isPlaying = false;
                }
            }
            catch (Exception error) { SessionState.SetBool(Key, false); Debug.LogError("Furniture phase " + phase + ": " + error); EditorApplication.Exit(1); }
        }
    }
}
