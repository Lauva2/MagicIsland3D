using System.Collections;
namespace MagicIsland.Managers
{
    public sealed partial class GameManager
    {
        private bool pauseOpening;
        private int pauseGeneration, settingsGeneration;
        public bool ReturnConfirmation { get; private set; }
        private bool PauseBusy => pauseOpening || UI.HasPause || settingsOpening || UI.HasSettings;
        public void OpenPause() => ShowPause(false);
        private void ShowPause(bool confirmReturn)
        {
            if (State != GameFlowState.Playing || pauseOpening || UI.HasPause || settingsOpening || UI.HasSettings || AnyGameplayCommit) return;
            if (!confirmReturn && ModalOpen) return;
            // The report's return button may open the confirmation above the report.
            if (confirmReturn && ModalOpen && !DailyReportBusy) return;
            ReturnConfirmation = confirmReturn; pauseOpening = true; SetInputEnabled(false); StartCoroutine(LoadPause(++pauseGeneration));
        }
        private IEnumerator LoadPause(int generation)
        {
            yield return UI.ShowPause(this, ResumeFromPause, OpenPauseSettings, ReturnToMainMenu);
            if (generation != pauseGeneration) yield break;
            pauseOpening = false;
            if (State != GameFlowState.Playing || UI.LastError != null || !UI.HasPause)
            { ClosePause(); Interactions.SetFeedback("暂时无法打开暂停菜单，请重试"); }
        }
        public void ResumeFromPause()
        {
            if (State != GameFlowState.Playing || !UI.HasPause || UI.HasSettings || settingsOpening) return;
            if (ReturnConfirmation) { ReturnConfirmation = false; UI.RefreshViews(); return; }
            ClosePause();
        }
        private void ClosePause()
        {
            pauseGeneration++; pauseOpening = false; ReturnConfirmation = false; UI.ClosePause();
            if (State == GameFlowState.Playing) SetInputEnabled(!ModalOpen);
        }
        public void OpenPauseSettings()
        { if (State == GameFlowState.Playing && UI.HasPause && !ReturnConfirmation && !settingsOpening && !UI.HasSettings) StartCoroutine(LoadSettings()); }
        private void CloseSettings()
        {
            settingsGeneration++; settingsOpening = false; Settings.Flush(); UI.CloseSettings();
            if (State == GameFlowState.Playing) SetInputEnabled(!ModalOpen);
        }
        private bool AnyGameplayCommit => shippingCommitting || inventoryUpgradeCommitting || reportCommitting || orderCommitting || animalCommitting || advancingDay
            || houseUpgradeCommitting || craftingCommitting || placementCommitting || fishingCommitting || shopCommitting || giftCommitting || dialogueCommitting || storageCommitting || consumableCommitting;
    }
}
