using System.IO;
using MagicIsland.UI.Views;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Factory = MagicIsland.EditorTools.PrototypeUiPrefabBuilder;

namespace MagicIsland.EditorTools
{
    public static class TaskPanelPrefabBuilder
    {
        public static void CreateMissing()
        {
            string path = Factory.Folder + "/TaskPanel.prefab";
            if (File.Exists(path)) return;
            GameObject root = Factory.CanvasRoot("TaskPanel");
            try
            {
                TaskView view = root.AddComponent<TaskView>(); view.Layer = UILayer.Common;
                view.Card = Factory.Panel("Task Card", root.transform, Vector2.one, Vector2.one,
                    new Vector2(-20, -330), new Vector2(310, 260), Factory.Cream); Factory.SoftShadow(view.Card);
                view.HeaderText = Factory.Label("Task Header", view.Card, new Vector2(16, -10), new Vector2(226, 28), "新手目标 · 1 / 6", 20);
                view.CollapseButton = Factory.Button("Collapse", view.Card, new Vector2(250, -10), new Vector2(44, 28), "收起", Factory.Sand);
                view.CollapseButton.GetComponentInChildren<TMPro.TMP_Text>().fontSize = 14;
                var details = Factory.Rect("Details", view.Card, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(310, 260));
                view.Details = details.gameObject;
                view.TitleText = Factory.Label("Task Title", details, new Vector2(16, -50), new Vector2(278, 30), "第一次耕地", 22);
                view.DescriptionText = Factory.Label("Task Description", details, new Vector2(16, -88), new Vector2(278, 64), "选择锄头，靠近农田，用鼠标点击一块空地。", 18);
                view.ProgressText = Factory.Label("Task Progress", details, new Vector2(16, -156), new Vector2(278, 28), "进度 0 / 1 · 奖励 5 金币", 17);
                view.ClaimButton = Factory.Button("Claim Reward", details, new Vector2(16, -192), new Vector2(278, 36), "完成目标后领取", new Color(0.7f, 0.81f, 0.62f));
                view.ClaimButton.GetComponentInChildren<TMPro.TMP_Text>().fontSize = 18;
                view.FeedbackText = Factory.Label("Task Feedback", details, new Vector2(16, -234), new Vector2(278, 20), "", 14);
                // Only the card and its buttons intercept clicks; the rest of the screen remains playable.
                foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true))
                    graphic.raycastTarget = graphic.gameObject == view.Card.gameObject || graphic.GetComponent<Button>() != null;
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
