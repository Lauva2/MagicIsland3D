using MagicIsland.Configuration;
using MagicIsland.UI.Views;
using TMPro;
using UnityEditor;
using UnityEngine;
using Factory = MagicIsland.EditorTools.PrototypeUiPrefabBuilder;
namespace MagicIsland.EditorTools
{
    public static class FeedCraftingSetup
    {
        public static void Ensure()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CraftingCatalog>("Assets/Config/CraftingCatalog.asset");
            if (catalog.Recipes.Exists(r => r.Id == "animal_feed")) return;
            var recipe = new CraftingRecipeConfig { Id = "animal_feed", DisplayName = "玉米饲料", OutputItemId = "animal_feed", IsSupply = true, Yield = 10 };
            recipe.Ingredients.Add(new CraftingIngredientConfig { ItemId = "corn" }); catalog.Recipes.Add(recipe); EditorUtility.SetDirty(catalog);
        }
        public static void UpgradePanel()
        {
            string path = Factory.Folder + "/CraftingPanel.prefab"; var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view = root.GetComponent<CraftingView>(); var parent = view.SaveButton.transform.parent; bool changed = false;
                var title = parent.Find("Title")?.GetComponent<TMP_Text>();
                if (title != null && title.text == "岛屿工作台 · 装饰与家具") { title.text = "岛屿工作台 · 制作"; changed = true; }
                var intro = parent.Find("Introduction")?.GetComponent<TMP_Text>();
                if (intro != null && intro.text == "用采集到的木材和石料，布置农场和小屋")
                { intro.text = "布置农场和小屋，也能把玉米加工成饲料"; changed = true; }
                if (view.PageText.text == "第 1 / 2 页") { view.PageText.text = "农场装饰 1 / 3"; changed = true; }
                if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            path = Factory.Folder + "/ToolUpgradePanel.prefab"; root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var label = root.GetComponent<ToolUpgradeView>().CraftingButton.GetComponentInChildren<TMP_Text>();
                if (label.text == "制作装饰") { label.text = "工作台制作"; PrefabUtility.SaveAsPrefabAsset(root, path); }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
