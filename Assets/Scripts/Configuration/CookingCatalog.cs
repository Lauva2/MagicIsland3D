using System;
using System.Collections.Generic;
using System.Linq;
using MagicIsland.Inventory;
using UnityEngine;
namespace MagicIsland.Configuration
{
    [Serializable] public sealed class CookingRecipeConfig
    {
        public string Id, DisplayName, IngredientId, FoodId;
        public int IngredientQuantity = 1, OutputQuantity = 1, StaminaRecovery = 20;
        public string RequiredEventId;
        public List<CookingIngredientConfig> AdditionalIngredients = new List<CookingIngredientConfig>();
        public CookingDefinition Definition() => new CookingDefinition(Id,
            new[] { new IngredientCost(IngredientId, IngredientQuantity) }.Concat((AdditionalIngredients ?? new List<CookingIngredientConfig>()).Select(c => new IngredientCost(c.ItemId, c.Quantity))),
            FoodId, OutputQuantity, StaminaRecovery, RequiredEventId);
    }
    [Serializable] public sealed class CookingIngredientConfig { public string ItemId; public int Quantity = 1; }
    [CreateAssetMenu(menuName = "Magic Island/Cooking Catalog")]
    public sealed class CookingCatalog : ScriptableObject
    {
        public const string Address = "CookingCatalog";
        public string StationAddress = "CookingStation";
        public List<CookingRecipeConfig> Recipes = new List<CookingRecipeConfig>();
        public IEnumerable<CookingDefinition> Definitions() => Recipes.Select(r => r.Definition());
        public bool Validate(ItemCatalog items)
        {
            if (Recipes == null || Recipes.Count < 1 || Recipes.Count > 12 || string.IsNullOrWhiteSpace(StationAddress)) return false;
            try { return Recipes.All(r => r != null && !string.IsNullOrWhiteSpace(r.DisplayName) && IsIngredient(items.Find(r.IngredientId))
                && (r.AdditionalIngredients == null || r.AdditionalIngredients.All(c => c != null && IsIngredient(items.Find(c.ItemId))))
                && items.Find(r.FoodId)?.Type == ItemType.Food && r.OutputQuantity <= items.Find(r.FoodId).MaxStack) && new CookingState(Definitions()) != null; }
            catch (ArgumentException) { return false; }
        }
        private static bool IsIngredient(ItemConfig item) => item != null && (item.Type == ItemType.Crop || item.Type == ItemType.Fish || item.Type == ItemType.AnimalProduct);
    }
}
