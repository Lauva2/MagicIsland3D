using System;
using UnityEngine;

namespace MagicIsland.Ocean
{
    [Serializable]
    public sealed class CoastalCrabHabits
    {
        [Min(.1f)] public float MinimumWait=.35f,MaximumWait=.9f;
        [Min(.5f)] public float ForageSeconds=1.6f,WatchSeconds=1.1f;
        public bool IsValid=>Finite(MinimumWait)&&Finite(MaximumWait)&&Finite(ForageSeconds)&&Finite(WatchSeconds)
            &&MinimumWait>=.1f&&MaximumWait>=MinimumWait&&MaximumWait<=20
            &&ForageSeconds>=.5f&&ForageSeconds<=10&&WatchSeconds>=.5f&&WatchSeconds<=10;
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
    }
    public enum CoastalCrabGesture { Resting, Foraging, Watching }

    // Transient, allocation-free state driven only by OceanSurface's clock.
    public sealed class CoastalCrabIdle
    {
        public CoastalCrabGesture Gesture {get;private set;}
        public Vector3 Pose {get;private set;} // Left/right claw pitch and body lean, degrees.
        float elapsed,wait;
        int sequence;
        bool started,finished;
        static float Sample(float phase,int step,float salt)=>Mathf.Repeat(Mathf.Sin(phase*6.37f+step*3.13f+salt)*3187.7f,1);
        public void Reset(){Gesture=CoastalCrabGesture.Resting;Pose=Vector3.zero;elapsed=wait=0;sequence=0;started=finished=false;}
        public void Advance(float seconds,bool settled,float phase,CoastalCrabHabits habits,float foragingRate=1)
        {
            if(seconds<=0)return;
            if(!started){wait=Wait(phase,habits);started=true;}
            foragingRate=Mathf.Max(.01f,foragingRate);
            var target=Vector3.zero;
            if(!settled)
            {
                if(elapsed>0||finished||Gesture!=CoastalCrabGesture.Resting){sequence++;wait=Wait(phase,habits);}
                Gesture=CoastalCrabGesture.Resting;elapsed=0;finished=false;
            }
            else if(!finished)
            {
                elapsed+=seconds*(Gesture==CoastalCrabGesture.Resting?foragingRate:1);
                if(Gesture==CoastalCrabGesture.Resting&&elapsed>=wait)
                {
                    elapsed=(elapsed-wait)/foragingRate;
                    Gesture=Sample(phase,sequence,11)<Mathf.Clamp01(.58f*foragingRate)?CoastalCrabGesture.Foraging:CoastalCrabGesture.Watching;
                }
                if(Gesture!=CoastalCrabGesture.Resting)
                {
                    float duration=Gesture==CoastalCrabGesture.Foraging?habits.ForageSeconds:habits.WatchSeconds;
                    float t=Mathf.Clamp01(elapsed/duration);
                    float envelope=Mathf.SmoothStep(0,1,t/.18f)*Mathf.SmoothStep(0,1,(1-t)/.2f);
                    float wave=Mathf.Sin(t*Mathf.PI*4+phase);
                    target=Gesture==CoastalCrabGesture.Foraging
                        ?new Vector3(28+9*wave,28-9*wave,4)*envelope
                        :new Vector3(-18-5*wave,-18+5*wave,-1.5f)*envelope;
                    if(elapsed>=duration){Gesture=CoastalCrabGesture.Resting;finished=true;target=Vector3.zero;}
                }
            }
            Pose=new Vector3(Mathf.MoveTowards(Pose.x,target.x,seconds*180),Mathf.MoveTowards(Pose.y,target.y,seconds*180),Mathf.MoveTowards(Pose.z,target.z,seconds*35));
        }
        float Wait(float phase,CoastalCrabHabits habits)=>Mathf.Lerp(habits.MinimumWait,habits.MaximumWait,Sample(phase,sequence,3));

        // Same rotations as OceanEcology; used to check the actual baked whole-body envelope.
        public static Vector3 Deform(Vector3 vertex,Vector4 hinge,Vector4 gait,Vector3 pose,float legPhase)
        {
            if(hinge.w>=110&&hinge.w<112)return RotateX(vertex,new Vector3(hinge.x,hinge.y,hinge.z),(hinge.w<111?pose.x:pose.y)*Mathf.Deg2Rad);
            if(hinge.w>=100&&hinge.w<104)
            {
                float angle=Mathf.Sin(gait.x+legPhase+gait.z)*.28f*gait.y;var p=vertex-new Vector3(hinge.x,hinge.y,hinge.z);
                return new Vector3(Mathf.Cos(angle)*p.x+Mathf.Sin(angle)*p.z,p.y,-Mathf.Sin(angle)*p.x+Mathf.Cos(angle)*p.z)+new Vector3(hinge.x,hinge.y,hinge.z);
            }
            return RotateX(vertex,new Vector3(0,.09f,0),pose.z*Mathf.Deg2Rad);
        }
        static Vector3 RotateX(Vector3 vertex,Vector3 pivot,float angle)
        {
            var p=vertex-pivot;return new Vector3(p.x,Mathf.Cos(angle)*p.y-Mathf.Sin(angle)*p.z,Mathf.Sin(angle)*p.y+Mathf.Cos(angle)*p.z)+pivot;
        }
    }
}
