using System;
using System.IO;
using System.Linq;
using MagicIsland.Ocean;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    [InitializeOnLoad] public static class CoastalWildlifeVerification
    {
        const string VisualKey="MagicIsland.CoastalWildlifeVisual";
        static CoastalWildlifeVerification(){if(SessionState.GetBool(VisualKey,false))EditorApplication.update+=VisualTick;}
        static void Require(bool ok,string why){if(!ok)throw new Exception("Coastal wildlife: "+why);}
        public static void RunVisual()
        {
            Require(Application.persistentDataPath.Contains("MagicIslandValidation"),"visual check requires isolated save directory");
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
            var settings=UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;
            settings.ActivePlayModeDataBuilderIndex=settings.DataBuilders.FindIndex(b=>b is UnityEditor.AddressableAssets.Build.DataBuilders.BuildScriptPackedPlayMode);
            SessionState.SetBool(VisualKey,true);SessionState.SetInt(VisualKey+".Phase",0);
            SessionState.SetFloat(VisualKey+".Start",(float)EditorApplication.timeSinceStartup);
            EditorApplication.update-=VisualTick;EditorApplication.update+=VisualTick;EditorApplication.isPlaying=true;
        }
        static void VisualTick()
        {
            if(!SessionState.GetBool(VisualKey,false))return;
            int phase=SessionState.GetInt(VisualKey+".Phase",0);
            if(phase==3&&!EditorApplication.isPlaying){SessionState.SetBool(VisualKey,false);EditorApplication.Exit(0);return;}
            try
            {
                Require(EditorApplication.timeSinceStartup-SessionState.GetFloat(VisualKey+".Start",0)<120,"visual check timeout");
                var g=MagicIsland.Managers.GameManager.Instance;if(!EditorApplication.isPlaying||g==null)return;
                if(phase==0&&g.State==MagicIsland.Managers.GameFlowState.MainMenu){g.EnterGame();SessionState.SetInt(VisualKey+".Phase",1);}
                else if(phase==1&&g.State==MagicIsland.Managers.GameFlowState.Playing)
                {LightingFlowVerification.SetTime(g,750);g.OpenPause();SessionState.SetInt(VisualKey+".Phase",2);}
                else if(phase==2&&g.UI.HasPause)
                {
                    var ocean=g.Ocean.Surface;var crab=ocean.Crabs[0];
                    var player=ocean.transform.TransformPoint(crab.Center-crab.Tangent*.6f)+Vector3.up*.1f;
                    int moving=0;
                    for(int frame=0;frame<32;frame++)
                    {
                        ocean.Tick(1f/60,1,false,g.Weather.Sun,player);
                        var focus=crab.transform.position+Vector3.up*.1f;
                        LightingFlowVerification.Capture(g,"CoastalCrabWalk"+frame.ToString("00"),focus,1.2f);
                        if(crab.Walking>.1f)moving++;
                        if(frame!=8)continue;
                        Require(crab.Walking>.1f,"GPU comparison captures active steps");
                        var renderer=crab.GetComponent<Renderer>();var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);
                        var motion=block.GetVector("_CrabMotion");block.SetVector("_CrabMotion",new Vector4(motion.x,0,motion.z,1));renderer.SetPropertyBlock(block);
                        LightingFlowVerification.Capture(g,"CoastalCrabRigidControl",focus,1.2f);
                        block.SetVector("_CrabMotion",motion);renderer.SetPropertyBlock(block);
                        var active=ReadPixels("Logs/CoastalCrabWalk08.png");var rigid=ReadPixels("Logs/CoastalCrabRigidControl.png");
                        Require(active.Where((p,i)=>p.r!=rigid[i].r||p.g!=rigid[i].g||p.b!=rigid[i].b).Count()>20,"fixed-root GPU legs change rendered pixels");
                    }
                    Require(moving>12,"clip contains actual walking rather than only the stopped alarm pose");
                    Debug.Log("Coastal wildlife visual verification PASSED: packed AA walking sequence and fixed-root GPU leg control.");
                    SessionState.SetInt(VisualKey+".Phase",3);EditorApplication.isPlaying=false;
                }
            }
            catch(Exception e){Debug.LogException(e);SessionState.SetBool(VisualKey,false);EditorApplication.isPlaying=false;EditorApplication.Exit(1);}
        }
        static Color32[] ReadPixels(string path)
        {
            var image=new Texture2D(2,2);
            try{image.LoadImage(File.ReadAllBytes(path));return image.GetPixels32();}
            finally{Object.DestroyImmediate(image);}
        }
        public static void Prepare()
        {
            var paths=new[]{OceanSetup.MeshFolder+"/Island Shore.asset",OceanBiomeSetup.MeshFolder+"/School 00.asset",OceanBiomeSetup.MaterialPath};
            var before=paths.Select(File.ReadAllBytes).ToArray();
            OceanBiomeSetup.RebuildCoastalWildlife();
            for(int i=0;i<paths.Length;i++)Require(before[i].SequenceEqual(File.ReadAllBytes(paths[i])),"rebake preserves seabed, fish and shared material");
            var prefab=File.ReadAllBytes(OceanSetup.PrefabPath);PrototypeAddressablesSetup.EnsureSetup();AssetDatabase.SaveAssets();
            Require(prefab.SequenceEqual(File.ReadAllBytes(OceanSetup.PrefabPath)),"ordinary Setup preserves authored population and parameters");
            Verify(AssetDatabase.LoadAssetAtPath<GameObject>(OceanSetup.PrefabPath));
            Debug.Log("Coastal wildlife preparation PASSED.");
        }
        public static void Verify(GameObject prefab)
        {
            var instance=Object.Instantiate(prefab);instance.SetActive(false);
            try
            {
                var ocean=instance.GetComponent<OceanSurface>();var plan=ocean.CoastalPopulation.CreatePlan();
                Require(ocean.Birds.Length==plan.Birds.Length&&ocean.Crabs.Length==plan.Crabs.Length,"seed plan matches actual baked AA count");
                Require(ocean.Birds.Select(b=>b.FlapSpeed).Distinct().Count()>=Mathf.Min(3,ocean.Birds.Length)&&ocean.Crabs.Select(c=>c.CrawlPeriod).Distinct().Count()>=Mathf.Min(3,ocean.Crabs.Length),"independent activity rhythms");
                for(int i=0;i<ocean.Birds.Length;i++)Require(Vector2.Distance(new Vector2(ocean.Birds[i].Center.x,ocean.Birds[i].Center.z),new Vector2((float)plan.Birds[i].X,(float)plan.Birds[i].Z))<.0001f,"bird random site preserved");
                for(int i=0;i<ocean.Crabs.Length;i++)Require(Vector2.Distance(new Vector2(ocean.Crabs[i].Center.x,ocean.Crabs[i].Center.z),new Vector2((float)plan.Crabs[i].X,(float)plan.Crabs[i].Z))<.0001f,"crab random site preserved");
                var material=ocean.Crabs[0].GetComponent<Renderer>().sharedMaterial;var original=EditorJsonUtility.ToJson(material);
                bool resting=false,moving=false;
                foreach(var crab in ocean.Crabs)
                {
                    var mesh=crab.GetComponent<MeshFilter>().sharedMesh;var hinges=new System.Collections.Generic.List<Vector4>();mesh.GetUVs(1,hinges);
                    Require(hinges.Count==mesh.vertexCount&&hinges.Any(v=>v.w>=100)&&hinges.Any(v=>v.w==0),"leg hinges and static body share one mesh");
                    var previous=crab.transform.localPosition;
                    for(int step=1;step<=900;step++)
                    {
                        float time=step/30f;crab.React(1f/30,null);crab.Render(time);var p=crab.transform.localPosition;
                        Require(Vector3.Distance(previous,p)<.06f,"calm crab moves continuously");
                        Require(MagicIsland.Interaction.IslandLayout.ContainsLand(p.x,p.z,.4),"complete crab stays on dry sand");
                        resting|=crab.Walking<.001f;moving|=crab.Walking>.2f;previous=p;
                    }
                    var pose=crab.transform.localPosition;float stride=crab.Stride;crab.React(0,crab.Center);crab.Render(30);
                    Require(pose==crab.transform.localPosition&&stride==crab.Stride,"pause freezes root and leg phase");
                    var block=new MaterialPropertyBlock();crab.GetComponent<Renderer>().GetPropertyBlock(block);Require(block.GetVector("_CrabMotion").w==1,"leg animation reaches per-renderer block");
                }
                Require(resting&&moving,"crabs alternate rest and crawling");
                foreach(var bird in ocean.Birds)
                {
                    var previous=bird.transform.localPosition;
                    for(int step=1;step<=900;step++)
                    {
                        bird.React(1f/30,null,step/30f,1,false);bird.Render(step/30f,1,false);
                        Require(Vector3.Distance(previous,bird.transform.localPosition)<.18f,"calm flight and landing remain continuous");previous=bird.transform.localPosition;
                    }
                    var rotation=bird.transform.localRotation;var wing=bird.LeftWing.localRotation;bird.React(0,null,30,1,false);bird.Render(30,1,false);
                    Require(previous==bird.transform.localPosition&&rotation==bird.transform.localRotation&&wing==bird.LeftWing.localRotation,"pause freezes bird attitude and wings");
                }
                var fish=ocean.Schools.First(s=>s.Species==ShallowFishSpecies.SilverMinnow);ocean.ConfigureFishHabitat(fish);fish.Render(0);
                var threat=fish.Steering.States[0].Position+Vector3.up*.8f;
                for(int i=1;i<=150;i++){fish.React(1f/30,null,threat);fish.Render(i/30f);}
                Require(fish.Alarm>.99f,"nearby standing player maintains fish alarm");
                float alarm=fish.Alarm;fish.React(0,null,null);Require(fish.Alarm==alarm,"pause cannot clear player threat");
                for(int i=151;i<=360;i++){fish.React(1f/30,null,null);fish.Render(i/30f);}
                Require(fish.Alarm==0,"player leaving allows fish to regroup");
                Require(original==EditorJsonUtility.ToJson(material),"runtime animation preserves shared AA material");
                Debug.Log("Coastal wildlife verification PASSED: sparse seeded population, independent rest/flight/crawl, articulated legs, continuous motion, player fish threat/recovery and pause.");
            }
            finally{Object.DestroyImmediate(instance);}
        }
    }
}
