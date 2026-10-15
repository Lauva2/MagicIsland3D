using System.IO;
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
    public static class AnimalSetup
    {
        public static void Ensure(AddressableAssetSettings settings, AddressableAssetGroup group, ItemCatalog items)
        {
            if (items.Find("animal_feed") == null) { items.Items.Add(new ItemConfig { Id = "animal_feed", DisplayName = "饲料", Type = ItemType.Material, MaxStack = 99, Description = "在鸡舍喂食，每只鸡每天消耗一份饲料。", Icon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Textures/UI/RoundedPanel.png"), IconColor = new Color(.83f, .68f, .38f) }); EditorUtility.SetDirty(items); }
            if (items.Find("egg") == null) { items.Items.Add(new ItemConfig { Id = "egg", DisplayName = "鸡蛋", Type = ItemType.AnimalProduct, MaxStack = 99, Description = "喂食后次日产出，可在商店的养殖用品面板出售。", Icon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Textures/UI/RoundedPanel.png"), IconColor = new Color(.98f, .92f, .76f) }); EditorUtility.SetDirty(items); }
            const string path = "Assets/Config/AnimalConfig.asset"; var config = AssetDatabase.LoadAssetAtPath<AnimalConfig>(path);
            if (config == null) { config = ScriptableObject.CreateInstance<AnimalConfig>(); AssetDatabase.CreateAsset(config, path); }
            Register(settings, group, path, AnimalConfig.Address);
            Directory.CreateDirectory("Assets/Art/Prefabs/Animals"); Directory.CreateDirectory("Assets/Art/Materials/Animals"); AssetDatabase.Refresh();
            CreateCoop(); CreateChicken(); Register(settings, settings.FindGroup("Buildings"), "Assets/Art/Prefabs/Buildings/ChickenCoop.prefab", config.CoopAddress); Register(settings, settings.FindGroup("Buildings"), "Assets/Art/Prefabs/Animals/Chicken.prefab", config.ChickenAddress);
        }
        static void Register(AddressableAssetSettings settings, AddressableAssetGroup group, string path, string address)
        { var guid = AssetDatabase.AssetPathToGUID(path); var entry = settings.FindAssetEntry(guid) ?? settings.CreateOrMoveEntry(guid, group); if (entry.address != address) entry.SetAddress(address); }
        static Material Mat(string name) => AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Buildings/" + name + ".mat");
        static Material AnimalMat(string name, Color color)
        { string path = "Assets/Art/Materials/Animals/" + name + ".mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(path); if (m != null) return m; m = new Material(Shader.Find("Standard")) { color = color }; m.SetFloat("_Glossiness", .1f); AssetDatabase.CreateAsset(m, path); return m; }
        static GameObject Part(Transform root, string name, Vector3 position, Vector3 size, Material mat, bool solid = false, PrimitiveType shape = PrimitiveType.Cube)
        { var go = GameObject.CreatePrimitive(shape); go.name = name; go.transform.SetParent(root, false); go.transform.localPosition = position; go.transform.localScale = size; go.GetComponent<Renderer>().sharedMaterial = mat; if (!solid) Object.DestroyImmediate(go.GetComponent<Collider>()); return go; }
        static void CreateCoop()
        {
            const string path = "Assets/Art/Prefabs/Buildings/ChickenCoop.prefab"; if (File.Exists(path)) return; var root = new GameObject("ChickenCoop", typeof(CoopActor));
            try
            {
                var wood = Mat("Hall Wood"); var sage = Mat("Hall Sage"); var cream = Mat("Hall Cream"); var paper = Mat("Board Paper");
                Part(root.transform, "House", new Vector3(0, .8f, .45f), new Vector3(1.5f, 1.6f, 1.2f), cream, true);
                for (int side = -1; side <= 1; side += 2)
                { Part(root.transform, "Roof", new Vector3(side * .42f, 1.87f, .45f), new Vector3(1.02f, .15f, 1.4f), sage).transform.localRotation = Quaternion.Euler(0, 0, side * -26); Part(root.transform, "Side Fence", new Vector3(side * .92f, .28f, -.25f), new Vector3(.08f, .56f, 1.4f), wood, true); }
                Part(root.transform, "Front Fence", new Vector3(0, .28f, -.96f), new Vector3(1.9f, .56f, .08f), wood, true);
                Part(root.transform, "Door", new Vector3(0, .4f, -.164f), new Vector3(.56f, .8f, .04f), wood);
                Part(root.transform, "Door Frame", new Vector3(0, .84f, -.17f), new Vector3(.66f, .09f, .06f), sage);
                Part(root.transform, "Window", new Vector3(.755f, 1.12f, .43f), new Vector3(.02f, .48f, .52f), sage); Part(root.transform, "Window Light", new Vector3(.769f, 1.12f, .43f), new Vector3(.015f, .33f, .36f), paper);
                var actor = root.GetComponent<CoopActor>(); actor.ChickenSpots = new Transform[2];
                for (int i = 0; i < 2; i++) { var spot = new GameObject("Chicken Spot " + (i + 1)).transform; spot.SetParent(root.transform, false); spot.localPosition = new Vector3(i == 0 ? -.34f : .34f, 0, -.52f); actor.ChickenSpots[i] = spot; }
                actor.EggsMarker = new GameObject("Ready Eggs"); actor.EggsMarker.transform.SetParent(root.transform, false);
                Part(actor.EggsMarker.transform, "Nest", new Vector3(.65f, .09f, -.5f), new Vector3(.36f, .18f, .3f), sage);
                for (int i = 0; i < 2; i++) Part(actor.EggsMarker.transform, "Egg", new Vector3(.59f + i * .12f, .25f, -.5f), new Vector3(.13f, .18f, .13f), paper, false, PrimitiveType.Sphere);
                actor.EggsMarker.SetActive(false);
                var label = new GameObject("Coop Name", typeof(TextMeshPro)); label.transform.SetParent(root.transform, false); label.transform.localPosition = new Vector3(0, 2.45f, 0);
                actor.NameText = label.GetComponent<TextMeshPro>(); actor.NameText.font = UiPrefabMigration.FontAsset; actor.NameText.text = "鸡舍"; actor.NameText.fontSize = 1.7f; actor.NameText.rectTransform.sizeDelta = new Vector2(2, .4f); actor.NameText.alignment = TextAlignmentOptions.Center; actor.NameText.color = new Color(1, .97f, .84f);
                root.SetActive(false); PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
        }
        static void CreateChicken()
        {
            const string path = "Assets/Art/Prefabs/Animals/Chicken.prefab"; if (File.Exists(path)) return; var root = new GameObject("Chicken", typeof(ChickenActor));
            try
            {
                var white = AnimalMat("Feathers", new Color(.97f, .93f, .81f)); var red = AnimalMat("Comb", new Color(.82f, .25f, .19f)); var gold = AnimalMat("Beak", new Color(.97f, .66f, .18f));
                Part(root.transform, "Body", new Vector3(0, .3f, 0), new Vector3(.37f, .36f, .45f), white, false, PrimitiveType.Sphere);
                Part(root.transform, "Head", new Vector3(0, .52f, .16f), new Vector3(.24f, .25f, .24f), white, false, PrimitiveType.Sphere);
                Part(root.transform, "Comb", new Vector3(0, .66f, .16f), new Vector3(.07f, .09f, .17f), red); Part(root.transform, "Beak", new Vector3(0, .51f, .3f), new Vector3(.1f, .07f, .1f), gold);
                for (int side = -1; side <= 1; side += 2) { Part(root.transform, "Wing", new Vector3(side * .17f, .32f, 0), new Vector3(.08f, .22f, .29f), white, false, PrimitiveType.Sphere); Part(root.transform, "Foot", new Vector3(side * .08f, .06f, .04f), new Vector3(.05f, .12f, .13f), gold); Part(root.transform, "Eye", new Vector3(side * .11f, .55f, .21f), new Vector3(.025f, .04f, .04f), Mat("Hall Wood"), false, PrimitiveType.Sphere); }
                Part(root.transform, "Tail", new Vector3(0, .42f, -.24f), new Vector3(.15f, .22f, .14f), white).transform.localRotation = Quaternion.Euler(-25, 0, 0);
                root.SetActive(false); PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
        }
        static RectTransform Card(GameObject root, string title)
        {
            var shade = Factory.Panel("Backdrop", root.transform, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, new Color(.12f, .2f, .15f, .58f)); shade.anchorMax = Vector2.one; shade.GetComponent<UnityEngine.UI.Image>().sprite = null;
            var card = Factory.Panel("Card", root.transform, Vector2.one * .5f, Vector2.one * .5f, Vector2.zero, new Vector2(700, 490), Factory.Cream); Factory.SoftShadow(card); Factory.Label("Title", card, new Vector2(32, -20), new Vector2(550, 44), title, 30); return card;
        }
        public static void CreatePanels()
        {
            string path = Factory.Folder + "/CoopPanel.prefab";
            if (!File.Exists(path))
            {
                var root = Factory.CanvasRoot("CoopPanel");
                try
                {
                    var v = root.AddComponent<CoopView>(); v.Layer = UILayer.PopUp; var c = Card(root, "鸡舍 · 日常照料");
                    v.CloseButton = Factory.Button("Close", c, new Vector2(614, -22), new Vector2(54, 42), "×", Factory.Sand);
                    v.StatusText = Factory.Label("Status", c, new Vector2(32, -90), new Vector2(636, 36), "鸡舍已建成 · 鸡 2 / 2 · 待收鸡蛋 2", 21);
                    v.CostsText = Factory.Label("Costs", c, new Vector2(32, -144), new Vector2(636, 36), "建造：木材 15 / 石料 5 / 金币 200", 21);
                    v.StockText = Factory.Label("Stock", c, new Vector2(32, -202), new Vector2(636, 78), "今日已喂 0 / 2 只 · 背包饲料 2\n背包木材 0 / 石料 0 · 金币 100", 18);
                    v.FeedbackText = Factory.Label("Feedback", c, new Vector2(32, -302), new Vector2(636, 52), "每只鸡每天一份饲料，次日产一枚鸡蛋", 19);
                    v.BuildButton = Factory.Button("Build", c, new Vector2(32, -366), new Vector2(202, 44), "申请建造", Factory.Sand);
                    v.FeedButton = Factory.Button("Feed", c, new Vector2(252, -366), new Vector2(202, 44), "喂食未喂的鸡", new Color(.7f, .81f, .62f));
                    v.CollectButton = Factory.Button("Collect", c, new Vector2(472, -366), new Vector2(196, 44), "收取全部鸡蛋", new Color(.7f, .81f, .62f));
                    v.SaveButton = Factory.Button("Save", c, new Vector2(488, -436), new Vector2(180, 32), "保存进度", Factory.Sand); PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { Object.DestroyImmediate(root); }
            }
            path = Factory.Folder + "/AnimalShopPanel.prefab";
            if (!File.Exists(path))
            {
                var root = Factory.CanvasRoot("AnimalShopPanel");
                try
                {
                    var v = root.AddComponent<AnimalShopView>(); v.Layer = UILayer.PopUp; var c = Card(root, "岛屿商店 · 养殖用品");
                    v.CloseButton = Factory.Button("Close", c, new Vector2(614, -22), new Vector2(54, 42), "×", Factory.Sand);
                    v.StockText = Factory.Label("Stock", c, new Vector2(32, -86), new Vector2(636, 64), "金币 100 · 鸡 0 / 2\n背包饲料 0 · 鸡蛋 0", 20);
                    TMP_Text[] prices = new TMP_Text[3]; UnityEngine.UI.Button[] buttons = new UnityEngine.UI.Button[3];
                    for (int i = 0; i < 3; i++) { var offer = Factory.Panel("Offer " + i, c, new Vector2(0, 1), new Vector2(0, 1), new Vector2(32 + i * 220, -180), new Vector2(i == 2 ? 196 : 202, 154), Factory.Sand); prices[i] = Factory.Label("Price", offer, new Vector2(14, -14), new Vector2(i == 2 ? 168 : 174, 62), i == 0 ? "鸡\n50 金币 / 只" : i == 1 ? "饲料\n10 金币 / 份" : "鸡蛋收购\n25 金币 / 枚", 20); buttons[i] = Factory.Button("Action", offer, new Vector2(14, -98), new Vector2(i == 2 ? 168 : 174, 40), i == 0 ? "购买一只" : i == 1 ? "购买一份" : "出售一枚", new Color(.7f, .81f, .62f)); }
                    v.ChickenPriceText = prices[0]; v.FeedPriceText = prices[1]; v.EggPriceText = prices[2]; v.ChickenButton = buttons[0]; v.FeedButton = buttons[1]; v.SellButton = buttons[2];
                    v.FeedbackText = Factory.Label("Feedback", c, new Vector2(32, -360), new Vector2(636, 52), "购买的鸡直接送到鸡舍，饲料放入背包", 19); v.SaveButton = Factory.Button("Save", c, new Vector2(488, -436), new Vector2(180, 32), "保存进度", Factory.Sand); PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { Object.DestroyImmediate(root); }
            }
            UpgradeNavigation();
        }
        static void UpgradeNavigation()
        {
            string path = Factory.Folder + "/OrderPanel.prefab"; var root = PrefabUtility.LoadPrefabContents(path);
            try { var v = root.GetComponent<OrderView>(); if (v.CoopButton == null) { var c = v.DeliverButton.transform.parent; c.GetComponent<RectTransform>().sizeDelta = new Vector2(640, 468); v.CoopButton = Factory.Button("Coop Service", c, new Vector2(464, -412), new Vector2(144, 32), "鸡舍建造", Factory.Sand); v.CoopButton.GetComponentInChildren<TMP_Text>().fontSize = 18; Factory.Label("Services", c, new Vector2(32, -412), new Vector2(420, 32), "更多办事服务", 18); PrefabUtility.SaveAsPrefabAsset(root, path); } }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            path = Factory.Folder + "/ShopPanel.prefab"; root = PrefabUtility.LoadPrefabContents(path);
            try { var v = root.GetComponent<ShopView>(); if (v.AnimalShopButton == null) { v.AnimalShopButton = Factory.Button("Animal Supplies", v.BalanceText.transform.parent, new Vector2(32, -526), new Vector2(180, 32), "养殖用品", Factory.Sand); v.AnimalShopButton.GetComponentInChildren<TMP_Text>().fontSize = 18; PrefabUtility.SaveAsPrefabAsset(root, path); } }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
