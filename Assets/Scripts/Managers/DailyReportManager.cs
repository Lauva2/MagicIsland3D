using MagicIsland.Reports;
using UnityEngine;
namespace MagicIsland.Managers
{
    public sealed class DailyReportManager : MonoBehaviour
    {
        public DailyReportState State { get; private set; }
        public string Feedback { get; private set; }
        private FarmManager farm;
        private EconomyManager economy;
        public void Initialize(FarmManager farm, EconomyManager economy, SavedDailyReport saved = null)
        {
            Clear(); this.farm = farm; this.economy = economy; State = new DailyReportState(farm.State.Day);
            if (saved != null) State.Restore(saved, farm.State.Cells.Length);
            economy.State.SetLedger(State.RecordMoney); farm.SetHarvestRecorder(State.RecordHarvest);
        }
        public void NextDay(int mature, int eggs, int qualityEggs, MagicIsland.Shipping.ShippingSale[] shipping = null)
        { State.NextDay(farm.State.Day, mature, eggs, qualityEggs, farm.State.Cells.Length, shipping); Feedback = null; }
        public void SetSaveFailed() => Feedback = "睡觉结算已保留，保存失败，请重试本次睡觉存档";
        public void SetSaved() => Feedback = "睡觉结算已保存，开始新的一天吧";
        public void Clear()
        { economy?.State?.SetLedger(null); farm?.SetHarvestRecorder(null); farm = null; economy = null; State = null; Feedback = null; }
        private void OnDestroy() => Clear();
    }
}
