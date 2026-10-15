using System;
using MagicIsland.Farming;
using MagicIsland.Inventory;
using MagicIsland.Stamina;
internal static class VerifyStamina
{
    static int checks;
    static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    static void Reject(Action action) { bool rejected = false; try { action(); } catch (ArgumentOutOfRangeException) { rejected = true; } Check(rejected, "Invalid parameter rejected"); }
    public static int Main()
    {
        Reject(() => new StaminaState(0));
        var state = new StaminaState(10); int notifications = 0; state.Changed += () => notifications++;
        Check(state.Current == 10, "Start full");
        Check(!state.TrySpend(-1) && !state.TrySpend(11) && state.Current == 10 && notifications == 0, "Rejected spending has no changes or events");
        Check(state.TrySpend(0) && notifications == 0, "Free action at no cost");
        Check(state.TrySpend(10) && state.Current == 0 && notifications == 1, "Spend exactly remaining stamina");
        Reject(() => state.Restore(-1)); Reject(() => state.Restore(11));
        Check(state.Current == 0, "Rejected restore is atomic"); state.Refill(); Check(state.Current == 10, "Sleep refill");
        var farm = new FarmState(2, 1);
        var bag = new InventoryState(1, new[] { new ItemDefinition("seed", "Seed", ItemType.Seed, 9), new ItemDefinition("crop", "Crop", ItemType.Crop, 9) });
        bag.TryAdd("seed", 2);
        var actions = new FarmActions(farm, bag, "seed", "crop", state, 3, 1, 2, 0);
        Check(!actions.TryAct(0, FarmTool.WateringCan, out _) && state.Current == 10, "Watering empty plot costs nothing");
        Check(actions.TryAct(0, FarmTool.Hoe, out _) && state.Current == 7, "Hoe cost");
        Check(!actions.TryAct(0, FarmTool.Hoe, out _) && state.Current == 7, "Repeated hoe costs nothing");
        bool coherent = false, nestedRejected = false;
        Action observer = () => { coherent = farm.Cells[0].Planted && bag.GetCount("seed") == 1 && state.Current == 6; nestedRejected = !actions.TryAct(1, FarmTool.Hoe, out _); };
        bag.Changed += observer;
        Check(actions.TryAct(0, FarmTool.Seeds, out _) && coherent && nestedRejected && !farm.Cells[1].Tilled, "Observers see committed stamina and nested action is rejected");
        bag.Changed -= observer;
        Check(!actions.TryAct(0, FarmTool.Seeds, out _) && state.Current == 6 && bag.GetCount("seed") == 1, "Duplicate plant is free");
        state.Restore(1);
        Check(!actions.CanPerform(0, FarmTool.WateringCan) && state.Current == 1 && !farm.Cells[0].Watered, "Low stamina readiness is read-only");
        Check(!actions.TryAct(0, FarmTool.WateringCan, out string low) && !farm.Cells[0].Watered && state.Current == 1 && low.Contains("体力不足"), "Low stamina preserves crop and explains failure");
        Check(actions.Describe(0, FarmTool.WateringCan).Contains("体力不足"), "Hover describes low stamina");
        state.Refill(); Check(actions.TryAct(0, FarmTool.WateringCan, out _) && state.Current == 8, "Water cost");
        Check(!actions.TryAct(0, FarmTool.WateringCan, out _) && state.Current == 8, "Duplicate watering is free");
        farm.AdvanceDay(); state.Restore(0);
        Check(!actions.CanPerform(0, FarmTool.Hoe) && farm.Cells[0].Mature, "Readiness rejects harvest into full bag");
        Check(!actions.TryAct(0, FarmTool.Hoe, out _) && farm.Cells[0].Mature && state.Current == 0, "Full bag preserves crop and stamina");
        bag.TryRemove("seed", 1);
        Check(actions.CanPerform(0, FarmTool.Hoe) && state.Current == 0 && farm.Cells[0].Mature, "Free harvest readiness works at zero stamina without settlement");
        Check(actions.TryAct(0, FarmTool.Hoe, out _) && bag.GetCount("crop") == 1 && state.Current == 0, "Free harvest works at zero stamina");
        state.Refill();
        Check(!actions.TryAct(0, FarmTool.Seeds, out _) && !farm.Cells[0].Planted && state.Current == 10, "No seed costs no stamina");
        var paid = new FarmActions(farm, bag, "seed", "crop", state, 4, 2, 3, 1);
        Check(paid.TryAct(1, FarmTool.Hoe, out _) && state.Current == 6, "Custom costs apply");
        Reject(() => new FarmActions(farm, bag, "seed", "crop", state, -1));
        var huge = new StaminaState(int.MaxValue);
        Check(huge.TrySpend(int.MaxValue) && huge.Current == 0, "Maximum integer spending stays in range");
        Console.WriteLine("PASS: " + checks + " stamina assertions"); return 0;
    }
}
