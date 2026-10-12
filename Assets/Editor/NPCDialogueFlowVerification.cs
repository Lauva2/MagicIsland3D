using System;
using System.IO;
using System.Linq;
using MagicIsland.CalendarSystem;
using MagicIsland.Farming;
using MagicIsland.Interaction;
using MagicIsland.Managers;
using MagicIsland.Orders;
using MagicIsland.Saving;
using MagicIsland.UI.Views;
using MagicIsland.WeatherSystem;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace MagicIsland.EditorTools
{
    [InitializeOnLoad]
    public static class NPCDialogueFlowVerification
    {
        const string Key = "MagicIsland.NPCVerification", Id = "island_steward";
        static NPCDialogueFlowVerification() { if (SessionState.GetBool(Key, false)) EditorApplication.update += Tick; }
        public static void Run()
        {
            PrototypeAddressablesSetup.EnsureSetup(); EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity"); PrototypeAddressablesSetup.BuildContent();
            var settings = AddressableAssetSettingsDefaultObject.Settings; settings.ActivePlayModeDataBuilderIndex = settings.DataBuilders.FindIndex(b => b is BuildScriptPackedPlayMode);
            SessionState.SetBool(Key, true); Phase(0); SessionState.SetFloat(Key + ".Start", (float)EditorApplication.timeSinceStartup);
            EditorApplication.update -= Tick; EditorApplication.update += Tick; EditorApplication.isPlaying = true;
        }
        static void Phase(int value) => SessionState.SetInt(Key + ".Phase", value);
        static void Require(bool ok, string message) { if (!ok) throw new Exception(message); }
        static DialogueView View => UnityEngine.Object.FindObjectOfType<DialogueView>();
        static NPCActor Actor(GameManager g) => g.NPCs.Actors[Id];
        static void Place(GameManager g) => g.Players.RestorePosition(Actor(g).transform.position + new Vector3(1.2f, .1f, -.8f));
        static void SaveDirectory(GameManager g, string dir) => g.Saves.Initialize(g.Configuration.Config, g.Configuration.Items, g.Configuration.Crops, dir,
            g.Configuration.Tasks, g.Configuration.Orders, g.Configuration.Seasons, g.Configuration.Gathering, g.Configuration.ToolUpgrades, g.Configuration.Storage, g.Configuration.NPCs, g.Configuration.Relationships, g.Configuration.Stories, g.Configuration.NPCSchedules, g.Configuration.Fishing, g.Configuration.Crafting);
        static void Capture(GameManager g, string name) { GameUiPreview.CaptureInventory(g.Players.Player.ViewCamera); File.Copy("Logs/InventoryPreview.png", "Logs/" + name + ".png", true); }
        static void CheckText()
        { foreach (var text in View.GetComponentsInChildren<TMPro.TMP_Text>()) { text.ForceMeshUpdate(); Require(!text.isTextOverflowing && text.text.Where(c => !char.IsControl(c)).All(c => text.font.HasCharacter(c)), "Dialogue Chinese/layout: " + text.name); } }
        static void Finish(GameManager g)
        {
            int limit = 32; while (g.UI.HasDialogue && limit-- > 0) View.NextButton.onClick.Invoke(); Require(!g.UI.HasDialogue && !g.Dialogue.State.Active, "Dialogue finishes");
        }
        static void Write(GameManager g, SaveData data) { var json = JsonUtility.ToJson(data); File.WriteAllText(g.Saves.SavePath, json); File.WriteAllText(g.Saves.SavePath + ".bak", json); }
        static void Tick()
        {
            if (!SessionState.GetBool(Key, false)) return; int phase = SessionState.GetInt(Key + ".Phase", 0);
            if (phase == 13 && !EditorApplication.isPlaying) { SessionState.SetBool(Key, false); EditorApplication.Exit(0); return; }
            try
            {
                if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + ".Start", 0) > 180) throw new Exception("NPC timeout phase " + phase);
                if (!EditorApplication.isPlaying) return; var g = GameManager.Instance; if (g == null) return; Require(g.State != GameFlowState.Failed, "Configuration failure");
                if (phase == 0 && g.State == GameFlowState.MainMenu)
                {
                    Require(g.Configuration.NPCs.Validate(g.Configuration.Config) && g.Configuration.Dialogues.Validate(g.Configuration.NPCs), "NPC and dialogue catalogs packed AA before menu");
                    int size = g.Configuration.Config.FarmSize; for (int i = 1; i <= 12; i++) { g.Configuration.Config.FarmSize = i; Require(g.Configuration.NPCs.Validate(g.Configuration.Config), "Actor clears all farm sizes"); } g.Configuration.Config.FarmSize = size;
                    string dir = Path.GetFullPath("Logs/NPCSaves/" + Guid.NewGuid().ToString("N")); SessionState.SetString(Key + ".Dir", dir); SaveDirectory(g, dir); g.EnterGame(); Phase(1);
                }
                else if (phase == 1 && g.State == GameFlowState.Playing)
                {
                    Require(g.NPCs.Actors.Count == 1 && UnityEngine.Object.FindObjectsOfType<NPCActor>().Length == 1 && !g.NPCs.State.HasMet(Id), "One AA NPC, initially unmet");
                    var point = Actor(g).transform.position; Physics.SyncTransforms(); var ray = new Ray(point + Vector3.up * 5, Vector3.down); g.Players.RestorePosition(new Vector3(0, .1f, -10));
                    Require(g.Interactions.Controller.RefreshNPCHover(ray, false) && g.Interactions.Controller.NPCHoverId == Id && g.Interactions.Controller.HoverIndex == -1
                        && !g.Interactions.Controller.RefreshNPCHover(ray, true), "NPC consumes farm ray and UI protection");
                    g.OpenDialogue(Id); g.OpenDialogue("missing"); Require(!g.UI.HasDialogue && !g.Dialogue.State.Active, "Distance/unknown NPC free");
                    var node = g.Gathering.Nodes[0]; g.Players.RestorePosition(node.transform.position + new Vector3(1.5f, .1f, 0)); g.Tools.SelectTool(FarmTool.Axe); Require(g.TryGather(0, out _), "Start tool swing");
                    Place(g); g.OpenDialogue(Id); Require(!g.UI.HasDialogue, "Busy swing blocks talking"); Phase(2);
                }
                else if (phase == 2 && !g.Tools.Busy)
                {
                    var point = Actor(g).transform.position; var camera = g.Players.Player.ViewCamera; camera.transform.position = point + new Vector3(5, 5, -8); camera.transform.LookAt(point + Vector3.up * 1.1f);
                    Actor(g).NameText.transform.rotation = camera.transform.rotation; Capture(g, "IslandStewardPreview"); g.OpenDialogue(Id); g.OpenDialogue(Id); Phase(3);
                }
                else if (phase == 3 && g.UI.HasDialogue && View != null)
                {
                    Require(View.transform.parent == g.UI.GetLayerRoot(UILayer.PopUp) && g.Dialogue.State.Lines.Count == 5 && g.Dialogue.State.Index == 0 && View.SpeakerText.text.Contains("林舟"), "Intro MVC, layer and no duplicated open");
                    CheckText(); Capture(g, "DialogueIntroPreview"); View.NextButton.onClick.Invoke(); Require(g.Dialogue.State.Index == 1 && !g.NPCs.State.HasMet(Id), "One sentence does not mark meeting");
                    SessionState.SetFloat(Key + ".Minute", (float)g.Time.Clock.Minute); SessionState.SetFloat(Key + ".Pause", (float)EditorApplication.timeSinceStartup); Phase(4);
                }
                else if (phase == 4 && EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + ".Pause", 0) > .2)
                {
                    int day = g.Farm.State.Day; g.AdvanceDay(); g.OpenCooking(); g.OpenStorage(); g.OpenOrder(); g.OpenShop(); g.OpenSleep(); g.OpenToolUpgrade(); g.ToggleInventory();
                    Require(Math.Abs(g.Time.Clock.Minute - SessionState.GetFloat(Key + ".Minute", 0)) < .001 && day == g.Farm.State.Day && !g.UI.HasCooking && !g.UI.HasStorage && !g.UI.HasOrder
                        && !g.UI.HasShop && !g.UI.HasSleep && !g.UI.HasToolUpgrade && !g.UI.IsInventoryVisible && !g.TryGather(0, out _), "Dialogue modal pauses time and blocks other actions");
                    while (!g.Dialogue.State.LastLine) { View.NextButton.onClick.Invoke(); CheckText(); }
                    View.CloseButton.onClick.Invoke(); Require(!g.NPCs.State.HasMet(Id) && !g.Dialogue.State.Active && !g.UI.HasDialogue, "Close on displayed final line does not meet");
                    g.ReturnToMainMenu(); Phase(5);
                }
                else if (phase == 5 && g.State == GameFlowState.MainMenu)
                {
                    Require(g.NPCs.State == null && g.NPCs.Actors.Count == 0 && g.Dialogue.State == null && UnityEngine.Object.FindObjectsOfType<NPCActor>().Length == 0, "NPC handles/state cleared on return");
                    Require(g.Saves.TryLoad(out var data) && data.NPCs.Length == 1 && !data.NPCs[0].Met && data.Version == SaveData.CurrentVersion, "Cancelled intro saved unmet"); g.ContinueGame(); Phase(6);
                }
                else if (phase == 6 && g.State == GameFlowState.Playing) { Place(g); g.OpenDialogue(Id); Phase(7); }
                else if (phase == 7 && g.UI.HasDialogue && View != null)
                {
                    Require(g.Dialogue.State.Index == 0 && g.Dialogue.State.Lines.Count == 5, "Cancelled intro reload restarts");
                    string dir = SessionState.GetString(Key + ".Dir", ""), blocked = Path.Combine(dir, "blocked"); File.WriteAllText(blocked, "file"); SaveDirectory(g, blocked); Finish(g);
                    Require(g.NPCs.State.HasMet(Id) && g.Interactions.Controller.Feedback.Contains("保存失败"), "Completed intro retains meeting on autosave failure");
                    g.AdvanceDialogue(); Require(g.NPCs.State.HasMet(Id), "Stale next click harmless"); SaveDirectory(g, dir); Require(g.SaveProgress() && g.Saves.TryLoad(out var saved) && saved.NPCs[0].Met, "Retry saves meeting without repeating talk");
                    Require(g.Inventory.GetCount("turnip_seed") == 50 && g.Economy.Gold == 100 && g.Stamina.State.Current == 96 && g.Tasks.State.Capture().All(t => t.Progress == 0), "Conversation no rewards/stamina/task changes"); g.ReturnToMainMenu(); Phase(8);
                }
                else if (phase == 8 && g.State == GameFlowState.MainMenu)
                {
                    Require(g.Saves.TryLoad(out var data), "Read context fixture"); var original = JsonUtility.ToJson(data); data.NPCs[0].Id = "unknown"; Write(g, data); Require(!g.Saves.TryLoad(out _), "Corrupt NPC ID rejected");
                    data = JsonUtility.FromJson<SaveData>(original); data.Day = 85; data.Weather = WeatherKind.Rainy;
                    data.Order = new OrderState(g.Configuration.Orders.Definitions(g.Configuration.Crops), g.Configuration.Seasons.DaysPerSeason, data.Order.Seed, data.Day).Capture(); Write(g, data); g.ContinueGame(); Phase(9);
                }
                else if (phase == 9 && g.State == GameFlowState.Playing) { Require(g.NPCs.State.HasMet(Id) && g.Calendar.Date.Season == Season.Winter && g.Weather.State.Kind == WeatherKind.Rainy, "Saved meeting/context restored"); Place(g); g.OpenDialogue(Id); Phase(10); }
                else if (phase == 10 && g.UI.HasDialogue && View != null)
                {
                    var definition = g.Configuration.Dialogues.Find(Id); Require(g.Dialogue.State.Lines.Count == 3 && g.Dialogue.State.Text == definition.Repeat[0]
                        && g.Dialogue.State.Lines[1] == definition.Seasons[3] && g.Dialogue.State.Lines[2] == definition.Weather[1], "Daily dialogue uses saved meeting, winter and rain");
                    View.NextButton.onClick.Invoke(); CheckText(); Capture(g, "DialogueWinterPreview"); Finish(g); g.ReturnToMainMenu(); Phase(11);
                }
                else if (phase == 11 && g.State == GameFlowState.MainMenu)
                {
                    Require(g.Saves.TryLoad(out var data), "Read legacy fixture"); string order = JsonUtility.ToJson(data.Order), resource = JsonUtility.ToJson(data.Gathering[0]);
                    data.Version = 11; data.NPCs = null; Write(g, data); Require(g.Saves.TryLoad(out var migrated) && migrated.Version == SaveData.CurrentVersion && migrated.NPCs.Length == 1 && !migrated.NPCs[0].Met
                        && migrated.Day == 85 && migrated.Stamina == 96 && migrated.Gold == 100 && JsonUtility.ToJson(migrated.Order) == order && JsonUtility.ToJson(migrated.Gathering[0]) == resource, "v11 migration preserves progress and initializes unmet NPC"); g.ContinueGame(); Phase(12);
                }
                else if (phase == 12 && g.State == GameFlowState.Playing)
                {
                    Require(!g.NPCs.State.HasMet(Id) && g.NPCs.Actors.Count == 1 && g.Gathering.State.HitsRemaining(0) == 2, "Migrated save playable without duplicated actors/resources");
                    File.WriteAllText("Logs/NPCDialogueVerification.txt", "PASS: packed AA NPC/dialogue configs and actor/MVC UI, all farm sizes, reach/UI/busy/modal guards, sentence progression, cancellation and completion-only meeting, autosave failure/retry, exact reload, winter/rain context, v11 migration, corrupt ID rejection, lifecycle and Chinese layout.\n");
                    Debug.Log("NPC/dialogue flow verification PASSED."); Phase(13); EditorApplication.isPlaying = false;
                }
            }
            catch (Exception error) { SessionState.SetBool(Key, false); Debug.LogException(error); EditorApplication.Exit(1); }
        }
    }
}
