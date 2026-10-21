using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using MagicIsland.Managers;
using MagicIsland.TerrainSystem;
using MagicIsland.WeatherSystem;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    public static class GrasslandReadabilityVerification
    {
        const string Folder = "Logs/GrasslandReadability", Baseline = "Assets/Editor/GrasslandReadabilityBaseline";
        static void Check(bool ok, string why)
        {
            if (!ok) throw new InvalidOperationException("Grassland readability verification: " + why);
        }
        static string Hash(string path)
        {
            using (var sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path)));
        }
        static Vector4[] UV(Mesh mesh, int channel)
        {
            var result = new List<Vector4>(); mesh.GetUVs(channel, result); return result.ToArray();
        }

        public static void BakeAndVerify()
        {
            Check(Application.dataPath.Replace('\\', '/').Contains("/Logs/"), "isolated validation project required");
            var root = PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);
            GrasslandReadabilitySetup.Edit[] plan;
            try
            {
                var terrain = root.GetComponent<IslandTerrain>(); plan = GrasslandReadabilitySetup.Plan(terrain);
                var renderer = terrain.SeasonalDetails.TouchSample.Renderers[0];
                var material = renderer.sharedMaterial; var custom = new Material(Shader.Find("Standard"));
                try
                {
                    renderer.sharedMaterial = custom; bool refused = false;
                    try { GrasslandReadabilitySetup.Plan(terrain); } catch (InvalidOperationException) { refused = true; }
                    Check(refused, "custom material refused before writes");
                }
                finally { renderer.sharedMaterial = material; Object.DestroyImmediate(custom); }
                var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(plan[0].Mesh));
                string metadata = importer.userData;
                try
                {
                    importer.userData = metadata + ";Artist.Custom"; bool refused = false;
                    try { GrasslandReadabilitySetup.Plan(terrain); } catch (InvalidOperationException) { refused = true; }
                    Check(refused, "unknown metadata refused before writes");
                }
                finally { importer.userData = metadata; }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            var paths = plan.Select(e => AssetDatabase.GetAssetPath(e.Mesh)).ToArray();
            var vertices = plan.Select(e => e.Mesh.vertices).ToArray(); var normals = plan.Select(e => e.Mesh.normals).ToArray();
            var colors = plan.Select(e => e.Mesh.colors).ToArray(); var indices = plan.Select(e => e.Mesh.triangles).ToArray();
            var channels = plan.Select(e => Enumerable.Range(0, 8).Select(ch => UV(e.Mesh, ch)).ToArray()).ToArray();
            var guids = paths.Select(AssetDatabase.AssetPathToGUID).ToArray();
            if (!AssetDatabase.IsValidFolder(Baseline)) AssetDatabase.CreateFolder("Assets/Editor", "GrasslandReadabilityBaseline");
            foreach (string path in paths)
            {
                string target = Baseline + "/" + Path.GetFileName(path);
                if (File.Exists(target))
                {
                    var fixture = AssetDatabase.LoadAssetAtPath<Mesh>(target);
                    var source = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    Check(fixture != null && fixture.vertices.SequenceEqual(source.vertices)
                        && fixture.normals.SequenceEqual(source.normals) && fixture.colors.SequenceEqual(source.colors)
                        && fixture.triangles.SequenceEqual(source.triangles)
                        && Enumerable.Range(0, 8).All(ch => UV(fixture, ch).SequenceEqual(UV(source, ch))),
                        "retain only the exact original comparison fixture");
                }
                else Check(AssetDatabase.CopyAsset(path, target), "source comparison fixture");
            }
            var excluded = new HashSet<string>(paths.SelectMany(p => new[] { p, p + ".meta" }));
            var protectedFiles = Directory.GetFiles("Assets/Art", "*", SearchOption.AllDirectories)
                .Select(p => p.Replace('\\', '/')).Where(p => !excluded.Contains(p)).ToArray();
            var protectedHashes = protectedFiles.Select(Hash).ToArray();
            GrasslandReadabilitySetup.Rebuild();
            for (int e = 0; e < plan.Length; e++)
            {
                var mesh = plan[e].Mesh;
                Check(AssetDatabase.AssetPathToGUID(paths[e]) == guids[e] && mesh.triangles.SequenceEqual(indices[e]),
                    "original GUID, vertex count and exact face order");
                Check(mesh.vertices.SequenceEqual(plan[e].Vertices) && mesh.normals.SequenceEqual(plan[e].Normals)
                    && mesh.colors.SequenceEqual(plan[e].Colors) && mesh.bounds == plan[e].Bounds, "exact planned art");
                for (int ch = 0; ch < 8; ch++) Check(UV(mesh, ch).SequenceEqual(channels[e][ch]), "all eight original UV channels");
                var actualV = mesh.vertices; var actualN = mesh.normals; var actualC = mesh.colors;
                for (int i = 0; i < mesh.vertexCount; i++)
                {
                    Check(actualV[i].x == vertices[e][i].x && actualV[i].z == vertices[e][i].z,
                        "all plant positions and horizontal envelopes unchanged");
                    if (!plan[e].Changed[i]) Check(actualV[i] == vertices[e][i] && actualN[i] == normals[e][i]
                        && actualC[i] == colors[e][i], "anchors, flowers, other plants and outside sample unchanged");
                    Check(float.IsFinite(actualN[i].x) && actualN[i].sqrMagnitude > .99f,
                        "finite unit normals without inverted double-sided shading");
                }
            }
            for (int i = 0; i < protectedFiles.Length; i++) Check(Hash(protectedFiles[i]) == protectedHashes[i],
                "prefab, terrain, collisions, material, other biome and ecology bytes retained " + protectedFiles[i]);

            // A completed pass must preserve later sculpting/paint and all existing explicit/automatic entrances.
            int edited = Array.FindIndex(plan, e => e.Changed.Any(b => b));
            Check(edited >= 0, "an actual grass-bearing batch for the manual-paint fixture");
            string first = paths[edited]; byte[] savedMesh = File.ReadAllBytes(first);
            try
            {
                var mesh = plan[edited].Mesh; var paint = mesh.colors; var points = mesh.vertices;
                int index = Array.FindIndex(plan[edited].Changed, b => b); paint[index].g *= .99f; points[index].y += .001f;
                mesh.colors = paint; mesh.vertices = points; EditorUtility.SetDirty(mesh); AssetDatabase.SaveAssets();
                var all = Directory.GetFiles("Assets/Art", "*", SearchOption.AllDirectories); var hashes = all.Select(Hash).ToArray();
                GrasslandReadabilitySetup.Rebuild(); GrasslandTouchSetup.Rebuild(); GrasslandFloraSetup.Rebuild();
                PrototypeAddressablesSetup.EnsureSetup();
                for (int i = 0; i < all.Length; i++) Check(Hash(all[i]) == hashes[i], "repeat/old entrances/Setup preserve manual art " + all[i]);
            }
            finally { File.WriteAllBytes(first, savedMesh); AssetDatabase.ImportAsset(first, ImportAssetOptions.ForceUpdate); }
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Folder + "/Stats.json", JsonUtility.ToJson(new Stats
            {
                Meshes = plan.Length, Blades = plan.Sum(e => e.Blades), Low = plan.Sum(e => e.Low),
                Medium = plan.Sum(e => e.Medium), Tall = plan.Sum(e => e.Tall), Vertices = plan.Sum(e => e.Changed.Count(b => b))
            }, true));
            Debug.Log("Grassland readability bake verification PASSED: sample-only three-height patches and sage paint; "
                + "all roots/XZ/UV/topology/GUIDs, prefab, terrain/collisions and other art preserved; custom refusal and repeat/manual Setup.");
        }

        [Serializable] sealed class Stats { public int Meshes, Blades, Low, Medium, Tall, Vertices; }
        public static void After() => GrasslandTouchVerification.After();

        public static void Visit(GameManager g)
            => Compare(g, Folder, Baseline, "readability");

        internal static void Compare(GameManager g, string folder, string baseline, string label, bool partialBaseline = false,
            Action<bool> supplement = null)
        {
            var cover = g.Terrain.Terrain.SeasonalDetails; var settings = cover.TouchSample;
            var renderers = settings.Renderers;
            if (settings.ArtVersion == 0) return;
            bool Prepared(Renderer renderer)
            {
                // Bundle instances are not AssetDatabase assets; inspect their corresponding authoring mesh.
                string path = BiomeIslandsSetup.Folder + "/" + renderer.GetComponent<MeshFilter>().sharedMesh.name + ".asset";
                var importer = AssetImporter.GetAtPath(path);
                return importer != null && GrasslandReadabilitySetup.Done(importer.userData);
            }
            if (!renderers.All(Prepared)) return;
            Directory.CreateDirectory(folder);
            var filters = renderers.Select(r => r.GetComponent<MeshFilter>()).ToArray();
            var meshes = filters.Select(f => f.sharedMesh).ToArray();
            var camera = g.Players.Player.ViewCamera; var position = g.Players.Player.transform.position;
            var cameraPos = camera.transform.position; var cameraRot = camera.transform.rotation;
            float size = camera.orthographicSize, far = camera.farClipPlane;
            float wind = Shader.GetGlobalFloat("_IslandWindTime"), strength = Shader.GetGlobalFloat("_IslandWindStrength");
            var state = g.Weather.State; float rain = g.Weather.RainAmount;
            var stateProperty = typeof(WeatherManager).GetProperty("State");
            var rainProperty = typeof(WeatherManager).GetProperty("RainAmount");
            try
            {
                Check(g.Terrain.TryResolve(new Vector3(-128.59f, 0, 447.98f), out var focus), "actual sample ground");
                g.Players.RestorePosition(focus); g.Cameras.RestoreOccluders(); camera.farClipPlane = 3200;
                for (int i = 0; i < meshes.Length; i++)
                {
                    var authored = AssetDatabase.LoadAssetAtPath<Mesh>(BiomeIslandsSetup.Folder + "/" + meshes[i].name + ".asset");
                    Check(meshes[i].vertices.SequenceEqual(authored.vertices) && meshes[i].colors.SequenceEqual(authored.colors),
                        "actual Packed AA contains final geometry and palette");
                }
                foreach (string light in new[] { "Day", "Dusk", "Rain" })
                {
                    bool wet = light == "Rain";
                    stateProperty.SetValue(g.Weather, new WeatherState(state.Seed, 0, state.Day, wet ? WeatherKind.Rainy : WeatherKind.Sunny));
                    rainProperty.SetValue(g.Weather, wet ? 1f : 0f);
                    g.Weather.Tick(light == "Dusk" ? 1100 : 720, true);
                    g.Ocean.Surface.Tick(0, light == "Dusk" ? .4f : 1, wet, g.Weather.Sun);
                    cover.TickTouch(0, null, false, true);
                    Shader.SetGlobalFloat("_IslandWindTime", 12); Shader.SetGlobalFloat("_IslandWindStrength", .65f);
                    GrasslandTouchVerification.Frame(camera, focus);
                    for (int i = 0; i < filters.Length; i++)
                    {
                        string fixture = baseline + "/" + meshes[i].name + ".asset";
                        filters[i].sharedMesh = partialBaseline && !File.Exists(fixture) ? meshes[i]
                            : AssetDatabase.LoadAssetAtPath<Mesh>(fixture);
                        Check(filters[i].sharedMesh != null, label + " comparison fixture");
                    }
                    supplement?.Invoke(true);
                    var before = GrasslandTouchVerification.Capture(camera, folder + "/" + light + "Before.png");
                    for (int i = 0; i < filters.Length; i++) filters[i].sharedMesh = meshes[i];
                    supplement?.Invoke(false);
                    var after = GrasslandTouchVerification.Capture(camera, folder + "/" + light + "After.png");
                    Check(GrasslandCrownWindVerification.FrameChanges(before, after, out _) > 500,
                        "visible sample art change in normal camera " + light);
                }
                Check(!File.Exists(g.Saves.SavePath), "cosmetic pass never saves daytime progress");
                Debug.Log("Grassland " + label + " Packed verification PASSED: actual baked AA, same normal-camera Day/Dusk/Rain comparison; no daytime save.");
            }
            finally
            {
                supplement?.Invoke(false);
                for (int i = 0; i < filters.Length; i++) filters[i].sharedMesh = meshes[i];
                cover.TickTouch(0, null, false, true); g.Players.RestorePosition(position);
                stateProperty.SetValue(g.Weather, state); rainProperty.SetValue(g.Weather, rain);
                g.Weather.Tick(720, true); g.Ocean.Surface.Tick(0, 1, false, g.Weather.Sun);
                Shader.SetGlobalFloat("_IslandWindTime", wind); Shader.SetGlobalFloat("_IslandWindStrength", strength);
                camera.transform.SetPositionAndRotation(cameraPos, cameraRot); camera.orthographicSize = size; camera.farClipPlane = far;
            }
        }
    }
}
