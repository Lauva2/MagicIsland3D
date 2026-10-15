using MagicIsland.CalendarSystem;
using MagicIsland.Configuration;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
namespace MagicIsland.EditorTools
{
    public static class OrderCatalogSetup
    {
        public static void Ensure(AddressableAssetSettings settings, AddressableAssetGroup group)
        {
            const string path = "Assets/Config/OrderCatalog.asset";
            if (AssetDatabase.LoadAssetAtPath<OrderCatalog>(path) == null)
            {
                var catalog = ScriptableObject.CreateInstance<OrderCatalog>();
                catalog.Offers.Add(new OrderConfig { Id = "turnip_daily", CropId = "turnip", Seasons = SeasonMask.Spring | SeasonMask.Autumn | SeasonMask.Winter, GoldPerItem = 40 });
                catalog.Offers.Add(new OrderConfig { Id = "tomato_daily", CropId = "tomato", Seasons = SeasonMask.Summer | SeasonMask.Winter, GoldPerItem = 75 });
                catalog.Offers.Add(new OrderConfig { Id = "corn_daily", CropId = "corn", Seasons = SeasonMask.Summer | SeasonMask.Autumn | SeasonMask.Winter, GoldPerItem = 130 });
                AssetDatabase.CreateAsset(catalog, path);
            }
            string guid = AssetDatabase.AssetPathToGUID(path);
            var entry = settings.FindAssetEntry(guid) ?? settings.CreateOrMoveEntry(guid, group);
            if (entry.address != OrderCatalog.Address) entry.SetAddress(OrderCatalog.Address);
            OrderHallPrefabBuilder.Ensure(settings);
        }
        public static void EnsureAnimalOffers()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<OrderCatalog>("Assets/Config/OrderCatalog.asset");
            AddOffer(catalog, "egg_daily", "egg", 2, 35);
            AddOffer(catalog, "fried_egg_daily", "fried_egg", 1, 50);
            AddOffer(catalog, "egg_vegetable_soup_daily", "egg_vegetable_soup", 1, 90);
        }
        private static void AddOffer(OrderCatalog catalog, string id, string itemId, int quantity, int goldPerItem)
        {
            if (catalog.Offers.Exists(offer => offer.Id == id)) return;
            catalog.Offers.Add(new OrderConfig { Id = id, ItemId = itemId, RequiresCoop = true, Seasons = SeasonMask.All,
                MinQuantity = quantity, MaxQuantity = quantity, GoldPerItem = goldPerItem }); EditorUtility.SetDirty(catalog);
        }
    }
}
