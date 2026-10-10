using System;
using MagicIsland.Farming;
using MagicIsland.Inventory;
using System.Collections.Generic;
using MagicIsland.WeatherSystem;
using MagicIsland.Tasks;
using MagicIsland.Orders;
using MagicIsland.Gathering;
using MagicIsland.Tools;
using System.Linq;

namespace MagicIsland.Saving
{
    [Serializable]
    public sealed class SavedSlot
    {
        public string ItemId;
        public int Quantity;
    }

    [Serializable]
    public sealed class SaveData
    {
        public const int CurrentVersion = 11;
        public SavedSlot[] Storage = Array.Empty<SavedSlot>();
        public SavedToolUpgrade[] ToolUpgrades = Array.Empty<SavedToolUpgrade>();
        public SavedGatheringNode[] Gathering = Array.Empty<SavedGatheringNode>();
        public SavedOrder Order;
        public TaskProgress[] Tasks = Array.Empty<TaskProgress>();
        public int WeatherSeed = 1;
        public WeatherKind Weather;
        public string SelectedSeedItemId;
        public long Gold;
        public int Stamina = 100;
        public double ClockMinute = 360;
        public void Upgrade(int initialGold, int maxStamina = 100, string defaultCropId = "turnip", string defaultSeedId = "turnip_seed", IEnumerable<TaskDefinition> tasks = null, IEnumerable<OrderDefinition> orders = null, int daysPerSeason = 28, IEnumerable<GatheringDefinition> gathering = null, IEnumerable<ToolUpgradeDefinition> toolUpgrades = null, int storageSlots = 0)
        {
            if (Version == 1) { Gold = initialGold; Version = 2; }
            if (Version == 2) { ClockMinute = 360; Version = 3; }
            if (Version == 3) { Stamina = maxStamina; Version = 4; }
            if (Version == 4)
            {
                SelectedSeedItemId = defaultSeedId;
                if (Cells != null) foreach (FarmCell cell in Cells)
                    if (cell != null) cell.CropId = cell.Planted ? defaultCropId : null;
                Version = 5;
            }
            if (Version == 5) { WeatherSeed = 1; Weather = WeatherKind.Sunny; Version = 6; }
            if (Version == 6) { Tasks = tasks == null ? Array.Empty<TaskProgress>() : new TaskState(tasks).Capture(); Version = 7; }
            if (Version == 7) { Order = orders == null ? null : new OrderState(orders, daysPerSeason, 1, Day).Capture(); Version = 8; }
            if (Version == 8) { Gathering = gathering == null ? Array.Empty<SavedGatheringNode>() : new GatheringState(gathering, Day).Capture(); Version = 9; }
            if (Version == 9) { ToolUpgrades = toolUpgrades == null ? Array.Empty<SavedToolUpgrade>() : new ToolUpgradeState(toolUpgrades).Capture(); Version = 10; }
            if (Version == 10) { Storage = Enumerable.Range(0, storageSlots).Select(i => new SavedSlot()).ToArray(); Version = CurrentVersion; }
        }
        public int Version;
        public string SavedUtc;
        public int FarmSize;
        public int Day;
        public FarmCell[] Cells;
        public SavedSlot[] Slots;
        public float PlayerX, PlayerY, PlayerZ;

        public void Validate(int farmSize, int growthDays, InventoryState inventory, int maxStamina = 100,
            IEnumerable<CropDefinition> crops = null, string defaultCropId = "turnip", IEnumerable<TaskDefinition> tasks = null, IEnumerable<OrderDefinition> orders = null, int daysPerSeason = 28, IEnumerable<GatheringDefinition> gathering = null, IEnumerable<ToolUpgradeDefinition> toolUpgrades = null, int storageSlots = 0)
        {
            if (Version != CurrentVersion) throw new InvalidOperationException("Unsupported save version");
            if (storageSlots > 0) inventory.CreateEmpty(storageSlots).Restore(GetStorageSlots());
            if (tasks != null) new TaskState(tasks).Restore(Tasks);
            if (toolUpgrades != null) new ToolUpgradeState(toolUpgrades).Restore(ToolUpgrades);
            if (gathering != null) new GatheringState(gathering, Day).Restore(Gathering);
            if (orders != null)
            {
                if (Order == null) throw new InvalidOperationException("Missing saved order");
                new OrderState(orders, daysPerSeason, Order.Seed, Day).Restore(Order, Day);
            }
            if (WeatherSeed < 1 || !WeatherState.IsValid(Weather)) throw new InvalidOperationException("Invalid saved weather");
            if (double.IsNaN(ClockMinute) || double.IsInfinity(ClockMinute) || ClockMinute < 0 || ClockMinute > 1440)
                throw new InvalidOperationException("Invalid saved clock");
            if (maxStamina < 1 || Stamina < 0 || Stamina > maxStamina) throw new InvalidOperationException("Invalid saved stamina");
            if (Gold < 0) throw new InvalidOperationException("Invalid saved gold");
            if (FarmSize != farmSize || Day < 1 || Day == int.MaxValue || Cells == null || Cells.Length != farmSize * farmSize)
                throw new InvalidOperationException("Invalid saved farm dimensions or day");
            var definitions = new Dictionary<string, CropDefinition>();
            bool checkSelection = crops != null, selectionFound = false;
            if (crops == null) crops = new[] { new CropDefinition(defaultCropId, "turnip_seed", "turnip", growthDays) };
            foreach (CropDefinition crop in crops)
            { definitions.Add(crop.Id, crop); if (crop.SeedId == SelectedSeedItemId) selectionFound = true; }
            if (checkSelection && !selectionFound) throw new InvalidOperationException("Invalid selected seed");
            foreach (FarmCell cell in Cells)
            {
                string id = cell != null && cell.Planted ? cell.CropId : defaultCropId;
                CropDefinition crop = id != null && definitions.TryGetValue(id, out CropDefinition value) ? value : null;
                if (cell == null || crop == null || cell.RequiredGrowthDays != crop.GrowthDays || cell.Growth < 0 || cell.Growth > crop.GrowthDays
                    || (cell.Planted && !cell.Tilled) || (cell.Watered && !cell.Planted)
                    || (!cell.Planted && (cell.Growth != 0 || !string.IsNullOrEmpty(cell.CropId))))
                    throw new InvalidOperationException("Invalid saved farm cell");
            }
            if (!Finite(PlayerX) || !Finite(PlayerY) || !Finite(PlayerZ)
                || Math.Abs(PlayerX) > 12f || Math.Abs(PlayerZ) > 12f || PlayerY < -1f || PlayerY > 10f)
                throw new InvalidOperationException("Invalid saved player position");
            if (Slots == null || Slots.Length != inventory.Capacity) throw new InvalidOperationException("Saved inventory capacity changed");
            var restored = new InventorySlot[Slots.Length];
            for (int i = 0; i < Slots.Length; i++)
            {
                if (Slots[i] == null) throw new InvalidOperationException("Missing saved slot");
                restored[i] = new InventorySlot(Slots[i].ItemId, Slots[i].Quantity);
            }
            // This inventory is temporary during validation; no live world state is modified.
            inventory.Restore(restored);
        }

        public static SaveData Capture(FarmState farm, InventoryState inventory, float x, float y, float z, long gold = 0, double clockMinute = 360, int stamina = 100, string selectedSeed = null,
            int weatherSeed = 1, WeatherKind weather = WeatherKind.Sunny, TaskState tasks = null, OrderState orders = null, GatheringState gathering = null, ToolUpgradeState toolUpgrades = null, StorageState storage = null)
        {
            var data = new SaveData { Version = CurrentVersion, Storage = storage?.Capture().Select(s => new SavedSlot { ItemId = s.ItemId, Quantity = s.Quantity }).ToArray() ?? Array.Empty<SavedSlot>(), ToolUpgrades = toolUpgrades?.Capture() ?? Array.Empty<SavedToolUpgrade>(), Gathering = gathering?.Capture() ?? Array.Empty<SavedGatheringNode>(), Order = orders?.Capture(), Tasks = tasks?.Capture() ?? Array.Empty<TaskProgress>(), WeatherSeed = weatherSeed, Weather = weather, Gold = gold, ClockMinute = clockMinute, Stamina = stamina, SelectedSeedItemId = selectedSeed, SavedUtc = DateTime.UtcNow.ToString("O"),
                FarmSize = farm.Size, Day = farm.Day, PlayerX = x, PlayerY = y, PlayerZ = z,
                Cells = new FarmCell[farm.Cells.Length], Slots = new SavedSlot[inventory.Capacity] };
            for (int i = 0; i < data.Cells.Length; i++)
            {
                FarmCell cell = farm.Cells[i];
                data.Cells[i] = new FarmCell { CropId = cell.CropId, Tilled = cell.Tilled, Planted = cell.Planted, Watered = cell.Watered,
                    Growth = cell.Growth, RequiredGrowthDays = cell.RequiredGrowthDays };
            }
            for (int i = 0; i < data.Slots.Length; i++)
                data.Slots[i] = new SavedSlot { ItemId = inventory.Slots[i].ItemId, Quantity = inventory.Slots[i].Quantity };
            return data;
        }

        public InventorySlot[] GetStorageSlots()
        {
            if (Storage == null || Storage.Any(s => s == null)) throw new InvalidOperationException("Missing storage slots");
            return Storage.Select(s => new InventorySlot(s.ItemId, s.Quantity)).ToArray();
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
