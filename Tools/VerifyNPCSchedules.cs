using System;
using System.Linq;
using MagicIsland.Interaction;
using MagicIsland.Inventory;
using MagicIsland.Farming;
using MagicIsland.Saving;

class VerifyNPCSchedules
{
    static int count;
    static void Check(bool ok, string name) { count++; if (!ok) throw new Exception(name); }
    static void Reject(Action action) { try { action(); } catch (ArgumentException) { count++; return; } throw new Exception("Invalid schedule accepted"); }
    static NPCScheduleDefinition Route(int farm = 5, string id = "npc") => new NPCScheduleDefinition(id, 1.4, 720, 1080,
        new[] { new SchedulePoint(Math.Min(9.3, farm * .6 + 2.7) + 1.7, .95), new SchedulePoint(11.2, .95),
            new SchedulePoint(11.2, -6.8), new SchedulePoint(9.15, -6.8) });
    static void Main()
    {
        for (int size = 1; size <= 12; size++)
        {
            var d = Route(size); Check(d.AvoidsFarm(size), "Route avoids each farm size");
            foreach (double minute in new double[] { 0, 360, 719.999, 720, 1079.999, 1080, 1440 })
            {
                var state = new NPCScheduleState(new[] { d }, minute);
                Check(state.Distance("npc") == (minute >= 720 && minute < 1080 ? d.Length : 0), "Time boundary destination");
                Check(!state.Moving("npc"), "Initial/migrated schedule stands at destination");
            }
            foreach (double seconds in new double[] { 0, .001, .1, 1, 2, 5, 10, 100 })
            {
                var state = new NPCScheduleState(new[] { d }); state.Tick(720, seconds);
                Check(Math.Abs(state.Distance("npc") - Math.Min(d.Length, 1.4 * seconds)) < .000001, "Elapsed real-time speed");
                var saved = state.Capture(); var restored = new NPCScheduleState(new[] { d }, 720); restored.Restore(saved);
                Check(SchedulePoint.Distance(restored.Position("npc"), state.Position("npc")) < .000001, "Travel restores precisely");
                saved[0].Distance = 0; Check(restored.Distance("npc") == state.Distance("npc"), "Deep snapshot");
                double prior = state.Distance("npc"); state.Tick(720, 100, true); state.Tick(720, 100, focused: id => true);
                Check(state.Distance("npc") == prior, "Modal and focused pause");
                state.Tick(1080, .1); Check(Math.Abs(state.Distance("npc") - Math.Max(0, prior - .14)) < .000001, "Evening reverses unfinished route");
                state.Tick(1080, 100); Check(state.Distance("npc") == 0 && !state.Moving("npc"), "Home arrival");
                state.Reset(900); state.Reset(360); Check(state.Distance("npc") == 0 && state.Minute == 360, "Sleep reset");
            }
        }
        var route = Route(); var travel = new NPCScheduleState(new[] { route }); int legs = 0;
        travel.Tick(720, 100, canMove: (id, a, b) => { legs++; Check(a.X == b.X || a.Z == b.Z, "Large frame cannot cut across corner"); return true; });
        Check(legs == 3 && travel.Distance("npc") == route.Length, "Every corridor leg tested");
        travel.Reset(360); travel.Tick(720, 1, canMove: (id, a, b) => false); Check(travel.Distance("npc") == 0 && travel.Moving("npc"), "Blocked route waits without teleport");
        travel.Tick(720, 1); Check(Math.Abs(travel.Distance("npc") - 1.4) < .000001, "Unblocked route resumes");
        double stable = travel.Distance("npc");
        foreach (double bad in new[] { -1, route.Length + .001, double.NaN, double.PositiveInfinity }) Reject(() => travel.Restore(new[] { new SavedNPCSchedule { Id = "npc", Distance = bad } }));
        Reject(() => travel.Restore(null)); Reject(() => travel.Restore(new SavedNPCSchedule[] { null })); Reject(() => travel.Restore(new[] { new SavedNPCSchedule { Id = "other" } }));
        Check(travel.Distance("npc") == stable, "Invalid restore is atomic");
        Reject(() => travel.Tick(720, -1)); Reject(() => travel.Tick(double.NaN, 1)); Reject(() => travel.Reset(1441));
        Reject(() => new NPCScheduleState(new[] { route, route })); Reject(() => new NPCScheduleState(new NPCScheduleDefinition[0]));
        Reject(() => new NPCScheduleDefinition("x", double.NaN, 720, 1080, route.Points));
        Reject(() => new NPCScheduleDefinition("x", 1, 1080, 720, route.Points));
        var pair = new NPCScheduleState(new[] { route, Route(5, "other") }); pair.Tick(720, 1);
        var invalid = pair.Capture(); invalid[0].Distance = 0; invalid[1].Distance = double.NaN; Reject(() => pair.Restore(invalid));
        Check(pair.Distance("npc") > 0, "Multi-NPC restore commits all or none"); Reject(() => pair.Restore(pair.Capture().Reverse().ToArray()));
        var bag = new InventoryState(2, new[] { new ItemDefinition("fish", "Fish", ItemType.Fish, 99) }); bag.TryAdd("fish", 3);
        var npc = new NPCState(new[] { "npc" }); npc.MarkMet("npc");
        var rules = new[] { new RelationshipDefinition("npc", "soup") }; var relationship = new RelationshipState(rules, npc, 1);
        relationship.Restore(new[] { new SavedRelationship { Id = "npc", Points = 70, LastGiftDay = 1 } });
        var events = new[] { new StoryEventDefinition("history", "npc", 30) }; var story = new StoryEventState(events, relationship); story.Complete("history");
        var save = SaveData.Capture(new FarmState(5, 3), bag, 0, 0, 0, 123, 720, 85, npcs: npc, relationships: relationship, stories: story, schedules: travel);
        save.Validate(5, 3, bag.CreateEmpty(2), npcIds: new[] { "npc" }, relationships: rules, stories: events, schedules: new[] { route });
        Check(save.Version == SaveData.CurrentVersion && save.NPCSchedules[0].Distance == stable, "Schema 15 captures travel");
        foreach (double minute in new double[] { 360, 900, 1200 })
        {
            save.Version = 14; save.NPCSchedules = null; save.ClockMinute = minute;
            save.Upgrade(999, npcIds: new[] { "npc" }, relationships: rules, stories: events, schedules: new[] { route });
            save.Validate(5, 3, bag.CreateEmpty(2), npcIds: new[] { "npc" }, relationships: rules, stories: events, schedules: new[] { route });
            Check(save.NPCSchedules[0].Distance == route.Target(minute) && save.Gold == 123 && save.Stamina == 85 && save.Slots[0].Quantity == 3
                && save.NPCs[0].Met && save.Relationships[0].LastGiftDay == 1 && save.StoryEvents[0].Completed, "v14 migration preserves existing progress");
        }
        Console.WriteLine("PASS: " + count + " NPC schedule/save assertions");
    }
}
