using System;
using MagicIsland.Farming;
using MagicIsland.Inventory;

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
        public const int CurrentVersion = 1;
        public int Version;
        public string SavedUtc;
        public int FarmSize;
        public int Day;
        public FarmCell[] Cells;
        public SavedSlot[] Slots;
        public float PlayerX, PlayerY, PlayerZ;

        public void Validate(int farmSize, int growthDays, InventoryState inventory)
        {
            if (Version != CurrentVersion) throw new InvalidOperationException("Unsupported save version");
            if (FarmSize != farmSize || Day < 1 || Day == int.MaxValue || Cells == null || Cells.Length != farmSize * farmSize)
                throw new InvalidOperationException("Invalid saved farm dimensions or day");
            foreach (FarmCell cell in Cells)
                if (cell == null || cell.RequiredGrowthDays != growthDays || cell.Growth < 0 || cell.Growth > growthDays
                    || (cell.Planted && !cell.Tilled) || (cell.Watered && !cell.Planted)
                    || (!cell.Planted && cell.Growth != 0))
                    throw new InvalidOperationException("Invalid saved farm cell");
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

        public static SaveData Capture(FarmState farm, InventoryState inventory, float x, float y, float z)
        {
            var data = new SaveData { Version = CurrentVersion, SavedUtc = DateTime.UtcNow.ToString("O"),
                FarmSize = farm.Size, Day = farm.Day, PlayerX = x, PlayerY = y, PlayerZ = z,
                Cells = new FarmCell[farm.Cells.Length], Slots = new SavedSlot[inventory.Capacity] };
            for (int i = 0; i < data.Cells.Length; i++)
            {
                FarmCell cell = farm.Cells[i];
                data.Cells[i] = new FarmCell { Tilled = cell.Tilled, Planted = cell.Planted, Watered = cell.Watered,
                    Growth = cell.Growth, RequiredGrowthDays = cell.RequiredGrowthDays };
            }
            for (int i = 0; i < data.Slots.Length; i++)
                data.Slots[i] = new SavedSlot { ItemId = inventory.Slots[i].ItemId, Quantity = inventory.Slots[i].Quantity };
            return data;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
