using System;
using MagicIsland.WeatherSystem;
using MagicIsland.Farming;
using MagicIsland.Inventory;
using MagicIsland.Saving;
using MagicIsland.Stamina;

internal static class VerifyWeather
{
    static int checks;
    static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    static void Reject(Action action)
    {
        bool rejected = false;
        try { action(); } catch (ArgumentException) { rejected = true; } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "Invalid weather rejected");
    }
    static InventoryState Bag() => new InventoryState(4, new[] {
        new ItemDefinition("turnip_seed", "Seed", ItemType.Seed, 99), new ItemDefinition("turnip", "Crop", ItemType.Crop, 99) });

    public static int Main()
    {
        Reject(() => new WeatherState(0, 0.3)); Reject(() => new WeatherState(1, double.NaN));
        Reject(() => new WeatherState(1, -0.1)); Reject(() => new WeatherState(1, 1.1));
        Reject(() => new WeatherState(1, 0.3, 1, (WeatherKind)99));
        var dry = new WeatherState(42, 0); var wet = new WeatherState(42, 1);
        Check(dry.Kind == WeatherKind.Sunny && wet.Kind == WeatherKind.Sunny, "First day is always sunny");
        for (int day = 2; day <= 20; day++)
        { dry.NextDay(day); wet.NextDay(day); Check(dry.Kind == WeatherKind.Sunny && wet.Kind == WeatherKind.Rainy, "Chance endpoints"); }
        Reject(() => wet.NextDay(22)); Check(wet.Day == 20 && wet.Kind == WeatherKind.Rainy, "Rejected day is atomic");
        var original = new WeatherState(74521, 0.3); int rainy = 0;
        for (int day = 2; day <= 100; day++) { original.NextDay(day); if (original.Kind == WeatherKind.Rainy) rainy++; }
        Check(rainy > 10 && rainy < 60, "Mixed forecast for typical chance");
        var resumed = new WeatherState(original.Seed, 0.3, original.Day, original.Kind);
        for (int day = 101; day <= 130; day++)
        { original.NextDay(day); resumed.NextDay(day); Check(original.Kind == resumed.Kind, "Saved seed preserves subsequent forecast"); }
        Check(DaylightCycle.Evaluate(0) == 0 && DaylightCycle.Evaluate(1440) == 0, "Midnight endpoints match");
        Check(DaylightCycle.Evaluate(300) == 0 && DaylightCycle.Evaluate(480) == 1
            && DaylightCycle.Evaluate(1020) == 1 && DaylightCycle.Evaluate(1200) == 0, "Lighting boundaries");
        Check(DaylightCycle.Evaluate(390) == 0.5 && DaylightCycle.Evaluate(1110) == 0.5, "Dawn/dusk interpolate");
        double previous = 0;
        for (int minute = 300; minute <= 480; minute += 10)
        { double value = DaylightCycle.Evaluate(minute); Check(value >= previous && value <= 1, "Dawn rises smoothly"); previous = value; }
        Reject(() => DaylightCycle.Evaluate(double.NaN)); Reject(() => DaylightCycle.Evaluate(-1));
        Reject(() => DaylightCycle.Evaluate(1441)); Reject(() => DaylightCycle.Evaluate(360, 480, 480, 1020, 1200));

        var farm = new FarmState(2); var bag = Bag(); bag.TryAdd("turnip_seed", 5); var stamina = new StaminaState(100);
        var actions = new FarmActions(farm, bag, "turnip_seed", "turnip", stamina);
        actions.TryAct(0, FarmTool.Hoe, out _); actions.TryAct(0, FarmTool.Seeds, out _); actions.TryAct(1, FarmTool.Hoe, out _);
        int remaining = stamina.Current; long seeds = bag.GetCount("turnip_seed");
        Check(farm.WaterPlanted() && farm.Cells[0].Watered && !farm.Cells[1].Watered && !farm.Cells[2].Watered, "Rain only waters planted plots");
        Check(!farm.WaterPlanted() && stamina.Current == remaining && bag.GetCount("turnip_seed") == seeds && farm.Cells[0].Growth == 0,
            "Repeated rain consumes nothing and does not grow crops");
        farm.AdvanceDay(); Check(farm.Cells[0].Growth == 1 && !farm.Cells[0].Watered, "One rainy-day growth per morning");
        actions.TryAct(1, FarmTool.Seeds, out _); farm.WaterPlanted(); Check(farm.Cells[1].Watered, "Newly planted crop receives rain");
        for (int i = 0; i < 5; i++) { farm.WaterPlanted(); farm.AdvanceDay(); }
        Check(farm.Cells[0].Growth == 3 && farm.Cells[1].Growth == 3, "Rain never grows past maturity");
        var snapshot = SaveData.Capture(farm, bag, 0, 0.1f, -4, 73, 920.5, 46, "turnip_seed", 74521, WeatherKind.Rainy);
        snapshot.Validate(2, 3, Bag()); Check(snapshot.Version == SaveData.CurrentVersion && snapshot.WeatherSeed == 74521 && snapshot.Weather == WeatherKind.Rainy, "Weather snapshot");
        snapshot.Weather = (WeatherKind)99; Reject(() => snapshot.Validate(2, 3, Bag())); snapshot.Weather = WeatherKind.Rainy;
        snapshot.WeatherSeed = 0; Reject(() => snapshot.Validate(2, 3, Bag()));
        snapshot.Version = 5; snapshot.Upgrade(100);
        Check(snapshot.Version == SaveData.CurrentVersion && snapshot.Weather == WeatherKind.Sunny && snapshot.WeatherSeed == 1
            && snapshot.Gold == 73 && snapshot.Stamina == 46 && snapshot.ClockMinute == 920.5
            && snapshot.SelectedSeedItemId == "turnip_seed" && snapshot.Cells[0].CropId == "turnip", "v5 migration adds weather without changing existing progress");
        snapshot.Weather = WeatherKind.Rainy; snapshot.WeatherSeed = 77; snapshot.Upgrade(999);
        Check(snapshot.Weather == WeatherKind.Rainy && snapshot.WeatherSeed == 77, "Current saves never reroll weather");
        Console.WriteLine("PASS: " + checks + " weather/daylight/rain/save assertions"); return 0;
    }
}
