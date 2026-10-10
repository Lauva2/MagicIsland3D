using MagicIsland.Configuration;
using MagicIsland.Tasks;
using UnityEngine;

namespace MagicIsland.Managers
{
    public sealed class TaskManager : MonoBehaviour
    {
        public TaskState State { get; private set; }
        public TaskCatalog Catalog { get; private set; }
        public string Feedback { get; private set; }
        private FarmManager farm;
        private ShopManager shop;
        private EconomyManager economy;
        public void Initialize(TaskCatalog catalog, FarmManager farm, ShopManager shop, EconomyManager economy, TaskProgress[] saved = null)
        {
            Clear();
            if (catalog == null || !catalog.IsValid) throw new System.ArgumentException("Invalid task catalog");
            var state = new TaskState(catalog.Definitions());
            if (saved != null) state.Restore(saved);
            Catalog = catalog; State = state; this.farm = farm; this.shop = shop; this.economy = economy;
            farm.ActionSucceeded += Record; shop.CropSold += Sold;
        }
        private void Record(TaskAction action) { if (State.Record(action)) Feedback = null; }
        private void Sold(int quantity) { if (State.Record(TaskAction.Sell, quantity)) Feedback = null; }
        public void RecordSleep() => Record(TaskAction.Sleep);
        public bool ClaimReward()
        {
            int index = State.CurrentIndex;
            TaskClaimResult result = State.TryClaim(economy.State);
            Feedback = result == TaskClaimResult.Success ? "已领取 " + Catalog.Tasks[index].GoldReward + " 金币"
                : result == TaskClaimResult.GoldLimit ? "金币已达上限，暂时无法领取"
                : result == TaskClaimResult.Incomplete ? "目标尚未完成"
                : result == TaskClaimResult.Finished ? "全部新手任务已完成" : "正在领取，请稍候";
            return result == TaskClaimResult.Success;
        }
        public void SetSaveFailed() => Feedback = "奖励已领取，保存失败，请重试保存";
        public void Clear()
        {
            if (farm != null) farm.ActionSucceeded -= Record;
            if (shop != null) shop.CropSold -= Sold;
            State = null; Catalog = null; Feedback = null; farm = null; shop = null; economy = null;
        }
        private void OnDestroy() => Clear();
    }
}
