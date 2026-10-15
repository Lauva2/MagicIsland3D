using System;
using System.Linq;
using MagicIsland.Farming;
using MagicIsland.Gathering;
using MagicIsland.Inventory;
using MagicIsland.Saving;

internal static class VerifyBeachForage
{
    static int checks;
    static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    static void Reject(Action action)
    {
        bool rejected = false;
        try { action(); } catch (ArgumentException) { rejected = true; } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "Malformed beach state rejected");
    }
    static readonly ItemDefinition[] items = {
        new ItemDefinition("beach_shell", "贝壳", ItemType.Material, 99),
        new ItemDefinition("driftwood", "漂流木", ItemType.Material, 99),
        new ItemDefinition("sea_glass", "海玻璃", ItemType.Material, 99),
        new ItemDefinition("stone", "石头", ItemType.Material, 99),
        new ItemDefinition("turnip_seed", "种子", ItemType.Seed, 99),
        new ItemDefinition("turnip", "芜菁", ItemType.Crop, 99) };
    static string Fingerprint(BeachForageState state) => string.Join("|", state.Entries.Select(e => e.Id + ":" + e.ItemId));
    public static int Main()
    {
        Reject(() => new BeachForageDefinition(2)); Reject(() => new BeachForageDefinition(65));
        Reject(() => new BeachForageDefinition(initialCount: 17)); Reject(() => new BeachForageDefinition(dailyCount: 0));
        Reject(() => new BeachForageDefinition(maximumActive: 25)); Reject(() => new BeachForageState(0, 1));
        Reject(() => new BeachForageState(1, 0)); Reject(() => new BeachForageState(int.MaxValue, 1));
        var state = new BeachForageState(1, 73192); var bag = new InventoryState(10, items);
        Check(state.Count == 10 && state.Entries.Select(e => e.Site).Distinct().Count() == 10, "Fresh beach has ten distinct sites");
        Check(Fingerprint(state) == Fingerprint(new BeachForageState(1, 73192)), "Seed reproduces loot and site choice");
        Check(Fingerprint(state) != Fingerprint(new BeachForageState(1, 73193)), "Different island seed changes the beach");
        var entry = state.Entries[0]; double angle = state.AngleRadians(entry);
        Check(angle >= 0 && angle < Math.PI * 2, "Stable site angle remains within circumference");
        Check(state.Check("missing", bag) == GatherResult.InvalidTarget && state.Collect(null, bag) == GatherResult.InvalidTarget, "Missing loot cannot pay out");
        var full = new InventoryState(1, items); full.TryAdd("stone", 99);
        Check(state.Collect(entry.Id, full) == GatherResult.BagFull && state.Find(entry.Id) != null && full.GetCount("stone") == 99, "Full backpack retains visible beach loot");
        GatherResult reentry = GatherResult.Success;
        Action observer = () => {
            Check(state.Find(entry.Id) == null && bag.GetCount(entry.ItemId) == 1, "Inventory observers see committed loot and beach state");
            reentry = state.Collect(state.Entries[0].Id, bag);
        };
        bag.Changed += observer;
        Check(state.Collect(entry.Id, bag) == GatherResult.Success && reentry == GatherResult.Busy, "Reentrant collection cannot grant more items");
        bag.Changed -= observer;
        Check(state.Collect(entry.Id, bag) == GatherResult.InvalidTarget && bag.GetCount(entry.ItemId) == 1, "Repeat click pays only once");
        var save = state.Capture(); var loaded = new BeachForageState(1, 73192); loaded.Restore(save);
        Check(loaded.Count == 9 && loaded.Find(entry.Id) == null, "Reload does not regenerate a collected item");
        save.Entries[0].ItemId = "stone";
        Check(loaded.Find(save.Entries[0].Id).ItemId != "stone", "Saved DTO cannot mutate live entries");
        Action<Action<SavedBeachForage>> invalid = mutate => {
            var bad = loaded.Capture(); mutate(bad); string before = Fingerprint(loaded);
            Reject(() => loaded.Restore(bad)); Check(Fingerprint(loaded) == before, "Rejected restore leaves world unchanged");
        };
        Reject(() => loaded.Restore(null)); invalid(s => s.Day++); invalid(s => s.Seed++);
        invalid(s => s.Entries = null); invalid(s => s.Entries[0] = null);
        invalid(s => s.Entries[0].Site = 24); invalid(s => s.Entries[0].SpawnDay = 2);
        invalid(s => s.Entries[0].Id = "beach_1_99"); invalid(s => s.Entries[0].ItemId = "stone");
        invalid(s => s.Entries[1] = s.Entries[0]);
        var retained = loaded.Entries[0]; loaded.NextDay(2);
        Check(loaded.Count == 14 && loaded.Find(retained.Id).ItemId == retained.ItemId && loaded.AngleRadians(retained) == state.AngleRadians(retained), "Next day adds five and retains uncollected items in place");
        loaded.NextDay(3); Check(loaded.Count == 16, "Beach respects maximum population instead of unlimited accumulation");
        string capped = Fingerprint(loaded); loaded.NextDay(4);
        Check(loaded.Count == 16 && Fingerprint(loaded) == capped, "Full beach waits without replacing existing loot");
        foreach (var value in loaded.Entries.ToArray()) Check(loaded.Collect(value.Id, bag) == GatherResult.Success, "All three types can be collected");
        Check(loaded.Count == 0, "Fully picked beach stays empty during the day");
        var empty = new BeachForageState(4, 73192); empty.Restore(loaded.Capture());
        Check(empty.Count == 0, "Empty snapshot remains empty on reload");
        empty.NextDay(5); Check(empty.Count == 5, "Picked beach receives only the daily refill");
        Reject(() => empty.NextDay(5)); Reject(() => empty.NextDay(7));
        var nearLimit = new BeachForageState(int.MaxValue - 2, 1); nearLimit.NextDay(int.MaxValue - 1);
        Reject(() => nearLimit.NextDay(int.MaxValue));
        var farm = new FarmState(2); var snapshot = SaveData.Capture(farm, bag, 0, 0, 0, beachForage: state);
        snapshot.Validate(2, 3, new InventoryState(10, items));
        Check(snapshot.BeachForage.Entries.Length == 9 && snapshot.Version == SaveData.CurrentVersion, "World snapshot includes remaining beach loot");
        var old = SaveData.Capture(farm, bag, 0, 0, 0); old.Version = 28; old.BeachForage = null; old.Gold = 123;
        old.Upgrade(100); old.Validate(2, 3, new InventoryState(10, items));
        Check(old.Version == 29 && old.BeachForage.Day == 1 && old.BeachForage.Entries.Length == 10 && old.Gold == 123, "Version 28 adds fresh beach without changing inventory or gold");
        snapshot.Upgrade(999); Check(snapshot.BeachForage.Entries.Length == 9, "Current save upgrade never refills picked loot");
        var malformed = SaveData.Capture(farm, bag, 0, 0, 0); malformed.BeachForage = null;
        Reject(() => malformed.Validate(2, 3, new InventoryState(10, items)));
        for (int seed = 1; seed <= 32; seed++)
        {
            var coast = new BeachForageState(1, seed); var restore = new BeachForageState(1, seed);
            restore.Restore(coast.Capture()); coast.NextDay(2); restore.NextDay(2);
            Check(Fingerprint(coast) == Fingerprint(restore), "Refill remains deterministic after reload");
            Check(coast.Count == 15 && coast.Entries.All(e => BeachForageState.IsBeachItem(e.ItemId)), "Refill stays bounded and only yields beach materials");
            for (int day = 3; day <= 30; day++) coast.NextDay(day);
            Check(coast.Count == 16 && coast.Entries.Select(e => e.Site).Distinct().Count() == 16, "Long term accumulation stays bounded with no site overlap");
        }
        Console.WriteLine("PASS: " + checks + " beach collection/refill/inventory/save assertions"); return 0;
    }
}
