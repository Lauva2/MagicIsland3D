using System;
using System.IO;
using System.Linq;
using MagicIsland.Managers;
using MagicIsland.Ocean;
using MagicIsland.TerrainSystem;
using UnityEditor;
using UnityEngine;

namespace MagicIsland.EditorTools
{
    public static class CoastalLandscapeVerification
    {
        static void Require(bool ok,string why){if(!ok)throw new Exception("Coastal landscape: "+why);}
        public static void VerifySourceGuard()
        {
            Require(Application.dataPath.Replace('\\','/').Contains("/Logs/"),"source guard verification requires an isolated project");
            VerifyAssets();string path=OceanReefArtSetup.DataPath;var original=File.ReadAllBytes(path);
            var layout=JsonUtility.FromJson<OceanReefArtSetup.Layout>(File.ReadAllText(OceanReefArtSetup.LayoutPath));
            var bakedPaths=new[]{OceanSetup.PrefabPath,OceanReefArtSetup.LayoutPath,path+".meta"}.Concat(layout.groups.Select(g=>g.path)).ToArray();
            var bakedBytes=bakedPaths.Select(File.ReadAllBytes).ToArray();OceanReefArtSetup.RebuildDensity();
            for(int i=0;i<bakedPaths.Length;i++)Require(bakedBytes[i].SequenceEqual(File.ReadAllBytes(bakedPaths[i])),"density rebuild preserves unrelated component documents and is byte stable: "+bakedPaths[i]);
            var watched=new[]{OceanSetup.PrefabPath,OceanReefArtSetup.LayoutPath,path+".meta"};var bytes=watched.Select(File.ReadAllBytes).ToArray();
            try
            {
                // A changed export must never be marked as applied by the density-only operation.
                File.AppendAllText(path,"\n");bool rejected=false;
                try{OceanReefArtSetup.RebuildDensity();}catch(InvalidOperationException e){rejected=e.Message.Contains("Blender reef source changed");}
                Require(rejected,"density rebuild rejects an unapplied Blender export");
                for(int i=0;i<watched.Length;i++)Require(bytes[i].SequenceEqual(File.ReadAllBytes(watched[i])),"rejected export changes no layout, prefab or signature");
            }
            finally{File.WriteAllBytes(path,original);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);}
            Debug.Log("Coastal landscape source guard PASSED.");
        }
        public static void RebuildAndRun()
        {
            Require(Application.dataPath.Replace('\\','/').Contains("/Logs/"),"use an isolated verification project");
            string[] groundPaths={TerrainSetup.MeshFolder+"/Island Land.asset",OceanSetup.MeshFolder+"/Island Shore.asset"};
            var vertices=groundPaths.Select(p=>AssetDatabase.LoadAssetAtPath<Mesh>(p).vertices).ToArray();
            var triangles=groundPaths.Select(p=>AssetDatabase.LoadAssetAtPath<Mesh>(p).triangles).ToArray();
            string wildlife=Wildlife();
            CoastArtSetup.RebuildCoastalDetails();
            var root=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);
            try
            {
                var scatter=root.GetComponent<IslandTerrain>().PalmScatter;
                scatter.MinimumCount=16;scatter.MaximumCount=20;scatter.CrownSpacing=1.55f;
                PrefabUtility.SaveAsPrefabAsset(root,TerrainSetup.PrefabPath);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            CoastArtSetup.RebuildCoastalPalms();CoastArtSetup.RebuildCoastalDetails();
            OceanReefArtSetup.RebuildDensity();CoastArtSetup.RebuildCoastalGroundColours();
            Require(Wildlife()==wildlife,"fish, bird, crab layouts and their activity settings retained");
            for(int i=0;i<groundPaths.Length;i++)
            {
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(groundPaths[i]);
                Require(vertices[i].SequenceEqual(mesh.vertices)&&triangles[i].SequenceEqual(mesh.triangles),"all land and seabed geometry retained");
            }
            var paths=new[]{TerrainSetup.PrefabPath,OceanSetup.PrefabPath,OceanReefArtSetup.LayoutPath,OceanReefArtSetup.DataPath+".meta"}
                .Concat(groundPaths).Concat(JsonUtility.FromJson<OceanReefArtSetup.Layout>(File.ReadAllText(OceanReefArtSetup.LayoutPath)).groups.Select(g=>g.path)).ToArray();
            var bytes=paths.Select(File.ReadAllBytes).ToArray();
            CoastArtSetup.RebuildCoastalPalms();CoastArtSetup.RebuildCoastalDetails();OceanReefArtSetup.RebuildDensity();CoastArtSetup.RebuildCoastalGroundColours();
            for(int i=0;i<paths.Length;i++)Require(bytes[i].SequenceEqual(File.ReadAllBytes(paths[i])),"explicit rebuild is byte stable: "+paths[i]);
            CoastalPalmVerification.VerifyAssets();OceanReefArtVerification.Run();
            VerifyAssets();
            VerifyManualEdits(paths);
            var ocean=AssetDatabase.LoadAssetAtPath<GameObject>(OceanSetup.PrefabPath);
            OceanEcologyPolishVerification.Verify(ocean);OceanFishAcceptanceVerification.Verify(ocean);
            TerrainCoastFlowVerification.Run();
        }
        static void VerifyManualEdits(string[] paths)
        {
            var originals=paths.Select(File.ReadAllBytes).ToArray();
            try
            {
                var root=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);
                try
                {
                    var terrain=root.GetComponent<IslandTerrain>();terrain.DetailScatter.Seed+=11;
                    terrain.CoastalScenery.Cast<Transform>().First(t=>t.GetComponent<CoastalSceneryPatch>()==null).localPosition+=Vector3.right*.17f;
                    PrefabUtility.SaveAsPrefabAsset(root,TerrainSetup.PrefabPath);
                }
                finally{PrefabUtility.UnloadPrefabContents(root);}
                var land=AssetDatabase.LoadAssetAtPath<Mesh>(TerrainSetup.MeshFolder+"/Island Land.asset");var colors=land.colors;colors[120]=Color.magenta;land.colors=colors;EditorUtility.SetDirty(land);AssetDatabase.SaveAssets();
                var tuned=paths.Select(File.ReadAllBytes).ToArray();
                PrototypeAddressablesSetup.EnsureSetup();PrototypeAddressablesSetup.EnsureSetup();
                for(int i=0;i<paths.Length;i++)Require(tuned[i].SequenceEqual(File.ReadAllBytes(paths[i])),"Setup preserves manual detail layout/parameters/colours: "+paths[i]);
                root=PrefabUtility.LoadPrefabContents(OceanSetup.PrefabPath);
                try
                {
                    root.GetComponent<OceanSurface>().SwimObstacles[0].Radius+=.001f;
                    PrefabUtility.SaveAsPrefabAsset(root,OceanSetup.PrefabPath);
                }
                finally{PrefabUtility.UnloadPrefabContents(root);}
                var edited=paths.Select(File.ReadAllBytes).ToArray();bool rejected=false;
                try{OceanReefArtSetup.RebuildDensity();}catch(InvalidOperationException e){rejected=e.Message.Contains("Swim obstacles changed");}
                Require(rejected,"density rebuild rejects manually changed obstacles");
                for(int i=0;i<paths.Length;i++)Require(edited[i].SequenceEqual(File.ReadAllBytes(paths[i])),"rejected density rebuild preserves resources");
            }
            finally
            {
                for(int i=0;i<paths.Length;i++)if(!originals[i].SequenceEqual(File.ReadAllBytes(paths[i])))
                {File.WriteAllBytes(paths[i],originals[i]);AssetDatabase.ImportAsset(paths[i],ImportAssetOptions.ForceSynchronousImport);}
            }
        }
        static string Wildlife()
        {
            var ocean=AssetDatabase.LoadAssetAtPath<GameObject>(OceanSetup.PrefabPath).GetComponent<OceanSurface>();
            return JsonUtility.ToJson(ocean.CoastalPopulation)+string.Join("|",ocean.Schools.Select(s=>JsonUtility.ToJson(s)))
                +string.Join("|",ocean.Birds.Select(b=>JsonUtility.ToJson(b)))+string.Join("|",ocean.Crabs.Select(c=>JsonUtility.ToJson(c)));
        }
        public static void VerifyAssets()
        {
            var terrain=AssetDatabase.LoadAssetAtPath<GameObject>(TerrainSetup.PrefabPath).GetComponent<IslandTerrain>();
            int details=terrain.CoastalScenery.Cast<Transform>().Count(t=>t.GetComponent<CoastalSceneryPatch>()==null);
            Require(details==terrain.DetailScatter.Shrubs+terrain.DetailScatter.Shells+terrain.DetailScatter.Pebbles,"all three detail populations baked");
            Require(terrain.CoastalScenery.GetComponentsInChildren<Collider>(true).All(c=>c.GetComponent<CoastalSceneryPatch>()!=null),"details add no walking or pointer colliders");
            var layout=JsonUtility.FromJson<OceanReefArtSetup.Layout>(File.ReadAllText(OceanReefArtSetup.LayoutPath));
            var counts=layout.groups.Select(g=>g.members.Count(m=>!m.hidden)).ToArray();
            Require(counts.Min()<=4&&counts.Max()>=28,"open sand pockets and rich reef patches coexist");
            var surface=AssetDatabase.LoadAssetAtPath<GameObject>(OceanSetup.PrefabPath).GetComponent<OceanSurface>();
            Require(surface.SwimObstacles.All(o=>layout.originalObstacles.Contains(o)),"reef thinning only removes existing swim obstacles");
            Require(surface.GetComponentsInChildren<OceanHabitatPatch>().Sum(p=>p.Population)==counts.Sum(),"baked population metadata matches rendered members");
            Directory.CreateDirectory("Logs");File.WriteAllText("Logs/CoastalLandscapeAssets.txt","PASS: fixed scatter, loose palm crown spacing, reef density "+counts.Sum()+"/"+layout.groups.Sum(g=>g.members.Length)+", per-patch "+counts.Min()+"–"+counts.Max()+"; geometry/wildlife preserved; repeat rebuild byte stable; Setup/manual edit and art validation passed.\n");
        }
        public static void VerifyRuntime(GameManager game)
        {
            if(game.Terrain.Terrain.DetailScatter==null)return;
            var camera=game.Players.Player.ViewCamera;
            foreach(float size in new[]{5f,7f,9f})
            {
                float old=camera.orthographicSize;camera.orthographicSize=size;
                try{Require(camera.ViewportPointToRay(new Vector3(.5f,0,0)).origin.y>game.Ocean.Surface.SeaLevel,"normal zoom near plane stays above water");}
                finally{camera.orthographicSize=old;}
            }
            LightingFlowVerification.Capture(game,"CoastalLandscapeOverview",Vector3.zero,25);
            LightingFlowVerification.Capture(game,"CoastalLandscapeDetail",new Vector3(-9,0,-12),10);
            LightingFlowVerification.Capture(game,"CoastalLandscapeWest",new Vector3(-16,0,0),9);
            LightingFlowVerification.Capture(game,"CoastalLandscapeSouth",new Vector3(0,0,-17),9);
            LightingFlowVerification.Capture(game,"CoastalLandscapeEast",new Vector3(17,0,-5),9);
            LightingFlowVerification.Capture(game,"CoastalLandscapeNorth",new Vector3(0,0,17),9);
        }
    }
}
