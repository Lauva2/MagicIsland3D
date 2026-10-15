using System;
using System.IO;
using System.Linq;
using MagicIsland.Farming;
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
    public static class HousingFlowVerification
    {
        const string Key = "MagicIsland.HousingVerification";
        static HousingFlowVerification() { if (SessionState.GetBool(Key, false)) EditorApplication.update += Tick; }
        public static void Run()
        {
            PrototypeAddressablesSetup.EnsureSetup(); PrototypeAddressablesSetup.ValidateUiPrefabs(); EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity"); PrototypeAddressablesSetup.BuildContent();
            var settings = AddressableAssetSettingsDefaultObject.Settings; settings.ActivePlayModeDataBuilderIndex = settings.DataBuilders.FindIndex(b => b is BuildScriptPackedPlayMode);
            SessionState.SetBool(Key, true); Phase(0); SessionState.SetFloat(Key + ".Start", (float)EditorApplication.timeSinceStartup);
            EditorApplication.update -= Tick; EditorApplication.update += Tick; EditorApplication.isPlaying = true;
        }
        static void Phase(int value) => SessionState.SetInt(Key + ".Phase", value);
        static void Require(bool ok, string message) { if (!ok) throw new Exception(message); }
        static void SaveDirectory(GameManager g, string dir) => g.Saves.Initialize(g.Configuration.Config, g.Configuration.Items, g.Configuration.Crops, dir, g.Configuration.Tasks,
            g.Configuration.Orders, g.Configuration.Seasons, g.Configuration.Gathering, g.Configuration.ToolUpgrades, g.Configuration.Storage, g.Configuration.NPCs, g.Configuration.Relationships,
            g.Configuration.Stories, g.Configuration.NPCSchedules, g.Configuration.Fishing, g.Configuration.Crafting);
        static void AtDoor(GameManager g) => g.Players.RestorePosition(g.Housing.Entrance.Position + new Vector3(0, .1f, -1.4f));
        static void AtBed(GameManager g) => g.Players.RestorePosition(g.Housing.Interior.Bed.transform.position + new Vector3(1.5f, .1f, 0));
        static void AtExit(GameManager g) => g.Players.RestorePosition(g.Housing.Interior.Exit.Position + new Vector3(0, .1f, 1.5f));
        static void Capture(GameManager g, string name)
        { UnityEngine.Object.FindObjectOfType<MagicIsland.CameraSystem.CameraController>().SnapToTarget(); g.UI.RefreshViews(); GameUiPreview.CaptureInventory(g.Players.Player.ViewCamera); File.Copy("Logs/InventoryPreview.png", "Logs/" + name + ".png", true); }
        static void TextCheck(Component view)
        { foreach (var t in view.GetComponentsInChildren<TMPro.TMP_Text>()) { t.ForceMeshUpdate(); Require(!t.isTextOverflowing && t.text.Where(c => !char.IsControl(c)).All(c => t.font.HasCharacter(c)), "Housing Chinese/layout: " + t.name + "=" + t.text); } }
        static void Write(GameManager g, SaveData data)
        { string json = JsonUtility.ToJson(data); File.WriteAllText(g.Saves.SavePath, json); File.WriteAllText(g.Saves.SavePath + ".bak", json); }
        static void Tick()
        {
            if (!SessionState.GetBool(Key, false)) return; int phase = SessionState.GetInt(Key + ".Phase", 0);
            if (phase == 23 && !EditorApplication.isPlaying) { SessionState.SetBool(Key, false); EditorApplication.Exit(0); return; }
            try
            {
                if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + ".Start", 0) > 240) throw new Exception("Housing timeout phase " + phase);
                if (!EditorApplication.isPlaying) return; var g = GameManager.Instance; if (g == null) return; Require(g.State != GameFlowState.Failed, "Housing configuration failed");
                if (phase == 0 && g.State == GameFlowState.MainMenu)
                {
                    Require(g.Configuration.Housing != null && g.Configuration.Housing.IsValid, "19th AA housing configuration");
                    string dir = Path.GetFullPath("Logs/HousingSaves/" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir); SessionState.SetString(Key + ".Dir", dir); SaveDirectory(g, dir); g.EnterGame(); Phase(1);
                }
                else if (phase == 1 && g.State == GameFlowState.Playing)
                {
                    Require(g.Housing.Entrance != null && !g.Housing.Inside && g.Time.Bed == null && !g.Scenes.HomeScene.IsValid() && UnityEngine.Object.FindObjectsOfType<SleepBed>().Length == 0, "Exterior replaces outdoor bed; indoor scene is lazy");
                    g.Players.RestorePosition(new Vector3(0, .1f, -4)); g.EnterHome(); Require(g.State == GameFlowState.Playing, "Distant door denied");
                    g.Farm.TryAct(0, FarmTool.Hoe, out _); g.Farm.TryAct(0, FarmTool.Seeds, out _); g.Farm.TryAct(0, FarmTool.WateringCan, out _);
                    g.Inventory.TryAdd("wood_fence", 2); g.Inventory.TryAdd("turnip_soup", 1); g.Stamina.Restore(40); g.Time.Clock.Restore(900);
                    g.NPCs.State.MarkMet("island_steward"); g.Relationships.State.Restore(new[] { new SavedRelationship { Id = "island_steward", Points = 30, LastGiftDay = 1 } });
                    g.ToggleInventory(); var bag = UnityEngine.Object.FindObjectOfType<InventoryView>(); int slot = Enumerable.Range(0, g.Inventory.State.Capacity).First(i => g.Inventory.State.Slots[i].ItemId == "wood_fence");
                    bag.Slots[slot].SelectButton.onClick.Invoke(); bag.PlaceButton.onClick.Invoke(); Phase(2);
                }
                else if (phase == 2 && g.Placements.Ready && UnityEngine.Object.FindObjectOfType<PlacementView>() != null)
                {
                    g.Players.RestorePosition(new Vector3(-6, .1f, -8.8f)); g.Placements.SetCandidate(-6, -7); g.ConfirmPlacement(); Require(g.Placements.State.Count == 1, "Prepare persisted decoration");
                    AtDoor(g); Physics.SyncTransforms(); var controller = UnityEngine.Object.FindObjectOfType<InteractionController>();
                    Require(controller.RefreshHomeHover(new Ray(g.Housing.Entrance.transform.position + Vector3.up * 5, Vector3.down), false) && controller.Hint.Contains("进门"), "Door hover/range exposes entry");
                    Capture(g, "PlayerHousePreview");
                    g.ToggleInventory(); g.EnterHome(); Require(g.State == GameFlowState.Playing && !g.Housing.Inside, "Modal entry denied"); g.CloseInventory();
                    g.EnterHome(); g.EnterHome(); g.ReturnToMainMenu(); Require(g.State == GameFlowState.Transitioning && !g.Players.Player.enabled && !g.Tools.InputEnabled, "Transition disables actions and duplicate entry");
                    SessionState.SetString(Key + ".Clock", g.Time.Clock.Minute.ToString("R", System.Globalization.CultureInfo.InvariantCulture)); Phase(3);
                }
                else if (phase == 3 && g.State == GameFlowState.Transitioning && UnityEngine.Object.FindObjectOfType<SceneTransitionView>() != null)
                {
                    Require(g.Time.Clock.Minute == double.Parse(SessionState.GetString(Key + ".Clock", "0"), System.Globalization.CultureInfo.InvariantCulture), "Clock pauses during scene load");
                    var view = UnityEngine.Object.FindObjectOfType<SceneTransitionView>(); Require(view.Layer == UILayer.Top, "Transition AA panel uses Top"); TextCheck(view); Phase(4);
                }
                else if ((phase == 3 || phase == 4) && g.State == GameFlowState.Playing && g.Housing.Inside)
                {
                    Require(g.Scenes.HomeScene.IsValid() && g.Time.Bed == g.Housing.Interior.Bed.transform && !g.Tools.InputEnabled && g.Players.Player.enabled
                        && g.Farm.State.Cells[0].Planted && g.Placements.State.Count == 1 && g.NPCs.State.HasMet("island_steward") && g.Relationships.State.Points("island_steward") == 30, "Entry preserves shared world and changes bed/input");
                    Require(g.Saves.TryLoad(out var save) && save.PlayerArea == HousingState.Home && Math.Abs(save.PlayerX) <= 5.1 && save.PlacedObjects.Length == 1, "Entry autosaves local indoor coordinates");
                    Require(UnityEngine.SceneManagement.SceneManager.GetActiveScene() == g.Scenes.GameScene && g.Players.Player.gameObject.scene == g.Scenes.GameScene, "Island scene and actor ownership retained");
                    g.UI.RefreshViews(); var hud = UnityEngine.Object.FindObjectOfType<PrototypeHud>(); Require(hud.LocationText.text == "我的小屋" && hud.ToolButtons.All(b => !b.interactable), "HUD location and indoor tools"); TextCheck(hud);
                    g.Players.RestorePosition(g.Configuration.Housing.InteriorOrigin + new Vector3(0, .1f, -2.5f)); g.Time.Clock.Restore(720);
                    g.NPCSchedules.ResetMorning(360); SessionState.SetString(Key + ".Clock", g.Time.Clock.Minute.ToString("R", System.Globalization.CultureInfo.InvariantCulture)); Phase(5);
                }
                else if (phase == 5 && g.State == GameFlowState.Playing && g.Housing.Inside)
                {
                    Require(g.Time.Clock.Minute > 720 && g.NPCSchedules.State.Distance("island_steward") > 0, "Clock and outdoor NPC schedule continue indoors");
                    g.ToggleInventory(); var bag = UnityEngine.Object.FindObjectOfType<InventoryView>(); int fence = Enumerable.Range(0, g.Inventory.State.Capacity).First(i => g.Inventory.State.Slots[i].ItemId == "wood_fence");
                    bag.Slots[fence].SelectButton.onClick.Invoke(); Require(bag.PlaceButton.gameObject.activeSelf && !bag.PlaceButton.interactable, "Indoor decorative placement remains farm-only"); TextCheck(bag);
                    bag.PlaceButton.onClick.Invoke(); Require(!g.Placements.Active && g.UI.IsInventoryVisible, "Indoor placement guard");
                    int food = Enumerable.Range(0, g.Inventory.State.Capacity).First(i => g.Inventory.State.Slots[i].ItemId == "turnip_soup"); bag.Slots[food].SelectButton.onClick.Invoke(); bag.UseFoodButton.onClick.Invoke();
                    Require(g.Stamina.State.Current == 70 && g.Inventory.GetCount("turnip_soup") == 0 && g.Saves.TryLoad(out var save) && save.PlayerArea == HousingState.Home, "Indoor food use saves local position with farm inventory");
                    g.CloseInventory(); AtBed(g); Physics.SyncTransforms(); var controller = UnityEngine.Object.FindObjectOfType<InteractionController>();
                    Require(controller.RefreshIndoorHover(new Ray(g.Time.Bed.position + Vector3.up * 5, Vector3.down), false) && controller.Hint.Contains("休息"), "Owned indoor bed hover");
                    Capture(g, "PlayerHomeInteriorPreview"); g.RequestRest(); Phase(6);
                }
                else if (phase == 6 && UnityEngine.Object.FindObjectOfType<SleepView>() != null)
                { TextCheck(UnityEngine.Object.FindObjectOfType<SleepView>()); UnityEngine.Object.FindObjectOfType<SleepView>().ConfirmButton.onClick.Invoke(); g.StartNewDayFromReport(); Phase(7); }
                else if (phase == 7 && g.State == GameFlowState.Playing && !g.UI.HasSleep)
                {
                    Require(g.Housing.Inside && g.Farm.State.Day == 2 && g.Time.Clock.Minute >= 360 && g.Time.Clock.Minute < 365 && g.Stamina.State.Current == 100 && g.Farm.State.Cells[0].Growth == 1
                        && g.NPCSchedules.State.Distance("island_steward") == 0 && g.Placements.State.Count == 1 && g.Relationships.State.Points("island_steward") == 30, "Indoor sleep advances shared day once without world reset");
                    UnityEngine.Time.timeScale = 0; Require(g.SaveProgress(), "Save exact indoor fixture"); var p = g.Housing.SavedPosition;
                    SessionState.SetString(Key + ".Position", JsonUtility.ToJson(p)); g.ReturnToMainMenu(); Phase(8);
                }
                else if (phase == 8 && g.State == GameFlowState.MainMenu)
                { Require(!g.Scenes.HomeScene.IsValid() && g.Housing.State == null && g.Housing.Entrance == null && UnityEngine.Object.FindObjectsOfType<HomeInterior>().Length == 0, "Return releases interior scene/exterior and housing state"); g.ContinueGame(); Phase(9); }
                else if (phase == 9 && g.State == GameFlowState.Playing)
                {
                    var p = JsonUtility.FromJson<Vector3>(SessionState.GetString(Key + ".Position", "{}"));
                    Require(g.Housing.Inside && Vector3.Distance(g.Housing.SavedPosition, p) < .0001f && g.Placements.State.Count == 1 && g.Farm.State.Cells[0].Growth == 1
                        && UnityEngine.Object.FindObjectsOfType<HomeInterior>().Length == 1 && UnityEngine.Object.FindObjectsOfType<HomePortal>().Length == 2, "Indoor reload exact and no duplicate rooms");
                    g.Players.RestorePosition(g.Configuration.Housing.InteriorOrigin + new Vector3(5.65f, .1f, -4.6f));
                    Require(g.SaveProgress(), "Legitimate wall/exit-edge position can save"); g.ReturnToMainMenu(); Phase(24);
                }
                else if (phase == 24 && g.State == GameFlowState.MainMenu) { g.ContinueGame(); Phase(25); }
                else if (phase == 25 && g.State == GameFlowState.Playing && g.Housing.Inside)
                {
                    Require(Vector3.Distance(g.Housing.SavedPosition, new Vector3(5.65f, .1f, -4.6f)) < .0001f, "Wall position reload respects CharacterController skin width");
                    UnityEngine.Time.timeScale = 1; AtExit(g); Physics.SyncTransforms(); var controller = UnityEngine.Object.FindObjectOfType<InteractionController>();
                    Require(controller.RefreshHomeHover(new Ray(g.Housing.Interior.Exit.Position + Vector3.up * 3, Vector3.down), false), "Exit hover selects portal");
                    g.ExitHome(); g.ExitHome(); Phase(10);
                }
                else if (phase == 10 && g.State == GameFlowState.Playing && !g.Housing.Inside)
                {
                    Require(g.Time.Bed == null && g.Tools.InputEnabled && g.Players.Player.gameObject.layer == 0 && g.Scenes.HomeScene.IsValid() && g.Placements.State.Count == 1, "Exit restores outdoor input/layers; room retained for reuse");
                    Require(g.Saves.TryLoad(out var save) && save.PlayerArea == HousingState.Island && save.PlayerX == g.Housing.SavedPosition.x, "Exit autosaves outside location");
                    int sceneCount = UnityEngine.SceneManagement.SceneManager.sceneCount; SessionState.SetInt(Key + ".Scenes", sceneCount); AtDoor(g); g.EnterHome(); Phase(11);
                }
                else if (phase == 11 && g.State == GameFlowState.Playing && g.Housing.Inside)
                {
                    Require(UnityEngine.SceneManagement.SceneManager.sceneCount == SessionState.GetInt(Key + ".Scenes", 0), "Repeated entry reuses AA room scene");
                    string blocked = Path.Combine(SessionState.GetString(Key + ".Dir", ""), "blocked"); File.WriteAllText(blocked, "file"); SaveDirectory(g, blocked); AtExit(g); g.ExitHome(); Phase(12);
                }
                else if (phase == 12 && g.State == GameFlowState.Playing && !g.Housing.Inside)
                {
                    Require(g.Interactions.Controller.Feedback.Contains("保存失败") && g.Placements.State.Count == 1 && g.Inventory.GetCount("wood_fence") == 1, "Transition save failure keeps coherent location and world");
                    SaveDirectory(g, SessionState.GetString(Key + ".Dir", "")); Require(g.SaveProgress(), "Save retry succeeds"); g.ReturnToMainMenu(); Phase(13);
                }
                else if (phase == 13 && g.State == GameFlowState.MainMenu)
                {
                    Require(g.Saves.TryLoad(out var save), "Read legacy fixture"); save.Version = 16; save.PlayerArea = null;
                    save.PlayerX = g.Configuration.Housing.ExteriorPosition(5).x; save.PlayerY = .1f; save.PlayerZ = -4.6f; Write(g, save);
                    Require(g.Saves.TryLoad(out var migrated) && migrated.Version == SaveData.CurrentVersion && migrated.PlayerArea == HousingState.Island && migrated.PlacedObjects.Length == 1
                        && migrated.NPCSchedules[0].Distance == save.NPCSchedules[0].Distance && migrated.Stamina == 100 && migrated.Day == 2, "v16 migration preserves world and adds outside area");
                    g.ContinueGame(); Phase(14);
                }
                else if (phase == 14 && g.State == GameFlowState.Playing)
                {
                    Require(!g.Housing.Inside && Vector3.Distance(g.Players.Player.transform.position, g.Housing.Entrance.transform.position) > 1 && g.Farm.State.Cells[0].Growth == 1, "Legacy overlap with old bed becomes safe door location");
                    string original = g.Configuration.Housing.InteriorSceneAddress; SessionState.SetString(Key + ".Address", original); g.Configuration.Housing.InteriorSceneAddress = "MissingPlayerHome"; AtDoor(g); g.EnterHome(); Phase(15);
                }
                else if (phase == 15 && g.State == GameFlowState.Playing)
                {
                    Require(!g.Housing.Inside && !g.Housing.State.Busy && !g.Scenes.HomeScene.IsValid() && g.Tools.InputEnabled && UnityEngine.Object.FindObjectOfType<SceneTransitionView>() == null, "Missing room AA failure restores outside and releases UI");
                    g.Configuration.Housing.InteriorSceneAddress = SessionState.GetString(Key + ".Address", ""); AtDoor(g); g.EnterHome(); Phase(16);
                }
                else if (phase == 16 && g.State == GameFlowState.Playing && g.Housing.Inside)
                { Require(g.Placements.State.Count == 1 && g.Farm.State.Day == 2, "Entry retry after load failure retains progress"); g.ReturnToMainMenu(); Phase(17); }
                else if (phase == 17 && g.State == GameFlowState.MainMenu)
                {
                    Require(g.Saves.TryLoad(out var save), "Read invalid area fixture"); save.PlayerArea = "unknown"; Write(g, save); Require(!g.Saves.TryLoad(out _), "Unknown area rejected in primary/backup");
                    save.PlayerArea = HousingState.Home; save.PlayerX = 40; Write(g, save); Require(!g.Saves.TryLoad(out _), "World-space indoor coordinates rejected");
                    save.PlayerX = 0; save.PlayerY = .1f; save.PlayerZ = -2.5f; Write(g, save); g.ContinueGame(); Phase(18);
                }
                else if (phase == 18 && g.State == GameFlowState.Playing && g.Housing.Inside)
                { Require(g.Placements.State.Count == 1, "Valid restored indoor save re-enters after rejection"); g.ReturnToMainMenu(); Phase(19); }
                else if (phase == 19 && g.State == GameFlowState.MainMenu)
                { g.Configuration.Config.FarmSize = 12; SaveDirectory(g, Path.Combine(SessionState.GetString(Key + ".Dir", ""), "MaxFarm")); g.EnterGame(); Phase(20); }
                else if (phase == 20 && g.State == GameFlowState.Playing)
                { Require(g.Housing.Entrance != null && g.NPCSchedules.Bound && g.Farm.State.Size == 12, "Cottage clears maximum farm and NPC routes"); AtDoor(g); g.EnterHome(); Phase(21); }
                else if (phase == 21 && g.State == GameFlowState.Playing && g.Housing.Inside)
                { AtExit(g); g.ExitHome(); Phase(22); }
                else if (phase == 22 && g.State == GameFlowState.Playing && !g.Housing.Inside)
                {
                    Require(g.Players.Player.transform.position.x < -7.2f && g.Tools.InputEnabled, "Maximum farm returns at correct cottage");
                    File.WriteAllText("Logs/HousingVerification.txt", "PASS: packed AA housing config, exterior model/interior prefab/scene/Top MVC transition UI; reach/modal/reentry guards, movement and transition pause, lazy load and scene reuse, indoor layers/bed/HUD/food, shared farm/NPC/decoration/relationship/time state, indoor sleep and saving, exact local-position reload, exterior return, save failure/retry, v16 migration and old-bed overlap, unknown area/invalid local-coordinate rejection, AA failure recovery/retry, cleanup and maximum farm.\n");
                    Debug.Log("Housing flow verification PASSED."); Phase(23); EditorApplication.isPlaying = false;
                }
            }
            catch (Exception error) { UnityEngine.Time.timeScale = 1; SessionState.SetBool(Key, false); Debug.LogException(error); EditorApplication.Exit(1); }
        }
    }
}
