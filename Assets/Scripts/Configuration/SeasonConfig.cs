using MagicIsland.CalendarSystem;
using UnityEngine;

namespace MagicIsland.Configuration
{
    [CreateAssetMenu(menuName = "Magic Island/Season Configuration")]
    public sealed class SeasonConfig : ScriptableObject
    {
        public const string Address = "SeasonConfig";
        [Range(1, 365)] public int DaysPerSeason = 28;
        [Range(0, 1)] public float SpringRainChance = 0.3f;
        [Range(0, 1)] public float SummerRainChance = 0.2f;
        [Range(0, 1)] public float AutumnRainChance = 0.4f;
        [Range(0, 1)] public float WinterRainChance = 0.15f;
        public bool IsValid => DaysPerSeason >= 1 && DaysPerSeason <= 365
            && ValidChance(SpringRainChance) && ValidChance(SummerRainChance)
            && ValidChance(AutumnRainChance) && ValidChance(WinterRainChance);
        private static bool ValidChance(float chance) => !float.IsNaN(chance) && !float.IsInfinity(chance) && chance >= 0 && chance <= 1;
        public float RainChance(Season season)
        {
            switch (season)
            {
                case Season.Spring: return SpringRainChance;
                case Season.Summer: return SummerRainChance;
                case Season.Autumn: return AutumnRainChance;
                case Season.Winter: return WinterRainChance;
                default: throw new System.ArgumentOutOfRangeException(nameof(season));
            }
        }
    }
}
