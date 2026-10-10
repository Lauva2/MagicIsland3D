using MagicIsland.Configuration;
using MagicIsland.Inventory;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
namespace MagicIsland.EditorTools
{
    public static class CropCatalogSetup
    {
        public static void Ensure(GameConfig config, ItemCatalog items, AddressableAssetSettings settings, AddressableAssetGroup group)
        {
            Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Textures/UI/RoundedPanel.png");
            Add(items, "tomato_seed", "番茄种子", ItemType.Seed, 20, 0, new Color(0.89f, 0.47f, 0.32f), "播种后浇水，5 次生长后收获番茄。", icon);
            Add(items, "tomato", "番茄", ItemType.Crop, 0, 50, new Color(0.89f, 0.3f, 0.21f), "红润的番茄，可以在商店出售。", icon);
            Add(items, "corn_seed", "玉米种子", ItemType.Seed, 30, 0, new Color(0.9f, 0.75f, 0.32f), "播种后浇水，7 次生长后收获玉米。", icon);
            Add(items, "corn", "玉米", ItemType.Crop, 0, 85, new Color(0.97f, 0.79f, 0.24f), "金黄的玉米，可以在商店出售。", icon);
            const string path = "Assets/Config/CropCatalog.asset";
            CropCatalog crops = AssetDatabase.LoadAssetAtPath<CropCatalog>(path);
            if (crops == null)
            {
                crops = ScriptableObject.CreateInstance<CropCatalog>();
                crops.Crops.Add(new CropConfig { Id = "turnip", SeedItemId = config.SeedItemId, HarvestItemId = config.HarvestItemId,
                    GrowthDays = config.CropGrowthDays, Shape = CropShape.Round, MatureColor = new Color(0.94f, 0.87f, 0.74f), PlantingSeasons = SeasonConfigSetup.Defaults("turnip") });
                crops.Crops.Add(new CropConfig { Id = "tomato", SeedItemId = "tomato_seed", HarvestItemId = "tomato", GrowthDays = 5,
                    Shape = CropShape.Bush, MatureColor = new Color(0.88f, 0.28f, 0.19f), PlantingSeasons = SeasonConfigSetup.Defaults("tomato") });
                crops.Crops.Add(new CropConfig { Id = "corn", SeedItemId = "corn_seed", HarvestItemId = "corn", GrowthDays = 7,
                    Shape = CropShape.Tall, MatureColor = new Color(0.98f, 0.78f, 0.2f), PlantingSeasons = SeasonConfigSetup.Defaults("corn") });
                AssetDatabase.CreateAsset(crops, path);
            }
            string guid = AssetDatabase.AssetPathToGUID(path);
            var entry = settings.FindAssetEntry(guid) ?? settings.CreateOrMoveEntry(guid, group);
            if (entry.address != CropCatalog.Address) entry.SetAddress(CropCatalog.Address);
        }
        private static void Add(ItemCatalog items, string id, string name, ItemType type, int buy, int sell, Color color, string description, Sprite icon)
        {
            if (items.Find(id) != null) return;
            items.Items.Add(new ItemConfig { Id = id, DisplayName = name, Type = type, BuyPrice = buy, SellPrice = sell,
                MaxStack = 99, Icon = icon, IconColor = color, Description = description });
            EditorUtility.SetDirty(items);
        }
    }
}
