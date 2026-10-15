using System;
namespace MagicIsland.Managers
{
    public sealed partial class GameManager
    {
        private bool CanEditStorage => State == GameFlowState.Playing && !ConstructionBusy && UI.HasStorage && !storageOpening && !storageCommitting && !Tools.Busy
            && Storage.InRange(Players.Player.transform.position, Configuration.Config.InteractionReach);
        public bool BeginStorageDrag(bool deposit, int index) => CanEditStorage && Storage.BeginDrag(deposit, index);
        public void CancelStorageDrag() => Storage.CancelDrag();
        public void DropStorageItem(bool targetBag, int index)
        { if (CanEditStorage && Storage.IsDragging) ApplyStorageShortcut(() => Storage.Drop(targetBag, index)); }
        public void QuickTransferStorage(bool deposit, int index)
        { if (CanEditStorage && !Storage.IsDragging) ApplyStorageShortcut(() => Storage.QuickTransfer(deposit, index)); }
        public void DepositMatchingStorage()
        { if (CanEditStorage && !Storage.IsDragging) ApplyStorageShortcut(Storage.DepositMatching); }
        private void ApplyStorageShortcut(Func<bool> action)
        {
            storageCommitting = true;
            try { if (action()) Interactions.SetFeedback(Storage.Feedback); }
            finally { storageCommitting = false; }
            UI.RefreshViews();
        }
    }
}
