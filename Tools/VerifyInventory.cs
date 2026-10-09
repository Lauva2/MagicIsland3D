using System;
using MagicIsland.Farming;
using MagicIsland.Inventory;

internal static class VerifyInventory
{
    private static int assertions;
    private static void Check(bool condition, string message)
    { assertions++; if (!condition) throw new Exception(message); }

    public static int Main()
    {
        var items = new[] { new ItemDefinition("seed", "Seed", ItemType.Seed, 7), new ItemDefinition("crop", "Crop", ItemType.Crop, 7) };
        var bag = new InventoryState(3, items);
        int events = 0;
        bag.Changed += () => events++;
        Check(bag.TryAdd("seed", 10), "Split addition across stacks");
        Check(bag.Slots[0].Quantity == 7 && bag.Slots[1].Quantity == 3, "Correct stack sizes");
        Check(bag.TryAdd("crop", 7), "Fill last slot");
        Check(!bag.TryAdd("seed", 5) && bag.GetCount("seed") == 10 && events == 2, "Failed add must not partially fill or notify");
        Check(bag.TryAdd("seed", 4) && bag.GetCount("seed") == 14, "Existing stack can grow even with no empty slots");
        Check(!bag.TryRemove("seed", 15) && bag.GetCount("seed") == 14, "Insufficient removal is atomic");
        Check(bag.TryRemove("seed", 10) && bag.GetCount("seed") == 4 && bag.Slots[0].Empty, "Removal spans stacks and frees slots");
        Check(bag.TryAdd("crop", 3) && bag.Slots[2].Quantity == 7 && bag.Slots[0].Quantity == 3, "Reuse free slot");
        int before = events;
        Check(!bag.TryAdd("unknown", 1) && !bag.TryRemove("unknown", 1), "Unknown IDs rejected");
        Check(!bag.TryAdd("seed", 0) && !bag.TryAdd("seed", -1) && !bag.TryRemove("seed", -1), "Invalid quantities rejected");
        Check(events == before, "Rejected changes do not send notifications");

        var randomBag = new InventoryState(4, items);
        long[] expected = { 0, 0 };
        var random = new Random(318);
        for (int i = 0; i < 300; i++)
        {
            int type = random.Next(2), quantity = random.Next(1, 12);
            string id = type == 0 ? "seed" : "crop";
            bool add = random.Next(2) == 0;
            bool changed = add ? randomBag.TryAdd(id, quantity) : randomBag.TryRemove(id, quantity);
            if (changed) expected[type] += add ? quantity : -quantity;
            Check(randomBag.GetCount("seed") == expected[0] && randomBag.GetCount("crop") == expected[1], "Quantity conservation");
            foreach (InventorySlot slot in randomBag.Slots)
                Check(slot.Quantity >= 0 && slot.Quantity <= 7 && (!slot.Empty || slot.ItemId == null), "Valid slot invariants");
        }

        var farm = new FarmState(1, 1);
        var tightBag = new InventoryState(1, items);
        var actions = new FarmActions(farm, tightBag, "seed", "crop");
        tightBag.TryAdd("seed", 1);
        actions.TryAct(0, FarmTool.Hoe, out _);
        Check(actions.TryAct(0, FarmTool.Seeds, out _) && tightBag.GetCount("seed") == 0, "Plant consumes last seed and frees space");
        tightBag.TryAdd("seed", 7);
        actions.TryAct(0, FarmTool.WateringCan, out _);
        farm.AdvanceDay();
        Check(!actions.TryAct(0, FarmTool.Hoe, out _) && farm.Cells[0].Mature && tightBag.GetCount("crop") == 0,
            "Full bag must preserve mature crop");
        tightBag.TryRemove("seed", 7);
        bool observedComplete = false;
        tightBag.Changed += () => observedComplete = !farm.Cells[0].Planted;
        Check(actions.TryAct(0, FarmTool.Hoe, out _) && tightBag.GetCount("crop") == 1 && observedComplete,
            "Harvest event observes both states committed");
        Check(!actions.TryAct(0, FarmTool.Seeds, out _) && !farm.Cells[0].Planted, "No free seeds in farm state");
        Console.WriteLine("PASS: " + assertions + " inventory/farming assertions.");
        return 0;
    }
}
