using System;
using System.Linq;
using MagicIsland.Animals;
using MagicIsland.Inventory;
using MagicIsland.Economy;
using MagicIsland.Farming;
using MagicIsland.Saving;
class VerifyAnimals
{
    static int checks;
    static void Check(bool ok, string label) { checks++; if (!ok) throw new Exception(label); }
    static void Reject(Action action) { try { action(); } catch (ArgumentException) { checks++; return; } catch (InvalidOperationException) { checks++; return; } throw new Exception("Invalid animal action/state accepted"); }
    static AnimalDefinition Definition = new AnimalDefinition(15, 5, 200, 50, 10, 25);
    static ItemDefinition[] Items = { new ItemDefinition("wood", "Wood", ItemType.Material, 99), new ItemDefinition("stone", "Stone", ItemType.Material, 99), new ItemDefinition("animal_feed", "Feed", ItemType.Material, 99), new ItemDefinition("egg", "Egg", ItemType.AnimalProduct, 99) };
    static InventoryState Bag(int wood = 15, int stone = 5) { var bag = new InventoryState(4, Items); if (wood > 0) bag.TryAdd("wood", wood); if (stone > 0) bag.TryAdd("stone", stone); return bag; }
    static void Main()
    {
        Check((int)ItemType.AnimalProduct == 8 && (int)ItemType.Furniture == 7, "Item enum appended without changing old values");
        for (int w = 14; w <= 16; w++) for (int s = 4; s <= 6; s++) for (int g = 199; g <= 201; g++)
        {
            var state = new AnimalState(Definition); var bag = Bag(w, s); var gold = new EconomyState(g); bool success = w >= 15 && s >= 5 && g >= 200;
            Check((state.Build(bag, gold) == AnimalResult.Success) == success, "Build checks all costs"); Check(state.Built == success && bag.GetCount("wood") == w - (success ? 15 : 0) && bag.GetCount("stone") == s - (success ? 5 : 0) && gold.Gold == g - (success ? 200 : 0), "Build commits together or costs nothing");
        }
        var animals = new AnimalState(Definition); var inventory = Bag(); var economy = new EconomyState(1000);
        Check(animals.BuyChicken(economy) == AnimalResult.NeedsCoop && economy.Gold == 1000, "Chicken requires coop");
        int notifications = 0; Action observer = () => { notifications++; Check(animals.Built && inventory.GetCount("wood") == 0 && inventory.GetCount("stone") == 0 && economy.Gold == 800, "Build observers see complete transaction"); Check(animals.Build(inventory, economy) == AnimalResult.Busy, "Build reentry refused"); Reject(() => animals.NextDay(2)); };
        inventory.Changed += observer; economy.Changed += observer; animals.Build(inventory, economy); inventory.Changed -= observer; economy.Changed -= observer; Check(notifications == 2, "Atomic notifications once");
        Check(animals.Build(inventory, economy) == AnimalResult.AlreadyBuilt && economy.Gold == 800, "Duplicate build costs nothing");
        animals.BuyChicken(economy); inventory.TryAdd("animal_feed", 5); Check(animals.Feed(inventory) == AnimalResult.Success && inventory.GetCount("animal_feed") == 4, "One feed per chicken");
        animals.BuyChicken(economy); Check(animals.Unfed == 1 && animals.Feed(inventory) == AnimalResult.Success && inventory.GetCount("animal_feed") == 3, "New chicken can feed without charging fed bird again");
        Check(animals.BuyChicken(economy) == AnimalResult.ChickenLimit && animals.Count == 2 && economy.Gold == 700, "Capacity two and no extra payment");
        Check(animals.Feed(inventory) == AnimalResult.AlreadyFed && inventory.GetCount("animal_feed") == 3, "Same-day repeat no feed loss");
        var pending = animals.Capture(); var loaded = new AnimalState(Definition); loaded.Restore(pending); pending.Chickens[0].FedDay = 0; Check(loaded.Unfed == 0, "Restore snapshot deep copy");
        loaded.NextDay(2); Check(loaded.Eggs == 2 && loaded.Unfed == 2, "Fed birds produce once next day"); Reject(() => loaded.NextDay(2)); Check(loaded.Eggs == 2, "Duplicate day cannot duplicate eggs"); loaded.NextDay(3); Check(loaded.Eggs == 2, "Unfed birds produce no eggs");
        var full = new InventoryState(1, Items); full.TryAdd("wood", 99); Check(loaded.Collect(full) == AnimalResult.BagFull && loaded.Eggs == 2, "Full bag preserves eggs");
        Check(loaded.Collect(inventory) == AnimalResult.Success && inventory.GetCount("egg") == 2 && loaded.Eggs == 0 && loaded.Collect(inventory) == AnimalResult.NoEggs, "Collect once only");
        Check(loaded.Sell(inventory, economy) == AnimalResult.Success && economy.Gold == 725 && inventory.GetCount("egg") == 1, "Egg sale exact income");
        var limit = new EconomyState(long.MaxValue); Check(loaded.Sell(inventory, limit) == AnimalResult.GoldLimit && inventory.GetCount("egg") == 1, "Gold limit preserves egg");
        Check(loaded.BuyFeed(full, economy) == AnimalResult.BagFull && economy.Gold == 725, "Full bag feed purchase charges nothing");
        Check(loaded.BuyFeed(inventory, economy) == AnimalResult.Success && inventory.GetCount("animal_feed") == 4 && economy.Gold == 715, "Feed purchase exact inventory and gold");
        loaded.Restore(new SavedAnimals { Built = true, Day = 3, Eggs = 98, Chickens = new[] { new SavedChicken { Id = 1, FedDay = 2 }, new SavedChicken { Id = 2, FedDay = 2 } } });
        Check(loaded.Feed(inventory) == AnimalResult.EggsFull && inventory.GetCount("animal_feed") == 4, "Egg capacity refuses feeding without wasting feed");
        for (int day = 1; day <= 20; day++) for (int birds = 0; birds <= 2; birds++) for (int eggs = -1; eggs <= 100; eggs++)
        {
            var saved = new SavedAnimals { Built = true, Day = day, Eggs = eggs, Chickens = Enumerable.Range(1, birds).Select(i => new SavedChicken { Id = i, FedDay = day }).ToArray() };
            bool valid = eggs >= 0 && eggs <= 99 && eggs + birds <= 99 && (eggs == 0 || birds > 0);
            Check(AnimalState.Valid(saved, day) == valid, "Save day/chicken/egg reserve boundaries");
        }
        Reject(() => loaded.Restore(new SavedAnimals { Built = false, Day = 3, Chickens = new[] { new SavedChicken { Id = 1 } } }));
        Reject(() => loaded.Restore(new SavedAnimals { Built = true, Day = 3, Chickens = new[] { new SavedChicken { Id = 1 }, new SavedChicken { Id = 1 } } }));
        Reject(() => loaded.Restore(new SavedAnimals { Built = true, Day = 3, Chickens = new[] { new SavedChicken { Id = 1, FedDay = 4 } } }));
        var last = new AnimalState(Definition, int.MaxValue - 1); last.Restore(new SavedAnimals { Built = true, Day = int.MaxValue - 1, Chickens = new[] { new SavedChicken { Id = 1 } } }); Check(last.Feed(inventory) == AnimalResult.InvalidDay && inventory.GetCount("animal_feed") == 4, "Final playable day refuses unfulfillable feeding");
        var farm = new FarmState(5, 3); farm.AdvanceDay(); farm.AdvanceDay(); var save = SaveData.Capture(farm, inventory, 0, .1f, 0, economy.Gold, animals: loaded);
        save.Validate(5, 3, inventory.CreateEmpty(4)); Check(save.Animals.Day == 3 && save.Animals.Eggs == 98 && save.Animals.Chickens.Length == 2, "Animal snapshot with full world inventory");
        save.Version = 19; save.Animals = null; save.Upgrade(999); save.Validate(5, 3, inventory.CreateEmpty(4)); Check(!save.Animals.Built && save.Animals.Chickens.Length == 0 && save.Animals.Day == 3 && save.Gold == 715 && save.Slots.Any(s => s.ItemId == "egg"), "v19 migration grants no coop/birds and preserves inventory/gold/day");
        Console.WriteLine("PASS: " + checks + " animal economy/feeding/production/save assertions");
    }
}
