using System.IO;
using MagicIsland.UI.Views;
using UnityEditor;
using UnityEngine;
using Factory = MagicIsland.EditorTools.PrototypeUiPrefabBuilder;
namespace MagicIsland.EditorTools
{
    public static class SleepUiPrefabBuilder
    {
        public static void CreateMissing()
        {
            string path = Factory.Folder + "/SleepPanel.prefab";
            if (File.Exists(path)) return;
            GameObject root = Factory.CanvasRoot("SleepPanel");
            SleepView view = root.AddComponent<SleepView>(); view.Layer = UILayer.PopUp;
            RectTransform shade = Factory.Panel("Backdrop", root.transform, Vector2.zero, Vector2.zero,
                Vector2.zero, Vector2.zero, new Color(0.12f, 0.2f, 0.15f, 0.58f)); shade.anchorMax = Vector2.one;
            shade.GetComponent<UnityEngine.UI.Image>().sprite = null;
            RectTransform panel = Factory.Panel("Sleep Panel", root.transform, Vector2.one * 0.5f, Vector2.one * 0.5f,
                Vector2.zero, new Vector2(560, 280), Factory.Cream); Factory.SoftShadow(panel);
            Factory.Label("Title", panel, new Vector2(32, -22), new Vector2(496, 46), "休息到明天？", 30);
            view.MessageText = Factory.Label("Message", panel, new Vector2(32, -88), new Vector2(496, 80),
                "睡觉后进入下一天，作物生长并保存进度。", 22);
            view.CancelButton = Factory.Button("Cancel", panel, new Vector2(32, -196), new Vector2(232, 48), "继续忙碌", Factory.Sand);
            view.ConfirmButton = Factory.Button("Confirm", panel, new Vector2(296, -196), new Vector2(232, 48), "睡觉", new Color(0.7f, 0.81f, 0.62f));
            PrefabUtility.SaveAsPrefabAsset(root, path); Object.DestroyImmediate(root);
        }
    }
}
