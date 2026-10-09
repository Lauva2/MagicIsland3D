using System;
using System.Collections.Generic;
using MagicIsland.Inventory;
using UnityEngine;

namespace MagicIsland.Configuration
{
    [Serializable]
    public sealed class ItemConfig
    {
        public string Id;
        public string DisplayName;
        public ItemType Type;
        [Min(1)] public int MaxStack = 99;
        public Sprite Icon;
        public Color IconColor = Color.white;
        [TextArea] public string Description;
    }

    [CreateAssetMenu(menuName = "Magic Island/Item Catalog")]
    public sealed class ItemCatalog : ScriptableObject
    {
        public const string Address = "ItemCatalog";
        public List<ItemConfig> Items = new List<ItemConfig>();
        public ItemConfig Find(string id) => Items.Find(item => item != null && item.Id == id);
        public bool Validate(GameConfig game)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (ItemConfig item in Items)
                if (item == null || string.IsNullOrWhiteSpace(item.Id) || string.IsNullOrWhiteSpace(item.DisplayName)
                    || item.MaxStack < 1 || !ids.Add(item.Id)) return false;
            return Find(game.SeedItemId)?.Type == ItemType.Seed && Find(game.HarvestItemId)?.Type == ItemType.Crop;
        }
        public IEnumerable<ItemDefinition> Definitions()
        {
            foreach (ItemConfig item in Items) yield return new ItemDefinition(item.Id, item.DisplayName, item.Type, item.MaxStack);
        }
    }
}
