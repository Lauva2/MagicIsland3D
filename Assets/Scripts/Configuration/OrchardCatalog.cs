using System;
using System.Collections.Generic;
using System.Linq;
using MagicIsland.Interaction;
using MagicIsland.Inventory;
using UnityEngine;
namespace MagicIsland.Configuration
{
    [Serializable] public sealed class OrchardTypeConfig
    {
        public string SaplingId, FruitId, DisplayName;
        public int GrowthDays = 3, Interval = 2, Yield = 2;
        public OrchardDefinition Definition() => new OrchardDefinition(SaplingId, FruitId, GrowthDays, Interval, Yield);
    }
    [CreateAssetMenu(menuName = "Magic Island/Orchard Catalog")]
    public sealed class OrchardCatalog : ScriptableObject
    {
        public const string Address = "OrchardCatalog";
        public List<OrchardTypeConfig> Types = new List<OrchardTypeConfig>();
        public IEnumerable<OrchardDefinition> Definitions() => Types.Select(t => t.Definition());
        public OrchardTypeConfig Find(string sapling) => Types.Find(t => t.SaplingId == sapling);
        public bool Validate(ItemCatalog items, CraftingCatalog crafting)
        {
            if (Types == null || Types.Count < 1 || Types.Count > 8 || Types.Any(t => t == null || string.IsNullOrWhiteSpace(t.DisplayName)
                || items.Find(t.SaplingId)?.Type != ItemType.Placeable || items.Find(t.SaplingId).BuyPrice <= 0 || items.Find(t.FruitId)?.Type != ItemType.Crop || items.Find(t.FruitId).SellPrice <= 0
                || crafting.FindItem(t.SaplingId) == null || crafting.FindItem(t.SaplingId).IsFurniture || !crafting.PurchasedPlaceables.Any(r => r.OutputItemId == t.SaplingId))
                || Types.Select(t => t.SaplingId).Distinct().Count() != Types.Count || Types.Select(t => t.FruitId).Distinct().Count() != Types.Count) return false;
            try { foreach (var type in Types) type.Definition(); return true; } catch (ArgumentException) { return false; }
        }
    }
}
