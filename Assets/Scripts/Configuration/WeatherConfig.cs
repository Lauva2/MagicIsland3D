using MagicIsland.WeatherSystem;
using UnityEngine;

namespace MagicIsland.Configuration
{
    [CreateAssetMenu(menuName = "Magic Island/Weather Configuration")]
    public sealed class WeatherConfig : ScriptableObject
    {
        public const string Address = "WeatherConfig";
        [Tooltip("Fallback for weather initialized without CalendarManager. Game flow uses SeasonConfig probabilities.")]
        [Range(0, 1)] public float RainChance = 0.3f;
        public float DawnMinute = 300, DaylightMinute = 480, DuskMinute = 1020, NightMinute = 1200;
        public float DayIntensity = 1.05f, NightIntensity = 0.18f;
        [Range(0, 1)] public float RainLightMultiplier = 0.65f;
        public Color DayAmbient = new Color(0.68f, 0.73f, 0.69f);
        public Color NightAmbient = new Color(0.22f, 0.28f, 0.4f);
        public Color DaySun = new Color(1, 0.96f, 0.86f);
        public Color NightSun = new Color(0.57f, 0.67f, 0.95f);
        public Color TwilightSun = new Color(1, 0.65f, 0.4f);
        public Color DaySky = new Color(0.62f, 0.79f, 0.84f);
        public Color NightSky = new Color(0.08f, 0.12f, 0.23f);
        public Color RainSky = new Color(0.43f, 0.53f, 0.6f);
        public Material RainMaterial;
        public float RainParticlesPerSecond = 260;

        public bool IsValid => Finite(RainChance) && RainChance >= 0 && RainChance <= 1
            && DaylightCycle.ValidHours(DawnMinute, DaylightMinute, DuskMinute, NightMinute)
            && Finite(DayIntensity) && Finite(NightIntensity) && NightIntensity > 0 && DayIntensity >= NightIntensity
            && Finite(RainLightMultiplier) && RainLightMultiplier > 0 && RainLightMultiplier <= 1
            && ValidColor(DayAmbient) && ValidColor(NightAmbient) && ValidColor(DaySun) && ValidColor(NightSun)
            && ValidColor(TwilightSun) && ValidColor(DaySky) && ValidColor(NightSky) && ValidColor(RainSky)
            && RainMaterial != null && Finite(RainParticlesPerSecond) && RainParticlesPerSecond > 0 && RainParticlesPerSecond <= 2000;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool ValidColor(Color color) => Finite(color.r) && Finite(color.g) && Finite(color.b) && Finite(color.a)
            && color.r >= 0 && color.g >= 0 && color.b >= 0 && color.r <= 1 && color.g <= 1 && color.b <= 1 && color.a >= 0 && color.a <= 1;
    }
}
