using System;

namespace MagicIsland.Managers
{
    public sealed partial class GameManager
    {
        private bool CanEditInventory => State == GameFlowState.Playing && !ConstructionBusy && UI.IsInventoryVisible && !consumableCommitting && !Tools.Busy
            && !UI.HasCooking && !cookingOpening && !UI.HasDialogue && !dialogueOpening && !dialogueCommitting
            && !UI.HasGift && !giftOpening && !giftCommitting && !UI.HasFishing && !fishingOpening && !fishingCommitting;
        public bool BeginInventoryDrag(int source) => CanEditInventory && Inventory.BeginDrag(source);
        public void CancelInventoryDrag() => Inventory.CancelDrag();
        public void DropInventoryItem(int target)
        {
            if (!CanEditInventory || !Inventory.IsDragging) return;
            ApplyInventoryEdit(() => Inventory.Drop(target));
        }
        public void SplitInventoryItem(int source)
        {
            if (!CanEditInventory || Inventory.IsDragging) return;
            ApplyInventoryEdit(() => Inventory.Split(source));
        }
        private void ApplyInventoryEdit(Func<bool> edit)
        {
            consumableCommitting = true;
            try { if (edit()) Interactions.SetFeedback(Inventory.EditFeedback); }
            finally { consumableCommitting = false; }
            UI.RefreshViews();
        }
    }
}
