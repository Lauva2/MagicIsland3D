using System.IO;
using MagicIsland.Configuration;
using MagicIsland.Inventory;
using MagicIsland.UI.Views;
using TMPro;
using UnityEditor;
using UnityEngine;
using Factory = MagicIsland.EditorTools.PrototypeUiPrefabBuilder;
namespace MagicIsland.EditorTools
{
    public static class QualityEggSetup
    {
        public static void Ensure(ItemCatalog items)
        {
            var config = AssetDatabase.LoadAssetAtPath<AnimalConfig>("Assets/Config/AnimalConfig.asset");
            if (!File.ReadAllText("Assets/Config/AnimalConfig.asset").Contains("QualityEggItemId:")) EditorUtility.SetDirty(config);
            if (items.Find(config.QualityEggItemId) == null)
            {
                items.Items.Add(new ItemConfig { Id = config.QualityEggItemId, DisplayName = "优质鸡蛋", Type = ItemType.AnimalProduct, MaxStack = 99,
                    Description = "亲密度达到 70 的鸡喂食后有机会产出。可出售，也可替代普通蛋制作料理或交付订单。",
                    Icon = items.Find(config.EggItemId)?.Icon, IconColor = new Color(.94f, .73f, .31f) });
                EditorUtility.SetDirty(items);
            }
        }
        public static void UpgradePanels()
        {
            string path = Factory.Folder + "/AnimalShopPanel.prefab"; var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var v = root.GetComponent<AnimalShopView>();
                if (v.QualitySellButton == null)
                {
                    var parent = v.SaveButton.transform.parent;
                    TMP_Text[] prices = { v.ChickenPriceText, v.FeedPriceText, v.EggPriceText };
                    UnityEngine.UI.Button[] buttons = { v.ChickenButton, v.FeedButton, v.SellButton };
                    for (int i = 0; i < 3; i++)
                    {
                        var card = prices[i].transform.parent.GetComponent<RectTransform>(); card.anchoredPosition = new Vector2(32 + 163 * i, -180); card.sizeDelta = new Vector2(147, 154);
                        prices[i].rectTransform.sizeDelta = new Vector2(119, 62); prices[i].enableAutoSizing = true; prices[i].fontSizeMin = 16; prices[i].fontSizeMax = 20;
                        buttons[i].GetComponent<RectTransform>().sizeDelta = new Vector2(119, 40);
                        var label = buttons[i].GetComponentInChildren<TMP_Text>(); label.fontSize = 18; label.rectTransform.sizeDelta = new Vector2(119, 40);
                    }
                    var offer = Factory.Panel("Quality Egg Offer", parent, new Vector2(0, 1), new Vector2(0, 1), new Vector2(521, -180), new Vector2(147, 154), new Color(.94f, .87f, .69f));
                    v.QualityEggPriceText = Factory.Label("Price", offer, new Vector2(14, -14), new Vector2(119, 62), "优质鸡蛋收购\n40 金币 / 枚", 18);
                    v.QualitySellButton = Factory.Button("Action", offer, new Vector2(14, -98), new Vector2(119, 40), "出售一枚", new Color(.7f, .81f, .62f)); v.QualitySellButton.GetComponentInChildren<TMP_Text>().fontSize = 18;
                    v.StockText.enableAutoSizing = true; v.StockText.fontSizeMin = 17; v.StockText.fontSizeMax = 20;
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                else
                {
                    bool changed = false;
                    foreach (var button in new[] { v.ChickenButton, v.FeedButton, v.SellButton })
                    {
                        var label = button.GetComponentInChildren<TMP_Text>(); float width = label.rectTransform.sizeDelta.x;
                        if (button.GetComponent<RectTransform>().sizeDelta.x == 119 && (width == 174 || width == 168))
                        { label.rectTransform.sizeDelta = new Vector2(119, 40); changed = true; }
                    }
                    if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            path = Factory.Folder + "/CoopPanel.prefab"; root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var v = root.GetComponent<CoopView>();
                // A single migration of default fields; subsequent setup preserves edited layouts.
                if (!v.StatusText.enableAutoSizing)
                {
                    v.StatusText.enableAutoSizing = true; v.StatusText.fontSizeMin = 16; v.StatusText.fontSizeMax = 20;
                    v.GuideText.rectTransform.anchoredPosition = new Vector2(32, -554); v.GuideText.rectTransform.sizeDelta = new Vector2(636, 46); v.GuideText.fontSize = 16;
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
