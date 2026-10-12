using System.IO;
using System.Linq;
using MagicIsland.Configuration;
using MagicIsland.Interaction;
using MagicIsland.Inventory;
using MagicIsland.UI.Views;
using TMPro;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using Factory = MagicIsland.EditorTools.PrototypeUiPrefabBuilder;
namespace MagicIsland.EditorTools
{
    public static class FurnitureSetup
    {
        static readonly string[] Ids = { "wood_table", "wood_chair", "small_lamp" };
        static readonly string[] Names = { "木桌", "木椅", "小台灯" };
        static readonly string[] Addresses = { "WoodTable", "WoodChair", "SmallLamp" };
        public static void Ensure(AddressableAssetSettings settings, ItemCatalog items)
        {
            var crafting = AssetDatabase.LoadAssetAtPath<CraftingCatalog>("Assets/Config/CraftingCatalog.asset");
            Directory.CreateDirectory("Assets/Art/Prefabs/Furniture"); AssetDatabase.Refresh();
            for (int i = 0; i < 3; i++)
            {
                if (items.Find(Ids[i]) == null)
                {
                    items.Items.Add(new ItemConfig { Id = Ids[i], DisplayName = Names[i], Type = ItemType.Furniture, MaxStack = 99,
                        Description = "在小屋内从背包选择摆放，R 旋转，走近右键回收。", Icon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Textures/UI/RoundedPanel.png"),
                        IconColor = i == 2 ? new Color(.94f, .78f, .43f) : new Color(.66f, .48f, .33f) });
                    EditorUtility.SetDirty(items);
                }
                if (crafting.FindItem(Ids[i]) == null)
                {
                    var recipe = new CraftingRecipeConfig { Id = Ids[i], DisplayName = Names[i], OutputItemId = Ids[i], PrefabAddress = Addresses[i], IsFurniture = true };
                    recipe.Ingredients.Add(new CraftingIngredientConfig { ItemId = "wood", Quantity = i == 0 ? 4 : i == 1 ? 3 : 2 });
                    if (i == 2) recipe.Ingredients.Add(new CraftingIngredientConfig { ItemId = "stone", Quantity = 1 });
                    crafting.Recipes.Add(recipe); EditorUtility.SetDirty(crafting);
                }
                string path = "Assets/Art/Prefabs/Furniture/" + Addresses[i] + ".prefab"; CreateModel(path, i);
                var guid = AssetDatabase.AssetPathToGUID(path); var entry = settings.FindAssetEntry(guid) ?? settings.CreateOrMoveEntry(guid, settings.FindGroup("Buildings"));
                if (entry.address != Addresses[i]) entry.SetAddress(Addresses[i]);
            }
        }
        static Material Mat(string name) => AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Buildings/" + name + ".mat");
        static void Part(Transform root, string name, Vector3 position, Vector3 size, Material material)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube); part.name = name; part.transform.SetParent(root, false);
            part.transform.localPosition = position; part.transform.localScale = size; part.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(part.GetComponent<Collider>());
        }
        static void CreateModel(string path, int kind)
        {
            if (File.Exists(path)) return;
            var root = new GameObject(Addresses[kind], typeof(PlacedDecoration), typeof(BoxCollider));
            try
            {
                var wood = Mat("Hall Wood"); var sage = Mat("Hall Sage"); var paper = Mat("Board Paper");
                var c = root.GetComponent<BoxCollider>(); c.center = new Vector3(0, kind == 0 ? .44f : .6f, 0); c.size = new Vector3(kind == 0 ? .9f : .62f, kind == 0 ? .88f : 1.2f, kind == 0 ? .8f : .62f);
                if (kind < 2)
                {
                    float height = kind == 0 ? .81f : .46f;
                    Part(root.transform, "Top", new Vector3(0, height, 0), new Vector3(kind == 0 ? .9f : .6f, .14f, kind == 0 ? .8f : .6f), kind == 0 ? wood : sage);
                    for (int x = -1; x <= 1; x += 2) for (int z = -1; z <= 1; z += 2)
                        Part(root.transform, "Leg", new Vector3(x * (kind == 0 ? .33f : .21f), height * .5f, z * (kind == 0 ? .28f : .21f)), new Vector3(.1f, height, .1f), wood);
                    if (kind == 1)
                    {
                        Part(root.transform, "Chair Back", new Vector3(0, .97f, .25f), new Vector3(.6f, .46f, .1f), sage);
                        for (int side = -1; side <= 1; side += 2) Part(root.transform, "Back Post", new Vector3(side * .25f, .75f, .25f), new Vector3(.1f, .9f, .1f), wood);
                    }
                }
                else
                {
                    Part(root.transform, "Low Stand", new Vector3(0, .23f, 0), new Vector3(.58f, .46f, .58f), wood);
                    Part(root.transform, "Stand Top", new Vector3(0, .49f, 0), new Vector3(.62f, .06f, .62f), sage);
                    Part(root.transform, "Lamp Base", new Vector3(0, .55f, 0), new Vector3(.32f, .06f, .32f), paper);
                    Part(root.transform, "Stem", new Vector3(0, .77f, 0), new Vector3(.06f, .4f, .06f), wood);
                    Part(root.transform, "Shade", new Vector3(0, 1.03f, 0), new Vector3(.48f, .3f, .48f), paper);
                    var lamp = new GameObject("Warm Light", typeof(Light)); lamp.transform.SetParent(root.transform, false); lamp.transform.localPosition = new Vector3(0, .98f, 0);
                    var light = lamp.GetComponent<Light>(); light.type = LightType.Point; light.range = 3; light.intensity = .75f; light.color = new Color(1, .78f, .43f);
                }
                root.SetActive(false); PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
        }
        public static void UpgradePanel()
        {
            const string path = "Assets/Art/Prefabs/UI/CraftingPanel.prefab";
            if (!File.Exists(path)) return; var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view = root.GetComponent<CraftingView>();
                if (view.PreviousButton != null && view.NextButton != null && view.PageText != null) return;
                var parent = view.SaveButton.transform.parent;
                if (view.PreviousButton == null) view.PreviousButton = Factory.Button("Previous Page", parent, new Vector2(282, -414), new Vector2(100, 34), "上一页", Factory.Sand);
                if (view.NextButton == null) view.NextButton = Factory.Button("Next Page", parent, new Vector2(558, -414), new Vector2(100, 34), "下一页", Factory.Sand);
                if (view.PageText == null) view.PageText = Factory.Label("Page", parent, new Vector2(386, -414), new Vector2(168, 34), "第 1 / 2 页", 18, TextAnchor.MiddleCenter);
                var title = parent.Find("Title")?.GetComponent<TMP_Text>(); if (title != null && title.text == "岛屿工作台 · 制作装饰") title.text = "岛屿工作台 · 装饰与家具";
                var intro = parent.Find("Introduction")?.GetComponent<TMP_Text>(); if (intro != null && intro.text == "用采集到的木材和石料，布置自己的农场") intro.text = "用采集到的木材和石料，布置农场和小屋";
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
