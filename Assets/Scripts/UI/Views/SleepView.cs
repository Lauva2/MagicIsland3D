using TMPro;
using UnityEngine.UI;
using MagicIsland.UI.Models;
namespace MagicIsland.UI.Views
{
    public sealed class SleepView : UIView
    {
        public TMP_Text MessageText;
        public Button ConfirmButton, CancelButton;
        public void Render(SleepModel model) => MessageText.text = model.Message;
    }
}
