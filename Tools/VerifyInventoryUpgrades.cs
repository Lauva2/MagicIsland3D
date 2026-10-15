using System;
using System.Linq;
using MagicIsland.Inventory;
using MagicIsland.Economy;
using MagicIsland.Reports;
using MagicIsland.Saving;
using MagicIsland.Farming;
class VerifyInventoryUpgrades
{
    static int checks;
    static void Check(bool ok, string why) { checks++; if (!ok) throw new Exception(why); }
    static readonly ItemDefinition[] items = { new ItemDefinition("wood", "Wood", ItemType.Material, 99), new ItemDefinition("egg", "Egg", ItemType.AnimalProduct, 99) };
    static readonly InventoryUpgradeDefinition definition = new InventoryUpgradeDefinition(300, 800);
    static void Reject(Action a) { bool failed = false; try { a(); } catch (ArgumentException) { failed = true; } Check(failed, "Invalid upgrade data rejected"); }
    static void Main()
    {
        for (int basis = 1; basis <= 36; basis++)
        {
            var bag = new InventoryState(basis, items); var gold = new EconomyState(1100); var upgrade = new InventoryUpgradeState(definition, basis); var report = new DailyReportState(); gold.SetLedger(report.RecordMoney);
            for (int i = 0; i < basis; i++) bag.TryAdd(i % 2 == 0 ? "wood" : "egg", 99, false);
            var original = bag.Slots.ToArray(); var reference = bag;
            for (int level = 0; level < 2; level++)
            {
                int cost = upgrade.NextCost, next = upgrade.NextCapacity; bool seen = false;
                Action observer = () => { seen = true; Check(upgrade.Level == level + 1 && bag.Capacity == next && gold.Gold == (level == 0 ? 800 : 0), "Capacity, level and payment commit before bag observers"); Check(upgrade.Upgrade(bag, gold) == InventoryUpgradeResult.Busy, "Upgrade observer reentry blocked"); Reject(() => upgrade.Restore(upgrade.Capture())); };
                bag.Changed += observer; Check(upgrade.Upgrade(bag, gold) == InventoryUpgradeResult.Success && seen && Object.ReferenceEquals(reference, bag), "In-place upgrade succeeds"); bag.Changed -= observer;
                Check(bag.Capacity == basis + (level + 1) * 10 && Enumerable.Range(0, basis).All(i => bag.Slots[i].ItemId == original[i].ItemId && bag.Slots[i].Quantity == original[i].Quantity) && bag.Slots.Skip(basis).All(s => s.Empty), "Add exactly ten slots, preserve every original stack and index");
                var restored = new InventoryUpgradeState(definition, basis); restored.Restore(upgrade.Capture()); Check(restored.Capacity == bag.Capacity && restored.Level == upgrade.Level, "Upgrade snapshot restores exact capacity");
            }
            Check(upgrade.Upgrade(bag, gold) == InventoryUpgradeResult.MaximumLevel && gold.Gold == 0 && report.Capture().Today.Expense == "1100", "Max level no extra payment and exact daily expense");
            var save = SaveData.Capture(new FarmState(2), bag, 0, 0, 0, inventoryUpgrade: upgrade); save.Validate(2, 3, bag.CreateEmpty(bag.Capacity)); Check(save.InventoryUpgrade.BaseSlots == basis && save.InventoryUpgrade.Level == 2, "Expanded save capacity validates independently of new-game default");
        }
        var b = new InventoryState(10, items); var g = new EconomyState(299); var s = new InventoryUpgradeState(definition);
        Check(s.Upgrade(b, g) == InventoryUpgradeResult.InsufficientGold && b.Capacity == 10 && g.Gold == 299 && s.Level == 0, "Insufficient gold changes nothing");
        Check(s.Check(new InventoryState(12, items), g) == InventoryUpgradeResult.InvalidInventory, "Mismatched capacity cannot spend");
        var destination = new InventoryState(1, items); b.TryAdd("wood", 1); g.Restore(1100);
        destination.Changed += () => Check(s.Upgrade(b, g) == InventoryUpgradeResult.Busy && b.Capacity == 10 && g.Gold == 1100, "Transfer observer cannot expand source mid-transfer"); b.TryTransferSlotTo(0, 1, destination);
        foreach (var bad in new[] { new SavedInventoryUpgrade { BaseSlots = 0 }, new SavedInventoryUpgrade { BaseSlots = 37 }, new SavedInventoryUpgrade { Level = -1 }, new SavedInventoryUpgrade { Level = 3 } }) Reject(() => s.Restore(bad));
        Reject(() => new InventoryUpgradeDefinition(300)); Reject(() => new InventoryUpgradeDefinition(300, 0));
        for (int capacity = 6; capacity <= 36; capacity++)
        {
            var bag = new InventoryState(capacity, items); bag.TryAdd("egg", 99); var save = SaveData.Capture(new FarmState(2), bag, 0, 0, 0, 123); save.Version = 24; save.Upgrade(100);
            Check(save.Version == SaveData.CurrentVersion && save.InventoryUpgrade.Level == 0 && save.InventoryUpgrade.BaseSlots == capacity && save.Slots[0].Quantity == 99 && save.Gold == 123, "v24 preserves actual old capacity, items and gold");
            save.Validate(2, 3, bag.CreateEmpty(capacity)); var legacy = new InventoryUpgradeState(definition, capacity); legacy.Restore(save.InventoryUpgrade); Check(legacy.NextCapacity == capacity + 10, "Legacy capacity also grows by ten");
        }
        Console.WriteLine("PASS: " + checks + " backpack +10 expansion, identity, transaction, migration and save assertions");
    }
}
