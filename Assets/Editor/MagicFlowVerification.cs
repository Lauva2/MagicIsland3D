using System;
using System.IO;
using System.Linq;
using System.Reflection;
using MagicIsland.Farming;
using MagicIsland.Magic;
using MagicIsland.Managers;
using MagicIsland.Saving;
using MagicIsland.UI.Views;
using MagicIsland.WeatherSystem;
using TMPro;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    [InitializeOnLoad] public static class MagicFlowVerification
    {
        const string Key = "MagicIsland.MagicFlow";
        static MagicFlowVerification() { if (SessionState.GetBool(Key, false)) EditorApplication.update += Tick; }
        public static void Run()
        {
            PrototypeAddressablesSetup.EnsureSetup(); MagicContentSetup.RebuildRuneFacing(); PrototypeAddressablesSetup.ValidateUiPrefabs();
            var paths = Directory.GetFiles("Assets/Art/Magic", "*", SearchOption.AllDirectories)
                .Concat(new[] { "Assets/Config/MagicConfig.asset", "Assets/Config/CropCatalog.asset", "Assets/Config/ItemCatalog.asset", "Assets/Art/Prefabs/UI/FarmHudPanel.prefab", "Assets/Art/Prefabs/UI/ShopPanel.prefab", "Assets/Art/Prefabs/UI/MagicPanel.prefab" }).ToArray();
            var before = paths.Select(File.ReadAllBytes).ToArray(); PrototypeAddressablesSetup.EnsureSetup();
            for (int i = 0; i < paths.Length; i++) Require(before[i].SequenceEqual(File.ReadAllBytes(paths[i])), "Setup preserves authored magic and existing resources: " + paths[i]);
            EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity"); PrototypeAddressablesSetup.BuildContent();
            var settings = AddressableAssetSettingsDefaultObject.Settings; settings.ActivePlayModeDataBuilderIndex = settings.DataBuilders.FindIndex(b => b is BuildScriptPackedPlayMode);
            SessionState.SetBool(Key, true); SessionState.SetFloat(Key + ".Start", (float)EditorApplication.timeSinceStartup); Phase(0);
            EditorApplication.update -= Tick; EditorApplication.update += Tick; EditorApplication.isPlaying = true;
        }
        static void Require(bool ok, string why) { if (!ok) throw new Exception(why); }
        static T View<T>() where T : Component => Object.FindObjectOfType<T>();
        static void Phase(int p) { SessionState.SetFloat(Key + ".PhaseTime", (float)EditorApplication.timeSinceStartup); SessionState.SetInt(Key + ".Phase", p); Debug.Log("Magic phase " + p); }
        static void FinishAction(GameManager g, float delta = 2) => typeof(ToolManager).GetMethod("TickAction", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(g.Tools, new object[] { delta });
        static void SaveDirectory(GameManager g, string dir) => g.Saves.Initialize(g.Configuration.Config, g.Configuration.Items, g.Configuration.Crops, dir, g.Configuration.Tasks, g.Configuration.Orders, g.Configuration.Seasons, g.Configuration.Gathering, g.Configuration.ToolUpgrades, g.Configuration.Storage, g.Configuration.NPCs, g.Configuration.Relationships, g.Configuration.Stories, g.Configuration.NPCSchedules, g.Configuration.Fishing, g.Configuration.Crafting, g.Configuration.Animals);
        static void Near(GameManager g, int index) { g.Players.RestorePosition(g.Farm.Grid.CellCenter(index) + new Vector3(0, .1f, -1.65f)); Physics.SyncTransforms(); }
        static void Act(GameManager g, int index, FarmTool tool)
        { Near(g, index); Require(g.Tools.SelectTool(tool) && g.Tools.TryAct(index, out _), "Real farm action " + tool); FinishAction(g); }
        static void Water(GameManager g) { if (!g.Farm.State.Cells[0].Watered) Act(g, 0, FarmTool.WateringCan); }
        static void Home(GameManager g) { g.Players.RestorePosition(g.Housing.Entrance.Position + new Vector3(0, .1f, -1.4f)); g.EnterHome(); }
        static void Rest(GameManager g) { g.Players.RestorePosition(g.Configuration.Housing.InteriorOrigin + new Vector3(-1.5f, .1f, 2)); g.RequestRest(); }
        static void ExitHome(GameManager g) { g.Players.RestorePosition(g.Housing.Interior.Exit.Position + Vector3.up * .1f); g.ExitHome(); }
        static void TextCheck(Component root)
        {
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            { text.ForceMeshUpdate(true); Require(!text.isTextOverflowing && text.text.Where(c => !char.IsControl(c)).All(c => text.font.HasCharacter(c)), "Chinese label fits: " + text.name + "=" + text.text); }
        }
        static void Capture(GameManager g, string name, int width = 1280, int height = 720)
        {
            g.UI.RefreshViews(); Canvas.ForceUpdateCanvases(); g.Players.Player.ViewCamera.GetComponent<MagicIsland.CameraSystem.CameraController>().SnapToTarget();
            GameUiPreview.CaptureInventory(g.Players.Player.ViewCamera, width, height, () => { if (View<MagicView>() != null) TextCheck(View<MagicView>()); });
            File.Copy("Logs/InventoryPreview.png", "Logs/" + name + ".png", true);
        }
        static void Return(GameManager g) { g.ReturnToMainMenu(); }
        static void Migration(GameManager g)
        {
            string valid = File.ReadAllText(g.Saves.SavePath); Require(g.Saves.TryLoad(out var data) && data.Version == 30 && data.Magic.Stage == MagicStage.RainLearned, "Sleep saves learned magic in v30");
            long gold = data.Gold; string slots = JsonUtility.ToJson(data); var old = JsonUtility.FromJson<SaveData>(slots); old.Version = 29; old.Magic = null;
            File.WriteAllText(g.Saves.SavePath, JsonUtility.ToJson(old));
            Require(g.Saves.TryLoad(out var migrated) && migrated.Version == 30 && migrated.Magic.Stage == MagicStage.Unstudied && migrated.Gold == gold
                && migrated.Day == old.Day && migrated.Tasks.Select(t => t.Progress).SequenceEqual(old.Tasks.Select(t => t.Progress))
                && migrated.Slots.Select(s => s.ItemId + ":" + s.Quantity).SequenceEqual(old.Slots.Select(s => s.ItemId + ":" + s.Quantity)), "v29 migration preserves old progress and grants no seeds, magic or rewards");
            var bad = JsonUtility.FromJson<SaveData>(valid); bad.Magic.Stage = (MagicStage)99;
            File.WriteAllText(g.Saves.SavePath + ".bak", valid); File.WriteAllText(g.Saves.SavePath, JsonUtility.ToJson(bad));
            Require(g.Saves.TryLoad(out var backup) && g.Saves.RecoveredBackup && backup.Magic.Stage == MagicStage.RainLearned, "Invalid magic progress rejects whole primary and recovers valid backup");
            File.WriteAllText(g.Saves.SavePath, valid); File.WriteAllText(g.Saves.SavePath + ".bak", valid);
        }
        static void Tick()
        {
            if (!SessionState.GetBool(Key, false)) return; int p = SessionState.GetInt(Key + ".Phase", 0);
            if (p == 30 && !EditorApplication.isPlaying) { SessionState.SetBool(Key, false); EditorApplication.Exit(0); return; }
            try
            {
                if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + ".Start", 0) > 300) throw new Exception("Magic flow timeout " + p);
                if (!EditorApplication.isPlaying) return; var g = GameManager.Instance; if (g == null) return;
                Require(g.State != GameFlowState.Failed, "Magic boot failure");
                if (p == 0 && g.State == GameFlowState.MainMenu)
                {
                    string dir = Path.GetFullPath("Logs/MagicSaves/" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir); SaveDirectory(g, dir); g.Settings.Initialize(dir + "/Settings"); g.EnterGame(); Phase(1);
                }
                else if (p == 1 && g.State == GameFlowState.Playing)
                {
                    Require(g.Configuration.Magic.Validate(g.Configuration.Items, g.Configuration.Crops) && g.Farm.Crops.Crops.Count == 4 && g.Magic.OwnsEffect && !g.Magic.Effect.Active, "Packed magic config, all crop stages and effect load before input");
                    Require(g.Magic.State.Stage == MagicStage.Unstudied && !File.Exists(g.Saves.SavePath), "New magic starts unstudied without a daytime save");
                    var clone = Object.Instantiate(g.Configuration.Magic); clone.RainStamina = 0; Require(!clone.Validate(g.Configuration.Items, g.Configuration.Crops), "Invalid spell costs rejected"); Object.Destroy(clone);
                    View<MagicIsland.Interaction.PrototypeHud>().MagicButton.onClick.Invoke(); Phase(2);
                }
                else if (p == 2 && View<MagicView>() != null)
                {
                    Require(!g.Players.Player.enabled && !g.Tools.InputEnabled, "Journal loads and opens as a real paused modal"); TextCheck(View<MagicView>()); Capture(g, "MagicJournal1280"); Capture(g, "MagicJournal1024", 1024, 768);
                    View<MagicView>().StudyButton.onClick.Invoke(); long count = g.Inventory.GetCount(g.Configuration.Magic.SeedId);
                    View<MagicView>().StudyButton.onClick.Invoke(); Require(count == g.Configuration.Magic.GiftSeeds && g.Inventory.GetCount(g.Configuration.Magic.SeedId) == count, "Study grants seeds exactly once");
                    g.UI.RefreshViews(); TextCheck(View<MagicView>()); g.CloseMagic();
                    Act(g, 0, FarmTool.Hoe); Require(g.Farm.SelectSeed(g.Configuration.Magic.SeedId), "Magic seeds selectable"); Act(g, 0, FarmTool.Seeds); Water(g);
                    Require(g.Farm.State.Cells[0].RequiredGrowthDays == 4 && !g.Tools.TryCastRain(g.Magic, 0, out _), "Moonflower grows normally and unknown spell is blocked");
                    Home(g); Phase(3);
                }
                else if (p == 3 && g.State == GameFlowState.Playing && g.Housing.Inside) { Rest(g); Phase(4); }
                else if (p == 4 && View<SleepView>() != null) { View<SleepView>().ConfirmButton.onClick.Invoke(); Phase(5); }
                else if (p == 5 && g.DailyReports.State.Pending)
                {
                    Require(g.Saves.TryLoad(out var saved) && saved.Magic.Stage == MagicStage.GrowingMoonflowers && saved.ClockMinute == 360, "Actual bed saves unfinished magic with morning farm");
                    g.StartNewDayFromReport(); ExitHome(g); Phase(g.Farm.State.Day < 5 ? 6 : 7);
                }
                else if (p == 6 && g.State == GameFlowState.Playing && !g.Housing.Inside) { Water(g); Home(g); Phase(3); }
                else if (p == 7 && g.State == GameFlowState.Playing && !g.Housing.Inside)
                {
                    Require(g.Farm.State.Cells[0].Mature, "Moonflower matures after four actual sleeps");
                    Near(g, 0); g.Time.Clock.Restore(1260); g.Weather.Tick(1260, true); g.Magic.Tick(0, true, false, 0); LightingFlowVerification.Capture(g, "MoonflowerNight", g.Farm.Grid.CellCenter(0), 3.2f);
                    Act(g, 0, FarmTool.Hoe); Require(g.Magic.State.Stage == MagicStage.OfferingMoonflower, "Actual successful harvest advances magic quest");
                    g.ToggleMagic(); Phase(8);
                }
                else if (p == 8 && View<MagicView>() != null)
                {
                    TextCheck(View<MagicView>()); View<MagicView>().StudyButton.onClick.Invoke(); g.UI.RefreshViews(); TextCheck(View<MagicView>());
                    Require(g.Magic.State.RainLearned && !g.Tools.TryCastRain(g.Magic, 0, out _) && g.Saves.TryLoad(out var saved) && saved.Magic.Stage == MagicStage.GrowingMoonflowers, "Learning consumes flower in memory and cannot cast through journal");
                    Capture(g, "MagicLearned"); g.CloseMagic();
                    typeof(WeatherState).GetProperty("Kind").SetValue(g.Weather.State, WeatherKind.Sunny); LightingFlowVerification.SetTime(g, 720); g.Stamina.Refill();
                    foreach (int i in new[] { 8, 9 }) { Act(g, i, FarmTool.Hoe); Act(g, i, FarmTool.Seeds); }
                    Near(g, 8); var target = g.Farm.Grid.CellCenter(8); var ray = new Ray(target + Vector3.up * 12, Vector3.down);
                    g.Interactions.RefreshPointer(ray, true); Require(!g.Interactions.TryCastRain(), "UI clears spell pointer");
                    g.Interactions.RefreshPointer(ray, false); Require(g.Interactions.Current.Kind == MagicIsland.Interaction.InteractionKind.Farm, "Spell uses same depth-tested farm target");
                    g.Players.RestorePosition(target + Vector3.back * 8); int energy = g.Stamina.State.Current; Require(!g.Interactions.TryCastRain() && g.Stamina.State.Current == energy, "Fresh cast rechecks distance"); Near(g, 8);
                    int waterProgress = g.Tasks.State.GetProgress(2);
                    Action observer = () => Require(!g.Interactions.TryCastRain() && g.Farm.State.Cells[8].Watered && g.Farm.State.Cells[9].Watered, "Cast observers see full area commit and cannot reenter");
                    g.Stamina.State.Changed += observer; Require(g.Interactions.TryCastRain(), "Point and cast works"); g.Stamina.State.Changed -= observer;
                    Require(g.Stamina.State.Current == energy - g.Configuration.Magic.RainStamina && g.Tasks.State.GetProgress(2) == waterProgress && g.Tools.Busy && !g.Magic.Effect.Active, "One cost and action lock; no water-task credit or early effect");
                    g.Tools.SetInputEnabled(false); FinishAction(g, 1); Require(!g.Magic.Effect.Active && g.Tools.Busy, "Pause freezes pending contact"); g.Tools.SetInputEnabled(true);
                    FinishAction(g, g.Configuration.Config.ToolActionDuration * .55f); Require(g.Magic.Effect.Active, "Rain effect starts once at tool contact");
                    g.Magic.Tick(.2f, false, false, 1); Require(g.Magic.Effect.Rain.particleCount > 0, "Bounded rain particles are visible"); Capture(g, "MagicRainCast");
                    float time = g.Magic.Effect.Rain.time; g.Magic.Tick(1, true, false, 1); Require(g.Magic.Effect.Rain.time == time, "Modal freezes active spell particles");
                    FinishAction(g); energy = g.Stamina.State.Current; Require(!g.Interactions.TryCastRain() && g.Stamina.State.Current == energy, "Watered area cannot consume another cast");
                    Home(g); Phase(9);
                }
                else if (p == 9 && g.State == GameFlowState.Playing && g.Housing.Inside) { Rest(g); Phase(10); }
                else if (p == 10 && View<SleepView>() != null) { View<SleepView>().ConfirmButton.onClick.Invoke(); Phase(11); }
                else if (p == 11 && g.DailyReports.State.Pending)
                {
                    Migration(g); g.StartNewDayFromReport(); Return(g); Phase(12);
                }
                else if (p == 12 && View<PauseView>() != null) { View<PauseView>().ReturnMenuButton.onClick.Invoke(); Phase(13); }
                else if (p == 13 && g.State == GameFlowState.MainMenu)
                {
                    Require(g.Magic.State == null && !g.Magic.OwnsEffect && !g.UI.HasMagic && Shader.GetGlobalFloat("_IslandMagicNight") == 0, "Exit releases magic effect, state, subscriptions and globals"); g.ContinueGame(); Phase(14);
                }
                else if (p == 14 && g.State == GameFlowState.Playing)
                {
                    Require(g.Magic.State.RainLearned && g.Housing.Inside && g.Magic.OwnsEffect && !g.Magic.Effect.Active, "Reload restores learned spell without replaying presentation");
                    g.ToggleMagic(); Phase(15);
                }
                else if (p == 15 && View<MagicView>() != null)
                {
                    Require(!View<MagicView>().StudyButton.gameObject.activeSelf, "Reload cannot claim seeds or learn twice"); TextCheck(View<MagicView>()); g.HandleEscape(); Require(!g.UI.HasMagic && g.Players.Player.enabled, "ESC closes journal and restores input");
                    Return(g); Phase(16);
                }
                else if (p == 16 && View<PauseView>() != null) { View<PauseView>().ReturnMenuButton.onClick.Invoke(); Phase(17); }
                else if (p == 17 && g.State == GameFlowState.MainMenu)
                {
                    g.EnterGame(); Phase(18);
                }
                else if (p == 18 && g.State == GameFlowState.Playing)
                {
                    Require(g.Magic.State.Stage == MagicStage.Unstudied && g.Inventory.GetCount(g.Configuration.Magic.SeedId) == 0, "New game clears all prior magic progress and reward inventory");
                    SessionState.SetString(Key + ".EffectAddress", g.Configuration.Magic.RainEffectAddress);
                    g.Configuration.Magic.RainEffectAddress = "MissingMagicRainVerification";
                    g.StartCoroutine(g.Magic.LoadEffect(g.Farm.Grid.transform.parent)); Phase(19);
                }
                else if (p == 19 && g.Magic.LastError != null)
                {
                    Require(!g.Magic.OwnsEffect && g.Magic.Effect == null, "Failed effect load releases dependency and instance");
                    g.Configuration.Magic.RainEffectAddress = SessionState.GetString(Key + ".EffectAddress", "MagicRainEffect");
                    g.StartCoroutine(g.Magic.LoadEffect(g.Farm.Grid.transform.parent)); g.Magic.Clear(); Phase(20);
                }
                else if (p == 20 && EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + ".PhaseTime", 0) > .3f)
                {
                    Require(!g.Magic.OwnsEffect && g.Magic.State == null && Object.FindObjectsOfType<MagicRainEffect>(true).Length == 0, "Cancelled effect load discards late results");
                    g.Magic.Initialize(g.Configuration.Magic, g.Farm, null); g.StartCoroutine(g.Magic.LoadEffect(g.Farm.Grid.transform.parent)); Phase(21);
                }
                else if (p == 21 && g.Magic.Effect != null)
                {
                    Require(g.Magic.LastError == null && Object.FindObjectsOfType<MagicRainEffect>(true).Length == 1, "Effect recovers without duplicate instances");
                    g.ToggleMagic(); g.CloseMagic(); Phase(22);
                }
                else if (p == 22 && EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + ".PhaseTime", 0) > .3f)
                {
                    Require(!g.UI.HasMagic && Object.FindObjectsOfType<MagicView>(true).Length == 0 && g.Players.Player.enabled, "Closing loading journal discards late controller and restores input");
                    g.ToggleMagic(); Phase(23);
                }
                else if (p == 23 && View<MagicView>() != null)
                {
                    Require(g.Magic.State.Stage == MagicStage.Unstudied, "Cancelled journal did not claim a reward"); g.CloseMagic();
                    File.WriteAllText("Logs/MagicVerification.txt", "PASS: real Packed AA, idempotent setup, magic journal MVC and Chinese at 1280x720/1024x768, one-time seed reward, full four-sleep crop growth, real harvest and flower offering, area rain and shared live targeting, cost/action lock/reentry, UI and distance guards, paused contact and active effects, no automatic Water task credit or daytime save, v29 migration, invalid primary/backup, v30 sleep/reload, cleanup/reentry/new game, effect load failure/cancellation/late release/recovery, journal loading cancellation.\n");
                    Debug.Log("Magic flow verification PASSED."); Phase(30); EditorApplication.isPlaying = false;
                }
            }
            catch (Exception error)
            { Debug.LogException(error); File.WriteAllText("Logs/MagicVerification.txt", "FAIL phase " + p + ": " + error); SessionState.SetBool(Key, false); EditorApplication.isPlaying = false; EditorApplication.Exit(1); }
        }
    }
}
