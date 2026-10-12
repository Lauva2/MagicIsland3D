using System.IO;
using MagicIsland.Configuration;
using MagicIsland.Interaction;
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
    public static class NPCSetup
    {
        public static void Ensure(AddressableAssetSettings settings, AddressableAssetGroup configuration)
        {
            const string npcPath = "Assets/Config/NPCCatalog.asset", dialoguePath = "Assets/Config/DialogueCatalog.asset";
            if (AssetDatabase.LoadAssetAtPath<NPCCatalog>(npcPath) == null)
            {
                var catalog = ScriptableObject.CreateInstance<NPCCatalog>();
                catalog.NPCs.Add(new NPCConfig { Id = "island_steward", DisplayName = "林舟", Role = "岛务员", PrefabAddress = "IslandSteward", HallOffset = new Vector3(1.7f, 0, -3.55f) });
                AssetDatabase.CreateAsset(catalog, npcPath);
            }
            if (AssetDatabase.LoadAssetAtPath<DialogueCatalog>(dialoguePath) == null)
            {
                var catalog = ScriptableObject.CreateInstance<DialogueCatalog>();
                catalog.Dialogues.Add(new DialogueConfig { NpcId = "island_steward",
                    Intro = new System.Collections.Generic.List<string> { "你好，欢迎来到这座小岛！我是林舟，负责办事处的日常事务。", "门前的公告栏会发布收购订单。准备好作物后，就来这里交付吧。", "田地、料理台和工具工作台都已经备好了。慢慢来，找到自己喜欢的生活节奏。" },
                    Repeat = new System.Collections.Generic.List<string> { "又见面了！今天的岛屿生活还顺利吗？" },
                    Seasons = new[] { "春天很适合种芜菁。播种以后记得浇水，收成会慢慢长起来。", "夏天可以试试番茄和玉米。记得留些玉米，烤着吃也很香。", "秋天能种芜菁和玉米。把多余的收成放进储物箱，冬天也用得上。", "冬天可以整理库存、采集材料。公告栏仍会收购你之前留下的作物。" },
                    Weather = new[] { "今天阳光不错，适合在岛上走走。劳作累了，就吃点料理补充体力。", "今天下雨了，雨水会照顾露天作物。也别忘了在储物箱里留些备用材料。" } });
                AssetDatabase.CreateAsset(catalog, dialoguePath);
            }
            foreach (string path in new[] { npcPath, dialoguePath })
            {
                string guid = AssetDatabase.AssetPathToGUID(path); var entry = settings.FindAssetEntry(guid) ?? settings.CreateOrMoveEntry(guid, configuration);
                string address = Path.GetFileNameWithoutExtension(path); if (entry.address != address) entry.SetAddress(address);
            }
            CreateActor(); const string modelPath = "Assets/Art/Prefabs/Characters/IslandSteward.prefab";
            var group = settings.FindGroup("Characters") ?? settings.CreateGroup("Characters", false, false, true, null, typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            var model = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(modelPath)) ?? settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(modelPath), group);
            if (model.address != "IslandSteward") model.SetAddress("IslandSteward");
        }
        public static void CreatePanel()
        {
            string path = Factory.Folder + "/DialoguePanel.prefab"; if (File.Exists(path)) return; var root = Factory.CanvasRoot("DialoguePanel");
            try
            {
                var view = root.AddComponent<DialogueView>(); view.Layer = UILayer.PopUp;
                var shade = Factory.Panel("Backdrop", root.transform, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, new Color(.12f, .2f, .15f, .38f)); shade.anchorMax = Vector2.one; shade.GetComponent<Image>().sprite = null;
                var panel = Factory.Panel("Dialogue Card", root.transform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 42), new Vector2(960, 300), Factory.Cream); Factory.SoftShadow(panel);
                view.SpeakerText = Factory.Label("Speaker", panel, new Vector2(32, -20), new Vector2(760, 40), "林舟 · 岛务员", 28);
                view.CloseButton = Factory.Button("Close", panel, new Vector2(874, -22), new Vector2(54, 42), "×", Factory.Sand);
                var line = Factory.Panel("Accent", panel, new Vector2(0, 1), new Vector2(0, 1), new Vector2(32, -76), new Vector2(896, 4), new Color(.70f, .81f, .62f)); line.GetComponent<Image>().raycastTarget = false;
                view.BodyText = Factory.Label("Dialogue", panel, new Vector2(32, -98), new Vector2(896, 122), "你好，欢迎来到这座小岛！", 24);
                view.BodyText.enableAutoSizing = true; view.BodyText.fontSizeMin = 18; view.BodyText.fontSizeMax = 24;
                view.ProgressText = Factory.Label("Progress", panel, new Vector2(32, -244), new Vector2(670, 30), "1 / 5 · ESC 结束交谈", 18);
                view.NextButton = Factory.Button("Next", panel, new Vector2(732, -232), new Vector2(196, 48), "下一句", new Color(.70f, .81f, .62f));
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
        }
        private static GameObject Part(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name; go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material; Object.DestroyImmediate(go.GetComponent<Collider>()); return go;
        }
        private static void CreateActor()
        {
            const string path = "Assets/Art/Prefabs/Characters/IslandSteward.prefab"; if (File.Exists(path)) return;
            Directory.CreateDirectory("Assets/Art/Prefabs/Characters"); AssetDatabase.Refresh();
            var sage = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Buildings/Hall Sage.mat"); var wood = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Buildings/Hall Wood.mat");
            var skin = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Buildings/Hall Cream.mat"); var cream = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Buildings/Board Paper.mat");
            var stone = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Buildings/Hall Stone.mat");
            var root = new GameObject("IslandSteward", typeof(NPCActor), typeof(CapsuleCollider));
            try
            {
                var collider = root.GetComponent<CapsuleCollider>(); collider.center = new Vector3(0, .85f, 0); collider.height = 1.7f; collider.radius = .30f;
                Part(root.transform, "Coat", PrimitiveType.Capsule, new Vector3(0, .86f, 0), new Vector3(.57f, .44f, .43f), sage);
                Part(root.transform, "Collar", PrimitiveType.Cube, new Vector3(0, 1.19f, .12f), new Vector3(.31f, .16f, .12f), cream);
                Part(root.transform, "Head", PrimitiveType.Sphere, new Vector3(0, 1.46f, 0), new Vector3(.46f, .46f, .44f), skin);
                Part(root.transform, "Hair", PrimitiveType.Sphere, new Vector3(0, 1.57f, -.035f), new Vector3(.49f, .30f, .45f), wood);
                Part(root.transform, "Hat", PrimitiveType.Cylinder, new Vector3(0, 1.75f, 0), new Vector3(.53f, .06f, .48f), sage);
                Part(root.transform, "Hat Brim", PrimitiveType.Cube, new Vector3(0, 1.69f, .17f), new Vector3(.48f, .04f, .24f), sage);
                for (int side = -1; side <= 1; side += 2)
                {
                    Part(root.transform, "Leg", PrimitiveType.Capsule, new Vector3(side * .14f, .34f, 0), new Vector3(.18f, .21f, .18f), stone);
                    Part(root.transform, "Boot", PrimitiveType.Cube, new Vector3(side * .14f, .09f, .06f), new Vector3(.22f, .16f, .34f), wood);
                    Part(root.transform, "Sleeve", PrimitiveType.Capsule, new Vector3(side * .36f, .89f, 0), new Vector3(.18f, .24f, .18f), sage);
                    Part(root.transform, "Hand", PrimitiveType.Sphere, new Vector3(side * .36f, .62f, .03f), new Vector3(.16f, .17f, .16f), skin);
                    Part(root.transform, "Eye", PrimitiveType.Sphere, new Vector3(side * .085f, 1.49f, .207f), new Vector3(.038f, .044f, .020f), wood);
                }
                Part(root.transform, "Smile", PrimitiveType.Cube, new Vector3(0, 1.36f, .205f), new Vector3(.075f, .018f, .022f), wood);
                Part(root.transform, "Badge", PrimitiveType.Cube, new Vector3(.16f, 1.06f, .21f), new Vector3(.09f, .10f, .025f), cream);
                root.transform.localRotation = Quaternion.Euler(0, 180, 0);
                var label = new GameObject("Name", typeof(TextMeshPro)); label.transform.SetParent(root.transform, false); label.transform.localPosition = new Vector3(0, 2.0f, 0);
                var text = label.GetComponent<TextMeshPro>(); text.font = UiPrefabMigration.FontAsset; text.text = "林舟"; text.fontSize = 2.3f; text.rectTransform.sizeDelta = new Vector2(1.5f, .25f); text.alignment = TextAlignmentOptions.Center; text.color = new Color(1, .97f, .83f);
                root.GetComponent<NPCActor>().NameText = text; PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
