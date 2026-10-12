using System;
using System.Linq;
using MagicIsland.Inventory;
using MagicIsland.Stamina;
using MagicIsland.Farming;
using MagicIsland.Saving;
class VerifyCooking
{
    static int count;
    static readonly ItemDefinition[] items = { new ItemDefinition("turnip", "Turnip", ItemType.Crop, 5), new ItemDefinition("corn", "Corn", ItemType.Crop, 5), new ItemDefinition("soup", "Soup", ItemType.Food, 5), new ItemDefinition("roasted", "Roasted", ItemType.Food, 5) };
    static readonly CookingDefinition[] recipes = { new CookingDefinition("soup", "turnip", 2, "soup", 1, 30), new CookingDefinition("roasted", "corn", 1, "roasted", 1, 20) };
    static void Check(bool ok, string reason) { count++; if (!ok) throw new Exception(reason); }
    static string Snap(InventoryState bag) => string.Join("|", bag.Slots.Select(s => s.ItemId + ":" + s.Quantity));
    static void Main()
    {
        for (int recipeIndex = 0; recipeIndex < 2; recipeIndex++) for (int stock = 0; stock <= 5; stock++) for (int output = 0; output <= 5; output++)
        {
            var recipe = recipes[recipeIndex]; var bag = new InventoryState(2, items); if (stock > 0) bag.TryAdd(recipe.Ingredient, stock); if (output > 0) bag.TryAdd(recipe.Food, output);
            var cooking = new CookingState(recipes); string before = Snap(bag); bool available = stock >= recipe.Cost && (output < 5 || stock == recipe.Cost);
            Check((cooking.Cook(recipeIndex, bag) == CookingResult.Success) == available, "Capacity after ingredient consumption");
            Check(available ? bag.GetCount(recipe.Ingredient) == stock - recipe.Cost && bag.GetCount(recipe.Food) == output + 1 : Snap(bag) == before, "Atomic ingredient/output");
        }
        var inventory = new InventoryState(2, items); inventory.TryAdd("turnip", 3); inventory.TryAdd("corn", 5); var state = new CookingState(recipes);
        Check(state.Cook(0, inventory) == CookingResult.BagFull && inventory.GetCount("turnip") == 3, "Full bag free");
        inventory.TryRemove("turnip", 1); int events = 0;
        inventory.Changed += () => { events++; Check(inventory.GetCount("turnip") == 0 && inventory.GetCount("soup") == 1, "Observer sees full cooking commit"); Check(state.Cook(0, inventory) == CookingResult.Busy, "Cooking event reentry blocked"); };
        Check(state.Cook(0, inventory) == CookingResult.Success && events == 1, "Last ingredients free output slot and notify once");
        Check(state.Cook(-1, inventory) == CookingResult.InvalidRecipe, "Invalid recipe");
        foreach (var recipe in recipes) for (int current = 0; current <= 100; current++)
        {
            var bag = new InventoryState(2, items); bag.TryAdd(recipe.Food, 6); var stamina = new StaminaState(100); stamina.Restore(current); var food = new FoodState(recipes);
            bool success = current < 100; Check((food.Use(1, bag, stamina) == FoodResult.Success) == success, "Food/full stamina boundary");
            Check(stamina.Current == Math.Min(100, current + (success ? recipe.Recovery : 0)) && bag.Slots[0].Quantity == 5 && bag.Slots[1].Quantity == (success ? 0 : 1), "Selected food slot and clamped recovery");
        }
        var eatingBag = new InventoryState(2, items); eatingBag.TryAdd("soup", 2); var energy = new StaminaState(100); energy.Restore(90); var foods = new FoodState(recipes); int notices = 0;
        eatingBag.Changed += () => { notices++; Check(energy.Current == 100 && eatingBag.GetCount("soup") == 1, "Food atomic stamina/quantity"); Check(foods.Use(0, eatingBag, energy) == FoodResult.Busy, "Food reentry blocked"); };
        Check(foods.Use(0, eatingBag, energy) == FoodResult.Success && notices == 1 && foods.LastRecovery == 10, "Actual recovery reported");
        Check(foods.Use(99, eatingBag, energy) == FoodResult.InvalidFood && foods.Use(1, eatingBag, energy) == FoodResult.InvalidFood, "Invalid food selection free");
        var huge = new StaminaState(int.MaxValue); huge.Restore(int.MaxValue - 1); Check(huge.TryRecover(int.MaxValue) && huge.Current == int.MaxValue, "Recovery cannot overflow");
        var storageBag = new InventoryState(2, items); storageBag.TryAdd("soup", 1);
        var storage = new StorageState(2, items, storageBag); storage.Transfer(true, 0, 1); Check(foods.Use(0, storageBag, energy) == FoodResult.InvalidFood, "Stored food must be withdrawn");
        var save = SaveData.Capture(new FarmState(2, 3), storageBag, 0, 0, 0, 100, stamina: energy.Current, storage: storage);
        save.Validate(2, 3, storageBag.CreateEmpty(2), storageSlots: 2); Check(save.Version == SaveData.CurrentVersion && save.Storage[0].ItemId == "soup" && save.Stamina == 100, "Food fits existing schema11");
        save.Upgrade(999, storageSlots: 2); Check(save.Storage[0].Quantity == 1 && save.Gold == 100, "Current save upgrade preserves food");
        Console.WriteLine("PASS: " + count + " cooking/food/save assertions");
    }
}
