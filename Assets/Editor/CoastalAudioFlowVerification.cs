using System;
using System.IO;
using System.Linq;
using System.Reflection;
using MagicIsland.Configuration;
using MagicIsland.Managers;
using MagicIsland.Ocean;
using MagicIsland.Player;
using MagicIsland.UI.Views;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    [InitializeOnLoad] public static class CoastalAudioFlowVerification
    {
        const string Key="MagicIsland.CoastalAudioVerification";
        static int pausedSteps,pausedBirds;
        static float timer;
        static CoastalAudioFlowVerification(){if(SessionState.GetBool(Key,false))EditorApplication.update+=Tick;}
        public static void Prepare(){PrototypeAddressablesSetup.EnsureSetup();AssetDatabase.SaveAssets();}
        public static void Run()
        {
            Prepare();var paths=new[]{"Assets/Config/AudioCatalog.asset"}.Concat(Directory.GetFiles("Assets/Art/Audio/Environment","*.wav")).ToArray();
            var bytes=paths.Select(File.ReadAllBytes).ToArray();Prepare();for(int i=0;i<paths.Length;i++)Require(bytes[i].SequenceEqual(File.ReadAllBytes(paths[i])),"Preserve authored audio "+paths[i]);
            var catalog=AssetDatabase.LoadAssetAtPath<AudioCatalog>(paths[0]);Require(catalog.IsValid&&catalog.Clips.Count==22,"Complete migrated catalog");
            foreach(AudioCue cue in Enum.GetValues(typeof(AudioCue)))if(AudioCatalog.IsEnvironment(cue))
            {
                string path=AudioSetup.ClipPath(cue);var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(path);Require(clip!=null&&clip.length>.1f,"Environment WAV imported "+cue);
                Require(AddressableAssetSettingsDefaultObject.Settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(path)).address==catalog.Find(cue).Address,"Unique AA environment clip "+cue);
                var importer=(AudioImporter)AssetImporter.GetAtPath(path);Require(importer.defaultSampleSettings.loadType==(AudioCatalog.IsAmbient(cue)?AudioClipLoadType.Streaming:AudioClipLoadType.DecompressOnLoad),"Loop/foley import policy "+cue);
            }
            EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");PrototypeAddressablesSetup.BuildContent();var settings=AddressableAssetSettingsDefaultObject.Settings;
            settings.ActivePlayModeDataBuilderIndex=settings.DataBuilders.FindIndex(b=>b is BuildScriptPackedPlayMode);
            SessionState.SetBool(Key,true);SessionState.SetFloat(Key+".Start",(float)EditorApplication.timeSinceStartup);Phase(0);EditorApplication.update-=Tick;EditorApplication.update+=Tick;EditorApplication.isPlaying=true;
        }
        static void Require(bool ok,string why){if(!ok)throw new Exception("Coastal audio: "+why);}
        static void Phase(int p){SessionState.SetInt(Key+".Phase",p);SessionState.SetFloat(Key+".Time",(float)EditorApplication.timeSinceStartup);Debug.Log("Coastal audio phase "+p);}
        static T View<T>()where T:Component=>Object.FindObjectOfType<T>();
        static void Move(GameManager g,Vector3 p){g.Players.RestorePosition(p);Physics.SyncTransforms();}
        static void Walk(GameManager g,Vector2 direction,int frames)
        {for(int i=0;i<frames;i++)typeof(PlayerMovement).GetMethod("TickMovement",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(g.Players.Player,new object[]{direction,.04f});}
        static void Mix(GameManager g,float seconds=3)
        {typeof(AudioManager).GetMethod("TickAmbience",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(g.Audio,new object[]{seconds});}
        static AudioSource[] Sources(GameManager g)=>g.Audio.GetComponentsInChildren<AudioSource>();
        static void NoSave(GameManager g)=>Require(!File.Exists(g.Saves.SavePath),"Environment never writes daytime progress");
        static void CheckStep(GameManager g,Vector3 point,Vector2 direction,int frames,AudioCue cue)
        {
            Move(g,point);int count=g.Audio.FootstepCount;Walk(g,direction,frames);
            Require(g.Audio.FootstepCount>count&&g.Audio.LastEnvironmentSound==cue,"Actual grounded movement chooses "+cue+" at "+g.Players.Player.transform.position);
            count=g.Audio.FootstepCount;Walk(g,Vector2.zero,30);Require(g.Audio.FootstepCount==count,"Stationary foot is silent");
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;int p=SessionState.GetInt(Key+".Phase",0);
            if(p==20&&!EditorApplication.isPlaying){SessionState.SetBool(Key,false);EditorApplication.Exit(0);return;}
            if(EditorApplication.timeSinceStartup-SessionState.GetFloat(Key+".Time",0)<.4)return;
            try
            {
                Require(EditorApplication.timeSinceStartup-SessionState.GetFloat(Key+".Start",0)<240,"Timeout phase "+p);
                if(!EditorApplication.isPlaying)return;var g=GameManager.Instance;if(g==null)return;Require(g.State!=GameFlowState.Failed,"Startup");
                if(p==1&&g.State==GameFlowState.MainMenu)throw new Exception("Coastal audio game initialization returned to menu");
                if(p==0&&g.State==GameFlowState.MainMenu)
                {
                    Require(g.Audio.Ready&&g.Audio.OwnedHandleCount==22&&Sources(g).Length==AudioCatalog.SourceCount,"AA clips and bounded sixteen sources");
                    Require(Sources(g).Where(s=>s.name.StartsWith("Ambience")).All(s=>s.clip==null),"Menu has no island ambience");
                    string dir=Path.GetFullPath("Logs/CoastalAudioSaves/"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
                    g.Saves.Initialize(g.Configuration.Config,g.Configuration.Items,g.Configuration.Crops,dir,g.Configuration.Tasks,g.Configuration.Orders,g.Configuration.Seasons,g.Configuration.Gathering,g.Configuration.ToolUpgrades,g.Configuration.Storage,g.Configuration.NPCs,g.Configuration.Relationships,g.Configuration.Stories,g.Configuration.NPCSchedules,g.Configuration.Fishing,g.Configuration.Crafting,g.Configuration.Animals,g.Configuration.Orchards);
                    g.Settings.Initialize(Path.Combine(dir,"Settings"));g.EnterGame();Phase(1);
                }
                else if(p==1&&g.State==GameFlowState.Playing)
                {
                    g.Time.Clock.Restore(750);g.Weather.Tick(750,true);Move(g,new Vector3(0,.1f,0));g.Audio.TickEnvironment(g,0,false);Mix(g);float far=g.Audio.AmbientVolume(AudioCue.Waves);
                    Move(g,new Vector3(-16,.1f,0));g.Audio.TickEnvironment(g,0,false);Mix(g);Require(g.Audio.AmbientVolume(AudioCue.Waves)>far+.06f&&g.Audio.AmbientVolume(AudioCue.Wind)>0,"Shore distance fades waves and wind plays");
                    Require(g.Audio.AmbientVolume(AudioCue.Rain)==0,"Sunny weather has no rain loop");
                    var wave=Sources(g).Single(s=>s.name=="Ambience Waves");Require(wave.isPlaying&&wave.loop&&wave.clip.name=="Waves","Actual AA wave source plays");float at=wave.time;g.Audio.TickEnvironment(g,0,false);Mix(g);Require(wave.time>=at,"Repeated environment update does not restart loop");
                    CheckStep(g,new Vector3(-6,.1f,-1),Vector2.right,35,AudioCue.StepGrass);
                    CheckStep(g,new Vector3(-16,.1f,0),Vector2.up,26,AudioCue.StepSand);
                    CheckStep(g,new Vector3(14.8f,.3f,-8.2f),Vector2.right,30,AudioCue.StepWood);
                    int count=g.Audio.FootstepCount;g.Players.Player.ToolActionLocked=true;Walk(g,Vector2.right,30);g.Players.Player.ToolActionLocked=false;Require(g.Audio.FootstepCount==count,"Tool lock causes no footsteps");
                    Move(g,new Vector3(0,.1f,-1));Require(g.Audio.FootstepCount==count,"Teleport is silent");
                    var bird=g.Ocean.Surface.Birds.First(b=>b.Activity==CoastalBirdActivity.Perching&&b.Alarm==0);
                    Move(g,g.Ocean.Surface.transform.TransformPoint(bird.Center)+new Vector3(0,.1f,-.8f));g.Audio.TickEnvironment(g,4,false);int flaps=g.Audio.WingFlapCount;
                    g.Ocean.Surface.Tick(.2f,1,false,g.Weather.Sun,g.Players.Player.transform.position);g.Audio.TickEnvironment(g,.2f,false);
                    Require(g.Audio.WingFlapCount==flaps+1,"Actual nearby gull takeoff emits one flutter");g.Ocean.Surface.Tick(.2f,1,false,g.Weather.Sun,g.Players.Player.transform.position);g.Audio.TickEnvironment(g,.2f,false);Require(g.Audio.WingFlapCount==flaps+1,"Sustained alarm does not repeat flutter");
                    int calls=g.Audio.BirdCallCount;g.Audio.TickEnvironment(g,30,false);Require(g.Audio.BirdCallCount==calls+1&&g.Audio.BirdTimer>=g.Configuration.Audio.BirdMinInterval,"Nearby visible gull calls with bounded quiet interval");
                    Require(Sources(g).Any(s=>s.name.StartsWith("Wildlife")&&s.isPlaying&&s.clip.name=="SeagullCall"&&s.volume>0),"Wildlife clip audible with distance attenuation");
                    g.Time.Clock.Restore(1320);g.Audio.TickEnvironment(g,30,false);Require(g.Audio.BirdCallCount==calls+1&&Sources(g).Where(s=>s.name.StartsWith("Wildlife")).All(s=>s.clip==null),"Night suppresses gulls");
                    g.Weather.State.NextDay(g.Weather.State.Day+1,1);g.Time.Clock.Restore(750);g.Audio.TickEnvironment(g,30,false);Mix(g);Require(g.Audio.AmbientVolume(AudioCue.Rain)>.05f&&g.Audio.BirdCallCount==calls+1,"Rain replaces gull calls with rain and wind");
                    g.Settings.SetVolume(0);Require(AudioListener.volume==0,"Master mutes ambience/foley/music");g.Settings.SetVolume(.37f);g.Settings.Flush();
                    g.Weather.State.NextDay(g.Weather.State.Day+1,0);g.Audio.TickEnvironment(g,0,false);Mix(g);Require(g.Audio.AmbientVolume(AudioCue.Rain)==0,"Clear weather releases rain clip");
                    g.OpenPause();Phase(2);
                }
                else if(p==2&&View<PauseView>()!=null)
                {
                    g.Audio.TickEnvironment(g,0,true);pausedSteps=g.Audio.FootstepCount;pausedBirds=g.Audio.BirdCallCount;timer=g.Audio.BirdTimer;
                    g.Audio.TickEnvironment(g,40,true);Walk(g,Vector2.right,30);Require(g.Audio.BirdTimer==timer&&g.Audio.FootstepCount==pausedSteps&&g.Audio.BirdCallCount==pausedBirds,"Pause freezes wildlife timer and movement audio");
                    Require(Sources(g).Where(s=>s.name=="Footsteps"||s.name.StartsWith("Wildlife")).All(s=>s.clip==null),"Pause silences transient world voices");NoSave(g);
                    var quietBird=g.Ocean.Surface.Birds.First(b=>b.Activity==CoastalBirdActivity.Perching&&b.Alarm==0);
                    Move(g,g.Ocean.Surface.transform.TransformPoint(quietBird.Center)+new Vector3(0,.1f,-.8f));int silentFlaps=g.Audio.WingFlapCount;
                    g.Ocean.Surface.Tick(.2f,1,false,g.Weather.Sun,g.Players.Player.transform.position);g.Audio.TickEnvironment(g,.2f,true);g.ResumeFromPause();g.Audio.TickEnvironment(g,0,false);
                    Require(g.Audio.WingFlapCount==silentFlaps,"Muted takeoffs cannot replay after popup closes");
                    Move(g,g.Housing.Entrance.Position+new Vector3(0,.1f,-1.4f));g.EnterHome();Phase(3);
                }
                else if(p==3&&g.State==GameFlowState.Playing&&g.Housing.Inside)
                {
                    g.Audio.TickEnvironment(g,0,false);Mix(g);Require(Sources(g).Where(s=>s.name.StartsWith("Ambience")).All(s=>s.clip==null&&s.volume==0),"Outdoor loops fade out in home");
                    CheckStep(g,g.Configuration.Housing.InteriorOrigin+new Vector3(-2,.1f,-1),Vector2.right,25,AudioCue.StepWood);NoSave(g);
                    Move(g,g.Configuration.Housing.InteriorOrigin+new Vector3(0,.1f,-3.6f));g.ExitHome();Phase(4);
                }
                else if(p==4&&g.State==GameFlowState.Playing&&!g.Housing.Inside)
                {g.Audio.TickEnvironment(g,0,false);Mix(g);Require(g.Audio.AmbientVolume(AudioCue.Wind)>0&&Sources(g).Length==16,"Leaving home resumes ambience without new sources");g.ReturnToMainMenu();Phase(5);}
                else if(p==5&&View<PauseView>()!=null){View<PauseView>().ReturnMenuButton.onClick.Invoke();Phase(6);}
                else if(p==6&&g.State==GameFlowState.MainMenu)
                {
                    Require(Sources(g).Where(s=>s.name.StartsWith("Ambience")||s.name.StartsWith("Wildlife")||s.name=="Footsteps").All(s=>s.clip==null),"Menu clears all island voices");NoSave(g);g.EnterGame();Phase(7);
                }
                else if(p==7&&g.State==GameFlowState.Playing)
                {
                    CheckStep(g,new Vector3(-6,.1f,-1),Vector2.right,35,AudioCue.StepGrass);Require(g.Audio.FootstepCount<6&&Sources(g).Length==16,"Re-entry resets counts and subscribes movement once");NoSave(g);
                    File.WriteAllText("Logs/CoastalAudioVerification.txt","PASS: preserved 22-clip AA catalog and 8 environment WAVs; streaming loops and PCM foley; fixed 16 sources; shore-distance ambience; weather transitions; actual grounded grass/sand/pier/home footsteps; idle/tool/pause/teleport guards; real gull takeoff and one flutter per alarm; sparse visible daytime bird calls, distance/pan, night/rain suppression; master volume; home/menu/re-entry lifecycle; no daytime save.\n");
                    Debug.Log("Coastal audio flow verification PASSED.");Phase(20);EditorApplication.isPlaying=false;
                }
            }
            catch(Exception error){Debug.LogException(error);SessionState.SetBool(Key,false);EditorApplication.isPlaying=false;EditorApplication.Exit(1);}
        }
    }
}
