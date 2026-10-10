using System;
using MagicIsland.CalendarSystem;
using MagicIsland.Farming;
using MagicIsland.Inventory;
using MagicIsland.Stamina;
using MagicIsland.WeatherSystem;
using MagicIsland.Saving;

internal static class VerifyCalendar
{
    static int checks;
    static void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
    static void Reject(Action action) { bool rejected = false; try { action(); } catch (ArgumentException) { rejected = true; } Check(rejected, "Invalid calendar input rejected"); }
    public static int Main()
    {
        for (int length = 1; length <= 365; length++)
            for (int season = 0; season < 4; season++)
            {
                var first = new CalendarDate(season * length + 1, length);
                var last = new CalendarDate((season + 1) * length, length);
                Check(first.Year == 1 && (int)first.Season == season && first.Day == 1, "First day of each season");
                Check(last.Year == 1 && (int)last.Season == season && last.Day == length, "Last day of each season");
                var nextYear = new CalendarDate(length * 4 + 1, length);
                Check(nextYear.Year == 2 && nextYear.Season == Season.Spring && nextYear.Day == 1, "New year boundary");
            }
        Check(new CalendarDate(int.MaxValue - 1, 28).Year > 0, "Large saved day does not overflow");
        Reject(() => new CalendarDate(0, 28)); Reject(() => new CalendarDate(int.MaxValue, 28));
        Reject(() => new CalendarDate(1, 0)); Reject(() => new CalendarDate(1, 366));
        for (int mask = 1; mask <= 15; mask++)
            for (int season = 0; season < 4; season++) Check(Seasons.Includes((SeasonMask)mask, (Season)season) == ((mask & (1 << season)) != 0), "Season flag membership");
        Check(!Seasons.ValidMask(SeasonMask.None) && !Seasons.ValidMask((SeasonMask)16) && !Seasons.ValidMask((SeasonMask)(-1)), "Invalid masks rejected");
        Check(!Seasons.Includes(SeasonMask.All, (Season)4), "Invalid season rejected");
        Check(Seasons.Describe(SeasonMask.Spring | SeasonMask.Autumn) == "春季、秋季" && Seasons.Describe(SeasonMask.All) == "四季", "Season labels");

        var defs = new[] { new CropDefinition("turnip", "seed", "crop", 3), new CropDefinition("tomato", "tomato_seed", "tomato", 5) };
        var items = new[] { new ItemDefinition("seed", "seed", ItemType.Seed, 99), new ItemDefinition("crop", "crop", ItemType.Crop, 99),
            new ItemDefinition("tomato_seed", "tomato_seed", ItemType.Seed, 99), new ItemDefinition("tomato", "tomato", ItemType.Crop, 99) };
        var bag = new InventoryState(4, items);
        bag.TryAdd("seed", 3); bag.TryAdd("tomato_seed", 3);
        var farm = new FarmState(2, 3, defs); var stamina = new StaminaState(100);
        var actions = new FarmActions(farm, bag, "seed", "crop", stamina, 3, 1, 2, 0, defs,
            crop => Seasons.Includes(crop.Id == "turnip" ? SeasonMask.Spring | SeasonMask.Autumn : SeasonMask.Summer, new CalendarDate(farm.Day, 28).Season));
        Check(actions.SelectSeed("tomato_seed"), "Out-of-season seed can be selected for later planting");
        actions.TryAct(0, FarmTool.Hoe, out _); int before = stamina.Current, notifications = 0;
        bag.Changed += () => notifications++;
        Check(!actions.TryAct(0, FarmTool.Seeds, out string message) && message.Contains("季节"), "Wrong season rejects planting with explanation");
        Check(!farm.Cells[0].Planted && bag.GetCount("tomato_seed") == 3 && stamina.Current == before && notifications == 0, "Failure is atomic and free");
        actions.SelectSeed("seed"); farm.Restore(28, farm.Cells);
        Check(actions.TryAct(0, FarmTool.Seeds, out _) && actions.TryAct(0, FarmTool.WateringCan, out _), "Last day of spring accepts turnip");
        farm.AdvanceDay(); Check(new CalendarDate(farm.Day, 28).Season == Season.Summer && farm.Cells[0].Growth == 1, "Cross-season crop continues growing");
        actions.TryAct(1, FarmTool.Hoe, out _); before = stamina.Current;
        Check(!actions.TryAct(1, FarmTool.Seeds, out _) && stamina.Current == before, "Same selected seed checked against new day");
        actions.SelectSeed("tomato_seed"); Check(actions.TryAct(1, FarmTool.Seeds, out _), "Summer allows tomato");
        farm.Cells[0].Growth = 3; actions.SelectSeed("seed");
        Check(actions.TryAct(0, FarmTool.Seeds, out _) && bag.GetCount("crop") == 1, "Mature crop harvest takes precedence over season restriction");
        var save = SaveData.Capture(farm, bag, 0, 0, 0, 100, 360, stamina.Current, "seed");
        save.Validate(2, 3, new InventoryState(4, items), 100, defs);
        var restored = new FarmState(2, 3, defs); restored.Restore(save.Day, save.Cells);
        Check(save.Version == SaveData.CurrentVersion && new CalendarDate(restored.Day, 28).DisplayName == "第 1 年 夏季 1 日", "Version 6 total day restores calendar without new fields");
        var weather = new WeatherState(42, 0, 28); weather.NextDay(29, 1);
        Check(weather.Kind == WeatherKind.Rainy, "Target-day summer rain probability");
        weather.NextDay(30, 0); Check(weather.Kind == WeatherKind.Sunny, "Per-day rain probability can change");
        Reject(() => weather.NextDay(31, double.NaN)); Check(weather.Day == 30, "Invalid chance leaves weather intact");
        var uninterrupted = new WeatherState(321, 0, 28); uninterrupted.NextDay(29, 0.2);
        var loaded = new WeatherState(uninterrupted.Seed, 0, uninterrupted.Day, uninterrupted.Kind);
        for (int day = 30; day <= 120; day++)
        {
            double chance = (int)new CalendarDate(day, 28).Season * 0.2;
            uninterrupted.NextDay(day, chance); loaded.NextDay(day, chance);
            Check(uninterrupted.Kind == loaded.Kind, "Seasonal weather sequence remains stable after reload");
        }
        Console.WriteLine("PASS: " + checks + " calendar/season/planting/weather/save assertions"); return 0;
    }
}
