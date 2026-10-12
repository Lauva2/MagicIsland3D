using System;
using System.Linq;
using MagicIsland.Inventory;
using MagicIsland.Farming;
using MagicIsland.Saving;
using MagicIsland.Tools;
class VerifyStorage
{
    static int count;
    static readonly ItemDefinition[] items = { new ItemDefinition("wood", "Wood", ItemType.Material, 5), new ItemDefinition("stone", "Stone", ItemType.Material, 5) };
    static void Check(bool ok, string message) { count++; if (!ok) throw new Exception(message); }
    static void Reject(Action action) { bool rejected = false; try { action(); } catch (ArgumentException) { rejected = true; } catch (InvalidOperationException) { rejected = true; } Check(rejected, "Invalid restore rejected"); }
    static string Snapshot(InventoryState state) => string.Join("|", state.Slots.Select(s => s.ItemId + ":" + s.Quantity));
    static void Main()
    {
        foreach (bool deposit in new[] { true, false }) for (int existing = 0; existing <= 10; existing++) for (int quantity = 0; quantity <= 6; quantity++)
        {
            var bag = new InventoryState(2, items); var storage = new StorageState(2, items, bag);
            var source = deposit ? bag : storage.Contents; var target = deposit ? storage.Contents : bag;
            source.TryAdd("wood", 5); if (existing > 0) target.TryAdd("wood", existing);
            string beforeSource = Snapshot(source), beforeTarget = Snapshot(target); long total = source.GetCount("wood") + target.GetCount("wood");
            bool success = quantity >= 1 && quantity <= 5 && existing + quantity <= 10;
            Check((storage.Transfer(deposit, 0, quantity) == StorageResult.Success) == success, "Transfer quantity/capacity boundary");
            Check(source.GetCount("wood") + target.GetCount("wood") == total, "Total conserved");
            Check(success ? source.Slots[0].Quantity == 5 - quantity && target.GetCount("wood") == existing + quantity : Snapshot(source) == beforeSource && Snapshot(target) == beforeTarget, "Atomic transfer/failure");
        }
        var inventory = new InventoryState(3, items); inventory.TryAdd("wood", 7); var state = new StorageState(3, items, inventory); state.Contents.TryAdd("wood", 4);
        int bagEvents = 0, boxEvents = 0, stateEvents = 0;
        inventory.Changed += () => { bagEvents++; Check(inventory.Slots[0].Quantity == 5 && inventory.Slots[1].Empty && state.Contents.Slots[0].Quantity == 5 && state.Contents.Slots[1].Quantity == 1, "Selected slot only; destination stacks first");
            Check(state.Transfer(false, 0, 1) == StorageResult.Busy, "Cross direction reentry blocked"); Check(!inventory.TryTransferSlotTo(0, 1, state.Contents), "Low level transfer reentry blocked"); };
        state.Contents.Changed += () => boxEvents++;
        state.Changed += () => { stateEvents++; Check(state.Transfer(true, 0, 1) == StorageResult.Busy, "State observer reentry blocked"); };
        Check(state.Transfer(true, 1, 2) == StorageResult.Success && bagEvents == 1 && boxEvents == 1 && stateEvents == 1, "One complete transaction notification");
        Check(state.Transfer(true, 1, 1) == StorageResult.EmptySlot && state.Transfer(true, -1, 1) == StorageResult.InvalidSlot && state.Transfer(false, 99, 1) == StorageResult.InvalidSlot, "Invalid/empty selections");
        Check(!inventory.TryTransferSlotTo(0, 1, inventory) && !inventory.TryTransferSlotTo(0, 1, null), "No same inventory/null transfer");
        var incompatible = new InventoryState(1, new[] { items[1] }); Check(!inventory.TryTransferSlotTo(0, 1, incompatible), "Unknown target item free");
        var snapshot = state.Capture(); snapshot[0] = default; Check(state.Contents.Slots[0].Quantity == 5, "Capture independent");
        string stable = Snapshot(state.Contents);
        foreach (var invalid in new[] { new InventorySlot("wood", 6), new InventorySlot("missing", 1), new InventorySlot("stone", -1), new InventorySlot("stone", 0) })
        {
            var bad = state.Capture(); bad[2] = invalid; Reject(() => state.Restore(bad)); Check(Snapshot(state.Contents) == stable, "Restore rejects all before mutation");
        }
        Reject(() => state.Restore(null)); Reject(() => state.Restore(new InventorySlot[2])); Reject(() => new StorageState(37, items, inventory));
        var farm = new FarmState(2, 3); var save = SaveData.Capture(farm, inventory, 0, 0, 0, 123, storage: state);
        save.Validate(2, 3, inventory.CreateEmpty(3), storageSlots: 3); Check(save.Version == SaveData.CurrentVersion && save.Storage[0].Quantity == 5 && save.Slots[0].Quantity == 5, "One snapshot for both inventories");
        save.Storage[2].Quantity = 99; Reject(() => save.Validate(2, 3, inventory.CreateEmpty(3), storageSlots: 3));
        save = SaveData.Capture(farm, inventory, 0, 0, 0, 123, storage: state); save.Storage[0] = null; Reject(() => save.GetStorageSlots());
        var costs = new[] { new ToolUpgradeDefinition(FarmTool.Axe, 6, 2, 50), new ToolUpgradeDefinition(FarmTool.Pickaxe, 3, 4, 75) };
        var upgrades = new ToolUpgradeState(costs); upgrades.Restore(new[] { new SavedToolUpgrade { Tool = FarmTool.Axe, Level = 1 }, new SavedToolUpgrade { Tool = FarmTool.Pickaxe } });
        for (int version = 1; version <= 10; version++)
        {
            save = SaveData.Capture(farm, inventory, 0, 0, 0, 123, toolUpgrades: upgrades); save.Version = version; save.Storage = null;
            save.Upgrade(123, toolUpgrades: costs, storageSlots: 18); save.Validate(2, 3, inventory.CreateEmpty(3), toolUpgrades: costs, storageSlots: 18);
            Check(save.Version == SaveData.CurrentVersion && save.Storage.Length == 18 && save.Storage.All(s => s.Quantity == 0 && s.ItemId == null) && save.Gold == 123 && save.Slots[0].Quantity == 5, "Legacy migration empty chest preserves bag/gold");
            Check(save.ToolUpgrades[0].Level == (version == 10 ? 1 : 0), "v10 preserves upgraded tool");
        }
        Console.WriteLine("PASS: " + count + " storage transfer/save assertions");
    }
}
