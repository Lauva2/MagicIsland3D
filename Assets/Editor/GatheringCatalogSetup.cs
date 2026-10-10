using MagicIsland.Configuration;
using MagicIsland.Farming;
using MagicIsland.Inventory;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
namespace MagicIsland.EditorTools
{
    public static class GatheringCatalogSetup
    {
        public static void Ensure(ItemCatalog items, AddressableAssetSettings settings, AddressableAssetGroup group)
        {
            AddMaterial(items, "wood", "木材", new Color(0.59f, 0.39f, 0.23f), "砍树获得的木材，可用于后续制作、工具升级和建筑修缮。");
            AddMaterial(items, "stone", "石料", new Color(0.59f, 0.65f, 0.67f), "采石获得的石料，可用于后续制作、工具升级和建筑修缮。");
            const string path = "Assets/Config/GatheringCatalog.asset";
            if (AssetDatabase.LoadAssetAtPath<GatheringCatalog>(path) == null)
            {
                var catalog = ScriptableObject.CreateInstance<GatheringCatalog>();
                catalog.Types.Add(new GatheringTypeConfig { Id = "tree", DisplayName = "橡树", ItemId = "wood", PrefabAddress = "GatheringTree", Tool = FarmTool.Axe, Hits = 3, Yield = 3, StaminaCost = 4, RespawnDays = 3 });
                catalog.Types.Add(new GatheringTypeConfig { Id = "rock", DisplayName = "石头", ItemId = "stone", PrefabAddress = "GatheringRock", Tool = FarmTool.Pickaxe, Hits = 2, Yield = 2, StaminaCost = 5, RespawnDays = 2 });
                catalog.Nodes.Add(new GatheringNodeConfig { Id = "tree_01", TypeId = "tree", Position = new Vector3(-9.2f, 0, 1.5f) });
                catalog.Nodes.Add(new GatheringNodeConfig { Id = "tree_02", TypeId = "tree", Position = new Vector3(-9.2f, 0, 6.5f) });
                catalog.Nodes.Add(new GatheringNodeConfig { Id = "tree_03", TypeId = "tree", Position = new Vector3(-4, 0, 9.2f) });
                catalog.Nodes.Add(new GatheringNodeConfig { Id = "rock_01", TypeId = "rock", Position = new Vector3(-9.2f, 0, -8.2f) });
                catalog.Nodes.Add(new GatheringNodeConfig { Id = "rock_02", TypeId = "rock", Position = new Vector3(-4.8f, 0, -9.4f) });
                AssetDatabase.CreateAsset(catalog, path);
            }
            string guid = AssetDatabase.AssetPathToGUID(path); var entry = settings.FindAssetEntry(guid) ?? settings.CreateOrMoveEntry(guid, group);
            if (entry.address != GatheringCatalog.Address) entry.SetAddress(GatheringCatalog.Address);
            GatheringPrefabBuilder.Ensure(settings);
        }
        private static void AddMaterial(ItemCatalog catalog, string id, string name, Color color, string description)
        {
            if (catalog.Find(id) != null) return;
            catalog.Items.Add(new ItemConfig { Id = id, DisplayName = name, Type = ItemType.Material, MaxStack = 99,
                Icon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Textures/UI/RoundedPanel.png"), IconColor = color, Description = description });
            EditorUtility.SetDirty(catalog);
        }
    }
}
