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
        public float DayIntensity = 1.05f;
        [Tooltip("Moonlight intensity; sunlight is zero after sunset.")] public float NightIntensity = 0.12f;
        [Range(0, 1)] public float RainLightMultiplier = 0.32f;
        [Tooltip("Sky fill, independent of the direct sunlight.")] public Color DayAmbient = new Color(0.28f, 0.34f, 0.42f);
        public Color NightAmbient = new Color(0.055f, 0.075f, 0.12f);
        public Color DayEquator = new Color(0.16f, 0.19f, 0.235f), DayGround = new Color(0.09f, 0.08f, 0.065f);
        public Color NightEquator = new Color(0.03f, 0.045f, 0.075f), NightGround = new Color(0.012f, 0.018f, 0.035f);
        public float DayReflection = 0.45f, NightReflection = 0.015f;
        public float SunPeakElevation = 65, SunriseAzimuth = -105, SunsetAzimuth = 75;
        public Color DaySun = new Color(1, 0.975f, 0.92f);
        public Color NightSun = new Color(0.70f, 0.79f, 1f);
        public Color TwilightSun = new Color(1, 0.65f, 0.4f);
        public Color DaySky = new Color(0.62f, 0.79f, 0.84f);
        public Color NightSky = new Color(0.006f, 0.010f, 0.022f);
        public Color RainSky = new Color(0.43f, 0.53f, 0.6f);
        [Header("Visual transitions")]
        [Range(0, 20)] public float WeatherTransitionSeconds = 3;
        public Color TwilightSky = new Color(.78f, .52f, .41f);
        public Color TwilightAmbient = new Color(.43f, .25f, .16f);
        [Range(0, 1)] public float TwilightStrength = .22f;
        public Material RainMaterial;
        public float RainParticlesPerSecond = 260;
        [Header("Shared outdoor wind")]
        public Vector2 WindDirection=new Vector2(1,.35f);
        [Range(0,2)] public float SunnyWindStrength=.65f,RainWindStrength=1.25f;
        [Range(0,.5f)] public float WindGustAmount=.18f;
        [Range(.1f,20)] public float WindChangeSeconds=4;

        public bool IsValid => Finite(RainChance) && RainChance >= 0 && RainChance <= 1
            && Finite(WeatherTransitionSeconds) && WeatherTransitionSeconds >= 0 && WeatherTransitionSeconds <= 20
            && Finite(TwilightStrength) && TwilightStrength >= 0 && TwilightStrength <= 1 && ValidColor(TwilightSky) && ValidColor(TwilightAmbient)
            && Finite(WindDirection.x)&&Finite(WindDirection.y)&&WindDirection.sqrMagnitude>.01f&&WindDirection.sqrMagnitude<100
            && Finite(SunnyWindStrength)&&SunnyWindStrength>=0&&SunnyWindStrength<=2
            && Finite(RainWindStrength)&&RainWindStrength>=SunnyWindStrength&&RainWindStrength<=2
            && Finite(WindGustAmount)&&WindGustAmount>=0&&WindGustAmount<=.5f
            && Finite(WindChangeSeconds)&&WindChangeSeconds>=.1f&&WindChangeSeconds<=20
            && DaylightCycle.ValidHours(DawnMinute, DaylightMinute, DuskMinute, NightMinute)
            && Finite(DayIntensity) && Finite(NightIntensity) && NightIntensity > 0 && DayIntensity >= NightIntensity
            && Finite(RainLightMultiplier) && RainLightMultiplier > 0 && RainLightMultiplier <= 1
            && ValidColor(DayAmbient) && ValidColor(NightAmbient) && ValidColor(DaySun) && ValidColor(NightSun)
            && ValidColor(TwilightSun) && ValidColor(DaySky) && ValidColor(NightSky) && ValidColor(RainSky)
            && ValidColor(DayEquator) && ValidColor(DayGround) && ValidColor(NightEquator) && ValidColor(NightGround)
            && Finite(DayReflection) && Finite(NightReflection) && NightReflection >= 0 && DayReflection >= NightReflection && DayReflection <= 1
            && Finite(SunPeakElevation) && SunPeakElevation >= 15 && SunPeakElevation <= 85
            && Finite(SunriseAzimuth) && Finite(SunsetAzimuth) && SunriseAzimuth >= -180 && SunsetAzimuth <= 180 && SunriseAzimuth < SunsetAzimuth
            && RainMaterial != null && Finite(RainParticlesPerSecond) && RainParticlesPerSecond > 0 && RainParticlesPerSecond <= 2000;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool ValidColor(Color color) => Finite(color.r) && Finite(color.g) && Finite(color.b) && Finite(color.a)
            && color.r >= 0 && color.g >= 0 && color.b >= 0 && color.r <= 1 && color.g <= 1 && color.b <= 1 && color.a >= 0 && color.a <= 1;
    }
}
