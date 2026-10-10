using System;
using System.Collections.Generic;

namespace MagicIsland.CalendarSystem
{
    public enum Season { Spring, Summer, Autumn, Winter }
    [Flags]
    public enum SeasonMask { None = 0, Spring = 1, Summer = 2, Autumn = 4, Winter = 8, All = 15 }

    public readonly struct CalendarDate
    {
        public int Year { get; }
        public Season Season { get; }
        public int Day { get; }
        public CalendarDate(int totalDay, int daysPerSeason)
        {
            if (totalDay < 1 || totalDay == int.MaxValue || daysPerSeason < 1 || daysPerSeason > 365)
                throw new ArgumentOutOfRangeException(nameof(totalDay));
            int elapsed = totalDay - 1;
            Year = elapsed / (daysPerSeason * 4) + 1;
            Season = (Season)((elapsed / daysPerSeason) % 4);
            Day = elapsed % daysPerSeason + 1;
        }
        public string DisplayName => $"第 {Year} 年 {Seasons.Name(Season)} {Day} 日";
    }

    public static class Seasons
    {
        public static bool ValidMask(SeasonMask mask) => mask != SeasonMask.None && (mask & ~SeasonMask.All) == 0;
        public static bool Includes(SeasonMask mask, Season season)
        {
            if (!ValidMask(mask) || (int)season < 0 || (int)season > 3) return false;
            return (mask & (SeasonMask)(1 << (int)season)) != 0;
        }
        public static string Name(Season season)
        {
            switch (season)
            {
                case Season.Spring: return "春季";
                case Season.Summer: return "夏季";
                case Season.Autumn: return "秋季";
                case Season.Winter: return "冬季";
                default: throw new ArgumentOutOfRangeException(nameof(season));
            }
        }
        public static string Describe(SeasonMask mask)
        {
            if (!ValidMask(mask)) throw new ArgumentException("Invalid planting seasons");
            if (mask == SeasonMask.All) return "四季";
            var names = new List<string>();
            for (int i = 0; i < 4; i++) if (Includes(mask, (Season)i)) names.Add(Name((Season)i));
            return string.Join("、", names);
        }
    }
}
