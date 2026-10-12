using System;
using System.Collections.Generic;
using System.Linq;
namespace MagicIsland.Interaction
{
    public sealed class StoryEventDefinition
    {
        public string Id { get; }
        public string NpcId { get; }
        public int RequiredPoints { get; }
        public string PrerequisiteId { get; }
        public string RecipeId { get; }
        public StoryEventDefinition(string id, string npcId, int points, string prerequisite = null, string recipe = null)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(npcId) || points < 1 || prerequisite == id)
                throw new ArgumentException("Invalid story event");
            Id = id; NpcId = npcId; RequiredPoints = points; PrerequisiteId = prerequisite; RecipeId = recipe;
        }
    }
    [Serializable] public sealed class SavedStoryEvent { public string Id; public bool Completed; }
    public sealed class StoryEventState
    {
        public IReadOnlyList<StoryEventDefinition> Definitions { get; }
        private readonly RelationshipState relationships;
        private readonly bool[] completed;
        private bool committing;
        public event Action Changed;
        public StoryEventState(IEnumerable<StoryEventDefinition> definitions, RelationshipState relationships)
        {
            var rules = definitions?.ToArray() ?? throw new ArgumentNullException(nameof(definitions));
            this.relationships = relationships ?? throw new ArgumentNullException(nameof(relationships));
            if (rules.Length < 1 || rules.Length > 32 || rules.Any(r => r == null) || rules.Select(r => r.Id).Distinct().Count() != rules.Length
                || rules.Where(r => !string.IsNullOrEmpty(r.RecipeId)).Select(r => r.RecipeId).Distinct().Count() != rules.Count(r => !string.IsNullOrEmpty(r.RecipeId)))
                throw new ArgumentException("Invalid story events");
            for (int i = 0; i < rules.Length; i++)
            {
                var rule = rules[i]; var relation = relationships.Find(rule.NpcId);
                int previous = Array.FindIndex(rules, r => r.Id == rule.PrerequisiteId);
                if (relation == null || rule.RequiredPoints > relation.Maximum || (!string.IsNullOrEmpty(rule.PrerequisiteId)
                    && (previous < 0 || previous >= i || rules[previous].NpcId != rule.NpcId || rules[previous].RequiredPoints > rule.RequiredPoints)))
                    throw new ArgumentException("Invalid event relationship or prerequisite order");
            }
            Definitions = Array.AsReadOnly(rules); completed = new bool[rules.Length];
        }
        private int Index(string id) { for (int i = 0; i < Definitions.Count; i++) if (Definitions[i].Id == id) return i; return -1; }
        public bool Completed(string id) { int i = Index(id); return i >= 0 && completed[i]; }
        public bool Available(string id)
        {
            int i = Index(id); if (committing || i < 0 || completed[i]) return false;
            var rule = Definitions[i]; return relationships.Points(rule.NpcId) >= rule.RequiredPoints
                && (string.IsNullOrEmpty(rule.PrerequisiteId) || Completed(rule.PrerequisiteId));
        }
        public StoryEventDefinition Next(string npcId) => Definitions.FirstOrDefault(r => r.NpcId == npcId && !Completed(r.Id));
        public bool RecipeUnlocked(string recipeId) => !string.IsNullOrEmpty(recipeId) && Definitions.Any(r => r.RecipeId == recipeId && Completed(r.Id));
        public bool Complete(string id)
        {
            if (!Available(id)) return false;
            committing = true;
            try { completed[Index(id)] = true; Changed?.Invoke(); return true; }
            finally { committing = false; }
        }
        public SavedStoryEvent[] Capture() => Definitions.Select((r, i) => new SavedStoryEvent { Id = r.Id, Completed = completed[i] }).ToArray();
        public void Restore(SavedStoryEvent[] saved)
        {
            if (committing || saved == null || saved.Length != Definitions.Count) throw new ArgumentException("Invalid saved story events");
            for (int i = 0; i < saved.Length; i++)
            {
                var rule = Definitions[i]; int previous = Index(rule.PrerequisiteId);
                if (saved[i] == null || saved[i].Id != rule.Id || (saved[i].Completed && (relationships.Points(rule.NpcId) < rule.RequiredPoints
                    || (previous >= 0 && !saved[previous].Completed)))) throw new ArgumentException("Invalid saved event completion");
            }
            for (int i = 0; i < completed.Length; i++) completed[i] = saved[i].Completed;
            Changed?.Invoke();
        }
    }
}
