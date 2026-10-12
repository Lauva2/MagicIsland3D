namespace MagicIsland.UI.Models
{
    public sealed class CraftingModel
    {
        public string[] Names, Costs, Results, Labels;
        public bool[] CanCraft;
        public string Feedback;
    }
    public sealed class PlacementModel
    {
        public string Title, Status, Feedback;
        public bool CanConfirm;
    }
}
