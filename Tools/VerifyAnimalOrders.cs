using System;
using System.Linq;
using MagicIsland.Animals;
using MagicIsland.CalendarSystem;
using MagicIsland.Economy;
using MagicIsland.Farming;
using MagicIsland.Inventory;
using MagicIsland.Orders;
using MagicIsland.Saving;

class VerifyAnimalOrders
{
    static int checks;
    static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
    static void Reject(Action action)
    { bool rejected = false; try { action(); } catch (ArgumentException) { rejected = true; } catch (InvalidOperationException) { rejected = true; } Check(rejected, "Invalid state rejected"); }
    static readonly OrderDefinition[] crops = {
        new OrderDefinition("turnip_daily", "turnip", SeasonMask.Spring | SeasonMask.Autumn | SeasonMask.Winter, 1, 3, 40),
        new OrderDefinition("tomato_daily", "tomato", SeasonMask.Summer | SeasonMask.Winter, 1, 3, 75),
        new OrderDefinition("corn_daily", "corn", SeasonMask.Summer | SeasonMask.Autumn | SeasonMask.Winter, 1, 3, 130) };
    static readonly OrderDefinition[] animalOffers = {
        new OrderDefinition("egg_daily", "egg", SeasonMask.All, 2, 2, 35, true),
        new OrderDefinition("fried_egg_daily", "fried_egg", SeasonMask.All, 1, 1, 50, true),
        new OrderDefinition("egg_vegetable_soup_daily", "egg_vegetable_soup", SeasonMask.All, 1, 1, 90, true) };
    static readonly OrderDefinition[] all = crops.Concat(animalOffers).ToArray();
    static readonly ItemDefinition[] items = {
        new ItemDefinition("turnip_seed", "Seed", ItemType.Seed, 99), new ItemDefinition("turnip", "Turnip", ItemType.Crop, 99),
        new ItemDefinition("tomato", "Tomato", ItemType.Crop, 99), new ItemDefinition("corn", "Corn", ItemType.Crop, 99),
        new ItemDefinition("egg", "Egg", ItemType.AnimalProduct, 99), new ItemDefinition("fried_egg", "Fried egg", ItemType.Food, 99),
        new ItemDefinition("egg_vegetable_soup", "Egg soup", ItemType.Food, 99) };
    static bool Same(SavedOrder a, SavedOrder b) => a.Seed == b.Seed && a.Day == b.Day && a.DefinitionId == b.DefinitionId
        && a.ItemId == b.ItemId && a.Quantity == b.Quantity && a.Reward == b.Reward && a.Fulfilled == b.Fulfilled && a.CoopOffersUnlocked == b.CoopOffersUnlocked;
    static string Bag(InventoryState bag) => string.Join("|", bag.Slots.Select(s => s.ItemId + ":" + s.Quantity));
    static void Main()
    {
        Reject(() => new OrderState(animalOffers, 28, 1, 2, true));
        Reject(() => new OrderState(all, 28, 1, 1, true));
        var seen = new System.Collections.Generic.HashSet<string>();
        for (int seed = 1; seed <= 32; seed++) for (int day = 1; day <= 225; day++)
        {
            var legacy = new OrderState(crops, 28, seed, day); var locked = new OrderState(all, 28, seed, day);
            Check(Same(legacy.Capture(), locked.Capture()), "Expanded locked pool leaves every original seed/day unchanged");
            var restored = new OrderState(all, 28, 100, 1); restored.Restore(legacy.Capture(), day);
            Check(Same(restored.Capture(), legacy.Capture()), "Every old order restores without reroll");
            if (day == 1) continue;
            var unlocked = new OrderState(all, 28, seed, day, true); seen.Add(unlocked.ItemId);
            var definition = all.Single(d => d.Id == unlocked.Capture().DefinitionId);
            Check(unlocked.CoopOffersUnlocked && unlocked.Quantity >= definition.MinQuantity && unlocked.Quantity <= definition.MaxQuantity
                && unlocked.Reward == (long)unlocked.Quantity * definition.GoldPerItem && Seasons.Includes(definition.Seasons, new CalendarDate(day, 28).Season), "Unlocked seasonal generation and rewards");
            restored.Restore(unlocked.Capture(), day); Check(Same(restored.Capture(), unlocked.Capture()), "Unlocked crop and animal orders retain exact pool on reload");
            unlocked.NextDay(day + 1, true); restored.NextDay(day + 1, true);
            Check(Same(restored.Capture(), unlocked.Capture()) && !restored.Fulfilled, "Next day deterministic after reload");
        }
        Check(seen.Count == 6, "Every crop, egg and prepared food appears in the unlocked pool");
        foreach (var offer in animalOffers)
        {
            int seed = Enumerable.Range(1, 100).First(s => new OrderState(all, 28, s, 2, true).ItemId == offer.ItemId);
            var state = new OrderState(all, 28, seed, 1); var beforeBuild = state.Capture();
            Check(!state.CoopOffersUnlocked && Same(beforeBuild, state.Capture()), "Construction leaves today's order unchanged");
            state.NextDay(2, true); Check(state.ItemId == offer.ItemId && state.Reward == (offer.ItemId == "egg" ? 70 : offer.ItemId == "fried_egg" ? 50 : 90), "Day after building can request each new product with exact price");
            var bag = new InventoryState(3, items); var gold = new EconomyState(100);
            if (state.Quantity > 1) bag.TryAdd(state.ItemId, state.Quantity - 1); string before = Bag(bag);
            Check(state.Deliver(bag, gold) == DeliveryResult.InsufficientItems && Bag(bag) == before && gold.Gold == 100 && !state.Fulfilled, "Insufficient products consume nothing");
            bag.TryAdd(state.ItemId, 3); long stock = bag.GetCount(state.ItemId);
            Check(state.Deliver(bag, new EconomyState(long.MaxValue)) == DeliveryResult.GoldLimit && bag.GetCount(state.ItemId) == stock && !state.Fulfilled, "Gold limit retains every product");
            int notifications = 0; Action observer = () => { notifications++; Check(state.Fulfilled && gold.Gold == 100 + state.Reward && bag.GetCount(state.ItemId) == stock - state.Quantity, "Observers see full transaction"); Check(state.Deliver(bag, gold) == DeliveryResult.Busy, "Nested delivery cannot repeat"); Reject(() => state.NextDay(3, true)); };
            bag.Changed += observer; Check(state.Deliver(bag, gold) == DeliveryResult.Success && notifications == 1, "New product delivery commits once"); bag.Changed -= observer;
            Check(state.Deliver(bag, gold) == DeliveryResult.AlreadyFulfilled && gold.Gold == 100 + state.Reward, "Already delivered cannot pay twice");
            var saved = state.Capture(); var read = new OrderState(all, 28, 1, 1); read.Restore(saved, 2); saved.Fulfilled = false; saved.CoopOffersUnlocked = false;
            Check(read.Fulfilled && read.CoopOffersUnlocked && read.Deliver(bag, gold) == DeliveryResult.AlreadyFulfilled, "Capture and restore are independent and paid flag persists");
            var bad = read.Capture(); bad.CoopOffersUnlocked = false; var good = read.Capture(); Reject(() => read.Restore(bad, 2)); Check(Same(good, read.Capture()), "Wrong pool cannot smuggle animal order into a locked save");
            bad = read.Capture(); bad.Reward++; Reject(() => read.Restore(bad, 2)); Check(Same(good, read.Capture()), "Corrupt reward restore remains atomic");
            read.NextDay(3); Check(read.CoopOffersUnlocked, "Once unlocked, default next-day API retains eligibility");
        }
        for (int version = 8; version <= 20; version++) foreach (bool fulfilled in new[] { false, true })
        {
            var farm = new FarmState(2); for (int i = 1; i < 57; i++) farm.AdvanceDay();
            var bag = new InventoryState(3, items); bag.TryAdd("fried_egg", 2); bag.TryAdd("egg", 3);
            var old = new OrderState(crops, 28, 12345, farm.Day).Capture(); old.Fulfilled = fulfilled;
            var save = SaveData.Capture(farm, bag, 0, 0, 0, 123); save.Version = version; save.Order = old;
            save.Animals = new SavedAnimals { Built = true, Day = farm.Day }; string stock = Bag(bag);
            save.Upgrade(999, orders: all); save.Validate(2, 3, bag.CreateEmpty(3), orders: all);
            Check(save.Version == SaveData.CurrentVersion && Same(old, save.Order) && save.Gold == 123 && Bag(bag) == stock, "All legacy crop orders keep id/quantity/reward/paid state/inventory/gold");
            Check(save.Animals.Built == (version == 20), "Only v20 already-built coop survives older animal migrations");
            var resumed = new OrderState(all, 28, 1, 1); resumed.Restore(save.Order, farm.Day); resumed.NextDay(farm.Day + 1, save.Animals.Built);
            Check(resumed.CoopOffersUnlocked == (version == 20), "Migrated built coop joins new pool only on next day");
        }
        var f = new FarmState(2); f.AdvanceDay(); var inventory = new InventoryState(3, items); inventory.TryAdd("egg", 5);
        var order = new OrderState(all, 28, 1, 2, true); var snapshot = SaveData.Capture(f, inventory, 0, 0, 0, 200, orders: order);
        Reject(() => snapshot.Validate(2, 3, inventory.CreateEmpty(3), orders: all));
        snapshot.Animals.Built = true; snapshot.Validate(2, 3, inventory.CreateEmpty(3), orders: all);
        Check(snapshot.Order.CoopOffersUnlocked && snapshot.Gold == 200 && snapshot.Slots[0].Quantity == 5, "Full snapshot retains eligibility with coop and resources");
        Console.WriteLine("PASS: " + checks + " animal/food order unlock, deterministic, delivery and migration assertions");
    }
}
