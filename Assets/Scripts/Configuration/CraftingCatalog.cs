using System;
using System.Collections.Generic;
using System.Linq;
using MagicIsland.Inventory;
using MagicIsland.Interaction;
using UnityEngine;
namespace MagicIsland.Configuration
{
    [Serializable] public sealed class CraftingIngredientConfig { public string ItemId; public int Quantity = 1; }
    [Serializable] public sealed class CraftingRecipeConfig
    {
        public string Id, DisplayName, OutputItemId, PrefabAddress;
        public int Yield = 1;
        public int Clearance;
        public bool IsFurniture;
        public bool IsSupply;
        public bool IsFarmingSupply;
        public bool IsPlaceable => !IsSupply;
        public List<CraftingIngredientConfig> Ingredients = new List<CraftingIngredientConfig>();
        public CraftingDefinition Definition() => new CraftingDefinition(Id, OutputItemId, Yield, Ingredients.Select(c => new IngredientCost(c.ItemId, c.Quantity)));
    }
    [CreateAssetMenu(menuName = "Magic Island/Crafting Catalog")]
    public sealed class CraftingCatalog : ScriptableObject
    {
        public const string Address = "CraftingCatalog";
        public List<CraftingRecipeConfig> Recipes = new List<CraftingRecipeConfig>();
        public List<CraftingRecipeConfig> PurchasedPlaceables = new List<CraftingRecipeConfig>();
        public IEnumerable<CraftingRecipeConfig> PlacementEntries => Recipes.Concat(PurchasedPlaceables);
        public CraftingRecipeConfig FindItem(string id) => PlacementEntries.FirstOrDefault(r => r.OutputItemId == id);
        public IEnumerable<CraftingDefinition> Definitions() => Recipes.Select(r => r.Definition());
        public IEnumerable<PlaceableDefinition> Placeables(bool furniture = false) => PlacementEntries.Where(r => r.IsPlaceable && r.IsFurniture == furniture).Select(r => new PlaceableDefinition(r.OutputItemId, r.Clearance));
        public bool Validate(ItemCatalog items)
        {
            if (items == null || Recipes == null || Recipes.Any(r => r == null) || PurchasedPlaceables == null || PurchasedPlaceables.Any(r => r == null || !r.IsPlaceable || r.IsFurniture || string.IsNullOrWhiteSpace(r.DisplayName) || string.IsNullOrWhiteSpace(r.PrefabAddress) || items.Find(r.OutputItemId)?.Type != ItemType.Placeable)
                || PlacementEntries.Select(r => r.OutputItemId).Distinct().Count() != PlacementEntries.Count() || Recipes.Any(r => r == null || string.IsNullOrWhiteSpace(r.DisplayName)
                || (r.IsSupply && r.IsFurniture) || (r.IsFarmingSupply && !r.IsSupply) || (r.IsPlaceable && string.IsNullOrWhiteSpace(r.PrefabAddress))
                || r.Ingredients == null || r.Ingredients.Any(c => c == null || !IngredientAllowed(items.Find(c.ItemId), r.IsSupply))
                || items.Find(r.OutputItemId)?.Type != (r.IsSupply ? ItemType.Material : r.IsFurniture ? ItemType.Furniture : ItemType.Placeable))) return false;
            try { new CraftingState(Definitions()); new PlacementState(Placeables(), 5); new PlacementState(Placeables(true), 5, indoor: true); return true; } catch (ArgumentException) { return false; }
        }
        private static bool IngredientAllowed(ItemConfig item, bool supply) => item != null && (item.Type == ItemType.Material || (supply && item.Type == ItemType.Crop));
    }
}
