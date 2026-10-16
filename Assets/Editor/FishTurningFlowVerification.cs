using System;
using System.IO;
using System.Linq;
using MagicIsland.Managers;
using MagicIsland.Ocean;
using MagicIsland.UI.Views;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
namespace MagicIsland.EditorTools
{
    [InitializeOnLoad] public static class FishTurningFlowVerification
    {
        const string Key="MagicIsland.FishTurningVerification";
        static FishTurningFlowVerification(){if(SessionState.GetBool(Key,false))EditorApplication.update+=Tick;}
        static void Require(bool b,string why){if(!b)throw new InvalidOperationException(why);}
        public static void Run()
        {
            PrototypeAddressablesSetup.EnsureSetup();EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");PrototypeAddressablesSetup.BuildContent();
            var settings=AddressableAssetSettingsDefaultObject.Settings;settings.ActivePlayModeDataBuilderIndex=settings.DataBuilders.FindIndex(b=>b is BuildScriptPackedPlayMode);
            SessionState.SetBool(Key,true);SessionState.SetInt(Key+".Phase",0);SessionState.SetFloat(Key+".Start",(float)EditorApplication.timeSinceStartup);
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;EditorApplication.isPlaying=true;
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;int p=SessionState.GetInt(Key+".Phase",0);
            if(p==4&&!EditorApplication.isPlaying){SessionState.SetBool(Key,false);EditorApplication.Exit(0);return;}
            try
            {
                Require(EditorApplication.timeSinceStartup-SessionState.GetFloat(Key+".Start",0)<180,"Fish turning preview timeout.");
                if(!EditorApplication.isPlaying)return;var g=GameManager.Instance;if(g==null)return;Require(g.State!=GameFlowState.Failed,"Packed ocean startup.");
                if(p==0&&g.State==GameFlowState.MainMenu)
                {
                    string directory=Path.GetFullPath("Logs/FishTurning/Saves/"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
                    g.Saves.Initialize(g.Configuration.Config,g.Configuration.Items,g.Configuration.Crops,directory,g.Configuration.Tasks,g.Configuration.Orders,g.Configuration.Seasons,g.Configuration.Gathering,g.Configuration.ToolUpgrades,g.Configuration.Storage,g.Configuration.NPCs,g.Configuration.Relationships,g.Configuration.Stories,g.Configuration.NPCSchedules,g.Configuration.Fishing,g.Configuration.Crafting,g.Configuration.Animals);
                    g.Settings.Initialize(directory+"/Settings");g.EnterGame();SessionState.SetInt(Key+".Phase",1);
                }
                else if(p==1&&g.State==GameFlowState.Playing)
                {
                    g.Time.Clock.Restore(750);g.Weather.Tick(750,false);
                    Require(g.Ocean.OwnsOcean&&g.Ocean.Surface.FishAnimation.IsValid&&Mathf.Abs(g.Ocean.Surface.FishAnimation.TurnAcceleration-540)<.001f,"AA carries authored turning parameters.");
                    CapturePresentation(g);g.ReturnToMainMenu();SessionState.SetInt(Key+".Phase",2);
                }
                else if(p==2&&Object.FindObjectOfType<PauseView>()!=null){Object.FindObjectOfType<PauseView>().ReturnMenuButton.onClick.Invoke();SessionState.SetInt(Key+".Phase",3);}
                else if(p==3&&g.State==GameFlowState.MainMenu)
                {
                    Require(!g.Ocean.OwnsOcean&&g.Ocean.Surface==null,"Preview releases AA ocean and turn momentum.");
                    File.WriteAllText("Logs/FishTurning/Verification.txt","PASS: real Packed AA turn parameters; bounded angular acceleration and velocity through controlled target reversal; exact paused pose/image; smooth constrained-body sequence; GPU reef grass movement and fixed-clock image; original shared resources and menu cleanup.\n");
                    Debug.Log("Fish turning packed verification PASSED.");SessionState.SetInt(Key+".Phase",4);EditorApplication.isPlaying=false;
                }
            }
            catch(Exception e){Debug.LogException(e);SessionState.SetBool(Key,false);EditorApplication.isPlaying=false;EditorApplication.Exit(1);}
        }
        static void CapturePresentation(GameManager game)
        {
            Directory.CreateDirectory("Logs/FishTurning");var ocean=game.Ocean.Surface;
            // An inspection camera avoids altering the owner's visibility submission while recording.
            // The full OceanFlow verifies the actual gameplay-camera callback separately.
            var cameraObject=new GameObject("Fish turning inspection camera",typeof(Camera));var camera=cameraObject.GetComponent<Camera>();camera.CopyFrom(game.Players.Player.ViewCamera);camera.enabled=false;
            camera.transform.SetPositionAndRotation(game.Players.Player.ViewCamera.transform.position,game.Players.Player.ViewCamera.transform.rotation);
            var position=camera.transform.position;float size=camera.orthographicSize;int mask=camera.cullingMask;var background=camera.backgroundColor;
            var canvases=Object.FindObjectsOfType<Canvas>();var visible=canvases.Select(c=>c.enabled).ToArray();game.Cameras.SetOcclusionEnabled(false);
            var patch=ocean.GetComponentsInChildren<OceanHabitatPatch>().OrderByDescending(h=>h.Grasses).First();var plant=patch.GetComponentInChildren<MeshRenderer>();int plantLayer=plant.gameObject.layer;
            var original=new MaterialPropertyBlock();plant.GetPropertyBlock(original);
            var school=ocean.Schools.First(s=>s.Species==ShallowFishSpecies.Clownfish);var fish=school.GetComponent<Renderer>();int fishLayer=fish.gameObject.layer;
            var mesh=school.GetComponent<MeshFilter>().sharedMesh;var material=fish.sharedMaterial;string prefabBefore=File.ReadAllText(OceanSetup.PrefabPath);
            try
            {
                foreach(var canvas in canvases)canvas.enabled=false;camera.cullingMask=1<<31;camera.backgroundColor=Color.black;
                plant.gameObject.layer=31;camera.orthographicSize=3.8f;camera.transform.position=plant.bounds.center-camera.transform.forward*22;
                var block=new MaterialPropertyBlock();plant.GetPropertyBlock(block);block.SetFloat("_OceanTime",0);plant.SetPropertyBlock(block);var a=Capture(camera,"ReefGrassA");
                block.SetFloat("_OceanTime",2.6f);plant.SetPropertyBlock(block);var b=Capture(camera,"ReefGrassB");
                Require(a.Zip(b,(x,y)=>Math.Abs(x.r-y.r)+Math.Abs(x.g-y.g)+Math.Abs(x.b-y.b)>8?1:0).Sum()>30,"Real GPU grass weights deform the reef at a fixed root/camera/light.");
                Require(b.SequenceEqual(Capture(camera,"ReefGrassPause")),"Paused reef wind produces the same image.");plant.SetPropertyBlock(original);plant.gameObject.layer=plantLayer;
                fish.gameObject.layer=31;camera.orthographicSize=1.25f;
                float time=ocean.AnimationTime;school.Render(time);var state=school.Steering.States[0];var body=school.Steering.BodyBounds(0);
                var target=state.Position+new Vector3(Mathf.Cos(state.Yaw),0,-Mathf.Sin(state.Yaw))*1.2f;
                school.Steering.Navigation.FindClear(target,body,body.Pivot.w,out target);state.Activity=OceanFishActivity.Foraging;state.Until=10000;state.Target=target;school.Steering.States[0]=state;
                float rate=state.YawRate;float peak=0;int moving=0;
                for(int frame=0;frame<180;frame++)
                {
                    if(frame==75)
                    {
                        state=school.Steering.States[0];target=state.Position-new Vector3(Mathf.Sin(state.Yaw),0,Mathf.Cos(state.Yaw))*1.1f;
                        school.Steering.Navigation.FindClear(target,body,body.Pivot.w,out target);state.Target=target;school.Steering.States[0]=state;
                    }
                    school.React(1f/30,null);school.Render(time+(frame+1)/30f);state=school.Steering.States[0];
                    var previousStep=school.Steering.PreviousStepState(0);
                    Require(Mathf.Abs(state.YawRate-previousStep.YawRate)<=ocean.FishAnimation.TurnAcceleration*OceanFishSteering.StepSeconds+.01f,"Actual AA fish has bounded turn acceleration through target changes.");
                    rate=state.YawRate;peak=Mathf.Max(peak,Mathf.Abs(rate));if(state.Velocity.magnitude>.02f)moving++;
                    camera.transform.position=school.transform.parent.TransformPoint(school.Steering.RenderState(0).Position)-camera.transform.forward*18;
                    var image=Capture(camera,"FishTurn"+frame.ToString("000"));
                    if(frame==120)
                    {
                        var paused=school.Steering.RenderState(0);school.React(0,null);school.Render(school.MotionTime,false);
                        Require(paused.Position==school.Steering.RenderState(0).Position&&paused.Yaw==school.Steering.RenderState(0).Yaw,"Pause freezes turn pose and momentum.");
                        Require(image.SequenceEqual(Capture(camera,"FishTurnPause")),"Paused fish image is identical.");
                    }
                }
                Require(peak>30&&moving>90,"Recorded actual fish swims and turns through the retarget.");
                Require(mesh==school.GetComponent<MeshFilter>().sharedMesh&&material==fish.sharedMaterial&&prefabBefore==File.ReadAllText(OceanSetup.PrefabPath),"Preview does not alter authored shared mesh/material/prefab.");
            }
            finally
            {
                plant.SetPropertyBlock(original);plant.gameObject.layer=plantLayer;fish.gameObject.layer=fishLayer;school.ResetSteering();school.Render(ocean.AnimationTime);
                camera.transform.position=position;camera.orthographicSize=size;camera.cullingMask=mask;camera.backgroundColor=background;
                for(int i=0;i<canvases.Length;i++)canvases[i].enabled=visible[i];game.Cameras.SetOcclusionEnabled(true);Object.DestroyImmediate(cameraObject);
            }
        }
        static Color32[] Capture(Camera camera,string name)
        {
            var rt=new RenderTexture(960,540,24);var image=new Texture2D(960,540,TextureFormat.RGB24,false);var target=camera.targetTexture;var active=RenderTexture.active;
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,960,540),0,0);image.Apply();File.WriteAllBytes("Logs/FishTurning/"+name+".png",image.EncodeToPNG());return image.GetPixels32();}
            finally{camera.targetTexture=target;RenderTexture.active=active;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(image);}
        }
    }
}
