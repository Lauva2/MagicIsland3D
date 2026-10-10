using TMPro;
using MagicIsland.UI.Models;
using UnityEngine.UI;

namespace MagicIsland.UI.Views
{
    public sealed class InventoryView : UIView
    {
        public Button CloseButton, UseSeedButton;
        public TMP_Text CapacityText;
        public TMP_Text DetailTitleText;
        public TMP_Text DetailText;
        public InventorySlotView[] Slots;
        public void Render(InventoryModel model)
        {
            UseSeedButton.interactable = model.CanUseSeed;
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
