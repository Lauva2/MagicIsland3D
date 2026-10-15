namespace MagicIsland.UI.Models
{
    public sealed class CoopModel
    {
        public string Status, Stock, Costs, Feedback, Guide; public bool CanBuild, CanFeed, CanCollect;
        public string[] Names = new string[2], Affinities = new string[2], Care = new string[2], PetLabels = new string[2];
        public int[] Points = new int[2]; public bool[] CanPet = new bool[2], Hearts = new bool[2];
    }
    public sealed class AnimalShopModel
    { public string Stock, ChickenPrice, FeedPrice, EggPrice, QualityEggPrice, Feedback; public bool CanChicken, CanFeed, CanSell, CanSellQuality; }
}
