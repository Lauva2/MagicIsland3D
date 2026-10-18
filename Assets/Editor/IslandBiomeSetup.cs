using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MagicIsland.TerrainSystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    public static class IslandBiomeSetup
    {
        public const string Folder = "Assets/Art/Meshes/Terrain/Biomes";
        public const string GroundPath = Folder + "/Biome Land.asset";
        public const string MaterialFolder = "Assets/Art/Materials/Terrain";
        public static void Ensure()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TerrainSetup.PrefabPath);
            if (prefab != null && prefab.GetComponent<IslandTerrain>().BiomeArtVersion == 0) Rebuild();
        }
        [MenuItem("Magic Island/Art/Rebuild Island Biomes (Explicit)")]
        public static void Rebuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var root = PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);
            try
            {
                var terrain = root.GetComponent<IslandTerrain>();
                if(terrain.World!=null)throw new InvalidOperationException("Large starter island is active. Use Rebuild Large Starter Island (Explicit) to preserve its village and mainland layout.");
                if (terrain.Biomes == null) terrain.Biomes = new IslandBiomeSettings();
                if (!terrain.Biomes.IsValid || terrain.SeasonalDetails == null) throw new InvalidOperationException("Invalid biome settings or missing seasonal cover; existing art preserved.");
                var kept = terrain.SeasonalDetails.Patches.Where(p => terrain.BiomeScenery == null || !p.transform.IsChildOf(terrain.BiomeScenery)).ToArray();
                int previousExtra = terrain.SeasonalDetails.Patches.Length - kept.Length;
                if (kept.Length + terrain.Biomes.DesertPatches + terrain.Biomes.SnowPatches > 160) throw new InvalidOperationException("Biome scenery exceeds the existing 160-patch budget.");
                int newExtra = terrain.Biomes.DesertPatches + terrain.Biomes.SnowPatches;
                if (terrain.SeasonalDetails.MaximumPatches - previousExtra + newExtra > 160) throw new InvalidOperationException("Biome scatter range exceeds the cover budget.");
                // Prepare the complete plan before replacing any authored biome resources.
                var ground = Ground(terrain.Ground.sharedMesh, terrain.Biomes);
                var sites = Sites(terrain.Biomes);
                Directory.CreateDirectory(Folder); Directory.CreateDirectory(MaterialFolder); AssetDatabase.Refresh();
                var surface = Material("Biome Ground", "MagicIsland/Island Biome Ground");
                var details = Material("Biome Details", "MagicIsland/Island Biome Details");
                var meshes = new[] { Cactus(), Sandstone(), Pine(), SnowRock() };
                string[] names = { "Cactus", "Sandstone", "Snow Pine", "Snow Rock" };
                for (int i = 0; i < meshes.Length; i++) meshes[i] = Store(Folder + "/" + names[i] + ".asset", meshes[i]);
                ground = Store(GroundPath, ground);
                terrain.Ground.GetComponent<MeshFilter>().sharedMesh = ground;
                terrain.Ground.GetComponent<MeshRenderer>().sharedMaterial = surface;
                var group = terrain.BiomeScenery;
                if (group == null) { group = new GameObject("Biome scenery").transform; group.SetParent(terrain.SeasonalDetails.transform, false); group.gameObject.layer = 2; }
                terrain.BiomeScenery = group;
                var existing = group.GetComponentsInChildren<SeasonalGroundPatch>(true); int cursor = 0;
                var patches = new List<SeasonalGroundPatch>(kept);
                foreach (var site in sites)
                {
                    var go = cursor < existing.Length ? existing[cursor].gameObject : new GameObject(names[site.Kind], typeof(MeshFilter), typeof(MeshRenderer), typeof(SeasonalGroundPatch));
                    cursor++; go.name = names[site.Kind];
                    go.layer = 2; go.transform.SetParent(group, false); go.transform.localPosition = site.Position;
                    go.transform.localRotation = Quaternion.Euler(0, site.Yaw, 0); go.transform.localScale = Vector3.one * site.Scale;
                    var patch = go.GetComponent<SeasonalGroundPatch>(); patch.Filter = go.GetComponent<MeshFilter>(); patch.Surface = go.GetComponent<MeshRenderer>();
                    patch.Filter.sharedMesh = meshes[site.Kind]; patch.Surface.sharedMaterial = details; patch.Surface.enabled = false;
                    patch.Seasons = Enumerable.Repeat(meshes[site.Kind], 4).ToArray(); patch.FootprintRadius = .48f;
                    patch.Biome = site.Kind < 2 ? IslandBiome.Desert : IslandBiome.Snow; patches.Add(patch);
                }
                for (int i = cursor; i < existing.Length; i++) Object.DestroyImmediate(existing[i].gameObject);
                terrain.SeasonalDetails.MinimumPatches += newExtra - previousExtra;
                terrain.SeasonalDetails.MaximumPatches += newExtra - previousExtra;
                terrain.SeasonalDetails.Patches = patches.ToArray(); terrain.BiomeArtVersion = 1;
                PrefabUtility.SaveAsPrefabAsset(root, TerrainSetup.PrefabPath); AssetDatabase.SaveAssets();
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        static Material Material(string name, string shader)
        {
            string path = MaterialFolder + "/" + name + ".mat"; var value = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (value != null) return value;
            var found = Shader.Find(shader); if (found == null) throw new InvalidOperationException("Missing shader: " + shader);
            value = new Material(found) { name = name }; AssetDatabase.CreateAsset(value, path); return value;
        }
        static Mesh Store(string path, Mesh mesh)
        {
            mesh.name = Path.GetFileNameWithoutExtension(path); var current = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (current == null) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
            EditorUtility.CopySerialized(mesh, current); EditorUtility.SetDirty(current); Object.DestroyImmediate(mesh); return current;
        }
        public static Mesh Ground(Mesh source, IslandBiomeSettings settings)
        {
            var original = source.vertices; var originalColors = source.colors;
            if (original.Length == 0 || original[0] != Vector3.zero || originalColors.Length != original.Length)
                throw new InvalidOperationException("Ground has no matching central fan/colours; preserve artist geometry.");
            var vertices = new List<Vector3>(original); var colors = new List<Color>(originalColors); var triangles = new List<int>();
            var rays = new Dictionary<int, int[]>(); const int divisions = 24;
            Func<int, int[]> ray = index =>
            {
                if (rays.TryGetValue(index, out var found)) return found;
                var indices = new int[divisions + 1]; indices[0] = 0; indices[divisions] = index;
                for (int j = 1; j < divisions; j++)
                {
                    indices[j] = vertices.Count; vertices.Add(Vector3.Lerp(original[0], original[index], j / (float)divisions));
                    colors.Add(Color.Lerp(originalColors[0], originalColors[index], j / (float)divisions));
                }
                rays.Add(index, indices); return indices;
            };
            var sourceIndices = source.triangles; int fans = 0;
            for (int i = 0; i < sourceIndices.Length; i += 3)
            {
                int a = sourceIndices[i], b = sourceIndices[i+1], c = sourceIndices[i+2];
                if (a != 0 && b != 0 && c != 0) { triangles.AddRange(new[] { a, b, c }); continue; }
                if (b == 0) { b = c; c = a; } else if (c == 0) { c = b; b = a; }
                var rb = ray(b); var rc = ray(c); fans++; triangles.AddRange(new[] { 0, rb[1], rc[1] });
                for (int j = 1; j < divisions; j++) triangles.AddRange(new[] { rb[j], rb[j+1], rc[j], rb[j+1], rc[j+1], rc[j] });
            }
            if (fans < 32) throw new InvalidOperationException("Unsupported authored central fan; preserve ground.");
            var uv = new List<Vector2>(vertices.Count);
            for (int i = 0; i < vertices.Count; i++)
            {
                var p = vertices[i]; var w = settings.Weights(p.x, p.z); var color = colors[i];
                float grass = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.01f, .12f, color.g-color.r)) * w.x;
                colors[i] = settings.Blend(color, p.x, p.z); uv.Add(new Vector2(grass, 0));
            }
            var mesh = new Mesh { indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
        struct Site { public Vector3 Position; public float Yaw, Scale; public int Kind; }
        static List<Site> Sites(IslandBiomeSettings config)
        {
            var result = new List<Site>(); var random = new System.Random(config.ScenerySeed);
            for (int region = 0; region < 2; region++)
            {
                int count = region == 0 ? config.DesertPatches : config.SnowPatches, start = result.Count;
                for (int attempt = 0; result.Count-start < count && attempt < 30000; attempt++)
                {
                    float x = (float)random.NextDouble()*36-18, z = (float)random.NextDouble()*36-18;
                    if (config.Weight(region == 0 ? IslandBiome.Desert : IslandBiome.Snow, x, z) < .7f
                        || !MagicIsland.Interaction.IslandLayout.ContainsLand(x,z,1) || result.Any(p => Vector2.Distance(new Vector2(p.Position.x,p.Position.z),new Vector2(x,z)) < 1.35f)) continue;
                    result.Add(new Site { Position = new Vector3(x,.015f,z), Yaw = (float)random.NextDouble()*360,
                        Scale = .78f + (float)random.NextDouble()*.22f, Kind = region*2+(result.Count-start)%2 });
                }
                if (result.Count-start != count) throw new InvalidOperationException("Cannot fit complete biome scenery plan; existing art preserved.");
            }
            return result;
        }
        static Mesh Cactus()
        {
            var g = new Geometry(); var green = new Color(.35f,.57f,.38f);
            g.Tube(new Vector3(0,0,0),new Vector3(0,1.15f,0),.145f,.13f,green);g.Ball(new Vector3(0,1.15f,0),new Vector3(.13f,.16f,.13f),green);
            foreach (int side in new[] {-1,1})
            {
                float y = side < 0 ? .50f : .72f;
                g.Tube(new Vector3(0,y,0),new Vector3(side*.26f,y,0),.09f,.085f,green);
                g.Ball(new Vector3(side*.26f,y,0),Vector3.one*.09f,green);
                g.Tube(new Vector3(side*.26f,y,0),new Vector3(side*.26f,y+.30f,0),.085f,.075f,green);
                g.Ball(new Vector3(side*.26f,y+.30f,0),Vector3.one*.075f,green);
            }
            g.Ball(new Vector3(.04f,1.31f,0),new Vector3(.075f,.035f,.075f),new Color(.98f,.70f,.55f));return g.Mesh();
        }
        static Mesh Sandstone()
        {
            var g = new Geometry();g.Ball(new Vector3(-.10f,.20f,0),new Vector3(.31f,.28f,.30f),new Color(.77f,.51f,.31f));
            g.Ball(new Vector3(.19f,.11f,.15f),new Vector3(.16f,.15f,.16f),new Color(.87f,.65f,.40f));return g.Mesh();
        }
        static Mesh Pine()
        {
            var g = new Geometry();g.Tube(Vector3.zero,new Vector3(0,1.6f,0),.07f,.04f,new Color(.42f,.34f,.27f));
            for (int j = 0; j < 4; j++)
            {
                float y = .32f+j*.34f, r = .41f-j*.07f;
                g.Tube(new Vector3(0,y,0),new Vector3(0,y+.65f,0),r,.015f,new Color(.25f,.43f,.38f));
                g.Tube(new Vector3(0,y+.12f,0),new Vector3(0,y+.68f,0),r*.90f,.008f,new Color(.91f,.96f,1));
            }
            return g.Mesh();
        }
        static Mesh SnowRock()
        {
            var g = new Geometry();g.Ball(new Vector3(0,.16f,0),new Vector3(.39f,.25f,.32f),new Color(.51f,.62f,.66f));
            g.Ball(new Vector3(-.015f,.31f,0),new Vector3(.35f,.13f,.29f),new Color(.92f,.96f,1));return g.Mesh();
        }
        sealed class Geometry
        {
            readonly List<Vector3> v = new List<Vector3>(); readonly List<Color> colors = new List<Color>(); readonly List<int> t = new List<int>();
            void Tri(Vector3 a,Vector3 b,Vector3 c,Color color)
            { int i=v.Count;v.AddRange(new[]{a,b,c});colors.AddRange(new[]{color,color,color});t.AddRange(new[]{i,i+1,i+2}); }
            public void Tube(Vector3 from,Vector3 to,float bottom,float top,Color color)
            {
                var q=Quaternion.FromToRotation(Vector3.up,to-from);
                for(int i=0;i<12;i++)
                {
                    float a=i*Mathf.PI/6,b=(i+1)*Mathf.PI/6;
                    var p=from+q*new Vector3(Mathf.Cos(a)*bottom,0,Mathf.Sin(a)*bottom);var r=from+q*new Vector3(Mathf.Cos(b)*bottom,0,Mathf.Sin(b)*bottom);
                    var s=to+q*new Vector3(Mathf.Cos(a)*top,0,Mathf.Sin(a)*top);var u=to+q*new Vector3(Mathf.Cos(b)*top,0,Mathf.Sin(b)*top);
                    Tri(p,s,r,color);Tri(r,s,u,color);Tri(from,p,r,color);Tri(to,u,s,color);
                }
            }
            public void Ball(Vector3 center,Vector3 scale,Color color)
            {
                Func<int,int,Vector3> point=(ring,side)=>center+Vector3.Scale(scale,new Vector3(Mathf.Sin(ring*Mathf.PI/8)*Mathf.Cos(side*Mathf.PI/6),Mathf.Cos(ring*Mathf.PI/8),Mathf.Sin(ring*Mathf.PI/8)*Mathf.Sin(side*Mathf.PI/6)));
                for(int y=0;y<8;y++)for(int x=0;x<12;x++)
                {var a=point(y,x);var b=point(y+1,x);var c=point(y,x+1);var d=point(y+1,x+1);if(y>0)Tri(a,c,b,color);if(y<7)Tri(c,d,b,color);}
            }
            public Mesh Mesh()
            { var mesh=new Mesh();mesh.SetVertices(v);mesh.SetColors(colors);mesh.SetTriangles(t,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh; }
        }
    }
}
