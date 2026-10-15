using System.Collections.Generic;
using MagicIsland.Inventory;
using UnityEngine;
namespace MagicIsland.Configuration
{
    [CreateAssetMenu(menuName = "Magic Island/Shipping Config")]
    public sealed class ShippingConfig : ScriptableObject
    {
        public const string Address = "ShippingConfig";
        public string BoxAddress = "ShippingBox";
        public Vector3 HouseOffset = new Vector3(-1.9f, 0, -1.5f);
        public bool IsValid => !string.IsNullOrWhiteSpace(BoxAddress) && HouseOffset.y == 0 && HouseOffset.x >= -2.1f && HouseOffset.x <= -1.8f && HouseOffset.z >= -1.8f && HouseOffset.z <= -1.4f;
        // Existing catalogs remain the single source for crop, fish and egg sale prices.
        public static IEnumerable<KeyValuePair<string, int>> Prices(ItemCatalog items, AnimalConfig animals)
        {
            foreach (var item in items.Items)
            {
                int price = animals != null && item.Id == animals.EggItemId ? animals.EggPrice
                    : animals != null && item.Id == animals.QualityEggItemId ? animals.QualityEggPrice
                    : item.Type == ItemType.Crop || item.Type == ItemType.Fish ? item.SellPrice : 0;
                if (price > 0) yield return new KeyValuePair<string, int>(item.Id, price);
            }
        }
    }
}
