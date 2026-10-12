using MagicIsland.UI.Models;
using TMPro;
namespace MagicIsland.UI.Views
{
    public sealed class SceneTransitionView : UIView
    {
        public TMP_Text MessageText;
        public void Render(SceneTransitionModel model) => MessageText.text = model.Message;
    }
}
