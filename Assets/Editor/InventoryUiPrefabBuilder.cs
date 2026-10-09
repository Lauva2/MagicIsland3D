using TMPro;
using System.IO;
using MagicIsland.Loading;
using MagicIsland.UI.Views;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Factory = MagicIsland.EditorTools.PrototypeUiPrefabBuilder;

namespace MagicIsland.EditorTools
{
    public static class InventoryUiPrefabBuilder
    {
        public static void CreateMissing()
        {
            string path = Factory.Folder + "/" + RuntimeUi.InventoryAddress + ".prefab";
            if (File.Exists(path)) return;
            GameObject root = Factory.CanvasRoot(RuntimeUi.InventoryAddress);
            
            InventoryView view = root.AddComponent<InventoryView>();
            view.Layer = UILayer.PopUp;
            RectTransform shade = Factory.Panel("Backdrop", root.transform, Vector2.zero, Vector2.zero,
                Vector2.zero, Vector2.zero, new Color(0.12f, 0.2f, 0.15f, 0.58f));
            shade.anchorMax = Vector2.one;
            shade.GetComponent<Image>().sprite = null;
            RectTransform panel = Factory.Panel("Inventory Panel", root.transform, Vector2.one * 0.5f, Vector2.one * 0.5f,
                Vector2.zero, new Vector2(820, 590), Factory.Cream);
            Factory.SoftShadow(panel);
            Factory.Label("Title", panel, new Vector2(34, -22), new Vector2(500, 46), "随身背包", 32);
            view.CloseButton = Factory.Button("Close", panel, new Vector2(730, -24), new Vector2(54, 42), "×", Factory.Sand);
            view.CapacityText = Factory.Label("Capacity", panel, new Vector2(34, -74), new Vector2(600, 28), "已用 1 / 12 格", 18);

            RectTransform scrollRect = Factory.Rect("Item Scroll", panel, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(34, -114), new Vector2(752, 290));
            ScrollRect scroll = scrollRect.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 25f;
            RectTransform viewport = Factory.Rect("Viewport", scrollRect, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            viewport.anchorMax = Vector2.one;
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform content = Factory.Rect("Slots", viewport, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(752, 0));
            GridLayoutGroup grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(105, 94);
            grid.spacing = new Vector2(12, 12);
            grid.padding = new RectOffset(31, 31, 8, 8);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 6;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;

            view.Slots = new InventorySlotView[36];
            Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Textures/UI/RoundedPanel.png");
            for (int i = 0; i < view.Slots.Length; i++)
            {
                Button button = Factory.Button("Slot " + (i + 1), content, Vector2.zero, new Vector2(105, 94), "空格", Factory.Sand);
                InventorySlotView slot = button.gameObject.AddComponent<InventorySlotView>();
                slot.SelectButton = button;
                slot.NameText = button.GetComponentInChildren<TMP_Text>();
                slot.NameText.fontSize = 14;
                slot.NameText.rectTransform.anchoredPosition = new Vector2(7, -65);
                slot.NameText.rectTransform.sizeDelta = new Vector2(91, 23);
                RectTransform iconRect = Factory.Rect("Icon", button.transform, new Vector2(0, 1), new Vector2(0, 1),
                    new Vector2(35, -10), new Vector2(35, 35));
                slot.Icon = iconRect.gameObject.AddComponent<Image>();
                slot.Icon.sprite = icon;
                slot.Icon.raycastTarget = false;
                slot.QuantityText = Factory.Label("Quantity", button.transform, new Vector2(60, -39), new Vector2(37, 24), "", 18, TextAnchor.MiddleRight);
                view.Slots[i] = slot;
            }
            RectTransform details = Factory.Panel("Details", panel, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(34, -422), new Vector2(752, 116), Factory.Sand);
            view.DetailTitleText = Factory.Label("Item Name", details, new Vector2(20, -10), new Vector2(712, 28), "选择物品", 20);
            view.DetailText = Factory.Label("Description", details, new Vector2(20, -43), new Vector2(712, 66), "点击一个格子，查看物品详情。", 17);
            Factory.Label("Controls", panel, new Vector2(34, -549), new Vector2(752, 24), "B / Esc 关闭背包", 16).color = new Color(0.42f, 0.46f, 0.37f);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }
    }
}
