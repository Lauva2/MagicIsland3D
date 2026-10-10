using System;
namespace MagicIsland.Farming
{
    public sealed class CropDefinition
    {
        public string Id { get; }
        public string SeedId { get; }
        public string HarvestId { get; }
        public int GrowthDays { get; }
        public CropDefinition(string id, string seedId, string harvestId, int growthDays)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(seedId) || string.IsNullOrWhiteSpace(harvestId) || growthDays < 1)
                throw new ArgumentException("Invalid crop definition");
            Id = id; SeedId = seedId; HarvestId = harvestId; GrowthDays = growthDays;
        }
    }
}
