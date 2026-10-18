using System;
using System.Linq;
using MagicIsland.Ocean;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    public static partial class OceanBiomeSetup
    {
        // Explicit fish-only rebake: validates and stages all safe habitats before committing
        // fish mesh changes. Leaves seabed, coral, materials, birds and crabs as authored.
        [MenuItem("Magic Island/Rebuild Fish Schools")]
        public static void RebuildFishSchools()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            var root=PrefabUtility.LoadPrefabContents(OceanSetup.PrefabPath);
            try
            {
                var surface=root.GetComponent<OceanSurface>();
                OceanFishPopulation.CreatePlan(surface.FishPopulations,surface.FishSeed);
                var old=root.transform.Find("Reef fish schools");
                var pending=new GameObject("Reef fish schools (pending)");pending.layer=2;pending.transform.SetParent(root.transform,false);
                var templates=surface.Schools.Where(s=>s!=null).ToArray();
                BuildFishSchools(root,surface,new OceanGeography(surface.GeographySeed),AssetDatabase.LoadAssetAtPath<Material>(MaterialPath),pending.transform,templates);
                if(old!=null)Object.DestroyImmediate(old.gameObject);pending.name="Reef fish schools";
                surface.EcosystemRenderers=root.GetComponentsInChildren<Renderer>(true).Where(r=>r!=surface.Water&&r!=surface.Shore&&r.GetComponent<OceanEffectParticle>()==null).ToArray();
                PrefabUtility.SaveAsPrefabAsset(root,OceanSetup.PrefabPath);AssetDatabase.SaveAssets();
                Debug.Log("Fish schools rebaked: seed="+surface.FishSeed+", schools="+surface.Schools.Length+", fish="+surface.Schools.Sum(s=>s.Members));
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }

        public static void VerifyFishScatter()
        {
            var root=PrefabUtility.LoadPrefabContents(OceanSetup.PrefabPath);
            var temporary=new System.Collections.Generic.List<OceanSchool>();
            try
            {
                var surface=root.GetComponent<OceanSurface>();var original=surface.Schools.ToArray();
                OceanSchool[] repeated=null;Vector3[] positions=null;
                // Authored homes survive reef-density edits, while a fresh bake uses the
                // current obstacle set. Compare two fresh plans with identical inputs.
                for(int pass=0;pass<3;pass++)
                {
                    if(pass==2)surface.FishSeed++;
                    var stage=new GameObject("Fish scatter verification "+pass);stage.transform.SetParent(root.transform,false);
                    BuildFishSchools(root,surface,new OceanGeography(surface.GeographySeed),AssetDatabase.LoadAssetAtPath<Material>(MaterialPath),stage.transform,original,false);
                    temporary.AddRange(surface.Schools);
                    if(pass==0)
                    {
                        if(surface.Schools.Length!=original.Length)throw new Exception("Repeat seed changed school count");
                        repeated=surface.Schools.ToArray();positions=repeated.Select(s=>s.Center).ToArray();
                    }
                    else if(pass==1)
                    {
                        if(surface.Schools.Length!=repeated.Length)throw new Exception("Repeat seed changed school count");
                        for(int i=0;i<repeated.Length;i++)
                            if(surface.Schools[i].Members!=repeated[i].Members||Vector3.Distance(surface.Schools[i].Center,positions[i])>.0001f||!surface.Schools[i].Swimmers.SequenceEqual(repeated[i].Swimmers))throw new Exception("Repeat seed changed fish count/position/spawns");
                    }
                    else if(surface.Schools.Length==repeated.Length&&surface.Schools.Select((s,i)=>Vector3.Distance(s.Center,positions[i])<.0001f).All(same=>same))
                        throw new Exception("Different fish seed did not change layout");
                    foreach(var school in surface.Schools)
                        for(int i=0;i<school.Members;i++)if(!school.Steering.Navigation.CanMove(school.Steering.States[i].Position,school.Steering.States[i].Position,school.Steering.BodyBounds(i)))throw new Exception("Random fish has unsafe full body spawn");
                }
                Debug.Log("Fish scatter verification PASSED: same seed reproduces count/position; different seed changes layout; safe whole-body spawns; no asset writes.");
            }
            finally
            {
                foreach(var school in temporary)if(school!=null){var mesh=school.GetComponent<MeshFilter>().sharedMesh;if(mesh!=null&&!AssetDatabase.Contains(mesh))Object.DestroyImmediate(mesh);}
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
