using MagicIsland.UI.Models;
using TMPro;
using UnityEngine.UI;
namespace MagicIsland.UI.Views
{
    public sealed class GiftView : UIView
    {
        public TMP_Text TitleText, StatusText, GuideText, DetailText, PreviewText, FeedbackText;
        public InventorySlotView[] Slots;
        public Button GiveButton, SaveButton, CloseButton;
        public void Render(GiftModel model)
        {
            TitleText.text = model.Title; StatusText.text = model.Status; GuideText.text = model.Guide;
            DetailText.text = model.Detail; PreviewText.text = model.Preview; FeedbackText.text = model.Feedback;
            GiveButton.interactable = model.CanGive; GiveButton.GetComponentInChildren<TMP_Text>().text = model.ButtonLabel;
            for (int i = 0; i < Slots.Length; i++)
            {
                bool visible = i < model.Slots.Length; Slots[i].gameObject.SetActive(visible);
                if (visible) { Slots[i].Render(model.Slots[i]); Slots[i].SelectButton.interactable = model.Eligible[i]; }
            }
        }
    }
}
