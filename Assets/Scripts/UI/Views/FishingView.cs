using MagicIsland.UI.Models;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace MagicIsland.UI.Views
{
    public sealed class FishingView : UIView
    {
        [UnityEngine.HideInInspector] public bool CoastLayout;
        public TMP_Text StatusText, HintText, TimerText, FeedbackText;
        public Image ProgressFill;
        public Button CastButton, ReelButton, SaveButton, CloseButton;
        public void Render(FishingModel model)
        {
            StatusText.text = model.Status; HintText.text = model.Hint; TimerText.text = model.Timer; FeedbackText.text = model.Feedback;
            CastButton.interactable = model.CanCast; ReelButton.interactable = model.CanReel; ReelButton.GetComponentInChildren<TMP_Text>().text = model.ReelLabel;
            ProgressFill.fillAmount = model.Progress; ProgressFill.color = model.CanReel && model.Status == "有鱼咬钩！" ? new Color(.90f, .65f, .32f) : new Color(.43f, .65f, .55f);
        }
    }
}
