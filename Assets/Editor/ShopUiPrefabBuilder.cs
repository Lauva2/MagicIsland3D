using System.IO;
using MagicIsland.UI.Views;
using UnityEditor;
using UnityEngine;
using Factory = MagicIsland.EditorTools.PrototypeUiPrefabBuilder;
namespace MagicIsland.EditorTools
{
    public static class ShopUiPrefabBuilder
    {
        public static void CreateMissing()
        {
            string path = Factory.Folder + "/ShopPanel.prefab";
            if (File.Exists(path)) return;
            GameObject root = Factory.CanvasRoot("ShopPanel");
            ShopView view = root.AddComponent<ShopView>(); view.Layer = UILayer.PopUp;
            RectTransform shade = Factory.Panel("Backdrop", root.transform, Vector2.zero, Vector2.zero,
                Vector2.zero, Vector2.zero, new Color(0.12f, 0.2f, 0.15f, 0.58f)); shade.anchorMax = Vector2.one;
            shade.GetComponent<UnityEngine.UI.Image>().sprite = null;
            RectTransform panel = Factory.Panel("Shop Panel", root.transform, Vector2.one * 0.5f, Vector2.one * 0.5f,
                Vector2.zero, new Vector2(700, 460), Factory.Cream); Factory.SoftShadow(panel);
            Factory.Label("Title", panel, new Vector2(32, -22), new Vector2(380, 46), "岛屿商店", 32);
            view.CloseButton = Factory.Button("Close", panel, new Vector2(614, -24), new Vector2(54, 42), "×", Factory.Sand);
            view.BalanceText = Factory.Label("Balance", panel, new Vector2(32, -82), new Vector2(636, 32), "金币 100", 22);
            RectTransform buy = Factory.Panel("Buy Card", panel, new Vector2(0, 1), new Vector2(0, 1), new Vector2(32, -136), new Vector2(636, 90), Factory.Sand);
            view.BuyText = Factory.Label("Seed Offer", buy, new Vector2(18, -18), new Vector2(420, 54), "芜菁种子 · 10 金币 / 个", 22);
            view.BuyButton = Factory.Button("Buy", buy, new Vector2(470, -23), new Vector2(146, 44), "购买 1 个", new Color(0.7f, 0.81f, 0.62f));
            RectTransform sell = Factory.Panel("Sell Card", panel, new Vector2(0, 1), new Vector2(0, 1), new Vector2(32, -242), new Vector2(636, 90), Factory.Sand);
            view.SellText = Factory.Label("Crop Offer", sell, new Vector2(18, -18), new Vector2(420, 54), "芜菁 · 25 金币 / 个", 22);
            view.SellButton = Factory.Button("Sell", sell, new Vector2(470, -23), new Vector2(146, 44), "出售 1 个", new Color(0.91f, 0.75f, 0.46f));
            view.StockText = Factory.Label("Stock", panel, new Vector2(32, -348), new Vector2(636, 32), "持有种子 50    持有作物 0", 19);
            view.FeedbackText = Factory.Label("Feedback", panel, new Vector2(32, -392), new Vector2(636, 40), "点击按钮，每次交易 1 个物品", 18);
            PrefabUtility.SaveAsPrefabAsset(root, path); Object.DestroyImmediate(root);
        }
    }
}
