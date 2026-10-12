using System;
using System.Linq;
using MagicIsland.Interaction;
using MagicIsland.Inventory;
using MagicIsland.Farming;
using MagicIsland.Saving;
class VerifyFurniture
{
    static int checks;
    static void Check(bool value, string name) { checks++; if (!value) throw new Exception(name); }
    static void Reject(Action action) { try { action(); } catch (ArgumentException) { checks++; return; } catch (InvalidOperationException) { checks++; return; } throw new Exception("Invalid furniture accepted"); }
    static PlaceableDefinition[] Definitions = { new PlaceableDefinition("table"), new PlaceableDefinition("chair"), new PlaceableDefinition("lamp") };
    static ItemDefinition[] Items = { new ItemDefinition("wood", "Wood", ItemType.Material, 99), new ItemDefinition("stone", "Stone", ItemType.Material, 99), new ItemDefinition("table", "Table", ItemType.Furniture, 99), new ItemDefinition("chair", "Chair", ItemType.Furniture, 99), new ItemDefinition("lamp", "Lamp", ItemType.Furniture, 99) };
    static PlacementState Room() => new PlacementState(Definitions, 5, indoor: true);
    static void Main()
    {
        Check((int)ItemType.Furniture == 7 && (int)ItemType.Placeable == 6, "Existing item enum values preserved");
        var empty = Room(); var bag = new InventoryState(6, Items); bag.TryAdd("wood", 20); bag.TryAdd("stone", 5);
        var crafting = new CraftingState(new[] { new CraftingDefinition("desk", "table", 1, new[] { new IngredientCost("wood", 4) }), new CraftingDefinition("seat", "chair", 1, new[] { new IngredientCost("wood", 3) }), new CraftingDefinition("light", "lamp", 1, new[] { new IngredientCost("wood", 2), new IngredientCost("stone", 1) }) });
        for (int i = 0; i < 3; i++) Check(crafting.Craft(i, bag) == CraftingResult.Success, "Three furniture recipes");
        Check(bag.GetCount("wood") == 11 && bag.GetCount("stone") == 4, "Exact material costs");
        for (int x = -7; x <= 7; x++) for (int z = -6; z <= 6; z++) for (int turns = -1; turns <= 4; turns++)
        {
            var result = empty.CheckLocation("table", x, z, turns);
            bool bounds = Math.Abs(x) <= 5 && Math.Abs(z) <= 4 && turns >= 0 && turns <= 3;
            Check(result == (!bounds ? PlacementResult.InvalidPosition : FurnitureLayout.Reserved(x, z) ? PlacementResult.RouteReserved : PlacementResult.Success), "Floor bounds, rotation and protected route");
        }
        Check(empty.CheckLocation("fence", 2, 2, 0) == PlacementResult.InvalidItem, "Outdoor decoration refused indoors");
        Check(empty.CheckLocation("table", int.MinValue, int.MaxValue, 0) == PlacementResult.InvalidPosition, "Extreme positions rejected without overflow");
        Check(empty.Place("table", 2, 2, 1, bag, out var desk) == PlacementResult.Success && bag.GetCount("table") == 0, "Placement consumes one");
        Check(empty.Place("chair", 2, 2, 0, bag, out _) == PlacementResult.Occupied && bag.GetCount("chair") == 1, "Overlap consumes nothing");
        Check(empty.Place("chair", 3, 2, 2, bag, out _) == PlacementResult.Success, "Independent furniture ID");
        var copy = empty.Capture(); copy[0].GridX = -99; Check(empty.Find(desk.Id).GridX == 2, "Capture is a deep copy");
        var restored = Room(); restored.Restore(empty.Capture()); Check(restored.Count == 2 && restored.Find(desk.Id).QuarterTurns == 1, "Exact restore");
        Reject(() => restored.Restore(new[] { new SavedPlacedObject { Id = 1, ItemId = "table", GridX = 0, GridZ = -3 } }));
        Check(restored.Count == 2, "Rejected restore keeps prior state");
        Reject(() => restored.Restore(new[] { desk, desk })); Reject(() => restored.Restore(null));
        var full = new InventoryState(1, Items); full.TryAdd("wood", 99);
        Check(restored.Recover(desk.Id, full) == PlacementResult.BagFull && restored.Count == 2, "Full bag recovery retains furniture");
        Check(restored.Recover(desk.Id, bag) == PlacementResult.Success && bag.GetCount("table") == 1, "Recovery returns exactly one");
        Check(restored.Recover(desk.Id, bag) == PlacementResult.InvalidItem && bag.GetCount("table") == 1, "Repeated recovery no duplication");
        var farm = new FarmState(5, 3);
        var saved = SaveData.Capture(farm, bag, 1, .1f, -1, 125, 700, 60, playerArea: HousingState.Home, furniture: empty);
        saved.Validate(5, 3, bag.CreateEmpty(6), furniture: Definitions);
        Check(saved.Version == SaveData.CurrentVersion && saved.Furniture.Length == 2 && saved.PlayerArea == HousingState.Home && saved.Gold == 125, "Furniture and local position snapshot");
        saved.Furniture[0].ItemId = "wood"; Reject(() => saved.Validate(5, 3, bag.CreateEmpty(6), furniture: Definitions));
        saved.Furniture = null; Reject(() => saved.Validate(5, 3, bag.CreateEmpty(6), furniture: Definitions));
        saved.Version = 17; saved.Upgrade(999);
        saved.Validate(5, 3, bag.CreateEmpty(6), furniture: Definitions);
        Check(saved.Version == SaveData.CurrentVersion && saved.Furniture.Length == 0 && saved.PlayerArea == HousingState.Home && saved.PlayerX == 1 && saved.Gold == 125 && saved.Stamina == 60, "v17 migration preserves area, position and progress; grants no furniture");
        Console.WriteLine("PASS: " + checks + " furniture/crafting/save assertions");
    }
}
