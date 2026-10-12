using System;
using System.Linq;
using MagicIsland.Inventory;
using MagicIsland.Stamina;
using MagicIsland.Interaction;
using MagicIsland.Farming;
using MagicIsland.Saving;
class VerifyFishCooking
{
    static int count;
    static void Check(bool ok, string reason) { count++; if (!ok) throw new Exception(reason); }
    static readonly ItemDefinition[] items = {
        new ItemDefinition("sardine", "Sardine", ItemType.Fish, 5), new ItemDefinition("sea_bass", "Bass", ItemType.Fish, 5),
        new ItemDefinition("turnip", "Turnip", ItemType.Crop, 5), new ItemDefinition("tomato", "Tomato", ItemType.Crop, 5),
        new ItemDefinition("grilled_sardine", "Grilled", ItemType.Food, 5), new ItemDefinition("bass_vegetable_soup", "Soup", ItemType.Food, 5), new ItemDefinition("wood", "Wood", ItemType.Material, 5) };
    static readonly CookingDefinition[] recipes = {
        new CookingDefinition("grilled_sardine", "sardine", 1, "grilled_sardine", 1, 35),
        new CookingDefinition("bass_vegetable_soup", new[] { new IngredientCost("sea_bass", 1), new IngredientCost("turnip", 1), new IngredientCost("tomato", 1) }, "bass_vegetable_soup", 1, 50) };
    static string Snap(InventoryState bag) => string.Join("|", bag.Slots.Select(s => s.ItemId + ":" + s.Quantity));
    static void Main()
    {
        for (int stock = 0; stock <= 5; stock++) for (int output = 0; output <= 5; output++)
        {
            var bag = new InventoryState(2, items); if (stock > 0) bag.TryAdd("sardine", stock); if (output > 0) bag.TryAdd("grilled_sardine", output); string before = Snap(bag);
            bool success = stock > 0 && (output < 5 || stock == 1); var cooking = new CookingState(recipes);
            Check((cooking.Cook(0, bag) == CookingResult.Success) == success, "Single fish ingredient post-consumption capacity");
            Check(success ? bag.GetCount("sardine") == stock - 1 && bag.GetCount("grilled_sardine") == output + 1 : Snap(bag) == before, "Grilled fish atomic input/output");
        }
        for (int capacity = 3; capacity <= 5; capacity++) for (int bass = 0; bass <= 3; bass++) for (int turnip = 0; turnip <= 3; turnip++) for (int tomato = 0; tomato <= 3; tomato++) for (int fill = 0; fill < 2; fill++)
        {
            var bag = new InventoryState(capacity, items); if (bass > 0) bag.TryAdd("sea_bass", bass); if (turnip > 0) bag.TryAdd("turnip", turnip); if (tomato > 0) bag.TryAdd("tomato", tomato);
            if (fill == 1) while (bag.CanAdd("wood", 5)) bag.TryAdd("wood", 5);
            bool success = bass > 0 && turnip > 0 && tomato > 0 && (bag.Slots.Any(s => s.Empty) || bass == 1 || turnip == 1 || tomato == 1);
            var cooking = new CookingState(recipes); string before = Snap(bag); int changes = 0;
            bag.Changed += () => { changes++; Check(cooking.Cook(1, bag) == CookingResult.Busy, "Reentrant fish cooking blocked"); };
            Check((cooking.Cook(1, bag) == CookingResult.Success) == success, "All mixed fish/crop/space combinations");
            Check(success ? bag.GetCount("sea_bass") == bass - 1 && bag.GetCount("turnip") == turnip - 1 && bag.GetCount("tomato") == tomato - 1 && bag.GetCount("bass_vegetable_soup") == 1 : Snap(bag) == before, "No partial mixed ingredient loss");
            Check(changes == (success ? 1 : 0), "Craft notifies once");
        }
        foreach (var recipe in recipes) for (int current = 0; current <= 100; current++)
        {
            var bag = new InventoryState(2, items); bag.TryAdd(recipe.Food, 6); var stamina = new StaminaState(100); stamina.Restore(current); var food = new FoodState(recipes);
            bool success = current < 100; Check((food.Use(1, bag, stamina) == FoodResult.Success) == success, "Food stamina boundary");
            Check(stamina.Current == Math.Min(100, current + (success ? recipe.Recovery : 0)) && bag.Slots[0].Quantity == 5 && bag.Slots[1].Quantity == (success ? 0 : 1), "35/50 recovery and selected stack");
        }
        var energy = new StaminaState(100); energy.Restore(50); var raw = new InventoryState(2, items); raw.TryAdd("sardine", 1);
        Check(new FoodState(recipes).Use(0, raw, energy) == FoodResult.InvalidFood && raw.GetCount("sardine") == 1 && energy.Current == 50, "Raw fish remains inedible");
        var npc = new NPCState(new[] { "npc" }); npc.MarkMet("npc"); var relationRules = new[] { new RelationshipDefinition("npc", "turnip_soup") }; var relationship = new RelationshipState(relationRules, npc, 1);
        var gifts = new InventoryState(3, items); gifts.TryAdd("grilled_sardine", 6); gifts.TryAdd("bass_vegetable_soup", 1);
        Check(relationship.Give("npc", 1, gifts) == GiftResult.Success && relationship.Points("npc") == 10 && gifts.Slots[0].Quantity == 5 && gifts.Slots[1].Empty, "Grilled fish gift normal +10 selected slot");
        Check(relationship.Give("npc", 2, gifts) == GiftResult.AlreadyGiven && gifts.GetCount("bass_vegetable_soup") == 1, "Daily limit shared with other recipes");
        relationship.NextDay(2); Check(relationship.Give("npc", 2, gifts) == GiftResult.Success && relationship.Points("npc") == 20 && gifts.GetCount("bass_vegetable_soup") == 0, "Fish soup gift normal +10");
        relationship.NextDay(3); gifts.TryAdd("sardine", 1); Check(relationship.Give("npc", 1, gifts) == GiftResult.InvalidItem && gifts.GetCount("sardine") == 1, "Raw fish not a gift");
        var storage = new StorageState(2, items, gifts); Check(storage.Transfer(true, 0, 5) == StorageResult.Success && storage.Contents.GetCount("grilled_sardine") == 5, "Fish food stacks transfer to storage");
        var farm = new FarmState(2, 3); farm.AdvanceDay(); farm.AdvanceDay();
        var save = SaveData.Capture(farm, gifts, 0, 0, 0, 100, stamina: 85, storage: storage, npcs: npc, relationships: relationship);
        save.Validate(2, 3, gifts.CreateEmpty(3), storageSlots: 2, npcIds: new[] { "npc" }, relationships: relationRules);
        Check(save.Version == SaveData.CurrentVersion && save.Stamina == 85 && save.Relationships[0].Points == 20 && save.Storage[0].ItemId == "grilled_sardine", "Current schema saves food/storage/stamina/gifts together");
        save.Upgrade(999, storageSlots: 2, npcIds: new[] { "npc" }, relationships: relationRules); Check(save.Gold == 100 && save.Storage[0].Quantity == 5, "Upgrade preserves existing inventory and food");
        Console.WriteLine("PASS: " + count + " fish cooking/food/gift/storage/save assertions");
    }
}
