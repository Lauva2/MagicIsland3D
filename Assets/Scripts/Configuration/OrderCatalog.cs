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
        public string ItemId;
        public bool RequiresCoop;
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
            string.IsNullOrWhiteSpace(offer.ItemId) ? crops.Find(offer.CropId).HarvestItemId : offer.ItemId,
            offer.Seasons, offer.MinQuantity, offer.MaxQuantity, offer.GoldPerItem, offer.RequiresCoop));
        public bool Validate(CropCatalog crops, ItemCatalog items)
        {
            if (string.IsNullOrWhiteSpace(BuildingAddress) || Offers == null || Offers.Count == 0) return false;
            var ids = new HashSet<string>();
            foreach (var offer in Offers)
            {
                if (offer == null || string.IsNullOrWhiteSpace(offer.Id) || !ids.Add(offer.Id) || !CalendarSystem.Seasons.ValidMask(offer.Seasons)
                    || offer.MinQuantity < 1 || offer.MaxQuantity < offer.MinQuantity || offer.MaxQuantity > 99) return false;
                bool direct = !string.IsNullOrWhiteSpace(offer.ItemId);
                if (direct == !string.IsNullOrWhiteSpace(offer.CropId)) return false;
                var crop = direct ? null : crops.Find(offer.CropId);
                var item = items.Find(direct ? offer.ItemId : crop?.HarvestItemId);
                if (item == null || (item.Type != ItemType.Crop && item.Type != ItemType.AnimalProduct && item.Type != ItemType.Food)
                    || (!direct && item.Type != ItemType.Crop) || offer.GoldPerItem < 1 || offer.GoldPerItem <= item.SellPrice) return false;
            }
            for (int i = 0; i < 4; i++) if (!Offers.Any(offer => !offer.RequiresCoop && CalendarSystem.Seasons.Includes(offer.Seasons, (Season)i))) return false;
            return true;
        }
    }
}
