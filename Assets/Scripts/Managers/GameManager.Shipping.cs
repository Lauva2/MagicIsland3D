using System.Collections;
namespace MagicIsland.Managers
{
    public sealed partial class GameManager
    {
        public ShippingManager Shipping { get; private set; }
        private bool shippingOpening, shippingCommitting;
        private int shippingGeneration;
        public void OpenShipping()
        {
            if (State != GameFlowState.Playing || ModalOpen || Tools.Busy || Housing.Inside || !Shipping.InRange(Players.Player.transform.position, Configuration.Config.InteractionReach)) return;
            shippingOpening = true; SetInputEnabled(false); StartCoroutine(LoadShipping(++shippingGeneration));
        }
        private IEnumerator LoadShipping(int generation)
        {
            yield return UI.ShowShipping(Shipping, TransferShipping, CloseShipping, SaveShipping);
            if (generation != shippingGeneration) yield break;
            shippingOpening = false;
            if (State != GameFlowState.Playing || UI.LastError != null || !UI.HasShipping)
            { CloseShipping(); Interactions.SetFeedback("暂时无法打开出货箱，请重试"); }
        }
        public void CloseShipping()
        {
            if (shippingCommitting) return;
            shippingGeneration++; shippingOpening = false; UI.CloseShipping(); if (State == GameFlowState.Playing) SetInputEnabled(!ModalOpen);
        }
        public void TransferShipping(bool deposit, int index, int quantity)
        {
            if (State != GameFlowState.Playing || ConstructionBusy || shippingOpening || shippingCommitting || !UI.HasShipping || Tools.Busy || Housing.Inside
                || !Shipping.InRange(Players.Player.transform.position, Configuration.Config.InteractionReach)) return;
            shippingCommitting = true;
            try { if (Shipping.Transfer(deposit, index, quantity)) Interactions.SetFeedback(Shipping.Feedback); }
            finally { shippingCommitting = false; } UI.RefreshViews();
        }
        public void SaveShipping()
        {
            Interactions.SetFeedback("进度只在每天睡觉结算后保存");
        }
    }
}
