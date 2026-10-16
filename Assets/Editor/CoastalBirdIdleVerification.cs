using System;
using System.IO;
using System.Linq;
using MagicIsland.Managers;
using MagicIsland.Ocean;
using UnityEngine;
using Object=UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    public static class CoastalBirdIdleVerification
    {
        static int checks;
        static void Require(bool ok,string why){checks++;if(!ok)throw new Exception("Seagull habits: "+why);}
        public static void Verify(GameObject prefab)
        {
            checks=0;var settings=new CoastalBirdHabits();var random=UnityEngine.Random.state;
            Require(settings.IsValid,"default habits valid");
            settings.MaximumWait=3;Require(!settings.IsValid,"inverted interval rejected");settings.MaximumWait=float.NaN;Require(!settings.IsValid,"nonfinite duration rejected");settings.MaximumWait=10;
            var body=prefab.GetComponent<OceanSurface>().Birds[0].GetComponent<MeshFilter>().sharedMesh;
            var vertices=body.vertices;var previous=Vector2.zero;int pecks=0,preens=0,different=0;
            var a=new CoastalBirdIdle();var b=new CoastalBirdIdle();var twin=new CoastalBirdIdle();
            for(int i=0;i<7200;i++)
            {
                a.Advance(1f/60,true,.7f,settings);b.Advance(1f/60,true,2.3f,settings);twin.Advance(1f/60,true,.7f,settings);
                Require(a.Head==twin.Head&&a.Gesture==twin.Gesture,"deterministic independent of gameplay RNG");
                Require(Mathf.Abs(a.Head.x-previous.x)<=5.001f&&Mathf.Abs(a.Head.y-previous.y)<=7.001f,"bounded neck turn");previous=a.Head;
                if(a.Gesture==CoastalBirdGesture.Pecking)pecks++;
                if(a.Gesture==CoastalBirdGesture.Preening)preens++;
                if(a.Gesture!=b.Gesture)different++;
                // Check every AA body vertex at representative poses throughout both actions.
                if(i%12!=0)continue;
                foreach(var vertex in vertices)
                {
                    var p=CoastalBirdIdle.Deform(vertex,a.Head);
                    Require(Mathf.Abs(p.x)<.9f&&Mathf.Abs(p.z)<.9f&&p.y>=-.001f&&p.y<=.6f,"head stays within original reserved perch body");
                    if(vertex.y<.18f)Require((p-vertex).sqrMagnitude<.0000001f,"feet and legs remain planted");
                }
            }
            Require(pecks>100&&preens>100&&different>1000,"both actions and staggered watching occur");
            var head=a.Head;float left=a.LeftWing,right=a.RightWing;var gesture=a.Gesture;
            a.Advance(0,false,.7f,settings);
            Require(head==a.Head&&left==a.LeftWing&&right==a.RightWing&&gesture==a.Gesture,"pause freezes action even when eligibility changes");
            for(int i=0;i<30;i++)a.Advance(1f/60,false,.7f,settings);
            Require(a.Head==Vector2.zero&&a.LeftWing==0&&a.RightWing==0&&a.Gesture==CoastalBirdGesture.Watching,"flight cancels and smoothly neutralizes neck and wings");
            for(int i=0;i<600;i++)a.Advance(1f/30,false,.7f,settings);
            a.Advance(.1f,true,.7f,settings);Require(a.Gesture==CoastalBirdGesture.Watching,"landing starts a new wait, never resumes old action");
            var fast=new CoastalBirdIdle();var slow=new CoastalBirdIdle();
            for(int i=0;i<80;i++)
            {
                for(int j=0;j<15;j++)fast.Advance(1f/60,true,1.2f,settings);
                slow.Advance(.25f,true,1.2f,settings);
                Require(fast.Gesture==slow.Gesture,"low frame rate consumes transition overshoot");
            }
            Require(JsonUtility.ToJson(random)==JsonUtility.ToJson(UnityEngine.Random.state),"no gameplay RNG consumed");
            VerifyRebuild(prefab);
            Debug.Log("Seagull habits rule verification PASSED: "+checks+" checks, both gestures, actual mesh envelope, planted feet, stagger, pause, cancellation and low frame rate.");
        }
        static void VerifyRebuild(GameObject prefab)
        {
            // An unsaved instance exercises the real explicit builder without writing AA assets.
            var root=Object.Instantiate(prefab);
            try
            {
                var surface=root.GetComponent<OceanSurface>();var template=surface.Birds.First(b=>b.Activity==CoastalBirdActivity.Perching);
                template.Habits=new CoastalBirdHabits{MinimumWait=1.5f,MaximumWait=2.7f,PeckSeconds=1.3f,PreenSeconds=4.9f};
                var sites=surface.Birds.Select(b=>b.Center).ToArray();var crabs=surface.Crabs.Select(c=>c.Center).ToArray();
                var shore=surface.Shore.GetComponent<MeshFilter>().sharedMesh;var school=surface.Schools[0];
                OceanBiomeSetup.BuildCoastalWildlife(root,surface,template.GetComponent<Renderer>().sharedMaterial);
                Require(surface.Birds.Select(b=>b.Center).SequenceEqual(sites)&&surface.Crabs.Select(c=>c.Center).SequenceEqual(crabs),"habit change does not alter seeded population or sites");
                Require(surface.Shore.GetComponent<MeshFilter>().sharedMesh==shore&&surface.Schools[0]==school,"coastal rebuild preserves seabed and fish");
                var birds=surface.Birds.Where(b=>b.Activity==CoastalBirdActivity.Perching).ToArray();
                foreach(var bird in birds)Require(bird.Habits.MinimumWait==1.5f&&bird.Habits.MaximumWait==2.7f&&bird.Habits.PeckSeconds==1.3f&&bird.Habits.PreenSeconds==4.9f,"explicit rebuild retains authored habits");
                Require(!ReferenceEquals(birds[0].Habits,template.Habits),"rebuilt habits are deep copies");
                if(birds.Length>1)Require(!ReferenceEquals(birds[0].Habits,birds[1].Habits),"individual birds do not share mutable habits");
            }
            finally{Object.DestroyImmediate(root);}
        }

        public static void VerifyRuntime(GameManager game)
        {
            var ocean=game.Ocean.Surface;var bird=ocean.Birds.First(b=>b.Activity==CoastalBirdActivity.Perching);
            var period=bird.ActivityPeriod;var rest=bird.RestFraction;var material=bird.GetComponent<Renderer>().sharedMaterial;
            var mesh=bird.GetComponent<MeshFilter>().sharedMesh;var meshBytes=mesh.vertices;
            bool peck=false,preen=false;var renderer=bird.GetComponent<Renderer>();var block=new MaterialPropertyBlock();
            try
            {
                bird.ActivityPeriod=100000;bird.RestFraction=.9f;
                for(int frame=0;frame<7200&&!(peck&&preen);frame++)
                {
                    ocean.Tick(1f/60,1,false,game.Weather.Sun);
                    if(bird.Alarm>0||bird.WaitingForLanding||Vector3.Distance(bird.transform.localPosition,bird.LandingPoint)>.025f)continue;
                    bool capturePeck=!peck&&bird.Gesture==CoastalBirdGesture.Pecking&&bird.HeadPose.x>58;
                    bool capturePreen=!preen&&bird.Gesture==CoastalBirdGesture.Preening&&Mathf.Abs(bird.HeadPose.y)>120;
                    if(!capturePeck&&!capturePreen)continue;
                    string label=capturePeck?"SeagullPeck":"SeagullPreen";
                    var root=bird.transform.localPosition;var rotation=bird.transform.localRotation;var wing=bird.LeftWing.localRotation;var pose=bird.HeadPose;
                    renderer.GetPropertyBlock(block);var gpu=block.GetVector("_GullPose");
                    Require(Mathf.Abs(gpu.x-pose.x*Mathf.Deg2Rad)<.00001f,"packed AA body receives neck pose MPB");
                    LightingFlowVerification.Capture(game,label,bird.transform.position+Vector3.up*.23f,.8f);
                    // Fixed-root, same-camera, same-environment control proves shader deformation.
                    block.SetVector("_GullPose",new Vector4(0,0,0,1));renderer.SetPropertyBlock(block);
                    LightingFlowVerification.Capture(game,label+"Control",bird.transform.position+Vector3.up*.23f,.8f);
                    block.SetVector("_GullPose",gpu);renderer.SetPropertyBlock(block);
                    Require(PixelDifference("Logs/"+label+".png","Logs/"+label+"Control.png")>30,"actual GPU head deformation changes pixels at a fixed root");
                    for(int i=0;i<5;i++)ocean.Tick(0,1,false,game.Weather.Sun);
                    Require(bird.HeadPose==pose&&root==bird.transform.localPosition&&rotation==bird.transform.localRotation&&wing==bird.LeftWing.localRotation,"paused packed action freezes head, body and wings");
                    if(capturePeck)peck=true;else preen=true;
                }
                Require(peck&&preen,"actual AA bird performs peck and preen using default intervals");
                var before=bird.TakeoffCount;var player=ocean.transform.TransformPoint(bird.LandingPoint)+Vector3.right*.65f;
                for(int i=0;i<60;i++)ocean.Tick(1f/60,1,false,game.Weather.Sun,player);
                Require(bird.Alarm>.9f&&bird.HeadPose==Vector2.zero&&bird.Gesture==CoastalBirdGesture.Watching,"player interrupts active preen and clears head while taking off");
                Require(bird.TakeoffCount==before+1,"interrupt yields exactly one existing takeoff event");
                Require(renderer.sharedMaterial==material&&bird.GetComponent<MeshFilter>().sharedMesh==mesh&&mesh.vertices.SequenceEqual(meshBytes),"shared material and mesh preserved");
                Debug.Log("Seagull habits packed verification PASSED: real AA peck/preen GPU controls, pause, player interruption, single takeoff and unchanged mesh/material.");
            }
            finally{bird.ActivityPeriod=period;bird.RestFraction=rest;}
        }
        static int PixelDifference(string a,string b)
        {
            var first=new Texture2D(2,2);var second=new Texture2D(2,2);
            try
            {
                first.LoadImage(File.ReadAllBytes(a));second.LoadImage(File.ReadAllBytes(b));
                var x=first.GetPixels32();var y=second.GetPixels32();int count=0;
                for(int i=0;i<x.Length;i++)if(x[i].r!=y[i].r||x[i].g!=y[i].g||x[i].b!=y[i].b)count++;
                return count;
            }
            finally{Object.DestroyImmediate(first);Object.DestroyImmediate(second);}
        }
    }
}
