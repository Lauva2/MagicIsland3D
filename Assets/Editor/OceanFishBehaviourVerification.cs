using System;
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
                Require(surface.SwimObstacles.Length>1500,"Individual coral and rock navigation volumes baked into AA ocean");
                foreach(var population in surface.FishPopulations)
                {
                    Require(population.IsValid,"Valid configurable species population range");
                    Require(surface.Schools.Where(s=>s.Species==population.Species).All(s=>s.Members>=population.Minimum&&s.Members<=population.Maximum),"School population lies inside species range");
                }
                Require(surface.Schools.GroupBy(s=>s.Species).Count(g=>g.Select(s=>s.Members).Distinct().Count()>1)>=3,"Generated populations vary between schools of the same species");
                int avoidance=0,feeding=0,exploration=0,slowFeeding=0;
                foreach(var school in surface.Schools)
                {
                    surface.ConfigureFishHabitat(school);
                    Require(school.Swimmers.Length==school.Members,"One lightweight state per baked fish");
                    if(school.Species==ShallowFishSpecies.Butterflyfish)Require(school.Habitat==OceanBiome.ReefSlope,"Butterflyfish inhabit deeper coral terrace");
                    if(school.Species==ShallowFishSpecies.SpottedRay)Require(school.Habitat==OceanBiome.SandFlat,"Rays inhabit flat sandy water");
                    if(school.Species==ShallowFishSpecies.Clownfish)Require(school.Obstacles.Any(o=>o.Coral),"Clownfish have coral habitat nearby");
                    school.ResetSteering();school.Render(0);var previous=new Vector3[school.Members];
                    var lowest=school.Steering.States.Select(s=>s.Position.y).ToArray();var highest=(float[])lowest.Clone();
                    var stationary=new int[school.Members];
                    for(int frame=1;frame<=1200;frame++)
                    {
                        for(int i=0;i<previous.Length;i++)previous[i]=school.Steering.States[i].Position;
                        float time=frame/30f;school.React(1f/30,null);school.Render(time);
                        for(int i=0;i<previous.Length;i++)
                        {
                            var state=school.Steering.States[i];var member=school.Steering.BodyBounds(i);
                            stationary[i]=state.Velocity.magnitude<.005f?stationary[i]+1:0;
                            Require(stationary[i]<90,"No member remains motionless for three seconds");
                            lowest[i]=Mathf.Min(lowest[i],state.Position.y);highest[i]=Mathf.Max(highest[i],state.Position.y);
                            for(int j=0;j<i;j++)Require(Vector3.Distance(state.Position,school.Steering.States[j].Position)>=member.Radius+school.Steering.BodyBounds(j).Radius+.024f,"Body envelopes remain separated");
                            Require(school.Steering.Navigation.CanMove(previous[i],state.Position,member),"Every movement segment clears reef objects and stays inside the home range: "+school.name+" frame="+frame+" member="+i+" from="+previous[i].ToString("F7")+" to="+state.Position.ToString("F7")+" endpoint="+school.Steering.Navigation.CanMove(state.Position,state.Position,member));
                            if(state.FeedBlend>.5f&&state.Velocity.magnitude<.12f)slowFeeding++;
                            if(frame%30!=0)continue;
                            float height=floor.Height(state.Position.x,state.Position.z);
                            Require(state.Position.y+member.Bottom>height+.06f&&state.Position.y+member.Top<surface.SeaLevel-.09f,"Actual steered body stays above floor and below waves");
                            if(school.Species==ShallowFishSpecies.SpottedRay)Require(state.Position.y+member.Bottom<height+.55f,"Rays cruise close to sand");
                        }
                    }
                    for(int i=0;i<school.Members;i++)Require(highest[i]-lowest[i]>.01f,"Every member changes depth in its safe water column");
                    avoidance+=school.Steering.AvoidanceCount;feeding+=school.Steering.ForagingCount;exploration+=school.Steering.ExplorationCount;
                    var snapshot=school.Steering.States.Select(s=>s.Position).ToArray();school.React(0,null);school.Render(40);
                    Require(snapshot.SequenceEqual(school.Steering.States.Select(s=>s.Position)),"Pause freezes positions and behaviour timers");
                    school.Startle(school.Center+Vector3.right);school.React(.3f,null);school.Render(40.3f);
                    Require(school.Steering.States.All(s=>s.Activity==OceanFishActivity.Fleeing),"Splash interrupts feeding and exploration");
                    for(int step=1;step<=240;step++){school.React(1f/30,null);school.Render(40.3f+step/30f);}
                    Require(school.Alarm==0&&school.Steering.States.All(s=>s.Activity!=OceanFishActivity.Fleeing),"Fish return to normal activity after disturbance");
                }
                Require(avoidance>100&&feeding>100&&exploration>100&&slowFeeding>100,"Schools avoid reefs, slow down to feed and explore individually");
                VerifyFrameRates(surface.Schools[0]);VerifyDetour();
                Debug.Log("Fish behaviour verification PASSED: ranged populations, bounded free swimming, swept reef avoidance, habitats, feeding/exploration, startle/recovery, pause and frame-rate independence. Avoidance="+avoidance+", slow feeding samples="+slowFeeding);
            }
            finally{Object.DestroyImmediate(root);}
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
