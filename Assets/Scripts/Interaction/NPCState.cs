using System;
using System.Collections.Generic;
using System.Linq;
using MagicIsland.CalendarSystem;
using MagicIsland.WeatherSystem;
namespace MagicIsland.Interaction
{
    [Serializable] public sealed class SavedNPC { public string Id; public bool Met; }
    public sealed class NPCState
    {
        private readonly string[] ids;
        private readonly bool[] met;
        public NPCState(IEnumerable<string> ids)
        {
            this.ids = ids?.ToArray() ?? throw new ArgumentNullException(nameof(ids));
            if (this.ids.Length < 1 || this.ids.Length > 8 || this.ids.Any(string.IsNullOrWhiteSpace) || this.ids.Distinct().Count() != this.ids.Length)
                throw new ArgumentException("Invalid NPC IDs");
            met = new bool[this.ids.Length];
        }
        public bool HasMet(string id) { int index = Array.IndexOf(ids, id); if (index < 0) throw new ArgumentException("Unknown NPC"); return met[index]; }
        public bool MarkMet(string id) { int index = Array.IndexOf(ids, id); if (index < 0) return false; bool changed = !met[index]; met[index] = true; return changed; }
        public SavedNPC[] Capture() => ids.Select((id, i) => new SavedNPC { Id = id, Met = met[i] }).ToArray();
        public void Restore(SavedNPC[] saved)
        {
            if (saved == null || saved.Length != ids.Length || saved.Where((s, i) => s == null || s.Id != ids[i]).Any()) throw new ArgumentException("Invalid saved NPCs");
            for (int i = 0; i < met.Length; i++) met[i] = saved[i].Met;
        }
    }
    public sealed class DialogueDefinition
    {
        public string NpcId { get; }
        private readonly string[] intro, repeat, seasons, weather;
        public DialogueDefinition(string id, IEnumerable<string> intro, IEnumerable<string> repeat, IEnumerable<string> seasons, IEnumerable<string> weather)
        {
            NpcId = id; this.intro = intro?.ToArray(); this.repeat = repeat?.ToArray(); this.seasons = seasons?.ToArray(); this.weather = weather?.ToArray();
            if (string.IsNullOrWhiteSpace(id) || !Valid(this.intro, 1, 16) || !Valid(this.repeat, 1, 16) || !Valid(this.seasons, 4, 4) || !Valid(this.weather, 2, 2))
                throw new ArgumentException("Invalid dialogue lines");
        }
        private static bool Valid(string[] lines, int min, int max) => lines != null && lines.Length >= min && lines.Length <= max
            && lines.All(s => !string.IsNullOrWhiteSpace(s) && s.Length <= 160);
        public string[] Lines(bool met, Season season, WeatherKind kind, string greeting = null)
        {
            if ((int)season < 0 || (int)season >= 4 || !WeatherState.IsValid(kind)) throw new ArgumentException("Invalid dialogue context");
            return (met ? greeting == null ? repeat : new[] { greeting } : intro).Concat(new[] { seasons[(int)season], weather[(int)kind] }).ToArray();
        }
    }
    public sealed class DialogueState
    {
        public bool Active { get; private set; }
        public string NpcId { get; private set; }
        public int Index { get; private set; }
        public IReadOnlyList<string> Lines { get; private set; } = Array.AsReadOnly(Array.Empty<string>());
        public bool LastLine => Active && Index == Lines.Count - 1;
        public string Text => Active ? Lines[Index] : "";
        public bool Begin(DialogueDefinition definition, bool met, Season season, WeatherKind weather, string greeting = null)
        {
            if (Active) return false;
            var lines = definition.Lines(met, season, weather, greeting);
            return BeginLines(definition.NpcId, lines);
        }
        public bool BeginLines(string npcId, IEnumerable<string> lines)
        {
            if (Active) return false;
            var copy = lines?.ToArray();
            if (string.IsNullOrWhiteSpace(npcId) || copy == null || copy.Length < 1 || copy.Length > 18 || copy.Any(l => string.IsNullOrWhiteSpace(l) || l.Length > 160))
                throw new ArgumentException("Invalid dialogue session");
            Lines = Array.AsReadOnly(copy); NpcId = npcId; Index = 0; Active = true; return true;
        }
        // True only when the user advances past the final line.
        public bool Advance()
        {
            if (!Active) return false;
            if (!LastLine) { Index++; return false; }
            Active = false; return true;
        }
        public void Clear() { Active = false; NpcId = null; Index = 0; Lines = Array.AsReadOnly(Array.Empty<string>()); }
    }
}
