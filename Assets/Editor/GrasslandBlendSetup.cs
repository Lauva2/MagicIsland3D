using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MagicIsland.TerrainSystem;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    // Baked local art only; the existing ground/grass shaders and AA ownership remain unchanged.
    public static class GrasslandBlendSetup
    {
        public const string Marker = "MagicIsland.GrasslandBlend.v1", Pending = "MagicIsland.GrasslandBlend.Pending.v1";
        public const string MapPath = GrasslandGroundLayersSetup.MapPath;
        static readonly HashSet<string> Known = new HashSet<string>
        {
            GrasslandRevitalizationSetup.Marker, GrasslandIslandMeadowSetup.Marker, GrasslandGroveSetup.Marker,
            GrasslandFloraSetup.Marker, GrasslandGroundLayersSetup.Marker, GrasslandShrubStoneSetup.Marker,
            GrasslandTouchSetup.Marker, GrasslandReadabilitySetup.Marker, GrasslandPatchSetup.Marker
        };
        public sealed class MeshEdit { public Mesh Mesh; public Color[] Colors; public bool[] Changed; public string Metadata; }
        public sealed class Plant { public Vector2 Root; public bool Grass, Flower; public float Radius, Weight; }
        public sealed class PlanData
        {
            public MeshEdit[] Meshes;
            public Color32[] Pixels;
            public bool[] Changed;
            public Vector4 Rect;
            public int Width, Height;
            public string MapMetadata;
            public Plant[] Plants;
        }
        public static void Check(bool ok, string why)
        {
            if (!ok) throw new InvalidOperationException("Grassland blend: " + why);
        }
        public static bool Done(string data) => data.Split(';').Contains(Marker);
        public static bool Incomplete(string data) => data.Split(';').Contains(Pending);
        static float Mask(Vector2 p, MeadowTouchSettings s, float inset)
            => 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(s.AreaRadius - s.Feather, s.AreaRadius - inset,
                Vector2.Distance(p, s.Center)));
        public static Color32[] ReadPixels(string path, out int width, out int height)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            try
            {
                Check(texture.LoadImage(File.ReadAllBytes(path)), "read original distribution pixels");
                width = texture.width; height = texture.height; return texture.GetPixels32();
            }
            finally { Object.DestroyImmediate(texture); }
        }
        public static Vector2 PixelWorld(int x, int y, int width, int height, Vector4 rect)
            => new Vector2(rect.x + (x + .5f) / width * rect.z, rect.y + (y + .5f) / height * rect.w);
        public static float TexelReach(int width, int height, Vector4 rect)
            => new Vector2(rect.z / width, rect.w / height).magnitude;

        static Plant[] Plants(IslandTerrain terrain)
        {
            var settings = terrain.SeasonalDetails.TouchSample; var result = new List<Plant>();
            foreach (var r in terrain.SeasonalDetails.MeadowBatches)
            {
                var mesh = r.GetComponent<MeshFilter>().sharedMesh;
                var v = mesh.vertices; var uv = mesh.uv; var roots = mesh.uv2; var floor = mesh.uv3;
                Check(new[] { uv.Length, roots.Length, floor.Length }.All(n => n == v.Length), "neighboring plant channels");
                foreach (var group in Enumerable.Range(0, v.Length).Where(i =>
                    (roots[i] - settings.Center).sqrMagnitude < Mathf.Pow(settings.AreaRadius + 6, 2)).GroupBy(i => roots[i]))
                {
                    var ids = group.ToArray(); bool grass = ids.All(i => uv[i].x >= -5 && uv[i].x <= -4);
                    float radius = ids.Max(i => Vector2.Distance(new Vector2(v[i].x, v[i].z), group.Key));
                    float height = ids.Max(i => Mathf.Max(0, v[i].y - floor[i].x));
                    result.Add(new Plant { Root = group.Key, Grass = grass,
                        Flower = ids.Any(i => uv[i].x >= -3 && uv[i].x <= -2), Radius = radius,
                        Weight = Mathf.Clamp(.55f + height * .6f + radius * .15f, .5f, 1.25f) });
                }
            }
            Check(result.Count(p => p.Grass) > 100, "actual prepared grass patches required"); return result.ToArray();
        }

        public static PlanData Plan(IslandTerrain terrain)
        {
            var settings = terrain.SeasonalDetails.TouchSample;
            Check(terrain.IsValid && terrain.World.ArtVersion == 3 && settings.ArtVersion == 1
                && terrain.transform.position == Vector3.zero && terrain.transform.rotation == Quaternion.identity
                && terrain.transform.lossyScale == Vector3.one, "complete original sample and ground-map transform");
            Check(settings.Renderers.Length == 6 && GrasslandPatchSetup.Batches(terrain).Length == 2 && GrasslandPatchSetup.Batches(terrain).All(r =>
                GrasslandPatchSetup.Done(AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(r.GetComponent<MeshFilter>().sharedMesh)).userData)),
                "completed grass patch art required");
            var material = AssetDatabase.LoadAssetAtPath<Material>(GrasslandGroundLayersSetup.MaterialPath);
            var grounds = GrasslandGroundLayersSetup.Grounds(terrain);
            Check(material != null && material.shader.name == GrasslandGroundLayersSetup.ShaderName && grounds.Length > 0
                && grounds.All(r => r.sharedMaterials.Length == 1 && r.sharedMaterial == material)
                && AssetDatabase.GetAssetPath(material.GetTexture("_UnderlayMap")) == MapPath,
                "preserve custom ground materials or texture references");
            var importer = AssetImporter.GetAtPath(MapPath) as TextureImporter;
            Check(importer != null && importer.userData == GrasslandGroundLayersSetup.Marker && !importer.sRGBTexture
                && !importer.mipmapEnabled && importer.textureType == TextureImporterType.Default
                && importer.wrapMode == TextureWrapMode.Clamp && importer.filterMode == FilterMode.Bilinear
                && importer.textureCompression == TextureImporterCompression.Uncompressed,
                "preserve custom map metadata or import settings");
            var rect = material.GetVector("_UnderlayRect");
            Check(new[] { rect.x, rect.y, rect.z, rect.w }.All(float.IsFinite) && rect.z > 0 && rect.w > 0,
                "finite authored map bounds");
            var collisions = new HashSet<Mesh>(terrain.GetComponentsInChildren<MeshCollider>(true).Select(c => c.sharedMesh));
            foreach (var r in settings.Renderers)
            {
                var mesh = r.GetComponent<MeshFilter>().sharedMesh; string path = AssetDatabase.GetAssetPath(mesh);
                string metadata = AssetImporter.GetAtPath(path).userData;
                Check(path.StartsWith(BiomeIslandsSetup.Folder + "/", StringComparison.Ordinal) && !collisions.Contains(mesh)
                    && r.transform.localToWorldMatrix == terrain.transform.localToWorldMatrix
                    && r.sharedMaterials.Length == 1 && r.sharedMaterial.shader.name == GrasslandFloraSetup.ShaderName,
                    "original visual batch, material and transform");
                Check(GrasslandReadabilitySetup.Done(metadata) && metadata.Split(';').All(Known.Contains),
                    "preserve custom metadata or partial migration");
            }
            var pixels = ReadPixels(MapPath, out int width, out int height);
            Check(width == 512 && height == 512, "preserve custom map resolution");
            var result = new PlanData { Width = width, Height = height, Rect = rect, Pixels = pixels.ToArray(),
                Changed = new bool[pixels.Length], MapMetadata = importer.userData, Plants = Plants(terrain) };
            float reach = TexelReach(width, height, rect);
            Check(settings.AreaRadius - settings.Feather < settings.AreaRadius - reach, "resolvable sample feather");
            var grass = result.Plants.Where(p => p.Grass).ToArray(); var accents = result.Plants.Where(p => !p.Grass).ToArray();
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                int i = y * width + x; var p = PixelWorld(x, y, width, height, rect);
                float blend = Mask(p, settings, reach);
                // One full texel diagonal protects bilinear support at the road and sample boundary.
                if (blend <= 0 || GrasslandIslandMeadowSetup.RouteDistance(p) <= 2.1f + reach || pixels[i].a == 0) continue;
                float density = 0, contact = 0, flower = 0;
                foreach (var plant in grass)
                {
                    float square = (p - plant.Root).sqrMagnitude;
                    if (square < 20.25f) density += Mathf.Exp(-square / 4.5f) * plant.Weight;
                }
                density = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.7f, 5.5f, density));
                foreach (var plant in accents)
                {
                    float distance = Vector2.Distance(p, plant.Root), radius = Mathf.Clamp(plant.Radius, .25f, 1.2f) + .9f;
                    float soft = Mathf.Exp(-distance * distance / (radius * radius));
                    contact = Mathf.Max(contact, soft * (plant.Flower ? .25f : .55f));
                    if (plant.Flower) flower = Mathf.Max(flower, soft);
                }
                float noise = GrasslandGroundLayersSetup.Noise(p, .19f, 73);
                var target = new Color(Mathf.Lerp(.18f, .98f, density),
                    Mathf.Lerp(.70f, .04f, density) * Mathf.Lerp(.88f, 1.05f, noise),
                    Mathf.Clamp01(density * .47f + contact + flower * .10f), pixels[i].a / 255f);
                var next = (Color32)Color.Lerp(pixels[i], target, blend * .90f); next.a = pixels[i].a;
                result.Pixels[i] = next; result.Changed[i] = !next.Equals(pixels[i]);
            }
            Check(result.Changed.Count(b => b) > 100, "bounded local distribution edit");
            var world = new GrasslandRevitalizationSetup.AuthoringWorld(terrain,
                new Vector4(settings.Center.x - settings.AreaRadius, settings.Center.y - settings.AreaRadius,
                    settings.AreaRadius * 2, settings.AreaRadius * 2));
            var floorCache = new Dictionary<Vector2, float>();
            float Floor(Vector2 p)
            {
                if (!floorCache.TryGetValue(p, out float floor)) { floor = world.Floor(p); floorCache.Add(p, floor); }
                return floor;
            }
            var edits = new List<MeshEdit>();
            foreach (var renderer in settings.Renderers)
            {
                var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                var v = mesh.vertices; var colors = mesh.colors; var roots = mesh.uv2; var uv = mesh.uv;
                Check(new[] { colors.Length, roots.Length, uv.Length }.All(n => n == v.Length), "complete grass colors and roots");
                var e = new MeshEdit { Mesh = mesh, Colors = colors.ToArray(), Changed = new bool[v.Length],
                    Metadata = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(mesh)).userData };
                for (int i = 0; i < v.Length; i++)
                {
                    if (uv[i].x < -5 || uv[i].x > -4) continue;
                    float blend = Mask(roots[i], settings, 1.5f); if (blend <= 0) continue;
                    var p = new Vector2(v[i].x, v[i].z);
                    Check(Vector2.Distance(p, roots[i]) <= 1.182f, "original complete grass footprint");
                    float h = Mathf.Max(0, v[i].y - Floor(p) - .018f);
                    float factor = Mathf.Lerp(.74f, 1.025f, Mathf.SmoothStep(0, 1, Mathf.Clamp01(h / .20f)));
                    var color = colors[i] * Mathf.Lerp(1, factor, blend); color.a = colors[i].a;
                    e.Colors[i] = color; e.Changed[i] = true;
                }
                edits.Add(e);
            }
            Check(edits.Sum(e => e.Changed.Count(b => b)) > 1000, "existing grass gradient sample");
            result.Meshes = edits.ToArray(); return result;
        }

        static void Clean(string path) => File.WriteAllText(path, string.Join("\n", File.ReadAllLines(path).Select(l => l.TrimEnd())) + "\n");
        [MenuItem("Magic Island/Art/Blend Grassland Meadow Ground Sample (Explicit)")]
        public static void Rebuild()
        {
            Check(!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling, "Edit Mode required");
            var root = PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath); bool saved = false;
            var backups = new Dictionary<string, byte[]>();
            try
            {
                var terrain = root.GetComponent<IslandTerrain>();
                Check(terrain.IsValid && terrain.SeasonalDetails.TouchSample.ArtVersion == 1
                    && terrain.SeasonalDetails.TouchSample.Renderers.Length == 6, "valid prepared original terrain");
                var paths = terrain.SeasonalDetails.TouchSample.Renderers.Select(r =>
                    AssetDatabase.GetAssetPath(r.GetComponent<MeshFilter>().sharedMesh)).Concat(new[] { MapPath }).ToArray();
                var metadata = paths.Select(p => AssetImporter.GetAtPath(p).userData).ToArray();
                Check(!metadata.Any(Incomplete), "preserve interrupted blend; do not reapply partially written colors");
                if (metadata.Any(Done))
                {
                    Check(metadata.All(Done), "preserve incomplete blend migration");
                    Debug.Log("Grassland ground blend and subsequent manual edits retained."); saved = true; return;
                }
                var plan = Plan(terrain);
                foreach (string path in paths.SelectMany(p => new[] { p, p + ".meta" })) backups.Add(path, File.ReadAllBytes(path));
                var mapImporter = AssetImporter.GetAtPath(MapPath);
                mapImporter.userData = plan.MapMetadata + ";" + Pending; mapImporter.SaveAndReimport();
                foreach (var e in plan.Meshes) { e.Mesh.colors = e.Colors; EditorUtility.SetDirty(e.Mesh); }
                AssetDatabase.SaveAssets();
                var texture = new Texture2D(plan.Width, plan.Height, TextureFormat.RGBA32, false, true);
                try { texture.SetPixels32(plan.Pixels); texture.Apply(); File.WriteAllBytes(MapPath, texture.EncodeToPNG()); }
                finally { Object.DestroyImmediate(texture); }
                AssetDatabase.ImportAsset(MapPath, ImportAssetOptions.ForceUpdate);
                foreach (var e in plan.Meshes)
                {
                    string path = AssetDatabase.GetAssetPath(e.Mesh); var importer = AssetImporter.GetAtPath(path);
                    importer.userData = e.Metadata + ";" + Marker; importer.SaveAndReimport(); Clean(path); Clean(path + ".meta");
                }
                mapImporter = AssetImporter.GetAtPath(MapPath);
                mapImporter.userData = plan.MapMetadata + ";" + Marker; mapImporter.SaveAndReimport(); Clean(MapPath + ".meta");
                saved = true;
                Debug.Log("Grassland blend prepared: " + plan.Changed.Count(b => b) + " local map pixels, "
                    + plan.Meshes.Sum(e => e.Changed.Count(b => b)) + " grass color vertices; no geometry, shader or material changes.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
                if (!saved)
                {
                    foreach (var p in backups) File.WriteAllBytes(p.Key, p.Value);
                    foreach (string path in backups.Keys.Where(p => !p.EndsWith(".meta", StringComparison.Ordinal)))
                        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                }
            }
        }
    }
}
