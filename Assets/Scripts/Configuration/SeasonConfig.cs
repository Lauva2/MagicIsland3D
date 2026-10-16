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
        public SeasonPalette Spring = new SeasonPalette(new Color(.57f,.73f,.39f),new Color(.51f,.70f,.32f),new Color(.39f,.63f,.29f),.55f);
        public SeasonPalette Summer = new SeasonPalette(new Color(.47f,.66f,.32f),new Color(.32f,.58f,.25f),new Color(.32f,.58f,.25f),.35f);
        public SeasonPalette Autumn = new SeasonPalette(new Color(.69f,.63f,.36f),new Color(.79f,.46f,.24f),new Color(.40f,.57f,.32f),.88f);
        public SeasonPalette Winter = new SeasonPalette(new Color(.58f,.63f,.54f),new Color(.50f,.57f,.51f),new Color(.39f,.52f,.43f),.82f);
        public bool IsValid => DaysPerSeason >= 1 && DaysPerSeason <= 365
            && ValidChance(SpringRainChance) && ValidChance(SummerRainChance)
            && ValidChance(AutumnRainChance) && ValidChance(WinterRainChance)
            && Spring != null && Spring.IsValid && Summer != null && Summer.IsValid
            && Autumn != null && Autumn.IsValid && Winter != null && Winter.IsValid;
        public SeasonPalette Palette(Season season)
        {
            switch(season)
            {
                case Season.Spring: return Spring;
                case Season.Summer: return Summer;
                case Season.Autumn: return Autumn;
                case Season.Winter: return Winter;
                default: throw new System.ArgumentOutOfRangeException(nameof(season));
            }
        }
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

    [System.Serializable]
    public sealed class SeasonPalette
    {
        public Color Grass, Foliage, Evergreen;
        [Range(0,1)] public float Strength;
        public SeasonPalette(Color grass,Color foliage,Color evergreen,float strength)
        { Grass=grass; Foliage=foliage; Evergreen=evergreen; Strength=strength; }
        public bool IsValid => ValidColor(Grass)&&ValidColor(Foliage)&&ValidColor(Evergreen)
            && !float.IsNaN(Strength)&&!float.IsInfinity(Strength)&&Strength>=0&&Strength<=1;
        private static bool ValidColor(Color c)=>Valid(c.r)&&Valid(c.g)&&Valid(c.b)&&Valid(c.a);
        private static bool Valid(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v)&&v>=0&&v<=1;
    }
}
