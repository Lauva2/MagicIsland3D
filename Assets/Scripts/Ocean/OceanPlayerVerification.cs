#if MAGIC_ISLAND_OCEAN_VERIFICATION
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MagicIsland.CameraSystem;
using MagicIsland.Interaction;
using MagicIsland.Managers;
using Unity.Profiling;
using UnityEngine;

namespace MagicIsland.Ocean
{
    // Included only by the explicit isolated verification build, never a normal player build.
    [DefaultExecutionOrder(200)] public sealed class OceanPlayerVerification : MonoBehaviour
    {
        [Serializable] public sealed class Sample
        {
            public string name;
            public int frames;
            public double fps,frameMeanMs,frameP95Ms,oceanMeanMs,oceanP95Ms,gpuMeanMs;
            public double visibleSchools,submittedSchools;
        }
        [Serializable] public sealed class Report
        {
            public string unity,cpu,gpu,graphics,persistentPath;
            public int width,height,schools,fish;
            public bool developmentBuild;
            public string notes="Hidden uncapped development player with explicit 1280x720 offscreen gameplay-camera rendering every frame; excludes overlay HUD and window presentation. FPS is scene-render throughput, not foreground game FPS. Full submission control versus frustum filtering. GPU zero means unavailable. Not a target-device guarantee.";
            public List<Sample> samples=new List<Sample>();
        }
        string output;
        bool finished;
        float started;
        Camera benchmarkCamera;
        RenderTexture benchmarkTarget;
        int renderedFrames;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Launch()
        {
            if(Environment.GetCommandLineArgs().Contains("-ocean-verify"))new GameObject("Ocean Player Verification").AddComponent<OceanPlayerVerification>();
        }
        void Awake(){DontDestroyOnLoad(gameObject);started=Time.realtimeSinceStartup;Application.runInBackground=true;Application.logMessageReceived+=OnLog;}
        void OnDestroy(){Application.logMessageReceived-=OnLog;if(benchmarkTarget!=null){benchmarkTarget.Release();Destroy(benchmarkTarget);}}
        void LateUpdate(){if(benchmarkCamera!=null&&!finished){benchmarkCamera.Render();renderedFrames++;}}
        void OnLog(string message,string stack,LogType type){if(type==LogType.Exception&&!finished)Fail(message+"\n"+stack);}
        void Update(){if(!finished&&Time.realtimeSinceStartup-started>240)Fail("Player verification timed out");}
        void Fail(string error){finished=true;if(output!=null)File.WriteAllText(Path.Combine(output,"FAILED.txt"),error);Debug.LogError(error);Application.Quit(1);}
        IEnumerator Start()
        {
            output=Path.GetFullPath(Path.Combine(Application.dataPath,"..","Results"));Directory.CreateDirectory(output);
            File.Delete(Path.Combine(output,"PASSED.txt"));File.Delete(Path.Combine(output,"FAILED.txt"));
            if(!Application.persistentDataPath.Contains("MagicIslandValidation"))throw new Exception("Verification build must use an isolated company/product");
            QualitySettings.vSyncCount=0;Application.targetFrameRate=-1;
            while(GameManager.Instance==null||GameManager.Instance.State!=GameFlowState.MainMenu)yield return null;
            var game=GameManager.Instance;game.EnterGame();
            while(game.State!=GameFlowState.Playing){if(game.State==GameFlowState.Failed)throw new Exception("Player game initialization failed");yield return null;}
            var camera=game.Players.Player.ViewCamera;
            benchmarkCamera=camera;benchmarkTarget=new RenderTexture(1280,720,24);camera.targetTexture=benchmarkTarget;camera.enabled=false;
            var report=new Report{unity=Application.unityVersion,cpu=SystemInfo.processorType,gpu=SystemInfo.graphicsDeviceName,
                graphics=SystemInfo.graphicsDeviceType.ToString(),persistentPath=Application.persistentDataPath,width=Screen.width,height=Screen.height,
                schools=game.Ocean.Surface.Schools.Length,fish=game.Ocean.Surface.Schools.Sum(s=>s.Members),developmentBuild=Debug.isDebugBuild};
            using(var simulation=ProfilerRecorder.StartNew(ProfilerCategory.Scripts,"MagicIsland.Ocean.Simulation",1))
            using(var presentation=ProfilerRecorder.StartNew(ProfilerCategory.Scripts,"MagicIsland.Ocean.Presentation",1))
            {
                string[] names={"DayPierFull","DayPierVisible","RainPierFull","RainPierVisible","NightPierFull","NightPierVisible","DayWestVisible","DayCenterVisible"};
                var timings=new FrameTiming[1];
                foreach(var name in names)
                {
                    bool rain=name.StartsWith("Rain"),night=name.StartsWith("Night");
                    game.Weather.State.NextDay(game.Weather.State.Day+1,rain?1:0);
                    game.Time.Clock.Restore(night?1320:720);game.Weather.Tick(night?1320:720,false);
                    var point=name.Contains("West")?new Vector3(-14.8f,.1f,0):name.Contains("Center")?new Vector3(0,.1f,-4.6f):new Vector3(17.2f,.12f,(float)IslandLayout.PierZ);
                    game.Players.RestorePosition(point);camera.GetComponent<CameraController>().SnapToTarget();
                    game.Ocean.Surface.CullFishPresentation=!name.EndsWith("Full");
                    for(int frame=0;frame<120;frame++)yield return null;
                    if(!simulation.Valid||!presentation.Valid)throw new Exception("Ocean CPU profiler markers unavailable");
                    var frameMs=new double[8192];var oceanMs=new double[8192];int count=0,gpuCount=0;double gpuTotal=0,visible=0,submitted=0;
                    float start=Time.realtimeSinceStartup;int renderedBefore=renderedFrames;
                    while((count<240||Time.realtimeSinceStartup-start<5)&&count<frameMs.Length)
                    {
                        yield return null;
                        frameMs[count]=Time.unscaledDeltaTime*1000.0;
                        oceanMs[count]=(simulation.LastValue+presentation.LastValue)/1000000.0;
                        visible+=game.Ocean.Surface.LastVisibleSchools;submitted+=game.Ocean.Surface.LastFishSubmissions;
                        FrameTimingManager.CaptureFrameTimings();
                        if(FrameTimingManager.GetLatestTimings(1,timings)>0&&timings[0].gpuFrameTime>0){gpuTotal+=timings[0].gpuFrameTime;gpuCount++;}
                        count++;
                    }
                    if(renderedFrames-renderedBefore<count-1)throw new Exception("A sampled frame did not render the gameplay camera");
                    if(name.EndsWith("Full")&&submitted/count<report.schools-.5)throw new Exception("Full submission control did not upload all schools");
                    if(!name.Contains("Center")&&visible/count<1)throw new Exception("No visible fish schools in coast sample");
                    var frames=frameMs.Take(count).OrderBy(v=>v).ToArray();var ocean=oceanMs.Take(count).OrderBy(v=>v).ToArray();
                    var sample=new Sample{name=name,frames=count,frameMeanMs=frames.Average(),frameP95Ms=frames[(int)(count*.95)],fps=1000/frames.Average(),
                        oceanMeanMs=ocean.Average(),oceanP95Ms=ocean[(int)(count*.95)],gpuMeanMs=gpuCount>0?gpuTotal/gpuCount:0,
                        visibleSchools=visible/count,submittedSchools=submitted/count};report.samples.Add(sample);
                    File.WriteAllText(Path.Combine(output,"OceanPlayerReport.json"),JsonUtility.ToJson(report,true));
                    Debug.Log("Ocean player sample "+name+": ocean="+sample.oceanMeanMs.ToString("F3")+" ms; FPS="+sample.fps.ToString("F1")+"; uploads="+sample.submittedSchools.ToString("F1"));
                    if(!name.EndsWith("Full"))Capture(camera,Path.Combine(output,name+".png"));
                }
            }
            game.Ocean.Surface.CullFishPresentation=true;
            File.WriteAllText(Path.Combine(output,"PASSED.txt"),"Standalone packed-AA ocean measurement complete; normal gameplay camera at configured pitch and size.");
            finished=true;Debug.Log("Ocean player verification PASSED.");Application.Quit(0);
        }
        static void Capture(Camera camera,string path)
        {
            var rt=new RenderTexture(1280,720,24);var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);
            var previous=camera.targetTexture;var active=RenderTexture.active;
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());}
            finally{camera.targetTexture=previous;RenderTexture.active=active;rt.Release();Destroy(rt);Destroy(texture);}
        }
    }
}
#endif
