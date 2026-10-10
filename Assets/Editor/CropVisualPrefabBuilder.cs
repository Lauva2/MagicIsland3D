using System;
using System.Collections.Generic;
using System.IO;
using MagicIsland.Configuration;
using MagicIsland.Farming;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace MagicIsland.EditorTools
{
    public static class CropVisualPrefabBuilder
    {
        public const string Folder = "Assets/Art/Prefabs/Crops";
        private const string MeshFolder = "Assets/Art/Meshes/Crops";
        private const string MaterialFolder = "Assets/Art/Materials/Crops";

        public static void Ensure(CropCatalog catalog, AddressableAssetSettings settings)
        {
            Directory.CreateDirectory(Folder); Directory.CreateDirectory(MeshFolder); Directory.CreateDirectory(MaterialFolder);
            AssetDatabase.Refresh();
            Mesh bulb = MeshAsset("FacetedBulb", () => Ellipsoid(8, 4));
            Mesh leaf = MeshAsset("FacetedLeaf", () => Ellipsoid(6, 3));
            Mesh stem = MeshAsset("OctagonalStem", Stem);
            Material green = MaterialAsset("CropLeaves", new Color(0.3f, 0.6f, 0.22f));
            Material stalk = MaterialAsset("CropStems", new Color(0.33f, 0.47f, 0.18f));
            Material young = MaterialAsset("YoungFruit", new Color(0.53f, 0.68f, 0.29f));
            Material tassel = MaterialAsset("CornTassel", new Color(0.77f, 0.52f, 0.24f));
            AddressableAssetGroup group = settings.FindGroup("Crop Models") ?? settings.CreateGroup("Crop Models", false, false, true, null,
                typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            foreach (CropConfig crop in catalog.Crops)
            {
                string prefix = crop.Id == "turnip" ? "Turnip" : crop.Id == "tomato" ? "Tomato" : crop.Id == "corn" ? "Corn" : null;
                if (prefix == null) continue; // Additional crops supply their own configured models.
                Material fruit = MaterialAsset(prefix + "Fruit", crop.MatureColor);
                foreach (CropGrowthStage stage in Enum.GetValues(typeof(CropGrowthStage)))
                {
                    string name = prefix + stage;
                    string path = Folder + "/" + name + ".prefab";
                    if (!File.Exists(path))
                    {
                        var root = new GameObject(name);
                        try
                        {
                            Build(root.transform, crop.Id, stage, bulb, leaf, stem, green, stalk, fruit, young, tassel);
                            PrefabUtility.SaveAsPrefabAsset(root, path);
                        }
                        finally { UnityEngine.Object.DestroyImmediate(root); }
                    }
                    string guid = AssetDatabase.AssetPathToGUID(path);
                    var entry = settings.FindAssetEntry(guid) ?? settings.CreateOrMoveEntry(guid, group);
                    if (entry.address != name) entry.SetAddress(name);
                    if (!string.IsNullOrWhiteSpace(crop.PrefabAddress(stage))) continue;
                    if (stage == CropGrowthStage.Seedling) crop.SeedlingPrefabAddress = name;
                    else if (stage == CropGrowthStage.Growing) crop.GrowingPrefabAddress = name;
                    else crop.MaturePrefabAddress = name;
                    EditorUtility.SetDirty(catalog);
                }
            }
        }

        private static void Build(Transform root, string id, CropGrowthStage stage, Mesh bulb, Mesh leaf, Mesh stem,
            Material green, Material stalk, Material fruit, Material young, Material tassel)
        {
            if (stage == CropGrowthStage.Seedling)
            {
                Part(root, "Stem", stem, stalk, Vector3.zero, new Vector3(0.035f, 0.17f, 0.035f));
                for (int i = 0; i < (id == "tomato" ? 3 : 2); i++)
                    Part(root, "Seed leaf " + i, leaf, green, new Vector3(i == 0 ? -0.09f : 0.09f, 0.16f, 0),
                        new Vector3(id == "corn" ? 0.1f : id == "tomato" ? 0.12f : 0.15f, id == "turnip" ? 0.22f : 0.27f, 0.06f),
                        new Vector3(0, i == 2 ? 90 : 0, i == 0 ? 50 : -50));
                return;
            }
            bool mature = stage == CropGrowthStage.Mature;
            if (id == "turnip")
            {
                float diameter = mature ? 0.58f : 0.35f;
                float height = mature ? 0.43f : 0.26f;
                Part(root, "Root", bulb, fruit, Vector3.up * (height * 0.5f + 0.015f), new Vector3(diameter, height, diameter));
                for (int i = 0; i < 5; i++)
                {
                    float angle = i * 72 * Mathf.Deg2Rad;
                    Part(root, "Leaf " + i, leaf, green, new Vector3(Mathf.Sin(angle) * 0.11f, height + 0.12f, Mathf.Cos(angle) * 0.11f),
                        new Vector3(0.13f, mature ? 0.4f : 0.28f, 0.055f), new Vector3(25, i * 72, 0));
                }
            }
            else if (id == "tomato")
            {
                float height = mature ? 0.74f : 0.5f;
                Part(root, "Stem", stem, stalk, Vector3.zero, new Vector3(0.06f, height, 0.06f));
                for (int i = 0; i < 6; i++)
                {
                    float angle = i * 137 * Mathf.Deg2Rad;
                    Vector3 radial = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle));
                    Vector3 pos = radial * 0.19f + Vector3.up * (0.24f + (i % 3) * height * 0.21f);
                    Part(root, "Leaf " + i, leaf, green, pos, new Vector3(0.18f, 0.33f, 0.07f), new Vector3(45, i * 137, 0));
                }
                for (int i = 0; i < 4; i++)
                {
                    float angle = (i * 90 + 25) * Mathf.Deg2Rad;
                    Vector3 pos = new Vector3(Mathf.Sin(angle) * 0.23f, 0.28f + (i % 2) * 0.22f, Mathf.Cos(angle) * 0.23f);
                    float size = mature ? 0.24f : 0.13f;
                    Part(root, "Fruit " + i, bulb, mature ? fruit : young, pos, new Vector3(size, size * 0.85f, size));
                    Part(root, "Fruit cap " + i, leaf, green, pos + Vector3.up * size * 0.45f, new Vector3(size * 0.65f, 0.035f, size * 0.65f));
                }
            }
            else
            {
                float height = mature ? 1.05f : 0.66f;
                Part(root, "Stalk", stem, stalk, Vector3.zero, new Vector3(0.065f, height, 0.065f));
                for (int i = 0; i < (mature ? 6 : 4); i++)
                {
                    float direction = i % 2 == 0 ? -1 : 1;
                    Part(root, "Long leaf " + i, leaf, green, new Vector3(direction * 0.17f, 0.2f + i * 0.12f, i % 3 == 0 ? 0.08f : 0),
                        new Vector3(0.13f, mature ? 0.51f : 0.4f, 0.05f), new Vector3(0, i % 3 * 25, -direction * 55));
                }
                if (mature)
                {
                    for (int i = 0; i < 2; i++)
                    {
                        float side = i == 0 ? -1 : 1;
                        Part(root, "Golden cob " + i, bulb, fruit, new Vector3(side * 0.13f, 0.55f + i * 0.21f, 0.03f),
                            new Vector3(0.17f, 0.4f, 0.17f), new Vector3(0, 0, -side * 20));
                    }
                    for (int i = 0; i < 3; i++) Part(root, "Tassel " + i, leaf, tassel, new Vector3((i - 1) * 0.07f, height + 0.035f, 0),
                        new Vector3(0.035f, 0.18f, 0.035f), new Vector3(0, 0, (i - 1) * -30));
                }
            }
        }

        private static void Part(Transform parent, string name, Mesh mesh, Material material, Vector3 position, Vector3 scale, Vector3 angles = default)
        {
            var item = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); item.transform.SetParent(parent, false);
            item.transform.localPosition = position; item.transform.localScale = scale; item.transform.localRotation = Quaternion.Euler(angles);
            item.GetComponent<MeshFilter>().sharedMesh = mesh; item.GetComponent<MeshRenderer>().sharedMaterial = material;
        }
        private static Material MaterialAsset(string name, Color color)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Standard")) { name = name, color = color }; material.SetFloat("_Glossiness", 0);
            AssetDatabase.CreateAsset(material, path); return material;
        }
        private static Mesh MeshAsset(string name, Func<Mesh> create)
        {
            string path = MeshFolder + "/" + name + ".asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null) return mesh;
            mesh = create(); mesh.name = name; AssetDatabase.CreateAsset(mesh, path); return mesh;
        }
        private static Mesh Ellipsoid(int sides, int rings)
        {
            var vertices = new List<Vector3>();
            Func<int, int, Vector3> point = (ring, side) =>
            {
                float theta = Mathf.PI * ring / rings, phi = Mathf.PI * 2 * side / sides;
                return new Vector3(Mathf.Sin(theta) * Mathf.Cos(phi), Mathf.Cos(theta), Mathf.Sin(theta) * Mathf.Sin(phi)) * 0.5f;
            };
            for (int ring = 0; ring < rings; ring++) for (int side = 0; side < sides; side++)
            {
                Vector3 a = point(ring, side), b = point(ring, side + 1), c = point(ring + 1, side), d = point(ring + 1, side + 1);
                Triangle(vertices, a, b, c); Triangle(vertices, b, d, c);
            }
            return Finish(vertices);
        }
        private static Mesh Stem()
        {
            var vertices = new List<Vector3>();
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4, b = (i + 1) * Mathf.PI / 4;
                Vector3 p = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 0.5f, q = new Vector3(Mathf.Cos(b), 0, Mathf.Sin(b)) * 0.5f;
                Triangle(vertices, p, p + Vector3.up, q); Triangle(vertices, q, p + Vector3.up, q + Vector3.up);
                Triangle(vertices, Vector3.zero, p, q); Triangle(vertices, Vector3.up, q + Vector3.up, p + Vector3.up);
            }
            return Finish(vertices);
        }
        private static void Triangle(List<Vector3> vertices, Vector3 a, Vector3 b, Vector3 c)
        {
            if (Vector3.Cross(b - a, c - a).sqrMagnitude < 0.00000001f) return;
            vertices.Add(a); vertices.Add(b); vertices.Add(c);
        }
        private static Mesh Finish(List<Vector3> vertices)
        {
            var mesh = new Mesh(); mesh.SetVertices(vertices);
            var triangles = new int[vertices.Count]; for (int i = 0; i < triangles.Length; i++) triangles[i] = i;
            mesh.triangles = triangles; mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
    }
}
