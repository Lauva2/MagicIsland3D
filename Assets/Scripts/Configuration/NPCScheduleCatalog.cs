using System;
using System.Collections.Generic;
using System.Linq;
using MagicIsland.Interaction;
using UnityEngine;

namespace MagicIsland.Configuration
{
    [Serializable] public sealed class NPCScheduleConfig
    {
        public string NpcId;
        public float WalkSpeed = 1.4f, CorridorX = 11.2f;
        public Vector3 CoastOffset = new Vector3(-1.25f, 0, 1.4f);
        public int CoastStartMinute = 720, ReturnMinute = 1080;
        public float HomeYaw = 180, CoastYaw = 90;
    }
    [CreateAssetMenu(menuName = "Magic Island/NPC Schedule Catalog")]
    public sealed class NPCScheduleCatalog : ScriptableObject
    {
        public const string Address = "NPCScheduleCatalog";
        public List<NPCScheduleConfig> Schedules = new List<NPCScheduleConfig>();
        public NPCScheduleConfig Find(string id) => Schedules.Find(s => s.NpcId == id);
        public IEnumerable<NPCScheduleDefinition> Definitions(GameConfig game, NPCCatalog npcs, FishingCatalog fishing)
        {
            var hall = new Vector3(Mathf.Min(9.3f, game.FarmSize * .6f + 2.7f), 0, 4.5f);
            foreach (var s in Schedules)
            {
                var home = hall + npcs.Find(s.NpcId).HallOffset;
                var coast = fishing.SpotPosition + s.CoastOffset;
                yield return new NPCScheduleDefinition(s.NpcId, s.WalkSpeed, s.CoastStartMinute, s.ReturnMinute,
                    new[] { new SchedulePoint(home.x, home.z), new SchedulePoint(s.CorridorX, home.z),
                        new SchedulePoint(s.CorridorX, coast.z), new SchedulePoint(coast.x, coast.z) });
            }
        }
        public bool Validate(GameConfig game, NPCCatalog npcs, FishingCatalog fishing)
        {
            if (game == null || npcs == null || fishing == null || Schedules == null
                || Schedules.Any(s => s == null || npcs.Find(s.NpcId) == null || s.CoastOffset.y != 0
                    || !Finite(s.HomeYaw) || !Finite(s.CoastYaw))
                || !Schedules.Select(s => s.NpcId).SequenceEqual(npcs.Ids())) return false;
            try { var state = new NPCScheduleState(Definitions(game, npcs, fishing)); return state.Definitions.All(d => d.AvoidsFarm(game.FarmSize)); }
            catch (ArgumentException) { return false; }
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
