using MagicIsland.UI.Models;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MagicIsland.UI.Views
{
    public sealed class TaskView : UIView
    {
        public RectTransform Card;
        public GameObject Details;
        public TMP_Text HeaderText, TitleText, DescriptionText, ProgressText, FeedbackText;
        public Button CollapseButton, ClaimButton;
        [Min(52)] public float ExpandedHeight = 260;
        public void Render(TaskModel model)
        {
            HeaderText.text = model.Header; TitleText.text = model.Title; DescriptionText.text = model.Description;
            ProgressText.text = model.Progress; FeedbackText.text = model.Feedback ?? "";
            Details.SetActive(!model.Collapsed);
            Card.sizeDelta = new Vector2(Card.sizeDelta.x, model.Collapsed ? 52 : ExpandedHeight);
            CollapseButton.GetComponentInChildren<TMP_Text>().text = model.Collapsed ? "展开" : "收起";
            ClaimButton.interactable = model.CanClaim;
            ClaimButton.GetComponentInChildren<TMP_Text>().text = model.Finished ? "引导已完成" : model.CanClaim ? "领取奖励" : "完成目标后领取";
        }
    }
}
