using System.IO;
using MagicIsland.Interaction;
using TMPro;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace MagicIsland.EditorTools
{
    public static class OrderHallPrefabBuilder
    {
        public const string Path = "Assets/Art/Prefabs/Buildings/OrderHall.prefab";
        public static void Ensure(AddressableAssetSettings settings)
        {
            if (!File.Exists(Path)) Create();
            var group = settings.FindGroup("Buildings") ?? settings.CreateGroup("Buildings", false, false, true, null,
                typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            string guid = AssetDatabase.AssetPathToGUID(Path);
            var entry = settings.FindAssetEntry(guid) ?? settings.CreateOrMoveEntry(guid, group);
            if (entry.address != "OrderHall") entry.SetAddress("OrderHall");
        }
        private static Material Material(string name, Color color)
        {
            string path = "Assets/Art/Materials/Buildings/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Standard")) { name = name, color = color };
            material.SetFloat("_Glossiness", 0.12f); AssetDatabase.CreateAsset(material, path); return material;
        }
        private static GameObject Box(Transform parent, string name, Vector3 position, Vector3 scale, Material material, float roll = 0)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube); part.name = name; part.transform.SetParent(parent, false);
            part.transform.localPosition = position; part.transform.localScale = scale;
            part.transform.localRotation = Quaternion.Euler(0, 0, roll); part.GetComponent<Renderer>().sharedMaterial = material; return part;
        }
        private static void Create()
        {
            Directory.CreateDirectory("Assets/Art/Prefabs/Buildings"); Directory.CreateDirectory("Assets/Art/Materials/Buildings");
            Directory.CreateDirectory("Assets/Art/Meshes/Buildings"); AssetDatabase.Refresh();
            var cream = Material("Hall Cream", new Color(0.91f, 0.85f, 0.68f));
            var sage = Material("Hall Sage", new Color(0.35f, 0.54f, 0.40f));
            var wood = Material("Hall Wood", new Color(0.39f, 0.25f, 0.16f));
            var stone = Material("Hall Stone", new Color(0.64f, 0.67f, 0.58f));
            var glass = Material("Hall Windows", new Color(0.51f, 0.76f, 0.79f));
            var paper = Material("Board Paper", new Color(1f, 0.96f, 0.81f));
            var pin = Material("Board Pins", new Color(0.86f, 0.45f, 0.30f));
            var root = new GameObject("OrderHall");
            try
            {
                Box(root.transform, "Foundation", new Vector3(0, 0.12f, 0), new Vector3(3.7f, 0.24f, 2.9f), stone);
                Box(root.transform, "Walls", new Vector3(0, 1.135f, 0), new Vector3(3.4f, 2.03f, 2.6f), cream);
                var gable = new GameObject("Gable", typeof(MeshFilter), typeof(MeshRenderer)); gable.transform.SetParent(root.transform, false);
                gable.transform.localPosition = new Vector3(0, 2.15f, 0); gable.GetComponent<MeshFilter>().sharedMesh = GableMesh();
                gable.GetComponent<MeshRenderer>().sharedMaterial = cream;
                Box(root.transform, "Left Roof", new Vector3(-0.91f, 2.7f, 0), new Vector3(2.25f, 0.22f, 3.25f), sage, 30);
                Box(root.transform, "Right Roof", new Vector3(0.91f, 2.7f, 0), new Vector3(2.25f, 0.22f, 3.25f), sage, -30);
                Box(root.transform, "Door Frame", new Vector3(-0.55f, 0.96f, -1.34f), new Vector3(0.98f, 1.85f, 0.16f), wood);
                Box(root.transform, "Door", new Vector3(-0.55f, 0.92f, -1.44f), new Vector3(0.75f, 1.63f, 0.1f), sage);
                Box(root.transform, "Handle", new Vector3(-0.3f, 0.93f, -1.53f), new Vector3(0.08f, 0.08f, 0.07f), paper);
                Box(root.transform, "Front Window Frame", new Vector3(0.94f, 1.34f, -1.35f), new Vector3(0.96f, 0.98f, 0.15f), wood);
                Box(root.transform, "Front Window", new Vector3(0.94f, 1.34f, -1.45f), new Vector3(0.8f, 0.82f, 0.06f), glass);
                Box(root.transform, "Window Cross", new Vector3(0.94f, 1.34f, -1.50f), new Vector3(0.05f, 0.82f, 0.06f), wood);
                Box(root.transform, "Window Crossbar", new Vector3(0.94f, 1.34f, -1.50f), new Vector3(0.8f, 0.05f, 0.06f), wood);
                Box(root.transform, "Side Window Frame", new Vector3(-1.74f, 1.4f, 0.1f), new Vector3(0.13f, 1f, 1.1f), wood);
                Box(root.transform, "Side Window", new Vector3(-1.82f, 1.4f, 0.1f), new Vector3(0.05f, 0.82f, 0.9f), glass);
                Box(root.transform, "Step", new Vector3(-0.55f, 0.10f, -1.78f), new Vector3(1.12f, 0.2f, 0.72f), stone);
                Box(root.transform, "Door Path", new Vector3(-0.55f, 0.025f, -2.65f), new Vector3(1.12f, 0.05f, 1.35f), stone);
                Box(root.transform, "Hall Sign", new Vector3(0, 2.17f, -1.4f), new Vector3(2.35f, 0.42f, 0.12f), wood);
                Label(root.transform, "Hall Name", "岛屿办事处", new Vector3(0, 2.17f, -1.48f), new Vector2(2.2f, 0.36f), 3.5f);
                var board = new GameObject("OrderBoard", typeof(OrderBoard)); board.transform.SetParent(root.transform, false);
                board.transform.localPosition = new Vector3(1.28f, 0, -2.40f);
                Box(board.transform, "Left Post", new Vector3(-0.70f, 0.88f, 0), new Vector3(0.12f, 1.76f, 0.14f), wood);
                Box(board.transform, "Right Post", new Vector3(0.70f, 0.88f, 0), new Vector3(0.12f, 1.76f, 0.14f), wood);
                Box(board.transform, "Board Frame", new Vector3(0, 1.28f, 0), new Vector3(1.62f, 0.94f, 0.16f), wood);
                Box(board.transform, "Board Surface", new Vector3(0, 1.28f, -0.10f), new Vector3(1.43f, 0.75f, 0.05f), cream);
                Box(board.transform, "Board Canopy", new Vector3(0, 1.90f, 0), new Vector3(1.85f, 0.14f, 0.57f), sage);
                for (int i = 0; i < 3; i++)
                {
                    float x = (i - 1) * 0.43f;
                    Box(board.transform, "Order Paper " + i, new Vector3(x, 1.28f, -0.16f), new Vector3(0.34f, 0.46f, 0.025f), paper, (i - 1) * 5);
                    Box(board.transform, "Pin " + i, new Vector3(x, 1.48f, -0.19f), new Vector3(0.06f, 0.06f, 0.035f), pin);
                    Box(board.transform, "Ink Line " + i, new Vector3(x, 1.31f, -0.19f), new Vector3(0.22f, 0.025f, 0.015f), sage);
                    Box(board.transform, "Ink Line Lower " + i, new Vector3(x, 1.22f, -0.19f), new Vector3(0.17f, 0.025f, 0.015f), sage);
                }
                Label(board.transform, "Board Name", "今日订单", new Vector3(0, 1.90f, -0.31f), new Vector2(1.55f, 0.16f), 1.8f);
                PrefabUtility.SaveAsPrefabAsset(root, Path);
            }
            finally { Object.DestroyImmediate(root); }
        }
        private static void Label(Transform parent, string name, string text, Vector3 position, Vector2 size, float fontSize)
        {
            var go = new GameObject(name, typeof(TextMeshPro)); go.transform.SetParent(parent, false); go.transform.localPosition = position;
            var label = go.GetComponent<TextMeshPro>(); label.rectTransform.sizeDelta = size; label.font = UiPrefabMigration.FontAsset;
            label.text = text; label.fontSize = fontSize; label.alignment = TextAlignmentOptions.Center; label.color = new Color(1f, 0.97f, 0.83f);
        }
        private static Mesh GableMesh()
        {
            const string path = "Assets/Art/Meshes/Buildings/Hall Gable.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (mesh != null) return mesh;
            mesh = new Mesh { name = "Hall Gable" };
            mesh.vertices = new[] { new Vector3(-1.7f, 0, -1.3f), new Vector3(0, 1, -1.3f), new Vector3(1.7f, 0, -1.3f),
                new Vector3(-1.7f, 0, 1.3f), new Vector3(0, 1, 1.3f), new Vector3(1.7f, 0, 1.3f) };
            mesh.triangles = new[] { 0, 1, 2, 5, 4, 3, 0, 3, 4, 0, 4, 1, 1, 4, 5, 1, 5, 2, 2, 5, 3, 2, 3, 0 };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh, path); return mesh;
        }
    }
}
