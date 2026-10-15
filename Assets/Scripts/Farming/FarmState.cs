using System;
using System.Collections.Generic;

namespace MagicIsland.Farming
{
    public enum FarmTool { Hoe, Seeds, WateringCan, Axe, Pickaxe, FishingRod, Fertilizer }

    [Serializable]
    public sealed class FarmCell
    {
        public bool Tilled;
        public string CropId;
        public bool Planted;
        public bool Watered;
        public int Growth;
        public int RequiredGrowthDays = FarmState.GrowthDays;
        public bool Fertilized;
        public CropQuality Quality;
        public int PlantingNumber;
        public bool Mature => Planted && Growth >= RequiredGrowthDays;
    }

    // Pure gameplay state: no scene objects, input or visual dependencies.
    public sealed class FarmState
    {
        public const int GrowthDays = 3;
        public int Size { get; }
        public int Day { get; private set; } = 1;
        public FarmCell[] Cells { get; }
        public string DefaultCropId { get; }
        public int QualitySeed { get; }
        public FertilizerDefinition Fertilizer { get; }
        public string HarvestId(int index) { var cell = Cells[index]; var crop = FindCrop(cell.CropId); return cell.Quality == CropQuality.Quality ? crop.QualityHarvestId : crop.HarvestId; }
        private readonly Dictionary<string, CropDefinition> definitions = new Dictionary<string, CropDefinition>(StringComparer.Ordinal);
        public CropDefinition FindCrop(string id) => id != null && definitions.TryGetValue(id, out CropDefinition crop) ? crop : null;

        public FarmState(int size, int growthDays = GrowthDays, IEnumerable<CropDefinition> crops = null, string defaultCropId = "turnip", FertilizerDefinition fertilizer = null, int qualitySeed = 1)
        {
            if (size <= 0) throw new ArgumentOutOfRangeException(nameof(size));
            if (growthDays < 1) throw new ArgumentOutOfRangeException(nameof(growthDays));
            Size = size;
            if (qualitySeed < 1) throw new ArgumentException("Invalid crop quality seed");
            QualitySeed = qualitySeed; Fertilizer = fertilizer ?? new FertilizerDefinition();
            if (crops == null) crops = new[] { new CropDefinition(defaultCropId, "turnip_seed", "turnip", growthDays) };
            foreach (CropDefinition crop in crops)
                if (crop == null || definitions.ContainsKey(crop.Id)) throw new ArgumentException("Duplicate or missing crop");
                else definitions.Add(crop.Id, crop);
            DefaultCropId = defaultCropId;
            if (FindCrop(defaultCropId)?.GrowthDays != growthDays) throw new ArgumentException("Invalid default crop");
            Cells = new FarmCell[size * size];
            for (int i = 0; i < Cells.Length; i++) Cells[i] = new FarmCell { RequiredGrowthDays = growthDays };
        }

        public string Describe(int index, FarmTool tool)
        {
            FarmCell cell = Cells[index];
            if (cell.Mature) return "收获";
            if (tool == FarmTool.Axe || tool == FarmTool.Pickaxe) return "请选择锄头、种子或水壶操作农田";
            switch (tool)
            {
                case FarmTool.Hoe: return cell.Tilled ? "土地已耕作" : "耕地";
                case FarmTool.Seeds:
                    if (!cell.Tilled) return "先用锄头耕地";
                    if (cell.Planted) return "已有作物";
                    return "播种";
                default:
                    if (!cell.Planted) return "先种植作物";
                    return cell.Watered ? "今天已浇水" : "浇水";
            }
        }

        public bool TryAct(int index, FarmTool tool, out string message, string cropId = null)
        {
            message = Describe(index, tool);
            FarmCell cell = Cells[index];
            if (tool == FarmTool.Fertilizer) { message = "使用施肥系统操作农田"; return false; }
            if (cell.Mature)
            {
                cell.Planted = false;
                cell.Watered = false;
                cell.Growth = 0;
                cell.CropId = null;
                cell.RequiredGrowthDays = definitions[DefaultCropId].GrowthDays;
                cell.Fertilized = false; cell.Quality = CropQuality.Ordinary;
                return true;
            }
            if (tool == FarmTool.Hoe && !cell.Tilled)
            {
                cell.Tilled = true;
                return true;
            }
            if (tool == FarmTool.Seeds && cell.Tilled && !cell.Planted)
            {
                CropDefinition crop = FindCrop(cropId ?? DefaultCropId);
                if (crop == null) { message = "无法种植这种种子"; return false; }
                if (cell.PlantingNumber == int.MaxValue) { message = "此地块无法继续播种"; return false; }
                cell.PlantingNumber++;
                cell.Planted = true;
                cell.CropId = crop.Id;
                cell.RequiredGrowthDays = crop.GrowthDays;
                ResolveQuality(index);
                return true;
            }
            if (tool == FarmTool.WateringCan && cell.Planted && !cell.Watered)
            {
                cell.Watered = true;
                return true;
            }
            return false;
        }

        public bool WaterPlanted()
        {
            bool changed = false;
            foreach (FarmCell cell in Cells)
                if (cell.Planted && !cell.Watered) { cell.Watered = true; changed = true; }
            return changed;
        }
        private void ResolveQuality(int index)
        {
            var cell = Cells[index]; var crop = FindCrop(cell.CropId);
            cell.Quality = crop.QualityHarvestId != null && FertilizerDefinition.Roll(QualitySeed, index, cell.PlantingNumber) < (cell.Fertilized ? Fertilizer.FertilizedChance : Fertilizer.NormalChance) ? CropQuality.Quality : CropQuality.Ordinary;
        }
        internal void ApplyFertilizer(int index) { Cells[index].Fertilized = true; if (Cells[index].Planted) ResolveQuality(index); }
        public static bool ValidQuality(FarmCell cell, CropDefinition crop) => cell != null && cell.PlantingNumber >= 0 && Enum.IsDefined(typeof(CropQuality), cell.Quality)
            && (!cell.Fertilized || cell.Tilled) && (cell.Planted || cell.Quality == CropQuality.Ordinary)
            && (cell.Quality != CropQuality.Quality || !string.IsNullOrWhiteSpace(crop?.QualityHarvestId));

        public void AdvanceDay()
        {
            foreach (FarmCell cell in Cells)
            {
                if (cell.Planted && cell.Watered && !cell.Mature) cell.Growth++;
                cell.Watered = false;
            }
            Day++;
        }

        public void Restore(int day, FarmCell[] saved)
        {
            if (day < 1 || saved == null || saved.Length != Cells.Length) throw new ArgumentException("Invalid saved farm");
            for (int i = 0; i < saved.Length; i++)
            {
                FarmCell cell = saved[i];
                CropDefinition crop = cell != null && cell.Planted ? FindCrop(cell.CropId) : definitions[DefaultCropId];
                if (cell == null || crop == null || !ValidQuality(cell, crop) || cell.RequiredGrowthDays != crop.GrowthDays
                    || cell.Growth < 0 || cell.Growth > cell.RequiredGrowthDays
                    || (cell.Planted && !cell.Tilled) || (cell.Watered && !cell.Planted)
                    || (!cell.Planted && (cell.Growth != 0 || !string.IsNullOrEmpty(cell.CropId)))) throw new ArgumentException("Invalid saved farm cell");
            }
            Day = day;
            for (int i = 0; i < saved.Length; i++)
            {
                Cells[i].Tilled = saved[i].Tilled;
                Cells[i].Planted = saved[i].Planted;
                Cells[i].Watered = saved[i].Watered;
                Cells[i].Growth = saved[i].Growth;
                Cells[i].CropId = saved[i].CropId;
                Cells[i].RequiredGrowthDays = saved[i].RequiredGrowthDays;
                Cells[i].Fertilized = saved[i].Fertilized; Cells[i].Quality = saved[i].Quality; Cells[i].PlantingNumber = saved[i].PlantingNumber;
            }
        }
    }
}
