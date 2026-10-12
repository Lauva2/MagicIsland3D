using System.IO;
using MagicIsland.Configuration;
using MagicIsland.Interaction;
using MagicIsland.Inventory;
using MagicIsland.UI.Views;
using TMPro;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.UI;
using Factory = MagicIsland.EditorTools.PrototypeUiPrefabBuilder;
namespace MagicIsland.EditorTools
{
    public static class CookingSetup
    {
        public static void Ensure(AddressableAssetSettings settings, AddressableAssetGroup configuration, ItemCatalog items)
        {
            AddFood(items, "turnip_soup", "芜菁汤", "热腾腾的芜菁汤，可在背包中食用恢复体力。", new Color(.94f, .77f, .48f));
            AddFood(items, "roasted_corn", "烤玉米", "香甜的烤玉米，可在背包中食用恢复体力。", new Color(.95f, .65f, .27f));
            const string path = "Assets/Config/CookingCatalog.asset";
            if (AssetDatabase.LoadAssetAtPath<CookingCatalog>(path) == null)
            {
                var catalog = ScriptableObject.CreateInstance<CookingCatalog>();
                catalog.Recipes.Add(new CookingRecipeConfig { Id = "turnip_soup", DisplayName = "芜菁汤", IngredientId = "turnip", IngredientQuantity = 2, FoodId = "turnip_soup", StaminaRecovery = 30 });
                catalog.Recipes.Add(new CookingRecipeConfig { Id = "roasted_corn", DisplayName = "烤玉米", IngredientId = "corn", FoodId = "roasted_corn", StaminaRecovery = 20 });
                AssetDatabase.CreateAsset(catalog, path);
            }
            var entry = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(path)) ?? settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), configuration);
            if (entry.address != CookingCatalog.Address) entry.SetAddress(CookingCatalog.Address);
            CreateStation(); const string station = "Assets/Art/Prefabs/Buildings/CookingStation.prefab";
            var model = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(station)) ?? settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(station), settings.FindGroup("Buildings"));
            if (model.address != "CookingStation") model.SetAddress("CookingStation");
        }
        private static void AddFood(ItemCatalog items, string id, string name, string description, Color color)
        {
            if (items.Find(id) != null) return;
            items.Items.Add(new ItemConfig { Id = id, DisplayName = name, Description = description, Type = ItemType.Food, MaxStack = 99, IconColor = color,
                Icon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Textures/UI/RoundedPanel.png") }); EditorUtility.SetDirty(items);
        }
        public static void CreatePanel()
        {
            string path = Factory.Folder + "/CookingPanel.prefab"; if (File.Exists(path)) return; var root = Factory.CanvasRoot("CookingPanel");
            try
            {
                var view = root.AddComponent<CookingView>(); view.Layer = UILayer.PopUp;
                var shade = Factory.Panel("Backdrop", root.transform, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, new Color(.12f, .2f, .15f, .58f)); shade.anchorMax = Vector2.one; shade.GetComponent<Image>().sprite = null;
                var panel = Factory.Panel("Cooking Card", root.transform, Vector2.one * .5f, Vector2.one * .5f, Vector2.zero, new Vector2(720, 456), Factory.Cream); Factory.SoftShadow(panel);
                Factory.Label("Title", panel, new Vector2(32, -22), new Vector2(560, 44), "农场料理台", 30);
                view.CloseButton = Factory.Button("Close", panel, new Vector2(634, -24), new Vector2(54, 42), "×", Factory.Sand);
                Factory.Label("Introduction", panel, new Vector2(32, -80), new Vector2(656, 30), "把收成变成料理，为今天的劳作补充体力", 19);
                view.TitleTexts = new TMP_Text[2]; view.IngredientTexts = new TMP_Text[2]; view.ResultTexts = new TMP_Text[2]; view.CookButtons = new Button[2];
                for (int i = 0; i < 2; i++)
                {
                    var card = Factory.Panel("Recipe " + i, panel, new Vector2(0, 1), new Vector2(0, 1), new Vector2(32 + 336 * i, -126), new Vector2(320, 218), Factory.Sand);
                    view.TitleTexts[i] = Factory.Label("Name", card, new Vector2(20, -18), new Vector2(280, 36), i == 0 ? "芜菁汤" : "烤玉米", 25);
                    view.IngredientTexts[i] = Factory.Label("Ingredient", card, new Vector2(20, -70), new Vector2(280, 30), "芜菁 × 2 · 持有 0", 18);
                    view.ResultTexts[i] = Factory.Label("Result", card, new Vector2(20, -110), new Vector2(280, 30), "制作 1 份 · 恢复 30 体力", 18);
                    view.CookButtons[i] = Factory.Button("Cook", card, new Vector2(20, -158), new Vector2(280, 44), "制作一份", new Color(.70f, .81f, .62f));
                }
                view.FeedbackText = Factory.Label("Feedback", panel, new Vector2(32, -360), new Vector2(656, 30), "使用背包中的作物制作 · 食物可在背包中食用", 18);
                view.SaveButton = Factory.Button("Save Progress", panel, new Vector2(488, -408), new Vector2(200, 32), "保存进度", Factory.Sand);
                Factory.Label("Controls", panel, new Vector2(32, -410), new Vector2(430, 26), "即时制作 · 自动保存 · ESC 关闭", 17);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
        }
        public static void UpgradeInventory()
        {
            const string path = "Assets/Art/Prefabs/UI/InventoryPanel.prefab"; var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view = root.GetComponent<InventoryView>(); if (view.UseFoodButton != null && view.SaveButton != null && view.FeedbackText != null) return;
                var panel = view.CapacityText.transform.parent; panel.GetComponent<RectTransform>().sizeDelta = new Vector2(820, 690);
                view.DetailText.rectTransform.sizeDelta = new Vector2(view.DetailText.rectTransform.sizeDelta.x, 108);
                view.DetailText.transform.parent.GetComponent<RectTransform>().sizeDelta = new Vector2(752, 158);
                if (view.UseFoodButton == null) view.UseFoodButton = Factory.Button("Use Food", view.DetailText.transform.parent, new Vector2(520, -56), new Vector2(210, 44), "食用一个", new Color(.70f, .81f, .62f));
                if (view.FeedbackText == null) view.FeedbackText = Factory.Label("Food Feedback", panel, new Vector2(34, -590), new Vector2(752, 30), "", 17);
                if (view.SaveButton == null) view.SaveButton = Factory.Button("Save Progress", panel, new Vector2(606, -636), new Vector2(180, 34), "保存进度", Factory.Sand);
                var controls = panel.Find("Controls"); if (controls != null) { controls.GetComponent<RectTransform>().anchoredPosition = new Vector2(34, -640); controls.GetComponent<RectTransform>().sizeDelta = new Vector2(540, 24); }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        private static void Box(Transform parent, string name, Vector3 position, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = scale; go.GetComponent<Renderer>().sharedMaterial = mat;
        }
        private static void CreateStation()
        {
            const string path = "Assets/Art/Prefabs/Buildings/CookingStation.prefab"; if (File.Exists(path)) return;
            var sage = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Buildings/Hall Sage.mat"); var wood = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Buildings/Hall Wood.mat");
            var stone = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Buildings/Hall Stone.mat"); var cream = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Buildings/Board Paper.mat");
            var root = new GameObject("CookingStation", typeof(CookingStation));
            try
            {
                Box(root.transform, "Cabinet", new Vector3(0, .40f, 0), new Vector3(1.6f, .8f, .85f), sage);
                Box(root.transform, "Counter", new Vector3(0, .85f, 0), new Vector3(1.7f, .12f, .95f), cream);
                Box(root.transform, "Oven Frame", new Vector3(-.4f, .42f, -.44f), new Vector3(.58f, .48f, .04f), stone);
                Box(root.transform, "Oven Door", new Vector3(-.4f, .41f, -.47f), new Vector3(.46f, .32f, .04f), wood);
                Box(root.transform, "Handle", new Vector3(-.4f, .55f, -.50f), new Vector3(.34f, .04f, .04f), cream);
                Box(root.transform, "Chopping Board", new Vector3(.45f, .93f, -.10f), new Vector3(.55f, .04f, .4f), wood);
                var pot = GameObject.CreatePrimitive(PrimitiveType.Cylinder); pot.name = "Soup Pot"; pot.transform.SetParent(root.transform, false); pot.transform.localPosition = new Vector3(-.4f, 1.03f, 0); pot.transform.localScale = new Vector3(.45f, .13f, .45f); pot.GetComponent<Renderer>().sharedMaterial = stone;
                Box(root.transform, "Pot Lid", new Vector3(-.4f, 1.18f, 0), new Vector3(.38f, .035f, .38f), sage);
                var label = new GameObject("Station Label", typeof(TextMeshPro)); label.transform.SetParent(root.transform, false); label.transform.localPosition = new Vector3(.38f, .44f, -.443f);
                var text = label.GetComponent<TextMeshPro>(); text.font = UiPrefabMigration.FontAsset; text.text = "农场料理台"; text.fontSize = .88f; text.rectTransform.sizeDelta = new Vector2(.69f, .18f); text.alignment = TextAlignmentOptions.Center; text.color = new Color(1, .97f, .83f);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
