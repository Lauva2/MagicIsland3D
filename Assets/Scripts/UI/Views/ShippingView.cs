using MagicIsland.UI.Models;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace MagicIsland.UI.Views
{
    public sealed class ShippingView : UIView
    {
        public InventorySlotView[] BagSlots, BoxSlots;
        public TMP_Text BagCapacityText, ExpectedIncomeText, DetailsText, FeedbackText, PageText;
        public Button CloseButton, MoveOneButton, MoveStackButton, SaveButton, PreviousPageButton, NextPageButton;
        public RectTransform PageControls;
        public void Render(ShippingModel model)
        {
            BagCapacityText.text = model.BagCapacity; ExpectedIncomeText.text = model.ExpectedIncome;
            DetailsText.text = model.Details; FeedbackText.text = model.Feedback;
            MoveOneButton.interactable = model.CanMoveOne; MoveStackButton.interactable = model.CanMoveStack;
            MoveOneButton.GetComponentInChildren<TMP_Text>().text = model.OneLabel; MoveStackButton.GetComponentInChildren<TMP_Text>().text = model.StackLabel;
            PageControls.gameObject.SetActive(model.PageCount > 1); PageText.text = $"第 {model.Page + 1} / {model.PageCount} 页";
            PreviousPageButton.interactable = model.Page > 0; NextPageButton.interactable = model.Page + 1 < model.PageCount;
            for (int i = 0; i < BagSlots.Length; i++)
            {
                int index = model.Page * 30 + i;
                if (index < model.BagSlots.Length) BagSlots[i].Render(model.BagSlots[index]); else BagSlots[i].RenderLocked();
            }
            for (int i = 0; i < BoxSlots.Length; i++) BoxSlots[i].Render(model.BoxSlots[i]);
        }
    }
}
