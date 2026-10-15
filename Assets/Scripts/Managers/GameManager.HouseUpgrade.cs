using System.Collections;
using MagicIsland.Interaction;
namespace MagicIsland.Managers
{
    public sealed partial class GameManager
    {
        private bool houseUpgradeOpening, houseUpgradeCommitting;
        private int houseUpgradeGeneration;
        private bool HouseUpgradeBusy => houseUpgradeOpening || houseUpgradeCommitting || UI.HasHouseUpgrade;
        private bool AtHouseOffice => !Housing.Inside && Orders.InRange(Players.Player.transform.position, Configuration.Config.InteractionReach);
        private void OpenHouseUpgradeFromOrders()
        { if (orderOpening || !UI.HasOrder || !AtHouseOffice) return; CloseOrder(); OpenHouseUpgrade(); }
        public void OpenHouseUpgrade()
        {
            if (State != GameFlowState.Playing || ModalOpen || Tools.Busy || !AtHouseOffice) return;
            houseUpgradeOpening = true; SetInputEnabled(false); StartCoroutine(LoadHouseUpgrade(++houseUpgradeGeneration));
        }
        private IEnumerator LoadHouseUpgrade(int generation)
        {
            yield return UI.ShowHouseUpgrade(Housing, Farm, Inventory, Economy, ApplyHouseUpgrade, CloseHouseUpgrade, SaveHouseUpgrade);
            if (generation != houseUpgradeGeneration) yield break;
            houseUpgradeOpening = false;
            if (State != GameFlowState.Playing || UI.LastError != null || !UI.HasHouseUpgrade)
            { CloseHouseUpgrade(); Interactions.SetFeedback("暂时无法打开小屋扩建申请，请重试"); }
        }
        public void CloseHouseUpgrade()
        {
            if (houseUpgradeCommitting) return;
            houseUpgradeGeneration++; houseUpgradeOpening = false; UI.CloseHouseUpgrade();
            if (State == GameFlowState.Playing) SetInputEnabled(!ModalOpen);
        }
        public void ApplyHouseUpgrade()
        {
            if (State != GameFlowState.Playing || !UI.HasHouseUpgrade || houseUpgradeOpening || houseUpgradeCommitting || Tools.Busy || !AtHouseOffice) return;
            houseUpgradeCommitting = true;
            try
            {
                if (Housing.ApplyUpgrade(Farm.State.Day, Inventory, Economy) == HouseUpgradeResult.Success)
                { Interactions.SetFeedback(Housing.UpgradeFeedback); }
                UI.RefreshViews();
            }
            finally { houseUpgradeCommitting = false; }
        }
        public void SaveHouseUpgrade()
        {
            Interactions.SetFeedback("进度只在每天睡觉结算后保存");
        }
    }
}
