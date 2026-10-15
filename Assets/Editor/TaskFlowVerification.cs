using System;
using System.IO;
using System.Linq;
using MagicIsland.Economy;
using MagicIsland.Farming;
using MagicIsland.Managers;
using MagicIsland.Saving;
using MagicIsland.Tasks;
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
    public static class TaskFlowVerification
    {
        private const string Running = "MagicIsland.TaskVerification";
        static TaskFlowVerification() { if (SessionState.GetBool(Running, false)) EditorApplication.update += Tick; }
        public static void Run()
        {
            PrototypeAddressablesSetup.EnsureSetup();
            EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity"); PrototypeAddressablesSetup.BuildContent();
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            settings.ActivePlayModeDataBuilderIndex = settings.DataBuilders.FindIndex(builder => builder is BuildScriptPackedPlayMode);
            SessionState.SetBool(Running, true); SessionState.SetInt(Running + ".Phase", 0);
            SessionState.SetFloat(Running + ".Started", (float)EditorApplication.timeSinceStartup);
            EditorApplication.update -= Tick; EditorApplication.update += Tick; EditorApplication.isPlaying = true;
        }
        private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
        private static void Phase(int phase) => SessionState.SetInt(Running + ".Phase", phase);
        private static TaskView View => UnityEngine.Object.FindObjectOfType<TaskView>();
        private static void CheckText(TaskView view)
        {
            foreach (var text in view.GetComponentsInChildren<TMPro.TMP_Text>())
            {
                text.ForceMeshUpdate(); Require(!text.isTextOverflowing, "Task text fits: " + text.name);
                Require(text.text.Where(c => !char.IsControl(c)).All(c => text.font.HasCharacter(c)), "Task Chinese glyphs supported");
            }
        }
        private static bool HitsTask(Vector2 point)
        {
            var results = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, results);
            return results.Any(hit => hit.gameObject.GetComponentInParent<TaskView>() != null);
        }
        private static void Capture(GameManager game, string file)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            GameUiPreview.CaptureInventory(game.Players.Player.ViewCamera); File.Copy("Logs/InventoryPreview.png", "Logs/" + file + ".png", true);
        }
        private static void Tick()
        {
            if (!SessionState.GetBool(Running, false)) return;
            int phase = SessionState.GetInt(Running + ".Phase", 0);
            if (phase == 11 && !EditorApplication.isPlaying)
            { SessionState.SetBool(Running, false); EditorApplication.Exit(0); return; }
            try
            {
                if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Running + ".Started", 0) > 150)
                    throw new Exception("Task verification timed out at phase " + phase);
                if (!EditorApplication.isPlaying) return;
                GameManager game = GameManager.Instance; if (game == null) return;
                Require(game.State != GameFlowState.Failed, "Game initialization failed");
                if (phase == 0 && game.State == GameFlowState.MainMenu)
                {
                    Require(game.Configuration.Tasks != null && game.Configuration.Tasks.IsValid && game.Configuration.Tasks.Tasks.Count == 6, "TaskCatalog loads via packed AA before menu");
                    var task = game.Configuration.Tasks.Tasks[0]; int amount = task.Required;
                    task.Required = 0; Require(!game.Configuration.Tasks.IsValid, "Invalid task count rejected"); task.Required = amount;
                    game.Configuration.Seasons.SpringRainChance = 0;
                    string directory = Path.GetFullPath("Logs/TaskSaves/" + Guid.NewGuid().ToString("N"));
                    game.Saves.Initialize(game.Configuration.Config, game.Configuration.Items, game.Configuration.Crops, directory, game.Configuration.Tasks, orders: game.Configuration.Orders, seasons: game.Configuration.Seasons, gathering: game.Configuration.Gathering, toolUpgrades: game.Configuration.ToolUpgrades, storage: game.Configuration.Storage, npcs: game.Configuration.NPCs, relationships: game.Configuration.Relationships, stories: game.Configuration.Stories, schedules: game.Configuration.NPCSchedules, fishing: game.Configuration.Fishing, crafting: game.Configuration.Crafting);
                    SessionState.SetString(Running + ".Directory", directory); game.EnterGame(); Phase(1);
                }
                else if (phase == 1 && game.State == GameFlowState.Playing)
                {
                    Require(View != null && View.Layer == UILayer.Common && View.transform.parent == game.UI.GetLayerRoot(UILayer.Common), "TaskPanel AA and Common layer");
                    game.UI.RefreshViews(); Require(!View.ClaimButton.interactable && View.TitleText.text == "第一次耕地", "MVC initial task");
                    Vector2 point = RectTransformUtility.WorldToScreenPoint(null, View.Card.TransformPoint(View.Card.rect.center));
                    Require(HitsTask(point), "Expanded card blocks world clicks"); View.CollapseButton.onClick.Invoke();
                    Require(!View.Details.activeSelf && View.Card.sizeDelta.y == 52 && !HitsTask(point), "Collapsed panel releases hidden area mouse input");
                    View.CollapseButton.onClick.Invoke(); CheckText(View); Capture(game, "TaskInitialPreview");
                    Require(!game.Farm.TryAct(0, FarmTool.WateringCan, out _) && game.Shop.Trade(false, 1) == TradeResult.InsufficientItems
                        && game.Tasks.State.GetProgress(2) == 0 && game.Tasks.State.GetProgress(5) == 0, "Failed farming/selling do not count");
                    Require(game.Shop.Trade(true, 1) == TradeResult.Success && game.Tasks.State.GetProgress(5) == 0, "Buying is not selling");
                    SessionState.SetString(Running + ".BaseGold", game.Economy.Gold.ToString());
                    Require(game.Farm.TryAct(0, FarmTool.Hoe, out _) && !game.Farm.TryAct(0, FarmTool.Hoe, out _), "Only successful hoe operation counts");
                    Require(game.Farm.TryAct(0, FarmTool.Seeds, out _) && game.Farm.TryAct(0, FarmTool.WateringCan, out _)
                        && !game.Farm.TryAct(0, FarmTool.WateringCan, out _), "Plant and water early, duplicate watering rejected");
                    Require(game.Tasks.State.CurrentIndex == 0 && game.Tasks.State.Ready && game.Tasks.State.GetProgress(1) == 1
                        && game.Tasks.State.GetProgress(2) == 1, "Early work remembered without skipping claim order");
                    game.Economy.Restore(long.MaxValue); game.ClaimTaskReward(); Require(!game.Tasks.State.IsClaimed(0) && game.Economy.Gold == long.MaxValue, "Reward overflow is atomic");
                    game.Economy.Restore(long.Parse(SessionState.GetString(Running + ".BaseGold", "0")));
                    game.UI.RefreshViews(); View.ClaimButton.onClick.Invoke();
                    Require(game.Tasks.State.CurrentIndex == 1 && game.Economy.Gold == 95 && game.Saves.TryLoad(out SaveData data)
                        && data.Version == SaveData.CurrentVersion && data.Gold == 95 && data.Tasks[0].Claimed && data.Tasks[1].Progress == 1, "Claim commits gold/flag and autosaves together");
                    game.ReturnToMainMenu(); Phase(2);
                }
                else if (phase == 2 && game.State == GameFlowState.MainMenu)
                {
                    Require(game.Tasks.State == null && View == null, "Task lifecycle releases state/UI and event bindings");
                    Require(game.Saves.TryLoad(out SaveData data), "Read task save"); string original = JsonUtility.ToJson(data);
                    data.Tasks[0].Id = "unknown"; string invalid = JsonUtility.ToJson(data);
                    File.WriteAllText(game.Saves.SavePath, invalid); File.WriteAllText(game.Saves.SavePath + ".bak", invalid);
                    game.ContinueGame(); Require(game.State == GameFlowState.MainMenu && !game.Saves.HasSave, "Invalid task save rejected before initialization");
                    File.WriteAllText(game.Saves.SavePath, original); File.WriteAllText(game.Saves.SavePath + ".bak", original);
                    game.ContinueGame(); Phase(3);
                }
                else if (phase == 3 && game.State == GameFlowState.Playing)
                {
                    Require(game.Tasks.State.CurrentIndex == 1 && game.Tasks.State.Ready && game.Tasks.State.GetProgress(2) == 1 && game.Economy.Gold == 95,
                        "Continue retains claim flags and early progress, with no repeated gold");
                    game.UI.RefreshViews(); View.ClaimButton.onClick.Invoke(); View.ClaimButton.onClick.Invoke();
                    Require(game.Tasks.State.CurrentIndex == 3 && !game.Tasks.State.Ready && game.Economy.Gold == 115, "Plant/water rewards unlock sleep");
                    game.ToggleInventory(); game.AdvanceDay(); game.StartNewDayFromReport(); game.ClaimTaskReward();
                    Require(game.Tasks.State.GetProgress(3) == 0 && game.Economy.Gold == 115, "Modal blocks day advance and task claim");
                    game.CloseInventory(); game.Players.RestorePosition(game.Housing.Entrance.Position + new Vector3(0, .1f, -1.4f)); game.EnterHome(); Phase(13);
                }
                else if (phase == 13 && game.State == GameFlowState.Playing && game.Housing.Inside)
                { game.Players.RestorePosition(game.Time.Bed.position + new Vector3(1.5f, .1f, 0)); game.OpenSleep(); Phase(4); }
                else if (phase == 4 && UnityEngine.Object.FindObjectOfType<SleepView>() != null)
                {
                    SleepView sleep = UnityEngine.Object.FindObjectOfType<SleepView>(); sleep.ConfirmButton.onClick.Invoke(); game.StartNewDayFromReport(); game.Players.RestorePosition(game.Housing.Interior.Exit.Position + new Vector3(0, .1f, 1.5f)); game.ExitHome(); Phase(14);
                }
                else if (phase == 14 && game.State == GameFlowState.Playing && !game.Housing.Inside) { Phase(5); }
                else if (phase == 5 && game.State == GameFlowState.Playing && !game.UI.HasSleep)
                {
                    Require(game.Tasks.State.Ready && game.Tasks.State.GetProgress(3) == 1 && game.Farm.State.Day == 2
                        && game.Saves.TryLoad(out SaveData data) && data.Tasks[3].Progress == 1, "Actual bed sleep counts once before daily autosave");
                    game.ClaimTaskReward(); Require(game.Tasks.State.CurrentIndex == 4 && game.Economy.Gold == 130, "Sleep reward");
                    game.Farm.TryAct(0, FarmTool.WateringCan, out _); game.AdvanceDay(); game.StartNewDayFromReport();
                    game.Farm.TryAct(0, FarmTool.WateringCan, out _); game.AdvanceDay(); game.StartNewDayFromReport();
                    Require(game.Farm.State.Cells[0].Mature && game.Tasks.State.GetProgress(4) == 0, "Maturity alone is not harvest");
                    Require(game.Farm.TryAct(0, FarmTool.Hoe, out _) && game.Tasks.State.GetProgress(4) == 1, "Mature crop operation records harvest, not hoe");
                    Require(!game.Farm.TryAct(0, FarmTool.Hoe, out _) && game.Tasks.State.GetProgress(4) == 1, "Failed repeated harvest does not count");
                    game.UI.RefreshViews(); CheckText(View); Capture(game, "TaskRewardPreview");
                    game.ClaimTaskReward(); Require(game.Tasks.State.CurrentIndex == 5 && game.Economy.Gold == 150, "Harvest reward unlocks sell");
                    Require(game.Shop.Trade(false, 1) == TradeResult.Success && game.Tasks.State.Ready && game.Economy.Gold == 175, "Successful sale records actual quantity");
                    Require(game.Shop.Trade(false, 1) == TradeResult.InsufficientItems && game.Tasks.State.GetProgress(5) == 1, "Failed sale does not duplicate progress");
                    game.ClaimTaskReward(); game.ClaimTaskReward();
                    Require(game.Tasks.State.Finished && game.Economy.Gold == 200 && game.Saves.TryLoad(out data) && data.Tasks.All(task => task.Claimed), "Full chain pays exactly 85 and cannot pay twice");
                    game.UI.RefreshViews(); CheckText(View); Capture(game, "TaskCompletedPreview"); game.ReturnToMainMenu(); Phase(6);
                }
                else if (phase == 6 && game.State == GameFlowState.MainMenu)
                { game.ContinueGame(); Phase(7); }
                else if (phase == 7 && game.State == GameFlowState.Playing)
                {
                    Require(game.Tasks.State.Finished && game.Economy.Gold == 200, "Completed tasks restore with no extra reward");
                    game.ClaimTaskReward(); Require(game.Economy.Gold == 200, "Repeated claim after reload free"); game.ReturnToMainMenu(); Phase(8);
                }
                else if (phase == 8 && game.State == GameFlowState.MainMenu)
                {
                    Require(game.Saves.TryLoad(out SaveData data), "Read completed save"); data.Version = 6; data.Tasks = null;
                    File.WriteAllText(game.Saves.SavePath, JsonUtility.ToJson(data)); File.WriteAllText(game.Saves.SavePath + ".bak", JsonUtility.ToJson(data));
                    Require(game.Saves.TryLoad(out SaveData legacy) && legacy.Version == SaveData.CurrentVersion && legacy.Gold == 200
                        && legacy.Tasks.All(task => !task.Claimed && task.Progress == 0), "v6 migration preserves world/gold and initializes unpaid tasks");
                    game.ContinueGame(); Phase(9);
                }
                else if (phase == 9 && game.State == GameFlowState.Playing)
                {
                    Require(game.Tasks.State.CurrentIndex == 0 && !game.Tasks.State.Ready && game.Economy.Gold == 200, "Legacy save opens fresh tutorial without paying");
                    game.ReturnToMainMenu(); Phase(10);
                }
                else if (phase == 10 && game.State == GameFlowState.MainMenu)
                {
                    File.WriteAllText("Logs/TaskFlowVerification.txt", "PASS: TaskCatalog and TaskPanel packed AA; Common layer, MVC, collapse and mouse interception, Chinese glyphs/layout.\n"
                        + "Successful-only farm/sale/bed reports, early work retention, ordered six-step claims, overflow protection, exactly 85 reward, reward autosave.\n"
                        + "Task save/resume, invalid task rejection, no repeat payment after reload, v6 migration preserving progress/gold, lifecycle cleanup.\n");
                    Debug.Log("Task flow verification PASSED."); Phase(11); EditorApplication.isPlaying = false;
                }
            }
            catch (Exception error) { SessionState.SetBool(Running, false); Debug.LogException(error); EditorApplication.Exit(1); }
        }
    }
}
