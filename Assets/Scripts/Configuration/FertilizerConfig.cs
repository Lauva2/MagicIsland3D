using MagicIsland.Farming;
using MagicIsland.Inventory;
using UnityEngine;
namespace MagicIsland.Configuration
{
    [CreateAssetMenu(menuName = "Magic Island/Fertilizer Config")]
    public sealed class FertilizerConfig : ScriptableObject
    {
        public const string Address = "FertilizerConfig";
        public string ItemId = "basic_fertilizer";
        public int NormalChance = 10, FertilizedChance = 40, StaminaCost = 2;
        public FertilizerDefinition Definition() => new FertilizerDefinition(ItemId, NormalChance, FertilizedChance, StaminaCost);
        public bool Validate(ItemCatalog items)
        { try { Definition(); return items.Find(ItemId)?.Type == ItemType.Material; } catch (System.ArgumentException) { return false; } }
    }
}
