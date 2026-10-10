using System;
using System.Linq;
using MagicIsland.Tasks;
using MagicIsland.Economy;
using MagicIsland.Farming;
using MagicIsland.Inventory;
using MagicIsland.Saving;

internal static class VerifyTasks
{
    static int checks;
    static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    static void Reject(Action action) { bool rejected = false; try { action(); } catch (ArgumentException) { rejected = true; } Check(rejected, "Invalid tasks rejected"); }
    static readonly TaskDefinition[] definitions = {
        new TaskDefinition("hoe", TaskAction.Hoe, 1, 5), new TaskDefinition("plant", TaskAction.Plant, 1, 10),
        new TaskDefinition("water", TaskAction.Water, 1, 10), new TaskDefinition("sleep", TaskAction.Sleep, 1, 15),
        new TaskDefinition("harvest", TaskAction.Harvest, 1, 20), new TaskDefinition("sell", TaskAction.Sell, 1, 25) };
    public static int Main()
    {
        Reject(() => new TaskDefinition("", TaskAction.Hoe, 1, 5)); Reject(() => new TaskDefinition("x", (TaskAction)99, 1, 5));
        Reject(() => new TaskDefinition("x", TaskAction.Hoe, 0, 5)); Reject(() => new TaskDefinition("x", TaskAction.Hoe, 1, 0));
        Reject(() => new TaskState(Array.Empty<TaskDefinition>())); Reject(() => new TaskState(new[] { definitions[0], definitions[0] }));
        var state = new TaskState(definitions); var gold = new EconomyState(100); int notifications = 0;
        state.Changed += () => notifications++;
        Check(!state.Ready && !state.Finished && state.CurrentIndex == 0, "New chain begins at hoe");
        Check(state.TryClaim(gold) == TaskClaimResult.Incomplete && gold.Gold == 100, "Incomplete claim is free");
        Check(!state.Record((TaskAction)99) && !state.Record(TaskAction.Hoe, 0) && !state.Record(TaskAction.Hoe, -1), "Invalid action reports ignored");
        Check(notifications == 0, "Invalid reports do not notify");
        Check(state.Record(TaskAction.Plant) && state.GetProgress(1) == 1 && state.CurrentIndex == 0 && !state.Ready, "Early later-stage work remembered but locked");
        Check(state.Record(TaskAction.Hoe, int.MaxValue) && state.GetProgress(0) == 1 && state.Ready, "Progress saturates without overflow");
        Check(!state.Record(TaskAction.Hoe) && notifications == 2, "Duplicate completed progress emits nothing");
        var saturated = new EconomyState(long.MaxValue);
        Check(state.TryClaim(saturated) == TaskClaimResult.GoldLimit && state.CurrentIndex == 0 && !state.IsClaimed(0), "Gold limit preserves pending reward");
        TaskClaimResult reentry = TaskClaimResult.Success;
        Action observer = () => {
            Check(state.IsClaimed(state.CurrentIndex - 1), "Gold observers see committed reward flag");
            reentry = state.TryClaim(gold);
        };
        gold.Changed += observer;
        Check(state.TryClaim(gold) == TaskClaimResult.Success && gold.Gold == 105 && state.CurrentIndex == 1 && state.Ready, "Claim unlocks already performed planting");
        Check(reentry == TaskClaimResult.Busy, "Observer reentry cannot claim next reward");
        gold.Changed -= observer;
        var saved = state.Capture(); state.Record(TaskAction.Water);
        Check(saved[2].Progress == 0, "Captured progress is independent");
        var restored = new TaskState(definitions); restored.Restore(saved); saved[0].Claimed = false;
        Check(restored.IsClaimed(0), "Restored state independent from input");
        Check(restored.TryClaim(gold) == TaskClaimResult.Success && gold.Gold == 115, "Restored claim pays only next reward");
        for (int i = 2; i < definitions.Length; i++)
        {
            restored.Record(definitions[i].Action); var copy = new TaskState(definitions); copy.Restore(restored.Capture());
            Check(copy.CurrentIndex == i && copy.Ready, "Pending reward survives reload");
            Check(copy.TryClaim(gold) == TaskClaimResult.Success, "Claim after reload"); restored = copy;
        }
        Check(restored.Finished && gold.Gold == 185 && restored.TryClaim(gold) == TaskClaimResult.Finished, "Full chain pays exactly 85 once");
        Check(!restored.Record(TaskAction.Sell, int.MaxValue), "Finished chain ignores further work");
        var guard = new TaskState(definitions); var original = restored.Capture(); guard.Restore(original);
        Action<TaskProgress[]> invalid = records => { Reject(() => guard.Restore(records)); Check(guard.Finished, "Rejected restore keeps previous state"); };
        invalid(null); invalid(Array.Empty<TaskProgress>());
        var bad = restored.Capture(); bad[0] = null; invalid(bad);
        bad = restored.Capture(); bad[0].Id = "unknown"; invalid(bad);
        bad = restored.Capture(); bad[0].Progress = -1; invalid(bad);
        bad = restored.Capture(); bad[0].Progress = 2; invalid(bad);
        bad = restored.Capture(); bad[0].Progress = 0; invalid(bad);
        bad = restored.Capture(); bad[0].Claimed = false; invalid(bad);
        var multiple = new TaskState(new[] { new TaskDefinition("many", TaskAction.Sell, 3, 5) });
        multiple.Record(TaskAction.Sell, 2); Check(!multiple.Ready && multiple.GetProgress(0) == 2, "Sale quantity contributes actual amount");
        multiple.Record(TaskAction.Sell, int.MaxValue); Check(multiple.Ready && multiple.GetProgress(0) == 3, "Batch sale caps progress");

        var farm = new FarmState(2); var items = new[] { new ItemDefinition("turnip_seed", "seed", ItemType.Seed, 99), new ItemDefinition("turnip", "crop", ItemType.Crop, 99) };
        var bag = new InventoryState(4, items);
        var snapshot = SaveData.Capture(farm, bag, 0, 0, 0, 185, tasks: restored);
        snapshot.Validate(2, 3, new InventoryState(4, items), tasks: definitions);
        Check(snapshot.Version == SaveData.CurrentVersion && snapshot.Tasks.All(task => task.Claimed), "Version 7 captures reward flags");
        snapshot.Tasks[0].Id = "wrong"; Reject(() => snapshot.Validate(2, 3, new InventoryState(4, items), tasks: definitions));
        for (int version = 1; version <= 6; version++)
        {
            var legacy = SaveData.Capture(farm, bag, 0, 0, 0, 87); legacy.Version = version; legacy.Upgrade(100, tasks: definitions);
            legacy.Validate(2, 3, new InventoryState(4, items), tasks: definitions);
            Check(legacy.Version == SaveData.CurrentVersion && legacy.Tasks.Length == 6 && legacy.Tasks.All(task => !task.Claimed && task.Progress == 0)
                && legacy.Gold == (version == 1 ? 100 : 87), "Legacy migration starts fresh tasks without granting reward");
        }
        var current = SaveData.Capture(farm, bag, 0, 0, 0, 185, tasks: restored); current.Upgrade(999, tasks: definitions);
        Check(current.Gold == 185 && current.Tasks.All(task => task.Claimed), "Current version upgrade does not reset progress or rewards");
        Console.WriteLine("PASS: " + checks + " task/reward/save assertions"); return 0;
    }
}
