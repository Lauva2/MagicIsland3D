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
    public static class GrasslandShrubDetailVerification
    {
        const string Folder = "Logs/GrasslandShrubDetail", Baseline = "Assets/Editor/GrasslandShrubDetailBaseline";
        static void Check(bool ok, string why) { if (!ok) throw new InvalidOperationException("Edge verification: " + why); }
        static string Hash(string p) { using (var sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(p))); }
        static Vector4[] UV(Mesh m, int channel) { var a = new List<Vector4>(); m.GetUVs(channel, a); return a.ToArray(); }
        public static void BakeAndVerify()
        {
            Check(Application.dataPath.Replace('\\', '/').Contains("/Logs/"), "isolated project required");
            var root = PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath); GrasslandShrubDetailSetup.Edit[] plan; MeadowTouchSettings settings;
            try
            {
                var terrain = root.GetComponent<IslandTerrain>(); settings = terrain.SeasonalDetails.TouchSample;
                plan = GrasslandShrubDetailSetup.Plan(terrain);
                var r = settings.Renderers[0]; var original = r.sharedMaterial; var custom = new Material(Shader.Find("Standard"));
                try
                {
                    r.sharedMaterial = custom; bool refused = false;
                    try { GrasslandShrubDetailSetup.Plan(terrain); } catch (InvalidOperationException) { refused = true; }
                    Check(refused, "custom material refused before writes");
                }
                finally { r.sharedMaterial = original; Object.DestroyImmediate(custom); }
                var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(plan[0].Mesh)); string metadata = importer.userData;
                try
                {
                    importer.userData = metadata + ";Artist.Custom"; bool refused = false;
                    try { GrasslandShrubDetailSetup.Plan(terrain); } catch (InvalidOperationException) { refused = true; }
                    Check(refused, "unknown metadata refused before writes");
                    foreach (string marker in new[] { GrasslandShrubDetailSetup.Pending, GrasslandShrubDetailSetup.Marker })
                    {
                        importer.userData = metadata + ";" + marker; refused = false;
                        try { GrasslandShrubDetailSetup.Rebuild(); } catch (InvalidOperationException) { refused = true; }
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
            if (!AssetDatabase.IsValidFolder(Baseline)) AssetDatabase.CreateFolder("Assets/Editor", "GrasslandShrubDetailBaseline");
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
            GrasslandShrubDetailSetup.Rebuild();
            for (int e = 0; e < plan.Length; e++)
            {
                var mesh = plan[e].Mesh; var v = mesh.vertices; var n = mesh.normals; var c = mesh.colors;var weight=mesh.uv;var roots=mesh.uv2;
                Check(AssetDatabase.AssetPathToGUID(paths[e]) == guids[e] && mesh.indexFormat == formats[e]
                    && v.SequenceEqual(plan[e].Vertices) && n.SequenceEqual(plan[e].Normals) && c.SequenceEqual(plan[e].Colors)
                    && mesh.triangles.SequenceEqual(plan[e].Indices) && mesh.bounds == plan[e].Bounds, "exact planned original-GUID meshes");
                for (int ch = 0; ch < 8; ch++) Check(UV(mesh, ch).Take(channels[e][ch].Length).SequenceEqual(channels[e][ch])
                    && (ch < 3 || UV(mesh, ch).Length == 0), "all original UV channels and root ownership retained");
                Check(mesh.triangles.Take(faces[e].Length).SequenceEqual(faces[e]),"all original face indices and order retained");
                var rootSet=new HashSet<Vector2>(plan[e].Roots);
                for(int i=0;i<vertices[e].Length;i++)
                {
                    Check(c[i].a==colors[e][i].a,"all original alpha retained");
                    if(!plan[e].Changed[i])Check(v[i]==vertices[e][i]&&n[i]==normals[e][i]&&c[i]==colors[e][i],"original grass/flowers/rocks and unselected art retained");
                    else Check(rootSet.Contains(roots[i])&&weight[i].x>=0
                        &&Vector2.Distance(new Vector2(v[i].x,v[i].z),roots[i])<=1.16f
                        &&Vector3.Distance(v[i],vertices[e][i])<.4f&&n[i].sqrMagnitude>.99f
                        &&Vector2.Distance(new Vector2(v[i].x,v[i].z),settings.Center)<settings.AreaRadius,"bounded original leaf lobes only");
                }
                for(int i=vertices[e].Length;i<v.Length;i++)
                    Check(rootSet.Contains(roots[i])&&Vector2.Distance(new Vector2(v[i].x,v[i].z),roots[i])<=1.16f
                        &&Vector2.Distance(roots[i],settings.Center)+1.3f<settings.AreaRadius
                        &&GrasslandIslandMeadowSetup.RouteDistance(roots[i])-1.3f>2.1f
                        &&weight[i].x>=-5&&weight[i].x<=1,"new details share existing roots and bounded motion/road clearance");
            }
            Check(plan.Sum(e=>e.Roots.Length)==13&&plan.Sum(e=>e.Lobes)==67,"thirteen original mixed-root shrubs and their 67 complete lobes, no rocks selected");
            for (int i = 0; i < protectedFiles.Length; i++) Check(Hash(protectedFiles[i]) == hashes[i],
                "insect landing flowers/habitats, ground/map, collisions, materials, prefab and other islands retained " + protectedFiles[i]);
            int edited = Array.FindIndex(plan, e => e.Changed.Any(b => b)); string first = paths[edited]; byte[] bytes = File.ReadAllBytes(first);
            try
            {
                var mesh = plan[edited].Mesh; var paint = mesh.colors; paint[Array.FindIndex(plan[edited].Changed, b => b)].r *= .99f;
                mesh.colors = paint; EditorUtility.SetDirty(mesh); AssetDatabase.SaveAssets();
                var all = Directory.GetFiles("Assets/Art", "*", SearchOption.AllDirectories); var manual = all.Select(Hash).ToArray();
                GrasslandShrubDetailSetup.Rebuild(); GrasslandSpatialSetup.Rebuild(); GrasslandSampleFinishSetup.Rebuild(); GrasslandPathEdgeSetup.Rebuild(); GrasslandEdgeSetup.Rebuild(); GrasslandWildflowerSetup.Rebuild(); GrasslandBlendSetup.Rebuild(); GrasslandPatchSetup.Rebuild(); GrasslandReadabilitySetup.Rebuild();
                GrasslandTouchSetup.Rebuild(); GrasslandFloraSetup.Rebuild(); PrototypeAddressablesSetup.EnsureSetup();
                for (int i = 0; i < all.Length; i++) Check(Hash(all[i]) == manual[i], "repeat/old entrances/Setup preserve manual art " + all[i]);
            }
            finally { File.WriteAllBytes(first, bytes); AssetDatabase.ImportAsset(first, ImportAssetOptions.ForceUpdate); }
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Folder+"/Stats.json",JsonUtility.ToJson(new Stats{Meshes=plan.Length,Shrubs=plan.Sum(e=>e.Roots.Length),Lobes=plan.Sum(e=>e.Lobes),Leaves=plan.Sum(e=>e.Leaves),Branches=plan.Sum(e=>e.Branches),Flowers=plan.Sum(e=>e.Flowers),AddedVertices=plan.Select((e,i)=>e.Vertices.Length-vertices[i].Length).Sum()},true));
            Debug.Log("Grassland shrub detail bake verification PASSED: original thirteen shrubs/67 leaf lobes, bounded reshaping and soft normals/palette, pointed leaves/fine branches/base details; original roots/UV/face order/index format and other art protected; custom/pending/partial refusal and repeat/manual Setup.");
        }
        [Serializable] sealed class Stats{public int Meshes,Shrubs,Lobes,Leaves,Branches,Flowers,AddedVertices;}
        public static void After()=>GrasslandSpatialVerification.After();
        public static bool Authored(Renderer r)
        {
            var m=r.GetComponent<MeshFilter>().sharedMesh;
            return r.name.StartsWith("Meadow undergrowth ",StringComparison.Ordinal)
                &&GrasslandShrubDetailSetup.Done(AssetImporter.GetAtPath(BiomeIslandsSetup.Folder+"/"+m.name+".asset").userData);
        }
        public static Vector2[] PlacementRoots(Renderer r)
        {
            var m=r.GetComponent<MeshFilter>().sharedMesh;
            var baseline=AssetDatabase.LoadAssetAtPath<Mesh>(Baseline+"/"+m.name+".asset");
            Check(baseline!=null&&m.vertexCount>=baseline.vertexCount,"original sample root fixture");
            // Later art passes append flower/grass vertices to the same batches. Select shrub tips/branches by their own codec.
            var roots=m.uv2;var uv=m.uv;
            bool wind=GrasslandShrubWindSetup.Done(AssetImporter.GetAtPath(BiomeIslandsSetup.Folder+"/"+m.name+".asset").userData);
            return Enumerable.Range(baseline.vertexCount,m.vertexCount-baseline.vertexCount)
                .Where(i=>wind?GrasslandShrubWindSetup.Marked(uv[i]):uv[i].x>=0).Select(i=>roots[i]).Distinct().ToArray();
        }
        public static void Visit(GameManager g)
        {
            var renderers = g.Terrain.Terrain.SeasonalDetails.TouchSample.Renderers;
            if (!renderers.All(r => GrasslandShrubDetailSetup.Done(AssetImporter.GetAtPath(BiomeIslandsSetup.Folder + "/" + r.GetComponent<MeshFilter>().sharedMesh.name + ".asset").userData))) return;
            var roots=renderers.Where(Authored).SelectMany(PlacementRoots).ToArray();
            Check(roots.Length==13,"actual AA existing shrub roots");
            var p=roots.OrderBy(q=>Vector2.Distance(q,new Vector2(-128.59f,447.98f))).First();
            Check(g.Terrain.TryResolve(new Vector3(p.x,0,p.y),out var focus),"shrub group real floor");
            foreach(var renderer in renderers)
            {
                var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;
                var authored=AssetDatabase.LoadAssetAtPath<Mesh>(BiomeIslandsSetup.Folder+"/"+mesh.name+".asset");
                Check(mesh.normals.SequenceEqual(authored.normals)&&mesh.triangles.SequenceEqual(authored.triangles)
                    &&Enumerable.Range(0,8).All(ch=>UV(mesh,ch).SequenceEqual(UV(authored,ch))),"actual AA full shrub detail geometry and channels");
            }
            GrasslandReadabilityVerification.Compare(g,Folder,Baseline,"shrub detail");
            GrasslandReadabilityVerification.Compare(g,Folder+"/Close",Baseline,"shrub detail close",false,before=>{
                GrasslandTouchVerification.Frame(g.Players.Player.ViewCamera,focus);g.Players.Player.ViewCamera.orthographicSize=3;
            });
        }
    }
}
