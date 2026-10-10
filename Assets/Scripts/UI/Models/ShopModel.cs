namespace MagicIsland.UI.Models
{
    public sealed class ShopModel
    {
        public string Balance, BuyLabel, SellLabel, Stock, Feedback;
        public bool CanBuy, CanSell;
        public string[] CropNames;
        public int SelectedIndex;
    }
}
