using MagicIsland.Inventory;
using MagicIsland.Stamina;
using System;
using System.Collections.Generic;

namespace MagicIsland.Farming
{
    // Coordinate farm changes with inventory changes before notifying inventory observers.
    public sealed class FarmActions
    {
        private readonly FarmState farm;
        private readonly InventoryState inventory;
        private readonly Dictionary<string, CropDefinition> definitions = new Dictionary<string, CropDefinition>();
        public CropDefinition SelectedCrop { get; private set; }
        private string SeedId => SelectedCrop.SeedId;
        private string HarvestId(FarmCell cell) => cell.Quality == CropQuality.Quality ? definitions[cell.CropId].QualityHarvestId : definitions[cell.CropId].HarvestId;
        private readonly StaminaState stamina;
        private readonly int hoeCost, seedCost, waterCost, harvestCost;
        private bool acting;
        private Action harvestRecorder;
        private Action<string> magicHarvestRecorder;
        internal void SetMagicHarvestRecorder(Action<string> recorder) => magicHarvestRecorder = recorder;
        internal void SetHarvestRecorder(Action recorder) => harvestRecorder = recorder;
        private readonly Func<CropDefinition, bool> canPlant;
        public FarmActions(FarmState farm, InventoryState inventory, string seedId, string cropId,
            StaminaState stamina = null, int hoeCost = 3, int seedCost = 1, int waterCost = 2, int harvestCost = 0,
            IEnumerable<CropDefinition> crops = null, Func<CropDefinition, bool> canPlant = null)
        {
            if (hoeCost < 0 || seedCost < 0 || waterCost < 0 || harvestCost < 0) throw new ArgumentOutOfRangeException(nameof(hoeCost));
            this.farm = farm; this.inventory = inventory;
            this.canPlant = canPlant ?? (_ => true);
            if (crops == null) crops = new[] { new CropDefinition(farm.DefaultCropId, seedId, cropId, farm.FindCrop(farm.DefaultCropId).GrowthDays) };
            var seeds = new HashSet<string>();
            foreach (CropDefinition crop in crops)
            {
                if (crop == null || definitions.ContainsKey(crop.Id) || !seeds.Add(crop.SeedId)
                    || farm.FindCrop(crop.Id)?.GrowthDays != crop.GrowthDays) throw new ArgumentException("Invalid crop mapping");
                definitions.Add(crop.Id, crop);
            }
            SelectedCrop = definitions[farm.DefaultCropId];
            this.stamina = stamina; this.hoeCost = hoeCost; this.seedCost = seedCost; this.waterCost = waterCost; this.harvestCost = harvestCost;
        }
        public bool SelectSeed(string seedId, bool requireStock = true)
        {
            if (acting || (requireStock && inventory.GetCount(seedId) < 1)) return false;
            foreach (CropDefinition crop in definitions.Values)
                if (crop.SeedId == seedId) { SelectedCrop = crop; return true; }
            return false;
        }

        private int Cost(FarmCell cell, FarmTool tool) => cell.Mature ? harvestCost
            : tool == FarmTool.Hoe ? hoeCost : tool == FarmTool.Seeds ? seedCost : waterCost;
        private bool CanAct(FarmCell cell, FarmTool tool)
        {
            if (cell.Mature) return inventory.CanAdd(HarvestId(cell), 1);
            if (tool == FarmTool.Hoe) return !cell.Tilled;
            if (tool == FarmTool.Seeds) return cell.Tilled && !cell.Planted && inventory.GetCount(SeedId) > 0 && canPlant(SelectedCrop);
            return tool == FarmTool.WateringCan && cell.Planted && !cell.Watered;
        }

        public bool CanPerform(int index, FarmTool tool)
        {
            if (acting || index < 0 || index >= farm.Cells.Length || tool == FarmTool.Fertilizer || tool == FarmTool.FishingRod) return false;
            var cell = farm.Cells[index];
            return CanAct(cell, tool) && (stamina == null || stamina.CanSpend(Cost(cell, tool)));
        }
        public string Describe(int index, FarmTool tool)
        {
            FarmCell cell = farm.Cells[index];
            if (CanAct(cell, tool) && stamina != null && !stamina.CanSpend(Cost(cell, tool))) return "体力不足，请到床边休息";
            if (cell.Mature) return inventory.CanAdd(HarvestId(cell), 1) ? "收获" : "背包已满，先腾出空间";
            if (tool == FarmTool.Seeds && cell.Tilled && !cell.Planted && !canPlant(SelectedCrop))
                return "当前季节不适宜播种，请选择适宜的种子";
            if (tool == FarmTool.Seeds && cell.Tilled && !cell.Planted && inventory.GetCount(SeedId) < 1)
                return "种子不足";
            return farm.Describe(index, tool);
        }

        public bool TryAct(int index, FarmTool tool, out string message)
        {
            if (tool == FarmTool.Fertilizer) { message = "使用施肥系统操作农田"; return false; }
            message = Describe(index, tool);
            FarmCell cell = farm.Cells[index];
            int cost = Cost(cell, tool);
            if (acting || !CanAct(cell, tool) || (stamina != null && !stamina.CanSpend(cost))) return false;
            acting = true;
            try
            {
                bool harvest = cell.Mature;
                string harvestedId = harvest ? HarvestId(cell) : null;
                string harvestedCrop = harvest ? cell.CropId : null;
                string usedSeed = SeedId;
                if (!farm.TryAct(index, tool, out message, SelectedCrop.Id)) return false;
                // All preflight checks passed. Commit stamina before inventory observers run.
                stamina?.TrySpend(cost, false);
                if (harvest) { harvestRecorder?.Invoke(); magicHarvestRecorder?.Invoke(harvestedCrop); inventory.TryAdd(harvestedId, 1); }
                else if (tool == FarmTool.Seeds) inventory.TryRemove(usedSeed, 1);
                if (cost > 0) stamina?.NotifyChanged();
                return true;
            }
            finally { acting = false; }
        }
    }
}
