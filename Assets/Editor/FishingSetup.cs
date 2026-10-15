using System;
using System.IO;
using MagicIsland.Configuration;
using MagicIsland.Interaction;
using MagicIsland.Inventory;
using MagicIsland.Tools;
using MagicIsland.UI.Views;
using TMPro;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using UnityEngine.UI;
using Factory = MagicIsland.EditorTools.PrototypeUiPrefabBuilder;
namespace MagicIsland.EditorTools
{
    public static class FishingSetup
    {
        public static void Ensure(AddressableAssetSettings settings, AddressableAssetGroup configuration, ItemCatalog items)
        {
            AddFish(items, "sardine", "沙丁鱼", 35, new Color(.55f, .75f, .78f)); AddFish(items, "sea_bass", "鲈鱼", 65, new Color(.49f, .66f, .59f));
            const string path = "Assets/Config/FishingCatalog.asset";
            if (AssetDatabase.LoadAssetAtPath<FishingCatalog>(path) == null)
            {
                var catalog = ScriptableObject.CreateInstance<FishingCatalog>(); catalog.Fish.Add(new FishConfig { ItemId = "sardine", Weight = 70 }); catalog.Fish.Add(new FishConfig { ItemId = "sea_bass", Weight = 30 }); AssetDatabase.CreateAsset(catalog, path);
            }
            Register(settings, configuration, path, FishingCatalog.Address);
            var group = settings.FindGroup("Fishing") ?? settings.CreateGroup("Fishing", false, false, true, null, typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            Directory.CreateDirectory("Assets/Art/Prefabs/Fishing"); AssetDatabase.Refresh(); CreateRod(); CreateSpot();
            Register(settings, group, "Assets/Art/Prefabs/Fishing/FishingRod.prefab", "FishingRod"); Register(settings, group, "Assets/Art/Prefabs/Fishing/CoastalFishingSpot.prefab", "CoastalFishingSpot"); UpgradeHud();
        }
        private static void Register(AddressableAssetSettings settings, AddressableAssetGroup group, string path, string address)
        { var entry = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(path)) ?? settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), group); if (entry.address != address) entry.SetAddress(address); }
        private static void AddFish(ItemCatalog items, string id, string name, int price, Color color)
        {
            if (items.Find(id) != null) return;
            items.Items.Add(new ItemConfig { Id = id, DisplayName = name, Type = ItemType.Fish, MaxStack = 99, SellPrice = price, IconColor = color,
                Icon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Textures/UI/RoundedPanel.png"), Description = "海岸钓到的鱼，可以存入储物箱或在商店出售。" }); EditorUtility.SetDirty(items);
        }
        public static void CreatePanel()
        {
            string path = Factory.Folder + "/FishingPanel.prefab"; if (File.Exists(path)) return; var root = Factory.CanvasRoot("FishingPanel");
            try
            {
                var view = root.AddComponent<FishingView>(); view.Layer = UILayer.PopUp; view.CoastLayout = true;
                var shade = Factory.Panel("Backdrop", root.transform, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, new Color(.12f, .2f, .15f, .22f)); shade.anchorMax = Vector2.one; shade.GetComponent<Image>().sprite = null;
                var card = Factory.Panel("Fishing Card", root.transform, new Vector2(0,1), new Vector2(0,1), new Vector2(20, -350), new Vector2(540, 364), Factory.Cream); Factory.SoftShadow(card);
                Factory.Label("Title", card, new Vector2(24, -18), new Vector2(420, 36), "海岸垂钓", 28);
                view.CloseButton = Factory.Button("Close", card, new Vector2(470, -18), new Vector2(46, 36), "×", Factory.Sand);
                view.StatusText = Factory.Label("Status", card, new Vector2(24, -68), new Vector2(492, 36), "等待鱼儿靠近", 25);
                view.TimerText = Factory.Label("Timer", card, new Vector2(24, -110), new Vector2(492, 28), "观察浮漂，等咬钩提示", 19);
                var bar = Factory.Panel("Progress", card, new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -148), new Vector2(492, 14), Factory.Sand);
                var fill = Factory.Panel("Fill", bar, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, new Color(.43f, .65f, .55f)); fill.anchorMax = Vector2.one;
                view.ProgressFill = fill.GetComponent<Image>(); view.ProgressFill.type = Image.Type.Filled; view.ProgressFill.fillMethod = Image.FillMethod.Horizontal;
                view.HintText = Factory.Label("Hint", card, new Vector2(24, -176), new Vector2(492, 48), "成功捕获消耗 5 体力 · 失败与取消不消耗\n咬钩后 2 秒内收竿", 17);
                view.CastButton = Factory.Button("Cast", card, new Vector2(24, -240), new Vector2(148, 44), "重新抛竿", Factory.Sand);
                view.ReelButton = Factory.Button("Reel", card, new Vector2(188, -240), new Vector2(328, 44), "立即收竿", new Color(.70f, .81f, .62f)); view.ReelButton.GetComponentInChildren<TMP_Text>().fontSize = 20;
                view.FeedbackText = Factory.Label("Feedback", card, new Vector2(24, -296), new Vector2(492, 26), "浮漂已入水，等它下沉再收竿", 17); view.FeedbackText.enableAutoSizing = true; view.FeedbackText.fontSizeMin = 13; view.FeedbackText.fontSizeMax = 17;
                view.SaveButton = Factory.Button("Save", card, new Vector2(366, -330), new Vector2(150, 26), "保存进度", Factory.Sand); view.SaveButton.GetComponentInChildren<TMP_Text>().fontSize = 17;
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        public static void UpgradeShop()
        {
            string path = Factory.Folder + "/ShopPanel.prefab"; var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view = root.GetComponent<ShopView>();
                if (view.SaveButton != null && view.CropButtons.Length >= 5)
                {
                    // Repair the untouched legacy 204px labels on the new 122px crop tabs once.
                    bool changed = false;
                    for (int i = 0; i < 3; i++)
                    {
                        var rect = view.CropButtons[i].GetComponent<RectTransform>(); var label = view.CropButtons[i].GetComponentInChildren<TMP_Text>().rectTransform;
                        if (rect.sizeDelta == new Vector2(122, 46) && label.sizeDelta == new Vector2(204, 46)) { label.sizeDelta = rect.sizeDelta; changed = true; }
                    }
                    if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
                    return;
                }
                var panel = view.BalanceText.transform.parent; panel.GetComponent<RectTransform>().sizeDelta = new Vector2(700, 578);
                int old = view.CropButtons.Length; Array.Resize(ref view.CropButtons, 5);
                for (int i = old; i < 5; i++) view.CropButtons[i] = Factory.Button("Fish " + i, panel, new Vector2(32 + i * 130, -130), new Vector2(122, 46), i == 3 ? "沙丁鱼" : "鲈鱼", Factory.Sand);
                for (int i = 0; i < 5; i++) { var rect = view.CropButtons[i].GetComponent<RectTransform>(); rect.anchoredPosition = new Vector2(32 + i * 130, -130); rect.sizeDelta = new Vector2(122, 46); var label = view.CropButtons[i].GetComponentInChildren<TMP_Text>(); label.fontSize = 20; label.rectTransform.sizeDelta = rect.sizeDelta; }
                if (view.SaveButton == null) view.SaveButton = Factory.Button("Save Progress", panel, new Vector2(488, -526), new Vector2(180, 32), "保存进度", Factory.Sand);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        private static void UpgradeHud()
        {
            string path = Factory.Folder + "/FarmHudPanel.prefab"; var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var hud = root.GetComponent<PrototypeHud>(); if (hud.ToolButtons.Length >= 6) return;
                var parent = hud.ToolButtons[0].transform.parent; Array.Resize(ref hud.ToolButtons, 6);
                hud.ToolButtons[5] = Factory.Button("Fishing Rod", parent, new Vector2(576, -14), new Vector2(104, 44), "6 鱼竿", Factory.Sand); hud.ToolButtons[5].GetComponentInChildren<TMP_Text>().fontSize = 20;
                hud.HintText.rectTransform.anchoredPosition = new Vector2(696, -14); hud.HintText.rectTransform.sizeDelta = new Vector2(228, 44); PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        private static GameObject Part(string name, Transform parent, PrimitiveType shape, Vector3 position, Vector3 scale, Material material, bool collider = false)
        {
            var go = GameObject.CreatePrimitive(shape); go.name = name; go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = scale; go.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>()); return go;
        }
        private static Material Material(string name, Color color)
        {
            const string folder = "Assets/Art/Materials/Fishing"; Directory.CreateDirectory(folder); AssetDatabase.Refresh(); string path = folder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path); if (material != null) return material;
            material = new Material(Shader.Find("Standard")) { color = color }; material.SetFloat("_Glossiness", .2f); AssetDatabase.CreateAsset(material, path); return material;
        }
        private static void CreateRod()
        {
            const string path = "Assets/Art/Prefabs/Fishing/FishingRod.prefab"; if (File.Exists(path)) return; var root = new GameObject("FishingRod", typeof(FishingRodVisual));
            try
            {
                var wood = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Buildings/Hall Wood.mat"); var metal = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Buildings/Hall Stone.mat");
                Part("Handle", root.transform, PrimitiveType.Cylinder, new Vector3(0, .08f, 0), new Vector3(.08f, .22f, .08f), wood);
                var pole = Part("Pole", root.transform, PrimitiveType.Cylinder, new Vector3(0, .94f, .29f), new Vector3(.035f, .71f, .035f), wood); pole.transform.localRotation = Quaternion.Euler(23, 0, 0);
                Part("Reel", root.transform, PrimitiveType.Cylinder, new Vector3(.085f, .20f, 0), new Vector3(.18f, .045f, .18f), metal).transform.localRotation = Quaternion.Euler(0, 0, 90);
                var tip = new GameObject("Tip").transform; tip.SetParent(root.transform, false); tip.localPosition = new Vector3(0, 1.59f, .57f); root.GetComponent<FishingRodVisual>().Tip = tip; PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        private static void CreateSpot()
        {
            const string path = "Assets/Art/Prefabs/Fishing/CoastalFishingSpot.prefab"; if (File.Exists(path)) return; var root = new GameObject("CoastalFishingSpot", typeof(FishingSpot));
            try
            {
                var wood = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Buildings/Hall Wood.mat"); var sand = Material("Sand", new Color(.84f, .77f, .56f)); var water = Material("Coastal Water", new Color(.28f, .62f, .73f)); var red = Material("Bobber Coral", new Color(.88f, .44f, .33f));
                Part("Sand", root.transform, PrimitiveType.Cube, new Vector3(-.85f, .035f, 0), new Vector3(1.7f, .05f, 4.3f), sand);
                Part("Water", root.transform, PrimitiveType.Cube, new Vector3(1.7f, .035f, 0), new Vector3(3, .06f, 4.3f), water, true).GetComponent<Collider>().isTrigger = true;
                Part("Deck", root.transform, PrimitiveType.Cube, new Vector3(.15f, .12f, 0), new Vector3(1.5f, .14f, 1.4f), wood, true);
                for (int i = 0; i < 4; i++) Part("Deck Seam " + i, root.transform, PrimitiveType.Cube, new Vector3(-.4f + .36f * i, .197f, 0), new Vector3(.02f, .014f, 1.35f), sand);
                var marker = root.GetComponent<FishingSpot>(); marker.Bobber = Part("Bobber", root.transform, PrimitiveType.Sphere, new Vector3(.95f, .16f, 0), Vector3.one * .17f, red).transform;
                var label = new GameObject("Spot Label", typeof(TextMeshPro)); label.transform.SetParent(root.transform, false); label.transform.localPosition = new Vector3(-.35f, .72f, 0); marker.Label = label.transform;
                var text = label.GetComponent<TextMeshPro>(); text.font = UiPrefabMigration.FontAsset; text.text = "海岸钓点"; text.fontSize = 1.5f; text.rectTransform.sizeDelta = new Vector2(2, .3f); text.alignment = TextAlignmentOptions.Center; text.color = new Color(1, .97f, .84f); PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
