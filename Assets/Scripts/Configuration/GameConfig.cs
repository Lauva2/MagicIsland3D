using UnityEngine;

namespace MagicIsland.Configuration
{
    [CreateAssetMenu(menuName = "Magic Island/Game Config")]
    public sealed class GameConfig : ScriptableObject
    {
        public const string Address = "GameConfig";
        public string GameSceneAddress = "Scenes/MainGame";
        [Range(1, 12)] public int FarmSize = 5;
        [Min(0)] public int InitialSeeds = 50;
        [Range(6, 36)] public int InventorySlots = 12;
        public string SeedItemId = "turnip_seed";
        public string HarvestItemId = "turnip";
        [Min(1)] public int CropGrowthDays = 3;
        [Min(0.1f)] public float PlayerSpeed = 4f;
        [Min(0.1f)] public float InteractionReach = 2.25f;
        [Range(15f, 75f)] public float CameraPitch = 42f;
        [Min(1f)] public float CameraSize = 7f;

        public bool IsValid => !string.IsNullOrWhiteSpace(GameSceneAddress) && FarmSize >= 1 && FarmSize <= 12
            && InitialSeeds >= 0 && InventorySlots >= 6 && InventorySlots <= 36
            && !string.IsNullOrWhiteSpace(SeedItemId) && !string.IsNullOrWhiteSpace(HarvestItemId)
            && CropGrowthDays >= 1 && PlayerSpeed > 0f && InteractionReach > 0f
            && CameraPitch >= 15f && CameraPitch <= 75f && CameraSize >= 1f;
    }
}
