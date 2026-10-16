using System.IO;
using MagicIsland.CalendarSystem;
using MagicIsland.Configuration;
using MagicIsland.Interaction;
using MagicIsland.UI.Views;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace MagicIsland.EditorTools
{
    public static class SeasonConfigSetup
    {
        public static void Ensure(AddressableAssetSettings settings, AddressableAssetGroup group)
        {
            const string path = "Assets/Config/SeasonConfig.asset";
            SeasonConfig config = AssetDatabase.LoadAssetAtPath<SeasonConfig>(path);
            if (config == null) AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<SeasonConfig>(), path);
            else if(!File.ReadAllText(path).Contains("Spring:"))EditorUtility.SetDirty(config);
            string guid = AssetDatabase.AssetPathToGUID(path);
            var entry = settings.FindAssetEntry(guid) ?? settings.CreateOrMoveEntry(guid, group);
            if (entry.address != SeasonConfig.Address) entry.SetAddress(SeasonConfig.Address);

            // Migrate only a catalog that predates the serialized season field.
            const string cropPath = "Assets/Config/CropCatalog.asset";
            if (!File.ReadAllText(cropPath).Contains("PlantingSeasons:"))
            {
                CropCatalog crops = AssetDatabase.LoadAssetAtPath<CropCatalog>(cropPath);
                foreach (CropConfig crop in crops.Crops) crop.PlantingSeasons = Defaults(crop.Id);
                EditorUtility.SetDirty(crops); AssetDatabase.SaveAssets();
            }
        }
        public static SeasonMask Defaults(string id) => id == "turnip" ? SeasonMask.Spring | SeasonMask.Autumn
            : id == "tomato" ? SeasonMask.Summer : id == "corn" ? SeasonMask.Summer | SeasonMask.Autumn : SeasonMask.All;

        public static void UpgradeUi()
        {
            Upgrade("FarmHudPanel", root =>
            {
                PrototypeHud view = root.GetComponent<PrototypeHud>();
                if (view.DayText.rectTransform.sizeDelta.x > 150) return false;
                view.DayText.rectTransform.sizeDelta = new Vector2(438, 30);
                view.DayText.enableAutoSizing = true; view.DayText.fontSizeMin = 15; view.DayText.fontSizeMax = 20;
                view.DayText.text = "第 1 年 春季 1 日";
                Transform top = view.DayText.transform.parent;
                foreach (RectTransform child in top)
                {
                    if (child == view.DayText.rectTransform) continue;
                    if (child == view.InventoryText.rectTransform)
                    { child.anchoredPosition = new Vector2(16, -82); child.sizeDelta = new Vector2(438, 30); }
                    else if (child.anchoredPosition.y <= -95) child.anchoredPosition += Vector2.down * 36;
                }
                top.GetComponent<RectTransform>().sizeDelta += Vector2.up * 36;
                return true;
            });
            Upgrade("InventoryPanel", root =>
            {
                InventoryView view = root.GetComponent<InventoryView>();
                if (view.DetailText.rectTransform.sizeDelta.y > 66) return false;
                view.DetailText.rectTransform.sizeDelta = new Vector2(view.DetailText.rectTransform.sizeDelta.x, 108);
                view.DetailText.transform.parent.GetComponent<RectTransform>().sizeDelta += Vector2.up * 42;
                view.CapacityText.transform.parent.GetComponent<RectTransform>().sizeDelta += Vector2.up * 42;
                foreach (TMPro.TMP_Text label in root.GetComponentsInChildren<TMPro.TMP_Text>())
                    if (label.name == "Controls") label.rectTransform.anchoredPosition += Vector2.down * 42;
                return true;
            });
            Upgrade("ShopPanel", root =>
            {
                ShopView view = root.GetComponent<ShopView>();
                if (view.StockText.rectTransform.sizeDelta.y > 32) return false;
                view.StockText.rectTransform.sizeDelta = new Vector2(view.StockText.rectTransform.sizeDelta.x, 60);
                view.FeedbackText.rectTransform.anchoredPosition += Vector2.down * 28;
                view.StockText.transform.parent.GetComponent<RectTransform>().sizeDelta += Vector2.up * 28;
                return true;
            });
        }
        private static void Upgrade(string name, System.Func<GameObject, bool> change)
        {
            string path = PrototypeUiPrefabBuilder.Folder + "/" + name + ".prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try { if (change(root)) PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
