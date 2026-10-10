using System;
using System.Collections.Generic;
using System.Linq;
using MagicIsland.Farming;
using MagicIsland.CalendarSystem;
using MagicIsland.Inventory;
using UnityEngine;
namespace MagicIsland.Configuration
{
    public enum CropShape { Round, Bush, Tall }
    [Serializable]
    public sealed class CropConfig
    {
        public string Id, SeedItemId, HarvestItemId;
        [Min(1)] public int GrowthDays = 3;
        public CropShape Shape;
        public Color MatureColor = Color.white;
        public SeasonMask PlantingSeasons = SeasonMask.All;
        public string SeedlingPrefabAddress, GrowingPrefabAddress, MaturePrefabAddress;
        public string PrefabAddress(CropGrowthStage stage)
        {
            switch (stage)
            {
                case CropGrowthStage.Seedling: return SeedlingPrefabAddress;
                case CropGrowthStage.Growing: return GrowingPrefabAddress;
                case CropGrowthStage.Mature: return MaturePrefabAddress;
                default: throw new ArgumentOutOfRangeException(nameof(stage));
            }
        }
        public CropDefinition Definition() => new CropDefinition(Id, SeedItemId, HarvestItemId, GrowthDays);
    }
    [CreateAssetMenu(menuName = "Magic Island/Crop Catalog")]
    public sealed class CropCatalog : ScriptableObject
    {
        public const string Address = "CropCatalog";
        public List<CropConfig> Crops = new List<CropConfig>();
        public CropConfig Find(string id) => Crops.Find(crop => crop != null && crop.Id == id);
        public CropConfig FindBySeed(string id) => Crops.Find(crop => crop != null && crop.SeedItemId == id);
        public IEnumerable<CropDefinition> Definitions() => Crops.Select(crop => crop.Definition());
        public bool Validate(GameConfig game, ItemCatalog items)
        {
            var ids = new HashSet<string>(); var seeds = new HashSet<string>(); var harvests = new HashSet<string>();
            foreach (CropConfig crop in Crops)
            {
                if (crop == null || string.IsNullOrWhiteSpace(crop.Id) || !ids.Add(crop.Id) || !seeds.Add(crop.SeedItemId)
                    || !harvests.Add(crop.HarvestItemId) || crop.GrowthDays < 1 || !Enum.IsDefined(typeof(CropShape), crop.Shape)
                    || string.IsNullOrWhiteSpace(crop.SeedlingPrefabAddress) || string.IsNullOrWhiteSpace(crop.GrowingPrefabAddress)
                    || string.IsNullOrWhiteSpace(crop.MaturePrefabAddress) || !Seasons.ValidMask(crop.PlantingSeasons)) return false;
                ItemConfig seed = items.Find(crop.SeedItemId), harvest = items.Find(crop.HarvestItemId);
                if (seed?.Type != ItemType.Seed || harvest?.Type != ItemType.Crop || seed.BuyPrice < 1 || harvest.SellPrice < 1) return false;
            }
            CropConfig initial = FindBySeed(game.SeedItemId);
            return initial != null && initial.HarvestItemId == game.HarvestItemId && initial.GrowthDays == game.CropGrowthDays;
        }
    }
}
