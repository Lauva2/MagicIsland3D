using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MagicIsland.CameraSystem;
using MagicIsland.Gathering;
using MagicIsland.Interaction;
using MagicIsland.Tools;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    public static class GatheringArtSetup
    {
        public const string MaterialFolder = "Assets/Art/Materials/Gathering", MeshFolder = "Assets/Art/Meshes/Gathering";
        public const int Version = 1;
        static Material bark, heart, leaves, tips, moss, stone, light, dark;
        public static void Ensure()
        {
            Directory.CreateDirectory(MaterialFolder); Directory.CreateDirectory(MeshFolder); AssetDatabase.Refresh();
            bark = Mat("Oak Bark", new Color(.49f, .32f, .19f)); heart = Mat("Oak Heart", new Color(.79f, .60f, .37f));
            leaves = Mat("Oak Leaves", new Color(.37f, .59f, .30f)); tips = Mat("Oak Tips", new Color(.49f, .69f, .36f));
            stone = Mat("Rock Grey", new Color(.54f, .62f, .64f)); light = Mat("Rock Light", new Color(.68f, .73f, .71f));
            dark = Mat("Stone Seams", new Color(.34f, .43f, .43f)); moss = Mat("Forest Moss", new Color(.41f, .54f, .28f));
            Upgrade(true); Upgrade(false); UpgradeEffects();
        }
        static Material Mat(string name, Color color)
        {
            string path = MaterialFolder + "/" + name + ".mat"; var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Standard")) { name = name, color = color }; material.SetFloat("_Glossiness", .06f); AssetDatabase.CreateAsset(material, path); return material;
        }
        static Transform Group(Transform parent, string name)
        { var root = new GameObject(name); root.layer = 2; root.transform.SetParent(parent, false); return root.transform; }
        static Transform Primitive(Transform parent, string name, PrimitiveType type, Vector3 point, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(type); part.name = name; part.layer = 2; part.transform.SetParent(parent, false);
            part.transform.localPosition = point; part.transform.localScale = scale; Object.DestroyImmediate(part.GetComponent<Collider>());
            part.GetComponent<Renderer>().sharedMaterial = material; return part.transform;
        }
        static Transform Part(Transform parent, string name, Mesh mesh, Vector3 point, Vector3 scale, Material material)
        {
            var part = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); part.layer = 2; part.transform.SetParent(parent, false);
            part.transform.localPosition = point; part.transform.localScale = scale; part.GetComponent<MeshFilter>().sharedMesh = mesh; part.GetComponent<Renderer>().sharedMaterial = material; return part.transform;
        }
        static Mesh MeshAsset(string name, Func<Mesh> create)
        {
            string path = MeshFolder + "/" + name + ".asset"; var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (mesh != null) return mesh;
            mesh = create(); mesh.name = name; AssetDatabase.CreateAsset(mesh, path); return mesh;
        }
        static void Upgrade(bool tree)
        {
            string path = "Assets/Art/Prefabs/Gathering/" + (tree ? "GatheringTree" : "GatheringRock") + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<GatheringArtVisual>()?.ArtVersion >= Version) return;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var node = root.GetComponent<GatheringNode>();
                // Keep the original colliders stationary and intact; only the new visual hierarchy reacts to hits.
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
                var art = root.GetComponent<GatheringArtVisual>() ?? root.AddComponent<GatheringArtVisual>();
                art.WholeMotion = Group(node.WholeVisual.transform, "Resource art"); art.DepletedMotion = Group(node.DepletedVisual.transform, "Depleted art");
                art.HitAngle = tree ? 3 : 1.7f;
                if (tree) Tree(art.WholeMotion, art.DepletedMotion); else Rock(art.WholeMotion, art.DepletedMotion);
                var occluder = node.WholeVisual.GetComponent<CameraOccluder>() ?? node.WholeVisual.AddComponent<CameraOccluder>();
                occluder.Renderers = art.WholeMotion.GetComponentsInChildren<Renderer>();
                art.ArtVersion = Version; PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        static void Tree(Transform whole, Transform depleted)
        {
            Mesh canopy = MeshAsset("Soft Oak Canopy", () => Rounded(false));
            Primitive(whole, "Oak trunk", PrimitiveType.Cylinder, new Vector3(0, 1.05f, 0), new Vector3(.42f, 1.05f, .42f), bark);
            for (int i = 0; i < 6; i++)
            {
                float angle = i * Mathf.PI / 3;
                var strip = Primitive(whole, "Bark groove", PrimitiveType.Cube, new Vector3(Mathf.Sin(angle) * .211f, .88f, Mathf.Cos(angle) * .211f), new Vector3(.016f, 1.34f, .012f), heart);
                strip.localRotation = Quaternion.Euler(0, i * 60, 0);
            }
            for (int i = 0; i < 4; i++)
            {
                float angle = i * Mathf.PI / 2;
                Vector3 radial = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle));
                Segment(whole, "Oak branch", radial * .05f + Vector3.up * 1.35f, radial * .53f + Vector3.up * 2.12f, .075f, bark);
                Segment(whole, "Root flare", Vector3.up * .23f, radial * .31f + Vector3.up * .035f, .075f, bark);
                Segment(depleted, "Stump root", Vector3.up * .17f, radial * .31f + Vector3.up * .035f, .066f, bark);
            }
            Part(whole, "Oak crown", canopy, new Vector3(0, 2.40f, 0), new Vector3(2.32f, 2, 2.12f), leaves);
            Part(whole, "Sunlit crown", canopy, new Vector3(.20f, 3.16f, .10f), new Vector3(1.67f, 1.45f, 1.57f), tips);
            Part(whole, "Left crown", canopy, new Vector3(-.63f, 2.25f, .25f), new Vector3(1.48f, 1.52f, 1.43f), tips);
            Part(whole, "Front leaf cluster", canopy, new Vector3(.59f, 2.03f, -.49f), new Vector3(.87f, .81f, .81f), leaves);
            for (int i = 0; i < 5; i++)
                Part(whole, "Young foliage", canopy, new Vector3(-.60f + i * .28f, 2.95f + (i % 2) * .18f, -.46f), Vector3.one * .38f, i % 2 == 0 ? tips : leaves);
            Primitive(depleted, "Oak stump", PrimitiveType.Cylinder, new Vector3(0, .17f, 0), new Vector3(.55f, .17f, .55f), bark);
            Primitive(depleted, "Fresh cut surface", PrimitiveType.Cylinder, new Vector3(0, .344f, 0), new Vector3(.47f, .006f, .47f), heart);
            Part(depleted, "Stump growth rings", MeshAsset("Stump Rings", Rings), new Vector3(0, .353f, 0), Vector3.one, bark);
            for (int i = 0; i < 3; i++)
            {
                float angle = i * 120;
                var crack = Primitive(depleted, "Cut crack", PrimitiveType.Cube, Quaternion.Euler(0, angle, 0) * new Vector3(0, .354f, .17f), new Vector3(.009f, .003f, .11f), bark);
                crack.localRotation = Quaternion.Euler(0, angle, 0);
            }
            Primitive(depleted, "Moss on stump", PrimitiveType.Sphere, new Vector3(.19f, .045f, -.14f), new Vector3(.19f, .06f, .11f), moss);
        }
        static void Rock(Transform whole, Transform depleted)
        {
            Mesh boulder = MeshAsset("Faceted Boulder", () => Rounded(true));
            Part(whole, "Main boulder", boulder, Vector3.zero, new Vector3(1.65f, 1.47f, 1.45f), stone);
            var side = Part(whole, "Side boulder", boulder, new Vector3(.60f, 0, .25f), new Vector3(.95f, .82f, .83f), light); side.localRotation = Quaternion.Euler(0, 31, 0);
            for (int i = 0; i < 3; i++)
            {
                var seam = Primitive(whole, "Mineral seam", PrimitiveType.Cube, new Vector3(-.20f + i * .18f, .34f + i * .15f, -.71f + i * .10f), new Vector3(.29f, .025f, .028f), dark);
                seam.localRotation = Quaternion.Euler(0, 0, -18);
            }
            Part(whole, "Rock moss patch", boulder, new Vector3(-.56f, .01f, .16f), new Vector3(.44f, .15f, .37f), moss);
            for (int i = 0; i < 6; i++)
            {
                float angle = i * 2.39996f;
                var fragment = Part(depleted, "Broken stone", boulder, new Vector3(Mathf.Cos(angle) * .36f, 0, Mathf.Sin(angle) * .24f),
                    new Vector3(.28f + (i % 2) * .13f, .18f + (i % 3) * .08f, .25f + (i % 2) * .09f), i % 2 == 0 ? stone : light);
                fragment.localRotation = Quaternion.Euler(0, i * 47, 0);
            }
        }
        static void Segment(Transform parent, string name, Vector3 a, Vector3 b, float width, Material material)
        {
            var part = Primitive(parent, name, PrimitiveType.Cylinder, (a + b) * .5f, new Vector3(width, Vector3.Distance(a, b) * .5f, width), material);
            part.localRotation = Quaternion.FromToRotation(Vector3.up, b - a);
        }
        static Mesh Rounded(bool rock)
        {
            var vertices = new List<Vector3>(); const int sides = 11;
            float[] heights = rock ? new[] { .01f, .24f, .57f, .81f, .89f } : new[] { -.5f, -.32f, -.07f, .20f, .42f, .53f };
            float[] radii = rock ? new[] { .47f, .63f, .50f, .27f, .025f } : new[] { .035f, .36f, .50f, .47f, .30f, .025f };
            Func<int, int, Vector3> point = (ring, side) =>
            {
                float angle = side * Mathf.PI * 2 / sides;
                float radius = radii[ring] * (1 + Mathf.Sin(side * 2.3f + ring * .9f) * (rock ? .11f : .045f));
                return new Vector3(Mathf.Cos(angle) * radius + (rock ? ring * .015f : 0), heights[ring], Mathf.Sin(angle) * radius);
            };
            for (int ring = 0; ring < heights.Length - 1; ring++) for (int side = 0; side < sides; side++)
            { var a = point(ring, side); var b = point(ring + 1, side); var c = point(ring, side + 1); var d = point(ring + 1, side + 1); Triangle(vertices, a, b, c); Triangle(vertices, c, b, d); }
            for (int side = 0; side < sides; side++)
            { Triangle(vertices, new Vector3(0, heights[0], 0), point(0, side), point(0, side + 1)); Triangle(vertices, new Vector3(0, heights[heights.Length - 1], 0), point(heights.Length - 1, side + 1), point(heights.Length - 1, side)); }
            return Finish(vertices);
        }
        static Mesh Rings()
        {
            var vertices = new List<Vector3>();
            foreach (float radius in new[] { .073f, .143f, .205f }) for (int i = 0; i < 28; i++)
            {
                float a = i * Mathf.PI * 2 / 28, b = (i + 1) * Mathf.PI * 2 / 28;
                Vector3 p = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)), q = new Vector3(Mathf.Cos(b), 0, Mathf.Sin(b));
                Triangle(vertices, p * radius, q * radius, p * (radius + .006f)); Triangle(vertices, p * (radius + .006f), q * radius, q * (radius + .006f));
            }
            return Finish(vertices);
        }
        static void Triangle(List<Vector3> vertices, Vector3 a, Vector3 b, Vector3 c) { vertices.Add(a); vertices.Add(b); vertices.Add(c); }
        static Mesh Finish(List<Vector3> vertices)
        { var mesh = new Mesh(); mesh.SetVertices(vertices); mesh.SetTriangles(Enumerable.Range(0, vertices.Count).ToArray(), 0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh; }
        static void UpgradeEffects()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FarmArtSetup.EffectPath); var before = prefab.GetComponent<FarmActionEffects>();
            if (before.WoodChip != null && before.StoneChip != null) return;
            var root = PrefabUtility.LoadPrefabContents(FarmArtSetup.EffectPath);
            try
            {
                var effects = root.GetComponent<FarmActionEffects>();
                if (effects.WoodChip == null) effects.WoodChip = Primitive(root.transform, "Wood chip", PrimitiveType.Cube, Vector3.zero, new Vector3(.07f, .025f, .13f), heart);
                if (effects.StoneChip == null) effects.StoneChip = Part(root.transform, "Stone chip", MeshAsset("Faceted Boulder", () => Rounded(true)), Vector3.zero, Vector3.one * .13f, light);
                foreach (var template in new[] { effects.WoodChip, effects.StoneChip })
                {
                    foreach (var renderer in template.GetComponentsInChildren<Renderer>()) { renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false; }
                    template.gameObject.SetActive(false);
                }
                PrefabUtility.SaveAsPrefabAsset(root, FarmArtSetup.EffectPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
