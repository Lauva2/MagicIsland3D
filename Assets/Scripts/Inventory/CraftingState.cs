using System;
using System.Collections.Generic;
using System.Linq;
namespace MagicIsland.Inventory
{
    public sealed class CraftingDefinition
    {
        public string Id { get; }
        public string Output { get; }
        public int Yield { get; }
        public IReadOnlyList<IngredientCost> Ingredients { get; }
        public CraftingDefinition(string id, string output, int yield, IEnumerable<IngredientCost> ingredients)
        {
            var costs = ingredients?.ToArray();
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(output) || yield < 1 || yield > 99
                || costs == null || costs.Length < 1 || costs.Length > 8 || costs.Any(c => c == null || c.Id == output)
                || costs.Select(c => c.Id).Distinct().Count() != costs.Length) throw new ArgumentException("Invalid crafting recipe");
            Id = id; Output = output; Yield = yield; Ingredients = Array.AsReadOnly(costs);
        }
    }
    public enum CraftingResult { Success, InvalidRecipe, MaterialsMissing, BagFull, Busy }
    public sealed class CraftingState
    {
        public IReadOnlyList<CraftingDefinition> Definitions { get; }
        private bool committing;
        public CraftingState(IEnumerable<CraftingDefinition> definitions)
        {
            var values = definitions?.ToArray();
            if (values == null || values.Length < 1 || values.Length > 12 || values.Any(d => d == null)
                || values.Select(d => d.Id).Distinct().Count() != values.Length || values.Select(d => d.Output).Distinct().Count() != values.Length)
                throw new ArgumentException("Invalid crafting catalog");
            Definitions = Array.AsReadOnly(values);
        }
        public CraftingResult Check(int index, InventoryState bag)
        {
            if (committing) return CraftingResult.Busy;
            if (index < 0 || index >= Definitions.Count) return CraftingResult.InvalidRecipe;
            var d = Definitions[index];
            if (d.Ingredients.Any(c => bag.GetCount(c.Id) < c.Quantity)) return CraftingResult.MaterialsMissing;
            return bag.CanExchange(d.Ingredients, d.Output, d.Yield) ? CraftingResult.Success : CraftingResult.BagFull;
        }
        public CraftingResult Craft(int index, InventoryState bag)
        {
            var result = Check(index, bag); if (result != CraftingResult.Success) return result;
            committing = true;
            try { var d = Definitions[index]; return bag.TryExchange(d.Ingredients, d.Output, d.Yield) ? CraftingResult.Success : CraftingResult.BagFull; }
            finally { committing = false; }
        }
    }
}
