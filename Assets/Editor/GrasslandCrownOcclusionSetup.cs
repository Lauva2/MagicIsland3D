using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MagicIsland.CameraSystem;
using MagicIsland.TerrainSystem;
using UnityEditor;
using UnityEngine;

namespace MagicIsland.EditorTools
{
    public static class GrasslandCrownOcclusionSetup
    {
        public sealed class Entry { public MeshFilter Filter; public LandscapeCrownOccluder.Crown[] Crowns; }
        static void Check(bool value, string why) { if (!value) throw new InvalidOperationException("Crown occlusion: " + why); }
        static bool Finite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);
        public static Entry[] Plan(IslandTerrain terrain)
        {
            Check(terrain.IsValid && terrain.World.ArtVersion == 3, "authored biome islands required");
            // Identify source crowns by authored geometry too: missing metadata must not hide a mesh.
            var filters = terrain.World.Scenery.GetComponentsInChildren<MeshFilter>(true).Where(f => f.name.StartsWith("Landscape ", StringComparison.Ordinal))
                .Where(f => { var colors = f.sharedMesh.colors; return BiomePolishSetup.Components(f.sharedMesh).Any(p => p.Length == 260 && p.All(i => colors[i].g - colors[i].r > .065f)); }).ToArray();
            Check(filters.Length > 0, "refined crowns required");
            var result = new List<Entry>();
            foreach (var filter in filters)
            {
                var mesh = filter.sharedMesh;
                var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(mesh));
                Check(importer.userData == GrasslandCrownWindSetup.Required + ";" + GrasslandCrownWindSetup.Marker, "known complete wind metadata required");
                var renderer = filter.GetComponent<Renderer>();
                Check(renderer != null && renderer.sharedMaterials.Length == 1 && renderer.sharedMaterial.shader.name == "MagicIsland/Island Biome Details", "original shared detail shader required");
                Check(!terrain.GetComponentsInChildren<CameraOccluder>(true).Any(c => c.Renderers != null && c.Renderers.Contains(renderer)), "renderer already owned by CameraOccluder");
                Check(!terrain.GetComponentsInChildren<LandscapeCrownOccluder>(true).Any(c => c.gameObject != filter.gameObject && c.Surface == renderer), "duplicate renderer owner");
                var uv = new List<Vector4>(); mesh.GetUVs(0, uv); var vertices = mesh.vertices;
                Check(uv.Count == vertices.Length, "complete wind UV0 required");
                var colors = mesh.colors;
                var leaves = BiomePolishSetup.Components(mesh).Where(p => p.Length == 260 && p.All(i => colors[i].g - colors[i].r > .065f)).SelectMany(p => p).ToArray();
                Check(leaves.All(i => uv[i].x >= -3 && uv[i].x <= -2 && uv[i].w > 0 && uv[i].w < 1), "every authored leaf retains complete wind data");
                var groups = Enumerable.Range(0, vertices.Length).Where(i => uv[i].x >= -3 && uv[i].x <= -2 && uv[i].w > 0 && uv[i].w < 1).GroupBy(i => uv[i].y).OrderBy(g => g.Key).ToArray();
                Check(groups.Length > 0 && groups.Length <= LandscapeCrownOccluder.MaximumCrowns, "bounded tree groups required");
                var crowns = new List<LandscapeCrownOccluder.Crown>();
                foreach (var group in groups)
                {
                    Check(Finite(group.Key) && crowns.All(c => c.Phase != group.Key), "distinct finite tree phases required");
                    var bounds = new Bounds(vertices[group.First()], Vector3.zero);
                    foreach (int i in group) bounds.Encapsulate(vertices[i]);
                    bounds.Expand(.65f); // Same maximum wind envelope as the source crown mesh.
                    crowns.Add(new LandscapeCrownOccluder.Crown { Phase = group.Key, Bounds = bounds });
                }
                result.Add(new Entry { Filter = filter, Crowns = crowns.ToArray() });
            }
            var existing = result.Select(e => e.Filter.GetComponent<LandscapeCrownOccluder>()).ToArray();
            if (existing.Any(c => c != null))
            {
                Check(existing.All(c => c != null), "incomplete migration retained; repair explicitly");
                for (int i = 0; i < existing.Length; i++)
                {
                    var c = existing[i];
                    Check(c.ArtVersion == 1 && c.Surface == result[i].Filter.GetComponent<Renderer>() && c.Crowns != null && c.Crowns.Length == result[i].Crowns.Length, "unknown or incomplete component retained");
                    for (int j = 0; j < c.Crowns.Length; j++)
                    {
                        var b = c.Crowns[j].Bounds; var expected = result[i].Crowns[j];
                        Check(c.Crowns[j].Phase == expected.Phase && Finite(b.center.sqrMagnitude) && Finite(b.size.sqrMagnitude)
                            && b.Contains(expected.Bounds.min) && b.Contains(expected.Bounds.max), "custom bounds must retain the complete moving crown");
                    }
                }
            }
            return result.ToArray();
        }
        [MenuItem("Magic Island/Art/Prepare Grassland Crown Occlusion (Explicit)")]
        public static void Rebuild()
        {
            Check(!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling, "Edit Mode required");
            string path = TerrainSetup.PrefabPath;
            byte[] before = File.ReadAllBytes(path), meta = File.ReadAllBytes(path + ".meta");
            var root = PrefabUtility.LoadPrefabContents(path); bool saved = false;
            try
            {
                var plan = Plan(root.GetComponent<IslandTerrain>());
                if (plan.All(e => e.Filter.GetComponent<LandscapeCrownOccluder>() != null))
                { Debug.Log("Grassland crown occlusion and valid later hand edits retained."); saved = true; return; }
                foreach (var e in plan)
                {
                    var c = e.Filter.gameObject.AddComponent<LandscapeCrownOccluder>();
                    c.Surface = e.Filter.GetComponent<Renderer>(); c.Crowns = e.Crowns; c.ArtVersion = 1;
                }
                PrefabUtility.SaveAsPrefabAsset(root, path, out bool written);
                Check(written, "prefab save failed");
                File.WriteAllText(path, string.Join("\n", File.ReadAllLines(path).Select(line => line.TrimEnd())) + "\n");
                saved = true;
                Debug.Log("Grassland crown occlusion prepared: " + plan.Sum(e => e.Crowns.Length) + " trees in " + plan.Length + " existing renderers.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
                if (!saved) { File.WriteAllBytes(path, before); File.WriteAllBytes(path + ".meta", meta); AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate); }
            }
        }
    }
}
