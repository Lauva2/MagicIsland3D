using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using MagicIsland.Managers;
using MagicIsland.Ocean;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    public static class CoastalCrabIdleVerification
    {
        static int checks;
        static void Require(bool ok,string why){checks++;if(!ok)throw new Exception("Crab habits: "+why);}
        public static void Prepare()
        {
            var paths=new[]{OceanSetup.PrefabPath,OceanSetup.MeshFolder+"/Island Shore.asset",OceanBiomeSetup.MeshFolder+"/School 00.asset",OceanBiomeSetup.MaterialPath};
            var bytes=paths.Select(File.ReadAllBytes).ToArray();
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(OceanBiomeSetup.MeshFolder+"/Shore Crab.asset");
            var vertices=mesh.vertices;var normals=mesh.normals;var colors=mesh.colors;var triangles=mesh.triangles;var bounds=mesh.bounds;
            var hinges=new List<Vector4>();mesh.GetUVs(1,hinges);var gait=new List<Vector4>();mesh.GetUVs(2,gait);
            OceanBiomeSetup.PrepareCrabHabits();
            Require(vertices.SequenceEqual(mesh.vertices)&&normals.SequenceEqual(mesh.normals)&&colors.SequenceEqual(mesh.colors)&&triangles.SequenceEqual(mesh.triangles)&&bounds==mesh.bounds,"preparation only annotates pivots, preserving authored geometry, normals, colour and bounds");
            var after=new List<Vector4>();mesh.GetUVs(1,after);var gaitAfter=new List<Vector4>();mesh.GetUVs(2,gaitAfter);
            for(int i=0;i<after.Count;i++)if(after[i].w<110)Require(after[i]==hinges[i],"existing body and leg hinges preserved");
            Require(gait.SequenceEqual(gaitAfter),"leg phase channel preserved");
            string crabPath=AssetDatabase.GetAssetPath(mesh);var tagged=File.ReadAllBytes(crabPath);OceanBiomeSetup.PrepareCrabHabits();
            Require(tagged.SequenceEqual(File.ReadAllBytes(crabPath)),"explicit preparation idempotent");
            PrototypeAddressablesSetup.EnsureSetup();AssetDatabase.SaveAssets();
            Require(tagged.SequenceEqual(File.ReadAllBytes(crabPath)),"ordinary setup preserves mesh metadata");
            for(int i=0;i<paths.Length;i++)Require(bytes[i].SequenceEqual(File.ReadAllBytes(paths[i])),"preparation preserves ecology/prefab "+paths[i]);
            Verify(AssetDatabase.LoadAssetAtPath<GameObject>(OceanSetup.PrefabPath));
            Debug.Log("Crab habits preparation PASSED: annotations only, artist data preserved, idempotent, no population or terrain rebuild.");
        }
        public static void Verify(GameObject prefab)
        {
            checks=0;var settings=new CoastalCrabHabits();Require(settings.IsValid,"default settings valid");
            settings.MaximumWait=.1f;Require(!settings.IsValid,"inverted interval rejected");settings.MaximumWait=float.NaN;Require(!settings.IsValid,"nonfinite timing rejected");settings.MaximumWait=.9f;
            var mesh=prefab.GetComponent<OceanSurface>().Crabs[0].GetComponent<MeshFilter>().sharedMesh;
            var vertices=mesh.vertices;var hinges=new List<Vector4>();mesh.GetUVs(1,hinges);var shapes=new List<Vector4>();mesh.GetUVs(2,shapes);
            Require(hinges.Count==vertices.Length&&hinges.Any(h=>h.w==110)&&hinges.Any(h=>h.w==111),"actual AA mesh has independent left/right claws");
            var random=UnityEngine.Random.state;var a=new CoastalCrabIdle();var b=new CoastalCrabIdle();var twin=new CoastalCrabIdle();
            int feeding=0,watching=0,different=0;var previous=Vector3.zero;
            for(int frame=0;frame<3600;frame++)
            {
                bool settled=frame%420<300;
                a.Advance(1f/60,settled,.7f,settings);b.Advance(1f/60,settled,2.3f,settings);twin.Advance(1f/60,settled,.7f,settings);
                Require(a.Pose==twin.Pose&&a.Gesture==twin.Gesture,"reproducible without gameplay random");
                Require(Mathf.Abs(a.Pose.x-previous.x)<=3.001f&&Mathf.Abs(a.Pose.y-previous.y)<=3.001f,"claws move at bounded speed");previous=a.Pose;
                if(a.Gesture==CoastalCrabGesture.Foraging)feeding++;if(a.Gesture==CoastalCrabGesture.Watching)watching++;if(a.Gesture!=b.Gesture)different++;
                if(frame%8!=0)continue;
                for(int i=0;i<vertices.Length;i++)
                {
                    var p=CoastalCrabIdle.Deform(vertices[i],hinges[i],new Vector4(frame*.1f,0,.7f,1),a.Pose,shapes[i].x);
                    Require(new Vector2(p.x,p.z).magnitude<.43f&&p.y>=-.001f&&p.y<.30f,"complete animated crab fits original dry-sand query volume");
                    if(hinges[i].w>=100&&hinges[i].w<104)Require((p-vertices[i]).sqrMagnitude<.0000001f,"stationary legs and feet remain planted");
                }
            }
            Require(feeding>100&&watching>100&&different>100,"foraging, watching and staggered rhythms present");
            var pose=a.Pose;var gesture=a.Gesture;a.Advance(0,false,.7f,settings);Require(a.Pose==pose&&a.Gesture==gesture,"pause freezes pose and action");
            for(int i=0;i<20;i++)a.Advance(1f/60,false,.7f,settings);
            Require(a.Pose==Vector3.zero&&a.Gesture==CoastalCrabGesture.Resting,"motion/threat smoothly cancels claws and lean");
            a.Advance(.05f,true,.7f,settings);Require(a.Gesture==CoastalCrabGesture.Resting,"next rest waits rather than resuming old gesture");
            var fast=new CoastalCrabIdle();var slow=new CoastalCrabIdle();
            for(int i=0;i<120;i++)
            {
                bool settled=i%28<20;for(int j=0;j<15;j++)fast.Advance(1f/60,settled,1.2f,settings);
                slow.Advance(.25f,settled,1.2f,settings);Require(fast.Gesture==slow.Gesture,"low frame rate consumes action boundaries");
            }
            Require(JsonUtility.ToJson(random)==JsonUtility.ToJson(UnityEngine.Random.state),"gameplay random unchanged");
            VerifyArtistSafety(mesh);VerifyRebuild(prefab);
            Debug.Log("Crab habits rule verification PASSED: "+checks+" checks, actual whole-body volume, planted feet, stagger, pause, cancellation, low FPS and artist safety.");
        }
        static void VerifyArtistSafety(Mesh source)
        {
            var mesh=Object.Instantiate(source);
            try
            {
                var uv=new List<Vector4>();mesh.GetUVs(1,uv);for(int i=0;i<uv.Count;i++)if(uv[i].w>=110)uv[i]=Vector4.zero;mesh.SetUVs(1,uv);
                var vertices=mesh.vertices;vertices[0]+=Vector3.right*.001f;mesh.vertices=vertices;
                bool rejected=false;try{OceanBiomeSetup.TagCrabClaws(mesh);}catch(Exception e){rejected=e.Message.Contains("Edited Shore Crab");}
                var after=new List<Vector4>();mesh.GetUVs(1,after);
                Require(rejected&&vertices.SequenceEqual(mesh.vertices)&&after.SequenceEqual(uv),"edited untagged artist geometry rejected without mutation");
            }
            finally{Object.DestroyImmediate(mesh);}
        }
        static void VerifyRebuild(GameObject prefab)
        {
            var root=Object.Instantiate(prefab);
            try
            {
                var ocean=root.GetComponent<OceanSurface>();var old=ocean.Crabs[0];
                old.Habits=new CoastalCrabHabits{MinimumWait=.5f,MaximumWait=1.1f,ForageSeconds=2.3f,WatchSeconds=1.4f};var habits=old.Habits;
                var sites=ocean.Crabs.Select(c=>c.Center).ToArray();var fish=ocean.Schools[0];var shore=ocean.Shore.GetComponent<MeshFilter>().sharedMesh;
                OceanBiomeSetup.BuildCoastalWildlife(root,ocean,old.GetComponent<Renderer>().sharedMaterial);
                Require(sites.SequenceEqual(ocean.Crabs.Select(c=>c.Center))&&fish==ocean.Schools[0]&&shore==ocean.Shore.GetComponent<MeshFilter>().sharedMesh,"habit rebake preserves distribution, fish and terrain");
                foreach(var crab in ocean.Crabs)Require(crab.Habits.MinimumWait==.5f&&crab.Habits.MaximumWait==1.1f&&crab.Habits.ForageSeconds==2.3f&&crab.Habits.WatchSeconds==1.4f&&!ReferenceEquals(crab.Habits,habits),"explicit rebake deep-copies authored habits");
            }
            finally{Object.DestroyImmediate(root);}
        }
        public static void VerifyRuntime(GameManager game)
        {
            var ocean=game.Ocean.Surface;var crab=ocean.Crabs[0];var renderer=crab.GetComponent<Renderer>();var mesh=crab.GetComponent<MeshFilter>().sharedMesh;var material=renderer.sharedMaterial;
            bool fed=false,watched=false,moved=false;Vector3 last=crab.transform.localPosition;var block=new MaterialPropertyBlock();
            for(int frame=0;frame<7200&&!(fed&&watched&&moved);frame++)
            {
                ocean.Tick(1f/60,1,false,game.Weather.Sun);
                if(crab.Walking>.1f&&Vector3.Distance(last,crab.transform.localPosition)>.0001f)moved=true;
                last=crab.transform.localPosition;
                bool forage=!fed&&crab.Gesture==CoastalCrabGesture.Foraging&&Mathf.Max(crab.ClawPose.x,crab.ClawPose.y)>30;
                bool watch=!watched&&crab.Gesture==CoastalCrabGesture.Watching&&Mathf.Min(crab.ClawPose.x,crab.ClawPose.y)<-18;
                if(!forage&&!watch)continue;
                string name=forage?"CrabForage":"CrabWatch";var pose=crab.ClawPose;var root=crab.transform.localPosition;var rotation=crab.transform.localRotation;float stride=crab.Stride;
                renderer.GetPropertyBlock(block);var gpu=block.GetVector("_CrabPose");Require(Mathf.Abs(gpu.x-pose.x*Mathf.Deg2Rad)<.00001f,"actual AA MPB receives claw pose");
                var focus=crab.transform.position+Vector3.up*.12f;LightingFlowVerification.Capture(game,name,focus,.65f);
                block.SetVector("_CrabPose",new Vector4(0,0,0,1));renderer.SetPropertyBlock(block);LightingFlowVerification.Capture(game,name+"Control",focus,.65f);
                block.SetVector("_CrabPose",gpu);renderer.SetPropertyBlock(block);
                Require(PixelDifference("Logs/"+name+".png","Logs/"+name+"Control.png")>30,"fixed-root GPU comparison proves articulated claws/body");
                for(int i=0;i<5;i++)ocean.Tick(0,1,false,game.Weather.Sun);
                Require(crab.ClawPose==pose&&root==crab.transform.localPosition&&rotation==crab.transform.localRotation&&stride==crab.Stride,"packed pause freezes claws, body and legs");
                // Interrupt each actual active gesture, retaining the existing safe escape.
                var player=ocean.transform.TransformPoint(crab.Center-crab.Tangent*.6f)+Vector3.up*.1f;
                for(int i=0;i<30;i++)ocean.Tick(1f/60,1,false,game.Weather.Sun,player);
                Require(crab.Alarm>.99f&&crab.ClawPose==Vector3.zero&&crab.Gesture==CoastalCrabGesture.Resting,"player interrupts both active gestures and neutralizes claws");
                Require(MagicIsland.Interaction.IslandLayout.ContainsLand(crab.transform.localPosition.x,crab.transform.localPosition.z,.4f),"interruption retains dry-sand navigation");
                if(forage)fed=true;else watched=true;
            }
            Require(fed&&watched&&moved,"actual AA crab performs both gestures then returns to sideways walking: forage="+fed+", watch="+watched+", moved="+moved+", waiting="+crab.WaitingForPath+", alarm="+crab.Alarm+", pose="+crab.ClawPose+", home="+crab.Center+", position="+crab.transform.localPosition);
            Require(renderer.sharedMaterial==material&&crab.GetComponent<MeshFilter>().sharedMesh==mesh,"runtime uses original shared mesh/material");
            Debug.Log("Crab habits packed verification PASSED: real AA forage/watch, GPU control, player interruptions, planted pauses, safe lateral motion and shared resources.");
        }
        static int PixelDifference(string a,string b)
        {
            var first=new Texture2D(2,2);var second=new Texture2D(2,2);
            try
            {
                first.LoadImage(File.ReadAllBytes(a));second.LoadImage(File.ReadAllBytes(b));var x=first.GetPixels32();var y=second.GetPixels32();int count=0;
                for(int i=0;i<x.Length;i++)if(x[i].r!=y[i].r||x[i].g!=y[i].g||x[i].b!=y[i].b)count++;return count;
            }
            finally{Object.DestroyImmediate(first);Object.DestroyImmediate(second);}
        }
    }
}
