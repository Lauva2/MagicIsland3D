using TMPro;
using MagicIsland.UI.Models;
using UnityEngine.UI;

namespace MagicIsland.UI.Views
{
    public sealed class InventoryView : UIView
    {
        public Button CloseButton, UseSeedButton;
        public Button UseFoodButton, SaveButton;
        public Button PlaceButton;
        public TMP_Text FeedbackText;
        public TMP_Text CapacityText;
        public TMP_Text DetailTitleText;
        public TMP_Text DetailText;
        public InventorySlotView[] Slots;
        public void Render(InventoryModel model)
        {
            UseSeedButton.interactable = model.CanUseSeed;
            UseSeedButton.gameObject.SetActive(!model.IsFood && !model.IsFish && !model.IsPlaceable);
            PlaceButton.gameObject.SetActive(model.IsPlaceable);
            PlaceButton.interactable = model.CanPlace;
            UseFoodButton.gameObject.SetActive(model.IsFood); UseFoodButton.interactable = model.CanUseFood;
            UseFoodButton.GetComponentInChildren<TMP_Text>().text = model.UseFoodLabel ?? "食用一个";
            FeedbackText.text = model.Feedback ?? "";
            CapacityText.text = $"已用 {model.Occupied} / {model.Slots.Length} 格";
            DetailTitleText.text = model.DetailTitle;
            DetailText.text = model.DetailText;
            for (int i = 0; i < Slots.Length; i++)
            {
                bool visible = i < model.Slots.Length;
                Slots[i].gameObject.SetActive(visible);
                if (visible) Slots[i].Render(model.Slots[i]);
            }
        }
    }
}
