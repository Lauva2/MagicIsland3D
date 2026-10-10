using System;
using System.Linq;
using MagicIsland.CalendarSystem;
using MagicIsland.Economy;
using MagicIsland.Farming;
using MagicIsland.Gathering;
using MagicIsland.Inventory;
using MagicIsland.Orders;
using MagicIsland.Saving;
using MagicIsland.Stamina;
using MagicIsland.Tasks;
internal static class VerifyGathering
{
    static int checks;
    static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    static void Reject(Action action) { bool rejected = false; try { action(); } catch (ArgumentException) { rejected = true; } Check(rejected, "Invalid gathering rejected"); }
    static readonly GatheringDefinition[] definitions = {
        new GatheringDefinition("tree", "wood", FarmTool.Axe, 3, 3, 4, 3), new GatheringDefinition("rock", "stone", FarmTool.Pickaxe, 2, 2, 5, 2) };
    static readonly ItemDefinition[] items = { new ItemDefinition("wood", "wood", ItemType.Material, 99), new ItemDefinition("stone", "stone", ItemType.Material, 99),
        new ItemDefinition("turnip_seed", "seed", ItemType.Seed, 99), new ItemDefinition("turnip", "crop", ItemType.Crop, 99) };
    public static int Main()
    {
        Reject(() => new GatheringDefinition("", "wood", FarmTool.Axe, 3, 3, 4, 3));
        Reject(() => new GatheringDefinition("x", "", FarmTool.Axe, 3, 3, 4, 3));
        Reject(() => new GatheringDefinition("x", "wood", FarmTool.Hoe, 3, 3, 4, 3));
        Reject(() => new GatheringDefinition("x", "wood", (FarmTool)99, 3, 3, 4, 3));
        Reject(() => new GatheringDefinition("x", "wood", FarmTool.Axe, 0, 3, 4, 3));
        Reject(() => new GatheringDefinition("x", "wood", FarmTool.Axe, 21, 3, 4, 3));
        Reject(() => new GatheringDefinition("x", "wood", FarmTool.Axe, 3, 0, 4, 3));
        Reject(() => new GatheringDefinition("x", "wood", FarmTool.Axe, 3, 100, 4, 3));
        Reject(() => new GatheringDefinition("x", "wood", FarmTool.Axe, 3, 3, -1, 3));
        Reject(() => new GatheringDefinition("x", "wood", FarmTool.Axe, 3, 3, 4, 0));
        Reject(() => new GatheringDefinition("x", "wood", FarmTool.Axe, 3, 3, 4, 366));
        Reject(() => new GatheringState(Array.Empty<GatheringDefinition>(), 1));
        Reject(() => new GatheringState(new[] { definitions[0], definitions[0] }, 1)); Reject(() => new GatheringState(definitions, 0));
        var state = new GatheringState(definitions, 1); var bag = new InventoryState(4, items); var stamina = new StaminaState(100);
        Check(state.Hit(-1, FarmTool.Axe, bag, stamina) == GatherResult.InvalidTarget && state.Hit(2, FarmTool.Axe, bag, stamina) == GatherResult.InvalidTarget, "Invalid index safe");
        Check(state.Hit(0, FarmTool.Pickaxe, bag, stamina) == GatherResult.WrongTool && stamina.Current == 100 && state.HitsRemaining(0) == 3, "Wrong tool changes nothing");
        stamina.Restore(3); Check(state.Hit(0, FarmTool.Axe, bag, stamina) == GatherResult.InsufficientStamina && state.HitsRemaining(0) == 3, "Low stamina free"); stamina.Refill();
        var full = new InventoryState(1, items); full.TryAdd("wood", 97);
        Check(state.Hit(0, FarmTool.Axe, full, stamina) == GatherResult.BagFull && state.HitsRemaining(0) == 3 && stamina.Current == 100 && full.GetCount("wood") == 97, "Need space for entire output even before final hit");
        Check(state.Hit(0, FarmTool.Axe, bag, stamina) == GatherResult.Success && state.HitsRemaining(0) == 2 && stamina.Current == 96 && bag.GetCount("wood") == 0, "Partial hit costs stamina, no early output");
        var partial = state.Capture(); var resumed = new GatheringState(definitions, 1); resumed.Restore(partial); partial[0].HitsRemaining = 3;
        Check(resumed.HitsRemaining(0) == 2, "Partial restore independent");
        resumed.Hit(0, FarmTool.Axe, bag, stamina); Check(resumed.HitsRemaining(0) == 1 && bag.GetCount("wood") == 0, "Second hit still partial");
        GatherResult reentry = GatherResult.Success;
        Action observer = () => { Check(resumed.IsDepleted(0) && resumed.RespawnDay(0) == 4 && stamina.Current == 88 && bag.GetCount("wood") == 3, "Observers see resource/stamina/bag committed"); reentry = resumed.Hit(1, FarmTool.Pickaxe, bag, stamina); };
        bag.Changed += observer;
        Check(resumed.Hit(0, FarmTool.Axe, bag, stamina) == GatherResult.Success && reentry == GatherResult.Busy, "Observer reentry blocked"); bag.Changed -= observer;
        Check(resumed.Hit(0, FarmTool.Axe, bag, stamina) == GatherResult.Depleted && stamina.Current == 88 && bag.GetCount("wood") == 3, "Depleted resource pays once");
        resumed.Hit(1, FarmTool.Pickaxe, bag, stamina); resumed.Hit(1, FarmTool.Pickaxe, bag, stamina);
        Check(resumed.IsDepleted(1) && resumed.RespawnDay(1) == 3 && stamina.Current == 78 && bag.GetCount("stone") == 2, "Rock tool/cost/output independent");
        var save = resumed.Capture(); var copy = new GatheringState(definitions, 1); copy.Restore(save); save[0].RespawnDay = 99;
        copy.NextDay(2); Check(copy.IsDepleted(0) && copy.IsDepleted(1), "First sleep retains cooldown");
        copy.NextDay(3); Check(copy.IsDepleted(0) && !copy.IsDepleted(1) && copy.HitsRemaining(1) == 2 && copy.RespawnDay(1) == 0, "Rock restores on day 3 only");
        copy.NextDay(4); Check(!copy.IsDepleted(0) && copy.HitsRemaining(0) == 3 && bag.GetCount("wood") == 3, "Tree restores day 4 without extra loot");
        Reject(() => copy.NextDay(6)); Reject(() => copy.NextDay(4));
        Action<Action<SavedGatheringNode[]>> invalid = mutate => { var bad = resumed.Capture(); mutate(bad); Reject(() => resumed.Restore(bad)); Check(resumed.IsDepleted(0) && resumed.RespawnDay(0) == 4, "Invalid restore is atomic"); };
        Reject(() => resumed.Restore(null)); Reject(() => resumed.Restore(Array.Empty<SavedGatheringNode>()));
        invalid(value => value[1] = null); invalid(value => value[0].Id = "unknown"); invalid(value => value[0].HitsRemaining = -1);
        invalid(value => value[0].HitsRemaining = 4); invalid(value => value[0].RespawnDay = 1); invalid(value => value[0].RespawnDay = 5);
        invalid(value => { value[0].HitsRemaining = 1; value[0].RespawnDay = 4; });
        var nearLimit = new GatheringState(definitions, int.MaxValue - 2); nearLimit.Hit(0, FarmTool.Axe, bag, stamina); nearLimit.Hit(0, FarmTool.Axe, bag, stamina); nearLimit.Hit(0, FarmTool.Axe, bag, stamina);
        Check(nearLimit.RespawnDay(0) == (long)int.MaxValue + 1, "Cooldown arithmetic cannot overflow"); nearLimit.NextDay(int.MaxValue - 1);
        Reject(() => nearLimit.NextDay(int.MaxValue));
        for (int day = 1; day <= 180; day++)
        {
            var cycle = new GatheringState(definitions, day); var cycleStamina = new StaminaState(100); var cycleBag = new InventoryState(4, items);
            for (int i = 0; i < definitions.Length; i++) for (int hit = 0; hit < definitions[i].Hits; hit++)
                Check(cycle.Hit(i, definitions[i].Tool, cycleBag, cycleStamina) == GatherResult.Success, "Fresh node hit");
            for (int offset = 1; offset <= 3; offset++)
            {
                var loaded = new GatheringState(definitions, cycle.Day); loaded.Restore(cycle.Capture()); loaded.NextDay(day + offset); cycle = loaded;
                Check(cycle.IsDepleted(0) == (offset < 3) && cycle.IsDepleted(1) == (offset < 2) && cycleBag.GetCount("wood") == 3 && cycleBag.GetCount("stone") == 2, "Repeated reload retains exact respawn day");
            }
        }
        var farm = new FarmState(2); var tasks = new[] { new TaskDefinition("hoe", TaskAction.Hoe, 1, 5) }; var taskState = new TaskState(tasks);
        taskState.Record(TaskAction.Hoe); var gold = new EconomyState(100); taskState.TryClaim(gold);
        var orders = new[] { new OrderDefinition("order", "turnip", SeasonMask.All, 1, 1, 40) }; var orderState = new OrderState(orders, 28, 1, 1);
        var snapshot = SaveData.Capture(farm, bag, 0, 0, 0, gold.Gold, tasks: taskState, orders: orderState, gathering: resumed);
        snapshot.Validate(2, 3, new InventoryState(4, items), tasks: tasks, orders: orders, gathering: definitions);
        Check(snapshot.Version == SaveData.CurrentVersion && snapshot.Gathering[0].RespawnDay == 4, "Snapshot includes cooldown");
        for (int version = 1; version <= 8; version++)
        {
            var legacy = SaveData.Capture(farm, bag, 0, 0, 0, 105, tasks: taskState, orders: orderState); legacy.Version = version;
            legacy.Upgrade(100, tasks: tasks, orders: orders, gathering: definitions);
            legacy.Validate(2, 3, new InventoryState(4, items), tasks: tasks, orders: orders, gathering: definitions);
            Check(legacy.Version == SaveData.CurrentVersion && legacy.Gathering.All(value => value.HitsRemaining > 0 && value.RespawnDay == 0)
                && legacy.Gold == (version == 1 ? 100 : 105) && legacy.Tasks[0].Claimed == (version >= 7), "Legacy migration adds fresh resources without altering loot or claimed rewards");
        }
        snapshot.Upgrade(999, tasks: tasks, orders: orders, gathering: definitions);
        Check(snapshot.Gathering[0].HitsRemaining == 0 && snapshot.Gold == 105, "Current save upgrade never resets depleted resources");
        Console.WriteLine("PASS: " + checks + " gathering/stamina/inventory/save assertions"); return 0;
    }
}
