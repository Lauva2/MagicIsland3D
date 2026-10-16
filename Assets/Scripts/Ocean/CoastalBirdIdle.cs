using System;
using UnityEngine;

namespace MagicIsland.Ocean
{
    [Serializable]
    public sealed class CoastalBirdHabits
    {
        [Min(.5f)] public float MinimumWait=4,MaximumWait=10;
        [Min(.5f)] public float PeckSeconds=2.2f,PreenSeconds=3.8f;
        public bool IsValid=>Finite(MinimumWait)&&Finite(MaximumWait)&&Finite(PeckSeconds)&&Finite(PreenSeconds)
            &&MinimumWait>=.5f&&MaximumWait>=MinimumWait&&MaximumWait<=120
            &&PeckSeconds>=.5f&&PeckSeconds<=20&&PreenSeconds>=.5f&&PreenSeconds<=20;
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
    }

    public enum CoastalBirdGesture { Watching, Pecking, Preening }

    // Session-only animation, advanced exclusively by the owning ocean clock.
    public sealed class CoastalBirdIdle
    {
        public CoastalBirdGesture Gesture {get;private set;}
        public Vector2 Head {get;private set;} // Degrees: pitch down, yaw right.
        public float LeftWing {get;private set;}
        public float RightWing {get;private set;}
        float elapsed,wait,side=1;
        int sequence;
        bool started;
        static float Sample(float phase,int step,float salt)=>Mathf.Repeat(Mathf.Sin(phase*7.13f+step*2.71f+salt)*4375.31f,1);
        public void Reset()
        {
            Gesture=CoastalBirdGesture.Watching;Head=Vector2.zero;LeftWing=RightWing=elapsed=wait=0;sequence=0;started=false;
        }
        public void Advance(float seconds,bool settled,float phase,CoastalBirdHabits habits,float foragingRate=1)
        {
            if(seconds<=0)return;
            if(!started){wait=Wait(phase,habits);started=true;}
            foragingRate=Mathf.Max(.01f,foragingRate);
            Vector2 target=Vector2.zero;float wing=0;
            if(!settled)
            {
                // Cancel rather than resume a half-finished peck after a flight.
                if(Gesture!=CoastalBirdGesture.Watching||elapsed>0){sequence++;wait=Wait(phase,habits);}
                Gesture=CoastalBirdGesture.Watching;elapsed=0;
            }
            else
            {
                elapsed+=seconds*(Gesture==CoastalBirdGesture.Watching?foragingRate:1);
                float duration=Gesture==CoastalBirdGesture.Watching?wait:Gesture==CoastalBirdGesture.Pecking?habits.PeckSeconds:habits.PreenSeconds;
                // Consume overshoot, including a low-frame-rate boundary crossing.
                for(int transitions=0;elapsed>=duration&&transitions<512;transitions++)
                {
                    elapsed-=duration;
                    elapsed*=Gesture==CoastalBirdGesture.Watching?1/foragingRate:foragingRate;
                    if(Gesture==CoastalBirdGesture.Watching)
                    {
                        Gesture=Sample(phase,sequence,11)<Mathf.Clamp01(.55f*foragingRate)?CoastalBirdGesture.Pecking:CoastalBirdGesture.Preening;
                        side=Sample(phase,sequence,23)<.5f?-1:1;
                    }
                    else{Gesture=CoastalBirdGesture.Watching;sequence++;wait=Wait(phase,habits);}
                    duration=Gesture==CoastalBirdGesture.Watching?wait:Gesture==CoastalBirdGesture.Pecking?habits.PeckSeconds:habits.PreenSeconds;
                }
                float progress=Mathf.Clamp01(elapsed/duration);
                if(Gesture==CoastalBirdGesture.Pecking)
                {
                    float envelope=Edge(progress,.12f);
                    float peck=Mathf.Pow(.5f-.5f*Mathf.Cos(progress*Mathf.PI*4),2);
                    target=new Vector2(66*peck*envelope,side*5*envelope);
                }
                else if(Gesture==CoastalBirdGesture.Preening)
                {
                    float envelope=Edge(progress,.18f);
                    target=new Vector2((35+7*Mathf.Sin(progress*Mathf.PI*8))*envelope,side*(126+8*Mathf.Sin(progress*Mathf.PI*6))*envelope);
                    wing=14*envelope;
                }
                else target.y=Mathf.Sin(progress*Mathf.PI*2)*12*Mathf.Sin(progress*Mathf.PI);
            }
            Head=new Vector2(Mathf.MoveTowards(Head.x,target.x,seconds*300),Mathf.MoveTowards(Head.y,target.y,seconds*420));
            LeftWing=Mathf.MoveTowards(LeftWing,side<0?wing:0,seconds*100);
            RightWing=Mathf.MoveTowards(RightWing,side>0?wing:0,seconds*100);
        }
        float Wait(float phase,CoastalBirdHabits habits)=>Mathf.Lerp(habits.MinimumWait,habits.MaximumWait,Sample(phase,sequence,3));
        static float Edge(float t,float edge)=>Mathf.SmoothStep(0,1,t/edge)*Mathf.SmoothStep(0,1,(1-t)/edge);

        // Mirrors OceanEcology's neck deformation for complete-body envelope checks.
        public static Vector3 Deform(Vector3 vertex,Vector2 head)
        {
            float weight=Mathf.SmoothStep(0,1,(vertex.y-.28f)/.09f)*Mathf.SmoothStep(0,1,(vertex.z-.07f)/.11f);
            var pivot=new Vector3(0,.29f,.17f);var p=vertex-pivot;
            float pitch=head.x*weight*Mathf.Deg2Rad,yaw=head.y*weight*Mathf.Deg2Rad;
            p=new Vector3(p.x,Mathf.Cos(pitch)*p.y-Mathf.Sin(pitch)*p.z,Mathf.Sin(pitch)*p.y+Mathf.Cos(pitch)*p.z);
            p=new Vector3(Mathf.Cos(yaw)*p.x+Mathf.Sin(yaw)*p.z,p.y,-Mathf.Sin(yaw)*p.x+Mathf.Cos(yaw)*p.z);
            return p+pivot;
        }
    }
}
