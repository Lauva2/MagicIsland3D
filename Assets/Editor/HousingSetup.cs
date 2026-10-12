using System.IO;
using MagicIsland.Configuration;
using MagicIsland.Interaction;
using MagicIsland.UI.Views;
using TMPro;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Factory = MagicIsland.EditorTools.PrototypeUiPrefabBuilder;
namespace MagicIsland.EditorTools
{
    public static class HousingSetup
    {
        const string ExteriorPath = "Assets/Art/Prefabs/Buildings/PlayerHouse.prefab", InteriorPath = "Assets/Art/Prefabs/Buildings/PlayerHomeInterior.prefab", ScenePath = "Assets/Scenes/PlayerHome.unity";
        public static void Ensure(AddressableAssetSettings settings, AddressableAssetGroup group)
        {
            const string configPath = "Assets/Config/HousingConfig.asset";
            var config = AssetDatabase.LoadAssetAtPath<HousingConfig>(configPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<HousingConfig>();
                int layer = LayerMask.NameToLayer("HousingInterior");
                if (layer < 0)
                {
                    var tag = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]); var list = tag.FindProperty("layers");
                    for (int i = 8; i < 32; i++) if (string.IsNullOrEmpty(list.GetArrayElementAtIndex(i).stringValue)) { layer = i; list.GetArrayElementAtIndex(i).stringValue = "HousingInterior"; tag.ApplyModifiedPropertiesWithoutUndo(); break; }
                }
                if (layer < 8) throw new System.Exception("No free layer for housing interior");
                config.InteriorLayer = layer; AssetDatabase.CreateAsset(config, configPath);
            }
            Register(settings, group, configPath, HousingConfig.Address);
            CreateExterior(); CreateInterior(config.InteriorLayer);
            Register(settings, settings.FindGroup("Buildings"), ExteriorPath, config.ExteriorAddress);
            Register(settings, settings.FindGroup("Buildings"), InteriorPath, "PlayerHomeInterior");
            if (!File.Exists(ScenePath))
            {
                var previous = SceneManager.GetActiveScene(); var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                try
                {
                    var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(InteriorPath), scene);
                    root.transform.position = config.InteriorOrigin; EditorSceneManager.SaveScene(scene, ScenePath);
                }
                finally { EditorSceneManager.CloseScene(scene, true); if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous); }
            }
            Register(settings, settings.FindAssetEntry(AssetDatabase.AssetPathToGUID("Assets/Scenes/MainGame.unity")).parentGroup, ScenePath, config.InteriorSceneAddress);
            EditorBuildSettings.scenes = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Where(EditorBuildSettings.scenes, s => s.path != ScenePath));
        }
        static void Register(AddressableAssetSettings settings, AddressableAssetGroup group, string path, string address)
        { var guid = AssetDatabase.AssetPathToGUID(path); var e = settings.FindAssetEntry(guid) ?? settings.CreateOrMoveEntry(guid, group); if (e.address != address) e.SetAddress(address); }
        static Material Mat(string name) => AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Buildings/" + name + ".mat");
        static GameObject Box(Transform parent, string name, Vector3 point, Vector3 size, Material mat, bool solid = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent, false);
            go.transform.localPosition = point; go.transform.localScale = size; go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!solid) Object.DestroyImmediate(go.GetComponent<Collider>()); return go;
        }
        static Transform Point(Transform root, string name, Vector3 position)
        { var t = new GameObject(name).transform; t.SetParent(root, false); t.localPosition = position; return t; }
        static TMP_Text Label(Transform parent, string name, Vector3 point, string content, float size)
        {
            var go = new GameObject(name, typeof(TextMeshPro)); go.transform.SetParent(parent, false); go.transform.localPosition = point;
            var text = go.GetComponent<TextMeshPro>(); text.font = UiPrefabMigration.FontAsset; text.fontSize = size; text.text = content;
            text.rectTransform.sizeDelta = new Vector2(1.8f, .3f); text.alignment = TextAlignmentOptions.Center; text.color = new Color(1, .97f, .83f); return text;
        }
        static void CreateExterior()
        {
            if (File.Exists(ExteriorPath)) return;
            var root = new GameObject("PlayerHouse", typeof(HomePortal), typeof(BoxCollider));
            try
            {
                var c = root.GetComponent<BoxCollider>(); c.center = new Vector3(0, 1.15f, 0); c.size = new Vector3(1.3f, 2.3f, 2);
                var cream = Mat("Hall Cream"); var wood = Mat("Hall Wood"); var sage = Mat("Hall Sage"); var paper = Mat("Board Paper");
                Box(root.transform, "Cottage Walls", new Vector3(0, 1.1f, 0), new Vector3(1.28f, 2.2f, 1.98f), cream, false);
                Box(root.transform, "Roof Left", new Vector3(-.38f, 2.47f, 0), new Vector3(.93f, .16f, 2.3f), sage, false).transform.localRotation = Quaternion.Euler(0, 0, 27);
                Box(root.transform, "Roof Right", new Vector3(.38f, 2.47f, 0), new Vector3(.93f, .16f, 2.3f), sage, false).transform.localRotation = Quaternion.Euler(0, 0, -27);
                Box(root.transform, "Door Frame", new Vector3(0, .89f, -1), new Vector3(.86f, 1.8f, .06f), wood, false);
                Box(root.transform, "Sage Door", new Vector3(0, .89f, -1.04f), new Vector3(.69f, 1.68f, .04f), sage, false);
                Box(root.transform, "Door Window", new Vector3(0, 1.26f, -1.069f), new Vector3(.41f, .42f, .02f), paper, false);
                Box(root.transform, "Door Handle", new Vector3(.25f, .79f, -1.079f), new Vector3(.07f, .09f, .03f), paper, false);
                Box(root.transform, "Side Window", new Vector3(-.651f, 1.35f, .28f), new Vector3(.02f, .63f, .71f), sage, false);
                Box(root.transform, "Chimney", new Vector3(.35f, 2.58f, .6f), new Vector3(.25f, .72f, .32f), Mat("Hall Stone"), false);
                var portal = root.GetComponent<HomePortal>(); portal.ReachPoint = Point(root.transform, "Door Reach", new Vector3(0, 0, -1.08f));
                portal.ArrivalPoint = Point(root.transform, "Return Point", new Vector3(0, .1f, -2.08f));
                portal.NameText = Label(root.transform, "Cottage Name", new Vector3(0, 3.15f, 0), "我的小屋", 1.7f);
                PrefabUtility.SaveAsPrefabAsset(root, ExteriorPath);
            }
            finally { Object.DestroyImmediate(root); }
        }
        static void CreateInterior(int layer)
        {
            if (File.Exists(InteriorPath)) return; var root = new GameObject("PlayerHomeInterior", typeof(HomeInterior));
            try
            {
                var wood = Mat("Hall Wood"); var cream = Mat("Hall Cream"); var sage = Mat("Hall Sage"); var paper = Mat("Board Paper"); var stone = Mat("Hall Stone");
                Box(root.transform, "Floor", new Vector3(0, -.16f, 0), new Vector3(12, .32f, 10), wood);
                for (int i = -5; i <= 5; i++) Box(root.transform, "Floor Inlay", new Vector3(i, .007f, 0), new Vector3(.015f, .009f, 9.8f), cream, false);
                Box(root.transform, "Back Wall", new Vector3(0, 1.6f, 5), new Vector3(12, 3.2f, .2f), cream);
                Box(root.transform, "Left Wall", new Vector3(-6, 1.6f, 0), new Vector3(.2f, 3.2f, 10), cream);
                Box(root.transform, "Right Wall", new Vector3(6, 1.6f, 0), new Vector3(.2f, 3.2f, 10), cream);
                var front = Box(root.transform, "Front Collision", new Vector3(0, 1.6f, -5), new Vector3(12, 3.2f, .2f), cream); front.GetComponent<Renderer>().enabled = false; front.layer = 2;
                Box(root.transform, "Back Skirting", new Vector3(0, .16f, 4.88f), new Vector3(11.8f, .32f, .08f), sage, false);
                Box(root.transform, "Window Frame", new Vector3(1.5f, 1.85f, 4.87f), new Vector3(2.7f, 1.55f, .08f), sage, false);
                Box(root.transform, "Window Light", new Vector3(1.5f, 1.85f, 4.81f), new Vector3(2.4f, 1.3f, .05f), paper, false);
                Box(root.transform, "Window Cross", new Vector3(1.5f, 1.85f, 4.76f), new Vector3(.07f, 1.32f, .03f), wood, false);
                Box(root.transform, "Window Bar", new Vector3(1.5f, 1.85f, 4.76f), new Vector3(2.42f, .07f, .03f), wood, false);
                Box(root.transform, "Sage Rug", new Vector3(0, .015f, -.3f), new Vector3(3.4f, .024f, 3), sage, false);
                var bed = new GameObject("Home Bed", typeof(SleepBed)); bed.transform.SetParent(root.transform, false); bed.transform.localPosition = new Vector3(-3, 0, 2);
                Box(bed.transform, "Bed Frame", new Vector3(0, .3f, 0), new Vector3(1.3f, .6f, 2), wood);
                Box(bed.transform, "Blanket", new Vector3(0, .65f, 0), new Vector3(1.25f, .12f, 1.9f), sage);
                Box(bed.transform, "Pillow", new Vector3(0, .77f, .62f), new Vector3(1, .15f, .5f), paper);
                Box(bed.transform, "Headboard", new Vector3(0, .53f, .99f), new Vector3(1.45f, 1.06f, .15f), cream);
                Box(root.transform, "Bedside Table", new Vector3(-4.6f, .38f, 2.6f), new Vector3(.8f, .76f, .8f), wood);
                Box(root.transform, "Reading Shelf", new Vector3(4.8f, 1.05f, 3.9f), new Vector3(1.6f, 2.1f, .7f), sage);
                for (int i = 0; i < 4; i++) Box(root.transform, "Books", new Vector3(4.3f + i * .32f, 1.8f, 3.51f), new Vector3(.18f, .37f, .14f), i % 2 == 0 ? paper : wood, false);
                var exit = new GameObject("Home Exit", typeof(HomePortal), typeof(BoxCollider)); exit.transform.SetParent(root.transform, false); exit.transform.localPosition = new Vector3(0, 0, -4.5f);
                var ec = exit.GetComponent<BoxCollider>(); ec.isTrigger = true; ec.center = new Vector3(0, .65f, 0); ec.size = new Vector3(1.65f, 1.3f, .25f);
                Box(exit.transform, "Door Mat", new Vector3(0, .017f, 0), new Vector3(1.5f, .024f, .45f), sage, false);
                var portal = exit.GetComponent<HomePortal>(); portal.IsExit = true; portal.NameText = Label(exit.transform, "Exit Name", new Vector3(0, .8f, 0), "返回农场", 1.3f);
                var room = root.GetComponent<HomeInterior>(); room.Bed = bed.GetComponent<SleepBed>(); room.Exit = portal; room.SpawnPoint = Point(root.transform, "Arrival", new Vector3(0, .1f, -2.5f));
                var daylight = new GameObject("Interior Daylight", typeof(Light)); daylight.transform.SetParent(root.transform, false); daylight.transform.rotation = Quaternion.Euler(55, -25, 0);
                var lamp = daylight.GetComponent<Light>(); lamp.type = LightType.Directional; lamp.intensity = .9f; lamp.color = new Color(1, .93f, .8f); lamp.cullingMask = 1 << layer; lamp.shadows = LightShadows.Soft;
                for (int i = -1; i <= 1; i += 2) { var l = new GameObject("Warm Room Light", typeof(Light)); l.transform.SetParent(root.transform, false); l.transform.localPosition = new Vector3(i * 3, 2.6f, 0); var light = l.GetComponent<Light>(); light.type = LightType.Point; light.range = 8; light.intensity = 1.6f; light.color = new Color(1, .8f, .55f); light.cullingMask = 1 << layer; }
                foreach (var child in root.GetComponentsInChildren<Transform>(true)) if (child.gameObject.layer != 2) child.gameObject.layer = layer;
                PrefabUtility.SaveAsPrefabAsset(root, InteriorPath);
            }
            finally { Object.DestroyImmediate(root); }
        }
        public static void CreatePanel()
        {
            string path = Factory.Folder + "/SceneTransitionPanel.prefab"; if (File.Exists(path)) return; var root = Factory.CanvasRoot("SceneTransitionPanel");
            try
            {
                var v = root.AddComponent<SceneTransitionView>(); v.Layer = UILayer.Top;
                var shade = Factory.Panel("Backdrop", root.transform, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, new Color(.14f, .21f, .17f, .94f)); shade.anchorMax = Vector2.one; shade.GetComponent<UnityEngine.UI.Image>().sprite = null;
                var card = Factory.Panel("Transition Card", root.transform, Vector2.one * .5f, Vector2.one * .5f, Vector2.zero, new Vector2(500, 150), Factory.Cream); Factory.SoftShadow(card);
                v.MessageText = Factory.Label("Message", card, new Vector2(28, -28), new Vector2(444, 44), "正在进入小屋…", 30, TextAnchor.MiddleCenter);
                Factory.Label("Hint", card, new Vector2(28, -86), new Vector2(444, 28), "稍等片刻，小岛生活马上继续", 18, TextAnchor.MiddleCenter);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
        }
        public static void UpgradeHud()
        {
            string path = Factory.Folder + "/FarmHudPanel.prefab"; var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var hud = root.GetComponent<PrototypeHud>(); if (hud.LocationText != null) return;
                foreach (var text in root.GetComponentsInChildren<TMP_Text>(true)) if (text.text == "岛屿农场") { hud.LocationText = text; break; }
                if (hud.LocationText == null) throw new System.Exception("Farm HUD needs a location title");
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
