using System;

namespace MagicIsland.Ocean
{
    // Transient alarm, advanced exclusively by the ocean's unpaused gameplay delta.
    public sealed class WildlifeAlarm
    {
        public float Level { get; private set; }
        public float HoldRemaining { get; private set; }
        public void Trigger(float holdSeconds) => HoldRemaining=Math.Max(HoldRemaining,Math.Max(0,holdSeconds));
        public void Advance(float seconds,bool nearby,float holdSeconds,float attackSeconds,float recoverSeconds)
        {
            if(seconds<=0||float.IsNaN(seconds)||float.IsInfinity(seconds))return;
            float alertTime=nearby?seconds:Math.Min(seconds,HoldRemaining);
            HoldRemaining=nearby?Math.Max(0,holdSeconds):Math.Max(0,HoldRemaining-seconds);
            Level=Math.Min(1,Level+alertTime/Math.Max(.01f,attackSeconds));
            Level=Math.Max(0,Level-(seconds-alertTime)/Math.Max(.01f,recoverSeconds));
        }
    }
}
