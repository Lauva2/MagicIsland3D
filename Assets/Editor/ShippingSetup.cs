using System.IO;
using System.Linq;
using MagicIsland.Configuration;
using MagicIsland.Interaction;
using MagicIsland.UI.Views;
using TMPro;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.UI;
using Factory = MagicIsland.EditorTools.PrototypeUiPrefabBuilder;
namespace MagicIsland.EditorTools
{
    public static class ShippingSetup
    {
        public static void Ensure(AddressableAssetSettings settings, AddressableAssetGroup configuration)
        {
            const string config = "Assets/Config/ShippingConfig.asset";
            if (!File.Exists(config)) AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<ShippingConfig>(), config);
            Register(settings, config, configuration, ShippingConfig.Address);
            const string path = "Assets/Art/Prefabs/Buildings/ShippingBox.prefab";
            if (!File.Exists(path))
            {
                var root = PrefabUtility.LoadPrefabContents("Assets/Art/Prefabs/Buildings/StorageChest.prefab");
                try
                {
                    root.name = "ShippingBox"; Object.DestroyImmediate(root.GetComponent<StorageChest>()); root.AddComponent<ShippingBox>();
                    foreach (var text in root.GetComponentsInChildren<TMP_Text>(true)) text.text = "出货箱";
                    var lid = root.transform.Find("Lid"); lid.localEulerAngles = new Vector3(-12, 0, 0);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            Register(settings, path, settings.FindGroup("Buildings"), "ShippingBox");
        }
        private static void Register(AddressableAssetSettings settings, string path, AddressableAssetGroup group, string address)
        { var entry = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(path)) ?? settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), group); if (entry.address != address) entry.SetAddress(address); }
        public static void CreatePanel()
        {
            string path = Factory.Folder + "/ShippingPanel.prefab";
            if (!File.Exists(path))
            {
                var root = PrefabUtility.LoadPrefabContents(Factory.Folder + "/StoragePanel.prefab");
                try
                {
                    root.name = "ShippingPanel"; var old = root.GetComponent<StorageView>(); var view = root.AddComponent<ShippingView>(); view.Layer = UILayer.PopUp;
                    view.BagSlots = old.BagSlots; view.BoxSlots = old.ChestSlots.Take(18).ToArray();
                    for (int i = 18; i < old.ChestSlots.Length; i++) Object.DestroyImmediate(old.ChestSlots[i].gameObject);
                    view.BagCapacityText = old.BagCapacityText; view.ExpectedIncomeText = old.ChestCapacityText; view.DetailsText = old.DetailsText; view.FeedbackText = old.FeedbackText;
                    view.CloseButton = old.CloseButton; view.MoveOneButton = old.MoveOneButton; view.MoveStackButton = old.MoveStackButton; view.SaveButton = old.SaveButton;
                    view.PageControls = old.PageControls; view.PageText = old.PageText; view.PreviousPageButton = old.PreviousPageButton; view.NextPageButton = old.NextPageButton;
                    view.ExpectedIncomeText.rectTransform.sizeDelta = new Vector2(1100, 30);
                    Object.DestroyImmediate(old.DepositMatchingButton.gameObject); Object.DestroyImmediate(old.DragPreview.gameObject);
                    foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                    {
                        if (text.text == "农场储物箱") text.text = "小屋出货箱";
                        if (text.name == "Controls") text.text = "只收购农作物、鱼和鸡蛋 · 点击格子选择 · 睡觉前可取回 · ESC 关闭";
                    }
                    view.ExpectedIncomeText.text = "待出货 · 预计收入 0 金币"; view.FeedbackText.text = "睡觉后统一售卖 · 当天可取回 · 成功存取自动保存";
                    Object.DestroyImmediate(old); PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            UpgradeReport();
        }
        private static void UpgradeReport()
        {
            string path = Factory.Folder + "/DailyReportPanel.prefab"; var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view = root.GetComponent<DailyReportView>(); if (view.ShippingText != null) return;
                var card = view.DateText.transform.parent.GetComponent<RectTransform>(); card.sizeDelta = new Vector2(720, 690);
                view.HarvestText.rectTransform.anchoredPosition = new Vector2(32, -246);
                view.MatureText.rectTransform.anchoredPosition = new Vector2(32, -284);
                view.EggsText.rectTransform.anchoredPosition = new Vector2(32, -322);
                view.EggsText.rectTransform.sizeDelta = new Vector2(656, 52);
                Factory.Label("Shipping Title", card, new Vector2(32, -388), new Vector2(656, 30), "出货明细", 22);
                var bg = Factory.Panel("Shipping Details", card, new Vector2(0, 1), new Vector2(0, 1), new Vector2(32, -426), new Vector2(656, 112), Factory.Sand);
                var scroll = bg.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped;
                var viewport = Factory.Rect("Viewport", bg, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero); viewport.anchorMax = Vector2.one; viewport.gameObject.AddComponent<RectMask2D>();
                view.ShippingText = Factory.Label("Sales", viewport, new Vector2(12, -8), new Vector2(628, 110), "当天没有出货", 19);
                view.ShippingText.enableWordWrapping = true; scroll.viewport = viewport; scroll.content = view.ShippingText.rectTransform;
                view.FeedbackText.rectTransform.anchoredPosition = new Vector2(32, -554);
                view.ReturnMenuButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(32, -626);
                view.SaveButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(224, -626);
                view.ContinueButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(400, -626);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
