using System;
using System.Collections.Generic;
using System.Linq;
using MagicIsland.Interaction;
using UnityEngine;
namespace MagicIsland.Configuration
{
    [Serializable] public sealed class NPCConfig
    {
        public string Id, DisplayName, Role, PrefabAddress;
        public Vector3 HallOffset;
    }
    [CreateAssetMenu(menuName = "Magic Island/NPC Catalog")]
    public sealed class NPCCatalog : ScriptableObject
    {
        public const string Address = "NPCCatalog";
        public List<NPCConfig> NPCs = new List<NPCConfig>();
        public NPCConfig Find(string id) => NPCs.Find(n => n != null && n.Id == id);
        public IEnumerable<string> Ids() => NPCs.Select(n => n.Id);
        public bool Validate(GameConfig game)
        {
            if (NPCs == null || NPCs.Any(n => n == null || string.IsNullOrWhiteSpace(n.DisplayName) || string.IsNullOrWhiteSpace(n.Role) || string.IsNullOrWhiteSpace(n.PrefabAddress))) return false;
            try
            {
                new NPCState(Ids());
                return NPCs.All(n => {
                    var p = new Vector3(Mathf.Min(9.3f, game.FarmSize * .6f + 2.7f), 0, 4.5f) + n.HallOffset;
                    return !float.IsNaN(p.x) && !float.IsNaN(p.z) && n.HallOffset.y == 0 && Mathf.Abs(p.x) <= 11.3f && Mathf.Abs(p.z) <= 11.3f
                        && (Mathf.Abs(p.x) > game.FarmSize * .6f + .5f || Mathf.Abs(p.z) > game.FarmSize * .6f + .5f);
                });
            }
            catch (ArgumentException) { return false; }
        }
    }
}
