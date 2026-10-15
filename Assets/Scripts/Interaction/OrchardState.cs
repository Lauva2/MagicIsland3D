using System;
using System.Collections.Generic;
using System.Linq;
using MagicIsland.Inventory;
namespace MagicIsland.Interaction
{
    public sealed class OrchardDefinition
    {
        public string SaplingId { get; }
        public string FruitId { get; }
        public int GrowthDays { get; }
        public int Interval { get; }
        public int Yield { get; }
        public OrchardDefinition(string sapling, string fruit, int growth, int interval, int yield)
        {
            if (string.IsNullOrWhiteSpace(sapling) || string.IsNullOrWhiteSpace(fruit) || sapling == fruit || growth < 2 || growth > 28 || interval < 1 || interval > 28 || yield < 1 || yield > 99) throw new ArgumentException("Invalid orchard definition");
            SaplingId = sapling; FruitId = fruit; GrowthDays = growth; Interval = interval; Yield = yield;
        }
    }
    [Serializable] public sealed class SavedOrchardTree { public int PlacementId, PlantedDay; public long NextFruitDay; }
    public enum OrchardResult { Success, InvalidTree, NotReady, BagFull, Busy }
    public sealed class OrchardState
    {
        private readonly Dictionary<string, OrchardDefinition> definitions;
        private readonly Dictionary<int, SavedOrchardTree> trees = new Dictionary<int, SavedOrchardTree>();
        private readonly PlacementState placement;
        private bool harvesting;
        public int Day { get; private set; }
        public int Count => trees.Count;
        public OrchardState(IEnumerable<OrchardDefinition> definitions, PlacementState placement, int day)
        {
            var values = definitions?.ToArray();
            if (values == null || values.Length < 1 || values.Length > 8 || values.Any(d => d == null) || values.Select(d => d.SaplingId).Distinct().Count() != values.Length || placement == null || placement.Indoor || day < 1 || day == int.MaxValue) throw new ArgumentException("Invalid orchard state");
            this.definitions = values.ToDictionary(d => d.SaplingId); this.placement = placement; Day = day;
        }
        public OrchardDefinition Definition(int id)
        { var record = placement.Find(id); return record != null && definitions.TryGetValue(record.ItemId, out var value) ? value : null; }
        public SavedOrchardTree Find(int id) => trees.TryGetValue(id, out var value) ? Copy(value) : null;
        private static SavedOrchardTree Copy(SavedOrchardTree value) => new SavedOrchardTree { PlacementId = value.PlacementId, PlantedDay = value.PlantedDay, NextFruitDay = value.NextFruitDay };
        public void Planted(SavedPlacedObject record)
        {
            if (!definitions.TryGetValue(record.ItemId, out var definition)) return;
            if (harvesting || placement.Find(record.Id)?.ItemId != record.ItemId || trees.ContainsKey(record.Id)) throw new InvalidOperationException("Invalid planted orchard record");
            trees.Add(record.Id, new SavedOrchardTree { PlacementId = record.Id, PlantedDay = Day, NextFruitDay = (long)Day + definition.GrowthDays });
        }
        public void Removed(int id) { if (harvesting) throw new InvalidOperationException("Orchard harvest in progress"); trees.Remove(id); }
        public int Stage(int id)
        {
            var definition = Definition(id); if (definition == null || !trees.TryGetValue(id, out var value)) return -1;
            int age = Day - value.PlantedDay; return age >= definition.GrowthDays ? 2 : age >= Math.Max(1, definition.GrowthDays / 2) ? 1 : 0;
        }
        public OrchardResult Check(int id, InventoryState inventory)
        {
            if (harvesting) return OrchardResult.Busy;
            if (Definition(id) == null || !trees.TryGetValue(id, out var value)) return OrchardResult.InvalidTree;
            if (Stage(id) < 2 || value.NextFruitDay > Day) return OrchardResult.NotReady;
            var definition = Definition(id); return inventory.CanAdd(definition.FruitId, definition.Yield) ? OrchardResult.Success : OrchardResult.BagFull;
        }
        public OrchardResult Harvest(int id, InventoryState inventory)
        {
            var result = Check(id, inventory); if (result != OrchardResult.Success) return result;
            harvesting = true;
            try
            {
                var definition = Definition(id); var value = trees[id]; value.NextFruitDay = (long)Day + definition.Interval;
                // Commit the cooldown before inventory observers can try harvesting again.
                if (!inventory.TryAdd(definition.FruitId, definition.Yield)) throw new InvalidOperationException("Validated orchard inventory changed");
                return OrchardResult.Success;
            }
            finally { harvesting = false; }
        }
        public void NextDay(int day) { if (harvesting || day != (long)Day + 1 || day == int.MaxValue) throw new ArgumentException("Orchard must advance with farm"); Day = day; }
        public SavedOrchardTree[] Capture() => trees.Values.OrderBy(t => t.PlacementId).Select(Copy).ToArray();
        public void Restore(SavedOrchardTree[] saved)
        {
            if (harvesting || saved == null || saved.Length > PlacementState.Maximum) throw new ArgumentException("Invalid orchard save");
            var ids = new HashSet<int>(); var expected = placement.Capture().Where(p => definitions.ContainsKey(p.ItemId)).ToArray();
            if (expected.Length != saved.Length) throw new ArgumentException("Orchard placement/save mismatch");
            foreach (var tree in saved)
            {
                var definition = tree == null ? null : Definition(tree.PlacementId);
                if (tree == null || definition == null || !ids.Add(tree.PlacementId) || tree.PlantedDay < 1 || tree.PlantedDay > Day
                    || tree.NextFruitDay < (long)tree.PlantedDay + definition.GrowthDays
                    || (tree.NextFruitDay > (long)tree.PlantedDay + definition.GrowthDays && tree.NextFruitDay < (long)tree.PlantedDay + definition.GrowthDays + definition.Interval)
                    || tree.NextFruitDay > Math.Max((long)tree.PlantedDay + definition.GrowthDays, (long)Day + definition.Interval)) throw new ArgumentException("Invalid orchard growth/cooldown record");
            }
            trees.Clear(); foreach (var tree in saved) trees.Add(tree.PlacementId, Copy(tree));
        }
    }
}
