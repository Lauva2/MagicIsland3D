using MagicIsland.UI.Models;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace MagicIsland.UI.Views
{
    public sealed class FeedbackView : UIView
    {
        public CanvasGroup Group;
        public TMP_Text MessageText, DetailText;
        public Image Accent;
        public void Render(FeedbackModel model)
        {
            Group.alpha = model.Alpha;
            MessageText.text = model.Message; DetailText.text = model.Detail;
            Accent.color = model.Success ? new Color(0.44f, 0.66f, 0.45f) : new Color(0.83f, 0.49f, 0.34f);
        }
    }
}
