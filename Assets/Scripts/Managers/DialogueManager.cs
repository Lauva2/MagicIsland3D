using MagicIsland.Configuration;
using MagicIsland.Interaction;
using UnityEngine;
namespace MagicIsland.Managers
{
    public sealed class DialogueManager : MonoBehaviour
    {
        public DialogueState State { get; private set; }
        public NPCManager NPCs { get; private set; }
        public RelationshipManager Relationships { get; private set; }
        public StoryEventManager Stories { get; private set; }
        public string ActiveEventId { get; private set; }
        private DialogueCatalog catalog;
        private CalendarManager calendar;
        private WeatherManager weather;
        public void Initialize(DialogueCatalog catalog, NPCManager npcs, CalendarManager calendar, WeatherManager weather, RelationshipManager relationships = null, StoryEventManager stories = null)
        { Clear(); this.catalog = catalog; NPCs = npcs; this.calendar = calendar; this.weather = weather; Relationships = relationships; Stories = stories; State = new DialogueState(); }
        public bool Begin(string id) => catalog.Find(id) != null && State.Begin(catalog.Find(id).Definition(), NPCs.State.HasMet(id), calendar.Date.Season, weather.State.Kind, Relationships?.Greeting(id));
        public bool BeginEvent(string eventId)
        {
            var entry = Stories?.Catalog.Find(eventId);
            if (entry == null || !Stories.State.Available(eventId) || !State.BeginLines(entry.NpcId, entry.Lines)) return false;
            ActiveEventId = eventId; return true;
        }
        public void Cancel() { State?.Clear(); ActiveEventId = null; }
        public void Clear() { Cancel(); State = null; NPCs = null; Relationships = null; Stories = null; catalog = null; calendar = null; weather = null; }
    }
}
