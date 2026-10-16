using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MagicIsland.Configuration;
using MagicIsland.Farming;
using MagicIsland.Tools;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    public static class FarmArtSetup
    {
        public const string PlotPath = "Assets/Art/Prefabs/Crops/FarmPlot.prefab";
        public const string EffectPath = "Assets/Art/Prefabs/Effects/FarmActionEffects.prefab";
        public const string MaterialFolder = "Assets/Art/Materials/Farm", MeshFolder = "Assets/Art/Meshes/Farm";
        public static void Ensure(AddressableAssetSettings settings, GameConfig config)
        {
            Directory.CreateDirectory(MaterialFolder); Directory.CreateDirectory(MeshFolder); AssetDatabase.Refresh();
            CreatePlot(); CreateEffects();
            Register(settings, PlotPath, "FarmPlot", "Crop Models"); Register(settings, EffectPath, "FarmActionEffects", "Effects");
            string source = File.ReadAllText("Assets/Config/GameConfig.asset");
            if (!source.Contains("FarmPlotPrefabAddress:") || !source.Contains("ToolEffectsPrefabAddress:")) EditorUtility.SetDirty(config);
        }
        static void Register(AddressableAssetSettings settings, string path, string address, string groupName)
        {
            var group = settings.FindGroup(groupName) ?? settings.CreateGroup(groupName, false, false, true, null,
                typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            var guid = AssetDatabase.AssetPathToGUID(path); var entry = settings.FindAssetEntry(guid) ?? settings.CreateOrMoveEntry(guid, group);
            if (entry.address != address) entry.SetAddress(address);
        }
        static Material Mat(string name, Color color, float gloss = .04f)
        {
            string path = MaterialFolder + "/" + name + ".mat"; var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            mat = new Material(Shader.Find(name=="Plot Grass"||name=="Grass Tips"?"MagicIsland/Seasonal Grass":"Standard")) { name = name, color = color }; mat.SetFloat("_Glossiness", gloss); AssetDatabase.CreateAsset(mat, path); return mat;
        }
        static Transform Group(Transform parent, string name)
        { var group = new GameObject(name); group.layer = 2; group.transform.SetParent(parent, false); return group.transform; }
        static Transform Part(Transform parent, string name, PrimitiveType type, Vector3 point, Vector3 scale, Material mat)
        {
            var part = GameObject.CreatePrimitive(type); part.name = name; part.layer = 2; part.transform.SetParent(parent, false);
            part.transform.localPosition = point; part.transform.localScale = scale; Object.DestroyImmediate(part.GetComponent<Collider>());
            part.GetComponent<Renderer>().sharedMaterial = mat; return part.transform;
        }
        static void CreatePlot()
        {
            if (File.Exists(PlotPath)) return;
            var root = new GameObject("FarmPlot", typeof(FarmPlotVisual)); root.layer = 2;
            try
            {
                var view = root.GetComponent<FarmPlotVisual>();
                view.DrySoil = Mat("Dry Soil", new Color(.49f, .33f, .20f)); view.WetSoil = Mat("Wet Soil", new Color(.27f, .20f, .14f), .20f);
                view.FertilizedDrySoil = Mat("Rich Dry Soil", new Color(.47f, .32f, .19f)); view.FertilizedWetSoil = Mat("Rich Wet Soil", new Color(.25f, .19f, .13f), .16f);
                var grass = Group(root.transform, "Grass cover"); view.GrassCover = grass.gameObject;
                var moss = Mat("Plot Grass", new Color(.52f, .66f, .32f)); var blade = Mat("Grass Tips", new Color(.61f, .72f, .39f));
                Bed(grass, "Soft grass patch", moss);
                for (int i = 0; i < 5; i++)
                {
                    float x = -.42f + i * .21f, z = (i % 2 == 0 ? -.40f : .39f);
                    var tuft = Part(grass, "Grass tuft", PrimitiveType.Cube, new Vector3(x, .022f, z), new Vector3(.018f, .065f, .028f), blade);
                    tuft.localRotation = Quaternion.Euler(0, i * 37, 18);
                }
                var soil = Group(root.transform, "Soil bed"); view.SoilBed = soil.gameObject; Bed(soil, "Rounded soil edge", view.DrySoil);
                for (int i = 0; i < 5; i++)
                    Part(soil, "Raised earth ridge", PrimitiveType.Sphere, new Vector3(0, .01f, -.40f + i * .20f), new Vector3(1.01f, .075f, .13f), view.DrySoil);
                view.SoilRenderers = soil.GetComponentsInChildren<Renderer>(); soil.gameObject.SetActive(false);
                var grains = Group(root.transform, "Fertilizer grains"); view.FertilizerGrains = grains.gameObject;
                var grain = Mat("Compost Grains", new Color(.75f, .60f, .36f));
                for (int i = 0; i < 12; i++)
                    Part(grains, "Compost grain", PrimitiveType.Sphere, new Vector3(-.43f + i % 4 * .28f, .041f, -.36f + i / 4 * .35f), new Vector3(.025f, .016f, .033f), grain);
                grains.gameObject.SetActive(false); PrefabUtility.SaveAsPrefabAsset(root, PlotPath);
            }
            finally { Object.DestroyImmediate(root); }
        }
        static void Bed(Transform parent, string name, Material material)
        {
            string path = MeshFolder + "/Rounded Plot.asset"; var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                const int count = 32; var vertices = new List<Vector3> { Vector3.zero }; var triangles = new List<int>();
                for (int ring = 0; ring < 2; ring++) for (int i = 0; i < count; i++)
                {
                    float angle = i * Mathf.PI * 2 / count, x = Mathf.Cos(angle), z = Mathf.Sin(angle);
                    float radius = ring == 0 ? .51f : .585f + Mathf.Sin(i * 2.3f) * .008f;
                    vertices.Add(new Vector3(Mathf.Sign(x) * Mathf.Pow(Mathf.Abs(x), .40f) * radius, ring == 0 ? 0 : -.025f,
                        Mathf.Sign(z) * Mathf.Pow(Mathf.Abs(z), .40f) * radius));
                }
                for (int i = 0; i < count; i++)
                {
                    int a = 1 + i, b = 1 + (i + 1) % count, c = a + count, d = b + count;
                    triangles.AddRange(new[] { 0, b, a, a, b, c, b, d, c });
                }
                mesh = new Mesh { name = "Rounded Plot" }; mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh, path);
            }
            var part = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); part.layer = 2; part.transform.SetParent(parent, false);
            part.GetComponent<MeshFilter>().sharedMesh = mesh; part.GetComponent<Renderer>().sharedMaterial = material;
        }
        static void CreateEffects()
        {
            if (File.Exists(EffectPath)) return;
            var root = new GameObject("FarmActionEffects", typeof(FarmActionEffects)); root.layer = 2;
            try
            {
                var view = root.GetComponent<FarmActionEffects>();
                view.Dust = Part(root.transform, "Dust puff", PrimitiveType.Sphere, Vector3.zero, new Vector3(.12f, .09f, .12f), Mat("Earth Dust", new Color(.70f, .51f, .31f)));
                view.Droplet = Part(root.transform, "Water droplet", PrimitiveType.Sphere, Vector3.zero, new Vector3(.040f, .090f, .040f), Mat("Water Drops", new Color(.52f, .81f, .94f), .45f));
                view.Spark = Part(root.transform, "Harvest sparkle", PrimitiveType.Cube, Vector3.zero, new Vector3(.055f, .13f, .035f), Mat("Harvest Gold", new Color(1, .85f, .36f)));
                Part(view.Spark, "Spark cross", PrimitiveType.Cube, Vector3.zero, new Vector3(2.3f, .30f, 1), view.Spark.GetComponent<Renderer>().sharedMaterial);
                view.Seed = Part(root.transform, "Seed grain", PrimitiveType.Sphere, Vector3.zero, new Vector3(.045f, .025f, .065f), Mat("Seed Grain", new Color(.86f, .69f, .40f)));
                foreach (var renderer in root.GetComponentsInChildren<Renderer>()) { renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false; }
                foreach (var template in new[] { view.Dust, view.Droplet, view.Spark, view.Seed }) template.gameObject.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root, EffectPath);
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
