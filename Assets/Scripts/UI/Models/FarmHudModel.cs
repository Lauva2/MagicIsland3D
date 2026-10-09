using MagicIsland.Farming;

namespace MagicIsland.UI.Models
{
    public sealed class FarmHudModel
    {
        public int Day;
        public long Seeds;
        public long Harvest;
        public FarmTool Tool;
        public string Hint;
        public string Feedback;
    }
}
