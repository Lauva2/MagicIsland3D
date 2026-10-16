using System;
using System.Linq;
using MagicIsland.Ocean;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    public static partial class OceanBiomeSetup
    {
        [MenuItem("Magic Island/Rebuild Coastal Wildlife")]
        public static void RebuildCoastalWildlife()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            var root=PrefabUtility.LoadPrefabContents(OceanSetup.PrefabPath);
            try
            {
                var surface=root.GetComponent<OceanSurface>();
                BuildCoastalWildlife(root,surface,AssetDatabase.LoadAssetAtPath<Material>(MaterialPath));
                PrepareCrabHabits();
                PrefabUtility.SaveAsPrefabAsset(root,OceanSetup.PrefabPath);AssetDatabase.SaveAssets();
                Debug.Log("Coastal wildlife rebaked: seed="+surface.CoastalPopulation.Seed+", gulls="+surface.Birds.Length+", crabs="+surface.Crabs.Length);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        internal static void BuildCoastalWildlife(GameObject root,OceanSurface surface,Material material)
        {
            var plan=surface.CoastalPopulation.CreatePlan();
            if(material==null)throw new Exception("Coastal wildlife shared material is missing");
            var random=new System.Random(unchecked(surface.CoastalPopulation.Seed+1807));
            var old=root.transform.Find("Coastal wildlife");
            var oldBirds=surface.Birds;var oldCrabs=surface.Crabs;
            var pending=new GameObject("Coastal wildlife (pending)");pending.layer=2;pending.transform.SetParent(root.transform,false);
            try
            {
                var body=WildlifeMesh("Seagull Body",GullBody);
                var left=WildlifeMesh("Seagull Left Wing",()=>GullWing(-1));
                var right=WildlifeMesh("Seagull Right Wing",()=>GullWing(1));
                var crabMesh=WildlifeMesh("Shore Crab",CrabMesh);
                var birds=new CoastalBird[plan.Birds.Length];var crabs=new CoastalCrab[plan.Crabs.Length];
                for(int i=0;i<birds.Length;i++)
                {
                    var site=plan.Birds[i];var activity=(CoastalBirdActivity)site.Activity;
                    float y=activity==CoastalBirdActivity.Perching?(float)MagicIsland.Interaction.IslandLayout.Height(site.X,site.Z):activity==CoastalBirdActivity.Skimming?.28f:(float)Range(random,3,5.5);
                    var go=MeshObject(pending.transform,"Seagull "+i,body,material,Vector3.zero);go.transform.localScale=Vector3.one*(float)site.Scale;
                    go.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.On;
                    var bird=go.AddComponent<CoastalBird>();bird.Activity=activity;bird.Center=new Vector3((float)site.X,y,(float)site.Z);
                    bird.Radius=(float)site.Travel;bird.Speed=(float)site.Speed;bird.Phase=(float)site.Phase;
                    bird.ActivityPeriod=(float)site.Period;bird.RestFraction=(float)site.Rest;bird.FlapSpeed=(float)Range(random,4.1,6.3);
                    bird.LeftWing=MeshObject(go.transform,"Left wing",left,material,new Vector3(-.07f,.27f,0)).transform;
                    bird.RightWing=MeshObject(go.transform,"Right wing",right,material,new Vector3(.07f,.27f,0)).transform;
                    bird.LeftWing.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.On;bird.RightWing.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.On;
                    var template=oldBirds?.FirstOrDefault(b=>b!=null&&b.Activity==activity);
                    CopyWildlifeStyle(template,bird,new[]{"ReactionDistance","EscapeDistance","EscapeHeight","QuietSeconds","TakeoffSeconds","RecoverySeconds","LandingSearchDistance","FlightSpeed","ReturnStagger"});
                    if(template?.Habits!=null)bird.Habits=JsonUtility.FromJson<CoastalBirdHabits>(JsonUtility.ToJson(template.Habits));
                    bird.Render(0,1,false);birds[i]=bird;
                }
                for(int i=0;i<crabs.Length;i++)
                {
                    var site=plan.Crabs[i];var go=MeshObject(pending.transform,"Shore crab "+i,crabMesh,material,Vector3.zero);go.transform.localScale=Vector3.one*(float)site.Scale;
                    var crab=go.AddComponent<CoastalCrab>();crab.Center=new Vector3((float)site.X,(float)MagicIsland.Interaction.IslandLayout.Height(site.X,site.Z),(float)site.Z);
                    crab.Tangent=new Vector3((float)-Math.Sin(site.Angle),0,(float)Math.Cos(site.Angle));
                    crab.Phase=(float)site.Phase;crab.Travel=(float)site.Travel;crab.CrawlPeriod=(float)site.Period;crab.RestFraction=(float)site.Rest;
                    CopyWildlifeStyle(oldCrabs?.FirstOrDefault(c=>c!=null),crab,new[]{"ReactionDistance","EscapeDistance","QuietSeconds","EscapeSeconds","RecoverySeconds","WalkSpeed","FleeSpeed","ReturnStagger"});
                    var template=oldCrabs?.FirstOrDefault(c=>c!=null);
                    if(template?.Habits!=null)crab.Habits=JsonUtility.FromJson<CoastalCrabHabits>(JsonUtility.ToJson(template.Habits));
                    // The plan reserved a maximum 1.9 metre escape; fail rather than truncate a manual edit.
                    if(crab.EscapeDistance>1.9f||crab.EscapeDistance<0)throw new Exception("Crab escape exceeds reserved coastal envelope");
                    crab.Render(0);crabs[i]=crab;
                }
                if(old!=null)Object.DestroyImmediate(old.gameObject);pending.name="Coastal wildlife";
                surface.Birds=birds;surface.Crabs=crabs;
                surface.EcosystemRenderers=root.GetComponentsInChildren<Renderer>(true).Where(r=>r!=surface.Water&&r!=surface.Shore&&r.GetComponent<OceanEffectParticle>()==null).ToArray();
            }
            catch{Object.DestroyImmediate(pending);throw;}
        }
        static Mesh WildlifeMesh(string name,Func<Mesh> create)
            =>AssetDatabase.LoadAssetAtPath<Mesh>(MeshFolder+"/"+name+".asset")??SaveMesh(MeshFolder+"/"+name+".asset",create());
        [MenuItem("Magic Island/Prepare Crab Habits")]
        public static void PrepareCrabHabits()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(MeshFolder+"/Shore Crab.asset");
            if(mesh==null)throw new Exception("Shore Crab mesh is missing; run the initial ocean Setup first");
            if(TagCrabClaws(mesh)){EditorUtility.SetDirty(mesh);AssetDatabase.SaveAssets();}
        }
        internal static bool TagCrabClaws(Mesh mesh)
        {
            var uv=new System.Collections.Generic.List<Vector4>();mesh.GetUVs(1,uv);
            if(uv.Count==mesh.vertexCount&&uv.Any(v=>v.w==110)&&uv.Any(v=>v.w==111))return false;
            var reference=CrabMesh();
            try
            {
                // Refuse edited geometry instead of overwriting the artist's work.
                if(!mesh.vertices.SequenceEqual(reference.vertices)||!mesh.triangles.SequenceEqual(reference.triangles))
                    throw new Exception("Edited Shore Crab geometry: annotate claw pivots in UV channel 1 (tags 110/111); original mesh preserved");
                if(uv.Count!=mesh.vertexCount)throw new Exception("Shore Crab leg metadata is missing; original mesh preserved");
                var source=new System.Collections.Generic.List<Vector4>();reference.GetUVs(1,source);
                for(int i=0;i<source.Count;i++)if(source[i].w==110||source[i].w==111)uv[i]=source[i];
                mesh.SetUVs(1,uv);return true;
            }
            finally{Object.DestroyImmediate(reference);}
        }
        static void CopyWildlifeStyle(Component source,Component target,string[] fields)
        {
            if(source==null)return;
            foreach(string name in fields){var field=source.GetType().GetField(name);field.SetValue(target,field.GetValue(source));}
        }
    }
}
