using MagicIsland.Magic;
using UnityEngine;

namespace MagicIsland.Configuration
{
    [CreateAssetMenu(menuName = "Magic Island/Magic Config")]
    public sealed class MagicConfig : ScriptableObject
    {
        public const string Address = "MagicConfig";
        public string CropId = "moonflower", SeedId = "moonflower_seed", FlowerId = "moonflower", QualityFlowerId = "quality_moonflower";
        [Range(1, 99)] public int GiftSeeds = 3;
        [Range(1, 2)] public int RainRadius = 1;
        [Range(1, 100)] public int RainStamina = 6;
        public string RainEffectAddress = "MagicRainEffect";
        public MagicDefinition Definition() => new MagicDefinition(CropId, SeedId, FlowerId, QualityFlowerId, GiftSeeds, RainRadius, RainStamina);
        public bool Validate(ItemCatalog items, CropCatalog crops)
        {
            try { Definition(); } catch { return false; }
            var crop = crops?.Find(CropId);
            return !string.IsNullOrWhiteSpace(RainEffectAddress) && items != null && crop != null && crop.SeedItemId == SeedId
                && crop.HarvestItemId == FlowerId && crop.QualityHarvestItemId == QualityFlowerId;
        }
    }
}
