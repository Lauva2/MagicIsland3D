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
    public static class EggCookingSetup
    {
        public static void Ensure(ItemCatalog items)
        {
            AddFood(items, "fried_egg", "煎蛋", "一份金黄的煎蛋，可恢复 25 体力，也可作为料理送礼。", new Color(.95f, .75f, .36f));
            AddFood(items, "egg_vegetable_soup", "蔬菜蛋汤", "鸡蛋与芜菁煮成的暖汤，可恢复 45 体力，也可作为料理送礼。", new Color(.77f, .82f, .55f));
            var cooking = AssetDatabase.LoadAssetAtPath<CookingCatalog>("Assets/Config/CookingCatalog.asset");
            if (!cooking.Recipes.Exists(r => r.Id == "fried_egg"))
            { cooking.Recipes.Add(new CookingRecipeConfig { Id = "fried_egg", DisplayName = "煎蛋", IngredientId = "egg", FoodId = "fried_egg", StaminaRecovery = 25 }); EditorUtility.SetDirty(cooking); }
            if (!cooking.Recipes.Exists(r => r.Id == "egg_vegetable_soup"))
            {
                var recipe = new CookingRecipeConfig { Id = "egg_vegetable_soup", DisplayName = "蔬菜蛋汤", IngredientId = "egg", FoodId = "egg_vegetable_soup", StaminaRecovery = 45 };
                recipe.AdditionalIngredients.Add(new CookingIngredientConfig { ItemId = "turnip" }); cooking.Recipes.Add(recipe); EditorUtility.SetDirty(cooking);
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
                var view = root.GetComponent<CookingView>();
                var catalog = AssetDatabase.LoadAssetAtPath<CookingCatalog>("Assets/Config/CookingCatalog.asset");
                int count = Math.Max(7, catalog?.Recipes.Count ?? 0), oldCount = view.CookButtons.Length;
                if (oldCount >= count) return;
                var content = view.RecipeScroll.content;
                Array.Resize(ref view.TitleTexts, count); Array.Resize(ref view.IngredientTexts, count); Array.Resize(ref view.ResultTexts, count); Array.Resize(ref view.CookButtons, count);
                for (int i = oldCount; i < count; i++)
                {
                    // Copy the existing card so its art and layout remain consistent with the panel.
                    var card = UnityEngine.Object.Instantiate(view.CookButtons[oldCount - 1].transform.parent.gameObject, content).transform;
                    card.name = "Recipe " + i;
                    view.TitleTexts[i] = card.Find("Name").GetComponent<TMP_Text>();
                    view.IngredientTexts[i] = card.Find("Ingredient").GetComponent<TMP_Text>();
                    view.ResultTexts[i] = card.Find("Result").GetComponent<TMP_Text>();
                    view.CookButtons[i] = card.Find("Cook").GetComponent<Button>();
                    var recipe = catalog != null && i < catalog.Recipes.Count ? catalog.Recipes[i] : new CookingRecipeConfig
                    { Id = i == 5 ? "fried_egg" : "egg_vegetable_soup", DisplayName = i == 5 ? "煎蛋" : "蔬菜蛋汤", StaminaRecovery = i == 5 ? 25 : 45 };
                    view.TitleTexts[i].text = recipe.DisplayName;
                    view.IngredientTexts[i].text = "鸡蛋 × 1 · 持有 0" + (recipe.Id == "egg_vegetable_soup" ? "\n芜菁 × 1 · 持有 0" : "");
                    view.ResultTexts[i].text = "制作 " + recipe.OutputQuantity + " 份 · 恢复 " + recipe.StaminaRecovery + " 体力";
                }
                var intro = view.FeedbackText.transform.parent.Find("Introduction");
                if (intro != null) intro.GetComponent<TMP_Text>().text = "共 " + count + " 道食谱 · 向下滚动查看鱼类和鸡蛋料理";
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
