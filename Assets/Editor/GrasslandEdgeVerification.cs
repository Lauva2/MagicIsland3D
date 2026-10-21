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
    public static class GrasslandEdgeVerification
    {
        const string Folder = "Logs/GrasslandEdge", Baseline = "Assets/Editor/GrasslandEdgeBaseline";
        static void Check(bool ok, string why) { if (!ok) throw new InvalidOperationException("Edge verification: " + why); }
        static string Hash(string p) { using (var sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(p))); }
        static Vector4[] UV(Mesh m, int channel) { var a = new List<Vector4>(); m.GetUVs(channel, a); return a.ToArray(); }
        public static void BakeAndVerify()
        {
            Check(Application.dataPath.Replace('\\', '/').Contains("/Logs/"), "isolated project required");
            var root = PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath); GrasslandEdgeSetup.Edit[] plan; MeadowTouchSettings settings;
            try
            {
                var terrain = root.GetComponent<IslandTerrain>(); settings = terrain.SeasonalDetails.TouchSample;
                plan = GrasslandEdgeSetup.Plan(terrain);
                var r = settings.Renderers[0]; var original = r.sharedMaterial; var custom = new Material(Shader.Find("Standard"));
                try
                {
                    r.sharedMaterial = custom; bool refused = false;
                    try { GrasslandEdgeSetup.Plan(terrain); } catch (InvalidOperationException) { refused = true; }
                    Check(refused, "custom material refused before writes");
                }
                finally { r.sharedMaterial = original; Object.DestroyImmediate(custom); }
                var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(plan[0].Mesh)); string metadata = importer.userData;
                try
                {
                    importer.userData = metadata + ";Artist.Custom"; bool refused = false;
                    try { GrasslandEdgeSetup.Plan(terrain); } catch (InvalidOperationException) { refused = true; }
                    Check(refused, "unknown metadata refused before writes");
                    foreach (string marker in new[] { GrasslandEdgeSetup.Pending, GrasslandEdgeSetup.Marker })
                    {
                        importer.userData = metadata + ";" + marker; refused = false;
                        try { GrasslandEdgeSetup.Rebuild(); } catch (InvalidOperationException) { refused = true; }
                        Check(refused, "pending or partial migration refused before writes");
                    }
                }
                finally { importer.userData = metadata; }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            var paths = plan.Select(e => AssetDatabase.GetAssetPath(e.Mesh)).ToArray();
            var vertices = plan.Select(e => e.Mesh.vertices).ToArray(); var normals = plan.Select(e => e.Mesh.normals).ToArray();
            var colors = plan.Select(e => e.Mesh.colors).ToArray(); var faces = plan.Select(e => e.Mesh.triangles).ToArray();
            var channels = plan.Select(e => Enumerable.Range(0, 8).Select(ch => UV(e.Mesh, ch)).ToArray()).ToArray();
            var guids = paths.Select(AssetDatabase.AssetPathToGUID).ToArray(); var formats = plan.Select(e => e.Mesh.indexFormat).ToArray();
            if (!AssetDatabase.IsValidFolder(Baseline)) AssetDatabase.CreateFolder("Assets/Editor", "GrasslandEdgeBaseline");
            foreach (string p in paths)
            {
                string target = Baseline + "/" + Path.GetFileName(p);
                if (File.Exists(target))
                {
                    var fixture = AssetDatabase.LoadAssetAtPath<Mesh>(target); var source = AssetDatabase.LoadAssetAtPath<Mesh>(p);
                    Check(fixture != null && fixture.vertices.SequenceEqual(source.vertices) && fixture.normals.SequenceEqual(source.normals)
                        && fixture.colors.SequenceEqual(source.colors) && fixture.triangles.SequenceEqual(source.triangles)
                        && Enumerable.Range(0, 8).All(ch => UV(fixture, ch).SequenceEqual(UV(source, ch))), "preserve exact original comparison fixture");
                }
                else Check(AssetDatabase.CopyAsset(p, target), "original comparison fixture");
            }
            var excluded = new HashSet<string>(paths.SelectMany(p => new[] { p, p + ".meta" }));
            var protectedFiles = Directory.GetFiles("Assets/Art", "*", SearchOption.AllDirectories).Select(p => p.Replace('\\', '/'))
                .Where(p => !excluded.Contains(p)).ToArray(); var hashes = protectedFiles.Select(Hash).ToArray();
            GrasslandEdgeSetup.Rebuild();
            for (int e = 0; e < plan.Length; e++)
            {
                var mesh = plan[e].Mesh; var v = mesh.vertices; var n = mesh.normals; var c = mesh.colors;
                Check(AssetDatabase.AssetPathToGUID(paths[e]) == guids[e] && mesh.indexFormat == formats[e]
                    && v.SequenceEqual(plan[e].Vertices) && n.SequenceEqual(plan[e].Normals) && c.SequenceEqual(plan[e].Colors)
                    && mesh.triangles.SequenceEqual(plan[e].Indices) && mesh.bounds == plan[e].Bounds, "exact planned original-GUID meshes");
                for (int ch = 0; ch < 8; ch++) Check(UV(mesh, ch).Take(channels[e][ch].Length).SequenceEqual(channels[e][ch])
                    && (ch < 3 || UV(mesh, ch).Length == 0), "all original UV channels and root ownership retained");
                var retained = new List<int>();
                for (int i = 0; i < faces[e].Length; i += 3)
                {
                    int a = faces[e][i], b = faces[e][i + 1], d = faces[e][i + 2];
                    Check(plan[e].Removed[a] == plan[e].Removed[b] && plan[e].Removed[a] == plan[e].Removed[d], "no partial stem triangles removed");
                    if (!plan[e].Removed[a]) retained.AddRange(new[] { a, b, d });
                }
                Check(mesh.triangles.Take(retained.Count).SequenceEqual(retained), "exact remaining old face order before new stems");
                for (int i = 0; i < vertices[e].Length; i++)
                {
                    Check(c[i].a == colors[e][i].a, "original alpha retained");
                    if (!plan[e].Changed[i]) Check(v[i] == vertices[e][i] && n[i] == normals[e][i] && c[i] == colors[e][i],
                        "all old flowers, rocks, removed vertices and outside-sample art retained");
                    else
                    {
                        var rootXZ = mesh.uv2[i];
                        Check(v[i].x == vertices[e][i].x && v[i].z == vertices[e][i].z && v[i].y <= vertices[e][i].y
                            && c[i] == colors[e][i] && mesh.uv[i].x >= -5 && mesh.uv[i].x <= -4
                            && Vector2.Distance(new Vector2(v[i].x,v[i].z), settings.Center) < settings.AreaRadius,
                            "only sample grass height lowered, original XZ/paint/root retained");
                    }
                }
                var roots = mesh.uv2; var weight = mesh.uv;
                for (int i = vertices[e].Length; i < v.Length; i++)
                    Check(Vector2.Distance(new Vector2(v[i].x, v[i].z), roots[i]) <= 1.16f && weight[i].x >= -5 && weight[i].x <= 1
                        && Vector2.Distance(roots[i], settings.Center) + 1.3f < settings.AreaRadius
                        && GrasslandIslandMeadowSetup.RouteDistance(roots[i]) - 1.3f > 2.1f,
                        "complete new shrub/stone/grass/flower moving envelope preserve sample and road");
            }
            for (int i = 0; i < protectedFiles.Length; i++) Check(Hash(protectedFiles[i]) == hashes[i],
                "insect landing flowers/habitats, ground/map, collisions, materials, prefab and other islands retained " + protectedFiles[i]);
            int edited = Array.FindIndex(plan, e => e.Changed.Any(b => b)); string first = paths[edited]; byte[] bytes = File.ReadAllBytes(first);
            try
            {
                var mesh = plan[edited].Mesh; var paint = mesh.colors; paint[Array.FindIndex(plan[edited].Changed, b => b)].r *= .99f;
                mesh.colors = paint; EditorUtility.SetDirty(mesh); AssetDatabase.SaveAssets();
                var all = Directory.GetFiles("Assets/Art", "*", SearchOption.AllDirectories); var manual = all.Select(Hash).ToArray();
                GrasslandEdgeSetup.Rebuild(); GrasslandWildflowerSetup.Rebuild(); GrasslandBlendSetup.Rebuild(); GrasslandPatchSetup.Rebuild(); GrasslandReadabilitySetup.Rebuild();
                GrasslandTouchSetup.Rebuild(); GrasslandFloraSetup.Rebuild(); PrototypeAddressablesSetup.EnsureSetup();
                for (int i = 0; i < all.Length; i++) Check(Hash(all[i]) == manual[i], "repeat/old entrances/Setup preserve manual art " + all[i]);
            }
            finally { File.WriteAllBytes(first, bytes); AssetDatabase.ImportAsset(first, ImportAssetOptions.ForceUpdate); }
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Folder + "/Stats.json",JsonUtility.ToJson(new Stats {
                Meshes=plan.Length,Groups=plan.Sum(e=>e.Sites.Length),Flowers=plan.Sum(e=>e.Sites.Count(s=>s.Flowers)),
                Thinned=plan.Sum(e=>e.Thinned),Lowered=plan.Sum(e=>e.Lowered),
                AddedVertices=plan.Select((e,i)=>e.Vertices.Length-vertices[i].Length).Sum() },true));
            File.WriteAllText(Folder+"/Sites.json",JsonUtility.ToJson(new Sites { Items=plan.SelectMany(e=>e.Sites).ToArray() },true));
            Debug.Log("Grassland edge bake verification PASSED: bounded merged shrub/stone/grass/flower groups; whole-root thinning, original grass XZ/UV/paint and face order; old flowers, ecology, ground/collision/prefab protected; custom/pending/partial refusal and repeat/manual Setup.");
        }
        [Serializable] sealed class Stats { public int Meshes,Groups,Flowers,Thinned,Lowered,AddedVertices; }
        [Serializable] sealed class Sites { public GrasslandEdgeSetup.Site[] Items; }
        public static void After() => GrasslandWildflowerVerification.After();
        public static bool Authored(Renderer r)
        {
            var m=r.GetComponent<MeshFilter>().sharedMesh;
            return r.name.StartsWith("Meadow undergrowth ",StringComparison.Ordinal)
                &&GrasslandEdgeSetup.Done(AssetImporter.GetAtPath(BiomeIslandsSetup.Folder+"/"+m.name+".asset").userData);
        }
        public static Vector2[] PlacementRoots(Renderer r)
        {
            var m=r.GetComponent<MeshFilter>().sharedMesh;
            var baseline=AssetDatabase.LoadAssetAtPath<Mesh>(Baseline+"/"+m.name+".asset");
            Check(baseline!=null&&m.vertexCount>baseline.vertexCount,"new edge root fixture");
            return m.uv2.Skip(baseline.vertexCount).Distinct().ToArray();
        }
        public static void Visit(GameManager g)
        {
            var renderers = g.Terrain.Terrain.SeasonalDetails.TouchSample.Renderers;
            if (!renderers.All(r => GrasslandEdgeSetup.Done(AssetImporter.GetAtPath(BiomeIslandsSetup.Folder + "/" + r.GetComponent<MeshFilter>().sharedMesh.name + ".asset").userData))) return;
            var roots=renderers.Where(Authored).SelectMany(PlacementRoots).ToArray();
            Check(roots.Length>=4,"actual AA edge roots");
            var p=roots.OrderBy(q=>Vector2.Distance(q,new Vector2(-128.59f,447.98f))).First();
            Check(g.Terrain.TryResolve(new Vector3(p.x,0,p.y),out var focus),"edge group real floor");
            foreach(var renderer in renderers.Where(Authored))
            {
                var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;
                var authored=AssetDatabase.LoadAssetAtPath<Mesh>(BiomeIslandsSetup.Folder+"/"+mesh.name+".asset");
                Check(mesh.normals.SequenceEqual(authored.normals)&&mesh.triangles.SequenceEqual(authored.triangles)
                    &&Enumerable.Range(0,8).All(ch=>UV(mesh,ch).SequenceEqual(UV(authored,ch))),"actual AA full edge geometry and channels");
            }
            GrasslandReadabilityVerification.Compare(g,Folder,Baseline,"edge",false,
                before=>GrasslandTouchVerification.Frame(g.Players.Player.ViewCamera,focus));
        }
    }
}
