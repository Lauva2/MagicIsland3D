using System;
using System.Linq;
using MagicIsland.Farming;
using MagicIsland.Inventory;
using MagicIsland.Magic;
using MagicIsland.Saving;

class VerifyMagicExploration
{
    static int checks;
    static void Check(bool ok, string why) { checks++; if (!ok) throw new Exception(why); }
    static MagicState Magic(bool learned = true)
    {
        var state = new MagicState(new MagicDefinition("moonflower", "seed", "flower", "quality"));
        if (learned) state.Restore(new SavedMagic { Stage = MagicStage.RainLearned });
        return state;
    }
    static ItemDefinition[] Items() => new[] { new ItemDefinition("seed", "Seed", ItemType.Seed, 99), new ItemDefinition("wood", "Wood", ItemType.Material, 99),
        new ItemDefinition("flower", "Flower", ItemType.Crop, 99), new ItemDefinition("quality", "Quality", ItemType.Crop, 99) };
    static int Main()
    {
        var state = Magic(); var bag = new InventoryState(1, Items()); int notices = 0, stateNotices = 0;
        bag.Changed += () => notices++; state.Changed += () => stateNotices++;
        Check(!Magic(false).CanReadMemory(0) && Magic(false).CompleteMemory(0, bag, "seed", 3) == MemoryResult.Locked, "Initiation required");
        Check(!state.CanReadMemory(-1) && !state.CanReadMemory(3) && !state.CanReadMemory(1), "Only first ordered memory is readable");
        for (int i = -2; i < 6; i++) if (i != 0) Check(state.CompleteMemory(i, bag, "seed", 3) == MemoryResult.OutOfOrder, "Reject wrong index " + i);
        Check(notices == 0 && stateNotices == 0 && state.MemoriesCompleted == 0, "Read-only and rejected requests publish nothing");
        bag.TryAdd("wood", 99); int before = notices;
        Check(state.CompleteMemory(0, bag, "seed", 3) == MemoryResult.BagFull && notices == before && state.MemoriesCompleted == 0, "Full bag preserves pending memory");
        bag.TryRemove("wood", 99); before = notices;
        Check(state.CheckMemory(0, bag, "seed", 3) == MemoryResult.Success && notices == before && state.MemoriesCompleted == 0, "Prepare has no effects");
        Action observe = () => {
            Check(state.MemoriesCompleted == 1 && bag.GetCount("seed") == 3, "Observer sees reward and progress together");
            Check(state.CompleteMemory(0, bag, "seed", 3) == MemoryResult.Busy && state.CompleteMemory(1, bag, "seed", 3) == MemoryResult.Busy, "Cannot reenter current or next reward");
            Check(state.Study(bag) == MagicResult.Busy, "Cannot reenter initiation during reward");
        };
        bag.Changed += observe;
        Check(state.CompleteMemory(0, bag, "seed", 3) == MemoryResult.Success && notices == before + 1 && stateNotices == 1, "One commit, one notification per owner");
        bag.Changed -= observe;
        before = notices;
        Check(state.CompleteMemory(0, bag, "seed", 3) == MemoryResult.OutOfOrder && notices == before && bag.GetCount("seed") == 3, "Duplicate reward rejected");
        var checkpoint = state.Capture(); Check(checkpoint.Exploration.Completed == 1, "Capture progress");
        state.CompleteMemory(1, bag, "seed", 5);
        Check(checkpoint.Exploration.Completed == 1 && state.MemoriesCompleted == 2, "Snapshot independent of future progress");
        checkpoint.Exploration.Completed = 0; Check(state.MemoriesCompleted == 2, "Snapshot mutation cannot alter runtime");
        Check(state.CompleteMemory(2, bag, "seed", 8) == MemoryResult.Success && state.MemoriesCompleted == 3 && bag.GetCount("seed") == 16, "All ordered rewards accumulate once");
        Check(state.CompleteMemory(2, bag, "seed", 8) == MemoryResult.Finished && !state.CanReadMemory(2), "Completed journey cannot reward again");
        var loaded = Magic(); loaded.Restore(state.Capture()); Check(loaded.MemoriesCompleted == 3 && !loaded.CanReadMemory(0), "Restore blocks replay rewards");
        foreach (int count in new[] {-1,4,int.MaxValue})
        {
            var bad = state.Capture(); bad.Exploration.Completed = count;
            Check(!MagicState.Valid(bad), "Reject count " + count);
            bool rejected = false; try { loaded.Restore(bad); } catch (ArgumentException) { rejected = true; }
            Check(rejected && loaded.MemoriesCompleted == 3, "Reject without partial restore");
        }
        var invalid = state.Capture(); invalid.Exploration = null; Check(!MagicState.Valid(invalid), "Missing exploration rejected");
        foreach (MagicStage stage in new[] {MagicStage.Unstudied,MagicStage.GrowingMoonflowers,MagicStage.OfferingMoonflower})
            Check(!MagicState.Valid(new SavedMagic { Stage = stage, Exploration = new SavedMagicExploration { Completed = 1 } }), "Cross-field prerequisite validated");
        for (int version = 1; version <= 30; version++)
        {
            var farm = new FarmState(4,3); var inventory = new InventoryState(2,Items()); inventory.TryAdd("wood",17);
            var legacy = SaveData.Capture(farm,inventory,1,0,1,234,selectedSeed:"seed");
            legacy.Version = version; legacy.Magic = new SavedMagic { Stage = MagicStage.RainLearned, Exploration = null };
            legacy.Upgrade(234, defaultSeedId:"seed");
            Check(legacy.Version == 31 && legacy.Magic.Exploration.Completed == 0 && legacy.Gold == 234
                && legacy.Slots.Single(s => s.ItemId == "wood").Quantity == 17 && legacy.Slots.Sum(s => s.Quantity) == 17, "No migration rewards or old inventory changes v" + version);
            Check(legacy.Magic.Stage == (version == 30 ? MagicStage.RainLearned : MagicStage.Unstudied), "Preserve v30 initiation only v" + version);
        }
        var current = new SaveData {Version=31,Magic=state.Capture()}; current.Upgrade(100);
        Check(current.Magic.Exploration.Completed==3,"Current version is never reset by migration");
        var broken = new SaveData { Version=30, Magic=null }; bool failed=false;
        try { broken.Upgrade(100); } catch (InvalidOperationException) { failed=true; }
        Check(failed && broken.Version==30,"Missing v30 magic rejects migration");
        Console.WriteLine("PASS: " + checks + " exploration order, atomic rewards, snapshots, validation and v1-30 migration assertions"); return 0;
    }
}
