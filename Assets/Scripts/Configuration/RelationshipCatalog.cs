using System;
using System.Collections.Generic;
using System.Linq;
using MagicIsland.Interaction;
using MagicIsland.Inventory;
using UnityEngine;
namespace MagicIsland.Configuration
{
    [Serializable] public sealed class RelationshipConfig
    {
        public string NpcId, FavoriteItemId;
        public int Maximum = 100, FamiliarAt = 30, FriendlyAt = 70, FavoriteGain = 20, FoodGain = 10, CropGain = 5;
        [TextArea] public string FamiliarGreeting, FriendlyGreeting, FavoriteReply, StandardReply;
        public RelationshipDefinition Definition() => new RelationshipDefinition(NpcId, FavoriteItemId, Maximum, FamiliarAt, FriendlyAt, FavoriteGain, FoodGain, CropGain);
    }
    [CreateAssetMenu(menuName = "Magic Island/Relationship Catalog")]
    public sealed class RelationshipCatalog : ScriptableObject
    {
        public const string Address = "RelationshipCatalog";
        public List<RelationshipConfig> Relationships = new List<RelationshipConfig>();
        public RelationshipConfig Find(string id) => Relationships.Find(r => r != null && r.NpcId == id);
        public IEnumerable<RelationshipDefinition> Definitions(NPCCatalog npcs) => npcs.Ids().Select(id => Find(id).Definition());
        private static bool ValidText(string text) => !string.IsNullOrWhiteSpace(text) && text.Length <= 160;
        public bool Validate(NPCCatalog npcs, ItemCatalog items)
        {
            if (Relationships == null || Relationships.Count != npcs.NPCs.Count || Relationships.Any(r => r == null || npcs.Find(r.NpcId) == null
                || (items.Find(r.FavoriteItemId)?.Type != ItemType.Food && items.Find(r.FavoriteItemId)?.Type != ItemType.Crop)
                || !ValidText(r.FamiliarGreeting) || !ValidText(r.FriendlyGreeting) || !ValidText(r.FavoriteReply) || !ValidText(r.StandardReply))
                || Relationships.Select(r => r.NpcId).Distinct().Count() != Relationships.Count) return false;
            try { new RelationshipState(Definitions(npcs), new NPCState(npcs.Ids()), 1); return true; } catch (ArgumentException) { return false; }
        }
    }
}
