using System;
using System.IO;
using System.Linq;
using MagicIsland.CalendarSystem;
using MagicIsland.Farming;
using MagicIsland.Interaction;
using MagicIsland.Managers;
using MagicIsland.Prototype;
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
    public static class CalendarFlowVerification
    {
        private const string Running = "MagicIsland.CalendarVerification";
        static CalendarFlowVerification() { if (SessionState.GetBool(Running, false)) EditorApplication.update += Tick; }
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
        private static InventoryView Bag => UnityEngine.Object.FindObjectOfType<InventoryView>(true);
        private static PrototypeHud Hud => UnityEngine.Object.FindObjectOfType<PrototypeHud>();
        // Jump only in the isolated fixture; normal gameplay always uses GameManager.AdvanceDay.
        private static void SetDay(GameManager game, int day)
        {
            game.Farm.State.Restore(day, game.Farm.State.Cells);
            while (game.Orders.State.Day < day) game.Orders.State.NextDay(game.Orders.State.Day + 1);
            while (game.Gathering.State.Day < day) game.Gathering.NextDay(game.Gathering.State.Day + 1);
            var world = UnityEngine.Object.FindObjectOfType<FarmPrototype>();
            game.Weather.Initialize(game.Configuration.Weather, game.Farm, world.Sun, world.Player.ViewCamera,
                world.transform, game.Weather.State.Seed, WeatherKind.Sunny, game.Calendar);
            game.UI.RefreshViews();
        }
        private static void Capture(GameManager game, string file)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            GameUiPreview.CaptureInventory(game.Players.Player.ViewCamera); File.Copy("Logs/InventoryPreview.png", "Logs/" + file + ".png", true);
        }
        private static void CheckText(TMPro.TMP_Text text)
        {
            text.ForceMeshUpdate();
            Require(!text.isTextOverflowing, "Seasonal text fits prefab: " + text.name);
            Require(text.text.Where(c => !char.IsControl(c)).All(c => text.font.HasCharacter(c)), "Seasonal Chinese glyphs baked: " + text.text);
        }
        private static void Tick()
        {
            if (!SessionState.GetBool(Running, false)) return;
            int phase = SessionState.GetInt(Running + ".Phase", 0);
            if (phase == 8 && !EditorApplication.isPlaying)
            { SessionState.SetBool(Running, false); EditorApplication.Exit(0); return; }
            try
            {
                if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Running + ".Started", 0) > 120)
                    throw new Exception("Calendar verification timed out at phase " + phase);
                if (!EditorApplication.isPlaying) return;
                GameManager game = GameManager.Instance; if (game == null) return;
                Require(game.State != GameFlowState.Failed, "Game configuration failed");
                if (phase == 0 && game.State == GameFlowState.MainMenu)
                {
                    var config = game.Configuration.Seasons;
                    Require(config != null && config.IsValid && config.DaysPerSeason == 28, "SeasonConfig loads via packed AA");
                    Require(config.SpringRainChance == 0.3f && config.SummerRainChance == 0.2f && config.AutumnRainChance == 0.4f
                        && config.WinterRainChance == 0.15f, "Default seasonal rain chances");
                    config.SpringRainChance = float.NaN; Require(!config.IsValid, "NaN rejected"); config.SpringRainChance = 0;
                    config.DaysPerSeason = 0; Require(!config.IsValid, "Invalid season length rejected"); config.DaysPerSeason = 28;
                    config.SummerRainChance = 1; config.AutumnRainChance = config.WinterRainChance = 0;
                    var crops = game.Configuration.Crops;
                    Require(crops.Find("turnip").PlantingSeasons == (SeasonMask.Spring | SeasonMask.Autumn)
                        && crops.Find("tomato").PlantingSeasons == SeasonMask.Summer
                        && crops.Find("corn").PlantingSeasons == (SeasonMask.Summer | SeasonMask.Autumn), "Default crop seasons serialized");
                    var savedMask = crops.Find("turnip").PlantingSeasons; crops.Find("turnip").PlantingSeasons = (SeasonMask)16;
                    Require(!crops.Validate(game.Configuration.Config, game.Configuration.Items), "Invalid crop season mask rejected"); crops.Find("turnip").PlantingSeasons = savedMask;
                    game.Saves.Initialize(game.Configuration.Config, game.Configuration.Items, crops, Path.GetFullPath("Logs/CalendarSaves/" + Guid.NewGuid().ToString("N")), tasks: game.Configuration.Tasks, orders: game.Configuration.Orders, seasons: game.Configuration.Seasons, gathering: game.Configuration.Gathering, toolUpgrades: game.Configuration.ToolUpgrades, storage: game.Configuration.Storage);
                    game.EnterGame(); Phase(1);
                }
                else if (phase == 1 && game.State == GameFlowState.Playing)
                {
                    Require(game.Calendar.Date.DisplayName == "第 1 年 春季 1 日" && Hud.DayText.text == game.Calendar.Date.DisplayName, "Initial date through MVC HUD");
                    Require(game.Weather.State.Kind == WeatherKind.Sunny, "New game first day stays sunny");
                    game.Inventory.TryAdd("tomato_seed", 2); game.Inventory.TryAdd("corn_seed", 3);
                    game.ToggleInventory();
                    int slot = Enumerable.Range(0, game.Inventory.State.Capacity).First(i => game.Inventory.State.Slots[i].ItemId == "tomato_seed");
                    Bag.Slots[slot].SelectButton.onClick.Invoke(); Phase(2);
                }
                else if (phase == 2 && game.State == GameFlowState.Playing)
                {
                    Require(Bag.DetailText.text.Contains("夏季") && Bag.DetailText.text.Contains("当前季节不适宜"), "Bag explains planting season");
                    CheckText(Bag.DetailText); Capture(game, "SeasonBagPreview"); Bag.UseSeedButton.onClick.Invoke();
                    Require(game.Farm.SeedItemId == "tomato_seed" && !game.UI.IsInventoryVisible, "Future-season seed remains selectable");
                    game.Farm.TryAct(0, FarmTool.Hoe, out _); int stamina = game.Stamina.State.Current;
                    Require(!game.Farm.TryAct(0, FarmTool.Seeds, out string message) && message.Contains("季节")
                        && game.Inventory.GetCount("tomato_seed") == 2 && game.Stamina.State.Current == stamina && !game.Farm.State.Cells[0].Planted, "Season rejection is free and atomic");
                    game.Players.RestorePosition(game.Shop.Counter.position + Vector3.back); game.OpenShop(); Phase(3);
                }
                else if (phase == 3 && game.UI.HasShop && UnityEngine.Object.FindObjectOfType<ShopView>() != null
                    && UnityEngine.Object.FindObjectOfType<ShopView>().StockText.text.Contains("适宜播种"))
                {
                    ShopView shop = UnityEngine.Object.FindObjectOfType<ShopView>(); shop.CropButtons[1].onClick.Invoke();
                    Require(shop.StockText.text.Contains("夏季") && shop.StockText.text.Contains("当前季节不适宜") && shop.BuyButton.interactable, "Shop displays seasons and allows year-round buying");
                    CheckText(shop.StockText); shop.BuyButton.onClick.Invoke(); Require(game.Inventory.GetCount("tomato_seed") == 3, "Out-of-season purchase works");
                    Capture(game, "SeasonShopPreview"); game.CloseShop(); SetDay(game, 28); game.Farm.SelectSeed("turnip_seed");
                    Require(game.Farm.TryAct(0, FarmTool.Seeds, out _) && game.Farm.TryAct(0, FarmTool.WateringCan, out _), "Plant turnip on last spring day");
                    game.AdvanceDay(); game.Farm.TryAct(1, FarmTool.Hoe, out _); int stamina = game.Stamina.State.Current;
                    Require(!game.Farm.TryAct(1, FarmTool.Seeds, out _) && game.Stamina.State.Current == stamina, "Summer blocks new turnip planting");
                    game.Farm.SelectSeed("tomato_seed"); Require(game.Farm.TryAct(1, FarmTool.Seeds, out _), "Summer allows tomato");
                    game.Weather.ApplyRain(); game.Players.RestorePosition(new Vector3(0, 0.1f, -4.6f)); Phase(4);
                }
                else if (phase == 4 && game.State == GameFlowState.Playing)
                {
                    Require(game.Calendar.Date.DisplayName == "第 1 年 夏季 1 日" && Hud.DayText.text == game.Calendar.Date.DisplayName
                        && game.Weather.State.Kind == WeatherKind.Rainy, "Season/weather/HUD advance together using target-day chance");
                    Require(game.Farm.State.Cells[0].Growth == 1 && game.Farm.State.Cells[0].Watered && game.Farm.State.Cells[1].Watered, "Cross-season growth and rainy new crop watering");
                    CheckText(Hud.DayText); Capture(game, "SeasonSummerPreview"); SessionState.SetInt(Running + ".Seed", game.Weather.State.Seed);
                    game.ReturnToMainMenu(); Phase(5);
                }
                else if (phase == 5 && game.State == GameFlowState.MainMenu)
                {
                    Require(game.Calendar.Config == null && game.Weather.State == null && game.Farm.State == null, "Calendar lifecycle clears on return");
                    Require(game.Saves.TryLoad(out SaveData data) && data.Version == SaveData.CurrentVersion && data.Day == 29 && data.Cells[0].Growth == 1
                        && data.Weather == WeatherKind.Rainy && data.SelectedSeedItemId == "tomato_seed", "Existing save fields preserve season context");
                    game.ContinueGame(); Phase(6);
                }
                else if (phase == 6 && game.State == GameFlowState.Playing)
                {
                    Require(game.Calendar.Date.DisplayName == "第 1 年 夏季 1 日" && game.Weather.State.Kind == WeatherKind.Rainy
                        && game.Weather.State.Seed == SessionState.GetInt(Running + ".Seed", 0) && game.Farm.State.Cells[0].Growth == 1, "Load restores season and weather without duplicate growth");
                    game.AdvanceDay(); game.AdvanceDay(); game.Farm.SelectSeed("turnip_seed");
                    Require(game.Farm.State.Cells[0].Mature && game.Farm.TryAct(0, FarmTool.Seeds, out _) && game.Inventory.GetCount("turnip") == 1, "Cross-season crop matures and harvests despite unsuitable selected seed");
                    SetDay(game, 56); game.Farm.SelectSeed("corn_seed"); game.Farm.TryAct(2, FarmTool.Hoe, out _);
                    Require(game.Farm.TryAct(2, FarmTool.Seeds, out _) && game.Farm.TryAct(2, FarmTool.WateringCan, out _), "Plant corn at summer end");
                    game.AdvanceDay(); Require(game.Calendar.Date.Season == Season.Autumn && game.Calendar.Date.Day == 1
                        && game.Weather.State.Kind == WeatherKind.Sunny && game.Farm.State.Cells[2].Growth == 1, "Autumn uses autumn rain probability and preserves crops");
                    game.Farm.TryAct(3, FarmTool.Hoe, out _); Require(game.Farm.TryAct(3, FarmTool.Seeds, out _), "Autumn still allows corn");
                    SetDay(game, 84); game.AdvanceDay(); game.Farm.TryAct(4, FarmTool.Hoe, out _);
                    foreach (var crop in game.Farm.Crops.Crops)
                    { game.Farm.SelectSeed(crop.SeedItemId, false); Require(!game.Farm.TryAct(4, FarmTool.Seeds, out _), "Default winter disallows new crop: " + crop.Id); }
                    Require(game.Calendar.Date.Season == Season.Winter && game.Weather.State.Kind == WeatherKind.Sunny && game.Farm.State.Cells[2].Planted, "Winter leaves existing crop intact");
                    SetDay(game, 112); game.AdvanceDay(); Require(game.Calendar.Date.DisplayName == "第 2 年 春季 1 日", "Winter-to-spring increments year");
                    game.Farm.SelectSeed("turnip_seed"); Require(game.Farm.TryAct(4, FarmTool.Seeds, out _), "New spring permits turnip again");
                    game.UI.RefreshViews(); CheckText(Hud.DayText); game.ReturnToMainMenu(); Phase(7);
                }
                else if (phase == 7 && game.State == GameFlowState.MainMenu)
                {
                    File.WriteAllText("Logs/CalendarFlowVerification.txt", "PASS: packed SeasonConfig AA and crop season validation; year/season/day MVC HUD and Chinese glyphs.\n"
                        + "Bag/shop seasonal descriptions fit prefabs; year-round purchases, season failure atomicity, selected seed and day boundary restrictions.\n"
                        + "All seasonal boundaries, target-day weather chance, cross-season growth/harvest, winter preservation, year rollover.\n"
                        + "Version 6 save/resume preserves total day/weather/selection/progress; CalendarManager cleanup.\n");
                    Debug.Log("Calendar flow verification PASSED."); Phase(8); EditorApplication.isPlaying = false;
                }
            }
            catch (Exception error) { SessionState.SetBool(Running, false); Debug.LogException(error); EditorApplication.Exit(1); }
        }
    }
}
