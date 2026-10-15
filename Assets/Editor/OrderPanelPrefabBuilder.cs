using System.IO;
using MagicIsland.UI.Views;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Factory = MagicIsland.EditorTools.PrototypeUiPrefabBuilder;
namespace MagicIsland.EditorTools
{
    public static class OrderPanelPrefabBuilder
    {
        public static void CreateMissing()
        {
            string path = Factory.Folder + "/OrderPanel.prefab";
            if (File.Exists(path)) { UpgradeWording(path); return; }
            GameObject root = Factory.CanvasRoot("OrderPanel");
            try
            {
                var view = root.AddComponent<OrderView>(); view.Layer = UILayer.PopUp;
                var shade = Factory.Panel("Backdrop", root.transform, Vector2.zero, Vector2.zero, Vector2.zero,
                    Vector2.zero, new Color(0.12f, 0.2f, 0.15f, 0.58f));
                shade.anchorMax = Vector2.one; shade.GetComponent<Image>().sprite = null;
                var panel = Factory.Panel("Notice Card", root.transform, Vector2.one * 0.5f, Vector2.one * 0.5f,
                    Vector2.zero, new Vector2(640, 420), Factory.Cream); Factory.SoftShadow(panel);
                Factory.Label("Title", panel, new Vector2(32, -20), new Vector2(480, 44), "岛屿公告栏", 30);
                view.CloseButton = Factory.Button("Close", panel, new Vector2(554, -22), new Vector2(54, 42), "×", Factory.Sand);
                view.DateText = Factory.Label("Date", panel, new Vector2(32, -74), new Vector2(576, 32), "第 1 年 春季 1 日 · 今日收购", 20);
                var request = Factory.Panel("Request", panel, new Vector2(0, 1), new Vector2(0, 1), new Vector2(32, -120), new Vector2(576, 120), Factory.Sand);
                view.CropIcon = Factory.Panel("Crop Icon", request, new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -28), new Vector2(64, 64), new Color(0.46f, 0.65f, 0.37f)).GetComponent<Image>();
                view.CropIcon.raycastTarget = false;
                view.RequestText = Factory.Label("Requested Crop", request, new Vector2(104, -16), new Vector2(452, 34), "芜菁 × 3", 26);
                view.StockText = Factory.Label("Owned", request, new Vector2(104, -57), new Vector2(452, 24), "背包持有 0 / 3", 18);
                view.RewardText = Factory.Label("Reward", request, new Vector2(104, -86), new Vector2(452, 26), "交付奖励 120 金币", 19);
                view.StatusText = Factory.Label("Status", panel, new Vector2(32, -254), new Vector2(576, 28), "有效至今天结束 · 睡觉后刷新", 18);
                view.DeliverButton = Factory.Button("Deliver", panel, new Vector2(32, -296), new Vector2(576, 48), "交付全部物品", new Color(0.7f, 0.81f, 0.62f));
                view.FeedbackText = Factory.Label("Feedback", panel, new Vector2(32, -362), new Vector2(576, 32), "物品数量不足，请先准备好全部物品", 18);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
        }
        private static void UpgradeWording(string path)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view = root.GetComponent<OrderView>(); bool changed = false;
                var label = view.DeliverButton.GetComponentInChildren<TMPro.TMP_Text>();
                if (label.text == "交付全部作物") { label.text = "交付全部物品"; changed = true; }
                if (view.FeedbackText.text == "作物数量不足，请先准备好全部作物")
                { view.FeedbackText.text = "物品数量不足，请先准备好全部物品"; changed = true; }
                if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
