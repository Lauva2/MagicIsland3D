using System;
using System.Collections.Generic;
using System.Linq;
using MagicIsland.Interaction;
using UnityEngine;
namespace MagicIsland.Configuration
{
    [Serializable] public sealed class DialogueConfig
    {
        public string NpcId;
        public List<string> Intro = new List<string>(), Repeat = new List<string>();
        public string[] Seasons = new string[4], Weather = new string[2];
        public DialogueDefinition Definition() => new DialogueDefinition(NpcId, Intro, Repeat, Seasons, Weather);
    }
    [CreateAssetMenu(menuName = "Magic Island/Dialogue Catalog")]
    public sealed class DialogueCatalog : ScriptableObject
    {
        public const string Address = "DialogueCatalog";
        public List<DialogueConfig> Dialogues = new List<DialogueConfig>();
        public DialogueConfig Find(string id) => Dialogues.Find(d => d != null && d.NpcId == id);
        public bool Validate(NPCCatalog npcs)
        {
            if (Dialogues == null || Dialogues.Count != npcs.NPCs.Count || Dialogues.Any(d => d == null || npcs.Find(d.NpcId) == null)
                || Dialogues.Select(d => d.NpcId).Distinct().Count() != Dialogues.Count) return false;
            try { foreach (var dialogue in Dialogues) dialogue.Definition(); return true; }
            catch (ArgumentException) { return false; }
        }
    }
}
