using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using MagicIsland.Ocean;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    public static class OceanFishAcceptanceVerification
    {
        [MenuItem("Magic Island/Verify Fish Presentation And CPU")]
        public static void Run()=>Verify(AssetDatabase.LoadAssetAtPath<GameObject>(OceanSetup.PrefabPath));
        static void Require(bool condition,string message){if(!condition)throw new Exception("Fish acceptance: "+message);}
        public static void Verify(GameObject prefab)
        {
            var root=Object.Instantiate(prefab);root.SetActive(false);
            try
            {
                var surface=root.GetComponent<OceanSurface>();
                foreach(var school in surface.Schools)
                {
                    surface.ConfigureFishHabitat(school);school.ResetSteering();school.Render(0);
                    Require(school.Steering.Spines!=null,"Actual AA mesh creates distance-constrained spines");
                    var mesh=school.GetComponent<MeshFilter>().sharedMesh;var vertices=mesh.vertices;
                    var pivots=new System.Collections.Generic.List<Vector4>();var shapes=new System.Collections.Generic.List<Vector4>();mesh.GetUVs(1,pivots);mesh.GetUVs(2,shapes);
                    // Observe the actual interpolated pose, including frames between 30 Hz steps.
                    for(int frame=1;frame<=480;frame++)
                    {
                        school.Render(frame/120f);
                        for(int i=0;i<school.Members;i++)
                        {
                            var pose=school.Steering.RenderState(i);var body=school.Steering.BodyBounds(i);
                            var spine=school.Steering.Spines[i];float rest=(spine.Anatomy.Head-spine.Anatomy.Tail)/(OceanFishSpine.Nodes-1);
                            for(int j=1;j<OceanFishSpine.Nodes;j++)Require(Mathf.Abs(Vector2.Distance(spine.Node(j),spine.Node(j-1))-rest)<.00002f,"Actual rendered spine does not stretch");
                            Require(school.Steering.Navigation.CanMove(pose.Position,pose.Position,body),"Interpolated body remains clear of reef, floor, water and home boundary");
                            for(int j=0;j<i;j++)Require(Vector3.Distance(pose.Position,school.Steering.RenderState(j).Position)>=body.Radius+school.Steering.BodyBounds(j).Radius+.02f,"Interpolated bodies remain separated");
                        }
                        if(frame%120==0)for(int v=0;v<vertices.Length;v++)
                        {
                            int i=Mathf.RoundToInt((pivots[v].w-1)/2.399963f);var pose=school.Steering.RenderState(i);var body=school.Steering.BodyBounds(i);
                            var p=school.DeformedVertex(vertices[v],pivots[v],shapes[v])-pose.Position;
                            Require(new Vector2(p.x,p.z).magnitude<=body.Radius+.0001f&&p.y>=body.Bottom-.0001f&&p.y<=body.Top+.0001f,"Every distance-deformed mesh vertex fits original swept body envelope");
                        }
                    }
                    var block=new MaterialPropertyBlock();school.GetComponent<Renderer>().GetPropertyBlock(block);
                    var poses=block.GetVectorArray("_FishPoses");var attitudes=block.GetVectorArray("_FishAttitudes");
                    var spinePoses=block.GetVectorArray("_FishSpines");Require(block.GetFloat("_FishBodyEnabled")==1,"Shader enables constrained body path");
                    Require(spinePoses.SequenceEqual(school.Steering.SpinePoses),"Shader gets actual constrained nodes");
                    for(int i=0;i<school.Members;i++)
                    {
                        var state=school.Steering.RenderState(i);var position=state.Position-school.transform.localPosition;
                        Require(Vector3.Distance(position,(Vector3)poses[i])<.00001f&&Mathf.Abs(poses[i].w-state.Yaw)<.00001f&&Mathf.Abs(attitudes[i].x-state.Pitch)<.00001f,"Shader arrays receive interpolated position, yaw and pitch");
                    }
                    float time=school.MotionTime;school.React(0,null);school.Render(time);school.GetComponent<Renderer>().GetPropertyBlock(block);
                    Require(poses.SequenceEqual(block.GetVectorArray("_FishPoses"))&&attitudes.SequenceEqual(block.GetVectorArray("_FishAttitudes"))&&spinePoses.SequenceEqual(block.GetVectorArray("_FishSpines")),"Pause freezes shader poses, body links and fin phase");
                }
                var samples=new double[240];float clock=4;
                Action<float> tick=time=>{foreach(var school in surface.Schools){school.React(1f/60,null);school.Render(time);}};
                for(int frame=0;frame<120;frame++){clock+=1f/60;tick(clock);}
                int collections=GC.CollectionCount(0);long allocated=GC.GetAllocatedBytesForCurrentThread();
                for(int frame=0;frame<samples.Length;frame++)
                {
                    clock+=1f/60;long start=Stopwatch.GetTimestamp();tick(clock);
                    samples[frame]=(Stopwatch.GetTimestamp()-start)*1000.0/Stopwatch.Frequency;
                }
                allocated=GC.GetAllocatedBytesForCurrentThread()-allocated;collections=GC.CollectionCount(0)-collections;
                double mean=samples.Average();Array.Sort(samples);double p95=samples[(int)(samples.Length*.95)];
                string report=string.Format(CultureInfo.InvariantCulture,"PASS: 120 Hz interpolated body clearance, separation, shader pose/pitch upload and pause.\nCPU observation: {0} schools / {1} fish, warmed 60 Hz simulation + renderer pose submission; mean {2:F3} ms, p95 {3:F3} ms, managed allocation {4} bytes/frame, Gen0 collections {5}. Editor batch observations are not player GPU/FPS measurements.\n",surface.Schools.Length,surface.Schools.Sum(s=>s.Members),mean,p95,allocated/samples.Length,collections);
                Directory.CreateDirectory("Logs");File.WriteAllText("Logs/OceanFishAcceptanceVerification.txt",report);UnityEngine.Debug.Log(report);
            }
            finally{Object.DestroyImmediate(root);}
        }
    }
}
