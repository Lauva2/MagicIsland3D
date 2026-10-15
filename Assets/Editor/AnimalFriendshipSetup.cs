using System.IO;
using MagicIsland.Configuration;
using MagicIsland.Interaction;
using MagicIsland.UI.Views;
using MagicIsland.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Factory = MagicIsland.EditorTools.PrototypeUiPrefabBuilder;
namespace MagicIsland.EditorTools
{
    public static class AnimalFriendshipSetup
    {
        public static void Ensure()
        {
            const string configPath = "Assets/Config/AnimalConfig.asset";
            var config = AssetDatabase.LoadAssetAtPath<AnimalConfig>(configPath); string serialized = File.ReadAllText(configPath);
            if (!serialized.Contains("ChickenNames:") || !serialized.Contains("PetAffinity:") || !serialized.Contains("FeedAffinity:")) EditorUtility.SetDirty(config);
            const string path = "Assets/Art/Prefabs/Animals/Chicken.prefab"; var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var actor = root.GetComponent<ChickenActor>(); if (actor.HeartRoot != null) return;
                var previous = root.transform.Find("Pet Heart"); if (previous != null) Object.DestroyImmediate(previous.gameObject);
                Directory.CreateDirectory("Assets/Art/Meshes/Animals"); AssetDatabase.Refresh();
                const string meshPath = "Assets/Art/Meshes/Animals/ChickenHeart.asset";
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if (mesh == null)
                {
                    mesh = new Mesh { name = "ChickenHeart" }; var vertices = new Vector3[HeartGraphic.Segments + 1]; var colors = new Color[vertices.Length]; var triangles = new int[HeartGraphic.Segments * 3];
                    vertices[0] = HeartGraphic.Center;
                    for (int i = 0; i < HeartGraphic.Segments; i++) { vertices[i + 1] = HeartGraphic.Point(i * Mathf.PI * 2 / HeartGraphic.Segments); triangles[i * 3] = 0; triangles[i * 3 + 1] = i + 1; triangles[i * 3 + 2] = (i + 1) % HeartGraphic.Segments + 1; }
                    for (int i = 0; i < colors.Length; i++) colors[i] = Color.white;
                    mesh.vertices = vertices; mesh.colors = colors; mesh.triangles = triangles; mesh.RecalculateNormals(); mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh, meshPath);
                }
                const string materialPath = "Assets/Art/Materials/Animals/Heart.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null) { material = new Material(Shader.Find("Sprites/Default")) { name = "Heart", color = Color.white }; AssetDatabase.CreateAsset(material, materialPath); }
                var go = new GameObject("Pet Heart", typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(root.transform, false); go.transform.localPosition = new Vector3(0, .85f, 0); go.transform.localScale = Vector3.one * .42f;
                go.GetComponent<MeshFilter>().sharedMesh = mesh; go.GetComponent<MeshRenderer>().sharedMaterial = material; go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; go.GetComponent<MeshRenderer>().receiveShadows = false;
                go.SetActive(false); actor.HeartRoot = go.transform;
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        public static void UpgradePanel()
        {
            string path = Factory.Folder + "/CoopPanel.prefab"; var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view = root.GetComponent<CoopView>(); var parent = view.SaveButton.transform.parent;
                if (view.PetButtons != null && view.PetButtons.Length == 2)
                {
                    bool changed = false;
                    if (view.HeartGraphics == null || view.HeartGraphics.Length != 2)
                    { view.HeartGraphics = new Graphic[2]; for (int i = 0; i < 2; i++) view.HeartGraphics[i] = CreateHeart(view.PetButtons[i].transform.parent); changed = true; }
                    foreach (var heart in view.HeartGraphics) if (heart.GetComponent<CanvasRenderer>() == null) { heart.gameObject.AddComponent<CanvasRenderer>(); changed = true; }
                    if (view.GuideText == null)
                    {
                        view.GuideText = parent.Find("Friendship Guide")?.GetComponent<TMP_Text>() ?? Factory.Label("Friendship Guide", parent, new Vector2(32, -560), new Vector2(636, 30), "每日摸摸 +5 · 首次喂食 +2 · 亲密度最高 100", 17);
                        changed = true;
                    }
                    if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
                    return;
                }
                parent.GetComponent<RectTransform>().sizeDelta = new Vector2(700, 650);
                view.StatusText.rectTransform.anchoredPosition = new Vector2(32, -80); view.StatusText.rectTransform.sizeDelta = new Vector2(636, 30);
                view.CostsText.rectTransform.anchoredPosition = new Vector2(32, -120); view.CostsText.rectTransform.sizeDelta = new Vector2(636, 30);
                view.StockText.rectTransform.anchoredPosition = new Vector2(32, -160); view.StockText.rectTransform.sizeDelta = new Vector2(636, 54);
                view.FeedbackText.rectTransform.anchoredPosition = new Vector2(32, -452); view.FeedbackText.rectTransform.sizeDelta = new Vector2(636, 42);
                view.BuildButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(32, -506);
                view.FeedButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(252, -506);
                view.CollectButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(472, -506);
                view.SaveButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(488, -604);
                view.ChickenNameTexts = new TMP_Text[2]; view.AffinityTexts = new TMP_Text[2]; view.CareTexts = new TMP_Text[2]; view.HeartGraphics = new Graphic[2]; view.PetButtons = new Button[2]; view.AffinityBars = new Slider[2];
                for (int i = 0; i < 2; i++)
                {
                    var card = Factory.Panel("Chicken " + (i + 1), parent, new Vector2(0, 1), new Vector2(0, 1), new Vector2(32 + i * 330, -228), new Vector2(306, 206), Factory.Sand);
                    view.ChickenNameTexts[i] = Factory.Label("Name", card, new Vector2(18, -12), new Vector2(232, 30), i == 0 ? "小白" : "团团", 22);
                    view.ChickenNameTexts[i].enableAutoSizing = true; view.ChickenNameTexts[i].fontSizeMin = 16; view.ChickenNameTexts[i].fontSizeMax = 22;
                    view.HeartGraphics[i] = CreateHeart(card);
                    view.AffinityTexts[i] = Factory.Label("Affinity", card, new Vector2(18, -52), new Vector2(270, 26), "亲密度 0 / 100 · 陌生", 18);
                    var rail = Factory.Panel("Affinity Bar", card, new Vector2(0, 1), new Vector2(0, 1), new Vector2(18, -86), new Vector2(270, 14), new Color(.82f, .81f, .72f));
                    var fill = Factory.Panel("Fill", rail, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, new Color(.57f, .72f, .47f)); fill.anchorMax = Vector2.one; fill.GetComponent<Image>().raycastTarget = false; rail.GetComponent<Image>().raycastTarget = false;
                    var slider = rail.gameObject.AddComponent<Slider>(); slider.fillRect = fill; slider.minValue = 0; slider.maxValue = 100; slider.wholeNumbers = true; slider.interactable = false; slider.navigation = new Navigation { mode = Navigation.Mode.None }; view.AffinityBars[i] = slider;
                    view.CareTexts[i] = Factory.Label("Daily Care", card, new Vector2(18, -112), new Vector2(270, 26), "今日未喂 · 未摸摸", 17);
                    view.PetButtons[i] = Factory.Button("Pet", card, new Vector2(18, -152), new Vector2(270, 38), "摸摸", new Color(.78f, .85f, .69f));
                }
                view.GuideText = Factory.Label("Friendship Guide", parent, new Vector2(32, -560), new Vector2(636, 30), "每日摸摸 +5 · 首次喂食 +2 · 亲密度最高 100", 17);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        private static Graphic CreateHeart(Transform parent)
        {
            var previous = parent.Find("Pet Heart"); if (previous != null) Object.DestroyImmediate(previous.gameObject);
            var rect = Factory.Rect("Pet Heart", parent, new Vector2(0, 1), new Vector2(0, 1), new Vector2(260, -12), new Vector2(30, 30));
            var heart = rect.gameObject.AddComponent<HeartGraphic>(); heart.color = new Color(.91f, .39f, .44f); heart.raycastTarget = false; rect.gameObject.SetActive(false); return heart;
        }
    }
}
