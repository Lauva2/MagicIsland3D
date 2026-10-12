using System;
using System.Collections.Generic;
using System.Linq;
using MagicIsland.Inventory;
namespace MagicIsland.Interaction
{
    public sealed class PlaceableDefinition
    {
        public string ItemId { get; }
        public PlaceableDefinition(string id)
        { if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Invalid placeable ID"); ItemId = id; }
    }
    [Serializable] public sealed class SavedPlacedObject
    { public int Id; public string ItemId; public int GridX, GridZ, QuarterTurns; }
    public enum PlacementResult { Success, InvalidItem, InvalidPosition, FarmReserved, RouteReserved, Occupied, MissingItem, BagFull, LimitReached, Busy, Blocked, TooFar }
    public sealed class PlacementState
    {
        public const int Maximum = 128;
        private readonly HashSet<string> items;
        private readonly List<SavedPlacedObject> placed = new List<SavedPlacedObject>();
        private readonly NPCScheduleDefinition[] routes;
        private readonly int farmSize;
        public bool Indoor { get; }
        public int HouseLevel { get; private set; }
        public void ExpandHome(int level)
        { if (!Indoor || level < HouseLevel || level > 1) throw new ArgumentException("Invalid room expansion"); HouseLevel = level; }
        private bool committing;
        public int Count => placed.Count;
        public PlacementState(IEnumerable<PlaceableDefinition> definitions, int farmSize, IEnumerable<NPCScheduleDefinition> routes = null, bool indoor = false, int houseLevel = 0)
        {
            var values = definitions?.ToArray();
            if (values == null || values.Length < 1 || values.Length > 12 || values.Any(d => d == null)
                || values.Select(d => d.ItemId).Distinct().Count() != values.Length || farmSize < 1 || farmSize > 12 || houseLevel < 0 || houseLevel > 1 || (!indoor && houseLevel != 0))
                throw new ArgumentException("Invalid placement definitions");
            items = new HashSet<string>(values.Select(d => d.ItemId), StringComparer.Ordinal);
            this.farmSize = farmSize; this.routes = routes?.ToArray() ?? Array.Empty<NPCScheduleDefinition>();
            Indoor = indoor; HouseLevel = houseLevel;
            if (this.routes.Any(r => r == null)) throw new ArgumentException("Invalid reserved routes");
        }
        private static bool Overlaps(double x, double z, double minX, double maxX, double minZ, double maxZ)
            => x + .5 > minX && x - .5 < maxX && z + .5 > minZ && z - .5 < maxZ;
        public PlacementResult CheckLocation(string item, int x, int z, int turns)
        {
            if (!items.Contains(item ?? "")) return PlacementResult.InvalidItem;
            if (Indoor)
            {
                if (x < -5 || x > 5 + HouseLevel * 4 || z < -4 || z > 4 + HouseLevel * 2 || turns < 0 || turns > 3) return PlacementResult.InvalidPosition;
                if (FurnitureLayout.Reserved(x, z)) return PlacementResult.RouteReserved;
                return placed.Any(p => p.GridX == x && p.GridZ == z) ? PlacementResult.Occupied : PlacementResult.Success;
            }
            if (Math.Abs((long)x) > 11 || Math.Abs((long)z) > 11 || turns < 0 || turns > 3) return PlacementResult.InvalidPosition;
            double half = farmSize * .6 + .5;
            if (Overlaps(x, z, -half, half, -half, half)) return PlacementResult.FarmReserved;
            foreach (var route in routes) for (int i = 1; i < route.Points.Count; i++)
            {
                var a = route.Points[i - 1]; var b = route.Points[i];
                if (Overlaps(x, z, Math.Min(a.X, b.X) - .4, Math.Max(a.X, b.X) + .4,
                    Math.Min(a.Z, b.Z) - .4, Math.Max(a.Z, b.Z) + .4)) return PlacementResult.RouteReserved;
            }
            return placed.Any(p => p.GridX == x && p.GridZ == z) ? PlacementResult.Occupied : PlacementResult.Success;
        }
        public PlacementResult CheckPlace(string item, int x, int z, int turns, InventoryState bag)
        {
            if (committing) return PlacementResult.Busy;
            var result = CheckLocation(item, x, z, turns); if (result != PlacementResult.Success) return result;
            if (Count >= Maximum) return PlacementResult.LimitReached;
            return bag.GetCount(item) < 1 ? PlacementResult.MissingItem : PlacementResult.Success;
        }
        public PlacementResult Place(string item, int x, int z, int turns, InventoryState bag, out SavedPlacedObject record, bool notify = true)
        {
            record = null; var result = CheckPlace(item, x, z, turns, bag); if (result != PlacementResult.Success) return result;
            committing = true;
            try
            {
                int id = Enumerable.Range(1, Maximum).First(i => placed.All(p => p.Id != i));
                var value = new SavedPlacedObject { Id = id, ItemId = item, GridX = x, GridZ = z, QuarterTurns = turns };
                bag.TryRemove(item, 1, false); placed.Add(value); record = Copy(value); if (notify) bag.NotifyChanged(); return PlacementResult.Success;
            }
            finally { committing = false; }
        }
        public SavedPlacedObject Find(int id) { var record = placed.Find(p => p.Id == id); return record == null ? null : Copy(record); }
        public PlacementResult Recover(int id, InventoryState bag, bool notify = true)
        {
            if (committing) return PlacementResult.Busy;
            var value = placed.Find(p => p.Id == id); if (value == null) return PlacementResult.InvalidItem;
            if (!bag.CanAdd(value.ItemId, 1)) return PlacementResult.BagFull;
            committing = true;
            try { bag.TryAdd(value.ItemId, 1, false); placed.Remove(value); if (notify) bag.NotifyChanged(); return PlacementResult.Success; }
            finally { committing = false; }
        }
        public SavedPlacedObject[] Capture() => placed.Select(Copy).ToArray();
        private static SavedPlacedObject Copy(SavedPlacedObject p) => new SavedPlacedObject { Id = p.Id, ItemId = p.ItemId, GridX = p.GridX, GridZ = p.GridZ, QuarterTurns = p.QuarterTurns };
        public void Restore(SavedPlacedObject[] saved)
        {
            if (saved == null || saved.Length > Maximum) throw new ArgumentException("Invalid placed object count");
            var check = new PlacementState(items.Select(i => new PlaceableDefinition(i)), farmSize, routes, Indoor, HouseLevel);
            foreach (var p in saved)
            {
                if (p == null || p.Id < 1 || p.Id > Maximum || check.placed.Any(v => v.Id == p.Id)
                    || check.CheckLocation(p.ItemId, p.GridX, p.GridZ, p.QuarterTurns) != PlacementResult.Success)
                    throw new ArgumentException("Invalid placed object record");
                check.placed.Add(Copy(p));
            }
            placed.Clear(); placed.AddRange(check.placed);
        }
    }
}
