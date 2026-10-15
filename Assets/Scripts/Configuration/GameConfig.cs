using UnityEngine;

namespace MagicIsland.Configuration
{
    [CreateAssetMenu(menuName = "Magic Island/Game Config")]
    public sealed class GameConfig : ScriptableObject
    {
        [Min(0.1f)] public float SecondsPerGameMinute = 1f;
        [Min(0.1f)] public float ToolActionDuration = 0.55f;
        [Min(1)] public int MaxStamina = 100;
        [Min(0)] public int HoeStaminaCost = 3;
        [Min(0)] public int SeedStaminaCost = 1;
        [Min(0)] public int WaterStaminaCost = 2;
        [Min(0)] public int HarvestStaminaCost = 0;
        public const string Address = "GameConfig";
        public string GameSceneAddress = "Scenes/MainGame";
        [Range(1, 12)] public int FarmSize = 5;
        [Min(0)] public int InitialSeeds = 50;
        [Min(0)] public int InitialGold = 100;
        [Range(6, 36)] public int InventorySlots = 10;
        public string SeedItemId = "turnip_seed";
        public string HarvestItemId = "turnip";
        [Min(1)] public int CropGrowthDays = 3;
        [Min(0.1f)] public float PlayerSpeed = 4f;
        public string PlayerPrefabAddress = "PlayerCharacter";
        public string InteractionHighlightAddress = "InteractionHighlight";
        public string OceanPrefabAddress = "IslandOcean";
        public string TerrainPrefabAddress = "IslandTerrain";
        public string ShopPrefabAddress = "IslandShop";
        public string FarmPlotPrefabAddress = "FarmPlot";
        public string ToolEffectsPrefabAddress = "FarmActionEffects";
        [Min(0.1f)] public float InteractionReach = 2.25f;
        [Range(15f, 75f)] public float CameraPitch = 42f;
        [Min(1f)] public float CameraSize = 7f;
        [Range(.05f, .8f)] public float CameraOccludedOpacity = .22f;
        [Range(.05f, 1f)] public float CameraFadeDuration = .2f;
        [Range(0f, .3f)] public float CameraOcclusionMargin = .08f;

        public bool IsValid => !string.IsNullOrWhiteSpace(GameSceneAddress) && FarmSize >= 1 && FarmSize <= 12
            && !string.IsNullOrWhiteSpace(OceanPrefabAddress) && !string.IsNullOrWhiteSpace(TerrainPrefabAddress)
            && !string.IsNullOrWhiteSpace(ShopPrefabAddress)
            && !string.IsNullOrWhiteSpace(FarmPlotPrefabAddress) && !string.IsNullOrWhiteSpace(ToolEffectsPrefabAddress)
            && ToolActionDuration >= 0.1f && !float.IsInfinity(ToolActionDuration)
            && MaxStamina > 0 && HoeStaminaCost >= 0 && SeedStaminaCost >= 0 && WaterStaminaCost >= 0 && HarvestStaminaCost >= 0
            && InitialSeeds >= 0 && InitialGold >= 0 && InventorySlots >= 6 && InventorySlots <= 36
            && !string.IsNullOrWhiteSpace(SeedItemId) && !string.IsNullOrWhiteSpace(HarvestItemId) && !string.IsNullOrWhiteSpace(PlayerPrefabAddress) && !string.IsNullOrWhiteSpace(InteractionHighlightAddress)
            && SecondsPerGameMinute >= 0.1f && !float.IsInfinity(SecondsPerGameMinute) && CropGrowthDays >= 1 && PlayerSpeed > 0f && InteractionReach > 0f
            && CameraPitch >= 15f && CameraPitch <= 75f && CameraSize >= 1f
            && CameraOccludedOpacity >= .05f && CameraOccludedOpacity <= .8f
            && CameraFadeDuration >= .05f && CameraFadeDuration <= 1f
            && CameraOcclusionMargin >= 0f && CameraOcclusionMargin <= .3f;
    }
}
