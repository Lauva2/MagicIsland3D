using System;
namespace MagicIsland.Farming
{
    public sealed class CropDefinition
    {
        public string Id { get; }
        public string SeedId { get; }
        public string HarvestId { get; }
        public int GrowthDays { get; }
        public string QualityHarvestId { get; }
        public CropDefinition(string id, string seedId, string harvestId, int growthDays, string qualityHarvestId = null)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(seedId) || string.IsNullOrWhiteSpace(harvestId) || growthDays < 1)
                throw new ArgumentException("Invalid crop definition");
            if (qualityHarvestId == harvestId) throw new ArgumentException("Quality crop must have a separate item ID");
            Id = id; SeedId = seedId; HarvestId = harvestId; GrowthDays = growthDays; QualityHarvestId = qualityHarvestId;
        }
    }
}
