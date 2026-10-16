using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    // Explicit art import only. Ordinary prototype setup never rebuilds this art.
    public static class PlayerCharacterArtSetup
    {
        public const string DataPath = "Assets/Art/Meshes/Player/IslandFarmer.meshdata.json";
        private const string PrefabPath = "Assets/Art/Prefabs/Characters/PlayerCharacter.prefab";
        private const string MeshFolder = "Assets/Art/Meshes/Player";
        private const string PalettePath = "Assets/Art/Textures/Player/IslandFarmerPalette.png";
        private const string MaterialPath = "Assets/Art/Materials/Player/IslandFarmer.mat";
        private const string ArtObject = "IslandFarmer Art";
        private const string MarkerPrefix = "IslandFarmerArt:1:";
        private static readonly string[] Joints =
            { "Bob", "Bob/Head", "Bob/LeftArm", "Bob/RightArm", "Bob/LeftLeg", "Bob/RightLeg" };

        [Serializable] private sealed class ModelData
        {
            public int version = 0;
            public string coordinateSpace = string.Empty;
            public MeshData[] parts = Array.Empty<MeshData>();
        }
        [Serializable] private sealed class MeshData
        {
            public string name = string.Empty, joint = string.Empty;
            public float[] positions = Array.Empty<float>(), normals = Array.Empty<float>(), uv = Array.Empty<float>();
            public int[] triangles = Array.Empty<int>();
        }

        [MenuItem("Magic Island/Art/Apply Blender Player Art")]
        public static void Apply() => Import(false);

        [MenuItem("Magic Island/Art/Rebuild Blender Player Art (Explicit)")]
        public static void Rebuild() => Import(true);

        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static MeshData[] ReadData(string json)
        {
            var data = JsonUtility.FromJson<ModelData>(json);
            Require(data != null && data.version == 1 && data.coordinateSpace == "Unity local metres"
                && data.parts != null && data.parts.Length == Joints.Length, "Invalid Island Farmer mesh data.");
            Require(data.parts.All(p => p != null)
                && data.parts.Select(p => p.joint).OrderBy(s => s).SequenceEqual(Joints.OrderBy(s => s))
                && data.parts.Select(p => p.name).Distinct().Count() == data.parts.Length, "Duplicate or missing player art parts.");
            foreach (var part in data.parts)
            {
                Require(!string.IsNullOrEmpty(part.name) && part.name.All(c => char.IsLetterOrDigit(c)), "Invalid player mesh asset name.");
                Require(part.positions != null && part.positions.Length >= 9 && part.positions.Length % 3 == 0
                    && part.positions.Length / 3 < 65536 && part.positions.All(Finite), "Invalid player vertices: " + part.name);
                int count = part.positions.Length / 3;
                Require(part.normals != null && part.normals.Length == count * 3 && part.normals.All(Finite)
                    && part.uv != null && part.uv.Length == count * 2 && part.uv.All(Finite), "Invalid player normals/UVs: " + part.name);
                Require(part.triangles != null && part.triangles.Length >= 3 && part.triangles.Length % 3 == 0
                    && part.triangles.All(i => i >= 0 && i < count), "Invalid player triangles: " + part.name);
                for (int i = 0; i < count; i++)
                {
                    var normal = new Vector3(part.normals[i * 3], part.normals[i * 3 + 1], part.normals[i * 3 + 2]);
                    Require(Mathf.Abs(normal.sqrMagnitude - 1) < .01f, "Player normal is not unit length: " + part.name);
                }
            }
            return data.parts;
        }

        private static Mesh MakeMesh(MeshData part)
        {
            int count = part.positions.Length / 3;
            var positions = new Vector3[count];
            var normals = new Vector3[count];
            var tangents = new Vector4[count];
            var uv = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                positions[i] = new Vector3(part.positions[i * 3], part.positions[i * 3 + 1], part.positions[i * 3 + 2]);
                normals[i] = new Vector3(part.normals[i * 3], part.normals[i * 3 + 1], part.normals[i * 3 + 2]);
                var axis = Mathf.Abs(normals[i].y) > .9f ? Vector3.right : Vector3.up;
                Vector3 tangent = Vector3.Cross(axis, normals[i]).normalized;
                tangents[i] = new Vector4(tangent.x, tangent.y, tangent.z, 1);
                uv[i] = new Vector2(part.uv[i * 2], part.uv[i * 2 + 1]);
            }
            var mesh = new Mesh { name = part.name, indexFormat = IndexFormat.UInt16 };
            mesh.vertices = positions; mesh.normals = normals; mesh.tangents = tangents;
            mesh.uv = uv; mesh.triangles = part.triangles; mesh.RecalculateBounds();
            return mesh;
        }

        private static Material GetMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (existing != null) return existing;
            var palette = AssetDatabase.LoadAssetAtPath<Texture2D>(PalettePath);
            Require(palette != null, "Missing Island Farmer palette.");
            var importer = (TextureImporter)AssetImporter.GetAtPath(PalettePath);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true; importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point; importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.SaveAndReimport();
            var shader = Shader.Find("Standard");
            Require(shader != null, "The built-in Standard shader is required.");
            var material = new Material(shader) { name = "IslandFarmer", color = Color.white,
                mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(PalettePath) };
            material.SetFloat("_Glossiness", .08f); material.SetFloat("_Metallic", 0);
            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }

        private static void Import(bool rebuild)
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling,
                "Import player art in Edit Mode after compilation.");
            string json = File.ReadAllText(DataPath, Encoding.UTF8);
            MeshData[] parts = ReadData(json);
            string marker;
            using (var hash = SHA256.Create())
                marker = MarkerPrefix + BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(json))).Replace("-", "").ToLowerInvariant();
            AssetDatabase.ImportAsset(DataPath, ImportAssetOptions.ForceSynchronousImport);
            var dataImporter = AssetImporter.GetAtPath(DataPath);
            Require(dataImporter != null, "Could not import the player art data.");
            if (dataImporter.userData.StartsWith(MarkerPrefix, StringComparison.Ordinal) && !rebuild)
            {
                Require(dataImporter.userData == marker,
                    "The Blender source has changed. Use Rebuild Blender Player Art (Explicit) to replace the previously imported geometry.");
                Debug.Log("Island Farmer art already applied; artist edits preserved.");
                return;
            }
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            Mesh[] prepared = null;
            try
            {
                var animator = root.GetComponentInChildren<Animator>(true);
                Require(animator != null && animator.runtimeAnimatorController != null, "Missing original player Animator.");
                Transform model = animator.transform;
                Require(Joints.All(path => model.Find(path) != null) && model.Find("Bob/RightArm/ToolSocket") != null,
                    "The original player joints and hand socket must be preserved.");
                Require(root.GetComponentsInChildren<Collider>(true).Length == 1 && root.GetComponent<CharacterController>() != null,
                    "The player must retain its single root CharacterController.");
                Require(rebuild || parts.All(p => !File.Exists(MeshFolder + "/" + p.name + ".asset")),
                    "Existing player meshes would be overwritten; use Rebuild Blender Player Art (Explicit).");
                prepared = parts.Select(MakeMesh).ToArray();
                Material material = GetMaterial();
                // Keep the original primitives for reference; only their renderers are disabled.
                foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
                for (int i = 0; i < parts.Length; i++)
                {
                    string path = MeshFolder + "/" + parts[i].name + ".asset";
                    var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if (mesh == null)
                    {
                        AssetDatabase.CreateAsset(prepared[i], path);
                        mesh = prepared[i]; prepared[i] = null;
                    }
                    else
                    {
                        Require(rebuild, "Existing player mesh would be overwritten; use the explicit rebuild command: " + path);
                        EditorUtility.CopySerialized(prepared[i], mesh); EditorUtility.SetDirty(mesh);
                    }
                    Transform joint = model.Find(parts[i].joint);
                    Transform child = joint.Find(ArtObject);
                    var go = child != null ? child.gameObject : new GameObject(ArtObject);
                    go.layer = 2; go.transform.SetParent(joint, false);
                    go.transform.localPosition = Vector3.zero;
                    go.transform.localRotation = Quaternion.identity;
                    go.transform.localScale = Vector3.one;
                    var filter = go.GetComponent<MeshFilter>();
                    if (filter == null) filter = go.AddComponent<MeshFilter>();
                    var renderer = go.GetComponent<MeshRenderer>();
                    if (renderer == null) renderer = go.AddComponent<MeshRenderer>();
                    filter.sharedMesh = mesh; renderer.sharedMaterial = material; renderer.enabled = true;
                    renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
                }
                Require(model.GetComponentsInChildren<Renderer>(true).Count(r => r.enabled) == Joints.Length,
                    "Player art import must enable exactly the six joint meshes.");
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool success);
                Require(success, "Could not save the player prefab.");
                AssetDatabase.SaveAssets();
                dataImporter.userData = marker; dataImporter.SaveAndReimport();
                Debug.Log("Applied Island Farmer art: six meshes, one shared palette material; original controller, clips, joints, socket and collider preserved. Build Addressables and run PlayerAnimationFlowVerification before acceptance.");
            }
            finally
            {
                if (prepared != null) foreach (var mesh in prepared) if (mesh != null) Object.DestroyImmediate(mesh);
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
