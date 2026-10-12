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
    public static class NPCScheduleFlowVerification
    {
        const string Key = "MagicIsland.ScheduleVerification", Id = "island_steward";
        static NPCScheduleFlowVerification() { if (SessionState.GetBool(Key, false)) EditorApplication.update += Tick; }
        public static void Run()
        {
            PrototypeAddressablesSetup.EnsureSetup(); EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity"); PrototypeAddressablesSetup.BuildContent();
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            settings.ActivePlayModeDataBuilderIndex = settings.DataBuilders.FindIndex(b => b is BuildScriptPackedPlayMode);
            SessionState.SetBool(Key, true); Phase(0); SessionState.SetFloat(Key + ".Start", (float)EditorApplication.timeSinceStartup);
            EditorApplication.update -= Tick; EditorApplication.update += Tick; EditorApplication.isPlaying = true;
        }
        static void Phase(int value) => SessionState.SetInt(Key + ".Phase", value);
        static void Require(bool ok, string message) { if (!ok) throw new Exception(message); }
        static NPCActor Actor(GameManager g) => g.NPCs.Actors[Id];
        static void SaveDirectory(GameManager g, string directory) => g.Saves.Initialize(g.Configuration.Config, g.Configuration.Items,
            g.Configuration.Crops, directory, g.Configuration.Tasks, g.Configuration.Orders, g.Configuration.Seasons, g.Configuration.Gathering,
            g.Configuration.ToolUpgrades, g.Configuration.Storage, g.Configuration.NPCs, g.Configuration.Relationships, g.Configuration.Stories,
            g.Configuration.NPCSchedules, g.Configuration.Fishing, g.Configuration.Crafting);
        static void AtNpc(GameManager g) => g.Players.RestorePosition(Actor(g).transform.position + new Vector3(-1.2f, .1f, -.7f));
        static void Write(GameManager g, SaveData data)
        { string json = JsonUtility.ToJson(data); File.WriteAllText(g.Saves.SavePath, json); File.WriteAllText(g.Saves.SavePath + ".bak", json); }
        static void Capture(GameManager g, string name)
        { UnityEngine.Object.FindObjectOfType<MagicIsland.CameraSystem.CameraController>().SnapToTarget(); GameUiPreview.CaptureInventory(g.Players.Player.ViewCamera); File.Copy("Logs/InventoryPreview.png", "Logs/" + name + ".png", true); }
        static void TextCheck(Component view)
        { foreach (var t in view.GetComponentsInChildren<TMPro.TMP_Text>()) { t.ForceMeshUpdate(); Require(!t.isTextOverflowing && t.text.Where(c => !char.IsControl(c)).All(c => t.font.HasCharacter(c)), "Schedule Chinese/layout: " + t.name); } }
        static void Tick()
        {
            if (!SessionState.GetBool(Key, false)) return;
            int phase = SessionState.GetInt(Key + ".Phase", 0);
            if (phase == 16 && !EditorApplication.isPlaying) { SessionState.SetBool(Key, false); EditorApplication.Exit(0); return; }
            try
            {
                if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + ".Start", 0) > 180) throw new Exception("Schedule timeout phase " + phase);
                if (!EditorApplication.isPlaying) return;
                var g = GameManager.Instance; if (g == null) return; Require(g.State != GameFlowState.Failed, "Schedule configuration failure");
                if (phase == 0 && g.State == GameFlowState.MainMenu)
                {
                    Require(g.Configuration.NPCSchedules != null, "17th configuration loaded with packed AA");
                    int size = g.Configuration.Config.FarmSize;
                    for (int i = 1; i <= 12; i++) { g.Configuration.Config.FarmSize = i; Require(g.Configuration.NPCSchedules.Validate(g.Configuration.Config, g.Configuration.NPCs, g.Configuration.Fishing), "Route clears farm " + i); }
                    g.Configuration.Config.FarmSize = size;
                    var config = g.Configuration.NPCSchedules.Schedules[0]; float x = config.CorridorX; config.CorridorX = float.NaN;
                    Require(!g.Configuration.NPCSchedules.Validate(g.Configuration.Config, g.Configuration.NPCs, g.Configuration.Fishing), "Invalid route rejected"); config.CorridorX = x;
                    string dir = Path.GetFullPath("Logs/NPCScheduleSaves/" + Guid.NewGuid().ToString("N")); SessionState.SetString(Key + ".Dir", dir);
                    SaveDirectory(g, dir); g.EnterGame(); Phase(1);
                }
                else if (phase == 1 && g.State == GameFlowState.Playing)
                {
                    Require(g.NPCSchedules.Bound && g.NPCSchedules.State.Distance(Id) == 0, "Physical route binds at office");
                    g.Players.RestorePosition(new Vector3(0, .1f, -4)); g.Time.Clock.Restore(720); g.NPCSchedules.Tick(720, 1, false);
                    Require(Math.Abs(g.NPCSchedules.State.Distance(Id) - 1.4) < .00001 && Actor(g).transform.forward.x > .9f, "Noon walks east at configured speed");
                    var d = g.NPCSchedules.State.Definitions[0]; var p = d.Position(2.1);
                    var block = GameObject.CreatePrimitive(PrimitiveType.Cube); block.name = "ScheduleTestObstacle"; block.transform.position = new Vector3((float)p.X, .8f, (float)p.Z); block.transform.localScale = new Vector3(.4f, 1.6f, .4f);
                    double prior = g.NPCSchedules.State.Distance(Id); g.NPCSchedules.Tick(720, 1, false);
                    Require(g.NPCSchedules.State.Distance(Id) == prior, "Solid obstacle prevents crossing"); UnityEngine.Object.DestroyImmediate(block);
                    g.Players.RestorePosition(new Vector3((float)p.X, .1f, (float)p.Z)); g.NPCSchedules.Tick(720, 1, false);
                    Require(g.NPCSchedules.State.Distance(Id) == prior, "Player blocks corridor");
                    g.Players.RestorePosition(new Vector3(0, .1f, -4)); g.NPCSchedules.Tick(720, 1, false);
                    Require(g.NPCSchedules.State.Distance(Id) > prior, "Moving blocker away resumes travel");
                    AtNpc(g); g.OpenDialogue(Id); Phase(2);
                }
                else if (phase == 2 && g.UI.HasDialogue && UnityEngine.Object.FindObjectOfType<DialogueView>() != null)
                {
                    double prior = g.NPCSchedules.State.Distance(Id); var rotation = Actor(g).transform.rotation;
                    g.NPCSchedules.Tick(g.Time.Clock.Minute, 100, true); g.NPCSchedules.Tick(g.Time.Clock.Minute, 100, false);
                    Require(g.NPCSchedules.State.Distance(Id) == prior && Actor(g).transform.rotation == rotation && g.NPCs.IsFocused(Id), "Dialogue pauses and preserves player-facing actor");
                    TextCheck(UnityEngine.Object.FindObjectOfType<DialogueView>());
                    g.CloseDialogue(); g.Players.RestorePosition(new Vector3(0, .1f, -4)); g.NPCSchedules.Tick(720, 1, false);
                    Require(g.NPCSchedules.State.Distance(Id) > prior && !g.NPCs.IsFocused(Id), "Closing dialogue resumes travel");
                    AtNpc(g); g.NPCs.State.MarkMet(Id); g.OpenGift(Id); Phase(3);
                }
                else if (phase == 3 && g.UI.HasGift && UnityEngine.Object.FindObjectOfType<GiftView>() != null)
                {
                    double prior = g.NPCSchedules.State.Distance(Id); g.NPCSchedules.Tick(720, 100, false);
                    Require(g.NPCSchedules.State.Distance(Id) == prior && g.NPCs.IsFocused(Id), "Gift focus pauses schedule");
                    g.CloseGift();
                    g.Relationships.State.Restore(new[] { new SavedRelationship { Id = Id, Points = 30, LastGiftDay = 1 } });
                    g.OpenDialogue(Id); Phase(17);
                }
                else if (phase == 17 && g.UI.HasDialogue && UnityEngine.Object.FindObjectOfType<DialogueView>() != null)
                { g.OpenStoryFromDialogue(); if (g.Dialogue.ActiveEventId != null) Phase(18); }
                else if (phase == 18 && g.UI.HasDialogue && g.Dialogue.ActiveEventId != null)
                {
                    double prior = g.NPCSchedules.State.Distance(Id); g.NPCSchedules.Tick(720, 100, false);
                    Require(g.NPCSchedules.State.Distance(Id) == prior && g.NPCs.IsFocused(Id), "Story event focus pauses schedule");
                    g.CloseDialogue(); g.Players.RestorePosition(new Vector3(0, .1f, -4)); g.ToggleInventory(); Phase(4);
                }
                else if (phase == 4 && g.UI.IsInventoryVisible && UnityEngine.Object.FindObjectOfType<InventoryView>() != null)
                { SessionState.SetString(Key + ".Paused", g.NPCSchedules.State.Distance(Id).ToString("R", System.Globalization.CultureInfo.InvariantCulture)); Phase(5); }
                else if (phase == 5 && g.UI.IsInventoryVisible)
                {
                    Require(g.NPCSchedules.State.Distance(Id) == double.Parse(SessionState.GetString(Key + ".Paused", "0"), System.Globalization.CultureInfo.InvariantCulture), "GameManager modal freezes real updates");
                    g.CloseInventory(); g.NPCSchedules.Tick(720, 100, false);
                    Require(!g.NPCSchedules.State.Moving(Id) && g.NPCSchedules.Describe(Id) == "在海岸休息", "Full route reaches coast");
                    Require(Actor(g).transform.position.x < g.Fishing.Spot.transform.position.x && Actor(g).transform.position.z > g.Fishing.Spot.transform.position.z, "Coast arrival stays clear of fishing deck");
                    AtNpc(g);
                    Physics.SyncTransforms();
                    var controller = UnityEngine.Object.FindObjectOfType<InteractionController>(); var ray = new Ray(Actor(g).transform.position + Vector3.up * 3, Vector3.down);
                    Require(controller.RefreshNPCHover(ray, false) && controller.Hint.Contains("在海岸休息"), "Hover explains current activity: " + controller.Hint); g.UI.RefreshViews();
                    Capture(g, "NPCAtCoastPreview");
                    g.Players.RestorePosition(new Vector3(0, .1f, -4)); g.Time.Clock.Restore(1080); g.NPCSchedules.Tick(1080, 1, false);
                    Require(g.NPCSchedules.State.Moving(Id) && g.NPCSchedules.State.Distance(Id) < g.NPCSchedules.State.Definitions[0].Length, "Evening returns along route");
                    AtNpc(g); Capture(g, "NPCScheduleWalkingPreview");
                    UnityEngine.Time.timeScale = 0; Require(g.SaveProgress(), "Travel snapshot saved");
                    Require(g.Saves.TryLoad(out var save) && save.Version == SaveData.CurrentVersion, "Travel snapshot read");
                    SessionState.SetString(Key + ".Saved", save.NPCSchedules[0].Distance.ToString("R", System.Globalization.CultureInfo.InvariantCulture)); g.ReturnToMainMenu(); Phase(6);
                }
                else if (phase == 6 && g.State == GameFlowState.MainMenu)
                { Require(g.NPCSchedules.State == null && !g.NPCSchedules.Bound && g.NPCs.Actors.Count == 0, "Return clears actor and schedule"); g.ContinueGame(); Phase(7); }
                else if (phase == 7 && g.State == GameFlowState.Playing)
                {
                    Require(g.NPCSchedules.State.Distance(Id) == double.Parse(SessionState.GetString(Key + ".Saved", "0"), System.Globalization.CultureInfo.InvariantCulture)
                        && g.NPCSchedules.State.Moving(Id) && UnityEngine.Object.FindObjectsOfType<NPCActor>().Length == 1, "Reload restores exact mid-route progress without duplicate actor");
                    UnityEngine.Time.timeScale = 1; g.Players.RestorePosition(new Vector3(0, .1f, -4)); g.NPCSchedules.Tick(1080, 100, false);
                    Require(g.NPCSchedules.State.Distance(Id) == 0 && !g.NPCSchedules.State.Moving(Id), "Return arrives at office");
                    g.Time.Clock.Restore(900); g.NPCSchedules.Tick(900, 100, false); g.AdvanceDay();
                    Require(g.Time.Clock.Minute == 360 && g.NPCSchedules.State.Distance(Id) == 0 && g.Saves.TryLoad(out var morning) && morning.NPCSchedules[0].Distance == 0, "Sleep resets and saves morning position");
                    g.ReturnToMainMenu(); Phase(8);
                }
                else if (phase == 8 && g.State == GameFlowState.MainMenu)
                {
                    Require(g.Saves.TryLoad(out var save), "Read migration fixture"); save.Version = 14; save.NPCSchedules = null; save.ClockMinute = 900; Write(g, save);
                    Require(g.Saves.TryLoad(out var legacy) && legacy.Version == SaveData.CurrentVersion && legacy.NPCSchedules[0].Distance > 0 && legacy.NPCs[0].Met && legacy.Day == 2, "Legacy migration retains meeting/day at afternoon coast");
                    UnityEngine.Time.timeScale = 0; g.ContinueGame(); Phase(9);
                }
                else if (phase == 9 && g.State == GameFlowState.Playing)
                { Require(!g.NPCSchedules.State.Moving(Id) && g.NPCSchedules.State.Distance(Id) > 0, "Migrated afternoon starts at coast"); g.ReturnToMainMenu(); Phase(10); }
                else if (phase == 10 && g.State == GameFlowState.MainMenu)
                {
                    Require(g.Saves.TryLoad(out var save), "Read invalid fixture"); save.NPCSchedules[0].Distance = -1; Write(g, save);
                    Require(!g.Saves.TryLoad(out _), "Invalid route distance rejected in primary and backup"); g.ContinueGame(); Phase(11);
                }
                else if (phase == 11 && g.State == GameFlowState.MainMenu)
                {
                    Require(g.NPCs.Actors.Count == 0 && g.NPCSchedules.State == null, "Invalid save leaves no partial world");
                    g.Configuration.Config.FarmSize = 12; SaveDirectory(g, Path.Combine(SessionState.GetString(Key + ".Dir", ""), "MaxFarm")); g.EnterGame(); Phase(12);
                }
                else if (phase == 12 && g.State == GameFlowState.Playing)
                {
                    Require(g.Farm.State.Size == 12 && g.NPCSchedules.Bound, "Maximum farm actual colliders validate route");
                    g.Players.RestorePosition(new Vector3(0, .1f, -9)); g.Time.Clock.Restore(720); g.NPCSchedules.Tick(720, 100, false);
                    Require(!g.NPCSchedules.State.Moving(Id) && g.NPCSchedules.State.Distance(Id) > 0, "Maximum farm traverses clear corridor");
                    g.Time.Clock.Restore(1080); g.NPCSchedules.Tick(1080, 100, false); Require(g.NPCSchedules.State.Distance(Id) == 0, "Maximum farm returns home");
                    g.ReturnToMainMenu(); Phase(13);
                }
                else if (phase == 13 && g.State == GameFlowState.MainMenu)
                {
                    // Modified scene geometry must fail route binding before input becomes available.
                    g.Configuration.Config.FarmSize = 5; SaveDirectory(g, Path.Combine(SessionState.GetString(Key + ".Dir", ""), "BlockedRoute"));
                    g.Configuration.NPCSchedules.Schedules[0].CorridorX = 4.5f; g.EnterGame(); Phase(14);
                }
                else if (phase == 14 && g.State == GameFlowState.MainMenu)
                {
                    Require(g.NPCs.Actors.Count == 0 && g.NPCSchedules.State == null, "Blocked shop route cleans failed initialization");
                    g.Configuration.NPCSchedules.Schedules[0].CorridorX = 11.2f; SaveDirectory(g, Path.Combine(SessionState.GetString(Key + ".Dir", ""), "Final"));
                    g.EnterGame(); Phase(15);
                }
                else if (phase == 15 && g.State == GameFlowState.Playing)
                {
                    Require(g.NPCSchedules.Bound && UnityEngine.Object.FindObjectsOfType<NPCActor>().Length == 1, "Re-enter after route failure succeeds");
                    UnityEngine.Time.timeScale = 1;
                    File.WriteAllText("Logs/NPCScheduleVerification.txt", "PASS: packed AA schedule configuration, all farm sizes, physical routes for 5/12, noon/evening travel and orientation, static/player obstruction and resume, dialogue/gift focus, real modal pause, coast hover, precise saved travel reload, sleep morning reset, v14 migration, corrupt progress rejection, failed route cleanup/re-entry and Chinese dialogue.\n");
                    Debug.Log("NPC schedule flow verification PASSED."); Phase(16); EditorApplication.isPlaying = false;
                }
            }
            catch (Exception error) { UnityEngine.Time.timeScale = 1; SessionState.SetBool(Key, false); Debug.LogException(error); EditorApplication.Exit(1); }
        }
    }
}
