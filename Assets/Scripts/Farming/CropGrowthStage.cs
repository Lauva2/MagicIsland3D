using System;

namespace MagicIsland.Farming
{
    public enum CropGrowthStage { Seedling, Growing, Mature }

    public static class CropStages
    {
        public static CropGrowthStage FromGrowth(int growth, int required)
        {
            if (required < 1 || growth < 0 || growth > required) throw new ArgumentException("Invalid crop growth");
            return growth == required ? CropGrowthStage.Mature : growth == 0 ? CropGrowthStage.Seedling : CropGrowthStage.Growing;
        }
    }
}
