using System;
using System.Linq;
using MagicIsland.Interaction;
using MagicIsland.Inventory;
using MagicIsland.Economy;
using MagicIsland.Farming;
using MagicIsland.Saving;
class VerifyHouseUpgrade
{
    static int checks;
    static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
    static void Reject(Action action) { try { action(); } catch (ArgumentException) { checks++; return; } catch (InvalidOperationException) { checks++; return; } throw new Exception("Invalid house upgrade accepted"); }
    static ItemDefinition[] Items = { new ItemDefinition("wood", "Wood", ItemType.Material, 99), new ItemDefinition("stone", "Stone", ItemType.Material, 99), new ItemDefinition("table", "Table", ItemType.Furniture, 99) };
    static PlaceableDefinition[] Furniture = { new PlaceableDefinition("table") };
    static InventoryState Bag(int wood = 20, int stone = 10) { var b = new InventoryState(3, Items); if (wood > 0) b.TryAdd("wood", wood); if (stone > 0) b.TryAdd("stone", stone); return b; }
    static HouseUpgradeState State(int days = 1) => new HouseUpgradeState(new HouseUpgradeDefinition(20, 10, 300, days));
    static void Main()
    {
        Reject(() => new HouseUpgradeDefinition(0, 1, 1)); Reject(() => new HouseUpgradeDefinition(1, 0, 1)); Reject(() => new HouseUpgradeDefinition(1, 1, 0)); Reject(() => new HouseUpgradeDefinition(1, 1, 1, 31));
        for (int wood = 19; wood <= 21; wood++) for (int stone = 9; stone <= 11; stone++) for (int gold = 299; gold <= 301; gold++)
        {
            var s = State(); var bag = Bag(wood, stone); var e = new EconomyState(gold);
            var result = s.Request(5, bag, e);
            bool success = wood >= 20 && stone >= 10 && gold >= 300;
            Check((result == HouseUpgradeResult.Success) == success, "Costs checked together");
            Check(bag.GetCount("wood") == wood - (success ? 20 : 0) && bag.GetCount("stone") == stone - (success ? 10 : 0) && e.Gold == gold - (success ? 300 : 0), "Atomic costs/no losses on refusal");
            Check(s.Level == 0 && s.CompletionDay == (success ? 6 : 0), "Pending state does not expand same day");
        }
        var state = State(); var inventory = Bag(); var economy = new EconomyState(400); int notifications = 0;
        Action observer = () => { notifications++; Check(inventory.GetCount("wood") == 0 && inventory.GetCount("stone") == 0 && economy.Gold == 100 && state.CompletionDay == 2, "Observers see committed costs and construction"); Check(state.Request(1, inventory, economy) == HouseUpgradeResult.Busy && !state.Complete(2), "Observer cannot reenter request/completion"); };
        inventory.Changed += observer; economy.Changed += observer; Check(state.Request(1, inventory, economy) == HouseUpgradeResult.Success && notifications == 2, "Both notifications fire once"); inventory.Changed -= observer; economy.Changed -= observer;
        Check(state.Request(1, inventory, economy) == HouseUpgradeResult.Building && !state.Complete(1), "Repeated request and same-day completion blocked");
        Check(state.Complete(2) && state.Level == 1 && !state.Building && !state.Complete(3), "Next day completes once");
        Check(state.Request(2, Bag(), new EconomyState(1000)) == HouseUpgradeResult.Completed, "Only one upgrade");
        for (int days = 1; days <= 30; days++)
        {
            var s = State(days); var bag = Bag(); var gold = new EconomyState(300); s.Request(10, bag, gold);
            var restored = State(days); restored.Restore(s.Capture(), 10);
            Check(!restored.Complete(10 + days - 1) && restored.Complete(10 + days), "Persisted completion date");
            Check(State(days).Check(int.MaxValue - days, Bag(), new EconomyState(300)) == HouseUpgradeResult.InvalidDay, "Date overflow protected");
        }
        Reject(() => State().Restore(null, 1)); Reject(() => State().Restore(new SavedHouseUpgrade { Level = 2 }, 1));
        Reject(() => State().Restore(new SavedHouseUpgrade { Level = 1, CompletionDay = 2 }, 1)); Reject(() => State().Restore(new SavedHouseUpgrade { CompletionDay = 1 }, 1));
        Reject(() => State().Restore(new SavedHouseUpgrade { CompletionDay = 32 }, 1)); Reject(() => State().Restore(new SavedHouseUpgrade { CompletionDay = -1 }, 1));
        var room = new PlacementState(Furniture, 5, indoor: true); var bag2 = Bag(); bag2.TryAdd("table", 2);
        Check(room.Place("table", 2, 2, 1, bag2, out var record) == PlacementResult.Success, "Original furniture placed");
        Check(room.CheckLocation("table", 7, 5, 0) == PlacementResult.InvalidPosition, "Extension locked before upgrade"); room.ExpandHome(1);
        Check(room.Find(record.Id).GridX == 2 && room.Find(record.Id).QuarterTurns == 1 && room.Place("table", 7, 5, 2, bag2, out _) == PlacementResult.Success, "Expansion preserves furniture and permits new area");
        Reject(() => room.ExpandHome(0));
        for (int level = 0; level <= 1; level++) for (int x = -7; x <= 11; x++) for (int z = -6; z <= 8; z++)
        {
            bool inside = x >= -5.8 && x <= 5.8 + level * 4 && z >= -4.8 && z <= 4.8 + level * 2;
            Check(HousingState.ValidPosition(HousingState.Home, x, .1, z, level) == inside, "Player bounds follow expansion without shifting origin");
        }
        var farm = new FarmState(5, 3); var save = SaveData.Capture(farm, bag2, 9.6f, .1f, 6.6f, 100, playerArea: HousingState.Home, furniture: room, houseUpgrade: state);
        save.Validate(5, 3, bag2.CreateEmpty(3), furniture: Furniture); Check(save.HouseUpgrade.Level == 1 && save.Furniture.Length == 2 && save.PlayerX == 9.6f, "Expanded player/furniture snapshot validates");
        save.HouseUpgrade = new SavedHouseUpgrade(); Reject(() => save.Validate(5, 3, bag2.CreateEmpty(3), furniture: Furniture));
        save = SaveData.Capture(farm, inventory, 1, .1f, -1, 100, playerArea: HousingState.Home); save.Version = 18; save.HouseUpgrade = null;
        save.Upgrade(999); save.Validate(5, 3, inventory.CreateEmpty(3), furniture: Furniture);
        Check(save.Version == SaveData.CurrentVersion && save.HouseUpgrade.Level == 0 && save.HouseUpgrade.CompletionDay == 0 && save.PlayerArea == HousingState.Home && save.PlayerX == 1 && save.Gold == 100, "v18 migration retains area and progress without free upgrade");
        Console.WriteLine("PASS: " + checks + " house upgrade/atomic costs/expansion/save assertions");
    }
}
