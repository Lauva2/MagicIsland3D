using System;
using MagicIsland.Farming;
using MagicIsland.Inventory;

internal static class VerifyFarmState
{
    private static int assertions;
    private static void Check(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new Exception(message);
    }

    public static int Main()
    {
        var farm = new FarmState(5);
        var inventory = new InventoryState(12, new[] { new ItemDefinition("seed", "Seed", ItemType.Seed, 99), new ItemDefinition("crop", "Crop", ItemType.Crop, 99) });
        inventory.TryAdd("seed", 50);
        var actions = new FarmActions(farm, inventory, "seed", "crop");
        Check(farm.Cells.Length == 25 && farm.Day == 1 && inventory.GetCount("seed") == 50, "Initial state");
        for (int i = 0; i < 25; i++)
        {
            Check(!actions.TryAct(i, FarmTool.Seeds, out _), "Cannot seed until tilled");
            Check(!actions.TryAct(i, FarmTool.WateringCan, out _), "Cannot water empty plot");
            Check(actions.TryAct(i, FarmTool.Hoe, out _), "Till entire field");
            Check(!actions.TryAct(i, FarmTool.Hoe, out _), "Repeated till is rejected");
            Check(actions.TryAct(i, FarmTool.Seeds, out _), "Plant entire field");
            Check(!actions.TryAct(i, FarmTool.Seeds, out _), "Repeated seeding is rejected");
        }
        Check(inventory.GetCount("seed") == 25, "Only successful planting consumes seeds");
        farm.AdvanceDay();
        foreach (var cell in farm.Cells) Check(cell.Growth == 0 && !cell.Mature, "Dry crops do not grow");
        for (int day = 0; day < 3; day++)
        {
            for (int i = 0; i < 25; i++)
            {
                Check(actions.TryAct(i, FarmTool.WateringCan, out _), "Water each day");
                Check(!actions.TryAct(i, FarmTool.WateringCan, out _), "Repeated watering is rejected");
            }
            farm.AdvanceDay();
            foreach (var cell in farm.Cells)
                Check(!cell.Watered && cell.Growth == day + 1, "Daily growth and water reset");
        }
        farm.AdvanceDay();
        foreach (var cell in farm.Cells) Check(cell.Mature && cell.Growth == 3, "Mature crops remain harvestable");
        for (int i = 0; i < 25; i++)
        {
            Check(actions.TryAct(i, (FarmTool)(i % 3), out _), "Harvest with any tool");
            Check(farm.Cells[i].Tilled && !farm.Cells[i].Planted && farm.Cells[i].Growth == 0, "Harvest resets crop, preserves soil");
            Check(actions.TryAct(i, FarmTool.Seeds, out _), "Replant after harvest");
        }
        Check(inventory.GetCount("crop") == 25 && inventory.GetCount("seed") == 0, "Full harvest and second planting");
        for (int day = 0; day < 3; day++)
        {
            for (int i = 0; i < 25; i++) actions.TryAct(i, FarmTool.WateringCan, out _);
            farm.AdvanceDay();
        }
        Check(actions.TryAct(0, FarmTool.Hoe, out _), "Harvest second crop");
        Check(!actions.TryAct(0, FarmTool.Seeds, out _), "No planting when seeds exhausted");
        Check(inventory.GetCount("seed") == 0 && inventory.GetCount("crop") == 26, "No negative seed balance");
        var configured = new FarmState(2, 1);
        var configuredInventory = new InventoryState(1, new[] { new ItemDefinition("seed", "Seed", ItemType.Seed, 99), new ItemDefinition("crop", "Crop", ItemType.Crop, 99) });
        configuredInventory.TryAdd("seed", 1);
        var configuredActions = new FarmActions(configured, configuredInventory, "seed", "crop");
        configuredActions.TryAct(0, FarmTool.Hoe, out _);
        configuredActions.TryAct(0, FarmTool.Seeds, out _);
        Check(configuredInventory.GetCount("seed") == 0 && configured.Cells.Length == 4, "Configured farm size and seed count");
        configured.AdvanceDay();
        Check(!configured.Cells[0].Mature, "Configured crop still needs water");
        configuredActions.TryAct(0, FarmTool.WateringCan, out _);
        configured.AdvanceDay();
        Check(configured.Cells[0].Mature, "Configured one-day growth");
        Console.WriteLine("PASS: " + assertions + " farm rule assertions (25 plots, two planting cycles).");
        return 0;
    }
}

