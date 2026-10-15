using MagicIsland.UI.Models;
using TMPro;
using UnityEngine.UI;
namespace MagicIsland.UI.Views
{
    public sealed class AnimalShopView : UIView
    {
        public TMP_Text StockText, ChickenPriceText, FeedPriceText, EggPriceText, QualityEggPriceText, FeedbackText;
        public Button ChickenButton, FeedButton, SellButton, QualitySellButton, CloseButton, SaveButton;
        public void Render(AnimalShopModel model)
        { StockText.text = model.Stock; ChickenPriceText.text = model.ChickenPrice; FeedPriceText.text = model.FeedPrice; EggPriceText.text = model.EggPrice; QualityEggPriceText.text = model.QualityEggPrice; QualitySellButton.interactable = model.CanSellQuality; FeedbackText.text = model.Feedback; ChickenButton.interactable = model.CanChicken; FeedButton.interactable = model.CanFeed; SellButton.interactable = model.CanSell; }
    }
}
