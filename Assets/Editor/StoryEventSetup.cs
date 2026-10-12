using System;
using System.Collections.Generic;
using MagicIsland.Configuration;
using MagicIsland.Inventory;
using MagicIsland.UI.Views;
using TMPro;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using Factory = MagicIsland.EditorTools.PrototypeUiPrefabBuilder;
namespace MagicIsland.EditorTools
{
    public static class StoryEventSetup
    {
        public static void Ensure(AddressableAssetSettings settings, AddressableAssetGroup group, ItemCatalog items)
        {
            if (items.Find("vegetable_soup") == null)
            {
                items.Items.Add(new ItemConfig { Id = "vegetable_soup", DisplayName = "蔬菜浓汤", Type = ItemType.Food, MaxStack = 99,
                    Description = "林舟分享的家常食谱，三种收成熬成的浓汤可恢复 50 体力。", IconColor = new Color(.79f, .71f, .39f),
                    Icon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Textures/UI/RoundedPanel.png") }); EditorUtility.SetDirty(items);
            }
            var cooking = AssetDatabase.LoadAssetAtPath<CookingCatalog>("Assets/Config/CookingCatalog.asset");
            if (!cooking.Recipes.Exists(r => r.Id == "vegetable_soup"))
            {
                var recipe = new CookingRecipeConfig { Id = "vegetable_soup", DisplayName = "蔬菜浓汤", IngredientId = "turnip", FoodId = "vegetable_soup", StaminaRecovery = 50, RequiredEventId = "steward_recipe" };
                recipe.AdditionalIngredients.Add(new CookingIngredientConfig { ItemId = "tomato" }); recipe.AdditionalIngredients.Add(new CookingIngredientConfig { ItemId = "corn" });
                cooking.Recipes.Add(recipe); EditorUtility.SetDirty(cooking);
            }
            const string path = "Assets/Config/StoryEventCatalog.asset";
            if (AssetDatabase.LoadAssetAtPath<StoryEventCatalog>(path) == null)
            {
                var catalog = ScriptableObject.CreateInstance<StoryEventCatalog>();
                catalog.Events.Add(new StoryEventConfig { Id = "steward_history", NpcId = "island_steward", DisplayName = "小岛往事", RequiredPoints = 30,
                    Lines = new List<string> {
                        "以前这片农田还只是长满杂草的空地。大家带着木板和种子，一点点把小岛建成了现在的样子。",
                        "办事处门前的公告栏也是那时立起来的。谁有多余的收成，谁需要帮忙，就在上面留一张纸条。",
                        "现在看到你照顾农田，我又想起了那段日子。谢谢你，让小岛继续生长。" } });
                catalog.Events.Add(new StoryEventConfig { Id = "steward_recipe", NpcId = "island_steward", DisplayName = "朋友的食谱", RequiredPoints = 70, PrerequisiteId = "steward_history", RecipeId = "vegetable_soup",
                    Lines = new List<string> {
                        "最近收到你送来的料理，我也想分享一点自己的手艺。今天把我常做的蔬菜浓汤教给你吧。",
                        "一颗芜菁、一个番茄，再加一根玉米，慢慢熬成一碗浓汤。忙完农活喝上一碗，会舒服很多。",
                        "食谱就交给你了！去料理台试试吧。等下次坐下来聊天，我们一起说说今天的收成。" } });
                AssetDatabase.CreateAsset(catalog, path);
            }
            var entry = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(path)) ?? settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), group);
            if (entry.address != StoryEventCatalog.Address) entry.SetAddress(StoryEventCatalog.Address);
        }
        public static void UpgradeDialogue()
        {
            string path = Factory.Folder + "/DialoguePanel.prefab"; var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view = root.GetComponent<DialogueView>(); if (view.StoryButton != null) return;
                view.StoryButton = Factory.Button("Story", view.SpeakerText.transform.parent, new Vector2(294, -232), new Vector2(196, 48), "小岛往事", Factory.Sand);
                view.StoryButton.GetComponentInChildren<TMP_Text>().fontSize = 18;
                view.ProgressText.rectTransform.sizeDelta = new Vector2(246, 30); PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        public static void UpgradeCooking()
        {
            string path = Factory.Folder + "/CookingPanel.prefab"; var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view = root.GetComponent<CookingView>(); if (view.CookButtons.Length >= 3) return;
                var panel = view.FeedbackText.transform.parent; panel.GetComponent<RectTransform>().sizeDelta = new Vector2(1056, 548);
                Array.Resize(ref view.TitleTexts, 3); Array.Resize(ref view.IngredientTexts, 3); Array.Resize(ref view.ResultTexts, 3); Array.Resize(ref view.CookButtons, 3);
                var newCard = Factory.Panel("Recipe 2", panel, new Vector2(0, 1), new Vector2(0, 1), new Vector2(704, -126), new Vector2(320, 326), Factory.Sand);
                view.TitleTexts[2] = Factory.Label("Name", newCard, new Vector2(20, -18), new Vector2(280, 36), "蔬菜浓汤", 25);
                view.IngredientTexts[2] = Factory.Label("Ingredient", newCard, new Vector2(20, -66), new Vector2(280, 90), "芜菁 × 1\n番茄 × 1\n玉米 × 1", 18);
                view.ResultTexts[2] = Factory.Label("Result", newCard, new Vector2(20, -166), new Vector2(280, 78), "恢复 50 体力\n林舟好感度 70 · 完成朋友的食谱", 17);
                view.CookButtons[2] = Factory.Button("Cook", newCard, new Vector2(20, -266), new Vector2(280, 44), "尚未解锁", new Color(.70f, .81f, .62f));
                for (int i = 0; i < 3; i++)
                {
                    var card = view.CookButtons[i].transform.parent.GetComponent<RectTransform>(); card.anchoredPosition = new Vector2(32 + 336 * i, -126); card.sizeDelta = new Vector2(320, 326);
                    view.IngredientTexts[i].rectTransform.anchoredPosition = new Vector2(20, -66); view.IngredientTexts[i].rectTransform.sizeDelta = new Vector2(280, 90);
                    view.ResultTexts[i].rectTransform.anchoredPosition = new Vector2(20, -166); view.ResultTexts[i].rectTransform.sizeDelta = new Vector2(280, 78); view.ResultTexts[i].fontSize = 17;
                    view.CookButtons[i].GetComponent<RectTransform>().anchoredPosition = new Vector2(20, -266);
                }
                view.CloseButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(970, -24);
                view.FeedbackText.rectTransform.anchoredPosition = new Vector2(32, -466); view.FeedbackText.rectTransform.sizeDelta = new Vector2(992, 30);
                view.SaveButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(824, -508);
                var controls = panel.Find("Controls"); if (controls != null) controls.GetComponent<RectTransform>().anchoredPosition = new Vector2(32, -510);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
