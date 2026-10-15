using System;
using System.Linq;
using MagicIsland.Interaction;
using MagicIsland.Inventory;
using MagicIsland.Saving;
internal static class VerifyOrchard
{
    static int checks;
    static void Check(bool ok, string why) { checks++; if (!ok) throw new Exception(why); }
    static void Reject(Action action) { bool rejected = false; try { action(); } catch (ArgumentException) { rejected = true; } catch (InvalidOperationException) { rejected = true; } Check(rejected, "Invalid orchard state rejected"); }
    static ItemDefinition[] items = { new ItemDefinition("apple_tree", "树苗", ItemType.Placeable, 99), new ItemDefinition("apple", "苹果", ItemType.Crop, 99), new ItemDefinition("stone", "石料", ItemType.Material, 99) };
    static OrchardDefinition[] types = { new OrchardDefinition("apple_tree", "apple", 3, 2, 2) };
    static PlaceableDefinition[] placeables = { new PlaceableDefinition("apple_tree", 1), new PlaceableDefinition("stone") };
    public static int Main()
    {
        for (int plantingDay = 1; plantingDay <= 30; plantingDay++)
        {
            var bag = new InventoryState(2, items); bag.TryAdd("apple_tree", 1);
            var placement = new PlacementState(placeables, 5); Check(placement.Place("apple_tree", -7, 5, 0, bag, out var record) == PlacementResult.Success, "Plant sapling consumes one placeable");
            var state = new OrchardState(types, placement, plantingDay); state.Planted(record);
            Check(state.Stage(record.Id) == 0 && bag.GetCount("apple_tree") == 0, "New tree starts at seedling");
            Check(placement.CheckLocation("stone", -6, 5, 0) == PlacementResult.Occupied, "Tree clearance protects growth space");
            for (int age = 0; age < 3; age++)
            {
                Check(state.Check(record.Id, bag) == OrchardResult.NotReady && state.Harvest(record.Id, bag) == OrchardResult.NotReady, "Immature tree cannot yield");
                var saved = state.Capture(); var clone = new OrchardState(types, placement, state.Day); clone.Restore(saved); Check(clone.Stage(record.Id) == state.Stage(record.Id), "Exact growth survives restore"); state.NextDay(state.Day + 1);
            }
            Check(state.Stage(record.Id) == 2 && state.Check(record.Id, bag) == OrchardResult.Success, "Tree is ready after three sleeps");
            bool busy = false; bag.Changed += () => { if (bag.GetCount("apple") == 2) busy = state.Harvest(record.Id, bag) == OrchardResult.Busy; };
            Check(state.Harvest(record.Id, bag) == OrchardResult.Success && bag.GetCount("apple") == 2 && busy, "Harvest is atomic and rejects callback reentry");
            Check(state.Harvest(record.Id, bag) == OrchardResult.NotReady, "Repeated click cannot duplicate fruit");
            state.NextDay(state.Day + 1); Check(state.Check(record.Id, bag) == OrchardResult.NotReady, "One day is insufficient to regrow");
            state.NextDay(state.Day + 1); Check(state.Check(record.Id, bag) == OrchardResult.Success, "Fruit returns after two days");
            var full = new InventoryState(1, items); full.TryAdd("stone", 99); long before = state.Find(record.Id).NextFruitDay;
            Check(state.Harvest(record.Id, full) == OrchardResult.BagFull && state.Find(record.Id).NextFruitDay == before, "Full bag retains ready fruit and cooldown");
            for (int i = 0; i < 5; i++) state.NextDay(state.Day + 1);
            Check(bag.GetCount("apple") == 2 && state.Harvest(record.Id, bag) == OrchardResult.Success && bag.GetCount("apple") == 4, "Unpicked fruit waits without automatic accumulation");
            var snapshot = state.Capture(); snapshot[0].NextFruitDay = long.MaxValue; Reject(() => state.Restore(snapshot));
            Reject(() => state.Restore(new SavedOrchardTree[0])); Reject(() => state.Planted(new SavedPlacedObject { Id = 127, ItemId = "apple_tree" }));
            var restored = new PlacementState(placeables, 5); restored.Restore(placement.Capture()); Check(restored.CheckLocation("stone", -6, 5, 0) == PlacementResult.Occupied, "Restore preserves tree clearance");
            Check(placement.Recover(record.Id, bag) == PlacementResult.Success, "Tree can return to backpack"); state.Removed(record.Id); Check(state.Count == 0, "Recovery clears old growth and fruit");
        }
        var old = new SaveData { Version = 27 }; old.Upgrade(100); Check(old.Version == SaveData.CurrentVersion && old.Orchard.Length == 0, "Version 27 migrates without free trees");
        var boundaryBag = new InventoryState(2, items); boundaryBag.TryAdd("apple_tree", 2);
        var boundaryPlacement = new PlacementState(placeables, 5);
        Check(boundaryPlacement.Place("apple_tree", -7, 5, 0, boundaryBag, out var first) == PlacementResult.Success, "First boundary tree");
        Check(boundaryPlacement.CheckLocation("apple_tree", -6, 6, 0) == PlacementResult.Occupied && boundaryPlacement.CheckLocation("apple_tree", -5, 5, 0) == PlacementResult.Success, "Diagonal clearance and two-cell spacing");
        Check(boundaryPlacement.Place("apple_tree", -5, 5, 0, boundaryBag, out var second) == PlacementResult.Success, "Second boundary tree");
        var boundary = new OrchardState(types, boundaryPlacement, 1); boundary.Planted(first); boundary.Planted(second);
        var copy = boundary.Capture(); copy[0].PlantedDay = 0;
        Check(boundary.Find(first.Id).PlantedDay == 1, "Capture is a deep snapshot");
        var found = boundary.Find(first.Id); found.NextFruitDay = long.MaxValue;
        Check(boundary.Find(first.Id).NextFruitDay == 4, "Find cannot mutate runtime growth");
        Reject(() => boundary.Restore(null));
        Reject(() => boundary.Restore(new[] { (SavedOrchardTree)null, boundary.Find(second.Id) }));
        Reject(() => boundary.Restore(new[] { boundary.Find(first.Id), boundary.Find(first.Id) }));
        Reject(() => boundary.Restore(new[] { new SavedOrchardTree { PlacementId = 128, PlantedDay = 1, NextFruitDay = 4 }, boundary.Find(second.Id) }));
        foreach (long badDate in new long[] { -1, 0, 3, 5, 6, long.MaxValue })
        { var invalid = boundary.Capture(); invalid[0].NextFruitDay = badDate; Reject(() => boundary.Restore(invalid)); }
        Check(boundary.Count == 2 && boundary.Find(first.Id).NextFruitDay == 4, "Rejected restore preserves complete running orchard");
        Reject(() => boundary.NextDay(1)); Reject(() => boundary.NextDay(3));
        for (int day = 2; day <= 4; day++) boundary.NextDay(day);
        var almostFull = new InventoryState(1, items); almostFull.TryAdd("apple", 98);
        Check(boundary.Harvest(first.Id, almostFull) == OrchardResult.BagFull && almostFull.GetCount("apple") == 98 && boundary.Find(first.Id).NextFruitDay == 4, "Entire two-fruit yield must fit; no partial harvest");
        Check(boundaryPlacement.Recover(first.Id, boundaryBag) == PlacementResult.Success, "Recover ripe tree"); boundary.Removed(first.Id);
        Check(boundaryPlacement.Place("apple_tree", -7, 5, 0, boundaryBag, out var replanted) == PlacementResult.Success, "Replant recovered sapling"); boundary.Planted(replanted);
        Check(boundary.Stage(replanted.Id) == 0 && boundary.Find(replanted.Id).PlantedDay == 4 && boundary.Find(replanted.Id).NextFruitDay == 7 && boundary.Check(replanted.Id, boundaryBag) == OrchardResult.NotReady, "Reused placement ID starts fresh without old ripe fruit");
        Reject(() => new OrchardDefinition("x", "x", 3, 2, 2)); Reject(() => new OrchardDefinition("x", "y", 1, 2, 2));
        Console.WriteLine("PASS: " + checks + " orchard growth/harvest/space/save assertions"); return 0;
    }
}
