using System;
using System.Collections.Generic;
using System.Linq;
using MagicIsland.Interaction;
using UnityEngine;
namespace MagicIsland.Configuration
{
    [Serializable] public sealed class StoryEventConfig
    {
        public string Id, NpcId, DisplayName, PrerequisiteId, RecipeId;
        public int RequiredPoints;
        [TextArea] public List<string> Lines = new List<string>();
        public StoryEventDefinition Definition() => new StoryEventDefinition(Id, NpcId, RequiredPoints, PrerequisiteId, RecipeId);
    }
    [CreateAssetMenu(menuName = "Magic Island/Story Event Catalog")]
    public sealed class StoryEventCatalog : ScriptableObject
    {
        public const string Address = "StoryEventCatalog";
        public List<StoryEventConfig> Events = new List<StoryEventConfig>();
        public StoryEventConfig Find(string id) => Events.Find(e => e != null && e.Id == id);
        public IEnumerable<StoryEventDefinition> Definitions() => Events.Select(e => e.Definition());
        public bool Validate(NPCCatalog npcs, RelationshipCatalog relationships, CookingCatalog cooking)
        {
            if (Events == null || Events.Any(e => e == null || string.IsNullOrWhiteSpace(e.DisplayName) || e.Lines == null || e.Lines.Count < 1 || e.Lines.Count > 16
                || e.Lines.Any(l => string.IsNullOrWhiteSpace(l) || l.Length > 160) || (!string.IsNullOrEmpty(e.RecipeId)
                && !cooking.Recipes.Any(r => r.Id == e.RecipeId && r.RequiredEventId == e.Id)))
                || cooking.Recipes.Any(r => !string.IsNullOrEmpty(r.RequiredEventId) && !Events.Any(e => e.Id == r.RequiredEventId && e.RecipeId == r.Id))) return false;
            try { new StoryEventState(Definitions(), new RelationshipState(relationships.Definitions(npcs), new NPCState(npcs.Ids()), 1)); return true; }
            catch (ArgumentException) { return false; }
        }
    }
}
