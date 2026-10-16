using System;
using System.IO;
using System.Linq;
using MagicIsland.CameraSystem;
using MagicIsland.Managers;
using MagicIsland.TerrainSystem;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    public static class CoastalPalmVerification
    {
        static void Require(bool ok,string why){if(!ok)throw new Exception("Coastal palms: "+why);}

        // Run this entry point in an isolated verification project; the regular asset check is read-only.
        public static void RebuildAndRun()
        {
            Require(Application.dataPath.Replace('\\','/').Contains("/Logs/"),"rebuild verification requires an isolated project");
            var props=PropTransforms();
            string guid=AssetDatabase.AssetPathToGUID(TerrainSetup.PrefabPath);
            CoastArtSetup.RebuildCoastalPalms();
            var terrain=AssetDatabase.LoadAssetAtPath<GameObject>(TerrainSetup.PrefabPath).GetComponent<IslandTerrain>();
            var after=PropTransforms();
            Require(props.SequenceEqual(after),"shrubs, shells and other scenery retain their transforms; removed="+string.Join(";",props.Except(after))+", added="+string.Join(";",after.Except(props)));
            Require(guid==AssetDatabase.AssetPathToGUID(TerrainSetup.PrefabPath),"terrain GUID retained");
            var baked=File.ReadAllBytes(TerrainSetup.PrefabPath);CoastArtSetup.RebuildCoastalPalms();
            Require(baked.SequenceEqual(File.ReadAllBytes(TerrainSetup.PrefabPath)),"same seed rebuild is byte stable");
            VerifyAssets();
            // Explicitly tune a nested instance and scatter setting; ordinary Setup must preserve both.
            try
            {
                var root=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);
                try
                {
                    var marker=root.GetComponent<IslandTerrain>();marker.PalmScatter.Seed+=17;
                    marker.CoastalScenery.GetComponentInChildren<CoastalSceneryPatch>().transform.localPosition+=Vector3.right*.13f;
                    PrefabUtility.SaveAsPrefabAsset(root,TerrainSetup.PrefabPath);
                }
                finally{PrefabUtility.UnloadPrefabContents(root);}
                var tuned=File.ReadAllBytes(TerrainSetup.PrefabPath);
                PrototypeAddressablesSetup.EnsureSetup();PrototypeAddressablesSetup.EnsureSetup();AssetDatabase.SaveAssets();
                Require(tuned.SequenceEqual(File.ReadAllBytes(TerrainSetup.PrefabPath)),"ordinary Setup preserves edited palm layout and seed");
            }
            finally
            {
                File.WriteAllBytes(TerrainSetup.PrefabPath,baked);
                AssetDatabase.ImportAsset(TerrainSetup.PrefabPath,ImportAssetOptions.ForceSynchronousImport);
            }
            TerrainCoastFlowVerification.Run();
        }
        static string[] PropTransforms()
        {
            var root=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);
            try
            {
                return root.GetComponent<IslandTerrain>().CoastalScenery.GetComponentsInChildren<Transform>(true)
                    .Where(t=>t.GetComponentInParent<CoastalSceneryPatch>(true)==null)
                    .Select(t=>t.name+"|"+t.localPosition.ToString("R")+"|"+t.localRotation.ToString("R")+"|"+t.localScale.ToString("R"))
                    .OrderBy(value=>value,StringComparer.Ordinal).ToArray();
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }

        public static void VerifyAssets()
        {
            var terrain=AssetDatabase.LoadAssetAtPath<GameObject>(TerrainSetup.PrefabPath).GetComponent<IslandTerrain>();
            VerifyPlan(terrain);
            Require(terrain.Paths!=null&&terrain.SeasonalDetails!=null,"existing terrain dependencies retained");
        }
        static void VerifyPlan(IslandTerrain terrain)
        {
            var plan=terrain.PalmScatter.CreatePlan();var palms=terrain.CoastalScenery.GetComponentsInChildren<CoastalSceneryPatch>(true);
            Require(plan.Length==palms.Length,"baked count matches settings");
            for(int i=0;i<plan.Length;i++)
            {
                var t=palms[i].transform;var s=plan[i];
                Require(Vector2.Distance(new Vector2(t.localPosition.x,t.localPosition.z),new Vector2((float)s.X,(float)s.Z))<.0001f,"baked position matches fixed plan");
                Require(Mathf.Abs(t.localScale.x-(float)s.Scale)<.0001f&&Mathf.Abs(Mathf.DeltaAngle(t.localEulerAngles.y,(float)s.Yaw))<.001f,"baked size and heading match plan");
                Require(palms[i].Obstacle!=null&&palms[i].Obstacle.enabled&&palms[i].gameObject.layer==2,"solid trunk and pointer filtering retained");
                Require(palms[i].GetComponent<CameraOccluder>()!=null,"camera fade retained");
                Require(PrefabUtility.IsPartOfPrefabInstance(palms[i]),"nested CoastalPalm dependency retained");
            }
        }
        public static void VerifyRuntime(GameManager game)
        {
            // Runtime instances are unpacked from AA, so only compare actual transforms here.
            var terrain=game.Terrain.Terrain;var plan=terrain.PalmScatter.CreatePlan();
            var palms=terrain.CoastalScenery.GetComponentsInChildren<CoastalSceneryPatch>(true);
            Require(palms.Length==plan.Length,"packed AA carries the rebaked palm count");
            for(int i=0;i<plan.Length;i++)
                Require(Vector2.Distance(new Vector2(palms[i].transform.localPosition.x,palms[i].transform.localPosition.z),new Vector2((float)plan[i].X,(float)plan[i].Z))<.0001f,"packed AA carries fixed palm positions");
            LightingFlowVerification.SetTime(game,720);
            LightingFlowVerification.Capture(game,"CoastalPalmsOverview",Vector3.zero,25);
            LightingFlowVerification.Capture(game,"CoastalPalmsDetail",new Vector3(-10,0,-12),10);
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/CoastalPalmVerification.txt","PASS: deterministic baked palm positions loaded from packed AA, GUID and non-palm scenery preservation, repeated explicit rebuild, manually tuned layout preserved by ordinary Setup.\n");
        }
    }
}
