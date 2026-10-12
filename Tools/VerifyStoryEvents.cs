using System;
using System.Linq;
using MagicIsland.Interaction;
using MagicIsland.Inventory;
using MagicIsland.Farming;
using MagicIsland.Saving;
using MagicIsland.Stamina;
class VerifyStoryEvents
{
    static int count;
    static void Check(bool ok, string message) { count++; if (!ok) throw new Exception(message); }
    static void Reject(Action action) { try { action(); } catch (ArgumentException) { count++; return; } throw new Exception("Invalid data accepted"); }
    static readonly RelationshipDefinition[] relations = { new RelationshipDefinition("npc", "soup") };
    static readonly StoryEventDefinition[] events = { new StoryEventDefinition("history", "npc", 30), new StoryEventDefinition("recipe", "npc", 70, "history", "soup") };
    static readonly ItemDefinition[] items = { new ItemDefinition("turnip", "Turnip", ItemType.Crop, 5), new ItemDefinition("tomato", "Tomato", ItemType.Crop, 5), new ItemDefinition("corn", "Corn", ItemType.Crop, 5), new ItemDefinition("soup", "Soup", ItemType.Food, 5), new ItemDefinition("wood", "Wood", ItemType.Material, 5) };
    static readonly CookingDefinition recipe = new CookingDefinition("soup", new[] { new IngredientCost("turnip", 1), new IngredientCost("tomato", 1), new IngredientCost("corn", 1) }, "soup", 1, 50, "recipe");
    static string Snap(InventoryState bag) => string.Join("|", bag.Slots.Select(s => s.ItemId + ":" + s.Quantity));
    static RelationshipState Relation(int points, bool met = true)
    {
        var npc = new NPCState(new[] { "npc" }); if (met) npc.MarkMet("npc"); var state = new RelationshipState(relations, npc, 1);
        if (points > 0) state.Restore(new[] { new SavedRelationship { Id = "npc", Points = points, LastGiftDay = 1 } }); return state;
    }
    static void Main()
    {
        for (int points = 0; points <= 100; points++)
        {
            var state = new StoryEventState(events, Relation(points)); Check(state.Next("npc").Id == "history" && !state.RecipeUnlocked("soup"), "Events initially pending");
            Check(state.Complete("recipe") == false && state.Available("history") == (points >= 30), "Prerequisite and threshold");
            Check(state.Complete("history") == (points >= 30) && !state.RecipeUnlocked("soup"), "History grants no recipe");
            Check(state.Complete("recipe") == (points >= 70) && state.RecipeUnlocked("soup") == (points >= 70), "Recipe unlock requires event completion");
            var restored = new StoryEventState(events, Relation(points)); restored.Restore(state.Capture()); Check(restored.RecipeUnlocked("soup") == (points >= 70) && !state.Complete("unknown"), "Snapshot round trip and unknown event");
            if (points >= 70) Check(!restored.Complete("recipe") && restored.Next("npc") == null, "Completed reward never repeats");
        }
        Check(!new StoryEventState(events, Relation(0, false)).Available("history"), "Unmet cannot access event");
        var relation = Relation(70); var story = new StoryEventState(events, relation); var bag = new InventoryState(3, items); foreach (var id in new[] { "turnip", "tomato", "corn" }) bag.TryAdd(id, 1);
        var cooking = new CookingState(new[] { recipe }, story.Completed); string before = Snap(bag);
        Check(cooking.Cook(0, bag) == CookingResult.Locked && Snap(bag) == before, "Relationship score alone does not unlock recipe");
        story.Complete("history"); Check(!story.RecipeUnlocked(null) && !story.RecipeUnlocked("unknown"), "History cannot unlock unspecified recipe"); int observed = 0;
        story.Changed += () => { observed++; Check(story.Completed("recipe") && story.RecipeUnlocked("soup"), "Observer sees completion and unlock together"); Check(!story.Complete("recipe"), "Event reentry blocked"); };
        Check(story.Complete("recipe") && observed == 1, "Recipe grant commits once");
        Check(cooking.Cook(0, bag) == CookingResult.Success && bag.GetCount("soup") == 1 && bag.GetCount("turnip") == 0 && bag.GetCount("tomato") == 0 && bag.GetCount("corn") == 0, "Unlock enables multi ingredient cooking");
        var snapshot = story.Capture(); snapshot[0].Completed = false; Check(story.Completed("history"), "Deep story snapshot");
        string stable = string.Join("|", story.Capture().Select(s => s.Completed));
        Reject(() => story.Restore(snapshot)); Reject(() => story.Restore(new[] { new SavedStoryEvent { Id = "history" }, new SavedStoryEvent { Id = "unknown" } }));
        Reject(() => story.Restore(new[] { new SavedStoryEvent { Id = "history" } })); Reject(() => story.Restore(new SavedStoryEvent[] { null, null }));
        Check(string.Join("|", story.Capture().Select(s => s.Completed)) == stable, "Invalid restore atomic");
        Reject(() => new StoryEventState(events, Relation(29)).Restore(story.Capture()));
        Reject(() => new StoryEventState(events.Reverse(), relation)); Reject(() => new StoryEventState(new[] { events[0], events[0] }, relation));
        Reject(() => new CookingDefinition("bad", new[] { new IngredientCost("turnip", 1), new IngredientCost("turnip", 2) }, "soup", 1, 50));
        for (int capacity = 3; capacity <= 5; capacity++) for (int a = 0; a <= 3; a++) for (int b = 0; b <= 3; b++) for (int c = 0; c <= 3; c++) for (int fill = 0; fill < 2; fill++)
        {
            var inventory = new InventoryState(capacity, items); if (a > 0) inventory.TryAdd("turnip", a); if (b > 0) inventory.TryAdd("tomato", b); if (c > 0) inventory.TryAdd("corn", c);
            if (fill == 1) { while (inventory.CanAdd("wood", 5)) inventory.TryAdd("wood", 5); }
            string prior = Snap(inventory); int changes = 0; var cook = new CookingState(new[] { recipe }, story.Completed);
            bool sufficient = a > 0 && b > 0 && c > 0;
            bool fits = inventory.Slots.Any(s => s.Empty) || a == 1 || b == 1 || c == 1; bool success = sufficient && fits;
            inventory.Changed += () => { changes++; Check(cook.Cook(0, inventory) == CookingResult.Busy, "Multi ingredient reentry blocked"); };
            Check((cook.Cook(0, inventory) == CookingResult.Success) == success, "All ingredient/capacity combinations");
            Check(success ? inventory.GetCount("turnip") == a - 1 && inventory.GetCount("tomato") == b - 1 && inventory.GetCount("corn") == c - 1 && inventory.GetCount("soup") == 1 : Snap(inventory) == prior, "No partial consumption");
            Check(changes == (success ? 1 : 0), "One notification after entire craft");
        }
        var outputStack = new InventoryState(4, items); foreach (var id in new[] { "turnip", "tomato", "corn", "soup" }) outputStack.TryAdd(id, 2);
        Check(new CookingState(new[] { recipe }, story.Completed).Cook(0, outputStack) == CookingResult.Success && outputStack.GetCount("soup") == 3, "Existing output stack in full bag");
        var energy = new StaminaState(100); energy.Restore(60); var food = new FoodState(new[] { recipe });
        Check(food.Use(3, outputStack, energy) == FoodResult.Success && energy.Current == 100 && food.LastRecovery == 40, "Soup recovery 50 clamped");
        var npcState = new NPCState(new[] { "npc" }); npcState.MarkMet("npc");
        var save = SaveData.Capture(new FarmState(2, 3), bag, 0, 0, 0, 123, npcs: npcState, relationships: relation, stories: story);
        save.Validate(2, 3, bag.CreateEmpty(3), npcIds: new[] { "npc" }, relationships: relations, stories: events);
        Check(save.Version == SaveData.CurrentVersion && save.StoryEvents.All(s => s.Completed), "Schema14 captures events and derived recipe unlock");
        save.StoryEvents[1].Id = "unknown"; Reject(() => save.Validate(2, 3, bag.CreateEmpty(3), npcIds: new[] { "npc" }, relationships: relations, stories: events));
        save.Version = 13; save.StoryEvents = null; save.Upgrade(999, npcIds: new[] { "npc" }, relationships: relations, stories: events);
        save.Validate(2, 3, bag.CreateEmpty(3), npcIds: new[] { "npc" }, relationships: relations, stories: events);
        Check(save.StoryEvents.All(s => !s.Completed) && save.Relationships[0].Points == 70 && save.NPCs[0].Met && save.Gold == 123 && save.Slots[0].ItemId == "soup", "v13 preserves world and defers reward until event");
        var talk = new DialogueState(); talk.BeginLines("npc", new[] { "one", "two" }); talk.Advance(); talk.Clear();
        Check(!talk.Active && !talk.Advance(), "Cancel last displayed line does not finish"); talk.BeginLines("npc", new[] { "one", "two" });
        Check(!talk.Advance() && talk.Advance() && !talk.Advance(), "Advance past final line completes once");
        Console.WriteLine("PASS: " + count + " story/event/unlock/multi-ingredient/save assertions");
    }
}
