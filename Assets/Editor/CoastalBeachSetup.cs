using System;
using System.IO;
using System.Linq;
using MagicIsland.Interaction;
using MagicIsland.TerrainSystem;
using UnityEditor;
using UnityEngine;

namespace MagicIsland.EditorTools
{
    // Explicit thinning of authored decorative instances. Never reroll positions or pickups.
    public static class CoastalBeachSetup
    {
        static void Require(bool ok,string why){if(!ok)throw new InvalidOperationException("Beach transition: "+why);}
        [MenuItem("Magic Island/Art/Thin Beach Shells And Pebbles (Explicit)")]
        public static void Rebuild()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling,"use Edit Mode");
            var root=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);
            try
            {
                var terrain=root.GetComponent<IslandTerrain>();Require(terrain.CoastalScenery!=null,"missing authored coastal scenery");
                var shells=AssetDatabase.LoadAssetAtPath<GameObject>(CoastArtSetup.PrefabFolder+"/BeachShell.prefab");
                var pebbles=AssetDatabase.LoadAssetAtPath<GameObject>(CoastArtSetup.PrefabFolder+"/ReefRock.prefab");
                Require(shells!=null&&pebbles!=null,"missing decorative templates; existing scenery retained");
                int hidden=0;
                foreach(Transform t in terrain.CoastalScenery)
                {
                    var source=PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
                    if(source!=shells&&source!=pebbles)continue;
                    var p=t.localPosition;float inset=(float)IslandLayout.CoastRadius(Math.Atan2(p.z,p.x))-new Vector2(p.x,p.z).magnitude;
                    if(inset<.7f||inset>3.1f)continue; // Preserve hand-placed inland or submerged pieces.
                    float pocket=Mathf.PerlinNoise(p.x*.31f+31.7f,p.z*.31f+59.2f);
                    if(pocket>=.43f||!t.gameObject.activeSelf)continue;
                    Require(t.GetComponentsInChildren<Collider>(true).Length==0,"decorations must have no gameplay collision");
                    t.gameObject.SetActive(false);hidden++;
                }
                if(hidden>0)
                {
                    PrefabUtility.SaveAsPrefabAsset(root,TerrainSetup.PrefabPath);
                    File.WriteAllText(TerrainSetup.PrefabPath,string.Join("\n",File.ReadAllLines(TerrainSetup.PrefabPath).Select(line=>line.TrimEnd()))+"\n");
                    AssetDatabase.ImportAsset(TerrainSetup.PrefabPath,ImportAssetOptions.ForceSynchronousImport);
                }
                Debug.Log("Beach detail thinning: "+hidden+" decorative shells/pebbles hidden; all instances, transforms and references retained.");
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        public static void BakeAndVerify()
        {
            Require(Application.dataPath.Replace('\\','/').Contains("/Logs/"),"verification requires an isolated project");
            string[] meshes={OceanSeabedSlopeSetup.FloorPath,StarterIslandWorldSetup.Folder+"/Village Grass.asset",TerrainSetup.MeshFolder+"/Sea Boundary.asset"};
            var originals=meshes.Select(File.ReadAllBytes).ToArray();
            string ocean=File.ReadAllText(OceanSetup.PrefabPath);
            Rebuild();byte[] baked=File.ReadAllBytes(TerrainSetup.PrefabPath);
            Rebuild();Require(baked.SequenceEqual(File.ReadAllBytes(TerrainSetup.PrefabPath)),"repeat thinning preserves the baked prefab byte for byte");
            PrototypeAddressablesSetup.EnsureSetup();
            Require(baked.SequenceEqual(File.ReadAllBytes(TerrainSetup.PrefabPath)),"ordinary Setup retains authored beach layout and inactive overrides");
            Require(ocean==File.ReadAllText(OceanSetup.PrefabPath),"all ocean ecology and shared water retained");
            for(int i=0;i<meshes.Length;i++)Require(originals[i].SequenceEqual(File.ReadAllBytes(meshes[i])),"all geometry/colours/normals/UVs retained: "+meshes[i]);
            foreach(string shader in new[]{"MagicIsland/Island Shore","MagicIsland/Island Biome Ground"})
                Require(!ShaderUtil.ShaderHasError(Shader.Find(shader)),"wet beach shader compiles: "+shader);
            var terrain=AssetDatabase.LoadAssetAtPath<GameObject>(TerrainSetup.PrefabPath).GetComponent<IslandTerrain>();
            int active=0,inactive=0;
            foreach(Transform t in terrain.CoastalScenery){if(t.gameObject.activeSelf)active++;else inactive++;}
            Directory.CreateDirectory("Logs/BeachTransition");
            File.WriteAllText("Logs/BeachTransition/Bake.txt",$"PASS: {active} active / {inactive} inactive coastal instances; geometry, colours, UVs, ocean and repeat/Setup preservation verified.\n");
            Debug.Log("Beach transition bake verification PASSED.");
        }
        public static void BakeAndRun(){BakeAndVerify();TerrainCoastFlowVerification.Run();}
    }
}
