using System;
using System.Linq;
using MagicIsland.Interaction;
using MagicIsland.Inventory;
using MagicIsland.Farming;
using MagicIsland.Saving;
class VerifyHousing
{
    static int checks;
    static void Check(bool ok, string name) { checks++; if (!ok) throw new Exception(name); }
    static void Reject(Action action) { try { action(); } catch (ArgumentException) { checks++; return; } catch (InvalidOperationException) { checks++; return; } throw new Exception("Invalid residence accepted"); }
    static void Main()
    {
        var state = new HousingState(); Check(!state.Inside && !state.Busy && !state.Begin(HousingState.Island), "New game on island");
        for (int i = 0; i < 100; i++)
        {
            Check(state.Begin(HousingState.Home) && state.Busy && !state.Inside, "Entry claims transition before changing area");
            Check(!state.Begin(HousingState.Home) && !state.Begin(HousingState.Island), "Duplicate and reversed transitions blocked");
            state.Abort(); Check(!state.Inside && !state.Busy && !state.Complete(), "Failed entry keeps island");
            state.Begin(HousingState.Home); Check(state.Complete() && state.Inside && !state.Busy && !state.Complete(), "Successful entry commits once");
            state.Begin(HousingState.Island); state.Abort(); Check(state.Inside && !state.Busy, "Failed exit keeps home");
            state.Begin(HousingState.Island); Check(state.Complete() && !state.Inside, "Successful exit returns island");
        }
        Reject(() => new HousingState("unknown")); Reject(() => state.Begin("unknown"));
        for (int x = -13; x <= 13; x++) for (int z = -13; z <= 13; z++)
        {
            if(Math.Abs(x)<=12&&Math.Abs(z)<=12) Check(HousingState.ValidPosition(HousingState.Island,x,.1,z),"Legacy island positions stay valid on expanded terrain");
            Check(HousingState.ValidPosition(HousingState.Home, x, .1, z) == (Math.Abs(x) <= 5.8 && Math.Abs(z) <= 4.8), "Interior local bounds");
        }
        foreach (var area in new[] { HousingState.Home, HousingState.Island }) foreach (double invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            Check(!HousingState.ValidPosition(area, invalid, 0, 0) && !HousingState.ValidPosition(area, 0, invalid, 0) && !HousingState.ValidPosition(area, 0, 0, invalid), "Non-finite positions rejected");
        Check(!HousingState.ValidPosition(null, 0, 0, 0) && !HousingState.ValidPosition("unknown", 0, 0, 0) && !HousingState.ValidPosition(HousingState.Home, 40, .1, 0), "Unknown area/world-space interior positions rejected");
        Check(HousingState.ValidPosition(HousingState.Home, 5.65, .1, -4.6) && !HousingState.ValidPosition(HousingState.Home, 5.81, .1, 0) && !HousingState.ValidPosition(HousingState.Home, 0, .1, 4.81), "Standing near room wall and exit remains saveable within limits");
        var items = new[] { new ItemDefinition("wood", "Wood", ItemType.Material, 99), new ItemDefinition("fence", "Fence", ItemType.Placeable, 99) };
        var bag = new InventoryState(2, items); bag.TryAdd("wood", 20); bag.TryAdd("fence", 3);
        var farm = new FarmState(5, 3); var placements = new PlacementState(new[] { new PlaceableDefinition("fence") }, 5); placements.Place("fence", -6, -7, 2, bag, out _);
        var route = new NPCScheduleDefinition("npc", 1.4, 720, 1080, new[] { new SchedulePoint(7.4, .95), new SchedulePoint(11.2, .95), new SchedulePoint(11.2, -6.8), new SchedulePoint(9.15, -6.8) });
        var schedules = new NPCScheduleState(new[] { route }, 900); schedules.Restore(new[] { new SavedNPCSchedule { Id = "npc", Distance = 2 } });
        var npc = new NPCState(new[] { "npc" }); npc.MarkMet("npc");
        foreach (var area in new[] { HousingState.Home, HousingState.Island })
        {
            var save = SaveData.Capture(farm, bag, 1.5f, .1f, -2, 123, 900, 65, npcs: npc, schedules: schedules, placements: placements, playerArea: area);
            save.Validate(5, 3, bag.CreateEmpty(2), npcIds: new[] { "npc" }, schedules: new[] { route }, placeables: new[] { new PlaceableDefinition("fence") });
            Check(save.PlayerArea == area && save.PlayerX == 1.5f && save.PlacedObjects[0].QuarterTurns == 2 && save.NPCs[0].Met && save.NPCSchedules[0].Distance == 2, "Schema17 preserves area and world snapshots");
            save.PlayerArea = "unknown"; Reject(() => save.Validate(5, 3, bag.CreateEmpty(2))); save.PlayerArea = HousingState.Home;
            save.PlayerX = 40; Reject(() => save.Validate(5, 3, bag.CreateEmpty(2))); save.PlayerX = 1.5f;
            save.Version = 16; save.PlayerArea = null; save.Upgrade(999); save.Validate(5, 3, bag.CreateEmpty(2));
            Check(save.Version == SaveData.CurrentVersion && save.PlayerArea == HousingState.Island && save.Gold == 123 && save.Stamina == 65 && save.Slots.Sum(s => s.Quantity) == 22
                && save.PlacedObjects.Length == 1 && save.NPCSchedules[0].Distance == 2 && save.NPCs[0].Met, "v16 migration adds area without resetting world or inventory");
        }
        Console.WriteLine("PASS: " + checks + " housing/area/save assertions");
    }
}
