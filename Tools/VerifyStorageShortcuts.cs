using System;
using System.Linq;
using MagicIsland.Inventory;
internal static class VerifyStorageShortcuts
{
    static int assertions;
    static void Check(bool value, string why) { assertions++; if (!value) throw new Exception(why); }
    static ItemDefinition[] items = { new ItemDefinition("wood", "Wood", ItemType.Material, 99), new ItemDefinition("stone", "Stone", ItemType.Material, 99),
        new ItemDefinition("egg", "Egg", ItemType.AnimalProduct, 99), new ItemDefinition("quality_egg", "Quality", ItemType.AnimalProduct, 99), new ItemDefinition("large", "Large", ItemType.Food, int.MaxValue) };
    static InventorySlot S(string id, int count) => new InventorySlot(id, count);
    static void Restore(InventoryState state, params InventorySlot[] values) { var slots = new InventorySlot[state.Capacity]; Array.Copy(values, slots, values.Length); state.Restore(slots); }
    public static int Main()
    {
        var bag = new InventoryState(10, items); var storage = new StorageState(2, items, bag); var chest = storage.Contents;
        Restore(bag, S("wood", 50), S("stone", 7), S("quality_egg", 9)); Restore(chest, S("wood", 90), S("stone", 99));
        var bagView = bag.Slots; var chestView = chest.Slots;
        Check(storage.TransferAvailable(true, 0, out int moved, 0) == StorageResult.Success && moved == 9 && bag.Slots[0].Quantity == 41 && chest.Slots[0].Quantity == 99, "Targeted partial merge leaves exact remainder");
        Check(storage.TransferAvailable(true, 2, out moved, 1) == StorageResult.TargetOccupied && moved == 0 && bag.GetCount("quality_egg") == 9, "Unlike target does not swap or consume source");
        Check(storage.TransferAvailable(true, 0, out moved) == StorageResult.DestinationFull && moved == 0, "No available room causes no transfer");
        Check(storage.TransferAvailable(false, 0, out moved, 4) == StorageResult.Success && moved == 99 && chest.Slots[0].Empty && bag.Slots[4].Quantity == 99, "Withdrawal uses exact empty target");
        Check(storage.TransferAvailable(false, 1, out moved, 10) == StorageResult.InvalidSlot && moved == 0, "Locked bag index outside actual capacity rejected");
        Check(storage.TransferAvailable(true, -1, out moved) == StorageResult.InvalidSlot && storage.TransferAvailable(true, 9, out moved) == StorageResult.EmptySlot, "Invalid and empty sources refused");
        Check(ReferenceEquals(bagView, bag.Slots) && ReferenceEquals(chestView, chest.Slots), "Two inventory wrappers preserved");
        Restore(bag, S("wood", 20), S("wood", 50), S("egg", 5), S("quality_egg", 7), S("stone", 3)); Restore(chest, S("wood", 95), S("egg", 97));
        int bagEvents = 0, chestEvents = 0, stateEvents = 0;
        Action observe = () =>
        {
            Check(bag.GetCount("wood") == 66 && chest.GetCount("wood") == 99 && bag.GetCount("egg") == 3 && chest.GetCount("egg") == 99, "Both sides fully committed before batch observers");
            Check(!bag.CanSort && !chest.CanSort && storage.DepositMatching(out _) == StorageResult.Busy && storage.TransferAvailable(true, 0, out _) == StorageResult.Busy, "Observers cannot reenter matching or quick transfer");
        };
        Action onBag = () => { bagEvents++; observe(); }; Action onChest = () => { chestEvents++; observe(); };
        bag.Changed += onBag; chest.Changed += onChest; storage.Changed += () => stateEvents++;
        Check(storage.DepositMatching(out long total) == StorageResult.Success && total == 6 && bagEvents == 1 && chestEvents == 1 && stateEvents == 1, "Batch emits once per state with exact moved total");
        bag.Changed -= onBag; chest.Changed -= onChest;
        Check(bag.GetCount("quality_egg") == 7 && bag.GetCount("stone") == 3 && chest.GetCount("quality_egg") == 0, "Only pre-existing exact IDs qualify; premium egg distinct");
        Check(!storage.CanDepositMatching && storage.DepositMatching(out total) == StorageResult.DestinationFull && total == 0, "Repeated full batch is a no-op");
        Restore(chest); Check(storage.DepositMatching(out total) == StorageResult.NoMatchingItems && total == 0, "Empty chest cannot seed unrelated IDs");
        var bigBag = new InventoryState(56, items); var big = new StorageState(36, items, bigBag);
        Restore(bigBag, S("large", int.MaxValue), S("large", int.MaxValue), S("large", int.MaxValue)); Restore(big.Contents, S("large", 1));
        Check(big.DepositMatching(out total) == StorageResult.Success && total == 6442450941L && bigBag.GetCount("large") == 0 && big.Contents.GetCount("large") == 6442450942L, "Batch totals above int remain exact");
        var random = new Random(9241);
        foreach (int capacity in new[] { 10, 12, 20, 22, 30, 32, 56 })
        {
            bag = new InventoryState(capacity, items); storage = new StorageState(18, items, bag); chest = storage.Contents;
            for (int fixture = 0; fixture < 30; fixture++)
            {
                foreach (var inventory in new[] { bag, chest })
                {
                    var slots = new InventorySlot[inventory.Capacity];
                    for (int i = 0; i < slots.Length; i++) if (random.Next(5) > 0) { var item = items[random.Next(4)]; slots[i] = S(item.Id, random.Next(1, 100)); }
                    inventory.Restore(slots);
                }
                var totals = items.Select(item => bag.GetCount(item.Id) + chest.GetCount(item.Id)).ToArray();
                for (int operation = 0; operation < 60; operation++)
                {
                    var beforeBag = bag.Slots.ToArray(); var beforeChest = chest.Slots.ToArray(); var source = operation % 2 == 0 ? bag : chest; var target = source == bag ? chest : bag;
                    var initialMatches = chest.Slots.Where(s => !s.Empty).Select(s => s.ItemId).ToArray();
                    StorageResult result;
                    if (operation % 7 == 0) result = storage.DepositMatching(out total);
                    else result = storage.TransferAvailable(source == bag, random.Next(-1, source.Capacity + 1), out moved, operation % 3 == 0 ? random.Next(target.Capacity) : -1);
                    Check(result == StorageResult.Success || (beforeBag.SequenceEqual(bag.Slots) && beforeChest.SequenceEqual(chest.Slots)), "Rejected quick/batch leaves exact arrays unchanged");
                    for (int i = 0; i < items.Length; i++) Check(totals[i] == bag.GetCount(items[i].Id) + chest.GetCount(items[i].Id), "Random transfers conserve every exact ID");
                    foreach (var inventory in new[] { bag, chest }) foreach (var slot in inventory.Slots) Check(slot.Empty || slot.Quantity > 0 && slot.Quantity <= inventory.FindDefinition(slot.ItemId).MaxStack, "Random transfers respect stack limits");
                    if (operation % 7 == 0) Check(chest.Slots.Where(s => !s.Empty).All(s => initialMatches.Contains(s.ItemId)), "Batch never introduces unmatched ID");
                }
            }
        }
        Console.WriteLine("PASS: " + assertions + " storage shortcut assertions"); return 0;
    }
}
