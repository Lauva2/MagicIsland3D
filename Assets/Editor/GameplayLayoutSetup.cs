using System.IO;
using MagicIsland.Interaction;
using MagicIsland.UI.Views;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MagicIsland.EditorTools
{
    // Explicit art operation. Ordinary Setup never reapplies this layout to authored prefabs.
    public static class GameplayLayoutSetup
    {
        public static readonly string[] Names = { "FarmHudPanel", "CropInfoPanel", "InteractionPromptPanel", "TaskPanel", "FeedbackPanel" };
        [MenuItem("Magic Island/Apply Compact Gameplay Layout")]
        public static void Apply()
        {
            foreach (string name in Names)
            {
                string path = PrototypeUiPrefabBuilder.Folder + "/" + name + ".prefab";
                if (!File.Exists(path)) throw new System.InvalidOperationException("Create the UI before applying its layout: " + name);
                var root = PrefabUtility.LoadPrefabContents(path);
                try { Apply(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
        }
        public static void PrepareVerification()
        {
            PrototypeAddressablesSetup.EnsureSetup(); Apply();
            var paths = System.Array.ConvertAll(Names, name => PrototypeUiPrefabBuilder.Folder + "/" + name + ".prefab");
            var before = System.Array.ConvertAll(paths, File.ReadAllBytes);
            Apply(); PrototypeAddressablesSetup.EnsureSetup(); AssetDatabase.SaveAssets();
            for (int i = 0; i < paths.Length; i++)
                if (!System.Linq.Enumerable.SequenceEqual(before[i], File.ReadAllBytes(paths[i])))
                    throw new System.Exception("Layout reapply / ordinary Setup changed authored UI: " + paths[i]);
            Debug.Log("Compact layout preparation PASSED.");
        }
        static void Rect(RectTransform rect, float x, float y, float width, float height, Vector2 anchor)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = new Vector2(x, y); rect.sizeDelta = new Vector2(width, height);
        }
        static void Text(TMP_Text text, float x, float y, float width, float height, float size)
        {
            Rect(text.rectTransform, x, y, width, height, new Vector2(0, 1));
            text.fontSize = size; text.enableAutoSizing = true; text.fontSizeMin = size - 2; text.fontSizeMax = size;
        }
        static void Button(Button button, float x, float y, float width, float height, Vector2 anchor, float size)
        {
            Rect((RectTransform)button.transform, x, y, width, height, anchor);
            var label = button.GetComponentInChildren<TMP_Text>(true);
            Text(label, 0, 0, width, height, size);
        }
        static void Apply(GameObject root)
        {
            var hud = root.GetComponent<PrototypeHud>();
            if (hud != null)
            {
                var status = (RectTransform)hud.DayText.transform.parent;
                Rect(status, 20, -20, 380, 174, new Vector2(0, 1));
                Text(hud.LocationText, 16, -9, 172, 28, 22);
                Text(hud.WeatherText, 190, -11, 64, 26, 18);
                Text(hud.TimeText, 264, -9, 100, 28, 22);
                Text(hud.DayText, 16, -44, 236, 28, 18);
                Button(hud.NextDayButton, 260, -44, 104, 28, new Vector2(0, 1), 16);
                Text(hud.GoldText, 16, -80, 144, 26, 18);
                Text(hud.StaminaText, 168, -80, 196, 26, 18);
                var track = (RectTransform)hud.StaminaFill.transform.parent;
                Rect(track, 16, -115, 348, 10, new Vector2(0, 1));
                Rect(hud.StaminaFill.rectTransform, 0, 0, 348, 10, new Vector2(0, 1));
                Text(hud.SeedText, 16, -136, 348, 24, 16);
                hud.InventoryText.gameObject.SetActive(false);
                var controls = status.Find("Controls"); if (controls != null) controls.gameObject.SetActive(false);
                Button(hud.ReturnMenuButton, -20, -20, 108, 36, Vector2.one, 18);
                Button(hud.InventoryButton, -138, -20, 108, 36, Vector2.one, 18);
                Button(hud.MapButton, -256, -20, 108, 36, Vector2.one, 18);
                var toolbar = (RectTransform)hud.ToolButtons[0].transform.parent;
                Rect(toolbar, 0, 16, 700, 80, new Vector2(.5f, 0));
                for (int i = 0; i < hud.ToolButtons.Length; i++) Button(hud.ToolButtons[i], 24 + i * 94, -10, 88, 36, new Vector2(0, 1), 18);
                Text(hud.HintText, 24, -50, 652, 22, 16);
                hud.FeedbackText.gameObject.SetActive(false);
            }
            var crop = root.GetComponent<CropInfoView>();
            if (crop != null)
            {
                Rect((RectTransform)crop.NameText.transform.parent, -20, -72, 300, 150, Vector2.one);
                Text(crop.NameText, 18, -10, 264, 30, 22);
                Text(crop.DetailsText, 18, -49, 264, 55, 18);
                Text(crop.HintText, 18, -115, 264, 24, 17);
            }
            var prompt = root.GetComponent<InteractionPromptView>();
            if (prompt != null)
            {
                Rect((RectTransform)prompt.NameText.transform.parent, -20, -72, 300, 150, Vector2.one);
                Text(prompt.NameText, 36, -10, 168, 28, 21);
                Text(prompt.StatusText, 208, -10, 74, 28, 16);
                Text(prompt.OperationText, 18, -46, 264, 28, 19);
                Text(prompt.DetailsText, 18, -82, 264, 56, 17);
            }
            var task = root.GetComponent<TaskView>();
            if (task != null)
            {
                task.ExpandedHeight = 234;
                Rect(task.Card, -20, -234, 300, task.ExpandedHeight, Vector2.one);
                Rect((RectTransform)task.Details.transform, 0, 0, 300, task.ExpandedHeight, new Vector2(0, 1));
                Text(task.HeaderText, 16, -10, 222, 26, 18);
                Button(task.CollapseButton, 244, -9, 40, 28, new Vector2(0, 1), 14);
                Text(task.TitleText, 16, -44, 268, 28, 21);
                Text(task.DescriptionText, 16, -80, 268, 52, 17);
                Text(task.ProgressText, 16, -138, 268, 24, 16);
                Button(task.ClaimButton, 16, -168, 268, 32, new Vector2(0, 1), 17);
                Text(task.FeedbackText, 16, -207, 268, 20, 14);
            }
            var feedback = root.GetComponent<FeedbackView>();
            if (feedback != null)
            {
                Rect((RectTransform)feedback.MessageText.transform.parent, 0, 108, 420, 72, new Vector2(.5f, 0));
                Rect(feedback.Accent.rectTransform, 16, -12, 6, 48, new Vector2(0, 1));
                Text(feedback.MessageText, 36, -8, 368, 28, 20);
                Text(feedback.DetailText, 36, -40, 368, 24, 17);
            }
        }
    }
}
