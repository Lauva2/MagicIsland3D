using System;
namespace MagicIsland.Tools
{
    public sealed class ToolActionGate
    {
        public double Remaining { get; private set; }
        public bool Busy => Remaining > 0;
        public bool TryBegin(double duration)
        {
            if (duration <= 0 || double.IsNaN(duration) || double.IsInfinity(duration)) throw new ArgumentOutOfRangeException(nameof(duration));
            if (Busy) return false;
            Remaining = duration; return true;
        }
        public void Tick(double seconds, bool paused)
        {
            if (seconds < 0 || double.IsNaN(seconds) || double.IsInfinity(seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
            if (!paused) Remaining = Math.Max(0, Remaining - seconds);
        }
        public void Reset() => Remaining = 0;
    }
}
