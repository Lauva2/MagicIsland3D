using MagicIsland.Interaction;
using MagicIsland.UI.Views;
using UnityEditor;
using UnityEngine;
using Factory = MagicIsland.EditorTools.PrototypeUiPrefabBuilder;
namespace MagicIsland.EditorTools
{
    public static class CropUiUpgrade
    {
        public static void Apply()
        {
            string path = Factory.Folder + "/InventoryPanel.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                InventoryView view = root.GetComponent<InventoryView>();
                if (view.UseSeedButton == null)
                {
                    view.DetailText.rectTransform.sizeDelta = new Vector2(490, 66);
                    view.UseSeedButton = Factory.Button("Use Seed", view.DetailText.transform.parent, new Vector2(520, -56),
                        new Vector2(210, 44), "用于播种", new Color(0.7f, 0.81f, 0.62f));
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            path = Factory.Folder + "/ShopPanel.prefab"; root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                ShopView view = root.GetComponent<ShopView>();
                if (view.CropButtons == null || view.CropButtons.Length == 0)
                {
                    Transform panel = view.BalanceText.transform.parent;
                    panel.GetComponent<RectTransform>().sizeDelta = new Vector2(700, 534);
                    view.BuyButton.transform.parent.GetComponent<RectTransform>().anchoredPosition = new Vector2(32, -200);
                    view.SellButton.transform.parent.GetComponent<RectTransform>().anchoredPosition = new Vector2(32, -306);
                    view.StockText.rectTransform.anchoredPosition = new Vector2(32, -412);
                    view.FeedbackText.rectTransform.anchoredPosition = new Vector2(32, -456);
                    view.CropButtons = new UnityEngine.UI.Button[3];
                    string[] labels = { "芜菁", "番茄", "玉米" };
                    for (int i = 0; i < 3; i++) view.CropButtons[i] = Factory.Button("Crop " + i, panel,
                        new Vector2(32 + i * 216, -130), new Vector2(204, 46), labels[i], Factory.Sand);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            path = Factory.Folder + "/FarmHudPanel.prefab"; root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                PrototypeHud hud = root.GetComponent<PrototypeHud>();
                if (hud.SeedText == null)
                {
                    Transform top = hud.DayText.transform.parent; top.GetComponent<RectTransform>().sizeDelta = new Vector2(470, 274);
                    hud.SeedText = Factory.Label("Selected Seed", top, new Vector2(16, -226), new Vector2(438, 32), "播种：芜菁种子", 20);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
