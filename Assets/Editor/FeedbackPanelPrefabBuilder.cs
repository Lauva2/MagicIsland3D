using System.IO;
using MagicIsland.UI.Views;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Factory = MagicIsland.EditorTools.PrototypeUiPrefabBuilder;
namespace MagicIsland.EditorTools
{
    public static class FeedbackPanelPrefabBuilder
    {
        public static void CreateMissing()
        {
            string path = Factory.Folder + "/FeedbackPanel.prefab";
            if (File.Exists(path)) return;
            GameObject root = Factory.CanvasRoot("FeedbackPanel");
            FeedbackView view = root.AddComponent<FeedbackView>(); view.Layer = UILayer.Top;
            view.Group = root.AddComponent<CanvasGroup>(); view.Group.alpha = 0;
            view.Group.interactable = false; view.Group.blocksRaycasts = false;
            RectTransform card = Factory.Panel("Toast", root.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(0, 154), new Vector2(440, 90), Factory.Cream); Factory.SoftShadow(card);
            view.Accent = Factory.Panel("Accent", card, new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -18), new Vector2(8, 54), Factory.Sand).GetComponent<Image>();
            view.MessageText = Factory.Label("Message", card, new Vector2(40, -12), new Vector2(376, 32), "浇水完成", 23);
            view.DetailText = Factory.Label("Detail", card, new Vector2(40, -48), new Vector2(376, 28), "体力 -2", 19);
            foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>()) graphic.raycastTarget = false;
            PrefabUtility.SaveAsPrefabAsset(root, path); Object.DestroyImmediate(root);
        }
    }
}
