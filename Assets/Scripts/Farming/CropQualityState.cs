using System;
using MagicIsland.Inventory;
using MagicIsland.Stamina;
namespace MagicIsland.Farming
{
    public enum CropQuality { Ordinary, Quality }
    public sealed class FertilizerDefinition
    {
        public string ItemId { get; }
        public int NormalChance { get; }
        public int FertilizedChance { get; }
        public int StaminaCost { get; }
        public FertilizerDefinition(string itemId = "basic_fertilizer", int normalChance = 10, int fertilizedChance = 40, int staminaCost = 2)
        {
            if (string.IsNullOrWhiteSpace(itemId) || normalChance < 0 || fertilizedChance <= normalChance || fertilizedChance > 100 || staminaCost < 0) throw new ArgumentException("Invalid fertilizer rules");
            ItemId = itemId; NormalChance = normalChance; FertilizedChance = fertilizedChance; StaminaCost = staminaCost;
        }
        public static int Roll(int seed, int index, int planting)
        {
            unchecked { uint value = (uint)seed ^ ((uint)index + 1) * 0x9e3779b9u ^ (uint)planting * 0x85ebca6bu; value ^= value >> 16; value *= 0x7feb352du; value ^= value >> 15; value *= 0x846ca68bu; value ^= value >> 16; return (int)(value % 100); }
        }
    }
    public enum FertilizerResult { Success, Busy, InvalidPlot, Untilled, AlreadyApplied, Mature, MissingFertilizer, Exhausted }
    public sealed class FertilizerState
    {
        private readonly FarmState farm;
        private readonly InventoryState bag;
        private readonly StaminaState stamina;
        private bool applying;
        public FertilizerState(FarmState farm, InventoryState bag, StaminaState stamina) { this.farm = farm; this.bag = bag; this.stamina = stamina; }
        public FertilizerResult Check(int index)
        {
            if (applying || !bag.CanSort) return FertilizerResult.Busy;
            if (index < 0 || index >= farm.Cells.Length) return FertilizerResult.InvalidPlot;
            var cell = farm.Cells[index];
            if (!cell.Tilled) return FertilizerResult.Untilled;
            if (cell.Mature) return FertilizerResult.Mature;
            if (cell.Fertilized) return FertilizerResult.AlreadyApplied;
            if (bag.GetCount(farm.Fertilizer.ItemId) < 1) return FertilizerResult.MissingFertilizer;
            return stamina != null && !stamina.CanSpend(farm.Fertilizer.StaminaCost) ? FertilizerResult.Exhausted : FertilizerResult.Success;
        }
        public FertilizerResult Apply(int index)
        {
            var check = Check(index); if (check != FertilizerResult.Success) return check;
            applying = true;
            try
            {
                farm.ApplyFertilizer(index);
                bag.TryRemove(farm.Fertilizer.ItemId, 1, false); stamina?.TrySpend(farm.Fertilizer.StaminaCost, false);
                bag.NotifyChanged(); if (farm.Fertilizer.StaminaCost > 0) stamina?.NotifyChanged(); return FertilizerResult.Success;
            }
            finally { applying = false; }
        }
    }
}
