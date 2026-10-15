using System.IO;
using MagicIsland.Configuration;
using MagicIsland.Interaction;
using MagicIsland.UI.Views;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Factory = MagicIsland.EditorTools.PrototypeUiPrefabBuilder;

namespace MagicIsland.EditorTools
{
    public static class InteractionPromptSetup
    {
        public const string HighlightPath = "Assets/Art/Prefabs/Effects/InteractionHighlight.prefab";
        public static void CreatePanel()
        {
            string path = Factory.Folder + "/InteractionPromptPanel.prefab"; if (File.Exists(path)) return;
            var root = Factory.CanvasRoot("InteractionPromptPanel");
            try
            {
                var view = root.AddComponent<InteractionPromptView>(); view.Layer = UILayer.Common;
                view.Group = root.AddComponent<CanvasGroup>(); view.Group.alpha = 0; view.Group.interactable = view.Group.blocksRaycasts = false;
                var card = Factory.Panel("Interaction Card", root.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-20, -150), new Vector2(310, 160), Factory.Cream);
                Factory.SoftShadow(card);
                var mark = Factory.Panel("Status Mark", card, new Vector2(0, 1), new Vector2(0, 1), new Vector2(18, -17), new Vector2(10, 10), new Color(.34f, .57f, .36f));
                view.StatusMark = mark.GetComponent<Image>();
                view.NameText = Factory.Label("Target Name", card, new Vector2(38, -10), new Vector2(172, 30), "办事处公告栏", 23);
                view.StatusText = Factory.Label("Target Status", card, new Vector2(216, -12), new Vector2(76, 28), "可操作", 17, TextAnchor.MiddleRight);
                view.OperationText = Factory.Label("Target Operation", card, new Vector2(18, -47), new Vector2(274, 30), "左键查看今日订单", 20);
                view.DetailsText = Factory.Label("Target Details", card, new Vector2(18, -84), new Vector2(274, 64), "交付订单、扩建小屋或建造鸡舍", 17);
                view.DetailsText.enableAutoSizing = true; view.DetailsText.fontSizeMin = 14; view.DetailsText.fontSizeMax = 17;
                view.NameText.enableAutoSizing = true; view.NameText.fontSizeMin = 17; view.NameText.fontSizeMax = 23;
                foreach (var graphic in root.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
        }
        public static void Ensure(AddressableAssetSettings settings, GameConfig config)
        {
            Directory.CreateDirectory("Assets/Art/Prefabs/Effects"); Directory.CreateDirectory("Assets/Art/Materials/Effects"); AssetDatabase.Refresh();
            const string materialPath = "Assets/Art/Materials/Effects/InteractionRing.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null) { material = new Material(Shader.Find("Sprites/Default")) { name = "Interaction Ring", color = Color.white }; AssetDatabase.CreateAsset(material, materialPath); }
            if (!File.Exists(HighlightPath))
            {
                var root = new GameObject("InteractionHighlight", typeof(LineRenderer), typeof(InteractionHighlight));
                try
                {
                    root.layer = 2; var line = root.GetComponent<LineRenderer>(); line.loop = true; line.positionCount = 48;
                    line.useWorldSpace = true; line.widthMultiplier = .035f; line.numCornerVertices = 2; line.sharedMaterial = material;
                    line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false;
                    root.GetComponent<InteractionHighlight>().Ring = line; root.SetActive(false); PrefabUtility.SaveAsPrefabAsset(root, HighlightPath);
                }
                finally { Object.DestroyImmediate(root); }
            }
            var group = settings.FindGroup("Effects") ?? settings.CreateGroup("Effects", false, false, true, null, typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            var guid = AssetDatabase.AssetPathToGUID(HighlightPath); var entry = settings.FindAssetEntry(guid) ?? settings.CreateOrMoveEntry(guid, group);
            if (entry.address != "InteractionHighlight") entry.SetAddress("InteractionHighlight");
            if (!File.ReadAllText("Assets/Config/GameConfig.asset").Contains("InteractionHighlightAddress:")) EditorUtility.SetDirty(config);
        }
    }
}
