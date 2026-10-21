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
    // Editor-only art pass. All additions belong to existing AA cover meshes and their original owner.
    public static class GrasslandPatchSetup
    {
        public const string Marker = "MagicIsland.GrasslandPatch.v1";
        public const float StaticRadius = .82f, MotionRadius = 1.30f;
        static readonly HashSet<string> Known = new HashSet<string>
        {
            GrasslandGroundLayersSetup.Marker, GrasslandTouchSetup.Marker, GrasslandReadabilitySetup.Marker,
            GrasslandBlendSetup.Marker, GrasslandWildflowerSetup.Marker, GrasslandEdgeSetup.Marker, GrasslandPathEdgeSetup.Marker,
            GrasslandSampleFinishSetup.Marker, GrasslandSpatialSetup.Marker, GrasslandShrubDetailSetup.Marker, GrasslandShrubWindSetup.Marker, GrasslandGroundDetailSetup.Marker, GrasslandBladeDetailSetup.Marker, GrasslandFlowerDetailSetup.Marker
        };
        public sealed class Site
        {
            public Vector2 Root;
            public float Phase, Height, Feather;
            public int Blades, Kind;
        }
        public sealed class Edit
        {
            public Mesh Mesh;
            public Vector3[] Vertices, Normals;
            public Color[] Colors;
            public Vector2[][] Channels;
            public int[] Indices;
            public bool[] Changed;
            public Bounds Bounds;
            public string Metadata;
            public int Reshaped, AddedBlades;
            public Site[] Sites;
        }
        public sealed class Neighbor
        {
            public Vector2 Root;
            public bool Grass, Flower;
            public float Radius;
        }
        public static bool Done(string data) => data.Split(';').Contains(Marker);
        public static void Check(bool ok, string why)
        {
            if (!ok) throw new InvalidOperationException("Grassland patches: " + why);
        }
        static float Noise(Vector2 p, float scale, float salt) => GrasslandGroundLayersSetup.Noise(p, scale, salt);
        static float Feather(Vector2 p, MeadowTouchSettings s)
            => 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(s.AreaRadius - s.Feather - 1, s.AreaRadius - 2,
                Vector2.Distance(p, s.Center)));
        public static Renderer[] Batches(IslandTerrain terrain)
            => terrain.SeasonalDetails.TouchSample.Renderers.Where(r => r.name.StartsWith("Meadow undergrowth ", StringComparison.Ordinal)).ToArray();

        static Neighbor[] Neighbors(IslandTerrain terrain, MeadowTouchSettings settings)
        {
            var result = new List<Neighbor>();
            foreach (var r in terrain.SeasonalDetails.MeadowBatches)
            {
                var m = r.GetComponent<MeshFilter>().sharedMesh; var roots = m.uv2; var uv = m.uv; var v = m.vertices;
                Check(roots.Length == v.Length && uv.Length == v.Length, "complete neighboring plant roots");
                foreach (var group in Enumerable.Range(0, roots.Length)
                    .Where(i => (roots[i] - settings.Center).sqrMagnitude < Mathf.Pow(settings.AreaRadius + 5, 2)).GroupBy(i => roots[i]))
                {
                    var ids = group.ToArray();
                    result.Add(new Neighbor { Root = group.Key, Grass = ids.All(i => uv[i].x >= -5 && uv[i].x <= -4),
                        Flower = ids.Any(i => uv[i].x >= -3 && uv[i].x <= -2),
                        Radius = ids.Max(i => Vector2.Distance(new Vector2(v[i].x, v[i].z), group.Key)) });
                }
            }
            return result.ToArray();
        }

        public static Site[] Sites(IslandTerrain terrain, GrasslandRevitalizationSetup.AuthoringWorld world)
        {
            var settings = terrain.SeasonalDetails.TouchSample; var neighbors = Neighbors(terrain, settings);
            var ecology = terrain.World.GetComponentInChildren<WorldWaterEcology>(true);
            var tracks = ecology.RabbitHabitats.SelectMany(h => h.Track).Select(p =>
                terrain.transform.InverseTransformPoint(ecology.transform.TransformPoint(p))).Select(p => new Vector2(p.x, p.z)).ToArray();
            var random = new System.Random(214739); var candidates = new List<(Site site, double rank)>();
            for (float x = settings.Center.x - settings.AreaRadius; x < settings.Center.x + settings.AreaRadius; x += .72f)
                for (float z = settings.Center.y - settings.AreaRadius; z < settings.Center.y + settings.AreaRadius; z += .72f)
                {
                    var p = new Vector2(x + (float)random.NextDouble() * .61f, z + (float)random.NextDouble() * .61f);
                    float draw = (float)random.NextDouble(), phase = (float)random.NextDouble() * Mathf.PI * 2;
                    double rank = random.NextDouble(); float feather = Feather(p, settings);
                    if (feather < .07f || Vector2.Distance(p, settings.Center) > settings.AreaRadius - 2.4f
                        || !BiomeIslandLayout.Contains(p.x, p.y, 20) || BiomeIslandLayout.At(p.x, p.y) != IslandBiome.Grassland
                        || GrasslandIslandMeadowSetup.RouteDistance(p) < 3.45f) continue;
                    float field = Noise(p, .105f, 67) * .72f + Noise(p, .26f, 23) * .28f;
                    float flower = neighbors.Where(n => n.Flower).Select(n => Vector2.Distance(n.Root, p)).DefaultIfEmpty(100).Min();
                    float rock = neighbors.Where(n => !n.Grass && !n.Flower).Select(n => Vector2.Distance(n.Root, p)).DefaultIfEmpty(100).Min();
                    float edge = Mathf.Max(0, 1 - flower / 4.5f);
                    float density = Mathf.SmoothStep(0, .86f, Mathf.InverseLerp(.37f, .67f, field)) + edge * .20f;
                    if (draw > density * feather || neighbors.Any(n => Vector2.Distance(p, n.Root) < (n.Grass ? .48f : n.Radius + MotionRadius))
                        || tracks.Any(q => Vector2.Distance(q, p) < 1.9f)) continue;
                    float floor = world.Floor(p);
                    if (!world.Clear(p, floor, MotionRadius, 1)) continue;
                    bool safe = true;
                    foreach (var d in new[] { Vector2.left, Vector2.right, Vector2.up, Vector2.down })
                        if (Mathf.Abs(world.Floor(p + d * MotionRadius) - floor) > .16f) safe = false;
                    if (!safe) continue;
                    float height = Mathf.Lerp(.17f, .48f, Mathf.InverseLerp(.37f, .72f, field)) + edge * .055f;
                    if (rock < 4) height *= .70f;
                    candidates.Add((new Site { Root = p, Phase = phase, Height = height, Feather = feather,
                        Blades = 6 + random.Next(6), Kind = random.Next(3) }, rank));
                }
            var sites = new List<Site>();
            foreach (var candidate in candidates.OrderBy(c => c.rank))
            {
                if (sites.Any(s => Vector2.Distance(s.Root, candidate.site.Root) < .54f)) continue;
                sites.Add(candidate.site); if (sites.Count == 520) break;
            }
            Check(sites.Count >= 60 && sites.Select(s => s.Kind).Distinct().Count() == 3, "bounded irregular meadow patches " + sites.Count);
            return sites.ToArray();
        }

        public static Edit[] Plan(IslandTerrain terrain)
        {
            var settings = terrain.SeasonalDetails.TouchSample;
            Check(terrain.IsValid && terrain.World.ArtVersion == 3 && settings.ArtVersion == 1
                && terrain.transform.lossyScale == Vector3.one && terrain.transform.rotation == Quaternion.identity,
                "complete original sample and transform");
            var renderers = Batches(terrain);
            Check(renderers.Length == 2 && renderers.Select(r => r.GetComponent<MeshFilter>().sharedMesh).Distinct().Count() == 2,
                "two original undergrowth batches");
            var collisions = new HashSet<Mesh>(terrain.GetComponentsInChildren<MeshCollider>(true).Select(c => c.sharedMesh));
            foreach (var r in renderers)
            {
                var m = r.GetComponent<MeshFilter>().sharedMesh; string path = AssetDatabase.GetAssetPath(m);
                var importer = AssetImporter.GetAtPath(path);
                Check(path.StartsWith(BiomeIslandsSetup.Folder + "/", StringComparison.Ordinal) && !collisions.Contains(m)
                    && r.transform.localToWorldMatrix == terrain.transform.localToWorldMatrix, "owned original visual mesh");
                Check(r.sharedMaterials.Length == 1 && r.sharedMaterial.shader.name == GrasslandFloraSetup.ShaderName, "preserve custom material");
                Check(importer != null && GrasslandReadabilitySetup.Done(importer.userData)
                    && importer.userData.Split(';').All(Known.Contains), "preserve custom metadata or partial migration");
                Check(m.subMeshCount == 1 && m.tangents.Length == 0 && m.uv4.Length == 0
                    && Enumerable.Range(3, 5).All(ch => { var a = new List<Vector4>(); m.GetUVs(ch, a); return a.Count == 0; }),
                    "preserve custom channels and submeshes");
                var weights = m.uv;
                Check(weights.Length == m.vertexCount, "complete original grass weights");
                foreach (var part in BiomePolishSetup.Components(m))
                    if (part.All(i => weights[i].x >= -5 && weights[i].x <= -4))
                        Check(part.Length == 5, "unrecognized grass component or geometry written without its completion marker");
            }
            var world = new GrasslandRevitalizationSetup.AuthoringWorld(terrain,
                new Vector4(settings.Center.x - settings.AreaRadius, settings.Center.y - settings.AreaRadius, settings.AreaRadius * 2, settings.AreaRadius * 2));
            var sites = Sites(terrain, world); var result = new List<Edit>();
            foreach (var renderer in renderers)
            {
                var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                var v = mesh.vertices; var n = mesh.normals; var c = mesh.colors; var uv = mesh.uv; var roots = mesh.uv2; var season = mesh.uv3;
                Check(new[] { n.Length, c.Length, uv.Length, roots.Length, season.Length }.All(l => l == v.Length), "complete original channels");
                var points = v.ToList(); var normals = n.ToList(); var colors = c.ToList();
                var channels = new[] { uv.ToList(), roots.ToList(), season.ToList() }; var indices = mesh.triangles.ToList();
                var edit = new Edit { Mesh = mesh, Changed = new bool[v.Length], Bounds = mesh.bounds,
                    Metadata = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(mesh)).userData };
                var radii = Enumerable.Range(0, v.Length).GroupBy(i => roots[i]).ToDictionary(g => g.Key,
                    g => g.Max(i => Vector2.Distance(new Vector2(v[i].x, v[i].z), g.Key)));
                foreach (var part in BiomePolishSetup.Components(mesh))
                {
                    if (part.Length != 5 || part.Any(i => uv[i].x < -5 || uv[i].x > -4)) continue;
                    var root = roots[part[0]]; float feather = Feather(root, settings);
                    if (feather <= 0) continue;
                    var bases = part.Where(i => Mathf.Abs(uv[i].x + 4) < .0001f).ToArray();
                    Check(bases.Length == 2 && part.All(i => roots[i] == root), "recognizable anchored grass blade");
                    var anchor = (v[bases[0]] + v[bases[1]]) * .5f;
                    var top = part.OrderByDescending(i => v[i].y - world.Floor(new Vector2(v[i].x, v[i].z))).First();
                    float height = v[top].y - world.Floor(new Vector2(v[top].x, v[top].z)) - .018f;
                    Check(height > .03f && height < .75f, "original readable blade height");
                    float phase = uv[top].y + anchor.x * 3.71f + anchor.z * 2.13f;
                    int kind = Mathf.FloorToInt((.5f + .5f * Mathf.Sin(phase)) * 2.999f);
                    float angle = Noise(root, .13f, 44) * Mathf.PI * 2 + Mathf.Sin(phase * 1.7f) * .65f;
                    var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    var side = new Vector2(-direction.y, direction.x);
                    var oldSide = new Vector2(v[bases[1]].x - v[bases[0]].x, v[bases[1]].z - v[bases[0]].z).normalized;
                    float reach = kind == 0 ? .11f + height * .12f : kind == 1 ? .24f + height * .26f : .32f + height * .28f;
                    foreach (int i in part)
                    {
                        if (bases.Contains(i)) continue;
                        var old = new Vector2(v[i].x, v[i].z);
                        float h = Mathf.Max(0, v[i].y - world.Floor(old) - .018f), w = Mathf.Clamp01(h / height);
                        var delta = old - new Vector2(anchor.x, anchor.z);
                        float width = Vector2.Dot(delta, oldSide);
                        var desired = new Vector2(anchor.x, anchor.z) + direction * reach * (w * .25f + w * w * .75f)
                            + side * (width + .045f * Mathf.Sin(phase) * w * w);
                        var radial = desired - root;
                        if (radial.magnitude > radii[root]) desired = root + radial.normalized * radii[root];
                        var next = Vector2.Lerp(old, desired, feather);
                        float drop = kind == 1 ? h * .55f * w * w : 0;
                        points[i] = new Vector3(next.x, world.Floor(next) + .018f + h - drop * feather, next.y);
                        var normal = new Vector3(-direction.x * .24f, 1, -direction.y * .24f).normalized;
                        normals[i] = Vector3.Lerp(n[i], normal, feather).normalized; edit.Changed[i] = true;
                    }
                    edit.Reshaped++;
                }
                string suffix = mesh.name.Substring("Meadow undergrowth ".Length);
                edit.Sites = sites.Where(s => Mathf.FloorToInt(s.Root.x / 128) + " " + Mathf.FloorToInt(s.Root.y / 128) == suffix).ToArray();
                foreach (var site in edit.Sites)
                {
                    float floor = world.Floor(site.Root);
                    for (int blade = 0; blade < site.Blades; blade++)
                    {
                        float phase = site.Phase + blade * 2.399963f;
                        int kind = (site.Kind + blade) % 3;
                        float angle = site.Phase + Mathf.Sin(blade * 3.17f + site.Phase) * .85f;
                        var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)); var side = new Vector2(-direction.y, direction.x);
                        var baseOffset = new Vector2(Mathf.Cos(phase), Mathf.Sin(phase)) * (.10f + blade % 4 * .075f);
                        float height = site.Height * (.75f + .36f * (.5f + .5f * Mathf.Sin(phase))) * Mathf.Lerp(.28f, 1, site.Feather);
                        float reach = kind == 0 ? .10f : kind == 1 ? .28f : .38f;
                        float width = .035f + blade % 3 * .012f;
                        var tint = Color.Lerp(new Color(.30f, .47f, .27f), new Color(.42f, .55f, .32f), Noise(site.Root, .13f, 41));
                        int first = points.Count;
                        void Vertex(float t, float lateral)
                        {
                            float curve = t * .2f + t * t * .8f;
                            var p = site.Root + baseOffset + direction * (reach * curve) + side * (lateral + Mathf.Sin(phase) * .04f * t * t);
                            float h = height * t * (kind == 1 ? 1 - .55f * t * t : 1);
                            Check(Vector2.Distance(p, site.Root) <= StaticRadius && h < .75f, "complete new grass envelope");
                            points.Add(new Vector3(p.x, world.Floor(p) + .018f + h, p.y));
                            normals.Add(new Vector3(-direction.x * .24f, 1, -direction.y * .24f).normalized);
                            var color = tint * Mathf.Lerp(.90f, 1.10f, t); color.a = 1; colors.Add(color);
                            channels[0].Add(new Vector2(-4 - Mathf.Clamp01(h * 1.5f), site.Phase));
                            channels[1].Add(site.Root); channels[2].Add(new Vector2(floor, 1));
                        }
                        Vertex(0, -width); Vertex(0, width); Vertex(.40f, -width * .82f); Vertex(.40f, width * .82f);
                        Vertex(.77f, -width * .42f); Vertex(.77f, width * .42f); Vertex(1, 0);
                        var faces = new[] { 0, 2, 1, 1, 2, 3, 2, 4, 3, 3, 4, 5, 4, 6, 5 };
                        for (int i = 0; i < faces.Length; i += 3)
                        {
                            indices.AddRange(new[] { first + faces[i], first + faces[i + 1], first + faces[i + 2],
                                first + faces[i], first + faces[i + 2], first + faces[i + 1] });
                        }
                        edit.AddedBlades++;
                    }
                }
                edit.Vertices = points.ToArray(); edit.Normals = normals.ToArray(); edit.Colors = colors.ToArray();
                Check(mesh.indexFormat == UnityEngine.Rendering.IndexFormat.UInt32 || points.Count <= 65535,
                    "preserve original index format without overflow");
                edit.Channels = channels.Select(a => a.ToArray()).ToArray(); edit.Indices = indices.ToArray();
                foreach (var p in points.Skip(v.Length).Concat(points.Where((p, i) => i < v.Length && edit.Changed[i])))
                {
                    edit.Bounds.Encapsulate(p + Vector3.one * .35f); edit.Bounds.Encapsulate(p - Vector3.one * .35f);
                }
                result.Add(edit);
            }
            Check(result.Sum(e => e.Sites.Length) == sites.Length && result.Sum(e => e.Reshaped) > 50, "all sites assigned to original batches");
            return result.ToArray();
        }

        static void Clean(string path) => File.WriteAllText(path, string.Join("\n", File.ReadAllLines(path).Select(l => l.TrimEnd())) + "\n");
        [MenuItem("Magic Island/Art/Refine Grassland Meadow Patch Sample (Explicit)")]
        public static void Rebuild()
        {
            Check(!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling, "Edit Mode required");
            var root = PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath); bool saved = false;
            var backups = new Dictionary<string, byte[]>();
            try
            {
                var terrain = root.GetComponent<IslandTerrain>();
                Check(terrain.IsValid && terrain.SeasonalDetails.TouchSample.ArtVersion == 1, "valid prepared sample");
                var batches = Batches(terrain); Check(batches.Length == 2, "original sample batches");
                var done = batches.Select(r => Done(AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(r.GetComponent<MeshFilter>().sharedMesh)).userData)).ToArray();
                if (done.Any(b => b))
                {
                    Check(done.All(b => b), "preserve incomplete patch migration");
                    Debug.Log("Grassland meadow patches and subsequent manual edits retained."); saved = true; return;
                }
                var plan = Plan(terrain);
                foreach (string path in plan.Select(e => AssetDatabase.GetAssetPath(e.Mesh)).SelectMany(p => new[] { p, p + ".meta" })) backups.Add(path, File.ReadAllBytes(path));
                foreach (var e in plan)
                {
                    e.Mesh.SetVertices(e.Vertices); e.Mesh.SetNormals(e.Normals); e.Mesh.SetColors(e.Colors);
                    for (int ch = 0; ch < 3; ch++) e.Mesh.SetUVs(ch, e.Channels[ch]);
                    e.Mesh.SetTriangles(e.Indices, 0, false); e.Mesh.bounds = e.Bounds; EditorUtility.SetDirty(e.Mesh);
                }
                AssetDatabase.SaveAssets();
                foreach (var e in plan)
                {
                    string path = AssetDatabase.GetAssetPath(e.Mesh); var importer = AssetImporter.GetAtPath(path);
                    importer.userData = e.Metadata + ";" + Marker; importer.SaveAndReimport(); Clean(path); Clean(path + ".meta");
                }
                saved = true;
                Debug.Log("Grassland patch sample prepared: " + plan.Sum(e => e.Reshaped) + " reshaped original blades, "
                    + plan.Sum(e => e.Sites.Length) + " new clumps / " + plan.Sum(e => e.AddedBlades) + " curved blades in two original AA batches.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
                if (!saved)
                {
                    foreach (var p in backups) File.WriteAllBytes(p.Key, p.Value);
                    foreach (string p in backups.Keys.Where(p => !p.EndsWith(".meta", StringComparison.Ordinal))) AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate);
                }
            }
        }
    }
}
