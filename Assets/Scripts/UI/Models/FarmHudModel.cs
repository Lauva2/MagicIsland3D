using MagicIsland.Farming;

namespace MagicIsland.UI.Models
{
    public sealed class FarmHudModel
    {
        public int Day;
        public string Date;
        public string SeedName;
        public int Stamina, MaxStamina;
        public string Time;
        public string Weather;
        public long Seeds;
        public long Harvest;
        public long Wood, Stone;
        public long Gold;
        public FarmTool Tool;
        public string Hint;
        public string Feedback;
        public string Location;
        public bool Inside;
    }
}
