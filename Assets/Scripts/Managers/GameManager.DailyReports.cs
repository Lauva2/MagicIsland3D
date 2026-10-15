namespace MagicIsland.Managers
{
    public sealed partial class GameManager
    {
        public DailyReportManager DailyReports { get; private set; }
        private bool reportCommitting;
        private bool sleepSaveFailed;
        public bool CanStartSavedMorning => !sleepSaveFailed && !Saves.SleepSavePending;
        private bool SaveSleepCheckpoint()
        {
            bool saved = Saves.SaveSleep(Farm, Inventory, Players, Economy, Time, Stamina, Weather, Tasks, Orders, Gathering, ToolUpgrades, Storage, NPCs, Relationships, Stories, NPCSchedules, Placements, Housing, Furniture, Animals, DailyReports, Shipping);
            sleepSaveFailed = !saved; Interactions.SetFeedback(saved ? "睡觉结算已保存" : "睡觉保存失败，请在日报重试");
            if (!saved) UnityEngine.Debug.LogWarning("Sleep save failed: " + Saves.LastError);
            return saved;
        }
        private bool DailyReportBusy => reportCommitting || (DailyReports?.State?.Pending ?? false) || UI.HasDailyReport;
        public void StartNewDayFromReport()
        {
            if (State != GameFlowState.Playing || advancingDay || reportCommitting || PauseBusy || sleepSaveFailed || !UI.HasDailyReport || !DailyReports.State.Pending) return;
            reportCommitting = true;
            try
            {
                DailyReports.State.Acknowledge();
                UI.SetDailyReportVisible(false); SetInputEnabled(!ModalOpen);
            }
            finally { reportCommitting = false; if (!DailyReports.State.Pending) SetInputEnabled(!ModalOpen); }
        }
        public void SaveDailyReport()
        {
            if (State != GameFlowState.Playing || advancingDay || reportCommitting || PauseBusy || !sleepSaveFailed || !Saves.SleepSavePending || !UI.HasDailyReport) return;
            reportCommitting = true;
            try { sleepSaveFailed = !Saves.RetrySleepSave(); if (!sleepSaveFailed) DailyReports.SetSaved(); else DailyReports.SetSaveFailed(); UI.RefreshViews(); }
            finally { reportCommitting = false; }
        }
    }
}
