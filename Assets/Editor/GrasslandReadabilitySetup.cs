using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MagicIsland.TerrainSystem;
using UnityEditor;
using UnityEngine;

namespace MagicIsland.EditorTools
{
    // A separate, explicit art pass: existing touch settings and runtime ownership are unchanged.
    public static class GrasslandReadabilitySetup
    {
        public const string Marker = "MagicIsland.GrasslandReadability.v1";
        static readonly HashSet<string> Known = new HashSet<string>
        {
            GrasslandRevitalizationSetup.Marker, GrasslandIslandMeadowSetup.Marker,
            GrasslandGroveSetup.Marker, GrasslandFloraSetup.Marker,
            GrasslandGroundLayersSetup.Marker, GrasslandShrubStoneSetup.Marker,
            GrasslandTouchSetup.Marker
        };

        public sealed class Edit
        {
            public Mesh Mesh;
            public Vector3[] Vertices, Normals;
            public Color[] Colors;
            public bool[] Changed;
            public Bounds Bounds;
            public string Metadata;
            public int Blades, Low, Medium, Tall;
        }

        public static void Check(bool ok, string why)
        {
            if (!ok) throw new InvalidOperationException("Grassland readability: " + why);
        }

        public static bool Done(string value) => value.Split(';').Contains(Marker);
        static float Noise(Vector2 p, float scale, float salt)
            => GrasslandGroundLayersSetup.Noise(p, scale, salt);

        public static Edit[] Plan(IslandTerrain terrain)
        {
            var settings = terrain.SeasonalDetails.TouchSample;
            Check(terrain.IsValid && terrain.World.ArtVersion == 3 && settings.ArtVersion == 1,
                "complete original islands and prepared touch sample required");
            Check(terrain.transform.lossyScale == Vector3.one && terrain.transform.rotation == Quaternion.identity,
                "preserve custom terrain transform");
            var collisions = new HashSet<Mesh>(terrain.GetComponentsInChildren<MeshCollider>(true).Select(c => c.sharedMesh));
            var world = new GrasslandRevitalizationSetup.AuthoringWorld(terrain,
                new Vector4(settings.Center.x - settings.AreaRadius, settings.Center.y - settings.AreaRadius,
                    settings.AreaRadius * 2, settings.AreaRadius * 2));
            var result = new List<Edit>();
            foreach (var renderer in settings.Renderers)
            {
                var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                string path = AssetDatabase.GetAssetPath(mesh), metadata = AssetImporter.GetAtPath(path).userData;
                Check(path.StartsWith(BiomeIslandsSetup.Folder + "/", StringComparison.Ordinal)
                    && !collisions.Contains(mesh) && renderer.transform.localToWorldMatrix == terrain.transform.localToWorldMatrix,
                    "owned visual mesh and original transform " + path);
                Check(renderer.sharedMaterials.Length == 1 && renderer.sharedMaterial.shader.name == GrasslandFloraSetup.ShaderName,
                    "preserve custom material");
                Check(GrasslandTouchSetup.Done(metadata) && metadata.Split(';').All(Known.Contains),
                    "preserve unknown metadata or partial migration " + path);
                var v = mesh.vertices; var n = mesh.normals; var c = mesh.colors; var uv = mesh.uv; var roots = mesh.uv2;
                Check(v.Length == n.Length && v.Length == c.Length && v.Length == uv.Length && v.Length == roots.Length,
                    "complete original visual and root data");
                var edit = new Edit { Mesh = mesh, Vertices = v.ToArray(), Normals = n.ToArray(), Colors = c.ToArray(),
                    Changed = new bool[v.Length], Bounds = mesh.bounds, Metadata = metadata };
                foreach (var part in BiomePolishSetup.Components(mesh))
                {
                    if (part.Length != 5 || part.Any(i => uv[i].x > -4 || uv[i].x < -5)) continue;
                    var root = roots[part[0]];
                    float distance = Vector2.Distance(root, settings.Center);
                    if (distance >= settings.AreaRadius - 1) continue;
                    Check(part.All(i => roots[i] == root), "complete folded leaf with one root");
                    var bases = part.Where(i => Mathf.Abs(uv[i].x + 4) < .0001f).ToArray();
                    Check(bases.Length == 2 && part.All(i => c[i].g > c[i].r && c[i].g > c[i].b),
                        "preserve unrecognized leaf shape or paint");
                    float Height(int i) => Mathf.Max(0, v[i].y - world.Floor(new Vector2(v[i].x, v[i].z)) - .018f);
                    float oldHeight = part.Max(Height);
                    Check(oldHeight > .03f && oldHeight < .6f, "recognizable prepared low grass");
                    Check(part.All(i => Vector2.Distance(new Vector2(v[i].x, v[i].z), root) <= 1.182f),
                        "original horizontal plant envelope");

                    // Coherent patches set the clump height; a small per-leaf variation avoids hedges.
                    float patch = Noise(root, .16f, 67) * .7f + Noise(root, .32f, 23) * .3f;
                    float phase = uv[part[0]].y + v[bases[0]].x * 3.71f + v[bases[0]].z * 2.13f;
                    float variation = .88f + .24f * (.5f + .5f * Mathf.Sin(phase));
                    float target = patch < .44f ? Mathf.Lerp(.09f, .17f, Mathf.InverseLerp(.25f, .44f, patch))
                        : patch < .60f ? Mathf.Lerp(.28f, .43f, Mathf.InverseLerp(.44f, .60f, patch))
                        : Mathf.Lerp(.50f, .64f, Mathf.InverseLerp(.60f, .78f, patch));
                    float road = GrasslandIslandMeadowSetup.RouteDistance(root);
                    float shoulder = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(2.5f, 4.5f, road));
                    target = Mathf.Lerp(.10f, target * variation, shoulder);
                    float blend = 1 - Mathf.SmoothStep(0, 1,
                        Mathf.InverseLerp(settings.AreaRadius - settings.Feather, settings.AreaRadius - 1, distance));
                    float scale = Mathf.Lerp(1, target / oldHeight, blend);
                    var sage = Color.Lerp(new Color(.30f, .47f, .27f), new Color(.42f, .55f, .32f),
                        Noise(root, .13f, 41));
                    foreach (int i in part)
                    {
                        if (bases.Contains(i)) continue;
                        float height = Height(i), weight = Mathf.Clamp01(height / oldHeight);
                        edit.Vertices[i].y = v[i].y + height * (scale - 1);
                        edit.Normals[i] = new Vector3(n[i].x, n[i].y / scale, n[i].z).normalized;
                        var tint = Color.Lerp(sage * .88f, sage * 1.10f, weight);
                        tint.a = c[i].a;
                        edit.Colors[i] = Color.Lerp(c[i], tint, blend);
                        edit.Changed[i] = true;
                        // Keep the old culling volume, expanding only where a taller tip needs room for motion.
                        edit.Bounds.Encapsulate(edit.Vertices[i] + Vector3.up * .25f);
                        edit.Bounds.Encapsulate(edit.Vertices[i] - Vector3.up * .25f);
                        Check(edit.Vertices[i].y - world.Floor(new Vector2(v[i].x, v[i].z)) <= .75f,
                            "bounded knee-high grass, below flowers and the player's face");
                    }
                    edit.Blades++;
                    if (target < .22f) edit.Low++; else if (target < .48f) edit.Medium++; else edit.Tall++;
                }
                result.Add(edit);
            }
            Check(result.Count > 0 && result.Sum(e => e.Blades) > 50
                && result.Sum(e => e.Low) > 0 && result.Sum(e => e.Medium) > 0 && result.Sum(e => e.Tall) > 0
                && result.Select(e => e.Mesh).Distinct().Count() == result.Count, "bounded sample with three grass heights");
            return result.ToArray();
        }

        static void Clean(string path)
            => File.WriteAllText(path, string.Join("\n", File.ReadAllLines(path).Select(l => l.TrimEnd())) + "\n");

        [MenuItem("Magic Island/Art/Refine Grassland Grass Readability Sample (Explicit)")]
        public static void Rebuild()
        {
            Check(!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling, "Edit Mode required");
            var root = PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);
            var backups = new Dictionary<string, byte[]>(); bool saved = false;
            try
            {
                var terrain = root.GetComponent<IslandTerrain>(); var settings = terrain.SeasonalDetails.TouchSample;
                Check(terrain.IsValid && settings.ArtVersion == 1, "complete original touch sample required");
                var marked = settings.Renderers.Select(r => Done(AssetImporter.GetAtPath(
                    AssetDatabase.GetAssetPath(r.GetComponent<MeshFilter>().sharedMesh)).userData)).ToArray();
                if (marked.Any(b => b))
                {
                    Check(marked.All(b => b), "preserve incomplete sample migration");
                    Debug.Log("Grassland readability sample and subsequent manual edits retained."); saved = true; return;
                }
                var plan = Plan(terrain);
                foreach (string path in plan.Select(e => AssetDatabase.GetAssetPath(e.Mesh)).SelectMany(p => new[] { p, p + ".meta" }))
                    backups.Add(path, File.ReadAllBytes(path));
                foreach (var e in plan)
                {
                    e.Mesh.vertices = e.Vertices; e.Mesh.normals = e.Normals; e.Mesh.colors = e.Colors;
                    e.Mesh.bounds = e.Bounds; EditorUtility.SetDirty(e.Mesh);
                }
                AssetDatabase.SaveAssets();
                foreach (var e in plan)
                {
                    string path = AssetDatabase.GetAssetPath(e.Mesh); var importer = AssetImporter.GetAtPath(path);
                    importer.userData = e.Metadata + ";" + Marker; importer.SaveAndReimport(); Clean(path); Clean(path + ".meta");
                }
                saved = true;
                Debug.Log("Grassland readability sample prepared: " + plan.Length + " original meshes, "
                    + plan.Sum(e => e.Blades) + " blades; low/medium/tall=" + plan.Sum(e => e.Low) + "/"
                    + plan.Sum(e => e.Medium) + "/" + plan.Sum(e => e.Tall) + ". No new objects, faces, materials or colliders.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
                if (!saved)
                {
                    foreach (var p in backups) File.WriteAllBytes(p.Key, p.Value);
                    foreach (string p in backups.Keys.Where(p => !p.EndsWith(".meta", StringComparison.Ordinal)))
                        AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate);
                }
            }
        }
    }
}
