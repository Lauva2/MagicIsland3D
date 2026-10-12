using TMPro;
using MagicIsland.UI.Models;
using UnityEngine.UI;
namespace MagicIsland.UI.Views
{
    public sealed class ShopView : UIView
    {
        public Button CloseButton, BuyButton, SellButton, SaveButton;
        public Button[] CropButtons;
        public TMP_Text BalanceText, BuyText, SellText, StockText, FeedbackText;
        public void Render(ShopModel model)
        {
            for (int i = 0; i < CropButtons.Length; i++)
            {
                bool visible = i < model.CropNames.Length; CropButtons[i].gameObject.SetActive(visible);
                if (!visible) continue;
                CropButtons[i].GetComponentInChildren<TMP_Text>().text = model.CropNames[i];
                CropButtons[i].GetComponent<Image>().color = i == model.SelectedIndex ? new UnityEngine.Color(0.7f, 0.81f, 0.62f) : new UnityEngine.Color(0.92f, 0.89f, 0.8f);
            }
            BalanceText.text = model.Balance; BuyText.text = model.BuyLabel; SellText.text = model.SellLabel;
            StockText.text = model.Stock; FeedbackText.text = model.Feedback;
            BuyButton.interactable = model.CanBuy; SellButton.interactable = model.CanSell;
        }
    }
}
