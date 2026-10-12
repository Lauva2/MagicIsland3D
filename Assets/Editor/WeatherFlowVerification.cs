using System;
using System.IO;
using System.Linq;
using MagicIsland.Farming;
using MagicIsland.Interaction;
using MagicIsland.Managers;
using MagicIsland.Prototype;
using MagicIsland.Saving;
using MagicIsland.WeatherSystem;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MagicIsland.EditorTools
{
    [InitializeOnLoad]
    public static class WeatherFlowVerification
    {
        private const string Running = "MagicIsland.WeatherVerification";
        static WeatherFlowVerification() { if (SessionState.GetBool(Running, false)) EditorApplication.update += Tick; }
        public static void Run()
        {
            PrototypeAddressablesSetup.EnsureSetup();
            EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
            PrototypeAddressablesSetup.BuildContent();
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            settings.ActivePlayModeDataBuilderIndex = settings.DataBuilders.FindIndex(builder => builder is BuildScriptPackedPlayMode);
            SessionState.SetBool(Running, true); SessionState.SetInt(Running + ".Phase", 0);
            SessionState.SetFloat(Running + ".Started", (float)EditorApplication.timeSinceStartup);
            EditorApplication.update -= Tick; EditorApplication.update += Tick; EditorApplication.isPlaying = true;
        }
        private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
        private static void Capture(GameManager game, string name)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            GameUiPreview.CaptureInventory(game.Players.Player.ViewCamera);
            File.Copy("Logs/InventoryPreview.png", "Logs/" + name + "Preview.png", true);
        }
        private static void Phase(int phase) => SessionState.SetInt(Running + ".Phase", phase);

        private static void Tick()
        {
            if (!SessionState.GetBool(Running, false)) return;
            int phase = SessionState.GetInt(Running + ".Phase", 0);
            if (phase == 12 && !EditorApplication.isPlaying)
            { SessionState.SetBool(Running, false); EditorApplication.Exit(0); return; }
            try
            {
                if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Running + ".Started", 0) > 120)
                    throw new Exception("Weather verification timed out at phase " + phase);
                if (!EditorApplication.isPlaying) return;
                GameManager game = GameManager.Instance; if (game == null) return;
                Require(game.State != GameFlowState.Failed, "Game initialization failed");
                if (phase == 0 && game.State == GameFlowState.MainMenu)
                {
                    Require(game.Configuration.Weather != null && game.Configuration.Weather.IsValid, "WeatherConfig loads via packed AA");
                    game.Configuration.Seasons.SpringRainChance = 1; // Force next-day rain without changing the source asset.
                    string directory = Path.GetFullPath("Logs/WeatherSaves/" + Guid.NewGuid().ToString("N"));
                    game.Saves.Initialize(game.Configuration.Config, game.Configuration.Items, game.Configuration.Crops, directory, tasks: game.Configuration.Tasks, orders: game.Configuration.Orders, seasons: game.Configuration.Seasons, gathering: game.Configuration.Gathering, toolUpgrades: game.Configuration.ToolUpgrades, storage: game.Configuration.Storage, npcs: game.Configuration.NPCs, relationships: game.Configuration.Relationships, stories: game.Configuration.Stories, schedules: game.Configuration.NPCSchedules, fishing: game.Configuration.Fishing, crafting: game.Configuration.Crafting);
                    game.EnterGame(); Phase(1);
                }
                else if (phase == 1 && game.State == GameFlowState.Playing)
                {
                    Require(game.Weather.State.Kind == WeatherKind.Sunny && game.Weather.State.Day == 1 && game.Weather.Rain.isStopped, "First day sunny with no rain");
                    SessionState.SetInt(Running + ".Seed", game.Weather.State.Seed);
                    var config = game.Configuration.Weather; float chance = config.RainChance;
                    config.RainChance = float.NaN; Require(!config.IsValid, "Invalid weather chance rejected"); config.RainChance = chance;
                    game.Time.Clock.Restore(720); game.Weather.Tick(720, false); Phase(2);
                }
                else if (phase == 2 && game.State == GameFlowState.Playing)
                {
                    Require(UnityEngine.Object.FindObjectOfType<PrototypeHud>().WeatherText.text == "晴天", "MVC weather text");
                    var world = UnityEngine.Object.FindObjectOfType<FarmPrototype>();
                    SessionState.SetFloat(Running + ".DayIntensity", world.Sun.intensity); Capture(game, "SunnyDay");
                    game.Time.Clock.Restore(1320); game.Weather.Tick(1320, false); Phase(3);
                }
                else if (phase == 3 && game.State == GameFlowState.Playing)
                {
                    var world = UnityEngine.Object.FindObjectOfType<FarmPrototype>();
                    Require(world.Sun.intensity > 0 && world.Sun.intensity < SessionState.GetFloat(Running + ".DayIntensity", 0), "Night darkens sun while retaining visibility");
                    Require(RenderSettings.ambientLight.b > RenderSettings.ambientLight.r, "Night uses blue ambient"); Capture(game, "Night");
                    Require(game.Farm.TryAct(0, FarmTool.Hoe, out _) && game.Farm.TryAct(0, FarmTool.Seeds, out _), "Plant unwatered sunny crop");
                    game.Farm.TryAct(1, FarmTool.Hoe, out _); game.AdvanceDay();
                    Require(game.Farm.State.Day == 2 && game.Weather.State.Day == 2 && game.Weather.State.Kind == WeatherKind.Rainy, "Morning advances farm/weather together");
                    Require(game.Farm.State.Cells[0].Growth == 0 && game.Farm.State.Cells[0].Watered && !game.Farm.State.Cells[1].Watered,
                        "Previous dry crop did not grow; new rainy morning waters only crops");
                    Require(game.Saves.TryLoad(out SaveData data) && data.Version == SaveData.CurrentVersion && data.Weather == WeatherKind.Rainy && data.Cells[0].Watered, "Rainy morning autosave");
                    game.ToggleInventory(); SessionState.SetFloat(Running + ".PausedMinute", (float)game.Time.Clock.Minute); Phase(4);
                }
                else if (phase == 4 && game.State == GameFlowState.Playing)
                {
                    Require(game.Weather.Rain.isPaused && Math.Abs(game.Time.Clock.Minute - SessionState.GetFloat(Running + ".PausedMinute", 0)) < 0.001,
                        "Bag pauses rain particles and clock");
                    game.CloseInventory(); game.Time.Clock.Restore(720); game.Weather.Tick(720, false);
                    game.Weather.Rain.Simulate(0.65f, true, false); game.Weather.Rain.Play(true); Phase(5);
                }
                else if (phase == 5 && game.State == GameFlowState.Playing)
                {
                    Require(game.Weather.Rain.isPlaying && game.Weather.Rain.particleCount > 0, "Rain resumes with visible particles");
                    Require(UnityEngine.Object.FindObjectOfType<PrototypeHud>().WeatherText.text == "雨天", "Rain label supports Chinese"); Capture(game, "RainyDay");
                    int stamina = game.Stamina.State.Current;
                    game.Farm.TryAct(2, FarmTool.Hoe, out _); game.Farm.TryAct(2, FarmTool.Seeds, out _); game.Weather.ApplyRain(); game.Weather.ApplyRain();
                    Require(game.Farm.State.Cells[2].Watered && game.Farm.State.Cells[2].Growth == 0
                        && game.Stamina.State.Current == stamina - game.Configuration.Config.HoeStaminaCost - game.Configuration.Config.SeedStaminaCost,
                        "Rain waters new plant without stamina cost or immediate growth");
                    game.AdvanceDay(); Require(game.Farm.State.Cells[0].Growth == 1 && game.Farm.State.Cells[2].Growth == 1 && game.Farm.State.Cells[2].Watered,
                        "Rainy sleep grows once and next rain waters again");
                    game.Time.Clock.Restore(900.5); game.ReturnToMainMenu(); Phase(6);
                }
                else if (phase == 6 && game.State == GameFlowState.MainMenu)
                {
                    Require(game.Weather.State == null && game.Weather.Rain == null && UnityEngine.Object.FindObjectsOfType<ParticleSystem>().Length == 0,
                        "Leaving world clears weather and particles");
                    Require(game.Saves.TryLoad(out SaveData data) && data.WeatherSeed == SessionState.GetInt(Running + ".Seed", 0)
                        && data.Day == 3 && data.Weather == WeatherKind.Rainy && data.ClockMinute == 900.5, "Saved weather/time preserved");
                    string json = JsonUtility.ToJson(data); data.WeatherSeed = 0;
                    File.WriteAllText(game.Saves.SavePath, JsonUtility.ToJson(data)); File.WriteAllText(game.Saves.SavePath + ".bak", JsonUtility.ToJson(data));
                    game.ContinueGame(); Require(game.State == GameFlowState.MainMenu && !game.Saves.HasSave && game.Weather.State == null, "Invalid weather rejects loading before world init");
                    File.WriteAllText(game.Saves.SavePath, json); File.WriteAllText(game.Saves.SavePath + ".bak", json);
                    game.ContinueGame(); Phase(7);
                }
                else if (phase == 7 && game.State == GameFlowState.Playing)
                {
                    Require(game.Weather.State.Day == 3 && game.Weather.State.Kind == WeatherKind.Rainy
                        && game.Weather.State.Seed == SessionState.GetInt(Running + ".Seed", 0) && game.Farm.State.Cells[0].Growth == 1
                        && game.Inventory.GetCount(game.Farm.SeedItemId) == game.Configuration.Config.InitialSeeds - 2,
                        "Rain save/resume does not reroll, regrow or grant seeds");
                    Require(Math.Abs(game.Time.Clock.Minute - 900.5) < 2 && game.Stamina.State.Current == game.Configuration.Config.MaxStamina, "Time/stamina restored");
                    foreach (var label in UnityEngine.Object.FindObjectsOfType<TMPro.TMP_Text>())
                        Require(label.text.Where(c => !char.IsControl(c)).All(c => label.font.HasCharacter(c)), "All UI glyphs supported");
                    var world = UnityEngine.Object.FindObjectOfType<FarmPrototype>(); game.Configuration.Seasons.SpringRainChance = 0;
                    game.Weather.Initialize(game.Configuration.Weather, game.Farm, world.Sun, world.Player.ViewCamera, world.transform, game.Weather.State.Seed, WeatherKind.Rainy, game.Calendar);
                    game.AdvanceDay(); Phase(8);
                }
                else if (phase == 8 && game.State == GameFlowState.Playing)
                {
                    Require(game.Weather.State.Kind == WeatherKind.Sunny && game.Weather.Rain.isStopped && game.Weather.Rain.particleCount == 0
                        && game.Farm.State.Cells[0].Growth == 2 && !game.Farm.State.Cells[0].Watered, "Rain-to-sun transition clears particles/water after growth");
                    game.Farm.TryAct(3, FarmTool.Hoe, out _); game.Farm.TryAct(3, FarmTool.Seeds, out _); game.Weather.ApplyRain();
                    Require(!game.Farm.State.Cells[3].Watered, "Sunny day does not water new plants");
                    game.ReturnToMainMenu(); Phase(9);
                }
                else if (phase == 9 && game.State == GameFlowState.MainMenu)
                {
                    Require(game.Saves.TryLoad(out SaveData data), "Read current save");
                    data.Version = 5; data.WeatherSeed = 0; data.Weather = (WeatherKind)99;
                    File.WriteAllText(game.Saves.SavePath, JsonUtility.ToJson(data));
                    Require(game.Saves.TryLoad(out SaveData upgraded) && upgraded.Version == SaveData.CurrentVersion && upgraded.Weather == WeatherKind.Sunny && upgraded.WeatherSeed == 1
                        && upgraded.Gold == data.Gold && upgraded.Stamina == data.Stamina && upgraded.Cells[0].Growth == data.Cells[0].Growth,
                        "v5 migration preserves progress and adds safe sunny weather");
                    game.ContinueGame(); Phase(10);
                }
                else if (phase == 10 && game.State == GameFlowState.Playing)
                {
                    Require(game.Weather.State.Kind == WeatherKind.Sunny && game.Weather.State.Seed == 1 && game.Farm.State.Day == 4
                        && game.Farm.State.Cells[0].Growth == 2 && !game.Farm.State.Cells[3].Watered, "Legacy world restores without applying rain");
                    game.ReturnToMainMenu(); Phase(11);
                }
                else if (phase == 11 && game.State == GameFlowState.MainMenu)
                {
                    File.WriteAllText("Logs/WeatherFlowVerification.txt", "PASS: packed WeatherConfig AA, sunny/rainy HUD and Chinese glyphs, daylight/night lighting.\n"
                        + "Rain particles pause/resume/cleanup, newly planted watering without cost, one growth per day, rainy autosave and sunny transition.\n"
                        + "Saved weather/seed/time restore, invalid weather rejection, v5 migration without progress changes.\n");
                    Debug.Log("Weather flow verification PASSED."); Phase(12); EditorApplication.isPlaying = false;
                }
            }
            catch (Exception error) { SessionState.SetBool(Running, false); Debug.LogException(error); EditorApplication.Exit(1); }
        }
    }
}
