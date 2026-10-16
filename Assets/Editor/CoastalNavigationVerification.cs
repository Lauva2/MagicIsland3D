using System;
using MagicIsland.Interaction;
using MagicIsland.Ocean;
using MagicIsland.Managers;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    public static class CoastalNavigationVerification
    {
        static int checks;
        static void Require(bool ok,string why){checks++;if(!ok)throw new Exception("Coastal navigation: "+why);}
        static Vector3 Home
        {
            get{double angle=-Math.PI/2,r=IslandLayout.CoastRadius(angle)-1.28;return new Vector3(0,(float)IslandLayout.Height(0,-r)-.008f,(float)-r);}
        }
        static GameObject Box(Transform parent,Vector3 p,Vector3 size)
        {
            var go=new GameObject("Coastal navigation fixture obstacle",typeof(BoxCollider));go.transform.SetParent(parent,false);go.transform.localPosition=p;
            var collider=go.GetComponent<BoxCollider>();collider.size=size;collider.center=Vector3.up*(size.y*.5f);return go;
        }
        public static void VerifyRuntime(GameManager game)
        {
            Require(game.Ocean.OwnsCoastalNavigation,"successful AA load owns one navigation service");
            var ocean=game.Ocean.Surface;var bird=ocean.Birds.First(b=>b.Activity==CoastalBirdActivity.Perching);
            float period=bird.ActivityPeriod,rest=bird.RestFraction,phase=bird.Phase;
            GameObject obstacle=null;
            try
            {
                bird.ActivityPeriod=1000;bird.RestFraction=.9f;bird.Phase=0;
                obstacle=Box(ocean.transform.parent,bird.Center,new Vector3(8,1.4f,8));Physics.SyncTransforms();
                for(int i=0;i<150;i++)ocean.Tick(1f/30,1,false,game.Weather.Sun,game.Players.Player.transform.position);
                Require(bird.WaitingForLanding&&bird.transform.localPosition.y>bird.LandingPoint.y+1.8f,"actual AA gull loiters when all local landings become occupied: waiting="+bird.WaitingForLanding+", position="+bird.transform.localPosition+", landing="+bird.LandingPoint);
                float scale=Mathf.Max(bird.transform.localScale.x,bird.transform.localScale.z);
                foreach(var renderer in bird.GetComponentsInChildren<MeshFilter>())foreach(var vertex in renderer.sharedMesh.vertices)
                {
                    var p=ocean.transform.InverseTransformPoint(renderer.transform.TransformPoint(vertex))-bird.transform.localPosition;
                    Require(Mathf.Abs(p.x)<scale*1.001f&&Mathf.Abs(p.z)<scale*1.001f&&p.y>-.351f*scale&&p.y<1.051f*scale,"actual AA wings and body fit queried flight envelope");
                }
                int takeoffs=bird.TakeoffCount;var position=bird.transform.localPosition;var rotation=bird.transform.localRotation;
                ocean.Tick(0,1,false,game.Weather.Sun,game.Players.Player.transform.position);
                Require(position==bird.transform.localPosition&&rotation==bird.transform.localRotation,"paused AA loiter freezes retry and movement");
                for(int i=0;i<60;i++)ocean.Tick(1f/30,1,false,game.Weather.Sun,game.Players.Player.transform.position);
                Require(takeoffs==bird.TakeoffCount,"continuing blocked landing does not repeat takeoffs");
                Object.DestroyImmediate(obstacle);obstacle=null;Physics.SyncTransforms();
                for(int i=0;i<360;i++)ocean.Tick(1f/30,1,false,game.Weather.Sun,game.Players.Player.transform.position);
                Require(!bird.WaitingForLanding&&Vector3.Distance(bird.transform.localPosition,bird.LandingPoint)<.2f,"actual AA gull finds and reaches a cleared landing");
                LightingFlowVerification.Capture(game,"CoastalSafeLanding",bird.LandingPoint+Vector3.up*.5f,5);
                Debug.Log("Coastal packed navigation verification PASSED: dynamic occupied landings, loiter, pause, one takeoff and safe recovery.");
            }
            finally
            {
                bird.ActivityPeriod=period;bird.RestFraction=rest;bird.Phase=phase;
                if(obstacle!=null)Object.DestroyImmediate(obstacle);Physics.SyncTransforms();
            }
        }
        public static void Run()
        {
            checks=0;var world=new GameObject("Coastal navigation isolated fixture");
            var probe=new CoastalWildlifePhysics(world.transform,world.transform,null,null);
            var nav=new CoastalWildlifeNavigation(3){SolidClear=probe.ClearVolume};
            try
            {
                var home=Home;var obstacle=Box(world.transform,CoastalWildlifeNavigation.Along(home,.85f),new Vector3(.2f,.7f,.2f));Physics.SyncTransforms();
                Require(!nav.GroundClear(home,CoastalWildlifeNavigation.Along(home,1.8f),home,3,.35f,.3f),"sweep detects narrow trunk between clear endpoints");
                var route=new CoastalGroundRoute();var p=home;var goal=CoastalWildlifeNavigation.Along(home,1.8f);
                for(int i=0;i<240;i++)
                {
                    var next=route.Move(nav,p,goal,home,3,.35f,.3f,.7f,1f/30);
                    Require(nav.GroundClear(p,next,home,3,.35f,.3f),"every submitted ground step is a safe full-body sweep");
                    Require(new Vector2(p.x-next.x,p.z-next.z).magnitude<.024f,"bounded horizontal crab movement");
                    Require(Mathf.Abs(next.y-((float)IslandLayout.Height(next.x,next.z)-.008f))<.0001f,"crab follows existing ground height seam");p=next;
                }
                Require(route.Detours>0&&Vector3.Distance(p,goal)<.1f,"crab completes stable radial detour around tree");
                for(int i=0;i<20;i++)
                {
                    p=route.Move(nav,p,goal,home,3,.35f,.3f,.7f,1f/30);
                    Require(!route.Waiting,"arrived crab stays settled between route retries");
                }
                var denied=new CoastalWildlifeNavigation(0){SolidClear=(a,b,r,h)=>false};
                var deniedRoute=new CoastalGroundRoute();deniedRoute.Move(denied,goal,goal,home,3,.35f,.3f,.7f,1f/30);
                Require(deniedRoute.Waiting,"occupied current goal never counts as safe rest");denied.Clear();
                foreach(float delta in new[]{.25f,.1f,1f/60})
                {
                    var slow=new CoastalGroundRoute();var sample=home;
                    for(int i=0;i<Mathf.CeilToInt(8/delta);i++)
                    {
                        sample=slow.Move(nav,sample,goal,home,3,.35f,.3f,.7f,delta);
                        Require(nav.BodyClear(sample,sample,.35f,.3f),"low and high frame rates keep full body outside trunk");
                    }
                    Require(Vector3.Distance(sample,goal)<.1f,"low and high frame rates finish safe detour");
                }
                var frozen=route.Move(nav,p,home,home,3,.35f,.3f,4,0);Require(frozen==p,"paused route does not advance");
                Object.DestroyImmediate(obstacle);
                obstacle=Box(world.transform,p,new Vector3(.5f,.7f,.5f));Physics.SyncTransforms();
                for(int i=0;i<180;i++)p=route.Move(nav,p,home,home,3,.35f,.3f,.7f,1f/30);
                Require(nav.BodyClear(p,p,.35f,.3f)&&Vector3.Distance(p,home)<.1f,"new object placed over crab permits continuous safe escape and return");
                Object.DestroyImmediate(obstacle);
                var unrelated=Box(null,home,new Vector3(1,1,1));Physics.SyncTransforms();
                try{Require(nav.BodyClear(home,home,.35f,.3f),"another world's collider is not owned");}
                finally{Object.DestroyImmediate(unrelated);}
                obstacle=Box(world.transform,goal,new Vector3(.7f,1,.7f));Physics.SyncTransforms();
                Require(!nav.LandingClear(goal,0,.46f,.55f),"new decoration blocks a previous landing");
                Require(nav.FindLanding(goal,0,.46f,.55f,3,.2f,out var alternate)&&Vector3.Distance(goal,alternate)>.5f,"safe alternative landing selected");
                nav.Reserve(0,alternate,.46f);Require(!nav.LandingClear(alternate,1,.46f,.55f),"two birds cannot reserve the same landing");
                nav.Release(0);nav.SetPlayer(alternate);Require(!nav.LandingClear(alternate,1,.46f,.55f),"player standing on landing prevents descent");
                nav.SetPlayer(home);Require(!nav.BodyClear(home-Vector3.right*2,home+Vector3.right*2,.35f,.3f),"large step cannot cross player's feet");
                Require(nav.BodyClear(home+Vector3.right*.1f,home+Vector3.right*.3f,.35f,.3f),"overlapping player permits continuous escape away");
                nav.SetPlayer(null);Object.DestroyImmediate(obstacle);
                obstacle=Box(world.transform,home,new Vector3(10,1.5f,6));Physics.SyncTransforms();
                var birdObject=new GameObject("Blocked landing bird",typeof(CoastalBird));birdObject.transform.SetParent(world.transform,false);
                var bird=birdObject.GetComponent<CoastalBird>();bird.Activity=CoastalBirdActivity.Perching;bird.Center=home;bird.ActivityPeriod=100;bird.RestFraction=.9f;
                bird.Render(0,1,false);bird.transform.localPosition=home+Vector3.up*3;bird.BindNavigation(nav,0);
                var previous=bird.transform.localPosition;
                for(int i=1;i<=120;i++)
                {
                    bird.Render(i/30f,1,false);var next=bird.transform.localPosition;
                    Require(nav.BodyClear(previous-Vector3.up*.35f,next-Vector3.up*.35f,1,1.4f),"loiter path clears actual solid volume");
                    Require(Vector3.Distance(previous,next)<.201f,"blocked landing never teleports");previous=next;
                }
                Require(bird.WaitingForLanding&&bird.transform.localPosition.y>home.y+2,"blocked coast keeps gull circling");
                var rotation=bird.transform.localRotation;bird.Render(4,1,false);Require(previous==bird.transform.localPosition&&rotation==bird.transform.localRotation,"waiting gull freezes during pause");
                Object.DestroyImmediate(obstacle);Physics.SyncTransforms();
                for(int i=121;i<=360;i++)bird.Render(i/30f,1,false);
                Require(!bird.WaitingForLanding&&Vector3.Distance(bird.transform.localPosition,bird.LandingPoint)<.08f,"cleared landing permits gradual return");
                var staggerNav=new CoastalWildlifeNavigation(2);
                var one=world.AddComponent<CoastalBird>();var two=world.AddComponent<CoastalBird>();
                one.Activity=two.Activity=CoastalBirdActivity.Perching;one.Center=two.Center=home;two.Phase=1;
                one.BindNavigation(staggerNav,0);two.BindNavigation(staggerNav,1);
                one.React(1,home,1,1,false);two.React(1,home,1,1,false);
                one.React(8.1f,null,9.1f,1,false);two.React(8.1f,null,9.1f,1,false);
                Require(Mathf.Abs(one.Alarm-two.Alarm)>.02f,"different gull phases stagger recovery");
                float level=one.Alarm;one.React(0,home,9.1f,1,false);Require(one.Alarm==level&&one.TakeoffCount==1,"pause does not retrigger or consume stagger");
                var crabOne=world.AddComponent<CoastalCrab>();var crabTwo=world.AddComponent<CoastalCrab>();
                crabOne.Center=crabTwo.Center=home;crabOne.Tangent=crabTwo.Tangent=Vector3.right;crabTwo.Phase=1;
                crabOne.Navigation=crabTwo.Navigation=staggerNav;crabOne.React(1,home);crabTwo.React(1,home);
                crabOne.React(6,null);crabTwo.React(6,null);Require(Mathf.Abs(crabOne.Alarm-crabTwo.Alarm)>.05f,"different crab phases stagger recovery");
                staggerNav.Clear();nav.Clear();probe.Clear();
                Require(!nav.BodyClear(home,home,.3f,.3f)&&!probe.ClearVolume(home,home,.3f,.3f),"released services reject stale queries");
                Debug.Log("Coastal navigation verification PASSED: "+checks+" swept-volume, detour, dynamic obstacle, player, reservation, loiter, recovery and pause assertions.");
            }
            finally{nav.Clear();probe.Clear();Object.DestroyImmediate(world);}
        }
    }
}
