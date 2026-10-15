using MagicIsland.Inventory;
using UnityEngine;
namespace MagicIsland.Configuration
{
    [CreateAssetMenu(menuName = "Magic Island/Inventory Upgrade Config")]
    public sealed class InventoryUpgradeConfig : ScriptableObject
    {
        public const string Address = "InventoryUpgradeConfig";
        public int[] GoldCosts = { 300, 800 };
        public InventoryUpgradeDefinition Definition() => new InventoryUpgradeDefinition(GoldCosts);
        public bool IsValid { get { try { Definition(); return true; } catch (System.ArgumentException) { return false; } } }
    }
}
