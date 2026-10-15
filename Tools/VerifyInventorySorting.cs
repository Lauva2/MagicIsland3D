using System;
using System.Linq;
using MagicIsland.Inventory;

internal static class VerifyInventorySorting
{
    static int assertions;
    static void Check(bool value, string reason) { assertions++; if (!value) throw new Exception(reason); }
    static void Verify(InventoryState bag, ItemDefinition[] definitions)
    {
        var totals = definitions.Select(d => bag.GetCount(d.Id)).ToArray();
        var wrapper = bag.Slots;
        int capacity = bag.Capacity, notifications = 0;
        uint version = bag.ReorderVersion;
        Action observer = () =>
        {
            notifications++;
            Check(!bag.CanSort && !bag.TrySort(), "Nested sort rejected until notification completes");
            Check(bag.ReorderVersion == version + 1, "Selection revision visible before observers");
            for (int i = 0; i < definitions.Length; i++) Check(totals[i] == bag.GetCount(definitions[i].Id), "All counts committed before observer");
        };
        bag.Changed += observer;
        Check(bag.TrySort(), "Sort accepted");
        bag.Changed -= observer;
        Check(bag.CanSort && notifications == 1 && ReferenceEquals(wrapper, bag.Slots) && bag.Capacity == capacity, "One event, same slots wrapper and capacity");
        var occupied = bag.Slots.Where(s => !s.Empty).ToArray();
        Check(bag.Slots.Take(occupied.Length).All(s => !s.Empty) && bag.Slots.Skip(occupied.Length).All(s => s.Empty && s.ItemId == null), "Empty slots trailing and canonical");
        for (int i = 0; i < occupied.Length; i++)
        {
            var current = bag.FindDefinition(occupied[i].ItemId);
            Check(occupied[i].Quantity > 0 && occupied[i].Quantity <= current.MaxStack, "Valid stack limit");
            if (i == 0) continue;
            var previous = bag.FindDefinition(occupied[i - 1].ItemId);
            Check(previous.Type < current.Type || (previous.Type == current.Type && StringComparer.Ordinal.Compare(previous.Id, current.Id) <= 0), "Deterministic type and ordinal ID order");
            if (previous.Id == current.Id) Check(occupied[i - 1].Quantity == previous.MaxStack, "Only last stack may be partial");
        }
        var sorted = bag.Slots.ToArray();
        Check(bag.TrySort() && sorted.SequenceEqual(bag.Slots), "Idempotent repeated sort");
        for (int i = 0; i < definitions.Length; i++) Check(totals[i] == bag.GetCount(definitions[i].Id), "Quantity conserved");
    }
    public static int Main()
    {
        var definitions = Enum.GetValues(typeof(ItemType)).Cast<ItemType>().Select(t => new ItemDefinition("item_" + (int)t, "Item", t, t == ItemType.Tool ? 1 : 99)).Concat(new[] {
            new ItemDefinition("egg", "Egg", ItemType.AnimalProduct, 99),
            new ItemDefinition("quality_egg", "Quality egg", ItemType.AnimalProduct, 99),
            new ItemDefinition("A", "A", ItemType.Material, int.MaxValue),
            new ItemDefinition("a", "a", ItemType.Material, 2) }).ToArray();
        var random = new Random(81173);
        foreach (int capacity in new[] { 10, 12, 20, 22, 30, 32, 56 })
        {
            var bag = new InventoryState(capacity, definitions);
            Verify(bag, definitions);
            for (int run = 0; run < 120; run++)
            {
                var fixture = new InventorySlot[capacity];
                for (int i = 0; i < capacity; i++)
                {
                    if (run > 0 && random.Next(4) == 0) continue;
                    var item = definitions[random.Next(definitions.Length)];
                    fixture[i] = new InventorySlot(item.Id, item.MaxStack == int.MaxValue ? int.MaxValue - random.Next(99) : random.Next(1, item.MaxStack + 1));
                }
                bag.Restore(fixture); Verify(bag, definitions);
            }
        }
        var source = new InventoryState(10, definitions); var target = source.CreateEmpty(10);
        source.TryAdd("quality_egg", 99); source.TryAdd("egg", 5);
        source.Changed += () => Check(!source.TrySort() && !target.TrySort(), "Sort blocked during two-inventory transfer");
        target.Changed += () => Check(!source.TrySort() && !target.TrySort(), "Both inventories remain busy for both notifications");
        Check(source.TryTransferSlotTo(0, 3, target), "Transfer succeeds");
        Check(source.GetCount("quality_egg") == 96 && source.GetCount("egg") == 5 && target.GetCount("quality_egg") == 3, "Egg quality stays distinct");
        Console.WriteLine("PASS: " + assertions + " inventory sorting assertions"); return 0;
    }
}
