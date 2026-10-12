using MagicIsland.UI.Models;
using TMPro;
using UnityEngine.UI;
namespace MagicIsland.UI.Views
{
    public sealed class HouseUpgradeView : UIView
    {
        public TMP_Text CostText, OwnedText, StatusText, FeedbackText;
        public Button ApplyButton, CloseButton, SaveButton;
        public void Render(HouseUpgradeModel model)
        {
            CostText.text = model.Costs; OwnedText.text = model.Owned; StatusText.text = model.Status; FeedbackText.text = model.Feedback;
            ApplyButton.interactable = model.CanApply; ApplyButton.GetComponentInChildren<TMP_Text>().text = model.ButtonLabel;
        }
    }
}
