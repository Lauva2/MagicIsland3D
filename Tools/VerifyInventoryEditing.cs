using System;
using System.Linq;
using MagicIsland.Inventory;

internal static class VerifyInventoryEditing
{
    static int assertions;
    static void Check(bool ok, string reason) { assertions++; if (!ok) throw new Exception(reason); }
    static ItemDefinition[] Items = {
        new ItemDefinition("wood", "Wood", ItemType.Material, 99), new ItemDefinition("seed", "Seed", ItemType.Seed, 99),
        new ItemDefinition("egg", "Egg", ItemType.AnimalProduct, 99), new ItemDefinition("quality_egg", "Quality", ItemType.AnimalProduct, 99),
        new ItemDefinition("tool", "Tool", ItemType.Tool, 1), new ItemDefinition("large", "Large", ItemType.Food, int.MaxValue) };
    static InventoryState Fixture(int capacity, params InventorySlot[] slots)
    { var bag = new InventoryState(capacity, Items); var array = new InventorySlot[capacity]; Array.Copy(slots, array, slots.Length); bag.Restore(array); return bag; }
    static InventorySlot Slot(string id, int quantity) => new InventorySlot(id, quantity);
    public static int Main()
    {
        var bag = Fixture(10, Slot("wood", 40), Slot("wood", 80), Slot("seed", 5));
        var wrapper = bag.Slots; int events = 0;
        bag.Changed += () => events++;
        Check(bag.MoveSlot(0, 7) == InventoryEditResult.Success && bag.Slots[0].Empty && bag.Slots[7].Quantity == 40, "Whole stack moves to chosen empty slot");
        Check(bag.MoveSlot(7, 1) == InventoryEditResult.Success && bag.Slots[1].Quantity == 99 && bag.Slots[7].Quantity == 21, "Partial merge leaves exact overflow in source");
        Check(bag.MoveSlot(7, 2) == InventoryEditResult.Success && bag.Slots[2].ItemId == "wood" && bag.Slots[2].Quantity == 21 && bag.Slots[7].ItemId == "seed", "Different items swap exact stacks");
        Check(bag.SplitSlot(2) == InventoryEditResult.Success && bag.Slots[0].Quantity == 10 && bag.Slots[2].Quantity == 11, "Odd split uses first empty slot, larger half remains");
        Check(bag.SplitSlot(0) == InventoryEditResult.Success && bag.Slots[0].Quantity == 5 && bag.Slots[3].Quantity == 5, "Even split halves quantities");
        Check(ReferenceEquals(wrapper, bag.Slots) && bag.Capacity == 10 && events == 5 && bag.ReorderVersion == 5 && bag.GetCount("wood") == 120 && bag.GetCount("seed") == 5, "Identity, capacity, one event/revision per successful edit and conservation");
        var full = Fixture(2, Slot("wood", 2), Slot("wood", 99));
        Check(full.SplitSlot(0) == InventoryEditResult.NoEmptySlot, "Full inventory cannot split into existing stacks");
        Check(full.MoveSlot(0, 1) == InventoryEditResult.StackFull && full.Slots[0].Quantity == 2, "Full destination refuses merge");
        var one = Fixture(2, Slot("tool", 1));
        Check(one.SplitSlot(0) == InventoryEditResult.CannotSplit && one.SplitSlot(1) == InventoryEditResult.EmptySource, "Single item and empty source refused");
        Check(one.MoveSlot(0, 0) == InventoryEditResult.SameSlot && one.MoveSlot(-1, 0) == InventoryEditResult.InvalidSlot && one.MoveSlot(0, 2) == InventoryEditResult.InvalidSlot && one.SplitSlot(2) == InventoryEditResult.InvalidSlot, "Invalid and same-slot edits refused");
        var eggs = Fixture(3, Slot("egg", 3), Slot("quality_egg", 7));
        Check(eggs.MoveSlot(0, 1) == InventoryEditResult.Success && eggs.Slots[0].ItemId == "quality_egg" && eggs.Slots[1].ItemId == "egg", "Egg quality swaps rather than merging");
        Check(eggs.SplitSlot(0) == InventoryEditResult.Success && eggs.Slots[2].ItemId == "quality_egg" && eggs.Slots[2].Quantity == 3, "Splitting retains egg quality");
        var large = Fixture(3, Slot("large", int.MaxValue), Slot("large", int.MaxValue - 1));
        Check(large.MoveSlot(0, 1) == InventoryEditResult.Success && large.Slots[0].Quantity == int.MaxValue - 1 && large.Slots[1].Quantity == int.MaxValue, "Merge arithmetic does not overflow");
        Check(large.SplitSlot(1) == InventoryEditResult.Success && large.Slots[1].Quantity == 1073741824 && large.Slots[2].Quantity == 1073741823 && large.GetCount("large") == 4294967293L, "Large split and total stay exact");
        var random = new Random(77491);
        foreach (int capacity in new[] { 10, 12, 20, 22, 30, 32, 56 })
        {
            int notified = 0; uint observedVersion = 0;
            for (int run = 0; run < 120; run++)
            {
                var fixture = new InventorySlot[capacity];
                for (int i = 0; i < capacity; i++)
                {
                    if (run % 3 != 0 && random.Next(4) == 0) continue;
                    var item = Items[random.Next(Items.Length)];
                    fixture[i] = Slot(item.Id, item.MaxStack == int.MaxValue ? int.MaxValue - random.Next(90) : random.Next(1, item.MaxStack + 1));
                }
                // Restore emits a separate initialization event, so use a new fixture object.
                bag = Fixture(capacity, fixture); wrapper = bag.Slots; notified = 0;
                var totals = Items.Select(item => bag.GetCount(item.Id)).ToArray();
                bag.Changed += () =>
                {
                    notified++; Check(bag.ReorderVersion == observedVersion + 1 && !bag.CanSort && bag.MoveSlot(0, 1) == InventoryEditResult.Busy && bag.SplitSlot(0) == InventoryEditResult.Busy && !bag.TrySort(), "Complete revision and guarded observer reentry");
                    for (int i = 0; i < Items.Length; i++) Check(totals[i] == bag.GetCount(Items[i].Id), "Observers see both slots committed without loss");
                };
                for (int operation = 0; operation < 50; operation++)
                {
                    var before = bag.Slots.ToArray(); int notifications = notified; observedVersion = bag.ReorderVersion;
                    int from = random.Next(-1, capacity + 1), to = random.Next(-1, capacity + 1);
                    var result = operation % 3 == 0 ? bag.SplitSlot(from) : bag.MoveSlot(from, to);
                    bool success = result == InventoryEditResult.Success;
                    Check(notified == notifications + (success ? 1 : 0) && bag.ReorderVersion == observedVersion + (success ? 1U : 0U), "Only success notifies and revises selection");
                    Check(success || before.SequenceEqual(bag.Slots), "Rejected operation leaves exact original order/counts");
                    Check(bag.Capacity == capacity && ReferenceEquals(wrapper, bag.Slots) && bag.CanSort, "Edit preserves capacity/identity and releases lock");
                    for (int i = 0; i < Items.Length; i++) Check(totals[i] == bag.GetCount(Items[i].Id), "Random move/split sequence conserves every item ID");
                    foreach (var value in bag.Slots) Check(value.Empty || value.Quantity <= bag.FindDefinition(value.ItemId).MaxStack, "Random edits respect stack limits");
                }
            }
        }
        var source = Fixture(10, Slot("wood", 3)); var target = source.CreateEmpty(10);
        Action observer = () => Check(source.MoveSlot(0, 1) == InventoryEditResult.Busy && source.SplitSlot(0) == InventoryEditResult.Busy && target.MoveSlot(0, 1) == InventoryEditResult.Busy && target.SplitSlot(0) == InventoryEditResult.Busy, "Both inventories reject edits until transfer notifications finish");
        source.Changed += observer; target.Changed += observer;
        Check(source.TryTransferSlotTo(0, 1, target), "Existing transfer still works");
        Console.WriteLine("PASS: " + assertions + " inventory drag/split assertions"); return 0;
    }
}
