using System;
using System.Linq;
using MagicIsland.Farming;
using MagicIsland.Inventory;
using MagicIsland.Magic;
using MagicIsland.Stamina;
using MagicIsland.Saving;

class VerifyMagic
{
    static int checks;
    static void Check(bool ok, string why) { checks++; if (!ok) throw new Exception(why); }
    static readonly MagicDefinition definition = new MagicDefinition("moonflower", "moon_seed", "moon", "quality_moon");
    static ItemDefinition[] Items() => new[] {
        new ItemDefinition("moon_seed", "Seed", ItemType.Seed, 99), new ItemDefinition("moon", "Flower", ItemType.Crop, 99),
        new ItemDefinition("quality_moon", "Quality flower", ItemType.Crop, 99), new ItemDefinition("wood", "Wood", ItemType.Material, 99) };
    static FarmState Farm(int size = 4) => new FarmState(size, 4, new[] { new CropDefinition("moonflower", "moon_seed", "moon", 4, "quality_moon") }, "moonflower");
    static void Plant(FarmState farm, int i)
    { farm.TryAct(i, FarmTool.Hoe, out _); farm.TryAct(i, FarmTool.Seeds, out _, "moonflower"); }
    static void Reject(Action run) { bool rejected = false; try { run(); } catch (ArgumentException) { rejected = true; } Check(rejected, "Invalid progress rejected"); }
    static int Main()
    {
        var bag = new InventoryState(2, Items()); var magic = new MagicState(definition); var farm = Farm(); var stamina = new StaminaState(100);
        Check(magic.CheckRain(farm, stamina, 0) == MagicResult.Locked, "Unknown spell stays locked");
        bag.TryAdd("wood", 198);
        Check(magic.Study(bag) == MagicResult.BagFull && magic.Stage == MagicStage.Unstudied && bag.GetCount("moon_seed") == 0, "Full bag cannot consume seed reward");
        bag.TryRemove("wood", 99);
        Action observer = () => {
            Check(magic.Stage == MagicStage.GrowingMoonflowers && bag.GetCount("moon_seed") == 3, "Reward and progress commit together");
            Check(magic.Study(bag) == MagicResult.Busy, "Inventory observer cannot reenter study");
        };
        bag.Changed += observer; Check(magic.Study(bag) == MagicResult.Success, "Study grants seeds"); bag.Changed -= observer;
        Check(magic.Study(bag) == MagicResult.WrongStage && bag.GetCount("moon_seed") == 3, "Seed reward cannot repeat");
        magic.RecordHarvest("turnip"); Check(magic.Stage == MagicStage.GrowingMoonflowers, "Ordinary crops do not advance magic");
        magic.RecordHarvest("moonflower"); Check(magic.Stage == MagicStage.OfferingMoonflower, "Actual moonflower harvest advances quest");
        Check(magic.Study(bag) == MagicResult.MissingFlower && !magic.RainLearned, "Sold or stored flower cannot pay offering");
        bag.TryRemove("wood", 99); bag.TryAdd("quality_moon", 1);
        observer = () => { Check(magic.RainLearned && bag.GetCount("quality_moon") == 0, "Quality offering atomically unlocks spell"); Check(magic.Study(bag) == MagicResult.Busy, "Unlock observer cannot consume twice"); };
        bag.Changed += observer; Check(magic.Study(bag) == MagicResult.Success, "Quality flower substitutes for ordinary flower"); bag.Changed -= observer;
        Check(magic.Study(bag) == MagicResult.WrongStage, "Learned spell cannot repeat offering");
        var snapshot = magic.Capture(); snapshot.Stage = MagicStage.Unstudied; Check(magic.RainLearned, "Captured magic record is independent");
        Reject(() => magic.Restore(null)); Reject(() => magic.Restore(new SavedMagic { Stage = (MagicStage)(-1) })); Reject(() => magic.Restore(new SavedMagic { Stage = (MagicStage)4 }));
        Check(magic.RainLearned, "Invalid restore leaves live progress intact");
        Check(magic.CheckRain(farm, stamina, -1) == MagicResult.InvalidPlot && magic.CheckRain(farm, stamina, 16) == MagicResult.InvalidPlot, "Bad target indices rejected");
        Check(magic.CheckRain(farm, stamina, 0) == MagicResult.InvalidPlot, "Untilled center rejected");
        farm.TryAct(0, FarmTool.Hoe, out _); Check(magic.CheckRain(farm, stamina, 0) == MagicResult.NothingToWater, "Empty farmland is free");
        for (int i = 0; i < 16; i++) Plant(farm, i);
        farm.Cells[1].Watered = true; farm.Cells[4].Growth = 4;
        stamina.Restore(5); Check(magic.CastRain(farm, stamina, 0, out int watered) == MagicResult.InsufficientStamina && watered == 0 && !farm.Cells[0].Watered, "Insufficient stamina changes no plot");
        stamina.Refill();
        Action staminaObserver = () => {
            Check(stamina.Current == 94 && farm.Cells[0].Watered && farm.Cells[5].Watered && !farm.Cells[2].Watered && !farm.Cells[4].Watered, "Observer sees all dry seedlings watered, mature and outside plots untouched");
            Check(magic.CastRain(farm, stamina, 0, out _) == MagicResult.Busy, "Cast notifications cannot reenter spell");
        };
        stamina.Changed += staminaObserver;
        Check(magic.CastRain(farm, stamina, 0, out watered) == MagicResult.Success && watered == 2, "Corner casts clamp to grid and skip wet or mature plants"); stamina.Changed -= staminaObserver;
        Check(magic.CastRain(farm, stamina, 0, out watered) == MagicResult.NothingToWater && stamina.Current == 94, "Repeated rain on wet area is free");
        Check(farm.Cells.All(c => c.Growth == 0 || c.Growth == 4) && farm.Day == 1, "Spell does not advance time or grow crops");
        Check(magic.CastRain(farm, stamina, 10, out watered) == MagicResult.Success && watered == 8 && stamina.Current == 88, "Inner cast affects a 3 by 3 neighborhood with flat cost");
        var both = new InventoryState(4, Items()); both.TryAdd("moon", 1); both.TryAdd("quality_moon", 1);
        var other = new MagicState(definition); other.Restore(new SavedMagic { Stage = MagicStage.OfferingMoonflower });
        Check(other.Study(both) == MagicResult.Success && both.GetCount("moon") == 0 && both.GetCount("quality_moon") == 1, "Ordinary flower consumed before quality");
        var growing = new MagicState(definition); growing.Restore(new SavedMagic { Stage = MagicStage.GrowingMoonflowers });
        var harvestBag = new InventoryState(1, Items()); harvestBag.TryAdd("wood", 99); var harvestFarm = Farm(1); Plant(harvestFarm, 0); harvestFarm.Cells[0].Growth = 4;
        var actions = new FarmActions(harvestFarm, harvestBag, "moon_seed", "moon", crops: new[] { harvestFarm.FindCrop("moonflower") }); actions.SetMagicHarvestRecorder(growing.RecordHarvest);
        Check(!actions.TryAct(0, FarmTool.Hoe, out _) && growing.Stage == MagicStage.GrowingMoonflowers && harvestFarm.Cells[0].Mature, "Full bag harvest cannot unlock quest");
        harvestBag.TryRemove("wood", 99);
        observer = () => Check(growing.Stage == MagicStage.OfferingMoonflower && harvestBag.GetCount("moon") + harvestBag.GetCount("quality_moon") == 1 && !harvestFarm.Cells[0].Planted, "Real harvest commits progress before inventory observers");
        harvestBag.Changed += observer; Check(actions.TryAct(0, FarmTool.Hoe, out _), "Harvest action records magic crop");
        for (int version = 1; version <= 29; version++)
        {
            var legacy = SaveData.Capture(Farm(), new InventoryState(2, Items()), 1, 0, 1, 123, selectedSeed: "moon_seed");
            legacy.Version = version; legacy.Magic = new SavedMagic { Stage = MagicStage.RainLearned };
            legacy.Upgrade(123, defaultCropId: "moonflower", defaultSeedId: "moon_seed");
            Check(legacy.Version == SaveData.CurrentVersion && legacy.Magic.Stage == MagicStage.Unstudied && legacy.Gold == 123 && legacy.Slots.All(s => s.Quantity == 0), "Legacy chain initializes only new magic without rewards: v" + version);
        }
        var invalid = SaveData.Capture(Farm(), new InventoryState(2, Items()), 1, 0, 1, selectedSeed: "moon_seed");
        invalid.Magic = null;
        bool failed = false; try { invalid.Validate(4, 4, new InventoryState(2, Items()), crops: new[] { Farm().FindCrop("moonflower") }, defaultCropId: "moonflower"); } catch (InvalidOperationException) { failed = true; }
        Check(failed, "Current save missing magic rejects whole record");
        Console.WriteLine("PASS: " + checks + " magic transaction, progression, area watering and harvest assertions"); return 0;
    }
}
