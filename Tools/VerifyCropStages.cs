using System;
using MagicIsland.Farming;

internal static class VerifyCropStages
{
    static int checks;
    static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    static void Reject(Action action)
    { bool rejected = false; try { action(); } catch (ArgumentException) { rejected = true; } Check(rejected, "Invalid stage inputs rejected"); }
    public static int Main()
    {
        foreach (int duration in new[] { 1, 3, 5, 7 })
        {
            var farm = new FarmState(1, duration); farm.TryAct(0, FarmTool.Hoe, out _); farm.TryAct(0, FarmTool.Seeds, out _);
            Check(CropStages.FromGrowth(farm.Cells[0].Growth, duration) == CropGrowthStage.Seedling, "Planting starts as seedling");
            farm.AdvanceDay(); Check(CropStages.FromGrowth(farm.Cells[0].Growth, duration) == CropGrowthStage.Seedling, "Dry day does not change stage");
            for (int growth = 1; growth <= duration; growth++)
            {
                farm.TryAct(0, FarmTool.WateringCan, out _); farm.AdvanceDay();
                Check(CropStages.FromGrowth(farm.Cells[0].Growth, duration) == (growth == duration ? CropGrowthStage.Mature : CropGrowthStage.Growing), "Correct stage after watered morning");
                var restored = new FarmState(1, duration); restored.Restore(farm.Day, farm.Cells);
                Check(CropStages.FromGrowth(restored.Cells[0].Growth, duration) == CropStages.FromGrowth(growth, duration), "Restored progress derives same stage");
            }
            farm.WaterPlanted(); farm.AdvanceDay(); Check(CropStages.FromGrowth(farm.Cells[0].Growth, duration) == CropGrowthStage.Mature, "Mature stage remains stable");
            farm.TryAct(0, FarmTool.Hoe, out _); farm.TryAct(0, FarmTool.Seeds, out _);
            Check(CropStages.FromGrowth(farm.Cells[0].Growth, duration) == CropGrowthStage.Seedling, "Replant resets stage");
        }
        Reject(() => CropStages.FromGrowth(-1, 3)); Reject(() => CropStages.FromGrowth(4, 3)); Reject(() => CropStages.FromGrowth(0, 0));
        Console.WriteLine("PASS: " + checks + " crop stage and restore assertions"); return 0;
    }
}
