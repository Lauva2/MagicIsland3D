using System.Linq;
using MagicIsland.UI;
using MagicIsland.UI.Views;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Factory = MagicIsland.EditorTools.PrototypeUiPrefabBuilder;
namespace MagicIsland.EditorTools
{
    public static class StorageShortcutSetup
    {
        public static void UpgradePanel()
        {
            string path = Factory.Folder + "/StoragePanel.prefab"; var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var v = root.GetComponent<StorageView>(); if (v.ShortcutsLayout) return;
                foreach (var extra in v.BagSlots.Skip(InventoryView.DisplaySlots)) Object.DestroyImmediate(extra.gameObject);
                v.BagSlots = v.BagSlots.Take(InventoryView.DisplaySlots).ToArray();
                var panel = (RectTransform)v.CloseButton.transform.parent; panel.sizeDelta = new Vector2(1160, 704);
                v.CloseButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(1074, -22);
                v.BagCapacityText.rectTransform.anchoredPosition = new Vector2(32, -70); v.BagCapacityText.rectTransform.sizeDelta = new Vector2(780, 28); v.BagCapacityText.fontSize = 18;
                v.ChestCapacityText.rectTransform.anchoredPosition = new Vector2(32, -318); v.ChestCapacityText.rectTransform.sizeDelta = new Vector2(1096, 28); v.ChestCapacityText.fontSize = 18;
                Grid(v.BagSlots, new Vector2(32, -104), false); Grid(v.ChestSlots, new Vector2(32, -352), true);
                foreach (var slot in v.BagSlots.Concat(v.ChestSlots))
                {
                    if (slot.GetComponent<InventorySlotInput>() == null) slot.gameObject.AddComponent<InventorySlotInput>();
                    slot.NameText.rectTransform.anchoredPosition = new Vector2(4, -34); slot.NameText.rectTransform.sizeDelta = new Vector2(90, 20); slot.NameText.fontSize = slot.NameText.fontSizeMax = 12;
                    slot.Icon.rectTransform.anchoredPosition = new Vector2(9, -6); slot.Icon.rectTransform.sizeDelta = new Vector2(24, 24);
                    slot.QuantityText.rectTransform.anchoredPosition = new Vector2(59, -10); slot.QuantityText.rectTransform.sizeDelta = new Vector2(32, 20); slot.QuantityText.fontSize = 16;
                }
                for (int i = 0; i < v.BagSlots.Length; i++)
                {
                    var slot = v.BagSlots[i];
                    if (slot.LockIcon == null)
                    {
                        var lockRect = Factory.Rect("Lock", slot.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(37, -6), new Vector2(24, 24));
                        slot.LockIcon = lockRect.gameObject.AddComponent<InventoryLockGraphic>(); slot.LockIcon.color = new Color(.46f, .5f, .43f); slot.LockIcon.raycastTarget = false; slot.LockIcon.enabled = false;
                    }
                    slot.gameObject.SetActive(true); if (i >= 10) slot.RenderLocked();
                }
                var details = (RectTransform)v.DetailsText.transform.parent; details.anchoredPosition = new Vector2(32, -564); details.sizeDelta = new Vector2(1096, 30);
                v.DetailsText.rectTransform.sizeDelta = new Vector2(1064, 24); v.DetailsText.fontSize = 17;
                v.MoveOneButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(32, -604); v.MoveOneButton.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 36);
                v.MoveStackButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(248, -604); v.MoveStackButton.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 36);
                v.SaveButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(928, -604); v.SaveButton.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 36);
                v.FeedbackText.rectTransform.anchoredPosition = new Vector2(32, -646); v.FeedbackText.rectTransform.sizeDelta = new Vector2(1096, 24); v.FeedbackText.fontSize = 16;
                var hint = root.GetComponentsInChildren<TMP_Text>(true).First(t => t.name == "Controls"); hint.rectTransform.anchoredPosition = new Vector2(32, -674); hint.rectTransform.sizeDelta = new Vector2(1096, 20); hint.fontSize = 15;
                hint.text = "Shift + 点击转移整组 · 拖拽物品存取 · Esc 关闭";
                v.DepositMatchingButton = Factory.Button("Deposit Matching", panel, new Vector2(720, -24), new Vector2(320, 40), "存入同类", new Color(.7f, .81f, .62f));
                v.PageControls = Factory.Rect("Bag Pages", panel, new Vector2(0, 1), new Vector2(0, 1), new Vector2(832, -70), new Vector2(296, 28));
                v.PreviousPageButton = Factory.Button("Previous Page", v.PageControls, Vector2.zero, new Vector2(80, 28), "< 上页", Factory.Sand);
                v.NextPageButton = Factory.Button("Next Page", v.PageControls, new Vector2(216, 0), new Vector2(80, 28), "下页 >", Factory.Sand);
                v.PageText = Factory.Label("Page", v.PageControls, new Vector2(86, 0), new Vector2(124, 28), "第 1 / 2 页", 15, TextAnchor.MiddleCenter);
                v.PreviousPageButton.GetComponentInChildren<TMP_Text>().fontSize = v.NextPageButton.GetComponentInChildren<TMP_Text>().fontSize = 15; v.PageControls.gameObject.SetActive(false);
                v.DragPreview = Factory.Panel("Drag Preview", root.transform, Vector2.one * .5f, Vector2.one * .5f, Vector2.zero, new Vector2(142, 110), Factory.Cream);
                var group = v.DragPreview.gameObject.AddComponent<CanvasGroup>(); group.blocksRaycasts = false; group.interactable = false; Factory.SoftShadow(v.DragPreview);
                v.DragIcon = Factory.Panel("Icon", v.DragPreview, new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -12), new Vector2(38, 38), Color.white).GetComponent<Image>();
                v.DragQuantityText = Factory.Label("Quantity", v.DragPreview, new Vector2(64, -12), new Vector2(64, 38), "", 20, TextAnchor.MiddleRight);
                v.DragNameText = Factory.Label("Name", v.DragPreview, new Vector2(10, -58), new Vector2(122, 38), "", 17, TextAnchor.MiddleCenter); v.DragPreview.gameObject.SetActive(false);
                v.ShortcutsLayout = true; PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        private static void Grid(InventorySlotView[] slots, Vector2 position, bool scrollable)
        {
            var scroll = slots[0].GetComponentInParent<ScrollRect>(); var rect = (RectTransform)scroll.transform;
            rect.anchoredPosition = position; rect.sizeDelta = new Vector2(1096, 202); scroll.horizontal = false; scroll.vertical = scrollable;
            scroll.content.sizeDelta = new Vector2(1096, 202); scroll.content.anchoredPosition = Vector2.zero;
            var grid = scroll.content.GetComponent<GridLayoutGroup>(); grid.constraintCount = 10; grid.cellSize = new Vector2(98, 58); grid.spacing = new Vector2(8, 6); grid.padding = new RectOffset(22, 22, 8, 8);
        }
    }
}
