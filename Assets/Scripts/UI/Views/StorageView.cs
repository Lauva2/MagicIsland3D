using MagicIsland.UI.Models;
using TMPro;
using UnityEngine.UI;
using UnityEngine;
using UnityEngine.EventSystems;
namespace MagicIsland.UI.Views
{
    public sealed class StorageView : UIView
    {
        public InventorySlotView[] BagSlots, ChestSlots;
        public TMP_Text BagCapacityText, ChestCapacityText, DetailsText, FeedbackText;
        public Button CloseButton, MoveOneButton, MoveStackButton, SaveButton;
        public Button DepositMatchingButton, PreviousPageButton, NextPageButton;
        public RectTransform PageControls, DragPreview;
        public TMP_Text PageText, DragNameText, DragQuantityText;
        public Image DragIcon;
        [HideInInspector] public bool ShortcutsLayout;
        public void MoveDragPreview(PointerEventData data)
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)DragPreview.parent, data.position, data.pressEventCamera, out var point)) DragPreview.anchoredPosition = point + new Vector2(28, -28);
        }
        public void Render(StorageModel model)
        {
            BagCapacityText.text = model.BagCapacity; ChestCapacityText.text = model.ChestCapacity;
            DetailsText.text = model.Details; FeedbackText.text = model.Feedback;
            MoveOneButton.interactable = model.CanMoveOne; MoveStackButton.interactable = model.CanMoveStack;
            SaveButton.interactable = !model.Dragging; DepositMatchingButton.interactable = !model.Dragging && model.CanDepositMatching;
            DragPreview.gameObject.SetActive(model.Dragging); DragNameText.text = model.Dragging ? model.DragName ?? "" : "";
            DragQuantityText.text = model.Dragging ? model.DragQuantity.ToString() : "";
            DragIcon.sprite = model.DragIcon; DragIcon.color = model.DragColor; DragIcon.enabled = model.Dragging && model.DragIcon != null;
            PageControls.gameObject.SetActive(model.PageCount > 1); PageText.text = $"第 {model.Page + 1} / {model.PageCount} 页";
            PreviousPageButton.interactable = !model.Dragging && model.Page > 0; NextPageButton.interactable = !model.Dragging && model.Page + 1 < model.PageCount;
            MoveOneButton.GetComponentInChildren<TMP_Text>().text = model.OneLabel; MoveStackButton.GetComponentInChildren<TMP_Text>().text = model.StackLabel;
            for (int i = 0; i < BagSlots.Length; i++)
            {
                int index = model.Page * InventoryView.DisplaySlots + i; BagSlots[i].gameObject.SetActive(true);
                if (index < model.BagSlots.Length) BagSlots[i].Render(model.BagSlots[index]); else BagSlots[i].RenderLocked();
            }
            RenderSlots(ChestSlots, model.ChestSlots);
        }
        private static void RenderSlots(InventorySlotView[] views, InventorySlotModel[] models)
        { for (int i = 0; i < views.Length; i++) { views[i].gameObject.SetActive(i < models.Length); if (i < models.Length) views[i].Render(models[i]); } }
    }
}
