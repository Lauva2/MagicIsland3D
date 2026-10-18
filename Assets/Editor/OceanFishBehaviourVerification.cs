using System;
using System.IO;
using System.Linq;
using MagicIsland.Ocean;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    public static class OceanFishBehaviourVerification
    {
        static void Require(bool yes,string message){if(!yes)throw new Exception(message);}
        public static void Prepare()
        {
            PrototypeAddressablesSetup.EnsureSetup();OceanBiomeSetup.Rebuild();
            Verify(AssetDatabase.LoadAssetAtPath<GameObject>(OceanSetup.PrefabPath));AssetDatabase.SaveAssets();
        }
        public static void Verify(GameObject prefab)
        {
            OceanFishBoidsVerification.VerifyFlocking();
            var root=Object.Instantiate(prefab);root.SetActive(false);
            try
            {
                var surface=root.GetComponent<OceanSurface>();var floor=new OceanBiomeSetup.BakedFloor(surface.Shore.GetComponent<MeshFilter>().sharedMesh);
                VerifyReefObstacles(surface);
                foreach(var population in surface.FishPopulations)
                {
                    Require(population.IsValid,"Valid configurable species population range");
                    Require(surface.Schools.Where(s=>s.Species==population.Species).All(s=>s.Members>=population.Minimum&&s.Members<=population.Maximum),"School population lies inside species range");
                }
                var plan=OceanFishPopulation.CreatePlan(surface.FishPopulations,surface.FishSeed);
                Require(plan.Length==surface.Schools.Length,"Seeded plan matches baked school count");
                foreach(var population in surface.FishPopulations)
                    Require(plan.Where(p=>p.Species==population.Species).Select(p=>p.Members).OrderBy(n=>n).SequenceEqual(surface.Schools.Where(s=>s.Species==population.Species).Select(s=>s.Members).OrderBy(n=>n)),"Seeded school/member plan matches actual bake");
                int avoidance=0,feeding=0,exploration=0,slowFeeding=0,passages=0;
                foreach(var school in surface.Schools)
                {
                    surface.ConfigureFishHabitat(school);
                    Require(school.Swimmers.Length==school.Members,"One lightweight state per baked fish");
                    if(school.Species==ShallowFishSpecies.Butterflyfish)Require(school.Habitat==OceanBiome.ReefSlope,"Butterflyfish inhabit deeper coral terrace");
                    if(school.Species==ShallowFishSpecies.SpottedRay)Require(school.Habitat==OceanBiome.SandFlat,"Rays inhabit flat sandy water");
                    if(school.Species==ShallowFishSpecies.Clownfish)Require(school.Obstacles.Any(o=>o.Coral),"Clownfish have coral habitat nearby");
                    school.ResetSteering();school.Render(0);
                    var lowest=school.Steering.States.Select(s=>s.Position.y).ToArray();var highest=(float[])lowest.Clone();
                    var stationary=new int[school.Members];
                    for(int frame=1;frame<=1200;frame++)
                    {
                        float time=frame/30f;school.React(1f/30,null);school.Render(time);
                        for(int i=0;i<school.Members;i++)
                        {
                            var state=school.Steering.States[i];var member=school.Steering.BodyBounds(i);
                            stationary[i]=state.Velocity.magnitude<.005f?stationary[i]+1:0;
                            Require(stationary[i]<90,"No member remains motionless for three seconds");
                            lowest[i]=Mathf.Min(lowest[i],state.Position.y);highest[i]=Mathf.Max(highest[i],state.Position.y);
                            for(int j=0;j<i;j++)Require(Vector3.Distance(state.Position,school.Steering.States[j].Position)>=member.Radius+school.Steering.BodyBounds(j).Radius+.024f,"Body envelopes remain separated");
                            var from=school.Steering.PreviousStepState(i).Position;
                            Require(school.Steering.Navigation.CanMove(from,state.Position,member),"Completed fixed step stays clear: "+school.name+" frame="+frame+" member="+i);
                            if(state.FeedBlend>.5f&&state.Velocity.magnitude<.12f)slowFeeding++;
                            if(frame%30!=0)continue;
                            float height=floor.Height(state.Position.x,state.Position.z);
                            Require(state.Position.y+member.Bottom>height+.06f&&state.Position.y+member.Top<surface.SeaLevel-.09f,"Actual steered body stays above floor and below waves");
                            if(school.Species==ShallowFishSpecies.SpottedRay)Require(state.Position.y+member.Bottom<height+.55f,"Rays cruise close to sand");
                        }
                    }
                    for(int i=0;i<school.Members;i++)Require(highest[i]-lowest[i]>.01f,"Every member changes depth in its safe water column");
                    avoidance+=school.Steering.AvoidanceCount;feeding+=school.Steering.ForagingCount;exploration+=school.Steering.ExplorationCount;
                    passages+=school.Steering.PassageCount;
                    var passageSnapshot=school.Steering.States.Select(s=>s.Passage).ToArray();
                    var snapshot=school.Steering.States.Select(s=>s.Position).ToArray();school.React(0,null);school.Render(40);
                    Require(snapshot.SequenceEqual(school.Steering.States.Select(s=>s.Position)),"Pause freezes positions and behaviour timers");
                    Require(passageSnapshot.SequenceEqual(school.Steering.States.Select(s=>s.Passage)),"Pause freezes split guides, delay and regroup blend");
                    school.Startle(school.Center+Vector3.right);school.React(.3f,null);school.Render(40.3f);
                    Require(school.Steering.States.All(s=>s.Activity==OceanFishActivity.Fleeing),"Splash interrupts feeding and exploration");
                    Require(school.Steering.States.All(s=>!s.Passage.Active&&s.Passage.Blend==0),"Splash cancels detours before escape");
                    for(int step=1;step<=240;step++){school.React(1f/30,null);school.Render(40.3f+step/30f);}
                    Require(school.Alarm==0&&school.Steering.States.All(s=>s.Activity!=OceanFishActivity.Fleeing),"Fish return to normal activity after disturbance");
                }
                Require(avoidance>100&&feeding>100&&exploration>100&&slowFeeding>100,"Schools avoid reefs, slow down to feed and explore individually");
                Require(passages>10,"Actual baked habitats exercise temporary individual passage guides");
                Debug.Log("Actual AA fish passage routes: "+passages);
                VerifyFrameRates(surface.Schools[0]);VerifyDetour();
                Debug.Log("Fish behaviour verification PASSED: ranged populations, bounded free swimming, swept reef avoidance, habitats, feeding/exploration, startle/recovery, pause and frame-rate independence. Avoidance="+avoidance+", slow feeding samples="+slowFeeding);
            }
            finally{Object.DestroyImmediate(root);}
        }
        static void VerifyReefObstacles(OceanSurface surface)
        {
            var layout=JsonUtility.FromJson<OceanReefArtSetup.Layout>(File.ReadAllText(OceanReefArtSetup.LayoutPath));
            var patches=surface.GetComponentsInChildren<OceanHabitatPatch>(true);
            var hidden=new System.Collections.Generic.List<Vector2>();
            int visible=0;
            foreach(var group in layout.groups)
            {
                var patch=patches.Single(p=>AssetDatabase.GetAssetPath(p.GetComponentInChildren<MeshFilter>().sharedMesh)==group.path);
                foreach(var member in group.members.Where(m=>m.source<4||m.source==5||m.source==8))
                {
                    var p=surface.transform.InverseTransformPoint(patch.transform.TransformPoint(member.position));var center=new Vector2(p.x,p.z);
                    bool found=surface.SwimObstacles.Any(o=>Vector2.Distance(o.Center,center)<.002f);
                    Require(found!=member.hidden,"Obstacle matches authored visible reef member");
                    if(member.hidden)hidden.Add(center);else visible++;
                }
            }
            Require(visible>0&&surface.SwimObstacles.Length>=visible,"Visible coral and rocks have navigation volumes");
            if(layout.originalObstacles!=null&&layout.originalObstacles.Length>0)
                Require(layout.originalObstacles.Where(o=>!hidden.Any(c=>Vector2.Distance(c,o.Center)<.002f)).SequenceEqual(surface.SwimObstacles),"Density preserves original obstacle envelopes and removes only hidden members");
        }
        static void VerifyFrameRates(OceanSchool school)
        {
            school.ResetSteering();school.Render(0);
            for(int i=1;i<=600;i++)school.Render(i/30f);
            var positions=school.Steering.States.Select(s=>s.Position).ToArray();school.ResetSteering();school.Render(0);
            for(int i=1;i<=1200;i++)school.Render(i/60f);
            for(int i=0;i<positions.Length;i++)Require(Vector3.Distance(positions[i],school.Steering.States[i].Position)<.0001f,"30 / 60 fps produce the same member trajectories");
        }
        static void VerifyDetour()
        {
            var root=new GameObject("Fish detour fixture");
            try
            {
                var school=root.AddComponent<OceanSchool>();school.Members=1;school.HomeRadius=4;school.Radius=1.7f;school.Speed=.4f;
                school.Obstacles=new[]{new OceanSwimObstacle{Center=Vector2.zero,Radius=.7f,Bottom=-1,Top=1}};
                school.Swimmers=new[]{new OceanFishMember{Pivot=new Vector4(0,0,0,1),Radius=.15f,Bottom=-.05f,Top=.05f,Spawn=new Vector3(-1.8f,0,0)}};
                var simulation=new OceanFishSteering(school);var state=simulation.States[0];state.Activity=OceanFishActivity.Exploring;state.Target=new Vector3(1.8f,0,0);state.Until=100;state.Yaw=Mathf.PI/2;simulation.States[0]=state;
                float sideways=0,furthestX=-1.8f;
                for(int i=0;i<900;i++)
                {
                    var before=simulation.States[0].Position;simulation.Advance(1f/30);var after=simulation.States[0].Position;
                    Require(simulation.Navigation.CanMove(before,after,school.Swimmers[0]),"Thin-step obstacle sweep cannot tunnel through coral");sideways=Mathf.Max(sideways,Mathf.Abs(after.z));furthestX=Mathf.Max(furthestX,after.x);
                }
                Require(sideways>.8f&&furthestX>1,"Fish takes a visible detour around a blocking reef and reaches the far side before circling");
            }
            finally{Object.DestroyImmediate(root);}
        }
    }
}
