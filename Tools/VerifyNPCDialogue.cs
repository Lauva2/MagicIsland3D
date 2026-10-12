using System;
using System.Linq;
using MagicIsland.Interaction;
using MagicIsland.CalendarSystem;
using MagicIsland.WeatherSystem;
using MagicIsland.Inventory;
using MagicIsland.Farming;
using MagicIsland.Saving;
class VerifyNPCDialogue
{
    static int count;
    static void Check(bool value, string message) { count++; if (!value) throw new Exception(message); }
    static void Reject(Action action) { bool rejected = false; try { action(); } catch (ArgumentException) { rejected = true; } Check(rejected, "Invalid data rejected"); }
    static DialogueDefinition Definition() => new DialogueDefinition("steward", new[] { "Welcome", "Orders", "Island" }, new[] { "Again" }, new[] { "Spring", "Summer", "Autumn", "Winter" }, new[] { "Sunny", "Rainy" });
    static void Main()
    {
        var npc = new NPCState(new[] { "steward", "visitor" }); Check(!npc.HasMet("steward") && npc.MarkMet("steward") && !npc.MarkMet("steward"), "Meeting once");
        var captured = npc.Capture(); captured[0].Met = false; Check(npc.HasMet("steward"), "NPC snapshot independent");
        var resumed = new NPCState(new[] { "steward", "visitor" }); resumed.Restore(npc.Capture()); Check(resumed.HasMet("steward") && !resumed.HasMet("visitor"), "Exact meeting restore");
        var bad = npc.Capture(); bad[0].Met = false; bad[1].Id = "unknown"; Reject(() => resumed.Restore(bad)); Check(resumed.HasMet("steward"), "Restore validates before mutation");
        Reject(() => resumed.Restore(null)); Reject(() => resumed.Restore(new SavedNPC[0])); Reject(() => resumed.Restore(npc.Capture().Reverse().ToArray()));
        Reject(() => new NPCState(new[] { "a", "a" })); Reject(() => new NPCState(new[] { " " })); Reject(() => npc.HasMet("unknown")); Check(!npc.MarkMet("unknown"), "Unknown NPC free");
        var definition = Definition();
        foreach (bool met in new[] { false, true }) foreach (Season season in Enum.GetValues(typeof(Season))) foreach (WeatherKind weather in Enum.GetValues(typeof(WeatherKind)))
        {
            var session = new DialogueState(); Check(session.Begin(definition, met, season, weather), "Begin session");
            Check(session.Lines.Count == (met ? 3 : 5) && session.Lines[session.Lines.Count - 2] == season.ToString() && session.Lines.Last() == weather.ToString(), "Context selection");
            Check(!session.Begin(definition, !met, Season.Winter, WeatherKind.Rainy) && session.Index == 0, "No duplicate session or context reroll");
            string[] lines = definition.Lines(met, season, weather); lines[0] = "mutated"; Check(session.Text != "mutated", "Independent dialogue snapshot");
            for (int i = 0; i < session.Lines.Count - 1; i++) { Check(session.Index == i && !session.Advance() && session.Active, "Advance exactly one sentence"); }
            Check(session.LastLine && session.Advance() && !session.Active && session.NpcId == "steward" && !session.Advance(), "Completion fires once");
            session.Clear(); Check(session.NpcId == null && session.Lines.Count == 0 && !session.Active, "Session clear");
        }
        for (int cancelAfter = 0; cancelAfter < 5; cancelAfter++)
        {
            var session = new DialogueState(); var state = new NPCState(new[] { "steward" }); session.Begin(definition, state.HasMet("steward"), Season.Spring, WeatherKind.Sunny);
            for (int i = 0; i < cancelAfter; i++) session.Advance(); session.Clear(); Check(!state.HasMet("steward") && !session.Advance(), "Cancel including final displayed line does not meet");
            Check(session.Begin(definition, false, Season.Spring, WeatherKind.Sunny) && session.Text == "Welcome", "Cancelled intro restarts");
        }
        Reject(() => definition.Lines(false, (Season)4, WeatherKind.Sunny)); Reject(() => definition.Lines(false, Season.Spring, (WeatherKind)2));
        Reject(() => new DialogueDefinition("x", new[] { " " }, new[] { "Repeat" }, new[] { "1", "2", "3", "4" }, new[] { "S", "R" }));
        var bag = new InventoryState(2, new[] { new ItemDefinition("seed", "Seed", ItemType.Seed, 99) }); bag.TryAdd("seed", 50);
        var save = SaveData.Capture(new FarmState(2, 3), bag, 0, 0, 0, 123, stamina: 77, npcs: npc); save.Validate(2, 3, bag.CreateEmpty(2), npcIds: new[] { "steward", "visitor" });
        Check(save.Version == SaveData.CurrentVersion && save.NPCs[0].Met && save.NPCs[1].Met == false, "Schema12 snapshot");
        save.Version = 11; save.NPCs = null; save.Upgrade(999, npcIds: new[] { "steward", "visitor" }); save.Validate(2, 3, bag.CreateEmpty(2), npcIds: new[] { "steward", "visitor" });
        Check(save.NPCs.All(s => !s.Met) && save.Gold == 123 && save.Stamina == 77 && save.Slots[0].Quantity == 50, "v11 preserves world and initializes unmet NPCs");
        Console.WriteLine("PASS: " + count + " NPC/dialogue/save assertions");
    }
}
