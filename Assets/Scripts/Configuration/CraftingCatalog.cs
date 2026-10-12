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
        public bool IsFurniture;
        public List<CraftingIngredientConfig> Ingredients = new List<CraftingIngredientConfig>();
        public CraftingDefinition Definition() => new CraftingDefinition(Id, OutputItemId, Yield, Ingredients.Select(c => new IngredientCost(c.ItemId, c.Quantity)));
    }
    [CreateAssetMenu(menuName = "Magic Island/Crafting Catalog")]
    public sealed class CraftingCatalog : ScriptableObject
    {
        public const string Address = "CraftingCatalog";
        public List<CraftingRecipeConfig> Recipes = new List<CraftingRecipeConfig>();
        public CraftingRecipeConfig FindItem(string id) => Recipes.Find(r => r.OutputItemId == id);
        public IEnumerable<CraftingDefinition> Definitions() => Recipes.Select(r => r.Definition());
        public IEnumerable<PlaceableDefinition> Placeables(bool furniture = false) => Recipes.Where(r => r.IsFurniture == furniture).Select(r => new PlaceableDefinition(r.OutputItemId));
        public bool Validate(ItemCatalog items)
        {
            if (items == null || Recipes == null || Recipes.Any(r => r == null || string.IsNullOrWhiteSpace(r.DisplayName)
                || string.IsNullOrWhiteSpace(r.PrefabAddress) || r.Ingredients == null || r.Ingredients.Any(c => c == null || items.Find(c.ItemId)?.Type != ItemType.Material)
                || items.Find(r.OutputItemId)?.Type != (r.IsFurniture ? ItemType.Furniture : ItemType.Placeable))) return false;
            try { new CraftingState(Definitions()); new PlacementState(Placeables(), 5); new PlacementState(Placeables(true), 5, indoor: true); return true; } catch (ArgumentException) { return false; }
        }
    }
}
