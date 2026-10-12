using MagicIsland.Configuration;
using MagicIsland.Interaction;
using UnityEngine;
namespace MagicIsland.Managers
{
    public sealed class StoryEventManager : MonoBehaviour
    {
        public StoryEventState State { get; private set; }
        public StoryEventCatalog Catalog { get; private set; }
        public void Initialize(StoryEventCatalog catalog, RelationshipManager relationships, SavedStoryEvent[] saved = null)
        { Clear(); Catalog = catalog; State = new StoryEventState(catalog.Definitions(), relationships.State); if (saved != null) State.Restore(saved); }
        public StoryEventConfig Next(string npcId) { var rule = State.Next(npcId); return rule == null ? null : Catalog.Find(rule.Id); }
        public string UnlockHint(string eventId)
        {
            var rule = Catalog.Find(eventId); return rule == null ? "食谱未解锁" : "林舟好感度 " + rule.RequiredPoints + "\n完成“" + rule.DisplayName + "”";
        }
        public void Clear() { State = null; Catalog = null; }
    }
}
