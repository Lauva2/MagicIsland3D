using System;
using System.Linq;
using MagicIsland.Ocean;
using MagicIsland.Managers;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    public static class OceanEcologyRhythmVerification
    {
        static int checks;
        static void Require(bool ok,string why){checks++;if(!ok)throw new Exception("Ocean ecology rhythm: "+why);}
        public static void Run()
        {
            checks=0;var settings=new CoastalEcologyRhythm();Require(settings.IsValid,"default coastal configuration valid");
            settings.CrabNightForaging=float.NaN;Require(!settings.IsValid,"nonfinite coastal rate rejected");settings.CrabNightForaging=1.35f;
            var fish=new OceanFishAnimation();fish.NightExploration=0;Require(!fish.IsValid,"invalid fish exploration rejected");fish.NightExploration=.25f;
            var a=new CoastalActivityClock();var b=new CoastalActivityClock();a.Set(settings.Crab(1,false),6,.7f);b.Set(settings.Crab(1,false),6,.7f);
            a.Advance(1);b.Advance(1);var time=a.Time;float motion=a.Motion,foraging=a.Foraging;
            a.Set(settings.Crab(0,true),6,.7f);a.Advance(0);
            Require(a.Time==time&&a.Motion==motion&&a.Foraging==foraging,"paused weather changes do not consume phase or alter the current rate");
            b.Set(settings.Crab(0,true),6,.7f);a.Advance(9);
            for(int i=0;i<540;i++)b.Advance(1f/60);
            Require(Mathf.Abs(a.Time-b.Time)<.0001f&&Mathf.Abs(a.Motion-b.Motion)<.00001f,"continuous integrated phase survives a frame hitch and ramp endpoint");
            var first=new CoastalActivityClock();var second=new CoastalActivityClock();first.Set(settings.Crab(1,false),6,0);second.Set(settings.Crab(1,false),6,2);
            first.Set(settings.Crab(0,false),6,0);second.Set(settings.Crab(0,false),6,2);first.Advance(.2f);second.Advance(.2f);
            Require(Mathf.Abs(first.Motion-second.Motion)>.0001f,"individual phases stagger transition");
            var loaded=new CoastalActivityClock();loaded.Set(settings.Crab(0,false),6,0);
            Require(loaded.Motion==settings.CrabNightMotion&&loaded.Foraging==settings.CrabNightForaging&&loaded.Time==0,"cold night initialization uses restored environment immediately");
            loaded.Reset();Require(!loaded.Configured&&loaded.Time==0&&loaded.Motion==1,"unbind clears transient clock");
            VerifyHabits(settings);VerifyFish();
            Debug.Log("Ocean ecology rhythm rule verification PASSED: "+checks+" checks, smooth clocks, cold load, pause, individual phases, real gesture frequency, sheltered fish and threat priority.");
        }
        static void VerifyHabits(CoastalEcologyRhythm settings)
        {
            var random=UnityEngine.Random.state;int dayCrabs=0,nightCrabs=0,dayBirds=0,rainBirds=0;
            for(int animal=0;animal<12;animal++)
            {
                var day=new CoastalCrabIdle();var night=new CoastalCrabIdle();var bird=new CoastalBirdIdle();var rain=new CoastalBirdIdle();
                var crabHabits=new CoastalCrabHabits();var birdHabits=new CoastalBirdHabits();float phase=animal*.73f;
                var previousDay=CoastalCrabGesture.Resting;var previousNight=previousDay;
                var previousBird=CoastalBirdGesture.Watching;var previousRain=previousBird;
                for(int frame=0;frame<9000;frame++)
                {
                    float t=frame/30f;
                    bool dayRest=Mathf.Repeat(t*settings.CrabDayMotion+phase*2,8)<3.6f;
                    bool nightRest=Mathf.Repeat(t*settings.CrabNightMotion+phase*2,8)<3.6f;
                    day.Advance(1f/30,dayRest,phase,crabHabits,settings.CrabDayForaging);
                    night.Advance(1f/30,nightRest,phase,crabHabits,settings.CrabNightForaging);
                    bird.Advance(1f/30,true,phase,birdHabits,1);
                    rain.Advance(1f/30,true,phase,birdHabits,settings.BirdRainForaging);
                    if(day.Gesture==CoastalCrabGesture.Foraging&&previousDay!=day.Gesture)dayCrabs++;
                    if(night.Gesture==CoastalCrabGesture.Foraging&&previousNight!=night.Gesture)nightCrabs++;
                    if(bird.Gesture==CoastalBirdGesture.Pecking&&previousBird!=bird.Gesture)dayBirds++;
                    if(rain.Gesture==CoastalBirdGesture.Pecking&&previousRain!=rain.Gesture)rainBirds++;
                    previousDay=day.Gesture;previousNight=night.Gesture;previousBird=bird.Gesture;previousRain=rain.Gesture;
                }
            }
            Require(nightCrabs>dayCrabs*1.5f,"night has more actual crab foraging gestures: "+dayCrabs+" -> "+nightCrabs);
            Require(dayBirds>rainBirds*2&&rainBirds>0,"rain reduces actual pecks while retaining some activity: "+dayBirds+" -> "+rainBirds);
            Debug.Log("Ecology gesture observations: crab day/night="+dayCrabs+"/"+nightCrabs+", gull pecks day/rain="+dayBirds+"/"+rainBirds);
            Require(JsonUtility.ToJson(random)==JsonUtility.ToJson(UnityEngine.Random.state),"ecology does not consume gameplay RNG");
            Require(settings.Bird(.65f,false,1).x<settings.Bird(1,false,0).x&&settings.Bird(1,true,0).y<1,"dusk reduces flight and rain reduces bird foraging");
        }
        static OceanSchool Fixture(GameObject root,float light,float twilight)
        {
            var school=root.AddComponent<OceanSchool>();school.Species=ShallowFishSpecies.Clownfish;school.Center=new Vector3(0,-.6f,0);
            school.Radius=2.4f;school.HomeRadius=4.5f;school.Members=1;school.Animation=new OceanFishAnimation();
            school.Swimmers=new[]{new OceanFishMember{Spawn=school.Center,Radius=.12f,Bottom=-.035f,Top=.035f,Pivot=new Vector4(0,0,0,1)}};
            school.ConfigureWater(-1.3f,-.1f,new[]{new OceanSwimObstacle{Center=new Vector2(1,0),Radius=.3f,Bottom=-1.3f,Top=-.1f,Coral=true}});
            school.SetEnvironment(light,false,twilight);return school;
        }
        static void VerifyFish()
        {
            var dayObject=new GameObject("Day fish rhythm fixture");var nightObject=new GameObject("Night fish rhythm fixture");
            try
            {
                var day=Fixture(dayObject,1,0);var night=Fixture(nightObject,0,0);var a=new OceanFishSteering(day);var b=new OceanFishSteering(night);
                float dayDistance=0,nightDistance=0;
                for(int step=0;step<9000;step++)
                {
                    a.Advance(1f/30);b.Advance(1f/30);
                    var p=b.States[0];Require(b.Navigation.CanMove(b.PreviousStepState(0).Position,p.Position,b.BodyBounds(0)),"sheltering keeps the same full-body swept bounds");
                    if(step>4500){dayDistance+=Vector3.Distance(a.States[0].Position,a.States[0].ShelterTarget);nightDistance+=Vector3.Distance(p.Position,p.ShelterTarget);}
                }
                Require(b.ExplorationCount<a.ExplorationCount,"night suppresses actual exploration: "+a.ExplorationCount+" -> "+b.ExplorationCount);
                Debug.Log("Ecology fish observations: exploration day/night="+a.ExplorationCount+"/"+b.ExplorationCount+", shelter-distance sums="+dayDistance+"/"+nightDistance);
                Require(nightDistance<dayDistance*.9f,"night fish remain closer to safe coral edge: "+nightDistance+" / "+dayDistance);
                var state=a.States[0];day.SetEnvironment(.6f,false,1);a.Advance(0);
                Require(a.States[0].ShelterBlend==state.ShelterBlend&&a.States[0].Position==state.Position,"paused dusk does not change fish or habitat blend");
                a.Advance(15);Require(a.States[0].ShelterBlend>=.749f,"dusk smoothly approaches shelter");
                night.Startle(night.Center);night.React(1f/30,null);b.Advance(1f/30);Require(b.States[0].Activity==OceanFishActivity.Fleeing,"night shelter yields to disturbance immediately");
            }
            finally{Object.DestroyImmediate(dayObject);Object.DestroyImmediate(nightObject);}
        }
        public static void VerifyRuntime(GameManager game)
        {
            var ocean=game.Ocean.Surface;var bird=ocean.Birds.First(b=>b.Activity==CoastalBirdActivity.Perching);var crab=ocean.Crabs[0];var school=ocean.Schools.First(s=>s.Species==ShallowFishSpecies.Clownfish);
            double minute=game.Time.Clock.Minute;var material=EditorJsonUtility.ToJson(ocean.Water.sharedMaterial);
            try
            {
                Stage(1,false,0);float dayCrab=crab.ForagingActivity,dayMotion=crab.MotionActivity,dayBird=bird.FlightActivity;
                Stage(.6f,false,1);Require(bird.FlightActivity<dayBird*.5f&&school.Steering.States[0].ShelterBlend>.7f,"packed dusk reduces flight and attracts reef fish to shelter");
                Stage(0,false,0);Require(crab.ForagingActivity>dayCrab&&crab.MotionActivity>dayMotion,"packed night increases crab activity");
                Require(ocean.Birds.All(b=>!b.gameObject.activeSelf)&&school.Steering.States[0].ExplorationLevel<.3f,"packed night keeps existing gull visibility and limits fish exploration");
                LightingFlowVerification.SetTime(game,1320);LightingFlowVerification.Capture(game,"EcologyCrabNight",crab.transform.position+Vector3.up*.15f,2);
                Stage(1,true,0);Require(crab.ForagingActivity<dayCrab&&bird.ForagingActivity<.2f,"packed rain lowers shoreline activity");
                var before=crab.ClawPose;float motion=crab.MotionActivity,foraging=crab.ForagingActivity;float blend=school.Steering.States[0].ShelterBlend;
                ocean.Tick(0,0,false,game.Weather.Sun);
                Require(crab.ClawPose==before&&crab.MotionActivity==motion&&crab.ForagingActivity==foraging&&school.Steering.States[0].ShelterBlend==blend,"paused environment changes freeze animals and fish blend");
                var player=ocean.transform.TransformPoint(bird.LandingPoint)+Vector3.right*.65f;int takeoffs=bird.TakeoffCount;
                for(int i=0;i<45;i++)ocean.Tick(1f/30,1,true,game.Weather.Sun,player);
                Require(bird.Alarm>.99f&&bird.TakeoffCount==takeoffs+1&&bird.HeadPose==Vector2.zero,"rain still allows one priority threat takeoff and cancels peck");
                Require(material==EditorJsonUtility.ToJson(ocean.Water.sharedMaterial),"climate does not rewrite shared AA material");
                Debug.Log("Ocean ecology rhythm packed verification PASSED: day/dusk/night/rain, sheltered fish, no paused transitions and rain disturbance priority.");
            }
            finally{LightingFlowVerification.SetTime(game,minute);for(int i=0;i<450;i++)ocean.Tick(1f/30,1,false,game.Weather.Sun);}
            void Stage(float light,bool rain,float twilight)
            {for(int i=0;i<450;i++)ocean.Tick(1f/30,light,rain,game.Weather.Sun,twilight:twilight);}
        }
    }
}
