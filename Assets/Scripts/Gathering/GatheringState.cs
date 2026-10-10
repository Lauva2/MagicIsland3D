using System;
using System.Collections.Generic;
using System.Linq;
using MagicIsland.Farming;
using MagicIsland.Inventory;
using MagicIsland.Stamina;
namespace MagicIsland.Gathering
{
    public enum GatherResult { Success, InvalidTarget, WrongTool, Depleted, BagFull, InsufficientStamina, Busy }
    public sealed class GatheringDefinition
    {
        public string Id { get; }
        public string ItemId { get; }
        public FarmTool Tool { get; }
        public int Hits { get; }
        public int Yield { get; }
        public int StaminaCost { get; }
        public int RespawnDays { get; }
        public GatheringDefinition(string id, string itemId, FarmTool tool, int hits, int yield, int staminaCost, int respawnDays)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(itemId) || (tool != FarmTool.Axe && tool != FarmTool.Pickaxe)
                || hits < 1 || hits > 20 || yield < 1 || yield > 99 || staminaCost < 0 || respawnDays < 1 || respawnDays > 365)
                throw new ArgumentException("Invalid gathering definition");
            Id = id; ItemId = itemId; Tool = tool; Hits = hits; Yield = yield; StaminaCost = staminaCost; RespawnDays = respawnDays;
        }
    }
    [Serializable]
    public sealed class SavedGatheringNode
    {
        public string Id;
        public int HitsRemaining;
        public long RespawnDay;
    }
    public sealed class GatheringState
    {
        public int Day { get; private set; }
        public IReadOnlyList<GatheringDefinition> Definitions { get; }
        private readonly GatheringDefinition[] definitions;
        private SavedGatheringNode[] nodes;
        private bool acting;
        public event Action Changed;
        public int Count => nodes.Length;
        public int HitsRemaining(int index) => nodes[index].HitsRemaining;
        public long RespawnDay(int index) => nodes[index].RespawnDay;
        public bool IsDepleted(int index) => HitsRemaining(index) == 0;
        public GatheringState(IEnumerable<GatheringDefinition> definitions, int day)
        {
            this.definitions = definitions?.ToArray() ?? throw new ArgumentNullException(nameof(definitions));
            if (day < 1 || day == int.MaxValue || this.definitions.Length == 0 || this.definitions.Length > 64 || this.definitions.Any(value => value == null)
                || this.definitions.Select(value => value.Id).Distinct().Count() != this.definitions.Length) throw new ArgumentException("Invalid gathering nodes/day");
            Day = day; Definitions = Array.AsReadOnly(this.definitions);
            nodes = this.definitions.Select(value => new SavedGatheringNode { Id = value.Id, HitsRemaining = value.Hits }).ToArray();
        }
        public GatherResult Check(int index, FarmTool tool, InventoryState inventory, StaminaState stamina)
        {
            if (acting) return GatherResult.Busy;
            if (index < 0 || index >= Count) return GatherResult.InvalidTarget;
            if (IsDepleted(index)) return GatherResult.Depleted;
            var definition = definitions[index];
            if (tool != definition.Tool) return GatherResult.WrongTool;
            if (!inventory.CanAdd(definition.ItemId, definition.Yield)) return GatherResult.BagFull;
            return stamina.CanSpend(definition.StaminaCost) ? GatherResult.Success : GatherResult.InsufficientStamina;
        }
        public GatherResult Hit(int index, FarmTool tool, InventoryState inventory, StaminaState stamina, int damage = 1)
        {
            if (damage < 1 || damage > 2) throw new ArgumentOutOfRangeException(nameof(damage));
            GatherResult result = Check(index, tool, inventory, stamina); if (result != GatherResult.Success) return result;
            acting = true;
            try
            {
                var definition = definitions[index]; var node = nodes[index];
                node.HitsRemaining = Math.Max(0, node.HitsRemaining - damage); if (node.HitsRemaining == 0) node.RespawnDay = (long)Day + definition.RespawnDays;
                stamina.TrySpend(definition.StaminaCost, false);
                if (node.HitsRemaining == 0) inventory.TryAdd(definition.ItemId, definition.Yield);
                if (definition.StaminaCost > 0) stamina.NotifyChanged(); Changed?.Invoke(); return GatherResult.Success;
            }
            finally { acting = false; }
        }
        public void NextDay(int day)
        {
            if (acting || day != (long)Day + 1 || day == int.MaxValue) throw new ArgumentException("Gathering must advance with farm");
            Day = day;
            for (int i = 0; i < Count; i++)
                if (nodes[i].HitsRemaining == 0 && nodes[i].RespawnDay <= day)
                { nodes[i].HitsRemaining = definitions[i].Hits; nodes[i].RespawnDay = 0; }
            Changed?.Invoke();
        }
        public SavedGatheringNode[] Capture() => nodes.Select(value => new SavedGatheringNode { Id = value.Id,
            HitsRemaining = value.HitsRemaining, RespawnDay = value.RespawnDay }).ToArray();
        public void Restore(SavedGatheringNode[] saved)
        {
            if (acting || saved == null || saved.Length != Count) throw new ArgumentException("Invalid saved gathering count");
            for (int i = 0; i < Count; i++)
            {
                var value = saved[i]; var definition = definitions[i];
                if (value == null || value.Id != definition.Id || value.HitsRemaining < 0 || value.HitsRemaining > definition.Hits
                    || (value.HitsRemaining > 0 && value.RespawnDay != 0)
                    || (value.HitsRemaining == 0 && (value.RespawnDay <= Day || value.RespawnDay > (long)Day + definition.RespawnDays)))
                    throw new ArgumentException("Invalid saved gathering state");
            }
            nodes = saved.Select(value => new SavedGatheringNode { Id = value.Id, HitsRemaining = value.HitsRemaining, RespawnDay = value.RespawnDay }).ToArray();
            Changed?.Invoke();
        }
    }
}
