using UnityEngine;
using MagicIsland.Interaction;
using MagicIsland.Inventory;
namespace MagicIsland.Configuration
{
    [CreateAssetMenu(menuName = "Magic Island/Housing Config")]
    public sealed class HousingConfig : ScriptableObject
    {
        public const string Address = "HousingConfig";
        public string ExteriorAddress = "PlayerHouse", InteriorSceneAddress = "Scenes/PlayerHome";
        public Vector3 InteriorOrigin = new Vector3(40, 0, 0);
        public int InteriorLayer = 8;
        public string ExteriorUpgradeAddress = "PlayerHouseExpansion", InteriorUpgradeAddress = "PlayerHomeExpansion";
        public int UpgradeWood = 20, UpgradeStone = 10, UpgradeGold = 300, BuildDays = 1;
        public HouseUpgradeDefinition UpgradeDefinition() => new HouseUpgradeDefinition(UpgradeWood, UpgradeStone, UpgradeGold, BuildDays);
        public bool Validate(ItemCatalog items) => items != null && IsValid && items.Find("wood")?.Type == ItemType.Material && items.Find("stone")?.Type == ItemType.Material;
        public Vector3 ExteriorPosition(int farmSize) => new Vector3(-farmSize * .6f - 1.5f, 0, -4.6f);
        public bool IsValid => !string.IsNullOrWhiteSpace(ExteriorAddress) && !string.IsNullOrWhiteSpace(InteriorSceneAddress)
            && InteriorLayer >= 8 && InteriorLayer <= 31 && Finite(InteriorOrigin.x) && Finite(InteriorOrigin.y) && Finite(InteriorOrigin.z)
            && InteriorOrigin.y == 0 && Mathf.Abs(InteriorOrigin.x) <= 1000 && Mathf.Abs(InteriorOrigin.z) <= 1000
            && (Mathf.Abs(InteriorOrigin.x) >= 35 || Mathf.Abs(InteriorOrigin.z) >= 35)
            && UpgradeWood > 0 && UpgradeStone > 0 && UpgradeGold > 0 && BuildDays >= 1 && BuildDays <= 30
            && !string.IsNullOrWhiteSpace(ExteriorUpgradeAddress) && !string.IsNullOrWhiteSpace(InteriorUpgradeAddress);
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
