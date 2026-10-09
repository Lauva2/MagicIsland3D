using TMPro;
using MagicIsland.UI.Models;
using MagicIsland.UI.Views;
using UnityEngine.UI;

namespace MagicIsland.Loading
{
    // View only: serialized uGUI references and rendering, no loading or gameplay calls.
    public sealed class MainMenuView : UIView
    {
        public TMP_Text StatusText;
        public Button EnterButton;
        public Button ContinueButton;
        public Button SettingsButton;
        public Button QuitButton;
        public void Render(MainMenuModel model)
        {
            StatusText.text = model.Status;
            EnterButton.interactable = !model.Busy;
            ContinueButton.interactable = !model.Busy && model.HasSave;
            SettingsButton.interactable = !model.Busy;
            QuitButton.interactable = !model.Busy;
        }
    }
}
