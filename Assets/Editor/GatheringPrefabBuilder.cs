using System.IO;
using MagicIsland.Interaction;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
namespace MagicIsland.EditorTools
{
    public static class GatheringPrefabBuilder
    {
        private const string Folder = "Assets/Art/Prefabs/Gathering";
        public static void Ensure(AddressableAssetSettings settings)
        {
            Directory.CreateDirectory(Folder); Directory.CreateDirectory("Assets/Art/Materials/Gathering"); Directory.CreateDirectory("Assets/Art/Meshes/Gathering"); AssetDatabase.Refresh();
            if (!File.Exists(Folder + "/GatheringTree.prefab")) Create(true);
            if (!File.Exists(Folder + "/GatheringRock.prefab")) Create(false);
            var group = settings.FindGroup("Gathering Models") ?? settings.CreateGroup("Gathering Models", false, false, true, null,
                typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            foreach (string name in new[] { "GatheringTree", "GatheringRock" })
            {
                string guid = AssetDatabase.AssetPathToGUID(Folder + "/" + name + ".prefab");
                var entry = settings.FindAssetEntry(guid) ?? settings.CreateOrMoveEntry(guid, group); if (entry.address != name) entry.SetAddress(name);
            }
        }
        private static Material Material(string name, Color color)
        {
            string path = "Assets/Art/Materials/Gathering/" + name + ".mat"; var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Standard")) { name = name, color = color }; material.SetFloat("_Glossiness", 0.08f);
            AssetDatabase.CreateAsset(material, path); return material;
        }
        private static GameObject Part(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material, bool trigger = false)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name; go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material; go.GetComponent<Collider>().isTrigger = trigger; return go;
        }
        private static void Create(bool tree)
        {
            var root = new GameObject(tree ? "GatheringTree" : "GatheringRock", typeof(GatheringNode));
            try
            {
                var node = root.GetComponent<GatheringNode>();
                node.WholeVisual = new GameObject("Whole"); node.WholeVisual.transform.SetParent(root.transform, false);
                node.DepletedVisual = new GameObject("Depleted"); node.DepletedVisual.transform.SetParent(root.transform, false);
                if (tree)
                {
                    var bark = Material("Oak Bark", new Color(0.49f, 0.32f, 0.19f)); var heart = Material("Oak Heart", new Color(0.79f, 0.60f, 0.37f));
                    var leaves = Material("Oak Leaves", new Color(0.37f, 0.59f, 0.30f)); var tips = Material("Oak Tips", new Color(0.49f, 0.69f, 0.36f));
                    Part(node.WholeVisual.transform, "Trunk", PrimitiveType.Cylinder, new Vector3(0, 1.05f, 0), new Vector3(0.42f, 1.05f, 0.42f), bark);
                    Part(node.WholeVisual.transform, "Crown", PrimitiveType.Sphere, new Vector3(0, 2.38f, 0), new Vector3(2.4f, 2.1f, 2.2f), leaves, true);
                    Part(node.WholeVisual.transform, "Crown Top", PrimitiveType.Sphere, new Vector3(0.2f, 3.2f, 0.1f), new Vector3(1.75f, 1.55f, 1.65f), tips, true);
                    Part(node.WholeVisual.transform, "Crown Side", PrimitiveType.Sphere, new Vector3(-0.65f, 2.25f, 0.25f), new Vector3(1.55f, 1.65f, 1.5f), tips, true);
                    Part(node.DepletedVisual.transform, "Stump", PrimitiveType.Cylinder, new Vector3(0, 0.17f, 0), new Vector3(0.55f, 0.17f, 0.55f), bark);
                    Part(node.DepletedVisual.transform, "Cut Surface", PrimitiveType.Cylinder, new Vector3(0, 0.344f, 0), new Vector3(0.47f, 0.006f, 0.47f), heart);
                }
                else
                {
                    var rock = Material("Rock Grey", new Color(0.54f, 0.62f, 0.64f)); var light = Material("Rock Light", new Color(0.68f, 0.73f, 0.71f));
                    Rock(node.WholeVisual.transform, "Large Stone", Vector3.zero, new Vector3(1.6f, 1.5f, 1.4f), rock);
                    Rock(node.WholeVisual.transform, "Side Stone", new Vector3(0.6f, 0, 0.25f), new Vector3(0.9f, 0.8f, 0.8f), light);
                    Rock(node.DepletedVisual.transform, "Fragments Left", new Vector3(-0.3f, 0, 0), new Vector3(0.42f, 0.24f, 0.4f), rock);
                    Rock(node.DepletedVisual.transform, "Fragments Right", new Vector3(0.3f, 0, 0.1f), new Vector3(0.5f, 0.3f, 0.4f), light);
                }
                node.DepletedVisual.SetActive(false); PrefabUtility.SaveAsPrefabAsset(root, Folder + "/" + root.name + ".prefab");
            }
            finally { Object.DestroyImmediate(root); }
        }
        private static void Rock(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider)); go.transform.SetParent(parent, false);
            go.transform.localPosition = position; go.transform.localScale = scale; var mesh = RockMesh();
            go.GetComponent<MeshFilter>().sharedMesh = mesh; go.GetComponent<MeshRenderer>().sharedMaterial = material; go.GetComponent<MeshCollider>().sharedMesh = mesh;
        }
        private static Mesh RockMesh()
        {
            const string path = "Assets/Art/Meshes/Gathering/Rock.asset"; var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (mesh != null) return mesh;
            var vertices = new[] { new Vector3(0.08f, 0.9f, 0.04f), new Vector3(-0.65f, 0.18f, 0), new Vector3(0, 0.15f, 0.7f),
                new Vector3(0.63f, 0.20f, 0), new Vector3(0, 0.16f, -0.68f), Vector3.zero };
            int[] indices = { 0, 1, 2, 0, 2, 3, 0, 3, 4, 0, 4, 1, 5, 2, 1, 5, 3, 2, 5, 4, 3, 5, 1, 4 };
            var flat = new Vector3[indices.Length]; var triangles = new int[indices.Length];
            for (int i = 0; i < indices.Length; i++) { flat[i] = vertices[indices[i]]; triangles[i] = i; }
            mesh = new Mesh { name = "Island Rock", vertices = flat, triangles = triangles }; mesh.RecalculateNormals(); mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh, path); return mesh;
        }
    }
}
