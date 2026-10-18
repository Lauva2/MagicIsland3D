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
            if (Group.alpha != model.Alpha) Group.alpha = model.Alpha;
            if (MessageText.text != model.Message) MessageText.text = model.Message;
            if (DetailText.text != model.Detail) DetailText.text = model.Detail;
            var color = model.Success ? new Color(0.44f, 0.66f, 0.45f) : new Color(0.83f, 0.49f, 0.34f);
            if (Accent.color != color) Accent.color = color;
        }
    }
}
