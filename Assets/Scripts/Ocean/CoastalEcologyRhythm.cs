using System;
using UnityEngine;

namespace MagicIsland.Ocean
{
    [Serializable]
    public sealed class CoastalEcologyRhythm
    {
        [Range(0,1)] public float BirdTwilightFlight=.3f,BirdRainFlight=0;
        [Range(.05f,1)] public float BirdTwilightForaging=.55f,BirdRainForaging=.15f;
        [Range(.1f,1.5f)] public float CrabDayMotion=.75f,CrabNightMotion=1;
        [Range(.05f,1)] public float CrabRainMotion=.55f;
        [Range(.1f,2)] public float CrabDayForaging=.7f,CrabNightForaging=1.35f;
        [Range(.05f,1)] public float CrabRainForaging=.35f;
        [Range(.5f,20)] public float TransitionSeconds=6;
        static bool Range(float v,float a,float b)=>!float.IsNaN(v)&&!float.IsInfinity(v)&&v>=a&&v<=b;
        public bool IsValid=>Range(BirdTwilightFlight,0,1)&&Range(BirdRainFlight,0,1)
            &&Range(BirdTwilightForaging,.05f,1)&&Range(BirdRainForaging,.05f,1)
            &&Range(CrabDayMotion,.1f,1.5f)&&Range(CrabNightMotion,.1f,1.5f)&&Range(CrabRainMotion,.05f,1)
            &&Range(CrabDayForaging,.1f,2)&&Range(CrabNightForaging,.1f,2)&&Range(CrabRainForaging,.05f,1)&&Range(TransitionSeconds,.5f,20);
        public Vector2 Bird(float light,bool rain,float twilight)=>new Vector2(
            Mathf.Clamp01(light)*Mathf.Lerp(1,BirdTwilightFlight,Mathf.Clamp01(twilight))*(rain?BirdRainFlight:1),
            Mathf.Lerp(.25f,1,Mathf.Clamp01(light))*Mathf.Lerp(1,BirdTwilightForaging,Mathf.Clamp01(twilight))*(rain?BirdRainForaging:1));
        public Vector2 Crab(float light,bool rain)=>new Vector2(
            Mathf.Lerp(CrabNightMotion,CrabDayMotion,Mathf.Clamp01(light))*(rain?CrabRainMotion:1),
            Mathf.Lerp(CrabNightForaging,CrabDayForaging,Mathf.Clamp01(light))*(rain?CrabRainForaging:1));
    }

    // One instance per animal, no Update or resource ownership. Set targets before Advance;
    // only the first binding initializes immediately, later weather changes require positive time.
    public sealed class CoastalActivityClock
    {
        public float Motion {get;private set;}=1;
        public float Foraging {get;private set;}=1;
        public float Time {get;private set;}
        public bool Configured {get;private set;}
        Vector2 target=Vector2.one;
        float duration=1;
        public void Set(Vector2 value,float seconds,float phase)
        {
            target=value;duration=seconds*(1+.2f*Mathf.Sin(phase*1.37f));
            if(Configured)return;
            Motion=value.x;Foraging=value.y;Configured=true;
        }
        public void Reset(){Motion=Foraging=1;Time=0;Configured=false;target=Vector2.one;}
        public void Advance(float seconds)
        {
            if(seconds<=0||!Configured)return;
            // Integrate the linear ramp exactly, including frames crossing its endpoint.
            float difference=target.x-Motion,moving=Mathf.Min(seconds,Mathf.Abs(difference)*duration);
            Time+=Motion*moving+Mathf.Sign(difference)*moving*moving/(2*duration)+target.x*(seconds-moving);
            Motion=Mathf.MoveTowards(Motion,target.x,seconds/duration);
            Foraging=Mathf.MoveTowards(Foraging,target.y,seconds/duration);
        }
    }
}
