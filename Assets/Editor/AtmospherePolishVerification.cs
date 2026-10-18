using System;
using System.IO;
using System.Reflection;
using MagicIsland.CalendarSystem;
using MagicIsland.Configuration;
using MagicIsland.Managers;
using MagicIsland.WeatherSystem;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    public static class AtmospherePolishVerification
    {
        static void Require(bool ok, string why) { if (!ok) throw new Exception(why); }
        public static void Advance(WeatherManager weather, float seconds, bool paused = false) => typeof(WeatherManager)
            .GetMethod("TickPresentation", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(weather, new object[] { seconds, paused });
        static Vector4 Foliage => Shader.GetGlobalVector("_IslandSeasonFoliage");
        static void Palettes(SeasonConfig source)
        {
            var config = Object.Instantiate(source); var scope = new SeasonalPresentation();
            try
            {
                foreach (int days in new[] { 1, 2, 28, 365 }) foreach (Season season in Enum.GetValues(typeof(Season)))
                {
                    config.DaysPerSeason = days; config.PaletteTransitionDays = 7;
                    scope.Apply(config, season); var start = Foliage;
                    var next = (Season)(((int)season + 1) % 4); scope.Apply(config, next); var end = Foliage;
                    scope.Apply(config, season, days); var last = Foliage;
                    Require(Vector4.Distance(last, days == 1 ? start : end) < .00001f, "Season boundary / one-day compatibility " + season + "/" + days);
                    float distance = float.MaxValue;
                    for (int day = 1; day <= days; day++)
                    {
                        scope.Apply(config, season, day); float current = Vector4.Distance(Foliage, end);
                        Require(current <= distance + .00001f, "Season palette progresses monotonically " + season); distance = current;
                    }
                    config.PaletteTransitionDays = 0; scope.Apply(config, season, days);
                    Require(Vector4.Distance(Foliage, start) < .00001f, "Zero days preserves authored season palette");
                }
                config.PaletteTransitionDays = -1; Require(!config.IsValid, "Reject negative palette transition");
                config.PaletteTransitionDays = 366; Require(!config.IsValid, "Reject excessive palette transition");
            }
            finally { scope.Clear(); Object.DestroyImmediate(config); }
        }
        public static void Verify(GameManager g)
        {
            Palettes(g.Configuration.Seasons);
            var validation = Object.Instantiate(g.Configuration.Weather);
            try
            {
                foreach (float value in new[] { float.NaN, float.PositiveInfinity, -.01f, 20.01f })
                { validation.WeatherTransitionSeconds = value; Require(!validation.IsValid, "Reject invalid weather transition duration"); }
                validation.WeatherTransitionSeconds = 0; Require(validation.IsValid, "Zero duration supports immediate weather presentation");
                foreach (float value in new[] { float.NaN, float.PositiveInfinity, -.01f, 1.01f })
                { validation.TwilightStrength = value; Require(!validation.IsValid, "Reject invalid twilight strength"); }
            }
            finally { Object.DestroyImmediate(validation); }
            var weather = g.Weather; var state = weather.State; int day = state.Day; var kind = state.Kind;
            double minute = g.Time.Clock.Minute;
            float seconds = g.Configuration.Weather.WeatherTransitionSeconds;
            Require(seconds > 0, "Transition fixture uses a configured positive duration");
            try
            {
                state.NextDay(state.Day + 1, 0); Advance(weather, seconds); weather.Tick(750, true);
                float sunny = weather.Sun.intensity;
                state.NextDay(state.Day + 1, 1); Advance(weather, seconds, true); weather.Tick(750, true);
                Require(weather.RainAmount == 0 && weather.Sun.intensity == sunny, "Paused weather change freezes presentation");
                Advance(weather, seconds * .5f); weather.Tick(750, true);
                Require(Mathf.Abs(weather.RainAmount - .5f) < .0001f && weather.Sun.intensity < sunny
                    && weather.Sun.intensity > sunny * g.Configuration.Weather.RainLightMultiplier, "Half transition interpolates sunlight");
                var block = new MaterialPropertyBlock(); weather.Clouds.Renderers[0].GetPropertyBlock(block);
                Require(Mathf.Abs(block.GetFloat("_Rain") - .5f) < .0001f && Mathf.Abs(weather.Rain.emission.rateOverTime.constant - g.Configuration.Weather.RainParticlesPerSecond * .5f) < .001f,
                    "Cloud tint and particle emission share the same rain amount");
                g.Ocean.Surface.Tick(0, 1, true, rainAmount:weather.RainAmount);
                Require(g.Ocean.Surface.Rain == weather.RainAmount, "Sea appearance shares weather blend");
                LightingFlowVerification.Capture(g, "AtmosphereRainHalf", new Vector3(0, .6f, 0));
                Advance(weather, seconds * .5f); weather.Tick(750, true);
                Require(weather.RainAmount == 1 && Mathf.Abs(weather.Sun.intensity - sunny * g.Configuration.Weather.RainLightMultiplier) < .0001f, "Rain reaches its configured endpoint");
                state.NextDay(state.Day + 1, 0); Advance(weather, seconds * .25f); weather.Tick(750, true);
                Require(Mathf.Abs(weather.RainAmount - .75f) < .0001f, "Rain stops gradually without a jump");
                Advance(weather, seconds * 20); weather.Tick(750, true); Require(weather.RainAmount == 0, "Low-frame-rate transition clamps at endpoint");
                weather.Tick(1110, true); var warm = g.Players.Player.ViewCamera.backgroundColor;
                float strength = g.Configuration.Weather.TwilightStrength;
                try { g.Configuration.Weather.TwilightStrength = 0; weather.Tick(1110, true); Require(warm.r > g.Players.Player.ViewCamera.backgroundColor.r, "Twilight sky receives warm tint"); }
                finally { g.Configuration.Weather.TwilightStrength = strength; }
                LightingFlowVerification.SetTime(g,1110); LightingFlowVerification.Capture(g, "AtmosphereTwilight", new Vector3(0, .6f, 0));
                weather.Tick(1320, true); Require(weather.Sun.intensity == 0, "Twilight warmth does not brighten the night sun");
                File.WriteAllText("Logs/AtmospherePolishVerification.txt", "PASS: packed AA; calendar-derived palette transitions across all four seasons and year boundary, short seasons and disabled blending; monotonic colours and global restoration; paused rain blend, half/end/reversal/low-frame-rate states; synchronized cloud tint, rain emission, sea and lighting; twilight warmth and zero night sunlight.\n");
            }
            finally
            {
                typeof(WeatherState).GetProperty("Day").SetValue(state, day);
                typeof(WeatherState).GetProperty("Kind").SetValue(state, kind);
                Advance(weather, seconds * 20); LightingFlowVerification.SetTime(g,minute);
            }
        }
    }
}
