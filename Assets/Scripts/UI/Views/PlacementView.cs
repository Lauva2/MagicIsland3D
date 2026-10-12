using MagicIsland.UI.Models;
using TMPro;
using UnityEngine.UI;
namespace MagicIsland.UI.Views
{
    public sealed class PlacementView : UIView
    {
        public TMP_Text TitleText, StatusText, FeedbackText;
        public Button ConfirmButton, RotateButton, CancelButton, SaveButton;
        public void Render(PlacementModel model)
        { TitleText.text = model.Title; StatusText.text = model.Status; FeedbackText.text = model.Feedback; ConfirmButton.interactable = model.CanConfirm; }
    }
}
