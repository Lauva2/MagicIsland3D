using TMPro;
using MagicIsland.UI.Models;
using UnityEngine.UI;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MagicIsland.UI.Views
{
    public sealed class InventoryView : UIView
    {
        public const int Columns = 10, Rows = 3, DisplaySlots = Columns * Rows;
        [HideInInspector] public bool ThreeRowLayout;
        public RectTransform PageControls;
        public Button PreviousPageButton, NextPageButton;
        public TMP_Text PageText;
        public Button CloseButton, UseSeedButton;
        public Button UseFoodButton, SaveButton;
        public Button PlaceButton, SortButton;
        public TMP_Text FeedbackText;
        public TMP_Text CapacityText;
        public TMP_Text DetailTitleText;
        public TMP_Text DetailText;
        public InventorySlotView[] Slots;
        public RectTransform DragPreview;
        public Image DragIcon;
        public TMP_Text DragNameText, DragQuantityText;
        public void MoveDragPreview(PointerEventData data)
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)DragPreview.parent, data.position, data.pressEventCamera, out var point))
                DragPreview.anchoredPosition = point + new Vector2(28, -28);
        }
        public void Render(InventoryModel model)
        {
            DragPreview.gameObject.SetActive(model.Dragging);
            DragNameText.text = model.Dragging ? model.DragName ?? "" : "";
            DragQuantityText.text = model.Dragging ? model.DragQuantity.ToString() : "";
            DragIcon.sprite = model.DragIcon; DragIcon.color = model.DragColor; DragIcon.enabled = model.Dragging && model.DragIcon != null;
            SaveButton.interactable = !model.Dragging;
            SortButton.interactable = model.CanSort;
            UseSeedButton.interactable = model.CanUseSeed;
            UseSeedButton.gameObject.SetActive(!model.IsFood && !model.IsFish && !model.IsPlaceable && !model.IsAnimalProduct && !model.IsSupply);
            PlaceButton.gameObject.SetActive(model.IsPlaceable);
            PlaceButton.interactable = model.CanPlace;
            UseFoodButton.gameObject.SetActive(model.IsFood); UseFoodButton.interactable = model.CanUseFood;
            UseFoodButton.GetComponentInChildren<TMP_Text>().text = model.UseFoodLabel ?? "食用一个";
            FeedbackText.text = model.Feedback ?? "";
            int offset = model.Page * DisplaySlots;
            int unlocked = Mathf.Clamp(model.Slots.Length - offset, 0, DisplaySlots);
            CapacityText.text = $"已用 {model.Occupied} / {model.Slots.Length} 格 · " + (model.PageCount > 1 ? "本页解锁 " : "已解锁 ") + unlocked + " / 30";
            PageControls.gameObject.SetActive(model.PageCount > 1);
            PageText.text = $"第 {model.Page + 1} / {model.PageCount} 页";
            PreviousPageButton.interactable = !model.Dragging && model.Page > 0;
            NextPageButton.interactable = !model.Dragging && model.Page + 1 < model.PageCount;
            DetailTitleText.text = model.DetailTitle;
            DetailText.text = model.DetailText;
            for (int i = 0; i < Slots.Length; i++)
            {
                bool visible = i < DisplaySlots;
                Slots[i].gameObject.SetActive(visible);
                if (!visible) continue;
                if (i + offset < model.Slots.Length) Slots[i].Render(model.Slots[i + offset]);
                else Slots[i].RenderLocked();
            }
        }
    }
}
