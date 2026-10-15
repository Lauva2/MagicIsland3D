using System;
using System.Linq;
using MagicIsland.Animals;
using MagicIsland.Inventory;
using MagicIsland.Economy;
using MagicIsland.Orders;
using MagicIsland.CalendarSystem;
using MagicIsland.Saving;
class VerifyQualityEggs
{
    static int checks;
    static void Check(bool ok, string why) { checks++; if (!ok) throw new Exception(why); }
    static readonly ItemDefinition[] items = {
        new ItemDefinition("egg", "Egg", ItemType.AnimalProduct, 99), new ItemDefinition("quality_egg", "Quality", ItemType.AnimalProduct, 99),
        new ItemDefinition("animal_feed", "Feed", ItemType.Material, 99), new ItemDefinition("wood", "Wood", ItemType.Material, 99),
        new ItemDefinition("turnip", "Turnip", ItemType.Crop, 99), new ItemDefinition("fried_egg", "Fried", ItemType.Food, 99), new ItemDefinition("soup", "Soup", ItemType.Food, 99) };
    static AnimalDefinition Rule(int chance = 25) => new AnimalDefinition(15, 5, 200, 50, 10, 25, qualityChance: chance);
    static SavedAnimals Record(int day = 1, int seed = 1, int affinity = 70) => new SavedAnimals { Built = true, Day = day, QualitySeed = seed,
        Chickens = new[] { new SavedChicken { Id = 1, FedDay = day, Affinity = affinity }, new SavedChicken { Id = 2, FedDay = day, Affinity = affinity } } };
    static AnimalState From(SavedAnimals s, AnimalDefinition rule = null) { var state = new AnimalState(rule ?? Rule(), s.Day); state.Restore(s); return state; }
    static void Reject(Action action) { bool failed = false; try { action(); } catch (ArgumentException) { failed = true; } catch (InvalidOperationException) { failed = true; } Check(failed, "Invalid mutation rejected"); }
    static void Main()
    {
        int quality = 0;
        for (int seed = 1; seed <= 4096; seed++)
        {
            var state = From(Record(seed: seed)); var twin = From(state.Capture()); state.NextDay(2); twin.NextDay(2);
            Check(state.TotalEggs == 2 && state.Eggs == twin.Eggs && state.QualityEggs == twin.QualityEggs, "One egg per fed bird and stable pre-sleep reload"); quality += state.QualityEggs;
            var loaded = From(state.Capture()); Check(loaded.QualitySeed == seed && loaded.QualityEggs == state.QualityEggs && loaded.Eggs == state.Eggs, "Pending quality survives reload");
            var before = loaded.Capture(); Reject(() => loaded.NextDay(2)); Check(loaded.TotalEggs == before.Eggs + before.QualityEggs && loaded.Day == 2, "No duplicate settlement");
            var low = From(Record(seed: seed, affinity: 69)); low.NextDay(2); Check(low.Eggs == 2 && low.QualityEggs == 0, "Below threshold never premium");
        }
        Check(quality > 1800 && quality < 2300, "25 percent distribution across 8192 independent deterministic rolls");
        var always = From(Record(), Rule(100)); always.NextDay(2); Check(always.QualityEggs == 2 && always.Eggs == 0, "Threshold inclusive");
        var never = From(Record(), Rule(0)); never.NextDay(2); Check(never.Eggs == 2, "Configured zero chance");
        var unfed = Record(); unfed.Chickens[1].FedDay = 0; var one = From(unfed, Rule(100)); one.NextDay(2); Check(one.QualityEggs == 1, "Unfed bird yields nothing");
        var mixed = Record(2); foreach (var c in mixed.Chickens) c.FedDay = 0; mixed.Eggs = 1; mixed.QualityEggs = 1;
        var animals = From(mixed); var bag = new InventoryState(1, items);
        Check(animals.Collect(bag) == AnimalResult.BagFull && animals.TotalEggs == 2 && bag.GetCount("egg") == 0 && bag.GetCount("quality_egg") == 0, "Two classes compete for one empty slot; collect atomic");
        bag = new InventoryState(2, items); bool observed = false;
        bag.Changed += () => { observed = true; Check(animals.TotalEggs == 0 && bag.GetCount("egg") == 1 && bag.GetCount("quality_egg") == 1, "Both classes committed before observers"); Check(animals.Collect(bag) == AnimalResult.Busy, "Collect reentry blocked"); };
        Check(animals.Collect(bag) == AnimalResult.Success && observed, "Collect all kinds once");
        bag = new InventoryState(2, items); bag.TryAdd("egg", 1); bag.TryAdd("quality_egg", 1); var gold = new EconomyState(0);
        Check(animals.Sell(bag, gold, quality: true) == AnimalResult.Success && gold.Gold == 40 && bag.GetCount("egg") == 1, "Quality sale only at 40");
        Check(animals.Sell(bag, gold) == AnimalResult.Success && gold.Gold == 65, "Ordinary sale unchanged at 25");
        bag.TryAdd("quality_egg", 1); gold.Restore(long.MaxValue - 39); Check(animals.Sell(bag, gold, quality: true) == AnimalResult.GoldLimit && bag.GetCount("quality_egg") == 1, "Overflow retains premium");
        var full = Record(2); full.Eggs = 49; full.QualityEggs = 50; foreach (var c in full.Chickens) c.FedDay = 0;
        var capped = From(full); bag.TryAdd("animal_feed", 1); Check(capped.CheckFeed(bag) == AnimalResult.EggsFull, "Combined pending capacity blocks feeding");
        full.Chickens[0].FedDay = 2; Check(!AnimalState.Valid(full, 2), "Reject pending plus tomorrow reserve overflow");
        foreach (int bad in new[] { -1, 100, int.MaxValue }) { var invalid = Record(2); invalid.QualityEggs = bad; Reject(() => animals.Restore(invalid)); }
        var seedZero = Record(2); seedZero.QualitySeed = 0; Reject(() => animals.Restore(seedZero));
        var cooking = new CookingState(new[] { new CookingDefinition("fried", "egg", 1, "fried_egg", 1, 25), new CookingDefinition("soup", new[] { new IngredientCost("egg", 2), new IngredientCost("turnip", 1) }, "soup", 1, 45) });
        bag = new InventoryState(4, items); bag.TryAdd("egg", 1); bag.TryAdd("quality_egg", 3); bag.TryAdd("turnip", 1);
        Check(cooking.Cook(0, bag) == CookingResult.Success && bag.GetCount("egg") == 0 && bag.GetCount("quality_egg") == 3, "Cooking ordinary first");
        Check(cooking.Cook(1, bag) == CookingResult.Success && bag.GetCount("quality_egg") == 1 && bag.GetCount("turnip") == 0, "Premium-only recipe accepted");
        Check(cooking.Cook(1, bag) == CookingResult.InsufficientIngredients && bag.GetCount("quality_egg") == 1, "Missing second ingredient atomic");
        var tiny = new InventoryState(1, items); tiny.TryAdd("quality_egg", 2); Check(cooking.Cook(0, tiny) == CookingResult.BagFull && tiny.GetCount("quality_egg") == 2, "Premium cook full-output retains ingredient");
        var order = new OrderState(new[] { new OrderDefinition("egg", "egg", SeasonMask.All, 2, 2, 35) }, 28, 1, 1);
        bag = new InventoryState(2, items); bag.TryAdd("egg", 1); bag.TryAdd("quality_egg", 2); gold = new EconomyState(0);
        bool orderSeen = false; bag.Changed += () => { orderSeen = true; Check(order.Fulfilled && gold.Gold == 70 && bag.GetCount("egg") == 0 && bag.GetCount("quality_egg") == 1, "Mixed delivery commits before observers"); Check(order.Deliver(bag, gold) == DeliveryResult.Busy, "Order reentry blocked"); };
        Check(order.Deliver(bag, gold) == DeliveryResult.Success && orderSeen, "Mixed order consumes normal then premium"); Check(order.Deliver(bag, gold) == DeliveryResult.AlreadyFulfilled, "Paid order no duplication");
        var old = new SaveData { Version = 22, Day = 2, Animals = mixed, Gold = 123, Order = order.Capture() }; old.Animals.Chickens[0].Affinity = 88; old.Animals.Chickens[0].LastPetDay = 2;
        old.Upgrade(100); Check(old.Version == SaveData.CurrentVersion && old.Animals.Eggs == 1 && old.Animals.QualityEggs == 0 && old.Animals.QualitySeed == 1 && old.Animals.Chickens[0].Affinity == 88 && old.Animals.Chickens[0].LastPetDay == 2 && old.Order.Fulfilled && old.Gold == 123, "v22 migration preserves original pending, affinity, dates and paid order");
        old.Animals.QualitySeed = 444; old.Animals.QualityEggs = 5; old.Upgrade(100); Check(old.Animals.QualitySeed == 444 && old.Animals.QualityEggs == 5, "Current upgrade keeps quality");
        Console.WriteLine("PASS: " + checks + " quality egg checks; quality rolls " + quality + "/8192");
    }
}
