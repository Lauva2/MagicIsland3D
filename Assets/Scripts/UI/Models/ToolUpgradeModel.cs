namespace MagicIsland.UI.Models
{
    public sealed class ToolUpgradeModel
    {
        public string Balance, Feedback;
        public string[] Titles = new string[2], Costs = new string[2], Effects = new string[2], Labels = new string[2];
        public bool[] CanUpgrade = new bool[2];
    }
}
