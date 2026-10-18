using System;
using System.IO;
using System.Linq;
using MagicIsland.Interaction;
using MagicIsland.TerrainSystem;
using UnityEditor;
using UnityEngine;

namespace MagicIsland.EditorTools
{
    public static class IslandBiomeVerification
    {
        static int checks;
        static void Require(bool b, string why) { checks++; if (!b) throw new InvalidOperationException("Island biomes: " + why); }
        public static void VerifyRebuild()
        {
            Require(Application.dataPath.Replace('\\','/').Contains("/Logs/"),"explicit rebuild tests require an isolated project");
            Run();
            var preserved = new[] { TerrainSetup.MeshFolder+"/Island Land.asset", TerrainSetup.MeshFolder+"/Sea Boundary.asset", OceanSetup.PrefabPath,
                OceanSetup.MeshFolder+"/Island Shore.asset", SeasonalDetailsSetup.PrefabPath };
            var original = preserved.Select(File.ReadAllBytes).ToArray();
            IslandBiomeSetup.Rebuild();
            var changed = new[] { TerrainSetup.PrefabPath }.Concat(Directory.GetFiles(IslandBiomeSetup.Folder,"*.asset")).ToArray();
            var first = changed.Select(File.ReadAllBytes).ToArray();IslandBiomeSetup.Rebuild();
            for(int i=0;i<changed.Length;i++)Require(first[i].SequenceEqual(File.ReadAllBytes(changed[i])),"repeat explicit rebuild preserves object identities and bytes: "+changed[i]);
            for(int i=0;i<preserved.Length;i++)Require(original[i].SequenceEqual(File.ReadAllBytes(preserved[i])),"explicit rebuild preserves old resources: "+preserved[i]);
            var path=IslandBiomeSetup.GroundPath;var bytes=File.ReadAllBytes(path);var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            try
            {
                var colors=mesh.colors;colors[0]=Color.magenta;mesh.colors=colors;EditorUtility.SetDirty(mesh);AssetDatabase.SaveAssets();
                var edited=File.ReadAllBytes(path);PrototypeAddressablesSetup.EnsureSetup();
                Require(edited.SequenceEqual(File.ReadAllBytes(path)),"ordinary Setup preserves deliberate artist mesh recolor");
            }
            finally { File.WriteAllBytes(path,bytes);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport); }
            Run();Debug.Log("Island biome explicit rebuild / artist preservation PASSED.");
        }
        public static void Run()
        {
            PrototypeAddressablesSetup.EnsureSetup(); checks = 0;
            var terrain = AssetDatabase.LoadAssetAtPath<GameObject>(TerrainSetup.PrefabPath).GetComponent<IslandTerrain>();
            if(terrain.World!=null){StarterIslandWorldVerification.Run();return;}
            Require(terrain.IsValid && terrain.BiomeArtVersion == 1 && terrain.Biomes.IsValid, "terrain AA authoring is valid");
            var settings = terrain.Biomes;
            Require(settings.At(-13,0)==IslandBiome.Desert && settings.At(2,14)==IslandBiome.Snow && settings.At(0,0)==IslandBiome.Grassland, "three distinct natural regions");
            Require(settings.At(settings.NewGameSpawn.x,settings.NewGameSpawn.z)==IslandBiome.Beach,"new-game point is on the beach");
            int desert=0,snow=0;
            for(float x=-18;x<=18;x+=.25f)for(float z=-18;z<=18;z+=.25f)
            {
                var w=settings.Weights(x,z);
                Require(w.x>=-.00001f&&w.y>=0&&w.z>=0&&Mathf.Abs(w.x+w.y+w.z-1)<.00001f,"finite normalized region weights");
                Require(Vector3.Distance(w,settings.Weights(x+.01f,z))<.03f&&Vector3.Distance(w,settings.Weights(x,z+.01f))<.03f,"continuous biome edges");
                if(Mathf.Abs(x)<=7.8f&&Mathf.Abs(z)<=7.8f)Require(w==Vector3.right,"maximum farm and original core remain grassland");
                if(w.y>.7f)desert++;if(w.z>.7f)snow++;
            }
            Require(desert>400&&snow>400,"both new regions have useful interior area");
            var baked=terrain.Ground.GetComponent<MeshFilter>().sharedMesh;var original=terrain.Ground.sharedMesh;
            Require(AssetDatabase.GetAssetPath(baked)==IslandBiomeSetup.GroundPath&&baked!=original,"separate baked visual mesh keeps original collision mesh");
            Require(original.vertices.SequenceEqual(baked.vertices.Take(original.vertexCount)),"all original coast and ground vertices are retained");
            Require(baked.vertexCount<16000&&baked.triangles.Length/3<24000,"bounded ground subdivision budget");
            foreach(var p in baked.vertices.Skip(original.vertexCount))Require(Mathf.Abs(p.y)<.00001f,"new fan vertices stay on the same flat collision plane");
            Require(baked.uv.Length==baked.vertexCount&&baked.uv.All(u=>u.x>=0&&u.x<=1),"explicit grass-only seasonal weight");
            foreach(string shader in new[]{"MagicIsland/Island Biome Ground","MagicIsland/Island Biome Details"})Require(Shader.Find(shader)!=null&&!ShaderUtil.ShaderHasError(Shader.Find(shader)),"compiled shader "+shader);
            var patches=terrain.BiomeScenery.GetComponentsInChildren<SeasonalGroundPatch>(true);
            Require(patches.Length==settings.DesertPatches+settings.SnowPatches&&terrain.SeasonalDetails.IsValid,"full scenery plan fits existing cover budget");
            Require(terrain.SeasonalDetails.Patches.Length>=terrain.SeasonalDetails.MinimumPatches&&terrain.SeasonalDetails.Patches.Length<=terrain.SeasonalDetails.MaximumPatches,"combined scatter count remains inside its authored range");
            Require(terrain.BiomeScenery.GetComponentsInChildren<Collider>(true).Length==0,"new scenery never blocks movement or clicks");
            foreach(var patch in patches)
            {
                var p=terrain.transform.InverseTransformPoint(patch.transform.position);
                Require(settings.Weight(patch.Biome,p.x,p.z)>.69f&&IslandLayout.ContainsLand(p.x,p.z,1),"scenery belongs to a dry biome interior");
                Require(patch.Seasons.All(m=>m==patch.Seasons[0])&&patch.Surface.sharedMaterial==patches[0].Surface.sharedMaterial,"permanent scenery uses shared mesh/material in all seasons");
                foreach(var v in patch.Filter.sharedMesh.vertices)Require(Mathf.Abs(v.x)<patch.FootprintRadius&&Mathf.Abs(v.z)<patch.FootprintRadius&&v.y>=-.1f&&v.y<2.1f,"complete decoration fits its reserved footprint");
            }
            foreach(char c in "当前位置：草原沙漠雪地沙滩")Require(UiPrefabMigration.FontAsset.HasCharacter(c),"Chinese font includes "+c);
            var paths=new[]{TerrainSetup.PrefabPath,IslandBiomeSetup.GroundPath,TerrainSetup.MeshFolder+"/Island Land.asset",TerrainSetup.MeshFolder+"/Sea Boundary.asset",OceanSetup.PrefabPath}
                .Concat(Directory.GetFiles(IslandBiomeSetup.Folder,"*.asset")).Concat(Directory.GetFiles(IslandBiomeSetup.MaterialFolder,"*.mat")).Distinct().ToArray();
            var bytes=paths.Select(File.ReadAllBytes).ToArray();PrototypeAddressablesSetup.EnsureSetup();PrototypeAddressablesSetup.EnsureSetup();
            for(int i=0;i<paths.Length;i++)Require(bytes[i].SequenceEqual(File.ReadAllBytes(paths[i])),"ordinary Setup preserves authored resources: "+paths[i]);
            Directory.CreateDirectory("Logs/IslandBiomes");File.WriteAllText("Logs/IslandBiomes/Assets.txt","PASS: "+checks+" checks; three continuous regions, beach start, preserved collision/coast vertices, bounded baked surface, shared collision-free scenery, original core and Setup preservation, Chinese map glyphs.\n");
            Debug.Log("Island biome asset verification PASSED: "+checks);
        }
    }
}
