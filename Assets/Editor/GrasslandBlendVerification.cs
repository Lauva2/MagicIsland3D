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
    public static class GrasslandBlendVerification
    {
        const string Folder = "Logs/GrasslandBlend", Baseline = "Assets/Editor/GrasslandBlendBaseline";
        static void Check(bool ok, string why)
        {
            if (!ok) throw new InvalidOperationException("Grassland blend verification: " + why);
        }
        static string Hash(string path)
        {
            using (var sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path)));
        }
        static Vector4[] UV(Mesh m, int channel) { var list = new List<Vector4>(); m.GetUVs(channel, list); return list.ToArray(); }
        static void WriteMap(Color32[] pixels, int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            try { texture.SetPixels32(pixels); texture.Apply(); File.WriteAllBytes(GrasslandBlendSetup.MapPath, texture.EncodeToPNG()); }
            finally { Object.DestroyImmediate(texture); }
            AssetDatabase.ImportAsset(GrasslandBlendSetup.MapPath, ImportAssetOptions.ForceUpdate);
        }
        public static void BakeAndVerify()
        {
            Check(Application.dataPath.Replace('\\', '/').Contains("/Logs/"), "isolated project required");
            var root = PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath); GrasslandBlendSetup.PlanData plan;
            MeadowTouchSettings settings;
            try
            {
                var terrain = root.GetComponent<IslandTerrain>(); settings = terrain.SeasonalDetails.TouchSample;
                plan = GrasslandBlendSetup.Plan(terrain);
                var renderer = GrasslandGroundLayersSetup.Grounds(terrain)[0]; var original = renderer.sharedMaterial;
                var custom = new Material(Shader.Find("Standard"));
                try
                {
                    renderer.sharedMaterial = custom; bool refused = false;
                    try { GrasslandBlendSetup.Plan(terrain); } catch (InvalidOperationException) { refused = true; }
                    Check(refused, "custom ground material refused before writes");
                }
                finally { renderer.sharedMaterial = original; Object.DestroyImmediate(custom); }
                var importer = AssetImporter.GetAtPath(GrasslandBlendSetup.MapPath); string metadata = importer.userData;
                try
                {
                    importer.userData = metadata + ";Artist.Custom"; bool refused = false;
                    try { GrasslandBlendSetup.Plan(terrain); } catch (InvalidOperationException) { refused = true; }
                    Check(refused, "custom texture metadata refused before writes");
                    importer.userData = metadata + ";" + GrasslandBlendSetup.Pending; refused = false;
                    try { GrasslandBlendSetup.Rebuild(); } catch (InvalidOperationException) { refused = true; }
                    Check(refused, "pending write marker blocks repeated color application");
                    importer.userData = metadata + ";" + GrasslandBlendSetup.Marker; refused = false;
                    try { GrasslandBlendSetup.Rebuild(); } catch (InvalidOperationException) { refused = true; }
                    Check(refused, "partial completed markers refused before writes");
                }
                finally { importer.userData = metadata; }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            var paths = plan.Meshes.Select(e => AssetDatabase.GetAssetPath(e.Mesh)).ToArray();
            var v = plan.Meshes.Select(e => e.Mesh.vertices).ToArray(); var n = plan.Meshes.Select(e => e.Mesh.normals).ToArray();
            var colors = plan.Meshes.Select(e => e.Mesh.colors).ToArray(); var faces = plan.Meshes.Select(e => e.Mesh.triangles).ToArray();
            var bounds = plan.Meshes.Select(e => e.Mesh.bounds).ToArray();
            var uv = plan.Meshes.Select(e => Enumerable.Range(0, 8).Select(ch => UV(e.Mesh, ch)).ToArray()).ToArray();
            var guids = paths.Select(AssetDatabase.AssetPathToGUID).ToArray(); string mapGuid = AssetDatabase.AssetPathToGUID(GrasslandBlendSetup.MapPath);
            var pixels = GrasslandBlendSetup.ReadPixels(GrasslandBlendSetup.MapPath, out int width, out int height);
            if (!AssetDatabase.IsValidFolder(Baseline)) AssetDatabase.CreateFolder("Assets/Editor", "GrasslandBlendBaseline");
            foreach (string path in paths.Concat(new[] { GrasslandBlendSetup.MapPath }))
            {
                string target = Baseline + "/" + Path.GetFileName(path);
                Check(!File.Exists(target), "fresh comparison fixture, preserve existing custom data");
                Check(AssetDatabase.CopyAsset(path, target), "original comparison fixture");
            }
            var excluded = new HashSet<string>(paths.Concat(new[] { GrasslandBlendSetup.MapPath }).SelectMany(p => new[] { p, p + ".meta" }));
            var protectedFiles = Directory.GetFiles("Assets/Art", "*", SearchOption.AllDirectories)
                .Select(p => p.Replace('\\', '/')).Where(p => !excluded.Contains(p)).ToArray(); var hashes = protectedFiles.Select(Hash).ToArray();
            GrasslandBlendSetup.Rebuild();
            for (int e = 0; e < plan.Meshes.Length; e++)
            {
                var mesh = plan.Meshes[e].Mesh; var actual = mesh.colors;
                Check(AssetDatabase.AssetPathToGUID(paths[e]) == guids[e] && mesh.vertices.SequenceEqual(v[e])
                    && mesh.normals.SequenceEqual(n[e]) && mesh.triangles.SequenceEqual(faces[e]) && mesh.bounds == bounds[e],
                    "exact original geometry, normals, faces, bounds and GUID");
                for (int ch = 0; ch < 8; ch++) Check(UV(mesh, ch).SequenceEqual(uv[e][ch]), "all eight original UV channels retained");
                Check(actual.SequenceEqual(plan.Meshes[e].Colors), "exact planned color gradient");
                for (int i = 0; i < actual.Length; i++)
                {
                    Check(actual[i].a == colors[e][i].a, "all original alpha retained");
                    if (!plan.Meshes[e].Changed[i]) Check(actual[i] == colors[e][i], "flowers, rocks, other plants and outside sample retain paint");
                    else Check(Vector2.Distance(new Vector2(v[e][i].x, v[e][i].z), settings.Center) < settings.AreaRadius,
                        "every recolored vertex remains in the sample");
                }
            }
            var finalPixels = GrasslandBlendSetup.ReadPixels(GrasslandBlendSetup.MapPath, out int finalWidth, out int finalHeight);
            Check(width == finalWidth && height == finalHeight && finalPixels.SequenceEqual(plan.Pixels)
                && AssetDatabase.AssetPathToGUID(GrasslandBlendSetup.MapPath) == mapGuid, "exact planned RGBA pixels, dimensions and texture GUID");
            float reach = GrasslandBlendSetup.TexelReach(width, height, plan.Rect);
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                int i = y * width + x; var p = GrasslandBlendSetup.PixelWorld(x, y, width, height, plan.Rect);
                Check(finalPixels[i].a == pixels[i].a, "all original map alpha preserved");
                if (!plan.Changed[i]) Check(finalPixels[i].Equals(pixels[i]), "all unselected pixels retained exactly");
                else Check(Vector2.Distance(p, settings.Center) + reach < settings.AreaRadius + .001f
                    && GrasslandIslandMeadowSetup.RouteDistance(p) - reach > 2.1f,
                    "full bilinear sampling support stays inside sample and outside road center");
            }
            for (int i = 0; i < protectedFiles.Length; i++) Check(Hash(protectedFiles[i]) == hashes[i],
                "ground geometry/collision, materials/shaders, prefab, other ecology/islands retained " + protectedFiles[i]);
            int edited = Array.FindIndex(plan.Meshes, e => e.Changed.Any(b => b));
            string first = paths[edited]; byte[] savedMesh = File.ReadAllBytes(first), savedMap = File.ReadAllBytes(GrasslandBlendSetup.MapPath);
            try
            {
                var mesh = plan.Meshes[edited].Mesh; var paint = mesh.colors;
                paint[Array.FindIndex(plan.Meshes[edited].Changed, b => b)].g *= .99f;
                mesh.colors = paint; EditorUtility.SetDirty(mesh); AssetDatabase.SaveAssets();
                var handMap = finalPixels.ToArray(); handMap[0].r ^= 1; WriteMap(handMap, width, height);
                var all = Directory.GetFiles("Assets/Art", "*", SearchOption.AllDirectories); var manual = all.Select(Hash).ToArray();
                GrasslandBlendSetup.Rebuild(); GrasslandPatchSetup.Rebuild(); GrasslandReadabilitySetup.Rebuild();
                GrasslandTouchSetup.Rebuild(); GrasslandGroundLayersSetup.Rebuild(); GrasslandFloraSetup.Rebuild(); PrototypeAddressablesSetup.EnsureSetup();
                for (int i = 0; i < all.Length; i++) Check(Hash(all[i]) == manual[i], "repeat/old entries/Setup preserve grass paint and hand-painted map " + all[i]);
            }
            finally
            {
                File.WriteAllBytes(first, savedMesh); File.WriteAllBytes(GrasslandBlendSetup.MapPath, savedMap);
                AssetDatabase.ImportAsset(first, ImportAssetOptions.ForceUpdate); AssetDatabase.ImportAsset(GrasslandBlendSetup.MapPath, ImportAssetOptions.ForceUpdate);
            }
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Folder + "/Stats.json", JsonUtility.ToJson(new Stats { Meshes = plan.Meshes.Length,
                Pixels = plan.Changed.Count(b => b), Colors = plan.Meshes.Sum(e => e.Changed.Count(b => b)),
                GrassClumps = plan.Plants.Count(p => p.Grass), AccentClumps = plan.Plants.Count(p => !p.Grass) }, true));
            Debug.Log("Grassland blend bake verification PASSED: exact local pixels/alpha/bilinear boundaries, grass-only color gradient; "
                + "all geometry/UV/other art retained, custom/pending/partial refusal, repeat/manual Setup.");
        }
        [Serializable] sealed class Stats { public int Meshes, Pixels, Colors, GrassClumps, AccentClumps; }
        public static void After() => GrasslandPatchVerification.After();
        static Color32[] GPU(Texture texture)
        {
            var target = new RenderTexture(texture.width, texture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var readback = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false, true); var active = RenderTexture.active;
            try
            {
                Graphics.Blit(texture, target); RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); readback.Apply(); return readback.GetPixels32();
            }
            finally { RenderTexture.active = active; Object.DestroyImmediate(target); Object.DestroyImmediate(readback); }
        }
        public static void Visit(GameManager g)
        {
            if (!GrasslandBlendSetup.Done(AssetImporter.GetAtPath(GrasslandBlendSetup.MapPath).userData)) return;
            var grounds = GrasslandGroundLayersSetup.Grounds(g.Terrain.Terrain);
            var original = grounds.Select(r => { var b = new MaterialPropertyBlock(); r.GetPropertyBlock(b); return b; }).ToArray();
            var baseline = AssetDatabase.LoadAssetAtPath<Texture2D>(Baseline + "/MeadowUnderlay.png");
            var source = AssetDatabase.LoadAssetAtPath<Texture2D>(GrasslandBlendSetup.MapPath);
            var actual = grounds[0].sharedMaterial.GetTexture("_UnderlayMap");
            Check(baseline != null && actual != null && GPU(actual).SequenceEqual(GPU(source)), "actual AA contains the final GPU ground map");
            Check(!GPU(actual).SequenceEqual(GPU(baseline)), "final ground pixels differ from the before fixture");
            int mapId = Shader.PropertyToID("_UnderlayMap"); var block = new MaterialPropertyBlock();
            void Swap(bool before)
            {
                for (int i = 0; i < grounds.Length; i++)
                {
                    if (!before) { grounds[i].SetPropertyBlock(original[i]); continue; }
                    grounds[i].GetPropertyBlock(block); block.SetTexture(mapId, baseline); grounds[i].SetPropertyBlock(block);
                }
            }
            try { GrasslandReadabilityVerification.Compare(g, Folder, Baseline, "blend", false, Swap); }
            finally { Swap(false); }
        }
    }
}
