using System.Collections.Generic;
using MagicIsland.Configuration;
using MagicIsland.Interaction;
using MagicIsland.Ocean;
using MagicIsland.Player;
using MagicIsland.WeatherSystem;
using UnityEngine;

namespace MagicIsland.Managers
{
    public sealed partial class AudioManager
    {
        private static readonly AudioCue[] AmbientCues={AudioCue.Waves,AudioCue.Wind,AudioCue.Rain};
        private AudioSource[] ambience,wildlife;
        private AudioSource footsteps;
        private readonly float[] ambientTargets=new float[3];
        private readonly Vector3[] birdPositions=new Vector3[2];
        private readonly float[] birdGains=new float[2];
        private readonly Dictionary<CoastalBird,int> observedTakeoffs=new Dictionary<CoastalBird,int>();
        private readonly System.Random environmentRandom=new System.Random(42731);
        private PlayerMovement walkingPlayer;
        private float birdTimer=6,flapCooldown,stepDistance;
        private int birdVoice;
        public int FootstepCount { get; private set; }
        public int BirdCallCount { get; private set; }
        public int WingFlapCount { get; private set; }
        public AudioCue? LastEnvironmentSound { get; private set; }
        public float BirdTimer=>birdTimer;
        public float AmbientVolume(AudioCue cue)
        {for(int i=0;i<AmbientCues.Length;i++)if(AmbientCues[i]==cue)return ambience?[i]?.volume??0;return 0;}

        private void CreateEnvironmentSources()
        {
            ambience=new AudioSource[3];for(int i=0;i<3;i++){ambience[i]=Source("Ambience "+AmbientCues[i],true);ambience[i].priority=160;}
            wildlife=new[]{Source("Wildlife A",false),Source("Wildlife B",false)};
            footsteps=Source("Footsteps",false);footsteps.priority=110;
        }
        private void BindEnvironment(PlayerMovement player)
        {
            walkingPlayer=player;if(player!=null)player.GroundMoved+=GroundStep;
            stepDistance=0;birdTimer=6;flapCooldown=0;observedTakeoffs.Clear();
        }
        private void UnbindEnvironment()
        {
            if(walkingPlayer!=null)walkingPlayer.GroundMoved-=GroundStep;walkingPlayer=null;
            SuspendEnvironment();StopEnvironmentLoops();observedTakeoffs.Clear();
            stepDistance=0;birdTimer=6;flapCooldown=0;birdVoice=0;
            FootstepCount=BirdCallCount=WingFlapCount=0;LastEnvironmentSound=null;
        }
        public void SuspendEnvironment()
        {System.Array.Clear(ambientTargets,0,ambientTargets.Length);StopEnvironmentVoices();stepDistance=0;}
        private void StopEnvironmentVoices()
        {
            if(footsteps!=null){footsteps.Stop();footsteps.clip=null;}
            if(wildlife!=null)foreach(var source in wildlife)if(source!=null){source.Stop();source.clip=null;}
        }
        private void StopEnvironmentLoops()
        {if(ambience!=null)foreach(var source in ambience)if(source!=null){source.Stop();source.clip=null;source.volume=0;}}
        internal void TickAmbience(float seconds)
        {
            if(!Ready||!isActiveAndEnabled||ambience==null)return;
            float delta=Mathf.Max(0,seconds)/catalog.AmbientFadeSeconds;
            for(int i=0;i<3;i++)
            {
                var source=ambience[i];float target=ambientTargets[i];
                if(target>0&&source.clip==null){source.clip=clips[AmbientCues[i]];source.Play();}
                source.volume=Mathf.MoveTowards(source.volume,target,delta);
                if(source.volume<=.00001f&&target==0&&source.clip!=null){source.Stop();source.clip=null;}
            }
        }
        public void TickEnvironment(GameManager game,float seconds,bool paused)
        {
            if(!Ready||!isActiveAndEnabled||game==null||game.State!=GameFlowState.Playing||game.Players.Player==null)
            {SuspendEnvironment();return;}
            var player=game.Players.Player;var p=player.transform.position;
            bool outside=!game.Housing.Inside&&game.Ocean.Surface!=null;
            bool raining=game.Weather.State.Kind==WeatherKind.Rainy;
            if(outside)
            {
                float distance=(float)System.Math.Max(0,p.z>100?StarterIslandLayout.CoastDistance(p.x,p.z):IslandLayout.CoastRadius(System.Math.Atan2(p.z,p.x))-System.Math.Sqrt(p.x*p.x+p.z*p.z));
                float shore=1-Mathf.SmoothStep(0,1,Mathf.Clamp01(distance/catalog.ShoreHearingDistance));
                float duck=paused?.45f:1;
                ambientTargets[0]=catalog.EffectsVolume*catalog.WavesVolume*catalog.Find(AudioCue.Waves).Gain*shore*(raining?1.12f:1)*duck;
                ambientTargets[1]=catalog.EffectsVolume*catalog.WindVolume*catalog.Find(AudioCue.Wind).Gain*Mathf.Clamp(game.Weather.WindFactor,0,3)*duck;
                ambientTargets[2]=raining?catalog.EffectsVolume*catalog.RainVolume*catalog.Find(AudioCue.Rain).Gain*duck:0;
            }
            else System.Array.Clear(ambientTargets,0,ambientTargets.Length);
            if(paused||!player.isActiveAndEnabled)
            {
                // Fishing can animate the coast behind its panel; never queue a late flutter for closing it.
                if(game.Ocean.Surface!=null)foreach(var bird in game.Ocean.Surface.Birds)if(bird!=null)observedTakeoffs[bird]=bird.TakeoffCount;
                StopEnvironmentVoices();stepDistance=0;return;
            }
            if(!outside){StopBirds();return;}
            float dt=Mathf.Max(0,seconds);flapCooldown=Mathf.Max(0,flapCooldown-dt);
            var weather=game.Configuration.Weather;
            float daylight=(float)DaylightCycle.Evaluate(game.Time.Clock.Minute,weather.DawnMinute,weather.DaylightMinute,weather.DuskMinute,weather.NightMinute);
            bool birdsAudible=daylight>.12f&&!raining;
            var birds=game.Ocean.Surface.Birds;
            CoastalBird nearest=null;float nearestDistance=catalog.BirdHearingDistance;
            foreach(var bird in birds)
            {
                if(bird==null)continue;
                if(!observedTakeoffs.TryGetValue(bird,out int seen))seen=0;
                observedTakeoffs[bird]=bird.TakeoffCount;
                float distance=Vector3.Distance(p,bird.transform.position);
                if(!birdsAudible||!bird.gameObject.activeInHierarchy)continue;
                if(bird.TakeoffCount>seen&&flapCooldown==0&&distance<12)
                {if(PlayBird(AudioCue.WingFlap,bird.transform.position,.8f,game)){WingFlapCount++;flapCooldown=2.5f;}}
                if(distance<nearestDistance){nearest=bird;nearestDistance=distance;}
            }
            if(!birdsAudible){StopBirds();return;}
            birdTimer-=dt;
            if(birdTimer<=0)
            {
                if(nearest!=null&&PlayBird(AudioCue.SeagullCall,nearest.transform.position,1,game))BirdCallCount++;
                birdTimer=catalog.BirdMinInterval+(float)environmentRandom.NextDouble()*(catalog.BirdMaxInterval-catalog.BirdMinInterval);
            }
            for(int i=0;i<wildlife.Length;i++)if(wildlife[i].clip!=null)ApplyBirdDistance(wildlife[i],birdPositions[i],birdGains[i],game);
        }
        private void StopBirds(){if(wildlife!=null)foreach(var source in wildlife){source.Stop();source.clip=null;}}
        private bool PlayBird(AudioCue cue,Vector3 position,float gain,GameManager game)
        {
            if(wildlife==null)return false;
            int index=birdVoice++%wildlife.Length;if(birdVoice==int.MaxValue)birdVoice=0;
            var source=wildlife[index];source.Stop();source.clip=clips[cue];source.pitch=.94f+(float)environmentRandom.NextDouble()*.12f;
            birdPositions[index]=position;birdGains[index]=gain*catalog.Find(cue).Gain;
            ApplyBirdDistance(source,position,birdGains[index],game);
            if(source.volume<.005f){source.clip=null;return false;}
            source.Play();LastEnvironmentSound=cue;return true;
        }
        private void ApplyBirdDistance(AudioSource source,Vector3 point,float gain,GameManager game)
        {
            var player=game.Players.Player;Vector3 relative=point-player.transform.position;
            float falloff=1-Mathf.Clamp01(relative.magnitude/catalog.BirdHearingDistance);
            source.volume=catalog.EffectsVolume*catalog.BirdVolume*gain*falloff*falloff;
            source.panStereo=Mathf.Clamp(Vector3.Dot(relative,player.ViewCamera.transform.right)/catalog.BirdHearingDistance,-.75f,.75f);
        }
        private void GroundStep(Vector3 position,float distance)
        {
            var game=GameManager.Instance;
            if(!Ready||!isActiveAndEnabled||walkingPlayer==null||!walkingPlayer.isActiveAndEnabled||game==null
                ||game.State!=GameFlowState.Playing||game.UI.AnyPopupVisible||walkingPlayer.ToolActionLocked||distance<=0)return;
            // Teleports/restores do not emit this event; discard abnormally large motion as well.
            if(distance>1){stepDistance=0;return;}
            stepDistance+=distance;if(stepDistance<catalog.FootstepStride)return;stepDistance%=catalog.FootstepStride;
            AudioCue cue=FootstepCue(position,game.Housing.Inside);
            footsteps.Stop();footsteps.clip=clips[cue];footsteps.pitch=.92f+(float)environmentRandom.NextDouble()*.16f;
            footsteps.volume=catalog.EffectsVolume*catalog.FootstepVolume*catalog.Find(cue).Gain;footsteps.Play();
            FootstepCount++;LastEnvironmentSound=cue;
        }
        public static AudioCue FootstepCue(Vector3 position,bool inside)
        {
            if(inside||IslandLayout.ContainsPier(position.x,position.z))return AudioCue.StepWood;
            if(position.z>23)
            {
                if(StarterIslandLayout.Corridor(position.x,position.z)||StarterIslandLayout.Bridge(position.x,position.z))return AudioCue.StepWood;
                return StarterIslandLayout.Region(position.x,position.z)==1||StarterIslandLayout.CoastDistance(position.x,position.z)<20?AudioCue.StepSand:AudioCue.StepGrass;
            }
            double radius=System.Math.Sqrt(position.x*position.x+position.z*position.z);
            return IslandLayout.CoastRadius(System.Math.Atan2(position.z,position.x))-radius<3.4?AudioCue.StepSand:AudioCue.StepGrass;
        }
    }
}
