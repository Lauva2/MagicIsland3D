using System;
using MagicIsland.Configuration;
using MagicIsland.Inventory;
using MagicIsland.UI.Views;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Factory = MagicIsland.EditorTools.PrototypeUiPrefabBuilder;
namespace MagicIsland.EditorTools
{
    public static class FishCookingSetup
    {
        public static void Ensure(ItemCatalog items)
        {
            AddFood(items, "grilled_sardine", "烤沙丁鱼", "香气扑鼻的烤鱼，可恢复 35 体力，也可作为料理送礼。", new Color(.83f, .66f, .40f));
            AddFood(items, "bass_vegetable_soup", "鲈鱼蔬菜汤", "鲈鱼与新鲜蔬菜熬成的汤，可恢复 50 体力，也可作为料理送礼。", new Color(.75f, .78f, .53f));
            var cooking = AssetDatabase.LoadAssetAtPath<CookingCatalog>("Assets/Config/CookingCatalog.asset");
            if (!cooking.Recipes.Exists(r => r.Id == "grilled_sardine"))
            { cooking.Recipes.Add(new CookingRecipeConfig { Id = "grilled_sardine", DisplayName = "烤沙丁鱼", IngredientId = "sardine", FoodId = "grilled_sardine", StaminaRecovery = 35 }); EditorUtility.SetDirty(cooking); }
            if (!cooking.Recipes.Exists(r => r.Id == "bass_vegetable_soup"))
            {
                var recipe = new CookingRecipeConfig { Id = "bass_vegetable_soup", DisplayName = "鲈鱼蔬菜汤", IngredientId = "sea_bass", FoodId = "bass_vegetable_soup", StaminaRecovery = 50 };
                recipe.AdditionalIngredients.Add(new CookingIngredientConfig { ItemId = "turnip" }); recipe.AdditionalIngredients.Add(new CookingIngredientConfig { ItemId = "tomato" }); cooking.Recipes.Add(recipe); EditorUtility.SetDirty(cooking);
            }
        }
        private static void AddFood(ItemCatalog items, string id, string name, string description, Color color)
        {
            if (items.Find(id) != null) return;
            items.Items.Add(new ItemConfig { Id = id, DisplayName = name, Description = description, Type = ItemType.Food, MaxStack = 99, IconColor = color,
                Icon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Textures/UI/RoundedPanel.png") }); EditorUtility.SetDirty(items);
        }
        public static void UpgradePanel()
        {
            string path = Factory.Folder + "/CookingPanel.prefab"; var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view = root.GetComponent<CookingView>(); if (view.CookButtons.Length >= 5 && view.RecipeScroll != null) return;
                var panel = view.FeedbackText.transform.parent; panel.GetComponent<RectTransform>().sizeDelta = new Vector2(1056, 640);
                var scrollRect = Factory.Rect("Recipe Scroll", panel, new Vector2(0, 1), new Vector2(0, 1), new Vector2(32, -126), new Vector2(992, 418));
                var scroll = scrollRect.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 30;
                var viewport = Factory.Rect("Viewport", scrollRect, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero); viewport.anchorMax = Vector2.one;
                viewport.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, .001f); viewport.gameObject.AddComponent<RectMask2D>();
                var content = Factory.Rect("Recipes", viewport, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(992, 668));
                var grid = content.gameObject.AddComponent<GridLayoutGroup>(); grid.cellSize = new Vector2(320, 326); grid.spacing = new Vector2(16, 16); grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 3;
                content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                for (int i = 0; i < view.CookButtons.Length; i++) view.CookButtons[i].transform.parent.SetParent(content, false);
                int oldCount = view.CookButtons.Length; Array.Resize(ref view.TitleTexts, 5); Array.Resize(ref view.IngredientTexts, 5); Array.Resize(ref view.ResultTexts, 5); Array.Resize(ref view.CookButtons, 5);
                for (int i = oldCount; i < 5; i++)
                {
                    var card = Factory.Panel("Recipe " + i, content, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(320, 326), Factory.Sand);
                    view.TitleTexts[i] = Factory.Label("Name", card, new Vector2(20, -18), new Vector2(280, 36), i == 3 ? "烤沙丁鱼" : "鲈鱼蔬菜汤", 25);
                    view.IngredientTexts[i] = Factory.Label("Ingredient", card, new Vector2(20, -66), new Vector2(280, 90), i == 3 ? "沙丁鱼 × 1 · 持有 0" : "鲈鱼 × 1 · 持有 0\n芜菁 × 1 · 持有 0\n番茄 × 1 · 持有 0", 18);
                    view.ResultTexts[i] = Factory.Label("Result", card, new Vector2(20, -166), new Vector2(280, 78), i == 3 ? "制作 1 份 · 恢复 35 体力" : "制作 1 份 · 恢复 50 体力", 17);
                    view.CookButtons[i] = Factory.Button("Cook", card, new Vector2(20, -266), new Vector2(280, 44), "制作一份", new Color(.70f, .81f, .62f));
                }
                scroll.viewport = viewport; scroll.content = content; view.RecipeScroll = scroll;
                var intro = panel.Find("Introduction"); if (intro != null) intro.GetComponent<TMP_Text>().text = "共 5 道食谱 · 向下滚动查看鱼类料理";
                view.FeedbackText.rectTransform.anchoredPosition = new Vector2(32, -556); view.FeedbackText.rectTransform.sizeDelta = new Vector2(992, 30);
                view.SaveButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(824, -600);
                var controls = panel.Find("Controls"); if (controls != null) controls.GetComponent<RectTransform>().anchoredPosition = new Vector2(32, -602);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
