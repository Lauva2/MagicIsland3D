using System;
using System.Linq;
using MagicIsland.Farming;
using MagicIsland.Inventory;
using MagicIsland.Economy;
using MagicIsland.Tools;
using MagicIsland.Gathering;
using MagicIsland.Stamina;
using MagicIsland.Saving;
class VerifyToolUpgrades
{
    static int count;
    static void Check(bool ok, string why) { count++; if (!ok) throw new Exception(why); }
    static void Reject(Action action) { bool rejected = false; try { action(); } catch (ArgumentException) { rejected = true; } Check(rejected, "Invalid state rejected"); }
    static readonly ToolUpgradeDefinition[] costs = { new ToolUpgradeDefinition(FarmTool.Axe, 6, 2, 50), new ToolUpgradeDefinition(FarmTool.Pickaxe, 3, 4, 75) };
    static readonly ItemDefinition[] items = { new ItemDefinition("wood", "Wood", ItemType.Material, 3), new ItemDefinition("stone", "Stone", ItemType.Material, 3) };
    static InventoryState Bag(int wood, int stone) { var bag = new InventoryState(12, items); if (wood > 0) bag.TryAdd("wood", wood); if (stone > 0) bag.TryAdd("stone", stone); return bag; }
    static void Main()
    {
        // Exhaustive cost boundaries exercise both tools and exact/insufficient budgets.
        foreach (var cost in costs) for (int wood = 0; wood <= 9; wood++) for (int stone = 0; stone <= 6; stone++) foreach (long gold in new long[] { 0, cost.Gold - 1, cost.Gold, 125 })
        {
            var state = new ToolUpgradeState(costs); var bag = Bag(wood, stone); var economy = new EconomyState(gold);
            bool enough = wood >= cost.Wood && stone >= cost.Stone && gold >= cost.Gold;
            Check((state.Upgrade(cost.Tool, bag, economy) == UpgradeResult.Success) == enough, "Purchase boundary");
            Check(bag.GetCount("wood") == wood - (enough ? cost.Wood : 0) && bag.GetCount("stone") == stone - (enough ? cost.Stone : 0)
                && economy.Gold == gold - (enough ? cost.Gold : 0) && state.Level(cost.Tool) == (enough ? 1 : 0), "No partial deduction");
            if (enough) Check(state.Upgrade(cost.Tool, bag, economy) == UpgradeResult.AlreadyUpgraded && economy.Gold == gold - cost.Gold, "No duplicate purchase");
        }
        var upgrades = new ToolUpgradeState(costs); var inventory = Bag(9, 6); var money = new EconomyState(125); int notifications = 0;
        inventory.Changed += () => { notifications++; Check(money.Gold == 75 && upgrades.Level(FarmTool.Axe) == 1 && inventory.GetCount("wood") == 3 && inventory.GetCount("stone") == 4, "Observers see complete transaction");
            Check(upgrades.Upgrade(FarmTool.Pickaxe, inventory, money) == UpgradeResult.Busy, "Reentry blocked across tools"); };
        Check(upgrades.Upgrade(FarmTool.Axe, inventory, money) == UpgradeResult.Success && notifications == 1, "One inventory notification");
        var restored = new ToolUpgradeState(costs); restored.Restore(upgrades.Capture()); Check(restored.Damage(FarmTool.Axe) == 2 && restored.Damage(FarmTool.Pickaxe) == 1, "Snapshot restore");
        var snapshot = upgrades.Capture(); snapshot[0].Level = 0; Check(upgrades.Level(FarmTool.Axe) == 1, "Deep capture");
        foreach (int level in new[] { -1, 2, int.MaxValue }) { var bad = upgrades.Capture(); bad[1].Level = level; Reject(() => restored.Restore(bad)); Check(restored.Level(FarmTool.Axe) == 1, "Restore atomic"); }
        Reject(() => restored.Restore(null)); Reject(() => restored.Restore(new SavedToolUpgrade[0]));
        var swapped = upgrades.Capture().Reverse().ToArray(); Reject(() => restored.Restore(swapped));
        Reject(() => new ToolUpgradeState(costs.Reverse())); Reject(() => new ToolUpgradeDefinition(FarmTool.Hoe, 1, 1, 1));
        Check(restored.Upgrade(FarmTool.Hoe, inventory, money) == UpgradeResult.InvalidTool, "Unsupported tool free");
        var gatheringDefinitions = new[] { new GatheringDefinition("tree", "wood", FarmTool.Axe, 3, 3, 4, 3), new GatheringDefinition("rock", "stone", FarmTool.Pickaxe, 2, 2, 5, 2) };
        var gathering = new GatheringState(gatheringDefinitions, 1); var bag2 = Bag(0, 0); var stamina = new StaminaState(100);
        Check(gathering.Hit(0, FarmTool.Axe, bag2, stamina, 2) == GatherResult.Success && gathering.HitsRemaining(0) == 1 && stamina.Current == 96, "Upgraded partial damage");
        Check(gathering.Hit(0, FarmTool.Axe, bag2, stamina, 2) == GatherResult.Success && gathering.IsDepleted(0) && stamina.Current == 92 && bag2.GetCount("wood") == 3 && gathering.RespawnDay(0) == 4, "Upgraded final yield once");
        Check(gathering.Hit(1, FarmTool.Pickaxe, bag2, stamina, 2) == GatherResult.Success && gathering.IsDepleted(1) && bag2.GetCount("stone") == 2 && stamina.Current == 87, "Pickaxe one hit");
        Reject(() => gathering.Hit(0, FarmTool.Axe, bag2, stamina, 0));
        var save = SaveData.Capture(new FarmState(2, 3), bag2, 0, 0, 0, 75, gathering: gathering, toolUpgrades: upgrades);
        save.Validate(2, 3, Bag(0, 0), gathering: gatheringDefinitions, toolUpgrades: costs);
        Check(save.Version == SaveData.CurrentVersion && save.ToolUpgrades[0].Level == 1, "Schema10 captures upgrade");
        save.Version = 9; save.ToolUpgrades = null; save.Upgrade(999, gathering: gatheringDefinitions, toolUpgrades: costs);
        save.Validate(2, 3, Bag(0, 0), gathering: gatheringDefinitions, toolUpgrades: costs);
        Check(save.Gold == 75 && save.Gathering[0].RespawnDay == 4 && save.Slots.Sum(s => s.Quantity) == 5 && save.ToolUpgrades.All(s => s.Level == 0), "v9 preserves progress and supplies ordinary tools");
        Console.WriteLine("PASS: " + count + " tool upgrade assertions");
    }
}
