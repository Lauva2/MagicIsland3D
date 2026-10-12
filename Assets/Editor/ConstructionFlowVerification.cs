using System;
using System.IO;
using System.Linq;
using MagicIsland.Interaction;
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
    public static class ConstructionFlowVerification
    {
        const string Key = "MagicIsland.ConstructionVerification";
        static ConstructionFlowVerification() { if (SessionState.GetBool(Key, false)) EditorApplication.update += Tick; }
        public static void Run()
        {
            PrototypeAddressablesSetup.EnsureSetup(); PrototypeAddressablesSetup.ValidateUiPrefabs();
            EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity"); PrototypeAddressablesSetup.BuildContent();
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            settings.ActivePlayModeDataBuilderIndex = settings.DataBuilders.FindIndex(b => b is BuildScriptPackedPlayMode);
            SessionState.SetBool(Key, true); Phase(0); SessionState.SetFloat(Key + ".Start", (float)EditorApplication.timeSinceStartup);
            EditorApplication.update -= Tick; EditorApplication.update += Tick; EditorApplication.isPlaying = true;
        }
        static void Phase(int value) => SessionState.SetInt(Key + ".Phase", value);
        static void Require(bool ok, string message) { if (!ok) throw new Exception(message); }
        static CraftingView CraftView => UnityEngine.Object.FindObjectOfType<CraftingView>();
        static PlacementView PlaceView => UnityEngine.Object.FindObjectOfType<PlacementView>();
        static InventoryView BagView => UnityEngine.Object.FindObjectOfType<InventoryView>();
        static void SaveDirectory(GameManager g, string directory) => g.Saves.Initialize(g.Configuration.Config, g.Configuration.Items, g.Configuration.Crops, directory,
            g.Configuration.Tasks, g.Configuration.Orders, g.Configuration.Seasons, g.Configuration.Gathering, g.Configuration.ToolUpgrades, g.Configuration.Storage,
            g.Configuration.NPCs, g.Configuration.Relationships, g.Configuration.Stories, g.Configuration.NPCSchedules, g.Configuration.Fishing, g.Configuration.Crafting);
        static int Slot(GameManager g, string item) => Enumerable.Range(0, g.Inventory.State.Capacity).First(i => g.Inventory.State.Slots[i].ItemId == item);
        static void Begin(GameManager g, string item)
        {
            if (!g.UI.IsInventoryVisible) g.ToggleInventory();
            BagView.Slots[Slot(g, item)].SelectButton.onClick.Invoke();
            Require(BagView.PlaceButton.gameObject.activeSelf && !BagView.UseFoodButton.gameObject.activeSelf && !BagView.UseSeedButton.gameObject.activeSelf, "Placeable-only bag action");
            TextCheck(BagView);
            BagView.PlaceButton.onClick.Invoke();
        }
        static void AtCell(GameManager g, int x, int z) => g.Players.RestorePosition(new Vector3(x, .1f, z - 1.8f));
        static void TextCheck(Component view)
        { foreach (var t in view.GetComponentsInChildren<TMPro.TMP_Text>()) { t.ForceMeshUpdate(); Require(!t.isTextOverflowing && t.text.Where(c => !char.IsControl(c)).All(c => t.font.HasCharacter(c)), "Construction Chinese/layout: " + t.name + "=" + t.text); } }
        static void Capture(GameManager g, string name)
        { UnityEngine.Object.FindObjectOfType<MagicIsland.CameraSystem.CameraController>().SnapToTarget(); g.UI.RefreshViews(); GameUiPreview.CaptureInventory(g.Players.Player.ViewCamera); File.Copy("Logs/InventoryPreview.png", "Logs/" + name + ".png", true); }
        static void Write(GameManager g, SaveData save)
        { string json = JsonUtility.ToJson(save); File.WriteAllText(g.Saves.SavePath, json); File.WriteAllText(g.Saves.SavePath + ".bak", json); }
        static void Tick()
        {
            if (!SessionState.GetBool(Key, false)) return; int phase = SessionState.GetInt(Key + ".Phase", 0);
            if (phase == 20 && !EditorApplication.isPlaying) { SessionState.SetBool(Key, false); EditorApplication.Exit(0); return; }
            try
            {
                if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + ".Start", 0) > 240) throw new Exception("Construction timeout phase " + phase);
                if (!EditorApplication.isPlaying) return; var g = GameManager.Instance; if (g == null) return;
                Require(g.State != GameFlowState.Failed, "Construction config failure");
                if (phase == 0 && g.State == GameFlowState.MainMenu)
                {
                    Require(g.Configuration.Crafting.Validate(g.Configuration.Items) && g.Configuration.Crafting.Recipes.Count(r => !r.IsFurniture) == 3, "18th AA config has three recipes/placeables");
                    var r = g.Configuration.Crafting.Recipes[0]; string material = r.Ingredients[0].ItemId;
                    r.Ingredients[0].ItemId = "sardine"; Require(!g.Configuration.Crafting.Validate(g.Configuration.Items), "Fish cannot replace material recipe"); r.Ingredients[0].ItemId = material;
                    string dir = Path.GetFullPath("Logs/ConstructionSaves/" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir); SessionState.SetString(Key + ".Dir", dir); SaveDirectory(g, dir); g.EnterGame(); Phase(1);
                }
                else if (phase == 1 && g.State == GameFlowState.Playing)
                {
                    Require(g.Placements.State.Count == 0 && g.Placements.Objects.Count == 0, "New game grants no decorations");
                    g.Inventory.TryAdd("wood", 20); g.Inventory.TryAdd("stone", 20); g.Time.Clock.Restore(700);
                    g.Players.RestorePosition(g.ToolUpgrades.Workbench.transform.position + new Vector3(0, .1f, -1.7f)); Physics.SyncTransforms();
                    var interaction = UnityEngine.Object.FindObjectOfType<InteractionController>();
                    Require(interaction.RefreshWorkbenchHover(new Ray(g.ToolUpgrades.Workbench.transform.position + Vector3.up * 4, Vector3.down), false) && interaction.Hint.Contains("制作"), "Workbench offers construction");
                    g.OpenCrafting(); Phase(2);
                }
                else if (phase == 2 && CraftView != null)
                {
                    Require(CraftView.Layer == UILayer.PopUp && CraftView.CraftButtons.Length == 3 && CraftView.CraftButtons.All(b => b.interactable), "AA MVC three-card panel"); TextCheck(CraftView); Capture(g, "CraftingPanelPreview");
                    string dir = SessionState.GetString(Key + ".Dir", ""), blocked = Path.Combine(dir, "blocked"); File.WriteAllText(blocked, "file"); SaveDirectory(g, blocked);
                    Action observer = () => { Require(g.Inventory.GetCount("wood") == 18 && g.Inventory.GetCount("wood_fence") == 1, "Craft atomic before observers"); g.CraftRecipe(0); g.ReturnToMainMenu(); g.CloseCrafting(); };
                    g.Inventory.State.Changed += observer; CraftView.CraftButtons[0].onClick.Invoke(); g.Inventory.State.Changed -= observer;
                    Require(g.UI.HasCrafting && g.State == GameFlowState.Playing && g.Inventory.GetCount("wood_fence") == 1 && CraftingFeedback(g).Contains("保存失败"), "Craft save failure retains one item and prevents reentry");
                    SaveDirectory(g, dir); CraftView.SaveButton.onClick.Invoke(); Require(g.Saves.TryLoad(out var saved) && saved.Version == SaveData.CurrentVersion && saved.PlacedObjects.Length == 0, "Craft retry saved current schema");
                    CraftView.CraftButtons[0].onClick.Invoke(); CraftView.CraftButtons[0].onClick.Invoke(); CraftView.CraftButtons[1].onClick.Invoke(); CraftView.CraftButtons[1].onClick.Invoke(); CraftView.CraftButtons[2].onClick.Invoke();
                    Require(g.Inventory.GetCount("wood") == 11 && g.Inventory.GetCount("stone") == 14 && g.Inventory.GetCount("wood_fence") == 3 && g.Inventory.GetCount("stone_path") == 2 && g.Inventory.GetCount("garden_lamp") == 1, "Three recipes exact consumption/yield");
                    var bag = g.Inventory.State.Slots.ToArray(); var full = Enumerable.Repeat(new InventorySlot("wood", 99), g.Inventory.State.Capacity).ToArray(); g.Inventory.State.Restore(full);
                    g.CraftRecipe(0); Require(g.Inventory.GetCount("wood") == 99 * full.Length && g.Inventory.GetCount("wood_fence") == 0, "Full bag craft consumes nothing");
                    full[0] = new InventorySlot("stone", 99); g.Inventory.State.Restore(full); g.CraftRecipe(2); Require(g.Inventory.GetCount("stone") == 99, "Full bag multi-material craft atomic");
                    g.Inventory.State.Restore(bag); Require(g.Stamina.State.Current == 100 && g.Economy.Gold == 100 && g.Tasks.State.Capture().All(t => t.Progress == 0), "Craft no stamina/gold/task effects");
                    SessionState.SetString(Key + ".Clock", g.Time.Clock.Minute.ToString("R", System.Globalization.CultureInfo.InvariantCulture)); Phase(3);
                }
                else if (phase == 3 && CraftView != null)
                {
                    Require(g.Time.Clock.Minute == double.Parse(SessionState.GetString(Key + ".Clock", "0"), System.Globalization.CultureInfo.InvariantCulture) && !g.Tools.InputEnabled, "Craft modal pauses world");
                    CraftView.UpgradeButton.onClick.Invoke(); Phase(4);
                }
                else if (phase == 4 && UnityEngine.Object.FindObjectOfType<ToolUpgradeView>() != null)
                { UnityEngine.Object.FindObjectOfType<ToolUpgradeView>().CraftingButton.onClick.Invoke(); Phase(5); }
                else if (phase == 5 && CraftView != null)
                { Require(!g.UI.HasToolUpgrade, "Worktable tabs mutually exclusive"); g.CloseCrafting(); g.CraftRecipe(0); Require(g.Inventory.GetCount("wood_fence") == 3, "Closed panel cannot craft"); Begin(g, "wood_fence"); Phase(6); }
                else if (phase == 6 && PlaceView != null && g.Placements.Ready)
                {
                    Require(PlaceView.Layer == UILayer.Common && g.Players.Player.enabled && !g.Tools.InputEnabled && !g.UI.IsInventoryVisible && g.Inventory.GetCount("wood_fence") == 3, "Preview permits movement, disables farm, consumes nothing");
                    AtCell(g, -6, -7);
                    foreach (var p in new[] { new Vector2Int(0, 0), new Vector2Int(11, 0), new Vector2Int(12, -7), new Vector2Int(-11, -7) })
                    { g.Placements.SetCandidate(p.x, p.y); Require(g.Placements.CheckCandidate() != PlacementResult.Success, "Farm/route/boundary/reach reject"); g.ConfirmPlacement(); }
                    g.Players.RestorePosition(new Vector3(-6, .1f, -7)); g.Placements.SetCandidate(-6, -7); Require(g.Placements.CheckCandidate() == PlacementResult.Blocked, "Player blocks placement");
                    AtCell(g, -6, -7); var obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube); obstacle.transform.position = new Vector3(-6, .8f, -7); obstacle.transform.localScale = new Vector3(.4f, 1.6f, .4f);
                    g.Placements.SetCandidate(-6, -7); Require(g.Placements.CheckCandidate() == PlacementResult.Blocked, "Physical geometry blocks placement"); UnityEngine.Object.DestroyImmediate(obstacle);
                    g.Players.RestorePosition(new Vector3(6, .1f, -1.8f)); g.Placements.SetCandidate(6, 0); Require(g.Placements.CheckCandidate() == PlacementResult.Blocked, "Worktable space protected");
                    g.Players.RestorePosition(new Vector3(10, .1f, -10)); g.Placements.SetCandidate(11, -9); Require(g.Placements.CheckCandidate() == PlacementResult.Blocked, "Water trigger cannot receive decoration");
                    AtCell(g, -6, -7); g.Placements.UpdatePreview(new Ray(new Vector3(-6, 10, -7), Vector3.down), false); PlaceView.RotateButton.onClick.Invoke(); g.UI.RefreshViews();
                    Require(g.Placements.CheckCandidate() == PlacementResult.Success && PlaceView.ConfirmButton.interactable && g.Placements.QuarterTurns == 1 && g.Placements.Feedback == null, "Ray preview snaps, rotates and clears stale rejection feedback");
                    var ghost = UnityEngine.Object.FindObjectsOfType<PlacedDecoration>(true).Single(a => a.Id == 0);
                    Require(ghost.GetComponentsInChildren<Collider>(true).All(c => !c.enabled), "Preview cannot collide");
                    var tint = new MaterialPropertyBlock(); ghost.GetComponentInChildren<Renderer>().GetPropertyBlock(tint);
                    Require(tint.GetColor("_Color").g > .8f, "Valid preview is green");
                    g.Placements.SetCandidate(0, 0); ghost.GetComponentInChildren<Renderer>().GetPropertyBlock(tint); Require(tint.GetColor("_Color").r > .9f, "Invalid preview is red");
                    g.Placements.SetCandidate(-6, -7);
                    g.Players.RestorePosition(new Vector3(-6, .1f, -7)); g.Placements.UpdatePreview(new Ray(Vector3.up * 10, Vector3.down), true);
                    Require(g.Placements.GridX == -6 && g.Placements.GridZ == -7 && g.Placements.Result == PlacementResult.Blocked, "UI hover preserves position and refreshes moving-player collision");
                    AtCell(g, -6, -7); g.Placements.UpdatePreview(new Ray(Vector3.up * 10, Vector3.down), true);
                    Require(g.Placements.Result == PlacementResult.Success, "UI button candidate resumes validity after player moves");
                    g.UI.RefreshViews(); TextCheck(PlaceView); Capture(g, "PlacementPreview");
                    SessionState.SetString(Key + ".Clock", g.Time.Clock.Minute.ToString("R", System.Globalization.CultureInfo.InvariantCulture)); Phase(7);
                }
                else if (phase == 7 && PlaceView != null)
                {
                    Require(g.Time.Clock.Minute == double.Parse(SessionState.GetString(Key + ".Clock", "0"), System.Globalization.CultureInfo.InvariantCulture), "Placement pauses clock across frames");
                    // A real mouse may move in batch mode; explicitly restore the test candidate before the click.
                    g.Placements.SetCandidate(-6, -7); g.UI.RefreshViews(); SaveDirectory(g, Path.Combine(SessionState.GetString(Key + ".Dir", ""), "blocked"));
                    Action observer = () => { Require(g.Placements.State.Count == 1 && g.Placements.Objects.Count == 1 && g.Inventory.GetCount("wood_fence") == 2, "Scene, placement and inventory committed before observers"); g.ConfirmPlacement(); g.CancelPlacement(); g.ReturnToMainMenu(); };
                    g.Inventory.State.Changed += observer; PlaceView.ConfirmButton.onClick.Invoke(); g.Inventory.State.Changed -= observer;
                    Require(g.Placements.State.Count == 1 && !g.Placements.Active && !g.UI.HasPlacement && g.Placements.Feedback.Contains("保存失败"), "Place save failure preserves one committed object");
                    var fence = g.Placements.Objects.Values.Single(); Require(fence.GetComponent<BoxCollider>().enabled && Mathf.Abs(fence.transform.localEulerAngles.y - 90) < .01f, "Committed collider and orientation");
                    SaveDirectory(g, SessionState.GetString(Key + ".Dir", "")); Require(g.SaveProgress() && g.Saves.TryLoad(out var save) && save.PlacedObjects.Length == 1 && save.PlacedObjects[0].QuarterTurns == 1, "Retry saves placement with remaining inventory");
                    Begin(g, "wood_fence"); g.CancelPlacement(); Phase(8);
                }
                else if (phase == 8 && !g.Placements.Active && !g.UI.HasPlacement)
                { Require(g.Inventory.GetCount("wood_fence") == 2 && g.Placements.State.Count == 1 && g.Tools.InputEnabled, "Cancel during AA loading restores input without item cost"); Begin(g, "stone_path"); Phase(9); }
                else if (phase == 9 && PlaceView != null && g.Placements.Ready)
                {
                    AtCell(g, -6, -7); g.Placements.SetCandidate(-6, -7); Require(g.Placements.CheckCandidate() == PlacementResult.Occupied, "Shared-cell object blocks path");
                    AtCell(g, -7, -7); g.Placements.SetCandidate(-7, -7); g.RotatePlacement(); g.RotatePlacement(); g.ConfirmPlacement();
                    Require(g.Placements.State.Count == 2 && g.Inventory.GetCount("stone_path") == 1, "Stone path placed once"); Begin(g, "garden_lamp"); Phase(10);
                }
                else if (phase == 10 && PlaceView != null && g.Placements.Ready)
                {
                    AtCell(g, -3, -7); g.Placements.SetCandidate(-3, -7); g.RotatePlacement(); g.RotatePlacement(); g.RotatePlacement(); g.ConfirmPlacement();
                    Require(g.Placements.State.Count == 3 && g.Inventory.GetCount("garden_lamp") == 0 && g.Placements.Objects.Values.Any(a => a.GetComponentInChildren<Light>() != null && a.GetComponentInChildren<Light>().enabled), "Garden lamp commits live light");
                    Capture(g, "FarmDecorationsPreview"); g.ReturnToMainMenu(); Phase(11);
                }
                else if (phase == 11 && g.State == GameFlowState.MainMenu)
                { Require(g.Placements.State == null && g.Placements.Objects.Count == 0 && UnityEngine.Object.FindObjectsOfType<PlacedDecoration>().Length == 0, "Exit releases placed AA actors"); g.ContinueGame(); Phase(12); }
                else if (phase == 12 && g.State == GameFlowState.Playing)
                {
                    Require(g.Placements.State.Count == 3 && g.Placements.Objects.Count == 3 && g.Placements.State.Capture().Select(p => p.QuarterTurns).SequenceEqual(new[] { 1, 2, 3 }), "Reload restores exact records/rotations");
                    var fence = g.Placements.Objects.Values.First(a => g.Placements.State.Find(a.Id).ItemId == "wood_fence"); AtCell(g, -6, -7); Physics.SyncTransforms();
                    var controller = UnityEngine.Object.FindObjectOfType<InteractionController>(); Require(controller.RefreshDecorationHover(new Ray(fence.transform.position + Vector3.up * 4, Vector3.down), false) && controller.Hint.Contains("回收"), "Owned object hover exposes recovery");
                    var slots = g.Inventory.State.Slots.ToArray(); g.Inventory.State.Restore(Enumerable.Repeat(new InventorySlot("wood", 99), g.Inventory.State.Capacity).ToArray()); g.RecoverDecoration(fence);
                    Require(g.Placements.State.Count == 3 && g.Placements.Objects.Count == 3 && fence.gameObject.activeSelf, "Full bag cannot destroy object");
                    g.Inventory.State.Restore(slots); g.Players.RestorePosition(new Vector3(0, .1f, -4)); g.RecoverDecoration(fence); Require(g.Placements.State.Count == 3, "Distant recovery rejected");
                    AtCell(g, -6, -7); g.RecoverDecoration(fence); g.RecoverDecoration(fence);
                    Require(g.Placements.State.Count == 2 && g.Inventory.GetCount("wood_fence") == 3 && g.Saves.TryLoad(out var save) && save.PlacedObjects.Length == 2, "Recovery saves one returned item exactly once");
                    g.ReturnToMainMenu(); Phase(13);
                }
                else if (phase == 13 && g.State == GameFlowState.MainMenu)
                {
                    Require(g.Saves.TryLoad(out var save), "Read migration fixture"); long wood = save.Slots.Sum(s => s.ItemId == "wood" ? s.Quantity : 0); save.Version = 15; save.PlacedObjects = null; Write(g, save);
                    Require(g.Saves.TryLoad(out var migrated) && migrated.Version == SaveData.CurrentVersion && migrated.PlacedObjects.Length == 0 && migrated.Slots.Sum(s => s.ItemId == "wood" ? s.Quantity : 0) == wood
                        && migrated.NPCSchedules[0].Distance == save.NPCSchedules[0].Distance && migrated.Gold == save.Gold, "v15 migration no free objects/resources or NPC reset"); g.ContinueGame(); Phase(14);
                }
                else if (phase == 14 && g.State == GameFlowState.Playing)
                { Require(g.Placements.State.Count == 0 && g.Placements.Objects.Count == 0, "Migrated world starts without decorative objects"); g.ReturnToMainMenu(); Phase(15); }
                else if (phase == 15 && g.State == GameFlowState.MainMenu)
                {
                    Require(g.Saves.TryLoad(out var save), "Read corrupt fixture"); save.PlacedObjects = new[] { new SavedPlacedObject { Id = 1, ItemId = "wood_fence", GridX = 11, GridZ = 0 } }; Write(g, save);
                    Require(!g.Saves.TryLoad(out _), "Route-blocking saved object rejected in both files"); g.ContinueGame(); Phase(16);
                }
                else if (phase == 16 && g.State == GameFlowState.MainMenu)
                {
                    Require(g.Placements.State == null && g.Placements.Objects.Count == 0, "Corrupt save leaves no partial world");
                    var save = JsonUtility.FromJson<SaveData>(File.ReadAllText(g.Saves.SavePath)); save.PlacedObjects[0].GridX = 6; save.PlacedObjects[0].GridZ = 0; Write(g, save);
                    Require(g.Saves.TryLoad(out _), "Static facility conflict reaches world validation"); g.ContinueGame(); Phase(21);
                }
                else if (phase == 21 && g.State == GameFlowState.MainMenu)
                {
                    Require(g.Placements.State == null && g.Placements.Objects.Count == 0 && UnityEngine.Object.FindObjectsOfType<PlacedDecoration>().Length == 0, "Saved facility conflict cleans all actors and returns to menu");
                    g.Configuration.Config.FarmSize = 12; SaveDirectory(g, Path.Combine(SessionState.GetString(Key + ".Dir", ""), "MaxFarm")); g.EnterGame(); Phase(17);
                }
                else if (phase == 17 && g.State == GameFlowState.Playing)
                { g.Inventory.TryAdd("wood_fence", 1); Begin(g, "wood_fence"); Phase(18); }
                else if (phase == 18 && PlaceView != null && g.Placements.Ready)
                {
                    AtCell(g, -10, -3); g.Placements.SetCandidate(-10, -3); Require(g.Placements.CheckCandidate() == PlacementResult.Success, "Maximum farm has usable construction area: " + g.Placements.CheckCandidate()); g.ConfirmPlacement();
                    Require(g.Placements.State.Count == 1 && g.Farm.State.Size == 12 && g.NPCSchedules.Bound, "Construction coexists with maximum farm and NPC routes"); g.ReturnToMainMenu(); Phase(19);
                }
                else if (phase == 19 && g.State == GameFlowState.MainMenu)
                {
                    File.WriteAllText("Logs/ConstructionVerification.txt", "PASS: packed AA recipes/three models/MVC panels, exact atomic crafting, save failure/retry and reentry, materials/bag capacity, workbench upgrade tabs, Chinese layout, inventory place action, ray/grid/rotation/color preview, movement and modal pauses, farm/NPC/facility/water/player/boundary/reach/occupied guards, confirm/cancel, live lamp, exact save/reload/rotation, recovery/full bag/distance/double recovery, v15 migration, corrupt route save rejection, AA cleanup, maximum farm.\n");
                    Debug.Log("Construction flow verification PASSED."); Phase(20); EditorApplication.isPlaying = false;
                }
            }
            catch (Exception error) { SessionState.SetBool(Key, false); Debug.LogException(error); EditorApplication.Exit(1); }
        }
        static string CraftingFeedback(GameManager g) => g.Crafting.Feedback ?? "";
    }
}
