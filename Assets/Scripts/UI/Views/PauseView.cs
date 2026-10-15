using MagicIsland.UI.Models;
using TMPro;
using UnityEngine.UI;
namespace MagicIsland.UI.Views
{
    public sealed class PauseView : UIView
    {
        public TMP_Text TitleText, MessageText;
        public Button ContinueButton, SettingsButton, ReturnMenuButton;
        public void Render(PauseModel model)
        {
            TitleText.text = model.Title; MessageText.text = model.Message;
            ContinueButton.GetComponentInChildren<TMP_Text>().text = model.ContinueLabel;
            ReturnMenuButton.GetComponentInChildren<TMP_Text>().text = model.ReturnLabel;
            SettingsButton.interactable = model.CanSettings;
        }
    }
}
