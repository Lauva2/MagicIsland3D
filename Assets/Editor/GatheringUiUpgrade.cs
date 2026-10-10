using MagicIsland.Interaction;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Factory = MagicIsland.EditorTools.PrototypeUiPrefabBuilder;
namespace MagicIsland.EditorTools
{
    public static class GatheringUiUpgrade
    {
        public static void Apply()
        {
            string path = Factory.Folder + "/FarmHudPanel.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var hud = root.GetComponent<PrototypeHud>(); if (hud.ToolButtons.Length >= 5) return;
                Transform toolbar = hud.ToolButtons[0].transform.parent;
                toolbar.GetComponent<RectTransform>().sizeDelta = new Vector2(940, 120);
                var buttons = new Button[5]; System.Array.Copy(hud.ToolButtons, buttons, 3);
                buttons[3] = Factory.Button("Axe", toolbar, Vector2.zero, new Vector2(104, 44), "4 斧头", Factory.Sand);
                buttons[4] = Factory.Button("Pickaxe", toolbar, Vector2.zero, new Vector2(104, 44), "5 镐子", Factory.Sand);
                for (int i = 0; i < buttons.Length; i++)
                {
                    var rect = buttons[i].GetComponent<RectTransform>(); rect.anchoredPosition = new Vector2(16 + i * 112, -14); rect.sizeDelta = new Vector2(104, 44);
                    var text = buttons[i].GetComponentInChildren<TMP_Text>(); text.rectTransform.sizeDelta = rect.sizeDelta; text.fontSize = 20;
                }
                hud.ToolButtons = buttons; hud.HintText.rectTransform.anchoredPosition = new Vector2(584, -14);
                hud.HintText.rectTransform.sizeDelta = new Vector2(340, 44); hud.HintText.fontSize = 18;
                hud.HintText.enableAutoSizing = true; hud.HintText.fontSizeMin = 15; hud.HintText.fontSizeMax = 18;
                hud.FeedbackText.rectTransform.sizeDelta = new Vector2(908, 35);
                hud.InventoryText.fontSize = 19; hud.InventoryText.enableAutoSizing = true; hud.InventoryText.fontSizeMin = 15; hud.InventoryText.fontSizeMax = 19;
                hud.InventoryText.text = "种子 50  作物 0  木材 0  石料 0";
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
