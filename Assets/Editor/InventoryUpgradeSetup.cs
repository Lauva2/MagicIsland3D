using System.IO;
using System.Linq;
using MagicIsland.Configuration;
using MagicIsland.Inventory;
using MagicIsland.UI.Views;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using Factory = MagicIsland.EditorTools.PrototypeUiPrefabBuilder;
namespace MagicIsland.EditorTools
{
    public static class InventoryUpgradeSetup
    {
        public static void Ensure(AddressableAssetSettings settings, AddressableAssetGroup group, GameConfig game)
        {
            const string path = "Assets/Config/InventoryUpgradeConfig.asset"; var config = AssetDatabase.LoadAssetAtPath<InventoryUpgradeConfig>(path);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<InventoryUpgradeConfig>(); AssetDatabase.CreateAsset(config, path);
                if (game.InventorySlots == 12 || !File.ReadAllText("Assets/Config/GameConfig.asset").Contains("InventorySlots:"))
                { game.InventorySlots = 10; EditorUtility.SetDirty(game); }
            }
            string guid = AssetDatabase.AssetPathToGUID(path); var entry = settings.FindAssetEntry(guid) ?? settings.CreateOrMoveEntry(guid, group);
            if (entry.address != InventoryUpgradeConfig.Address) entry.SetAddress(InventoryUpgradeConfig.Address);
        }
        public static void CreatePanels()
        {
            string path = Factory.Folder + "/InventoryUpgradePanel.prefab";
            if (!File.Exists(path))
            {
                var root = Factory.CanvasRoot("InventoryUpgradePanel");
                try
                {
                    var v = root.AddComponent<InventoryUpgradeView>(); v.Layer = UILayer.PopUp;
                    var shade = Factory.Panel("Backdrop", root.transform, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, new Color(.12f, .2f, .15f, .58f)); shade.anchorMax = Vector2.one; shade.GetComponent<UnityEngine.UI.Image>().sprite = null;
                    var card = Factory.Panel("Backpack Upgrade", root.transform, Vector2.one * .5f, Vector2.one * .5f, Vector2.zero, new Vector2(640, 424), Factory.Cream); Factory.SoftShadow(card);
                    Factory.Label("Title", card, new Vector2(32, -22), new Vector2(500, 42), "岛屿商店 · 背包扩容", 28);
                    v.CloseButton = Factory.Button("Close", card, new Vector2(558, -22), new Vector2(50, 40), "×", Factory.Sand);
                    v.CapacityText = Factory.Label("Capacity", card, new Vector2(32, -90), new Vector2(576, 30), "当前容量  10 格 · 升级 0 / 2", 22);
                    v.NextText = Factory.Label("Next", card, new Vector2(32, -136), new Vector2(576, 30), "下一档容量  20 格", 22);
                    v.CostText = Factory.Label("Cost", card, new Vector2(32, -182), new Vector2(576, 30), "升级费用  300 金币 · 增加 10 格", 21);
                    v.GoldText = Factory.Label("Gold", card, new Vector2(32, -226), new Vector2(576, 30), "持有金币  100", 21);
                    v.FeedbackText = Factory.Label("Feedback", card, new Vector2(32, -276), new Vector2(576, 50), "扩容即时生效，费用记入当天经营支出", 19);
                    v.SaveButton = Factory.Button("Save", card, new Vector2(32, -348), new Vector2(180, 44), "保存进度", Factory.Sand);
                    v.UpgradeButton = Factory.Button("Upgrade", card, new Vector2(232, -348), new Vector2(376, 44), "升级背包", new Color(.7f, .81f, .62f));
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { Object.DestroyImmediate(root); }
            }
            path = Factory.Folder + "/ShopPanel.prefab"; var shop = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var v = shop.GetComponent<ShopView>(); if (v.InventoryUpgradeButton == null)
                { v.InventoryUpgradeButton = Factory.Button("Backpack Upgrade", v.AnimalShopButton.transform.parent, new Vector2(224, -526), new Vector2(180, 32), "背包扩容", Factory.Sand); v.InventoryUpgradeButton.GetComponentInChildren<TMPro.TMP_Text>().fontSize = 18; PrefabUtility.SaveAsPrefabAsset(shop, path); }
            }
            finally { PrefabUtility.UnloadPrefabContents(shop); }
            ExtendPanel("InventoryPanel", 0); ExtendPanel("GiftPanel", 1); ExtendPanel("StoragePanel", 2);
        }
        private static void ExtendPanel(string name, int kind)
        {
            string path = Factory.Folder + "/" + name + ".prefab"; var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var slots = kind == 0 ? root.GetComponent<InventoryView>().Slots : kind == 1 ? root.GetComponent<GiftView>().Slots : root.GetComponent<StorageView>().BagSlots;
                int required = kind == 1 ? InventoryUpgradeState.MaximumCapacity : InventoryView.DisplaySlots;
                if (slots.Length >= required) return;
                var extended = slots.ToList();
                for (int i = slots.Length; i < required; i++)
                { var copy = Object.Instantiate(slots[0].gameObject, slots[0].transform.parent, false); copy.name = "Slot " + (i + 1); copy.SetActive(true); extended.Add(copy.GetComponent<InventorySlotView>()); }
                if (kind == 0) root.GetComponent<InventoryView>().Slots = extended.ToArray();
                else if (kind == 1) root.GetComponent<GiftView>().Slots = extended.ToArray();
                else root.GetComponent<StorageView>().BagSlots = extended.ToArray();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
