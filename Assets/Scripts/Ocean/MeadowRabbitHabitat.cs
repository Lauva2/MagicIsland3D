using System;
using UnityEngine;

namespace MagicIsland.Ocean
{
    // Baked safe strip in WorldWaterEcology local space; AA owns this cosmetic definition.
    [Serializable] public sealed class MeadowRabbitHabitat
    {
        public Vector3[] Track=Array.Empty<Vector3>();
        public float Phase,HopDistance=.65f,Speed=.85f,EscapeSpeed=1.7f,WaitSeconds=3.2f,StartleDistance=3.5f;
        public bool IsValid
        {
            get
            {
                if(Track==null||Track.Length<3||Track.Length>64||!Finite(Phase)||!Positive(HopDistance)||HopDistance>1
                    ||!Positive(Speed)||!Positive(EscapeSpeed)||EscapeSpeed<Speed||EscapeSpeed>3||!Positive(WaitSeconds)||!Positive(StartleDistance))return false;
                for(int i=0;i<Track.Length;i++)
                {
                    var p=Track[i];if(!Finite(p.x)||!Finite(p.y)||!Finite(p.z))return false;
                    if(i>0){float d=Vector3.Distance(Track[i-1],p);if(d<.01f||d>.3f)return false;}
                }
                return true;
            }
        }
        static bool Finite(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v);
        static bool Positive(float v)=>Finite(v)&&v>0;
    }
}
