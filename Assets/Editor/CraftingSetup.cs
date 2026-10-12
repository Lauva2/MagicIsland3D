using System.IO;
using MagicIsland.Configuration;
using MagicIsland.Inventory;
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
    public static class CraftingSetup
    {
        static readonly string[] Ids = { "wood_fence", "stone_path", "garden_lamp" };
        static readonly string[] Names = { "木栅栏", "石板路", "庭院灯" };
        static readonly string[] Addresses = { "WoodFence", "StonePath", "GardenLamp" };
        public static void Ensure(AddressableAssetSettings settings, AddressableAssetGroup group, ItemCatalog items)
        {
            for (int i = 0; i < 3; i++) if (items.Find(Ids[i]) == null)
            {
                items.Items.Add(new ItemConfig { Id = Ids[i], DisplayName = Names[i], Type = ItemType.Placeable, MaxStack = 99,
                    Description = "从背包选择摆放，R 旋转。已摆放后走近右键回收。", Icon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Textures/UI/RoundedPanel.png"),
                    IconColor = i == 0 ? new Color(.69f, .46f, .29f) : i == 1 ? new Color(.57f, .64f, .63f) : new Color(.93f, .73f, .37f) });
                EditorUtility.SetDirty(items);
            }
            const string path = "Assets/Config/CraftingCatalog.asset";
            if (AssetDatabase.LoadAssetAtPath<CraftingCatalog>(path) == null)
            {
                var catalog = ScriptableObject.CreateInstance<CraftingCatalog>();
                for (int i = 0; i < 3; i++)
                {
                    var r = new CraftingRecipeConfig { Id = Ids[i], DisplayName = Names[i], OutputItemId = Ids[i], PrefabAddress = Addresses[i] };
                    if (i != 1) r.Ingredients.Add(new CraftingIngredientConfig { ItemId = "wood", Quantity = i == 0 ? 2 : 3 });
                    if (i != 0) r.Ingredients.Add(new CraftingIngredientConfig { ItemId = "stone", Quantity = 2 });
                    catalog.Recipes.Add(r);
                }
                AssetDatabase.CreateAsset(catalog, path);
            }
            Register(settings, group, path, CraftingCatalog.Address);
            Directory.CreateDirectory("Assets/Art/Prefabs/Decorations"); AssetDatabase.Refresh();
            for (int i = 0; i < 3; i++) { string prefab = "Assets/Art/Prefabs/Decorations/" + Addresses[i] + ".prefab"; CreateModel(prefab, i); Register(settings, settings.FindGroup("Buildings"), prefab, Addresses[i]); }
        }
        static void Register(AddressableAssetSettings settings, AddressableAssetGroup group, string path, string address)
        { string guid = AssetDatabase.AssetPathToGUID(path); var e = settings.FindAssetEntry(guid) ?? settings.CreateOrMoveEntry(guid, group); if (e.address != address) e.SetAddress(address); }
        static void Part(Transform root, string name, Vector3 point, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(root, false);
            go.transform.localPosition = point; go.transform.localScale = size; go.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(go.GetComponent<Collider>());
        }
        static void CreateModel(string path, int kind)
        {
            if (File.Exists(path)) return;
            var wood = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Buildings/Hall Wood.mat");
            var stone = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Buildings/Hall Stone.mat");
            var sage = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Buildings/Hall Sage.mat");
            var paper = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Buildings/Board Paper.mat");
            var root = new GameObject(Addresses[kind], typeof(PlacedDecoration), typeof(BoxCollider));
            try
            {
                var c = root.GetComponent<BoxCollider>();
                if (kind == 0)
                {
                    c.center = new Vector3(0, .5f, 0); c.size = new Vector3(.9f, 1, .18f);
                    for (int side = -1; side <= 1; side += 2) { Part(root.transform, "Post", new Vector3(side * .36f, .5f, 0), new Vector3(.14f, 1, .18f), wood); Part(root.transform, "Cap", new Vector3(side * .36f, 1, 0), new Vector3(.18f, .09f, .22f), sage); }
                    Part(root.transform, "Upper Rail", new Vector3(0, .76f, 0), new Vector3(.87f, .13f, .12f), wood);
                    Part(root.transform, "Lower Rail", new Vector3(0, .35f, 0), new Vector3(.87f, .13f, .12f), wood);
                }
                else if (kind == 1)
                {
                    c.center = new Vector3(0, .035f, 0); c.size = new Vector3(.9f, .07f, .9f);
                    Part(root.transform, "Foundation", new Vector3(0, .02f, 0), new Vector3(.9f, .04f, .9f), stone);
                    for (int x = -1; x <= 1; x += 2) for (int z = -1; z <= 1; z += 2)
                        Part(root.transform, "Paver", new Vector3(x * .225f, .055f, z * .225f), new Vector3(.42f, .03f, .42f), paper);
                }
                else
                {
                    c.center = new Vector3(0, .77f, 0); c.size = new Vector3(.42f, 1.54f, .42f);
                    Part(root.transform, "Foot", new Vector3(0, .08f, 0), new Vector3(.4f, .16f, .4f), stone);
                    Part(root.transform, "Post", new Vector3(0, .61f, 0), new Vector3(.14f, 1.06f, .14f), wood);
                    Part(root.transform, "Lantern", new Vector3(0, 1.3f, 0), new Vector3(.32f, .34f, .32f), paper);
                    Part(root.transform, "Roof", new Vector3(0, 1.52f, 0), new Vector3(.42f, .1f, .42f), sage);
                    for (int x = -1; x <= 1; x += 2) for (int z = -1; z <= 1; z += 2) Part(root.transform, "Frame", new Vector3(x * .15f, 1.3f, z * .15f), new Vector3(.04f, .4f, .04f), wood);
                    var light = new GameObject("Warm Light", typeof(Light)); light.transform.SetParent(root.transform, false); light.transform.localPosition = new Vector3(0, 1.3f, 0);
                    var lamp = light.GetComponent<Light>(); lamp.type = LightType.Point; lamp.color = new Color(1, .8f, .45f); lamp.intensity = 1.5f; lamp.range = 3;
                }
                root.SetActive(false); PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
        }
        public static void CreatePanels()
        {
            string path = Factory.Folder + "/CraftingPanel.prefab";
            if (!File.Exists(path))
            {
                var root = Factory.CanvasRoot("CraftingPanel");
                try
                {
                    var v = root.AddComponent<CraftingView>(); v.Layer = UILayer.PopUp;
                    var shade = Factory.Panel("Backdrop", root.transform, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, new Color(.12f, .2f, .15f, .58f)); shade.anchorMax = Vector2.one; shade.GetComponent<Image>().sprite = null;
                    var panel = Factory.Panel("Crafting Card", root.transform, Vector2.one * .5f, Vector2.one * .5f, Vector2.zero, new Vector2(940, 470), Factory.Cream); Factory.SoftShadow(panel);
                    Factory.Label("Title", panel, new Vector2(30, -20), new Vector2(750, 44), "岛屿工作台 · 制作装饰", 30);
                    v.CloseButton = Factory.Button("Close", panel, new Vector2(856, -24), new Vector2(54, 42), "×", Factory.Sand);
                    Factory.Label("Introduction", panel, new Vector2(30, -80), new Vector2(880, 30), "用采集到的木材和石料，布置自己的农场", 20);
                    v.NameTexts = new TMP_Text[3]; v.CostTexts = new TMP_Text[3]; v.ResultTexts = new TMP_Text[3]; v.CraftButtons = new Button[3];
                    for (int i = 0; i < 3; i++)
                    {
                        var card = Factory.Panel("Recipe " + i, panel, new Vector2(0, 1), new Vector2(0, 1), new Vector2(30 + i * 298, -126), new Vector2(284, 218), Factory.Sand);
                        v.NameTexts[i] = Factory.Label("Name", card, new Vector2(18, -14), new Vector2(248, 34), Names[i], 25);
                        v.CostTexts[i] = Factory.Label("Materials", card, new Vector2(18, -60), new Vector2(248, 62), "木材 × 3 · 持有 0\n石料 × 2 · 持有 0", 18);
                        v.ResultTexts[i] = Factory.Label("Output", card, new Vector2(18, -124), new Vector2(248, 28), "产出 1 件 · 背包选择摆放", 17);
                        v.CraftButtons[i] = Factory.Button("Craft", card, new Vector2(18, -164), new Vector2(248, 40), "制作一份", new Color(.7f, .81f, .62f));
                    }
                    v.FeedbackText = Factory.Label("Feedback", panel, new Vector2(30, -356), new Vector2(880, 36), "制作后打开背包，选择成品进行摆放", 18);
                    v.UpgradeButton = Factory.Button("Tool Upgrades", panel, new Vector2(30, -414), new Vector2(200, 34), "工具升级", Factory.Sand);
                    v.SaveButton = Factory.Button("Save Progress", panel, new Vector2(710, -414), new Vector2(200, 34), "保存进度", Factory.Sand);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { Object.DestroyImmediate(root); }
            }
            path = Factory.Folder + "/PlacementPanel.prefab";
            if (File.Exists(path)) return;
            var previewRoot = Factory.CanvasRoot("PlacementPanel");
            try
            {
                var v = previewRoot.AddComponent<PlacementView>(); v.Layer = UILayer.Common;
                var panel = Factory.Panel("Placement Card", previewRoot.transform, Vector2.one, Vector2.one, new Vector2(-20, -140), new Vector2(430, 178), Factory.Cream); Factory.SoftShadow(panel);
                v.TitleText = Factory.Label("Title", panel, new Vector2(18, -10), new Vector2(394, 30), "木栅栏 · 摆放", 23);
                v.StatusText = Factory.Label("Status", panel, new Vector2(18, -44), new Vector2(394, 46), "位置可用 · 点击确认摆放\n位置 -6, -6 · 朝向 90°", 17);
                v.ConfirmButton = Factory.Button("Confirm", panel, new Vector2(18, -96), new Vector2(122, 34), "确认摆放", new Color(.7f, .81f, .62f));
                v.RotateButton = Factory.Button("Rotate", panel, new Vector2(154, -96), new Vector2(122, 34), "旋转 (R)", Factory.Sand);
                v.CancelButton = Factory.Button("Cancel", panel, new Vector2(290, -96), new Vector2(122, 34), "取消", Factory.Sand);
                v.FeedbackText = Factory.Label("Feedback", panel, new Vector2(18, -138), new Vector2(270, 28), "WASD 移动 · R 旋转 · ESC 取消", 14);
                v.FeedbackText.enableAutoSizing = true; v.FeedbackText.fontSizeMin = 10; v.FeedbackText.fontSizeMax = 14;
                v.SaveButton = Factory.Button("Save Progress", panel, new Vector2(300, -139), new Vector2(112, 26), "保存进度", Factory.Sand);
                v.SaveButton.GetComponentInChildren<TMP_Text>().fontSize = 15;
                PrefabUtility.SaveAsPrefabAsset(previewRoot, path);
            }
            finally { Object.DestroyImmediate(previewRoot); }
        }
        public static void UpgradePanels()
        {
            var path = Factory.Folder + "/InventoryPanel.prefab"; var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var v = root.GetComponent<InventoryView>();
                if (v.PlaceButton == null) { v.PlaceButton = Factory.Button("Place Item", v.UseFoodButton.transform.parent, v.UseFoodButton.GetComponent<RectTransform>().anchoredPosition, v.UseFoodButton.GetComponent<RectTransform>().sizeDelta, "摆放一个", new Color(.7f, .81f, .62f)); PrefabUtility.SaveAsPrefabAsset(root, path); }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            path = Factory.Folder + "/ToolUpgradePanel.prefab"; root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var v = root.GetComponent<ToolUpgradeView>();
                if (v.CraftingButton == null)
                {
                    v.CraftingButton = Factory.Button("Craft Decorations", v.SaveButton.transform.parent, new Vector2(32, -424), new Vector2(200, 36), "制作装饰", Factory.Sand);
                    var hint = v.SaveButton.transform.parent.Find("Hint"); if (hint != null) hint.gameObject.SetActive(false);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
