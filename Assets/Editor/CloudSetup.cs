using System;
using System.Collections.Generic;
using System.IO;
using MagicIsland.WeatherSystem;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    public static class CloudSetup
    {
        public const string PrefabPath = "Assets/Art/Prefabs/Environment/IslandClouds.prefab";
        public const string MeshPath = "Assets/Art/Meshes/Weather/Cloud Cluster.asset";
        public const string MaterialPath = "Assets/Art/Materials/Weather/Sky Cloud.mat";
        public const string CookiePath = "Assets/Art/Textures/Weather/Cloud Shadows.png";
        public const int CloudCount = 24;
        private const int Seed = 31879;

        public static void PrepareValidation()
        {
            PrototypeAddressablesSetup.EnsureSetup();
            AssetDatabase.SaveAssets();
        }

        public static void Ensure(AddressableAssetSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            foreach (var folder in new[] { "Assets/Art/Prefabs/Environment", Path.GetDirectoryName(MeshPath), Path.GetDirectoryName(MaterialPath), Path.GetDirectoryName(CookiePath) })
                Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (mesh == null) { mesh = BuildMesh(); AssetDatabase.CreateAsset(mesh, MeshPath); }
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                var shader = Shader.Find("MagicIsland/Sky Cloud");
                if (shader == null) throw new Exception("Missing sky cloud shader");
                material = new Material(shader) { name = "Sky Cloud" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            if (!File.Exists(CookiePath)) CreateCookie();
            var cookie = AssetDatabase.LoadAssetAtPath<Texture2D>(CookiePath);
            if (!File.Exists(PrefabPath))
            {
                var root = new GameObject(CloudField.Address, typeof(CloudField)); root.layer = 2;
                try
                {
                    var field = root.GetComponent<CloudField>();
                    field.ShadowCookie = cookie;
                    field.Clouds = new Transform[CloudCount]; field.Renderers = new Renderer[CloudCount];
                    var random = new System.Random(Seed);
                    for (int i = 0; i < CloudCount; i++)
                    {
                        Vector2 p = Position(i, random); float radius = Radius(random);
                        var cloud = new GameObject("Cloud " + (i + 1).ToString("00"), typeof(MeshFilter), typeof(MeshRenderer));
                        cloud.layer = 2; cloud.transform.SetParent(root.transform, false);
                        cloud.transform.localPosition = new Vector3(p.x, field.Altitude + (i % 5) * 1.7f, p.y);
                        cloud.transform.localScale = new Vector3(radius, radius * .35f, radius * .66f);
                        cloud.transform.localRotation = Quaternion.Euler(0, i * 47, 0);
                        cloud.GetComponent<MeshFilter>().sharedMesh = mesh;
                        var renderer = cloud.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
                        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                        field.Clouds[i] = cloud.transform; field.Renderers[i] = renderer;
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                }
                finally { Object.DestroyImmediate(root); }
            }
            var group = settings.FindGroup("Environment") ?? settings.CreateGroup("Environment", false, false, true, null,
                typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            string guid = AssetDatabase.AssetPathToGUID(PrefabPath);
            var entry = settings.FindAssetEntry(guid) ?? settings.CreateOrMoveEntry(guid, group);
            if (entry.address != CloudField.Address) entry.SetAddress(CloudField.Address);
        }

        private static Vector2 Position(int i, System.Random random) => i == 0
            ? new Vector2(6, 3) : new Vector2((float)random.NextDouble() * 128 - 64, (float)random.NextDouble() * 128 - 64);
        private static float Radius(System.Random random) => 5.2f + (float)random.NextDouble() * 4.5f;

        private static void CreateCookie()
        {
            CloudShadowSetup.CreateMissingCookie();
        }

        private static Mesh BuildMesh()
        {
            var vertices = new List<Vector3>(); var normals = new List<Vector3>(); var triangles = new List<int>();
            Puff(vertices, normals, triangles, new Vector3(0, 0, 0), new Vector3(.80f, .70f, .65f));
            Puff(vertices, normals, triangles, new Vector3(-.72f, -.12f, 0), new Vector3(.65f, .48f, .50f));
            Puff(vertices, normals, triangles, new Vector3(.75f, -.18f, .05f), new Vector3(.68f, .48f, .50f));
            Puff(vertices, normals, triangles, new Vector3(-.27f, .25f, .1f), new Vector3(.55f, .65f, .54f));
            Puff(vertices, normals, triangles, new Vector3(.30f, .12f, -.25f), new Vector3(.62f, .56f, .58f));
            Puff(vertices, normals, triangles, new Vector3(-.1f, -.18f, .45f), new Vector3(.85f, .42f, .50f));
            var mesh = new Mesh { name = "Cloud Cluster" };
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
            return mesh;
        }

        private static void Puff(List<Vector3> vertices, List<Vector3> normals, List<int> triangles, Vector3 center, Vector3 size)
        {
            const int sides = 20, rings = 12; int first = vertices.Count;
            for (int j = 0; j <= rings; j++) for (int i = 0; i <= sides; i++)
            {
                float latitude = Mathf.PI * j / rings, longitude = 2 * Mathf.PI * i / sides;
                var n = new Vector3(Mathf.Sin(latitude) * Mathf.Cos(longitude), Mathf.Cos(latitude), Mathf.Sin(latitude) * Mathf.Sin(longitude));
                vertices.Add(center + Vector3.Scale(n, size));
                normals.Add(new Vector3(n.x / size.x, n.y / size.y, n.z / size.z).normalized);
            }
            for (int j = 0; j < rings; j++) for (int i = 0; i < sides; i++)
            {
                int a = first + j * (sides + 1) + i, b = a + sides + 1;
                triangles.Add(a); triangles.Add(a + 1); triangles.Add(b);
                triangles.Add(a + 1); triangles.Add(b + 1); triangles.Add(b);
            }
        }
    }
}
