using System.Linq;
using MagicIsland.UI.Views;
using UnityEditor;
using UnityEngine;
using Factory = MagicIsland.EditorTools.PrototypeUiPrefabBuilder;

namespace MagicIsland.EditorTools
{
    public static class InventoryInteractionSetup
    {
        public static void UpgradePanel()
        {
            string path = Factory.Folder + "/InventoryPanel.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view = root.GetComponent<InventoryView>(); bool changed = false;
                foreach (var slot in view.Slots)
                    if (slot.GetComponent<InventorySlotInput>() == null) { slot.gameObject.AddComponent<InventorySlotInput>(); changed = true; }
                if (view.DragPreview == null)
                {
                    view.DragPreview = Factory.Panel("Drag Preview", root.transform, Vector2.one * .5f, Vector2.one * .5f, Vector2.zero, new Vector2(142, 110), Factory.Cream);
                    var group = view.DragPreview.gameObject.AddComponent<CanvasGroup>(); group.blocksRaycasts = false; group.interactable = false;
                    Factory.SoftShadow(view.DragPreview);
                    view.DragIcon = Factory.Panel("Icon", view.DragPreview, new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -12), new Vector2(38, 38), Color.white).GetComponent<UnityEngine.UI.Image>();
                    view.DragQuantityText = Factory.Label("Quantity", view.DragPreview, new Vector2(64, -12), new Vector2(64, 38), "99", 20, TextAnchor.MiddleRight);
                    view.DragNameText = Factory.Label("Name", view.DragPreview, new Vector2(10, -58), new Vector2(122, 38), "拖拽物品", 17, TextAnchor.MiddleCenter);
                    view.DragPreview.gameObject.SetActive(false); changed = true;
                    var hint = root.GetComponentsInChildren<TMPro.TMP_Text>(true).FirstOrDefault(t => t.name == "Controls");
                    if (hint != null) { hint.text = "左键拖拽 · 右键拆分 · B / Esc 关闭"; hint.rectTransform.sizeDelta = new Vector2(548, hint.rectTransform.sizeDelta.y); }
                }
                if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
