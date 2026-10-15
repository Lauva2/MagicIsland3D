using MagicIsland.UI.Views;
using UnityEditor;
using UnityEngine;
using Factory = MagicIsland.EditorTools.PrototypeUiPrefabBuilder;

namespace MagicIsland.EditorTools
{
    public static class InventorySortSetup
    {
        public static void UpgradePanel()
        {
            string path = Factory.Folder + "/InventoryPanel.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view = root.GetComponent<InventoryView>();
                if (view.SortButton != null) return;
                view.SortButton = Factory.Button("Sort", view.CloseButton.transform.parent,
                    new Vector2(554, -26), new Vector2(150, 38), "整理", new Color(.7f, .81f, .62f));
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
