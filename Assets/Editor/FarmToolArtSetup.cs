using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using MagicIsland.Tools;
using Object = UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    public static class FarmToolArtSetup
    {
        public const string DataPath = "Assets/Art/Meshes/Tools/FarmTools.meshdata.json";
        public const string PrefabPath = "Assets/Art/Prefabs/Tools/IslandFarmTools.prefab";
        public const string MaterialPath = "Assets/Art/Materials/Tools/FarmTools.mat";
        private const string PalettePath = "Assets/Art/Textures/Tools/FarmToolsPalette.png";
        private const string Marker = "FarmToolsArt:1:";
        [Serializable] private sealed class Data { public int version = 0; public string coordinateSpace = ""; public Part[] parts = Array.Empty<Part>(); }
        [Serializable] private sealed class Part
        {
            public string name = "", joint = "";
            public float[] positions = Array.Empty<float>(), normals = Array.Empty<float>(), uv = Array.Empty<float>();
            public int[] triangles = Array.Empty<int>();
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        [MenuItem("Magic Island/Art/Apply Blender Farm Tools")]
        public static void Apply() => Import(false);
        [MenuItem("Magic Island/Art/Rebuild Blender Farm Tools (Explicit)")]
        public static void Rebuild() => Import(true);

        // Ordinary setup only fills a missing reference. It never rebuilds art or replaces a legal override.
        public static void EnsureBinding()
        {
            var tools = AssetDatabase.LoadAssetAtPath<FarmToolModels>(PrefabPath);
            var effects = AssetDatabase.LoadAssetAtPath<GameObject>(FarmArtSetup.EffectPath);
            if (tools == null || effects == null || effects.GetComponent<FarmActionEffects>().HeldToolsPrefab != null) return;
            var root = PrefabUtility.LoadPrefabContents(FarmArtSetup.EffectPath);
            try
            {
                root.GetComponent<FarmActionEffects>().HeldToolsPrefab = tools;
                PrefabUtility.SaveAsPrefabAsset(root, FarmArtSetup.EffectPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static Mesh Mesh(Part part)
        {
            int count = part.positions.Length / 3;
            Require(count > 2 && count < 65536 && part.positions.Length == count * 3 && part.positions.All(Finite)
                && part.normals.Length == count * 3 && part.normals.All(Finite) && part.uv.Length == count * 2
                && part.uv.All(v => Finite(v) && v >= 0 && v <= 1) && part.triangles.Length > 2
                && part.triangles.Length % 3 == 0 && part.triangles.All(i => i >= 0 && i < count), "Invalid farm tool geometry.");
            var vertices = new Vector3[count]; var normals = new Vector3[count]; var uv = new Vector2[count]; var tangents = new Vector4[count];
            for (int i = 0; i < count; i++)
            {
                vertices[i] = new Vector3(part.positions[i * 3], part.positions[i * 3 + 1], part.positions[i * 3 + 2]);
                normals[i] = new Vector3(part.normals[i * 3], part.normals[i * 3 + 1], part.normals[i * 3 + 2]);
                Require(Mathf.Abs(normals[i].sqrMagnitude - 1) < .01f, "Non-unit farm tool normal.");
                uv[i] = new Vector2(part.uv[i * 2], part.uv[i * 2 + 1]);
                Vector3 tangent = Vector3.Cross(Mathf.Abs(normals[i].y) > .9f ? Vector3.right : Vector3.up, normals[i]).normalized;
                tangents[i] = new Vector4(tangent.x, tangent.y, tangent.z, 1);
            }
            var mesh = new Mesh { name = part.name, vertices = vertices, normals = normals, uv = uv, tangents = tangents, triangles = part.triangles };
            mesh.RecalculateBounds(); return mesh;
        }

        private static Material Material()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material != null && material.mainTexture != null) return material;
            var importer = AssetImporter.GetAtPath(PalettePath) as TextureImporter;
            Require(importer != null, "Missing farm tool palette.");
            importer.textureType = TextureImporterType.Default; importer.textureShape = TextureImporterShape.Texture2D;
            importer.sRGBTexture = true; importer.mipmapEnabled = false; importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaSource = TextureImporterAlphaSource.None; importer.SaveAndReimport();
            var palette = AssetDatabase.LoadAssetAtPath<Texture2D>(PalettePath);
            Require(palette != null, "Farm tool palette must import as a 2D texture.");
            if (material != null)
            { material.mainTexture = palette; EditorUtility.SetDirty(material); return material; }
            var shader = Shader.Find("Standard"); Require(shader != null, "The built-in Standard shader is required.");
            material = new Material(shader) { name = "Farm Tools", color = Color.white, mainTexture = palette };
            material.SetFloat("_Glossiness", .18f); AssetDatabase.CreateAsset(material, MaterialPath); return material;
        }

        private static void Import(bool rebuild)
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling, "Apply farm tools in Edit Mode after compilation.");
            var data = JsonUtility.FromJson<Data>(File.ReadAllText(DataPath));
            Require(data != null && data.version == 1 && data.coordinateSpace == "Unity local metres" && data.parts != null && data.parts.Length == 4
                && data.parts.All(p => p != null && p.positions != null && p.normals != null && p.uv != null && p.triangles != null)
                && data.parts.Select(p => p.name).OrderBy(n => n).SequenceEqual(new[] { "IslandAxe", "IslandHoe", "IslandPickaxe", "IslandWateringCan" })
                && data.parts.All(p => p.joint == (p.name == "IslandWateringCan" ? "Water" : p.name.Substring("Island".Length))), "Invalid farm tool source contract.");
            string marker;
            using (var hash = SHA256.Create()) marker = Marker + BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(DataPath))).Replace("-", "").ToLowerInvariant();
            AssetDatabase.ImportAsset(DataPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(DataPath);
            if (!rebuild && importer.userData.StartsWith(Marker, StringComparison.Ordinal))
            {
                Require(importer.userData == marker, "Farm tool source changed; use Rebuild Blender Farm Tools (Explicit).");
                EnsureBinding(); Debug.Log("Farm tools already applied; artist edits preserved."); return;
            }
            Directory.CreateDirectory("Assets/Art/Prefabs/Tools"); Directory.CreateDirectory("Assets/Art/Materials/Tools"); AssetDatabase.Refresh();
            Require(rebuild || (!File.Exists(PrefabPath) && data.parts.All(p => !File.Exists("Assets/Art/Meshes/Tools/" + p.name + ".asset"))),
                "Existing farm tool assets require explicit Rebuild.");
            var prepared = new Mesh[data.parts.Length]; GameObject root = null; bool loaded = false;
            try
            {
                for (int i = 0; i < data.parts.Length; i++) prepared[i] = Mesh(data.parts[i]);
                var material = Material();
                loaded = File.Exists(PrefabPath); root = loaded ? PrefabUtility.LoadPrefabContents(PrefabPath) : new GameObject("IslandFarmTools"); root.layer = 2;
                var models = root.GetComponent<FarmToolModels>(); if (models == null) models = root.AddComponent<FarmToolModels>();
                for (int i = 0; i < data.parts.Length; i++)
                {
                    var part = data.parts[i]; string meshPath = "Assets/Art/Meshes/Tools/" + part.name + ".asset";
                    var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                    if (mesh == null) { AssetDatabase.CreateAsset(prepared[i], meshPath); mesh = prepared[i]; prepared[i] = null; }
                    else { EditorUtility.CopySerialized(prepared[i], mesh); EditorUtility.SetDirty(mesh); }
                    Transform tool = part.joint == "Hoe" ? models.Hoe : part.joint == "Water" ? models.WateringCan
                        : part.joint == "Axe" ? models.Axe : models.Pickaxe;
                    bool created = tool == null;
                    if (tool == null)
                    {
                        tool = new GameObject(part.joint, typeof(MeshFilter), typeof(MeshRenderer)).transform;
                        tool.SetParent(root.transform, false); tool.gameObject.layer = 2;
                    }
                    var filter = tool.GetComponent<MeshFilter>(); if (filter == null) filter = tool.gameObject.AddComponent<MeshFilter>();
                    var renderer = tool.GetComponent<MeshRenderer>(); if (renderer == null) renderer = tool.gameObject.AddComponent<MeshRenderer>();
                    filter.sharedMesh = mesh;
                    if (renderer.sharedMaterial == null) renderer.sharedMaterial = material;
                    if (part.joint == "Hoe") models.Hoe = tool;
                    else if (part.joint == "Water") models.WateringCan = tool;
                    else if (part.joint == "Axe") models.Axe = tool;
                    else models.Pickaxe = tool;
                    if (!loaded || created) tool.gameObject.SetActive(false);
                }
                Require(models.IsValid, "Farm tools must retain four independent mesh roots and no colliders.");
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool saved); Require(saved, "Could not save farm tool prefab.");
                AssetDatabase.SaveAssets(); EnsureBinding();
                importer.userData = marker; importer.SaveAndReimport();
                Debug.Log("Applied Blender farm tools; models load with the FarmActionEffects AA dependency.");
            }
            finally
            {
                foreach (var mesh in prepared) if (mesh != null) Object.DestroyImmediate(mesh);
                if (root != null) { if (loaded) PrefabUtility.UnloadPrefabContents(root); else Object.DestroyImmediate(root); }
            }
        }
    }
}
