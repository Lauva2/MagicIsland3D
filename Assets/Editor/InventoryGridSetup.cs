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
    public static class InventoryGridSetup
    {
        public static void UpgradePanel()
        {
            string path = Factory.Folder + "/InventoryPanel.prefab"; var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var v = root.GetComponent<InventoryView>();
                bool repaired = false;
                if (v.Slots.Length > InventoryView.DisplaySlots)
                {
                    foreach (var extra in v.Slots.Skip(InventoryView.DisplaySlots)) Object.DestroyImmediate(extra.gameObject);
                    v.Slots = v.Slots.Take(InventoryView.DisplaySlots).ToArray(); repaired = true;
                }
                foreach (var slot in v.Slots)
                    if (slot.LockIcon != null && slot.LockIcon.GetComponent<CanvasRenderer>() == null)
                    { slot.LockIcon.gameObject.AddComponent<CanvasRenderer>(); slot.LockIcon.SetVerticesDirty(); slot.LockIcon.SetMaterialDirty(); repaired = true; }
                if (v.ThreeRowLayout) { if (repaired) PrefabUtility.SaveAsPrefabAsset(root, path); return; }
                var panel = (RectTransform)v.CloseButton.transform.parent; panel.sizeDelta = new Vector2(1120, 690);
                v.CloseButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(1030, -24);
                v.SortButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(854, -26);
                v.CapacityText.rectTransform.sizeDelta = new Vector2(640, 28);
                var scroll = root.GetComponentInChildren<ScrollRect>(true); scroll.horizontal = scroll.vertical = false;
                ((RectTransform)scroll.transform).sizeDelta = new Vector2(1052, 322);
                scroll.content.sizeDelta = new Vector2(1052, 322); scroll.content.anchoredPosition = Vector2.zero;
                var grid = scroll.content.GetComponent<GridLayoutGroup>(); grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = InventoryView.Columns; grid.cellSize = new Vector2(94, 94); grid.spacing = new Vector2(8, 12); grid.padding = new RectOffset(20, 20, 8, 8);
                foreach (var slot in v.Slots)
                {
                    slot.NameText.rectTransform.anchoredPosition = new Vector2(7, -65); slot.NameText.rectTransform.sizeDelta = new Vector2(80, 23); slot.NameText.fontSize = 13;
                    slot.Icon.rectTransform.anchoredPosition = new Vector2(29.5f, -10);
                    slot.QuantityText.rectTransform.anchoredPosition = new Vector2(49, -39);
                    if (slot.LockIcon == null)
                    {
                        var icon = Factory.Rect("Lock", slot.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(29.5f, -10), new Vector2(35, 35));
                        slot.LockIcon = icon.gameObject.AddComponent<InventoryLockGraphic>(); slot.LockIcon.color = new Color(.46f, .5f, .43f); slot.LockIcon.raycastTarget = false; slot.LockIcon.enabled = false;
                    }
                }
                for (int i = 0; i < v.Slots.Length; i++)
                { v.Slots[i].gameObject.SetActive(i < InventoryView.DisplaySlots); if (i >= 10 && i < InventoryView.DisplaySlots) v.Slots[i].RenderLocked(); }
                var details = (RectTransform)v.DetailText.transform.parent; details.anchoredPosition = new Vector2(34, -448); details.sizeDelta = new Vector2(1052, 148);
                v.DetailTitleText.rectTransform.sizeDelta = new Vector2(1012, 28); v.DetailText.rectTransform.sizeDelta = new Vector2(780, 98);
                foreach (var button in new[] { v.UseSeedButton, v.UseFoodButton, v.PlaceButton }) button.GetComponent<RectTransform>().anchoredPosition = new Vector2(820, -56);
                v.FeedbackText.rectTransform.anchoredPosition = new Vector2(34, -610); v.FeedbackText.rectTransform.sizeDelta = new Vector2(1052, 30);
                v.SaveButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(906, -648);
                var controls = root.GetComponentsInChildren<TMP_Text>(true).First(t => t.name == "Controls"); controls.rectTransform.anchoredPosition = new Vector2(34, -652);
                v.PageControls = Factory.Rect("Legacy Pages", panel, new Vector2(0, 1), new Vector2(0, 1), new Vector2(754, -74), new Vector2(332, 30));
                v.PreviousPageButton = Factory.Button("Previous Page", v.PageControls, Vector2.zero, new Vector2(80, 30), "< 上页", Factory.Sand);
                v.NextPageButton = Factory.Button("Next Page", v.PageControls, new Vector2(252, 0), new Vector2(80, 30), "下页 >", Factory.Sand);
                v.PageText = Factory.Label("Page", v.PageControls, new Vector2(86, 0), new Vector2(160, 30), "第 1 / 2 页", 16, TextAnchor.MiddleCenter);
                v.PreviousPageButton.GetComponentInChildren<TMP_Text>().fontSize = v.NextPageButton.GetComponentInChildren<TMP_Text>().fontSize = 16;
                v.PageControls.gameObject.SetActive(false); v.ThreeRowLayout = true;
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
