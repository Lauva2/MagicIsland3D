using System;
using System.Collections.Generic;
using System.Linq;

namespace MagicIsland.Inventory
{
    // One ingredient may accept a better quality item; always keep that item until needed.
    public sealed class IngredientResolver
    {
        public string OrdinaryId { get; }
        public string QualityId { get; }
        private readonly Dictionary<string, string> alternatives;
        public IngredientResolver(string ordinaryId = "egg", string qualityId = "quality_egg") : this(new[] { new KeyValuePair<string, string>(ordinaryId, qualityId) }) { }
        public IngredientResolver(IEnumerable<KeyValuePair<string, string>> pairs)
        {
            var values = pairs.ToArray(); alternatives = new Dictionary<string, string>(StringComparer.Ordinal);
            if (values.Any(p => string.IsNullOrWhiteSpace(p.Key) || string.IsNullOrWhiteSpace(p.Value) || p.Key == p.Value)
                || values.Select(p => p.Key).Distinct(StringComparer.Ordinal).Count() != values.Length || values.Select(p => p.Value).Distinct(StringComparer.Ordinal).Count() != values.Length
                || values.Any(p => values.Any(q => p.Value == q.Key))) throw new ArgumentException("Invalid ingredient alternatives");
            foreach (var pair in values) alternatives.Add(pair.Key, pair.Value);
            OrdinaryId = values.FirstOrDefault().Key; QualityId = values.FirstOrDefault().Value;
        }
        public long Count(InventoryState bag, string id) => bag.GetCount(id) + (id != null && alternatives.TryGetValue(id, out var quality) ? bag.GetCount(quality) : 0);
        public bool HasAlternative(string id) => id != null && alternatives.ContainsKey(id);
        public bool Resolve(InventoryState bag, IEnumerable<IngredientCost> ingredients, out IngredientCost[] result)
        {
            var remaining = new Dictionary<string, long>(StringComparer.Ordinal); var totals = new Dictionary<string, int>(StringComparer.Ordinal);
            // Reserve explicitly requested items first, then use ordinary stocks before their quality alternative.
            foreach (var ingredient in ingredients.OrderBy(i => alternatives.ContainsKey(i.Id) ? 1 : 0))
            {
                int left = ingredient.Quantity;
                foreach (string id in alternatives.TryGetValue(ingredient.Id, out var quality) ? new[] { ingredient.Id, quality } : new[] { ingredient.Id })
                {
                    if (!remaining.ContainsKey(id)) remaining[id] = bag.GetCount(id);
                    int used = (int)Math.Min(left, remaining[id]);
                    if (used > 0) { long total = (totals.TryGetValue(id, out var count) ? (long)count : 0) + used; if (total > int.MaxValue) { result = null; return false; } totals[id] = (int)total; remaining[id] -= used; left -= used; }
                }
                if (left > 0) { result = null; return false; }
            }
            var costs = new List<IngredientCost>(); foreach (var value in totals) costs.Add(new IngredientCost(value.Key, value.Value));
            result = costs.ToArray(); return true;
        }
    }
}
