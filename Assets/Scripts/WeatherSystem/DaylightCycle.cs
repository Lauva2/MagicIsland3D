using System;

namespace MagicIsland.WeatherSystem
{
    public static class DaylightCycle
    {
        public static double Evaluate(double minute, double dawn = 300, double daylight = 480, double dusk = 1020, double night = 1200)
        {
            if (double.IsNaN(minute) || double.IsInfinity(minute) || minute < 0 || minute > 1440
                || !ValidHours(dawn, daylight, dusk, night)) throw new ArgumentException("Invalid daylight time");
            if (minute <= dawn || minute >= night) return 0;
            if (minute >= daylight && minute <= dusk) return 1;
            double t = minute < daylight ? (minute - dawn) / (daylight - dawn) : (night - minute) / (night - dusk);
            return t * t * (3 - 2 * t);
        }

        public static bool ValidHours(double dawn, double daylight, double dusk, double night) =>
            dawn >= 0 && dawn < daylight && daylight < dusk && dusk < night && night <= 1440;
    }
}
