using System;
using System.Linq;
using MagicIsland.Inventory;
using MagicIsland.Interaction;
using MagicIsland.Saving;
using MagicIsland.Farming;
class VerifyConstruction
{
    static int checks;
    static void Check(bool ok, string name) { checks++; if (!ok) throw new Exception(name); }
    static void Reject(Action action) { try { action(); } catch (ArgumentException) { checks++; return; } throw new Exception("Invalid construction accepted"); }
    static readonly ItemDefinition[] Items = { new ItemDefinition("wood", "Wood", ItemType.Material, 5), new ItemDefinition("stone", "Stone", ItemType.Material, 5),
        new ItemDefinition("fence", "Fence", ItemType.Placeable, 5), new ItemDefinition("path", "Path", ItemType.Placeable, 5), new ItemDefinition("lamp", "Lamp", ItemType.Placeable, 5) };
    static readonly CraftingDefinition[] Recipes = { new CraftingDefinition("fence", "fence", 1, new[] { new IngredientCost("wood", 2) }),
        new CraftingDefinition("path", "path", 1, new[] { new IngredientCost("stone", 2) }), new CraftingDefinition("lamp", "lamp", 1, new[] { new IngredientCost("wood", 3), new IngredientCost("stone", 2) }) };
    static readonly PlaceableDefinition[] Placeables = { new PlaceableDefinition("fence"), new PlaceableDefinition("path"), new PlaceableDefinition("lamp") };
    static string Snap(InventoryState bag) => string.Join("|", bag.Slots.Select(s => s.ItemId + ":" + s.Quantity));
    static void Main()
    {
        for (int capacity = 2; capacity <= 5; capacity++) for (int wood = 0; wood <= 5; wood++) for (int stone = 0; stone <= 5; stone++) for (int recipe = 0; recipe < 3; recipe++)
        {
            var bag = new InventoryState(capacity, Items); if (wood > 0) bag.TryAdd("wood", wood); if (stone > 0) bag.TryAdd("stone", stone);
            var state = new CraftingState(Recipes); string before = Snap(bag); var r = Recipes[recipe]; var expected = state.Check(recipe, bag);
            int notifications = 0; bag.Changed += () => { notifications++; Check(state.Craft(recipe, bag) == CraftingResult.Busy, "Craft reentry blocked"); };
            Check(state.Craft(recipe, bag) == expected, "Check matches craft");
            if (expected == CraftingResult.Success)
            { Check(notifications == 1 && bag.GetCount(r.Output) == 1 && bag.GetCount("wood") == wood - r.Ingredients.Where(c => c.Id == "wood").Sum(c => c.Quantity)
                && bag.GetCount("stone") == stone - r.Ingredients.Where(c => c.Id == "stone").Sum(c => c.Quantity), "Atomic exact recipe exchange"); }
            else Check(Snap(bag) == before && notifications == 0, "Failed craft consumes nothing");
        }
        for (int farm = 1; farm <= 12; farm++) for (int x = -12; x <= 12; x++) for (int z = -12; z <= 12; z++)
        {
            var state = new PlacementState(Placeables, farm); var result = state.CheckLocation("fence", x, z, 0); double half = farm * .6 + .5;
            bool valid = Math.Abs(x) <= 11 && Math.Abs(z) <= 11 && !(x + .5 > -half && x - .5 < half && z + .5 > -half && z - .5 < half);
            Check((result == PlacementResult.Success) == valid, "All island/farm grid positions");
        }
        var route = new NPCScheduleDefinition("npc", 1.4, 720, 1080, new[] { new SchedulePoint(7.4, .95), new SchedulePoint(11.2, .95), new SchedulePoint(11.2, -6.8), new SchedulePoint(9.15, -6.8) });
        var placement = new PlacementState(Placeables, 5, new[] { route }); var inventory = new InventoryState(3, Items); inventory.TryAdd("fence", 5);
        Check(placement.CheckLocation("fence", 11, -4, 0) == PlacementResult.RouteReserved && placement.CheckLocation("fence", 9, -7, 0) == PlacementResult.RouteReserved, "NPC corridor/endpoints reserved");
        foreach (int turns in new[] { -1, 4, int.MaxValue }) Check(placement.CheckPlace("fence", -6, -7, turns, inventory) == PlacementResult.InvalidPosition, "Invalid rotation rejected");
        int events = 0; inventory.Changed += () => { events++; Check(placement.Count == 1 && inventory.GetCount("fence") == 4, "Observers see object and item committed");
            Check(placement.Place("fence", -5, -7, 0, inventory, out _) == PlacementResult.Busy && placement.Recover(1, inventory) == PlacementResult.Busy, "Placement reentry blocked"); };
        Check(placement.Place("fence", -6, -7, 3, inventory, out var record) == PlacementResult.Success && record.QuarterTurns == 3 && events == 1, "Place one rotated fence");
        Check(placement.Place("path", -6, -7, 0, inventory, out _) == PlacementResult.Occupied, "Shared-cell occupancy");
        var snapshot = placement.Capture(); snapshot[0].GridX = -10; Check(placement.Find(record.Id).GridX == -6, "Deep placed snapshot");
        foreach (var invalid in new[] { new SavedPlacedObject { Id = 0, ItemId = "fence", GridX = -6, GridZ = -7 }, new SavedPlacedObject { Id = 1, ItemId = "unknown", GridX = -6, GridZ = -7 },
            new SavedPlacedObject { Id = 1, ItemId = "fence", GridX = 0, GridZ = 0 }, new SavedPlacedObject { Id = 1, ItemId = "fence", GridX = 11, GridZ = -4 },
            new SavedPlacedObject { Id = 1, ItemId = "fence", GridX = int.MinValue, GridZ = -7 }, new SavedPlacedObject { Id = 1, ItemId = "fence", GridX = -6, GridZ = -7, QuarterTurns = 4 } })
            Reject(() => placement.Restore(new[] { invalid }));
        Reject(() => placement.Restore(null)); Reject(() => placement.Restore(new SavedPlacedObject[] { null })); Reject(() => placement.Restore(new[] { record, record }));
        Check(placement.Count == 1 && placement.Find(record.Id).GridX == -6, "Invalid restore leaves world unchanged");
        var full = new InventoryState(1, Items); full.TryAdd("wood", 5);
        Check(placement.Recover(record.Id, full) == PlacementResult.BagFull && placement.Count == 1 && full.GetCount("wood") == 5, "Full-bag recovery is atomic");
        var recovering = new InventoryState(1, Items); recovering.Changed += () => Check(placement.Count == 0 && recovering.GetCount("fence") == 1, "Recovery commits both states before notification");
        Check(placement.Recover(record.Id, recovering) == PlacementResult.Success && placement.Recover(record.Id, recovering) == PlacementResult.InvalidItem, "Recover exactly once");
        var many = new PlacementState(Placeables, 1); var supplies = new InventoryState(30, Items); supplies.TryAdd("fence", 140);
        for (int x = -11; x <= 11 && many.Count < 128; x++) for (int z = -11; z <= 11 && many.Count < 128; z++)
            if (many.CheckLocation("fence", x, z, 0) == PlacementResult.Success) Check(many.Place("fence", x, z, 0, supplies, out _) == PlacementResult.Success, "Place up to object limit");
        Check(many.CheckPlace("fence", 10, 10, 0, supplies) == PlacementResult.LimitReached && many.Capture().Select(p => p.Id).Distinct().Count() == 128, "Bounded unique IDs");
        var copy = new PlacementState(Placeables, 1); copy.Restore(many.Capture()); Check(copy.Count == 128, "Maximum snapshot restores");
        var bag2 = new InventoryState(2, Items); bag2.TryAdd("lamp", 2); var world = new PlacementState(Placeables, 5, new[] { route }); world.Place("lamp", -6, -7, 2, bag2, out _);
        var schedules = new NPCScheduleState(new[] { route }, 900); schedules.Restore(new[] { new SavedNPCSchedule { Id = "npc", Distance = 2 } });
        var save = SaveData.Capture(new FarmState(5, 3), bag2, 0, 0, 0, 123, 900, 85, schedules: schedules, placements: world);
        save.Validate(5, 3, bag2.CreateEmpty(2), schedules: new[] { route }, placeables: Placeables);
        Check(save.Version == SaveData.CurrentVersion && save.PlacedObjects[0].QuarterTurns == 2 && save.Slots[0].Quantity == 1, "Schema 16 preserves object, rotation and remaining item");
        save.Version = 15; save.PlacedObjects = null; save.Upgrade(999, schedules: new[] { route }, placeables: Placeables);
        save.Validate(5, 3, bag2.CreateEmpty(2), schedules: new[] { route }, placeables: Placeables);
        Check(save.PlacedObjects.Length == 0 && save.NPCSchedules[0].Distance == 2 && save.Gold == 123 && save.Stamina == 85 && save.Slots[0].Quantity == 1, "v15 migration adds no objects and preserves all existing progress");
        Console.WriteLine("PASS: " + checks + " construction/save assertions");
    }
}
