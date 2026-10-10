using MagicIsland.UI.Models;
using TMPro;
using UnityEngine.UI;
namespace MagicIsland.UI.Views
{
    public sealed class StorageView : UIView
    {
        public InventorySlotView[] BagSlots, ChestSlots;
        public TMP_Text BagCapacityText, ChestCapacityText, DetailsText, FeedbackText;
        public Button CloseButton, MoveOneButton, MoveStackButton, SaveButton;
        public void Render(StorageModel model)
        {
            BagCapacityText.text = model.BagCapacity; ChestCapacityText.text = model.ChestCapacity;
            DetailsText.text = model.Details; FeedbackText.text = model.Feedback;
            MoveOneButton.interactable = model.CanMoveOne; MoveStackButton.interactable = model.CanMoveStack;
            MoveOneButton.GetComponentInChildren<TMP_Text>().text = model.OneLabel; MoveStackButton.GetComponentInChildren<TMP_Text>().text = model.StackLabel;
            RenderSlots(BagSlots, model.BagSlots); RenderSlots(ChestSlots, model.ChestSlots);
        }
        private static void RenderSlots(InventorySlotView[] views, InventorySlotModel[] models)
        { for (int i = 0; i < views.Length; i++) { views[i].gameObject.SetActive(i < models.Length); if (i < models.Length) views[i].Render(models[i]); } }
    }
}
