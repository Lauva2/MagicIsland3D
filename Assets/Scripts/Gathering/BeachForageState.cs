using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MagicIsland.Inventory;

namespace MagicIsland.Gathering
{
    public sealed class BeachForageDefinition
    {
        public int Sites { get; }
        public int InitialCount { get; }
        public int DailyCount { get; }
        public int MaximumActive { get; }
        public BeachForageDefinition(int sites = 24, int initialCount = 10, int dailyCount = 5, int maximumActive = 16)
        {
            if (sites < 3 || sites > 64 || initialCount < 1 || dailyCount < 1 || maximumActive < 1
                || maximumActive > sites || initialCount > maximumActive || dailyCount > maximumActive)
                throw new ArgumentException("Invalid beach forage definition");
            Sites = sites; InitialCount = initialCount; DailyCount = dailyCount; MaximumActive = maximumActive;
        }
    }

    public sealed class BeachForageEntry
    {
        public string Id { get; }
        public int Site { get; }
        public string ItemId { get; }
        public int SpawnDay { get; }
        internal BeachForageEntry(string id, int site, string itemId, int spawnDay)
        { Id = id; Site = site; ItemId = itemId; SpawnDay = spawnDay; }
    }

    [Serializable]
    public sealed class SavedBeachForageEntry
    {
        public string Id, ItemId;
        public int Site, SpawnDay;
    }

    [Serializable]
    public sealed class SavedBeachForage
    {
        public int Day, Seed;
        public SavedBeachForageEntry[] Entries = Array.Empty<SavedBeachForageEntry>();
    }

    // Collection and the overnight refill share one state, so reloading cannot refill a picked beach.
    public sealed class BeachForageState
    {
        public const string ShellItemId = "beach_shell", DriftwoodItemId = "driftwood", GlassItemId = "sea_glass";
        public static bool IsBeachItem(string id) => id == ShellItemId || id == DriftwoodItemId || id == GlassItemId;
        public int Day { get; private set; }
        public int Seed { get; }
        public BeachForageDefinition Definition { get; }
        public IReadOnlyList<BeachForageEntry> Entries => entries.AsReadOnly();
        public int Count => entries.Count;
        public event Action Changed;
        private List<BeachForageEntry> entries = new List<BeachForageEntry>();
        private bool acting;

        public BeachForageState(int day, int seed, BeachForageDefinition definition = null)
        {
            if (day < 1 || day == int.MaxValue || seed < 1) throw new ArgumentException("Invalid beach day/seed");
            Day = day; Seed = seed; Definition = definition ?? new BeachForageDefinition();
            Refill(Definition.InitialCount);
        }
        public BeachForageState(int day, int seed, int sites, int initialCount, int dailyCount, int maximumActive)
            : this(day, seed, new BeachForageDefinition(sites, initialCount, dailyCount, maximumActive)) { }

        public BeachForageEntry Find(string id) => entries.Find(value => value.Id == id);
        public double AngleRadians(BeachForageEntry entry)
        {
            if (entry == null || entry.Site < 0 || entry.Site >= Definition.Sites) throw new ArgumentException("Invalid beach site");
            double jitter = (Hash(Seed, 0, entry.Site, 71) / (double)uint.MaxValue - .5) * .58;
            return (entry.Site + .5 + jitter) * Math.PI * 2 / Definition.Sites;
        }
        public GatherResult Check(string id, InventoryState inventory)
        {
            if (acting) return GatherResult.Busy;
            var entry = Find(id);
            if (entry == null || inventory == null) return GatherResult.InvalidTarget;
            return inventory.CanAdd(entry.ItemId, 1) ? GatherResult.Success : GatherResult.BagFull;
        }
        public GatherResult Collect(string id, InventoryState inventory)
        {
            var result = Check(id, inventory); if (result != GatherResult.Success) return result;
            acting = true;
            try
            {
                var entry = Find(id);
                // Observers see the object removed and the complete inventory transaction together.
                inventory.TryAdd(entry.ItemId, 1, false); entries.Remove(entry);
                inventory.NotifyChanged(); Changed?.Invoke(); return GatherResult.Success;
            }
            finally { acting = false; }
        }
        public void NextDay(int day)
        {
            if (acting || day != (long)Day + 1 || day == int.MaxValue) throw new ArgumentException("Beach must advance with farm");
            Day = day; Refill(Definition.DailyCount); Changed?.Invoke();
        }
        public SavedBeachForage Capture() => new SavedBeachForage { Day = Day, Seed = Seed,
            Entries = entries.Select(value => new SavedBeachForageEntry { Id = value.Id, Site = value.Site,
                ItemId = value.ItemId, SpawnDay = value.SpawnDay }).ToArray() };
        public void Restore(SavedBeachForage saved)
        {
            if (acting || saved == null || saved.Day != Day || saved.Seed != Seed || saved.Entries == null
                || saved.Entries.Length > Definition.MaximumActive) throw new ArgumentException("Invalid saved beach state");
            var sites = new HashSet<int>();
            foreach (var value in saved.Entries)
                if (value == null || value.Site < 0 || value.Site >= Definition.Sites || !sites.Add(value.Site)
                    || value.SpawnDay < 1 || value.SpawnDay > Day || value.Id != Id(value.SpawnDay, value.Site)
                    || value.ItemId != ItemFor(value.SpawnDay, value.Site)) throw new ArgumentException("Invalid saved beach entry");
            var restored = saved.Entries.Select(value => new BeachForageEntry(value.Id, value.Site, value.ItemId, value.SpawnDay)).ToList();
            entries = restored; Changed?.Invoke();
        }
        private void Refill(int amount)
        {
            var occupied = new HashSet<int>(entries.Select(value => value.Site));
            int[] available = Enumerable.Range(0, Definition.Sites).Where(site => !occupied.Contains(site))
                .OrderBy(site => Hash(Seed, Day, site, 13)).ThenBy(site => site).ToArray();
            int count = Math.Min(amount, Math.Min(Definition.MaximumActive - Count, available.Length));
            for (int i = 0; i < count; i++)
            {
                int site = available[i]; entries.Add(new BeachForageEntry(Id(Day, site), site, ItemFor(Day, site), Day));
            }
        }
        private static string Id(int day, int site) => "beach_" + day.ToString(CultureInfo.InvariantCulture) + "_" + site.ToString(CultureInfo.InvariantCulture);
        private string ItemFor(int day, int site)
        {
            uint roll = Hash(Seed, day, site, 43) % 100;
            return roll < 45 ? ShellItemId : roll < 80 ? DriftwoodItemId : GlassItemId;
        }
        private static uint Hash(int seed, int day, int site, int salt)
        {
            unchecked
            {
                uint value = (uint)seed * 747796405u ^ (uint)day * 2891336453u ^ (uint)site * 277803737u ^ (uint)salt * 1597334677u;
                value ^= value >> 16; value *= 2246822519u; value ^= value >> 13; value *= 3266489917u;
                return value ^ value >> 16;
            }
        }
    }
}
