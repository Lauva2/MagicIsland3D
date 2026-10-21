using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using MagicIsland.Managers;
using MagicIsland.TerrainSystem;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    public static class GrasslandPatchVerification
    {
        const string Folder = "Logs/GrasslandPatch", Baseline = "Assets/Editor/GrasslandPatchBaseline";
        static void Check(bool ok, string why)
        {
            if (!ok) throw new InvalidOperationException("Grassland patch verification: " + why);
        }
        static string Hash(string path)
        {
            using (var sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path)));
        }
        static Vector4[] UV(Mesh mesh, int channel)
        {
            var list = new List<Vector4>(); mesh.GetUVs(channel, list); return list.ToArray();
        }
        public static void BakeAndVerify()
        {
            Check(Application.dataPath.Replace('\\', '/').Contains("/Logs/"), "isolated project required");
            var root = PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath); GrasslandPatchSetup.Edit[] plan;
            MeadowTouchSettings settings;
            try
            {
                var terrain = root.GetComponent<IslandTerrain>(); settings = terrain.SeasonalDetails.TouchSample;
                plan = GrasslandPatchSetup.Plan(terrain);
                var renderer = GrasslandPatchSetup.Batches(terrain)[0]; var original = renderer.sharedMaterial;
                var material = new Material(Shader.Find("Standard"));
                try
                {
                    renderer.sharedMaterial = material; bool refused = false;
                    try { GrasslandPatchSetup.Plan(terrain); } catch (InvalidOperationException) { refused = true; }
                    Check(refused, "custom material refused before writes");
                }
                finally { renderer.sharedMaterial = original; Object.DestroyImmediate(material); }
                var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(plan[0].Mesh)); string metadata = importer.userData;
                try
                {
                    importer.userData = metadata + ";Artist.Custom"; bool refused = false;
                    try { GrasslandPatchSetup.Plan(terrain); } catch (InvalidOperationException) { refused = true; }
                    Check(refused, "unknown metadata refused before writes");
                    importer.userData = metadata + ";" + GrasslandPatchSetup.Marker; refused = false;
                    try { GrasslandPatchSetup.Rebuild(); } catch (InvalidOperationException) { refused = true; }
                    Check(refused, "partial migration refused before writes");
                }
                finally { importer.userData = metadata; }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            var paths = plan.Select(e => AssetDatabase.GetAssetPath(e.Mesh)).ToArray();
            var vertices = plan.Select(e => e.Mesh.vertices).ToArray(); var normals = plan.Select(e => e.Mesh.normals).ToArray();
            var colors = plan.Select(e => e.Mesh.colors).ToArray(); var faces = plan.Select(e => e.Mesh.triangles).ToArray();
            var channels = plan.Select(e => Enumerable.Range(0, 8).Select(ch => UV(e.Mesh, ch)).ToArray()).ToArray();
            var guids = paths.Select(AssetDatabase.AssetPathToGUID).ToArray(); var formats = plan.Select(e => e.Mesh.indexFormat).ToArray();
            if (!AssetDatabase.IsValidFolder(Baseline)) AssetDatabase.CreateFolder("Assets/Editor", "GrasslandPatchBaseline");
            foreach (var path in paths)
            {
                string target = Baseline + "/" + Path.GetFileName(path);
                if (File.Exists(target))
                {
                    var source = AssetDatabase.LoadAssetAtPath<Mesh>(path); var fixture = AssetDatabase.LoadAssetAtPath<Mesh>(target);
                    Check(fixture != null && fixture.vertices.SequenceEqual(source.vertices) && fixture.colors.SequenceEqual(source.colors)
                        && fixture.normals.SequenceEqual(source.normals) && fixture.triangles.SequenceEqual(source.triangles)
                        && Enumerable.Range(0, 8).All(ch => UV(fixture, ch).SequenceEqual(UV(source, ch))), "exact original fixture retained");
                }
                else Check(AssetDatabase.CopyAsset(path, target), "original comparison fixture");
            }
            var excluded = new HashSet<string>(paths.SelectMany(p => new[] { p, p + ".meta" }));
            var protectedFiles = Directory.GetFiles("Assets/Art", "*", SearchOption.AllDirectories).Select(p => p.Replace('\\', '/'))
                .Where(p => !excluded.Contains(p)).ToArray(); var hashes = protectedFiles.Select(Hash).ToArray();
            GrasslandPatchSetup.Rebuild();
            for (int e = 0; e < plan.Length; e++)
            {
                var mesh = plan[e].Mesh; var v = mesh.vertices; var n = mesh.normals;
                Check(AssetDatabase.AssetPathToGUID(paths[e]) == guids[e] && mesh.indexFormat == formats[e]
                    && mesh.triangles.Take(faces[e].Length).SequenceEqual(faces[e]), "original GUID, index format and face prefix");
                Check(v.SequenceEqual(plan[e].Vertices) && n.SequenceEqual(plan[e].Normals) && mesh.colors.SequenceEqual(plan[e].Colors)
                    && mesh.triangles.SequenceEqual(plan[e].Indices) && mesh.bounds == plan[e].Bounds, "exact planned mesh and bounds");
                Check(mesh.colors.Take(vertices[e].Length).SequenceEqual(colors[e]), "all original paint retained");
                for (int ch = 0; ch < 8; ch++)
                    Check(UV(mesh, ch).Take(channels[e][ch].Length).SequenceEqual(channels[e][ch])
                        && (ch < 3 || UV(mesh, ch).Length == 0), "all original UV channels and root addressing retained");
                for (int i = 0; i < vertices[e].Length; i++)
                    if (!plan[e].Changed[i]) Check(v[i] == vertices[e][i] && n[i] == normals[e][i],
                        "every old anchored base, flower and outside-sample vertex unchanged");
                var roots = mesh.uv2; var weights = mesh.uv; var season = mesh.uv3;
                for (int i = vertices[e].Length; i < mesh.vertexCount; i++)
                {
                    Check(Vector2.Distance(new Vector2(v[i].x, v[i].z), roots[i]) <= GrasslandPatchSetup.StaticRadius
                        && Vector2.Distance(roots[i], settings.Center) + GrasslandPatchSetup.MotionRadius < settings.AreaRadius,
                        "complete static and moving grass remains inside the sample");
                    Check(GrasslandIslandMeadowSetup.RouteDistance(roots[i]) - GrasslandPatchSetup.MotionRadius > 2.1f,
                        "full moving envelope preserves the original road center");
                    Check(weights[i].x >= -5 && weights[i].x <= -4 && season[i].y == 1 && n[i].sqrMagnitude > .99f,
                        "new grass uses original species, season, root and normal contract");
                }
                Check(mesh.vertexCount - vertices[e].Length == plan[e].AddedBlades * 7, "bounded three-section curved leaves");
            }
            for (int i = 0; i < protectedFiles.Length; i++) Check(Hash(protectedFiles[i]) == hashes[i],
                "prefab, terrain, collisions, materials, other plants and islands retained " + protectedFiles[i]);
            string first = paths[0]; byte[] saved = File.ReadAllBytes(first);
            try
            {
                var mesh = plan[0].Mesh; var paint = mesh.colors; paint[paint.Length - 1].g *= .99f;
                mesh.colors = paint; EditorUtility.SetDirty(mesh); AssetDatabase.SaveAssets();
                var all = Directory.GetFiles("Assets/Art", "*", SearchOption.AllDirectories); var manual = all.Select(Hash).ToArray();
                GrasslandPatchSetup.Rebuild(); GrasslandReadabilitySetup.Rebuild(); GrasslandTouchSetup.Rebuild();
                GrasslandFloraSetup.Rebuild(); PrototypeAddressablesSetup.EnsureSetup();
                for (int i = 0; i < all.Length; i++) Check(Hash(all[i]) == manual[i], "repeat/old entrances/Setup retain manual art " + all[i]);
            }
            finally { File.WriteAllBytes(first, saved); AssetDatabase.ImportAsset(first, ImportAssetOptions.ForceUpdate); }
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Folder + "/Stats.json", JsonUtility.ToJson(new Stats { Meshes = plan.Length,
                Reshaped = plan.Sum(e => e.Reshaped), Clumps = plan.Sum(e => e.Sites.Length),
                Blades = plan.Sum(e => e.AddedBlades), Vertices = plan.Sum(e => e.AddedBlades) * 7 }, true));
            Debug.Log("Grassland patch bake verification PASSED: bounded curved grass patches; old anchors/paint/UV/faces/GUIDs, "
                + "prefab, collision and other art retained; custom/partial refusal and repeat/manual Setup.");
        }
        [Serializable] sealed class Stats { public int Meshes, Reshaped, Clumps, Blades, Vertices; }
        public static void After()
        {
            VerifyUnmarkedGeometry();
            GrasslandReadabilityVerification.After();
        }
        static void VerifyUnmarkedGeometry()
        {
            Check(Application.dataPath.Replace('\\', '/').Contains("/Logs/"), "isolated partial-geometry fixture");
            var root = PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);
            var importers = GrasslandPatchSetup.Batches(root.GetComponent<IslandTerrain>()).Select(r =>
                AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(r.GetComponent<MeshFilter>().sharedMesh))).ToArray();
            var metadata = importers.Select(i => i.userData).ToArray();
            var hashes = importers.Select(i => Hash(i.assetPath + ".meta")).ToArray();
            try
            {
                Check(metadata.All(GrasslandPatchSetup.Done), "completed sample for interrupted-write simulation");
                foreach (var importer in importers) importer.userData = importer.userData.Replace(";" + GrasslandPatchSetup.Marker, "");
                bool refused = false;
                try { GrasslandPatchSetup.Plan(root.GetComponent<IslandTerrain>()); }
                catch (InvalidOperationException error) { refused = error.Message.Contains("unrecognized grass component"); }
                Check(refused, "geometry written before markers is rejected before any resource mutation");
            }
            finally
            {
                for (int i = 0; i < importers.Length; i++) importers[i].userData = metadata[i];
                PrefabUtility.UnloadPrefabContents(root);
            }
            for (int i = 0; i < importers.Length; i++) Check(Hash(importers[i].assetPath + ".meta") == hashes[i],
                "partial-geometry fixture preserves original importer files");
            Debug.Log("Grassland patch unmarked-geometry verification PASSED: interrupted geometry without markers refused, resources retained.");
        }
        public static bool Authored(Renderer renderer)
        {
            string path = BiomeIslandsSetup.Folder + "/" + renderer.GetComponent<MeshFilter>().sharedMesh.name + ".asset";
            var importer = AssetImporter.GetAtPath(path);
            return importer != null && GrasslandPatchSetup.Done(importer.userData);
        }
        public static IEnumerable<Vector2> PlacementRoots(Renderer renderer, MeadowTouchSettings settings)
        {
            var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
            var original = AssetDatabase.LoadAssetAtPath<Mesh>(Baseline + "/" + mesh.name + ".asset");
            Check(original != null && mesh.vertexCount > original.vertexCount, "new patch placement fixture");
            return mesh.uv2.Skip(original.vertexCount).Distinct()
                .Where(p => Vector2.Distance(p, settings.Center) < settings.AreaRadius - settings.Feather);
        }
        public static void Visit(GameManager g)
        {
            var batches = g.Terrain.Terrain.SeasonalDetails.TouchSample.Renderers.Where(Authored).ToArray();
            if (batches.Length == 0) return;
            Check(batches.Length == 2 && batches.All(r => PlacementRoots(r, g.Terrain.Terrain.SeasonalDetails.TouchSample).Any()),
                "actual AA appended roots in both original cover batches");
            GrasslandReadabilityVerification.Compare(g, Folder, Baseline, "patch", true);
        }
    }
}
