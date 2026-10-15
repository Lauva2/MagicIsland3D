using System;
using System.Collections.Generic;
using System.Linq;
using MagicIsland.Stamina;
namespace MagicIsland.Inventory
{
    public sealed class CookingDefinition
    {
        public string Id { get; }
        public string Ingredient { get; }
        public string Food { get; }
        public int Cost { get; }
        public int Yield { get; }
        public int Recovery { get; }
        public IReadOnlyList<IngredientCost> Ingredients { get; }
        public string RequiredEventId { get; }
        public CookingDefinition(string id, string ingredient, int cost, string food, int yield, int recovery)
            : this(id, new[] { new IngredientCost(ingredient, cost) }, food, yield, recovery) { }
        public CookingDefinition(string id, IEnumerable<IngredientCost> ingredients, string food, int yield, int recovery, string requiredEvent = null)
        {
            var costs = ingredients?.ToArray();
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(food) || costs == null || costs.Length < 1 || costs.Length > 8
                || costs.Any(c => c == null || c.Id == food) || costs.Select(c => c.Id).Distinct().Count() != costs.Length
                || yield < 1 || recovery < 1) throw new ArgumentException("Invalid cooking definition");
            Id = id; Ingredients = Array.AsReadOnly(costs); Ingredient = costs[0].Id; Cost = costs[0].Quantity; Food = food; Yield = yield; Recovery = recovery; RequiredEventId = requiredEvent;
        }
    }
    public enum CookingResult { Success, InvalidRecipe, InsufficientIngredients, BagFull, Busy, Locked }
    public sealed class CookingState
    {
        public IReadOnlyList<CookingDefinition> Definitions { get; }
        private bool cooking;
        public IngredientResolver Ingredients { get; }
        private readonly Func<string, bool> eventCompleted;
        public CookingState(IEnumerable<CookingDefinition> definitions, Func<string, bool> eventCompleted = null, IngredientResolver ingredients = null)
        {
            this.eventCompleted = eventCompleted; Ingredients = ingredients ?? new IngredientResolver();
            var values = definitions?.ToArray() ?? throw new ArgumentNullException(nameof(definitions));
            if (values.Length == 0 || values.Any(d => d == null) || values.Select(d => d.Id).Distinct().Count() != values.Length
                || values.Select(d => d.Food).Distinct().Count() != values.Length) throw new ArgumentException("Invalid recipes");
            Definitions = Array.AsReadOnly(values);
        }
        public CookingResult Check(int index, InventoryState bag)
        {
            if (cooking) return CookingResult.Busy;
            if (index < 0 || index >= Definitions.Count) return CookingResult.InvalidRecipe;
            var recipe = Definitions[index];
            if (!string.IsNullOrEmpty(recipe.RequiredEventId) && !(eventCompleted?.Invoke(recipe.RequiredEventId) ?? false)) return CookingResult.Locked;
            if (!Ingredients.Resolve(bag, recipe.Ingredients, out var costs)) return CookingResult.InsufficientIngredients;
            return bag.CanExchange(costs, recipe.Food, recipe.Yield) ? CookingResult.Success : CookingResult.BagFull;
        }
        public CookingResult Cook(int index, InventoryState bag)
        {
            var check = Check(index, bag); if (check != CookingResult.Success) return check;
            cooking = true;
            try { var recipe = Definitions[index]; Ingredients.Resolve(bag, recipe.Ingredients, out var costs); return bag.TryExchange(costs, recipe.Food, recipe.Yield) ? CookingResult.Success : CookingResult.BagFull; }
            finally { cooking = false; }
        }
    }
    public enum FoodResult { Success, InvalidFood, StaminaFull, Busy }
    public sealed class FoodState
    {
        private readonly Dictionary<string, int> foods;
        private bool eating;
        public int LastRecovery { get; private set; }
        public FoodState(IEnumerable<CookingDefinition> recipes)
        { foods = new CookingState(recipes).Definitions.ToDictionary(d => d.Food, d => d.Recovery); }
        public int Recovery(string id) => id != null && foods.TryGetValue(id, out var recovery) ? recovery : 0;
        public FoodResult Check(int index, InventoryState bag, StaminaState stamina)
        {
            if (eating) return FoodResult.Busy;
            if (index < 0 || index >= bag.Capacity || bag.Slots[index].Empty || Recovery(bag.Slots[index].ItemId) == 0) return FoodResult.InvalidFood;
            return stamina.Current == stamina.Maximum ? FoodResult.StaminaFull : FoodResult.Success;
        }
        public FoodResult Use(int index, InventoryState bag, StaminaState stamina)
        {
            var check = Check(index, bag, stamina); if (check != FoodResult.Success) return check;
            eating = true;
            try
            {
                int before = stamina.Current, amount = Recovery(bag.Slots[index].ItemId);
                bag.TryRemoveSlot(index, 1, false); stamina.TryRecover(amount, false); LastRecovery = stamina.Current - before;
                bag.NotifyChanged(); stamina.NotifyChanged(); return FoodResult.Success;
            }
            finally { eating = false; }
        }
    }
}
