using System;
using System.IO;
using System.Linq;
using MagicIsland.CameraSystem;
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
using Object = UnityEngine.Object;
namespace MagicIsland.EditorTools
{
    public static class OrchardSetup
    {
        public const string Folder = "Assets/Art/Prefabs/Orchard", Materials = "Assets/Art/Materials/Orchard", ConfigPath = "Assets/Config/OrchardCatalog.asset";
        public static void Ensure(AddressableAssetSettings settings)
        {
            Directory.CreateDirectory(Folder); Directory.CreateDirectory(Materials); AssetDatabase.Refresh();
            var items = AssetDatabase.LoadAssetAtPath<ItemCatalog>("Assets/Config/ItemCatalog.asset"); var crafting = AssetDatabase.LoadAssetAtPath<CraftingCatalog>("Assets/Config/CraftingCatalog.asset");
            var catalog = AssetDatabase.LoadAssetAtPath<OrchardCatalog>(ConfigPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<OrchardCatalog>();
                catalog.Types.Add(new OrchardTypeConfig { SaplingId = "apple_sapling", FruitId = "apple", DisplayName = "苹果树" });
                catalog.Types.Add(new OrchardTypeConfig { SaplingId = "orange_sapling", FruitId = "orange", DisplayName = "橙树" }); AssetDatabase.CreateAsset(catalog, ConfigPath);
            }
            for (int i = 0; i < catalog.Types.Count; i++)
            {
                var tree = catalog.Types[i]; string prefab = i == 0 ? "AppleTree" : "OrangeTree";
                Color fruit = i == 0 ? new Color(.86f, .28f, .21f) : new Color(.98f, .57f, .13f);
                AddItem(items, tree.SaplingId, tree.DisplayName + "苗", ItemType.Placeable, 150 + i * 30, 0, fruit, "从背包选择摆放到岛上空地。果树周围留一格空地，睡觉跨日成长；回收会重置成长和未采果实。");
                AddItem(items, tree.FruitId, i == 0 ? "苹果" : "橙子", ItemType.Crop, 0, 40 + i * 10, fruit, "果树成熟后定期采收的水果，可存入储物箱、卖给商店或投入出货箱。");
                if (!crafting.PurchasedPlaceables.Any(r => r.OutputItemId == tree.SaplingId))
                { crafting.PurchasedPlaceables.Add(new CraftingRecipeConfig { Id = tree.SaplingId, DisplayName = tree.DisplayName + "苗", OutputItemId = tree.SaplingId, PrefabAddress = prefab, Clearance = 1 }); EditorUtility.SetDirty(crafting); }
                BuildTree(prefab, fruit); Register(settings, Folder + "/" + prefab + ".prefab", prefab, "Buildings");
            }
            Register(settings, ConfigPath, OrchardCatalog.Address, "Configuration");
        }
        static void Register(AddressableAssetSettings settings, string path, string address, string group)
        { var guid = AssetDatabase.AssetPathToGUID(path); var entry = settings.FindAssetEntry(guid) ?? settings.CreateOrMoveEntry(guid, settings.FindGroup(group)); if (entry.address != address) entry.SetAddress(address); }
        static void AddItem(ItemCatalog catalog, string id, string name, ItemType type, int buy, int sell, Color color, string description)
        {
            if (catalog.Find(id) != null) return;
            catalog.Items.Add(new ItemConfig { Id = id, DisplayName = name, Type = type, MaxStack = 99, BuyPrice = buy, SellPrice = sell, IconColor = color, Description = description, Icon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Textures/UI/RoundedPanel.png") }); EditorUtility.SetDirty(catalog);
        }
        static Material Mat(string name, Color color)
        { string path = Materials + "/" + name + ".mat"; var mat = AssetDatabase.LoadAssetAtPath<Material>(path); if (mat != null) return mat; mat = new Material(Shader.Find("Standard")) { name = name, color = color }; mat.SetFloat("_Glossiness", .08f); AssetDatabase.CreateAsset(mat, path); return mat; }
        static Transform Group(Transform parent, string name)
        { var go = new GameObject(name); go.layer = 2; go.transform.SetParent(parent, false); return go.transform; }
        static Transform Part(Transform parent, string name, PrimitiveType type, Vector3 point, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name; go.layer = 2; go.transform.SetParent(parent, false); go.transform.localPosition = point; go.transform.localScale = scale;
            Object.DestroyImmediate(go.GetComponent<Collider>()); go.GetComponent<Renderer>().sharedMaterial = mat; return go.transform;
        }
        static void BuildTree(string name, Color color)
        {
            string path = Folder + "/" + name + ".prefab"; if (File.Exists(path)) return;
            var root = new GameObject(name, typeof(PlacedDecoration), typeof(OrchardActor), typeof(BoxCollider));
            try
            {
                var box = root.GetComponent<BoxCollider>(); box.center = new Vector3(0, .55f, 0); box.size = new Vector3(.66f, 1.1f, .66f);
                var bark = Mat("Fruit Tree Bark", new Color(.49f, .33f, .21f)); var leaves = Mat("Fruit Tree Leaves", new Color(.36f, .56f, .28f)); var tips = Mat("Fruit Tree Tips", new Color(.50f, .67f, .34f)); var fruit = Mat(name + " Fruit", color);
                var actor = root.GetComponent<OrchardActor>(); var sapling = Group(root.transform, "Sapling"); var young = Group(root.transform, "Young tree"); var mature = Group(root.transform, "Mature tree"); var fruits = Group(root.transform, "Fruit");
                actor.Sapling = sapling.gameObject; actor.Young = young.gameObject; actor.Mature = mature.gameObject; actor.Fruit = fruits.gameObject;
                for (int stage = 0; stage < 3; stage++)
                {
                    var parent = stage == 0 ? sapling : stage == 1 ? young : mature; float size = stage == 0 ? .30f : stage == 1 ? .62f : 1;
                    Part(parent, "Trunk", PrimitiveType.Cylinder, new Vector3(0, .61f * size, 0), new Vector3(.20f * size, .61f * size, .20f * size), bark);
                    var mesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Art/Meshes/Gathering/Soft Oak Canopy.asset");
                    for (int i = 0; i < 3; i++)
                    {
                        var canopy = new GameObject("Leaf crown", typeof(MeshFilter), typeof(MeshRenderer)); canopy.layer = 2; canopy.transform.SetParent(parent, false);
                        canopy.transform.localPosition = new Vector3((i - 1) * .38f * size, (1.80f + (i == 1 ? .30f : 0)) * size, 0); canopy.transform.localScale = new Vector3(1.22f, 1.25f, 1.20f) * size;
                        canopy.GetComponent<MeshFilter>().sharedMesh = mesh; canopy.GetComponent<Renderer>().sharedMaterial = i == 1 ? tips : leaves;
                    }
                }
                for (int i = 0; i < 2; i++)
                {
                    var point = new Vector3(i == 0 ? -.37f : .37f, 1.75f + i * .10f, -.59f);
                    Part(fruits, "Ripe fruit", PrimitiveType.Sphere, point, Vector3.one * .27f, fruit);
                    Part(fruits, "Fruit stem", PrimitiveType.Cylinder, point + Vector3.up * .15f, new Vector3(.018f, .025f, .018f), bark);
                }
                root.AddComponent<CameraOccluder>().Renderers = root.GetComponentsInChildren<Renderer>(true); actor.Render(0, false); PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
        }
        public static void UpgradeShop()
        {
            string path = Factory.Folder + "/ShopPanel.prefab"; var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (prefab.GetComponent<ShopView>().CropButtons.Length >= 10) return;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view = root.GetComponent<ShopView>(); int old = view.CropButtons.Length; Array.Resize(ref view.CropButtons, 10); var parent = view.BalanceText.transform.parent;
                for (int i = old; i < 10; i++) view.CropButtons[i] = Factory.Button("Fruit Tree Offer " + i, parent, Vector2.zero, new Vector2(122, 40), i == 8 ? "苹果" : "橙子", Factory.Sand);
                for (int i = 0; i < 10; i++) { var rect = view.CropButtons[i].GetComponent<RectTransform>(); rect.anchoredPosition = new Vector2(32 + i % 5 * 130, -130 - i / 5 * 50); rect.sizeDelta = new Vector2(122, 40); var label = view.CropButtons[i].GetComponentInChildren<TMP_Text>(); label.rectTransform.sizeDelta = rect.sizeDelta; label.fontSize = 17; }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
