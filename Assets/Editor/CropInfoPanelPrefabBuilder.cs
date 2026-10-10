using System.IO;
using MagicIsland.UI.Views;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Factory = MagicIsland.EditorTools.PrototypeUiPrefabBuilder;

namespace MagicIsland.EditorTools
{
    public static class CropInfoPanelPrefabBuilder
    {
        public static void CreateMissing()
        {
            string path = Factory.Folder + "/CropInfoPanel.prefab";
            if (File.Exists(path)) return;
            GameObject root = Factory.CanvasRoot("CropInfoPanel");
            try
            {
                CropInfoView view = root.AddComponent<CropInfoView>(); view.Layer = UILayer.Common;
                view.Group = root.AddComponent<CanvasGroup>(); view.Group.alpha = 0;
                view.Group.interactable = false; view.Group.blocksRaycasts = false;
                var card = Factory.Panel("Crop Info", root.transform, new Vector2(1, 1), new Vector2(1, 1),
                    new Vector2(-20, -150), new Vector2(310, 160), Factory.Cream); Factory.SoftShadow(card);
                view.NameText = Factory.Label("Crop Name", card, new Vector2(20, -12), new Vector2(270, 32), "芜菁", 24);
                view.DetailsText = Factory.Label("Crop Details", card, new Vector2(20, -52), new Vector2(270, 60), "幼苗 · 成长 0 / 3\n今天未浇水", 20);
                view.HintText = Factory.Label("Crop Hint", card, new Vector2(20, -120), new Vector2(270, 28), "浇水", 18);
                foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
