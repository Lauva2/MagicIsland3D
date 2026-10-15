using System.Collections;
namespace MagicIsland.Managers
{
    public sealed partial class GameManager
    {
        private bool inventoryUpgradeOpening, inventoryUpgradeCommitting;
        private int inventoryUpgradeGeneration;
        private bool InventoryUpgradeBusy => inventoryUpgradeOpening || inventoryUpgradeCommitting || UI.HasInventoryUpgrade;
        private bool AtInventoryShop => !Housing.Inside && Shop.InRange(Players.Player.transform.position, Configuration.Config.InteractionReach);
        private void OpenInventoryUpgradeFromShop() { if (shopOpening || shopCommitting || !UI.HasShop || !AtInventoryShop) return; CloseShop(); OpenInventoryUpgrade(); }
        public void OpenInventoryUpgrade()
        {
            if (State != GameFlowState.Playing || ModalOpen || Tools.Busy || !AtInventoryShop) return;
            inventoryUpgradeOpening = true; SetInputEnabled(false); StartCoroutine(LoadInventoryUpgrade(++inventoryUpgradeGeneration));
        }
        private IEnumerator LoadInventoryUpgrade(int generation)
        {
            yield return UI.ShowInventoryUpgrade(Inventory, Economy, UpgradeInventory, CloseInventoryUpgrade, SaveInventoryUpgrade);
            if (generation != inventoryUpgradeGeneration) yield break; inventoryUpgradeOpening = false;
            if (State != GameFlowState.Playing || UI.LastError != null || !UI.HasInventoryUpgrade) { CloseInventoryUpgrade(); Interactions.SetFeedback("暂时无法打开背包升级，请重试"); }
        }
        public void UpgradeInventory()
        {
            if (State != GameFlowState.Playing || inventoryUpgradeOpening || inventoryUpgradeCommitting || !UI.HasInventoryUpgrade || Tools.Busy || !AtInventoryShop) return;
            inventoryUpgradeCommitting = true;
            try { Inventory.Upgrade(Economy); UI.RefreshViews(); }
            finally { inventoryUpgradeCommitting = false; }
        }
        public void CloseInventoryUpgrade()
        { if (inventoryUpgradeCommitting) return; inventoryUpgradeGeneration++; inventoryUpgradeOpening = false; UI.CloseInventoryUpgrade(); if (State == GameFlowState.Playing) SetInputEnabled(!ModalOpen); }
        public void SaveInventoryUpgrade()
        {
            Interactions.SetFeedback("进度只在每天睡觉结算后保存");
        }
        private void ClearInventoryUpgradePanel()
        { inventoryUpgradeGeneration++; inventoryUpgradeOpening = inventoryUpgradeCommitting = false; UI.CloseInventoryUpgrade(); }
    }
}
