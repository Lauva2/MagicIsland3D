using System;
using System.Linq;
using MagicIsland.Inventory;
using MagicIsland.Animals;
using MagicIsland.Economy;
using MagicIsland.Farming;
using MagicIsland.Saving;

class VerifyFeedCrafting
{
    static int checks;
    static void Check(bool ok, string reason) { checks++; if (!ok) throw new Exception(reason); }
    static readonly ItemDefinition[] items = {
        new ItemDefinition("corn", "Corn", ItemType.Crop, 99), new ItemDefinition("animal_feed", "Feed", ItemType.Material, 99),
        new ItemDefinition("wood", "Wood", ItemType.Material, 99), new ItemDefinition("stone", "Stone", ItemType.Material, 99),
        new ItemDefinition("egg", "Egg", ItemType.AnimalProduct, 99) };
    static readonly CraftingDefinition[] recipes = { new CraftingDefinition("animal_feed", "animal_feed", 10, new[] { new IngredientCost("corn", 1) }) };
    static string Snapshot(InventoryState bag) => string.Join("|", bag.Slots.Select(s => s.ItemId + ":" + s.Quantity));
    static void Main()
    {
        for (int capacity = 1; capacity <= 4; capacity++) for (int corn = 0; corn <= 4; corn++) for (int feed = 0; feed <= 198; feed++)
        {
            var bag = new InventoryState(capacity, items); bag.TryAdd("corn", corn); bag.TryAdd("animal_feed", feed);
            while (bag.CanAdd("wood", 99)) bag.TryAdd("wood", 99);
            long initialCorn = bag.GetCount("corn"), initialFeed = bag.GetCount("animal_feed"); string before = Snapshot(bag);
            int openSlots = bag.Slots.Count(s => s.Empty) + (initialCorn == 1 ? 1 : 0);
            long space = bag.Slots.Where(s => s.ItemId == "animal_feed").Sum(s => 99 - s.Quantity) + openSlots * 99L;
            bool success = initialCorn > 0 && space >= 10; var state = new CraftingState(recipes); int events = 0;
            bag.Changed += () => { events++; Check(bag.GetCount("corn") == initialCorn - 1 && bag.GetCount("animal_feed") == initialFeed + 10, "Observers see complete ten-feed transaction"); Check(state.Craft(0, bag) == CraftingResult.Busy, "Nested crafting cannot duplicate feed"); };
            Check((state.Craft(0, bag) == CraftingResult.Success) == success, "Post-consumption capacity, partial feed stacks and corn boundaries");
            Check(success ? bag.GetCount("corn") == initialCorn - 1 && bag.GetCount("animal_feed") == initialFeed + 10 : Snapshot(bag) == before, "Full output or no loss");
            Check(events == (success ? 1 : 0), "Only successful craft notifies once");
        }
        var bag2 = new InventoryState(3, items); bag2.TryAdd("corn", 2); var crafting = new CraftingState(recipes);
        Check(crafting.Craft(0, bag2) == CraftingResult.Success && bag2.GetCount("animal_feed") == 10 && bag2.GetCount("corn") == 1, "One corn gives exactly ten feed");
        Check(crafting.Craft(0, bag2) == CraftingResult.Success && bag2.GetCount("animal_feed") == 20 && bag2.GetCount("corn") == 0, "Each intentional craft is one recipe");
        var storage = new StorageState(2, items, bag2); int feedSlot = Enumerable.Range(0, 3).First(i => bag2.Slots[i].ItemId == "animal_feed");
        Check(storage.Transfer(true, feedSlot, 20) == StorageResult.Success && storage.Contents.GetCount("animal_feed") == 20, "Crafted feed goes to storage like purchased feed");
        var farm = new FarmState(2); var save = SaveData.Capture(farm, bag2, 0, 0, 0, 123, stamina: 85, storage: storage);
        save.Validate(2, 3, bag2.CreateEmpty(3), storageSlots: 2);
        Check(save.Version == SaveData.CurrentVersion && save.Storage[0].ItemId == "animal_feed" && save.Storage[0].Quantity == 20 && save.Gold == 123 && save.Stamina == 85, "Existing v21 slots hold crafted feed without schema change");
        save.Upgrade(999, storageSlots: 2); Check(save.Storage[0].Quantity == 20 && save.Gold == 123, "Current save preserves supplies and money");
        var bag3 = new InventoryState(5, items); var gold = new EconomyState(500); bag3.TryAdd("wood", 15); bag3.TryAdd("stone", 5); bag3.TryAdd("corn", 1);
        var animals = new AnimalState(new AnimalDefinition(15, 5, 200, 50, 10, 25));
        Check(animals.Build(bag3, gold) == AnimalResult.Success && animals.BuyChicken(gold) == AnimalResult.Success && animals.BuyChicken(gold) == AnimalResult.Success, "Two birds ready");
        Check(crafting.Craft(0, bag3) == CraftingResult.Success && gold.Gold == 200, "Making feed spends no gold");
        for (int day = 1; day <= 5; day++)
        {
            Check(animals.Feed(bag3) == AnimalResult.Success && bag3.GetCount("animal_feed") == 10 - day * 2, "Two birds use two homemade feed per day");
            Check(animals.Feed(bag3) == AnimalResult.AlreadyFed, "Repeated feed does not spend supplies again"); animals.NextDay(day + 1); farm.AdvanceDay();
        }
        Check(animals.Eggs == 10 && animals.Collect(bag3) == AnimalResult.Success && bag3.GetCount("egg") == 10 && gold.Gold == 200, "One corn feeds two birds five days and yields ten eggs");
        Check(animals.Feed(bag3) == AnimalResult.MissingFeed && animals.Unfed == 2, "Consumed supplies require the next batch");
        Console.WriteLine("PASS: " + checks + " feed crafting/capacity/atomic/storage/save/husbandry assertions");
    }
}
