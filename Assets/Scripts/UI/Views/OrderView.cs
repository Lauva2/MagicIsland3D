using MagicIsland.UI.Models;
using TMPro;
using UnityEngine.UI;
namespace MagicIsland.UI.Views
{
    public sealed class OrderView : UIView
    {
        public TMP_Text DateText, RequestText, StockText, RewardText, StatusText, FeedbackText;
        public Button DeliverButton, CloseButton, HouseUpgradeButton;
        public Image CropIcon;
        public void Render(OrderModel model)
        {
            DateText.text = model.Date; RequestText.text = model.Request; StockText.text = model.Stock;
            RewardText.text = model.Reward; StatusText.text = model.Status; FeedbackText.text = model.Feedback ?? "";
            DeliverButton.interactable = model.CanDeliver; DeliverButton.GetComponentInChildren<TMP_Text>().text = model.ButtonLabel;
            CropIcon.sprite = model.Icon; CropIcon.color = model.IconColor;
        }
    }
}
