using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MagicIsland.Interaction;
using MagicIsland.Ocean;
using MagicIsland.TerrainSystem;
using UnityEditor;
using UnityEngine;

namespace MagicIsland.EditorTools
{
    public static class GrasslandWildflowerSetup
    {
        public const string Marker = "MagicIsland.GrasslandWildflower.v1", Pending = "MagicIsland.GrasslandWildflower.Pending.v1";
        static readonly HashSet<string> Known = new HashSet<string>
        {
            GrasslandRevitalizationSetup.Marker, GrasslandIslandMeadowSetup.Marker, GrasslandGroveSetup.Marker,
            GrasslandFloraSetup.Marker, GrasslandGroundLayersSetup.Marker, GrasslandShrubStoneSetup.Marker,
            GrasslandTouchSetup.Marker, GrasslandReadabilitySetup.Marker, GrasslandPatchSetup.Marker, GrasslandBlendSetup.Marker
        };
        static readonly Color[] Palette = { new Color(.96f,.935f,.82f), new Color(.95f,.87f,.60f), new Color(.79f,.73f,.88f) };
        public sealed class Site { public Vector2 Root; public float Phase, Scale; public int Stems, Color, Branch; }
        public sealed class Edit
        {
            public Mesh Mesh; public Vector3[] Vertices, Normals; public Color[] Colors; public Vector2[][] Channels;
            public int[] Indices; public bool[] Changed, Removed; public Bounds Bounds; public string Metadata;
            public int Bouquets, RetainedStems, RemovedStems; public Site[] Sites;
        }
        public static bool Done(string data) => data.Split(';').Contains(Marker);
        public static void Check(bool ok, string why) { if (!ok) throw new InvalidOperationException("Grassland wildflowers: " + why); }
        static float Noise(Vector2 p, float scale, float salt) => GrasslandGroundLayersSetup.Noise(p, scale, salt);
        static bool Flower(float x) => x >= -3 && x <= -2;
        static float Feather(Vector2 p, MeadowTouchSettings s) => 1 - Mathf.SmoothStep(0, 1,
            Mathf.InverseLerp(s.AreaRadius - s.Feather, s.AreaRadius - 1.5f, Vector2.Distance(p, s.Center)));

        static Site[] Sites(IslandTerrain terrain, GrasslandRevitalizationSetup.AuthoringWorld world)
        {
            var settings = terrain.SeasonalDetails.TouchSample; var plants = new List<(Vector2 root, bool grass, float radius)>();
            foreach (var renderer in terrain.SeasonalDetails.MeadowBatches)
            {
                var m = renderer.GetComponent<MeshFilter>().sharedMesh; var v = m.vertices; var uv = m.uv; var roots = m.uv2;
                Check(roots.Length == v.Length && uv.Length == v.Length, "neighboring roots");
                foreach (var group in Enumerable.Range(0, v.Length).Where(i => (roots[i] - settings.Center).sqrMagnitude < 900).GroupBy(i => roots[i]))
                {
                    var ids = group.ToArray(); plants.Add((group.Key, ids.All(i => uv[i].x >= -5 && uv[i].x <= -4),
                        ids.Max(i => Vector2.Distance(new Vector2(v[i].x, v[i].z), group.Key))));
                }
            }
            var ecology = terrain.World.GetComponentInChildren<WorldWaterEcology>(true);
            var tracks = ecology.RabbitHabitats.SelectMany(h => h.Track).Select(p => terrain.transform.InverseTransformPoint(ecology.transform.TransformPoint(p)))
                .Select(p => new Vector2(p.x, p.z)).ToArray();
            var grass = plants.Where(p => p.grass).ToArray(); var accents = plants.Where(p => !p.grass).ToArray();
            var random = new System.Random(218331); var candidates = new List<(Site site, double rank)>();
            for (float x = settings.Center.x - 22; x < settings.Center.x + 22; x += .95f)
                for (float z = settings.Center.y - 22; z < settings.Center.y + 22; z += .95f)
                {
                    var p = new Vector2(x + (float)random.NextDouble() * .8f, z + (float)random.NextDouble() * .8f);
                    float draw = (float)random.NextDouble(), phase = (float)random.NextDouble() * Mathf.PI * 2;
                    double rank = random.NextDouble();
                    if (Feather(p, settings) < .35f || Vector2.Distance(p, settings.Center) > settings.AreaRadius - 2.5f
                        || GrasslandIslandMeadowSetup.RouteDistance(p) < 3.5f || !BiomeIslandLayout.Contains(p.x, p.y, 20)) continue;
                    float density = 0;
                    foreach (var plant in grass) { float d = (plant.root - p).sqrMagnitude; if (d < 16) density += Mathf.Exp(-d / 3.2f); }
                    float edge = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.8f, 2.6f, density))
                        * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(4.6f, 7, density)));
                    float ribbon = Noise(p, .17f, 38) * .7f + Noise(p, .37f, 81) * .3f;
                    if (draw > edge * Mathf.SmoothStep(0, .65f, Mathf.InverseLerp(.41f, .68f, ribbon))) continue;
                    if (accents.Any(a => Vector2.Distance(a.root, p) < a.radius + 1.3f)
                        || grass.Any(a => Vector2.Distance(a.root, p) < .40f) || tracks.Any(q => Vector2.Distance(q, p) < 1.9f)) continue;
                    float floor = world.Floor(p); if (!world.Clear(p, floor, 1.3f, 1.15f)) continue;
                    bool safe = true;
                    foreach (var d in new[] { Vector2.left, Vector2.right, Vector2.up, Vector2.down })
                        if (Mathf.Abs(world.Floor(p + d * 1.3f) - floor) > .16f) safe = false;
                    if (!safe) continue;
                    int color = random.Next(10); int stems = draw < .11f ? 1 : draw < .28f ? 2 : 3;
                    candidates.Add((new Site { Root = p, Phase = phase, Scale = .62f + (float)random.NextDouble() * .18f,
                        Stems = stems, Color = color < 5 ? 0 : color < 9 ? 1 : 2, Branch = random.Next(7) }, rank));
                }
            var result = new List<Site>();
            foreach (var candidate in candidates.OrderBy(c => c.rank))
            {
                if (result.Any(s => Vector2.Distance(s.Root, candidate.site.Root) < 1.2f)) continue;
                result.Add(candidate.site); if (result.Count == 64) break;
            }
            Check(result.Count >= 12 && result.Any(s => s.Stems == 1) && result.Any(s => s.Stems > 1), "bounded interrupted flower ribbons " + result.Count);
            return result.ToArray();
        }

        public static Edit[] Plan(IslandTerrain terrain)
        {
            var settings = terrain.SeasonalDetails.TouchSample;
            Check(terrain.IsValid && settings.ArtVersion == 1 && settings.Renderers.Length == 6
                && terrain.transform.lossyScale == Vector3.one && terrain.transform.rotation == Quaternion.identity, "prepared original sample");
            var collisions = new HashSet<Mesh>(terrain.GetComponentsInChildren<MeshCollider>(true).Select(c => c.sharedMesh));
            foreach (var r in settings.Renderers)
            {
                var mesh = r.GetComponent<MeshFilter>().sharedMesh; var path = AssetDatabase.GetAssetPath(mesh); var data = AssetImporter.GetAtPath(path).userData;
                Check(path.StartsWith(BiomeIslandsSetup.Folder + "/", StringComparison.Ordinal) && !collisions.Contains(mesh)
                    && r.transform.localToWorldMatrix == terrain.transform.localToWorldMatrix && mesh.subMeshCount == 1
                    && mesh.tangents.Length == 0 && Enumerable.Range(3, 5).All(ch => { var a = new List<Vector4>(); mesh.GetUVs(ch, a); return a.Count == 0; }),
                    "owned original visual mesh and complete known channels");
                Check(r.sharedMaterials.Length == 1 && r.sharedMaterial.shader.name == GrasslandFloraSetup.ShaderName
                    && GrasslandBlendSetup.Done(data) && data.Split(';').All(Known.Contains), "preserve custom material/metadata or partial migration");
            }
            var world = new GrasslandRevitalizationSetup.AuthoringWorld(terrain,
                new Vector4(settings.Center.x - 25, settings.Center.y - 25, 50, 50));
            var sites = Sites(terrain, world);
            var template = JsonUtility.FromJson<BeachCompositionSetup.Export>(File.ReadAllText(GrasslandCompositionSetup.Source)).templates.Single(t => t.name == "Flowers");
            Check(template.positions.Length == 1995 * 3 && template.normals.Length == template.positions.Length
                && template.colors.Length == 1995 * 4, "original seven-branch Blender flower template");
            var result = new List<Edit>();
            foreach (var renderer in settings.Renderers)
            {
                var mesh = renderer.GetComponent<MeshFilter>().sharedMesh; var v = mesh.vertices; var n = mesh.normals;
                var c = mesh.colors; var uv = mesh.uv; var roots = mesh.uv2; var season = mesh.uv3; var faces = mesh.triangles;
                Check(new[] { n.Length, c.Length, uv.Length, roots.Length, season.Length }.All(l => l == v.Length), "complete flower channels");
                var points = v.ToList(); var normals = n.ToList(); var colors = c.ToList();
                var channels = new[] { uv.ToList(), roots.ToList(), season.ToList() };
                var edit = new Edit { Mesh = mesh, Changed = new bool[v.Length], Removed = new bool[v.Length], Bounds = mesh.bounds,
                    Metadata = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(mesh)).userData };
                foreach (var group in Enumerable.Range(0, v.Length).Where(i => Flower(uv[i].x)).GroupBy(i => roots[i]))
                {
                    var root = group.Key; float feather = Feather(root, settings); if (feather <= 0) continue;
                    var ids = group.OrderBy(i => i).ToArray();
                    int perStem = ids.Length == 1995 ? 285 : 96;
                    Check(ids.Length == 1995 || ids.Length == 672 || ids.Length == 192, "recognizable complete seven/two-stem original bouquet");
                    int count = ids.Length / perStem;
                    Check(ids.Zip(ids.Skip(1), (a, b) => b == a + 1).All(b => b), "original contiguous flower branches");
                    float field = Noise(root, .21f, 34); int keep = count == 2 ? 2 : field < .30f ? 1 : field < .58f ? 2 : field < .79f ? 3 : 5;
                    if (feather < .75f) keep = count;
                    int offset = Mathf.FloorToInt(Noise(root, .29f, 91) * count) % count;
                    var chosen = Enumerable.Range(0, keep).Select(i => (offset + i * 3) % count).ToHashSet();
                    if (count == 2) chosen = new HashSet<int>(new[] { 0, 1 });
                    float oldRadius = ids.Max(i => Vector2.Distance(new Vector2(v[i].x, v[i].z), root));
                    for (int branch = 0; branch < count; branch++)
                    {
                        var stem = ids.Skip(branch * perStem).Take(perStem).ToArray();
                        bool retain = chosen.Contains(branch);
                        foreach (int i in stem) edit.Removed[i] = !retain;
                        if (!retain) { edit.RemovedStems++; continue; }
                        var basePoint = stem.OrderBy(i => v[i].y - world.Floor(new Vector2(v[i].x, v[i].z))).First();
                        var anchor = new Vector2(v[basePoint].x, v[basePoint].z);
                        float phase = uv[stem[0]].y + branch * 2.399963f;
                        float scale = Mathf.Lerp(1, .70f + .24f * (.5f + .5f * Mathf.Sin(phase)), feather);
                        float angle = Mathf.Lerp(0, Mathf.Sin(phase * 1.31f) * .25f, feather);
                        var rotation = Quaternion.Euler(0, angle * Mathf.Rad2Deg, 0);
                        var shift = new Vector2(Mathf.Cos(phase), Mathf.Sin(phase)) * (.10f * feather);
                        foreach (int i in stem)
                        {
                            var old = new Vector2(v[i].x, v[i].z); float h = Mathf.Max(0, v[i].y - world.Floor(old) - .018f);
                            var delta = rotation * new Vector3(old.x - anchor.x, 0, old.y - anchor.y) * scale;
                            var next = anchor + new Vector2(delta.x, delta.z) + shift;
                            var radial = next - root; if (radial.magnitude > oldRadius) next = root + radial.normalized * oldRadius;
                            points[i] = new Vector3(next.x, world.Floor(next) + .018f + h * scale, next.y);
                            normals[i] = rotation * n[i];
                            bool petal = c[i].b > c[i].g || c[i].r > .92f;
                            if (petal)
                            {
                                int color = field < .48f ? 0 : field < .87f ? 1 : 2;
                                var tint = Palette[color]; tint.a = c[i].a; colors[i] = Color.Lerp(c[i], tint, feather);
                            }
                            edit.Changed[i] = true;
                        }
                        edit.RetainedStems++;
                    }
                    edit.Bouquets++;
                }
                var indices = new List<int>();
                for (int i = 0; i < faces.Length; i += 3)
                {
                    int removed = (edit.Removed[faces[i]] ? 1 : 0) + (edit.Removed[faces[i + 1]] ? 1 : 0) + (edit.Removed[faces[i + 2]] ? 1 : 0);
                    Check(removed == 0 || removed == 3, "whole branch faces only");
                    if (removed == 0) indices.AddRange(new[] { faces[i], faces[i + 1], faces[i + 2] });
                }
                bool undergrowth = renderer.name.StartsWith("Meadow undergrowth ", StringComparison.Ordinal);
                edit.Sites = undergrowth ? sites.Where(s => mesh.name.EndsWith(Mathf.FloorToInt(s.Root.x / 128) + " " + Mathf.FloorToInt(s.Root.y / 128), StringComparison.Ordinal)).ToArray() : Array.Empty<Site>();
                foreach (var site in edit.Sites)
                {
                    for (int member = 0; member < site.Stems; member++)
                    {
                        int branch = (site.Branch + member * 3) % 7, begin = branch * 285; var selected = Enumerable.Range(begin, 285).ToArray();
                        float minY = selected.Min(i => template.positions[i * 3 + 1]);
                        var baseIds = selected.Where(i => template.positions[i * 3 + 1] <= minY + .001f).ToArray();
                        var anchor = new Vector3(baseIds.Average(i => template.positions[i * 3]), 0, baseIds.Average(i => template.positions[i * 3 + 2]));
                        var rotation = Quaternion.Euler(0, site.Phase * Mathf.Rad2Deg + member * 73, 0);
                        var shift = new Vector2(Mathf.Cos(site.Phase + member * 2.4f), Mathf.Sin(site.Phase + member * 2.4f)) * (site.Stems == 1 ? 0 : .16f);
                        var lookup = new Dictionary<(Vector3, Vector3, Color), int>(); var map = new Dictionary<int, int>();
                        foreach (int i in selected)
                        {
                            var q = new Vector3(template.positions[i * 3], template.positions[i * 3 + 1], template.positions[i * 3 + 2]) - anchor;
                            q = rotation * q * site.Scale; var p = site.Root + new Vector2(q.x, q.z) + shift;
                            Check(Vector2.Distance(p, site.Root) <= .65f && q.y < .85f, "bounded new flower shape");
                            var normal = rotation * new Vector3(template.normals[i * 3], template.normals[i * 3 + 1], template.normals[i * 3 + 2]);
                            var color = new Color(template.colors[i * 4], template.colors[i * 4 + 1], template.colors[i * 4 + 2], 1);
                            bool petal = color.b > color.g || color.r > .95f, pollen = color.r > .85f && color.g < .7f;
                            if (petal) color = Palette[site.Color]; else if (pollen) color = new Color(.88f,.70f,.36f,1);
                            var point = new Vector3(p.x, world.Floor(p) + .018f + q.y, p.y); var key = (point, normal, color);
                            if (!lookup.TryGetValue(key, out int id))
                            {
                                id = points.Count; lookup.Add(key, id); points.Add(point); normals.Add(normal.normalized); colors.Add(color);
                                channels[0].Add(new Vector2(-2 - Mathf.Clamp01(q.y), site.Phase)); channels[1].Add(site.Root);
                                channels[2].Add(new Vector2(world.Floor(site.Root), petal || pollen ? 0 : 1));
                            }
                            map.Add(i, id);
                        }
                        for (int i = 0; i < template.triangles.Length; i += 3)
                        {
                            if (!map.ContainsKey(template.triangles[i])) continue;
                            Check(map.ContainsKey(template.triangles[i + 1]) && map.ContainsKey(template.triangles[i + 2]), "complete source stem");
                            indices.AddRange(new[] { map[template.triangles[i]], map[template.triangles[i + 1]], map[template.triangles[i + 2]] });
                        }
                    }
                }
                Check(mesh.indexFormat == UnityEngine.Rendering.IndexFormat.UInt32 || points.Count <= 65535, "preserve original index format");
                edit.Vertices = points.ToArray(); edit.Normals = normals.ToArray(); edit.Colors = colors.ToArray();
                edit.Channels = channels.Select(a => a.ToArray()).ToArray(); edit.Indices = indices.ToArray();
                foreach (var p in points.Skip(v.Length).Concat(points.Where((p, i) => i < v.Length && edit.Changed[i])))
                { edit.Bounds.Encapsulate(p + Vector3.one * .35f); edit.Bounds.Encapsulate(p - Vector3.one * .35f); }
                result.Add(edit);
            }
            Check(result.Sum(e => e.Sites.Length) == sites.Length && result.Sum(e => e.Bouquets) > 0 && result.Sum(e => e.RemovedStems) > 0,
                "recognized bouquets and assigned flower ribbon sites");
            return result.ToArray();
        }
        static void Clean(string p) => File.WriteAllText(p, string.Join("\n", File.ReadAllLines(p).Select(l => l.TrimEnd())) + "\n");
        [MenuItem("Magic Island/Art/Refine Grassland Wildflower Sample (Explicit)")]
        public static void Rebuild()
        {
            Check(!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling, "Edit Mode required");
            var root = PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath); var backups = new Dictionary<string, byte[]>(); bool saved = false;
            try
            {
                var terrain = root.GetComponent<IslandTerrain>();
                Check(terrain.IsValid && terrain.SeasonalDetails.TouchSample.ArtVersion == 1 && terrain.SeasonalDetails.TouchSample.Renderers.Length == 6, "valid sample");
                var paths = terrain.SeasonalDetails.TouchSample.Renderers.Select(r => AssetDatabase.GetAssetPath(r.GetComponent<MeshFilter>().sharedMesh)).ToArray();
                var data = paths.Select(p => AssetImporter.GetAtPath(p).userData).ToArray();
                Check(!data.Any(d => d.Split(';').Contains(Pending)), "preserve interrupted write; do not repeat partial thinning");
                if (data.Any(Done)) { Check(data.All(Done), "preserve partial flower migration"); saved = true; Debug.Log("Grassland wildflowers and manual edits retained."); return; }
                var plan = Plan(terrain);
                foreach (var p in paths.SelectMany(p => new[] { p, p + ".meta" })) backups.Add(p, File.ReadAllBytes(p));
                var first = AssetImporter.GetAtPath(paths[0]); first.userData = data[0] + ";" + Pending; first.SaveAndReimport();
                foreach (var e in plan)
                {
                    e.Mesh.SetVertices(e.Vertices); e.Mesh.SetNormals(e.Normals); e.Mesh.SetColors(e.Colors);
                    for (int ch = 0; ch < 3; ch++) e.Mesh.SetUVs(ch, e.Channels[ch]);
                    e.Mesh.SetTriangles(e.Indices, 0, false); e.Mesh.bounds = e.Bounds; EditorUtility.SetDirty(e.Mesh);
                }
                AssetDatabase.SaveAssets();
                foreach (var e in plan.Skip(1).Concat(plan.Take(1)))
                {
                    string path = AssetDatabase.GetAssetPath(e.Mesh); var importer = AssetImporter.GetAtPath(path);
                    importer.userData = e.Metadata + ";" + Marker; importer.SaveAndReimport(); Clean(path); Clean(path + ".meta");
                }
                saved = true;
                Debug.Log("Wildflower sample prepared: " + plan.Sum(e => e.Bouquets) + " original bouquets, " + plan.Sum(e => e.RemovedStems)
                    + " removed stems; " + sitesCount(plan) + " new groups / " + plan.Sum(e => e.Sites.Sum(s => s.Stems)) + " new stems.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
                if (!saved) { foreach (var p in backups) File.WriteAllBytes(p.Key, p.Value); foreach (var p in backups.Keys.Where(p => !p.EndsWith(".meta", StringComparison.Ordinal))) AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate); }
            }
        }
        static int sitesCount(Edit[] plan) => plan.Sum(e => e.Sites.Length);
    }
}
