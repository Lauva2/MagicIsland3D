using System;
using System.Linq;
using MagicIsland.Interaction;
using MagicIsland.Inventory;
using MagicIsland.Farming;
using MagicIsland.Saving;
class VerifyRelationships
{
    static int count;
    static void Check(bool ok, string message) { count++; if (!ok) throw new Exception(message); }
    static void Reject(Action action) { bool rejected = false; try { action(); } catch (ArgumentException) { rejected = true; } Check(rejected, "Invalid relationship rejected"); }
    static readonly ItemDefinition[] items = { new ItemDefinition("soup", "Soup", ItemType.Food, 5), new ItemDefinition("corn_food", "Corn Food", ItemType.Food, 5), new ItemDefinition("turnip", "Turnip", ItemType.Crop, 5), new ItemDefinition("wood", "Wood", ItemType.Material, 5), new ItemDefinition("seed", "Seed", ItemType.Seed, 5) };
    static readonly RelationshipDefinition[] rules = { new RelationshipDefinition("npc", "soup") };
    static void Main()
    {
        foreach (var item in items) for (int points = 0; points <= 100; points++)
        {
            var npc = new NPCState(new[] { "npc" }); npc.MarkMet("npc"); var state = new RelationshipState(rules, npc, 10);
            state.Restore(new[] { new SavedRelationship { Id = "npc", Points = points, LastGiftDay = points == 0 ? 0 : 9 } });
            var bag = new InventoryState(2, items); bag.TryAdd(item.Id, 6); int gain = rules[0].Gain(item); bool success = gain > 0 && points < 100;
            Check((state.Give("npc", 1, bag) == GiftResult.Success) == success, "Gift type and cap boundary");
            Check(state.Points("npc") == points + (success ? Math.Min(100 - points, gain) : 0) && bag.Slots[0].Quantity == 5 && bag.Slots[1].Quantity == (success ? 0 : 1), "Selected stack and clamped gain");
            Check(state.LastGiftDay("npc") == (success ? 10 : points == 0 ? 0 : 9), "Day commits with quantity/points");
            if (success) Check(state.Give("npc", 0, bag) == GiftResult.AlreadyGiven && bag.GetCount(item.Id) == 5, "Daily limit no deduction");
        }
        var resident = new NPCState(new[] { "npc" }); var relationship = new RelationshipState(rules, resident, 1); var inventory = new InventoryState(3, items); inventory.TryAdd("soup", 3);
        Check(relationship.Give("npc", 0, inventory) == GiftResult.NotMet && inventory.GetCount("soup") == 3, "Must meet first"); resident.MarkMet("npc"); int notifications = 0;
        Action observer = () => { notifications++; Check(relationship.Points("npc") == 20 && relationship.LastGiftDay("npc") == 1 && inventory.GetCount("soup") == 2, "Observers see complete commit"); Check(relationship.Give("npc", 0, inventory) == GiftResult.Busy, "Gift reentry blocked"); };
        inventory.Changed += observer; Check(relationship.Give("npc", 0, inventory) == GiftResult.Success && notifications == 1 && relationship.LastFavorite && relationship.LastGain == 20, "Favorite one-time commit"); inventory.Changed -= observer;
        relationship.NextDay(2); Check(relationship.Give("npc", 0, inventory) == GiftResult.Success && relationship.Points("npc") == 40 && rules[0].Rank(40) == 1, "Next total day allows gift and familiar rank");
        Check(rules[0].Rank(29) == 0 && rules[0].Rank(30) == 1 && rules[0].Rank(69) == 1 && rules[0].Rank(70) == 2 && rules[0].Rank(100) == 2, "Rank boundaries");
        var snapshot = relationship.Capture(); snapshot[0].Points = 0; Check(relationship.Points("npc") == 40, "Capture independent");
        foreach (var invalid in new[] { new SavedRelationship { Id = "unknown", Points = 40, LastGiftDay = 2 }, new SavedRelationship { Id = "npc", Points = -1 }, new SavedRelationship { Id = "npc", Points = 101, LastGiftDay = 2 }, new SavedRelationship { Id = "npc", Points = 0, LastGiftDay = 1 }, new SavedRelationship { Id = "npc", Points = 20 }, new SavedRelationship { Id = "npc", Points = 20, LastGiftDay = 3 } })
        { Reject(() => relationship.Restore(new[] { invalid })); Check(relationship.Points("npc") == 40 && relationship.LastGiftDay("npc") == 2, "Restore all-or-nothing"); }
        Reject(() => relationship.Restore(null)); Reject(() => relationship.Restore(new SavedRelationship[0])); Reject(() => relationship.NextDay(4));
        var unmet = new RelationshipState(rules, new NPCState(new[] { "npc" }), 2); Reject(() => unmet.Restore(relationship.Capture()));
        Check(relationship.Give("missing", 0, inventory) == GiftResult.UnknownNpc, "Unknown NPC harmless"); relationship.NextDay(3); Check(relationship.Give("npc", 99, inventory) == GiftResult.InvalidItem, "Unknown slot harmless");
        var save = SaveData.Capture(new FarmState(2, 3), inventory, 0, 0, 0, 123, npcs: resident, relationships: relationship); save.Day = 3;
        save.Validate(2, 3, inventory.CreateEmpty(3), npcIds: new[] { "npc" }, relationships: rules); Check(save.Version == SaveData.CurrentVersion && save.Relationships[0].Points == 40, "Schema13 captures gifts");
        save.Version = 12; save.Relationships = null; save.Upgrade(999, npcIds: new[] { "npc" }, relationships: rules); save.Validate(2, 3, inventory.CreateEmpty(3), npcIds: new[] { "npc" }, relationships: rules);
        Check(save.NPCs[0].Met && save.Relationships[0].Points == 0 && save.Relationships[0].LastGiftDay == 0 && save.Gold == 123 && save.Slots[0].Quantity == 1, "v12 preserves meeting/inventory and initializes relationships");
        var dialogue = new DialogueDefinition("npc", new[] { "Intro" }, new[] { "Repeat" }, new[] { "S", "S", "A", "W" }, new[] { "Sun", "Rain" });
        Check(dialogue.Lines(false, MagicIsland.CalendarSystem.Season.Spring, MagicIsland.WeatherSystem.WeatherKind.Sunny, "Friend")[0] == "Intro"
            && dialogue.Lines(true, MagicIsland.CalendarSystem.Season.Spring, MagicIsland.WeatherSystem.WeatherKind.Sunny, "Friend")[0] == "Friend", "Relationship greeting only replaces repeat dialogue");
        Console.WriteLine("PASS: " + count + " relationship/gift/save assertions");
    }
}
