using System.IO;
using MagicIsland.UI.Views;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Factory = MagicIsland.EditorTools.PrototypeUiPrefabBuilder;

namespace MagicIsland.EditorTools
{
    public static class SettingsUiPrefabBuilder
    {
        public static void CreateMissing()
        {
            string path = Factory.Folder + "/SettingsPanel.prefab";
            if (File.Exists(path)) return;
            GameObject root = Factory.CanvasRoot("SettingsPanel");
            SettingsView view = root.AddComponent<SettingsView>();
            view.Layer = UILayer.PopUp;
            RectTransform backdrop = Factory.Panel("Backdrop", root.transform, Vector2.zero, Vector2.zero,
                Vector2.zero, Vector2.zero, new Color(0.12f, 0.2f, 0.15f, 0.58f));
            backdrop.anchorMax = Vector2.one;
            backdrop.GetComponent<Image>().sprite = null;
            RectTransform panel = Factory.Panel("Settings Panel", root.transform, Vector2.one * 0.5f,
                Vector2.one * 0.5f, Vector2.zero, new Vector2(520, 350), Factory.Cream);
            Factory.SoftShadow(panel);
            Factory.Label("Title", panel, new Vector2(32, -22), new Vector2(350, 46), "设置", 32);
            view.CloseButton = Factory.Button("Close", panel, new Vector2(430, -24), new Vector2(54, 42), "×", Factory.Sand);
            Factory.Label("Volume Label", panel, new Vector2(32, -92), new Vector2(240, 36), "主音量", 22);
            view.VolumeText = Factory.Label("Volume Value", panel, new Vector2(370, -92), new Vector2(110, 36), "100%", 20, TextAnchor.MiddleRight);
            GameObject slider = DefaultControls.CreateSlider(new DefaultControls.Resources());
            slider.name = "Volume Slider";
            RectTransform sliderRect = slider.GetComponent<RectTransform>();
            sliderRect.SetParent(panel, false);
            sliderRect.anchorMin = sliderRect.anchorMax = sliderRect.pivot = new Vector2(0, 1);
            sliderRect.anchoredPosition = new Vector2(32, -146);
            sliderRect.sizeDelta = new Vector2(448, 32);
            view.VolumeSlider = slider.GetComponent<Slider>();
            slider.transform.Find("Background").GetComponent<Image>().color = Factory.Sand;
            slider.transform.Find("Fill Area/Fill").GetComponent<Image>().color = new Color(0.28f, 0.43f, 0.31f);
            RectTransform checkbox = Factory.Panel("Fullscreen", panel, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(32, -210), new Vector2(32, 32), Factory.Sand);
            view.FullscreenToggle = checkbox.gameObject.AddComponent<Toggle>();
            view.FullscreenToggle.targetGraphic = checkbox.GetComponent<Image>();
            RectTransform check = Factory.Panel("Checkmark", checkbox, Vector2.one * 0.5f, Vector2.one * 0.5f,
                Vector2.zero, new Vector2(18, 18), new Color(0.28f, 0.43f, 0.31f));
            check.GetComponent<Image>().raycastTarget = false;
            view.FullscreenToggle.graphic = check.GetComponent<Image>();
            Factory.Label("Fullscreen Label", panel, new Vector2(80, -210), new Vector2(360, 32), "全屏显示", 22);
            Factory.Label("Hint", panel, new Vector2(32, -280), new Vector2(448, 40), "设置在本次运行中生效 · Esc 关闭", 16);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }
    }
}
