using MagicIsland.UI.Models;
using TMPro;
using UnityEngine.UI;
namespace MagicIsland.UI.Views
{
    public sealed class InventoryUpgradeView : UIView
    {
        public TMP_Text CapacityText, NextText, CostText, GoldText, FeedbackText;
        public Button UpgradeButton, CloseButton, SaveButton;
        public void Render(InventoryUpgradeModel model)
        { CapacityText.text = model.Capacity; NextText.text = model.Next; CostText.text = model.Cost; GoldText.text = model.Gold; FeedbackText.text = model.Feedback; UpgradeButton.interactable = model.CanUpgrade; UpgradeButton.GetComponentInChildren<TMP_Text>().text = model.Button; }
    }
}
