using System.IO;
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
    public static class StorageSetup
    {
        public static void Ensure(AddressableAssetSettings settings, AddressableAssetGroup configuration)
        {
            const string path = "Assets/Config/StorageConfig.asset";
            if (AssetDatabase.LoadAssetAtPath<StorageConfig>(path) == null) AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<StorageConfig>(), path);
            var entry = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(path)) ?? settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), configuration);
            if (entry.address != StorageConfig.Address) entry.SetAddress(StorageConfig.Address);
            CreateChest(); const string chest = "Assets/Art/Prefabs/Buildings/StorageChest.prefab";
            var model = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(chest)) ?? settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(chest), settings.FindGroup("Buildings"));
            if (model.address != "StorageChest") model.SetAddress("StorageChest");
        }
        public static void CreatePanel()
        {
            string path = Factory.Folder + "/StoragePanel.prefab"; if (File.Exists(path)) return;
            var root = Factory.CanvasRoot("StoragePanel");
            try
            {
                var view = root.AddComponent<StorageView>(); view.Layer = UILayer.PopUp;
                var shade = Factory.Panel("Backdrop", root.transform, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, new Color(.12f, .2f, .15f, .58f));
                shade.anchorMax = Vector2.one; shade.GetComponent<Image>().sprite = null;
                var panel = Factory.Panel("Storage Card", root.transform, Vector2.one * .5f, Vector2.one * .5f, Vector2.zero, new Vector2(1000, 620), Factory.Cream); Factory.SoftShadow(panel);
                Factory.Label("Title", panel, new Vector2(32, -20), new Vector2(820, 44), "农场储物箱", 30);
                view.CloseButton = Factory.Button("Close", panel, new Vector2(914, -22), new Vector2(54, 42), "×", Factory.Sand);
                view.BagCapacityText = Factory.Label("Bag Capacity", panel, new Vector2(32, -80), new Vector2(452, 30), "随身背包 · 1 / 12 格", 22);
                view.ChestCapacityText = Factory.Label("Chest Capacity", panel, new Vector2(516, -80), new Vector2(452, 30), "储物箱 · 0 / 18 格", 22);
                view.BagSlots = Grid(panel, "Bag", 32); view.ChestSlots = Grid(panel, "Chest", 516);
                var details = Factory.Panel("Details", panel, new Vector2(0, 1), new Vector2(0, 1), new Vector2(32, -424), new Vector2(936, 40), Factory.Sand);
                view.DetailsText = Factory.Label("Selected Item", details, new Vector2(16, -4), new Vector2(904, 32), "选择左侧物品存入，或选择右侧物品取出", 19);
                view.MoveOneButton = Factory.Button("Move One", panel, new Vector2(32, -480), new Vector2(200, 44), "存入一个 →", new Color(.70f, .81f, .62f));
                view.MoveStackButton = Factory.Button("Move Stack", panel, new Vector2(248, -480), new Vector2(200, 44), "存入整组 →", new Color(.70f, .81f, .62f));
                view.SaveButton = Factory.Button("Save Progress", panel, new Vector2(768, -480), new Vector2(200, 44), "保存进度", Factory.Sand);
                view.FeedbackText = Factory.Label("Feedback", panel, new Vector2(32, -540), new Vector2(936, 30), "物品需要取回背包后才能使用 · 成功存取自动保存", 18);
                Factory.Label("Controls", panel, new Vector2(32, -584), new Vector2(936, 24), "点击格子选择 · 滚轮查看更多物品 · ESC 关闭", 16);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
        }
        private static InventorySlotView[] Grid(Transform panel, string name, float x)
        {
            var background = Factory.Panel(name + " Background", panel, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, -124), new Vector2(452, 284), new Color(.82f, .84f, .74f));
            var scroll = background.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 25;
            var viewport = Factory.Rect("Viewport", background, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero); viewport.anchorMax = Vector2.one; viewport.gameObject.AddComponent<RectMask2D>();
            var content = Factory.Rect("Slots", viewport, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(452, 0));
            var grid = content.gameObject.AddComponent<GridLayoutGroup>(); grid.cellSize = new Vector2(66, 80); grid.spacing = new Vector2(6, 6); grid.padding = new RectOffset(13, 13, 13, 13);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 6; content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport; scroll.content = content;
            var result = new InventorySlotView[36]; var icon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Textures/UI/RoundedPanel.png");
            for (int i = 0; i < result.Length; i++)
            {
                var button = Factory.Button("Slot " + (i + 1), content, Vector2.zero, new Vector2(66, 80), "空格", Factory.Sand);
                var slot = button.gameObject.AddComponent<InventorySlotView>(); slot.SelectButton = button; slot.NameText = button.GetComponentInChildren<TMP_Text>();
                slot.NameText.fontSize = 13; slot.NameText.enableAutoSizing = true; slot.NameText.fontSizeMin = 10; slot.NameText.fontSizeMax = 13;
                slot.NameText.rectTransform.anchoredPosition = new Vector2(4, -54); slot.NameText.rectTransform.sizeDelta = new Vector2(58, 22);
                var iconRect = Factory.Rect("Icon", button.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(18, -8), new Vector2(30, 30));
                slot.Icon = iconRect.gameObject.AddComponent<Image>(); slot.Icon.sprite = icon; slot.Icon.raycastTarget = false;
                slot.QuantityText = Factory.Label("Quantity", button.transform, new Vector2(28, -31), new Vector2(32, 22), "", 16, TextAnchor.MiddleRight); result[i] = slot;
            }
            return result;
        }
        private static void Box(Transform parent, string name, Vector3 position, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = scale; go.GetComponent<Renderer>().sharedMaterial = mat;
        }
        private static void CreateChest()
        {
            const string path = "Assets/Art/Prefabs/Buildings/StorageChest.prefab"; if (File.Exists(path)) return;
            var wood = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Buildings/Hall Wood.mat");
            var sage = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Buildings/Hall Sage.mat");
            var metal = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Buildings/Board Pins.mat");
            var paper = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Buildings/Board Paper.mat");
            var root = new GameObject("StorageChest", typeof(StorageChest));
            try
            {
                Box(root.transform, "Body", new Vector3(0, .35f, 0), new Vector3(1.30f, .66f, .82f), wood);
                Box(root.transform, "Lid", new Vector3(0, .72f, 0), new Vector3(1.38f, .14f, .90f), sage);
                for (int x = -1; x <= 1; x += 2)
                {
                    Box(root.transform, "Front Band", new Vector3(x * .44f, .34f, -.42f), new Vector3(.08f, .65f, .035f), sage);
                    Box(root.transform, "Lid Band", new Vector3(x * .44f, .80f, 0), new Vector3(.08f, .025f, .89f), metal);
                    Box(root.transform, "Foot", new Vector3(x * .48f, .03f, 0), new Vector3(.15f, .06f, .60f), wood);
                }
                Box(root.transform, "Latch", new Vector3(0, .58f, -.46f), new Vector3(.13f, .22f, .045f), metal);
                Box(root.transform, "Name Plate", new Vector3(0, .33f, -.43f), new Vector3(.70f, .19f, .035f), paper);
                var label = new GameObject("Chest Label", typeof(TextMeshPro)); label.transform.SetParent(root.transform, false); label.transform.localPosition = new Vector3(0, .33f, -.454f);
                var text = label.GetComponent<TextMeshPro>(); text.font = UiPrefabMigration.FontAsset; text.text = "农场储物箱"; text.fontSize = .85f; text.rectTransform.sizeDelta = new Vector2(.66f, .16f);
                text.alignment = TextAlignmentOptions.Center; text.color = new Color(.24f, .29f, .19f); PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
