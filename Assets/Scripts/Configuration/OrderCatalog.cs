using System;
using System.Collections.Generic;
using System.Linq;
using MagicIsland.CalendarSystem;
using MagicIsland.Inventory;
using MagicIsland.Orders;
using UnityEngine;

namespace MagicIsland.Configuration
{
    [Serializable]
    public sealed class OrderConfig
    {
        public string Id, CropId;
        public SeasonMask Seasons = SeasonMask.All;
        [Range(1, 99)] public int MinQuantity = 1, MaxQuantity = 3;
        [Min(1)] public int GoldPerItem = 40;
    }
    [CreateAssetMenu(menuName = "Magic Island/Order Catalog")]
    public sealed class OrderCatalog : ScriptableObject
    {
        public const string Address = "OrderCatalog";
        public string BuildingAddress = "OrderHall";
        public List<OrderConfig> Offers = new List<OrderConfig>();
        public IEnumerable<OrderDefinition> Definitions(CropCatalog crops) => Offers.Select(offer => new OrderDefinition(offer.Id,
            crops.Find(offer.CropId).HarvestItemId, offer.Seasons, offer.MinQuantity, offer.MaxQuantity, offer.GoldPerItem));
        public bool Validate(CropCatalog crops, ItemCatalog items)
        {
            if (string.IsNullOrWhiteSpace(BuildingAddress) || Offers == null || Offers.Count == 0) return false;
            var ids = new HashSet<string>();
            foreach (var offer in Offers)
            {
                if (offer == null || string.IsNullOrWhiteSpace(offer.Id) || !ids.Add(offer.Id) || !CalendarSystem.Seasons.ValidMask(offer.Seasons)
                    || offer.MinQuantity < 1 || offer.MaxQuantity < offer.MinQuantity || offer.MaxQuantity > 99) return false;
                var crop = crops.Find(offer.CropId); var item = crop == null ? null : items.Find(crop.HarvestItemId);
                if (item?.Type != ItemType.Crop || offer.GoldPerItem <= item.SellPrice) return false;
            }
            for (int i = 0; i < 4; i++) if (!Offers.Any(offer => CalendarSystem.Seasons.Includes(offer.Seasons, (Season)i))) return false;
            return true;
        }
    }
}
