using MagicIsland.CalendarSystem;
using MagicIsland.Configuration;
using UnityEngine;

namespace MagicIsland.Managers
{
    public sealed class CalendarManager : MonoBehaviour
    {
        public SeasonConfig Config { get; private set; }
        private FarmManager farm;
        // The farm's saved total day is the single source of truth for calendar and weather.
        public CalendarDate Date => new CalendarDate(farm.State.Day, Config.DaysPerSeason);
        public void Initialize(SeasonConfig config, FarmManager farm)
        {
            if (config == null || !config.IsValid || farm?.State == null) throw new System.ArgumentException("Invalid calendar initialization");
            Config = config; this.farm = farm;
        }
        public bool CanPlant(CropConfig crop) => crop != null && Seasons.Includes(crop.PlantingSeasons, Date.Season);
        public string PlantingDescription(CropConfig crop) => "适宜播种：" + Seasons.Describe(crop.PlantingSeasons)
            + (CanPlant(crop) ? " · 当前可播种" : " · 当前季节不适宜");
        public double RainChance(int totalDay) => Config.RainChance(new CalendarDate(totalDay, Config.DaysPerSeason).Season);
        public void Clear() { farm = null; Config = null; }
    }
}
