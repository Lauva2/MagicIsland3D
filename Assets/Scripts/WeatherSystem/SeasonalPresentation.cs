using MagicIsland.CalendarSystem;
using MagicIsland.Configuration;
using UnityEngine;

namespace MagicIsland.WeatherSystem
{
    // WeatherManager owns this scope after calendar/config initialization. It only changes
    // shader globals: no material instances, scene references, events or persistent state.
    public sealed class SeasonalPresentation
    {
        private static readonly int Grass=Shader.PropertyToID("_IslandSeasonGrass");
        private static readonly int Foliage=Shader.PropertyToID("_IslandSeasonFoliage");
        private static readonly int Evergreen=Shader.PropertyToID("_IslandSeasonEvergreen");
        private readonly Vector4 previousGrass,previousFoliage,previousEvergreen;
        private bool cleared;
        public SeasonalPresentation()
        {
            previousGrass=Shader.GetGlobalVector(Grass);previousFoliage=Shader.GetGlobalVector(Foliage);
            previousEvergreen=Shader.GetGlobalVector(Evergreen);
        }
        // Caller has validated AA config. Apply before enabling input, and after day settlement.
        // Null config means neutral presentation for isolated weather fixtures without a calendar.
        public void Apply(SeasonConfig config,Season season)
        {
            if(cleared)return;
            var palette=config==null?null:config.Palette(season);
            Shader.SetGlobalVector(Grass,Value(palette?.Grass??Color.white,palette?.Strength??0));
            Shader.SetGlobalVector(Foliage,Value(palette?.Foliage??Color.white,palette?.Strength??0));
            Shader.SetGlobalVector(Evergreen,Value(palette?.Evergreen??Color.white,palette?.Strength??0));
        }
        private static Vector4 Value(Color color,float strength)
        {
            if(QualitySettings.activeColorSpace==ColorSpace.Linear)color=color.linear;
            return new Vector4(color.r,color.g,color.b,strength);
        }
        // Idempotent cleanup before AA dependencies are released, including failed initialization.
        public void Clear()
        {
            if(cleared)return;cleared=true;
            Shader.SetGlobalVector(Grass,previousGrass);Shader.SetGlobalVector(Foliage,previousFoliage);
            Shader.SetGlobalVector(Evergreen,previousEvergreen);
        }
    }
}
