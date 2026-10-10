using System;

namespace MagicIsland.WeatherSystem
{
    public enum WeatherKind { Sunny, Rainy }

    // A saved seed and day determine future weather without consuming Unity's random stream.
    public sealed class WeatherState
    {
        public int Seed { get; }
        public int Day { get; private set; }
        public WeatherKind Kind { get; private set; }
        private readonly double rainChance;

        public WeatherState(int seed, double rainChance, int day = 1, WeatherKind kind = WeatherKind.Sunny)
        {
            if (seed < 1 || day < 1 || day == int.MaxValue || !IsValid(kind)
                || double.IsNaN(rainChance) || double.IsInfinity(rainChance) || rainChance < 0 || rainChance > 1)
                throw new ArgumentException("Invalid weather state");
            Seed = seed; Day = day; Kind = kind; this.rainChance = rainChance;
        }

        public void NextDay(int day)
            => NextDay(day, rainChance);

        public void NextDay(int day, double chance)
        {
            if (day != Day + 1 || day == int.MaxValue) throw new ArgumentException("Weather must advance one day with the farm");
            if (double.IsNaN(chance) || double.IsInfinity(chance) || chance < 0 || chance > 1) throw new ArgumentException("Invalid rain chance");
            uint hash;
            unchecked
            {
                hash = (uint)Seed ^ ((uint)day * 0x9e3779b9u);
                hash ^= hash >> 16; hash *= 0x7feb352du;
                hash ^= hash >> 15; hash *= 0x846ca68bu; hash ^= hash >> 16;
            }
            Kind = hash / 4294967296.0 < chance ? WeatherKind.Rainy : WeatherKind.Sunny;
            Day = day;
        }

        public static bool IsValid(WeatherKind kind) => kind == WeatherKind.Sunny || kind == WeatherKind.Rainy;
    }
}
