using System;

namespace MagicIsland.TimeSystem
{
    public sealed class ClockState
    {
        public const int Morning = 360;
        public const int EndOfDay = 1440;
        public double Minute { get; private set; } = Morning;
        public int DisplayMinute => (int)Minute;
        public bool Finished => Minute >= EndOfDay;
        public void Restore(double minute)
        {
            if (double.IsNaN(minute) || double.IsInfinity(minute) || minute < 0 || minute > EndOfDay)
                throw new ArgumentOutOfRangeException(nameof(minute));
            Minute = minute;
        }
        public void Tick(double seconds, double secondsPerMinute, bool paused)
        {
            if (seconds < 0 || double.IsNaN(seconds) || double.IsInfinity(seconds)
                || secondsPerMinute <= 0 || double.IsNaN(secondsPerMinute) || double.IsInfinity(secondsPerMinute))
                throw new ArgumentOutOfRangeException(nameof(seconds));
            if (!paused) Minute = Math.Min(EndOfDay, Minute + seconds / secondsPerMinute);
        }
        public void NextMorning() => Minute = Morning;
    }
}
