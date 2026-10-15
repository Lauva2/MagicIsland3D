using System;
using MagicIsland.Managers;
using MagicIsland.Inventory;
using MagicIsland.UI.Models;
using MagicIsland.UI.Views;
using UnityEngine.Events;
namespace MagicIsland.UI.Controllers
{
    public sealed class InventoryUpgradeController : IUIController
    {
        private readonly InventoryUpgradeView view;
        private readonly InventoryManager inventory;
        private readonly EconomyManager economy;
        private readonly UnityAction upgrade, close, save;
        public InventoryUpgradeModel Model { get; } = new InventoryUpgradeModel();
        public InventoryUpgradeController(InventoryUpgradeView view, InventoryManager inventory, EconomyManager economy, Action upgrade, Action close, Action save)
        {
            this.view = view; this.inventory = inventory; this.economy = economy; this.upgrade = () => upgrade(); this.close = () => close(); this.save = () => save();
            view.UpgradeButton.onClick.AddListener(this.upgrade); view.CloseButton.onClick.AddListener(this.close); view.SaveButton.onClick.AddListener(this.save); Tick();
        }
        public void Tick()
        {
            if (view == null || inventory.UpgradeState == null) return; var s = inventory.UpgradeState; bool full = s.Level == InventoryUpgradeState.MaximumLevel;
            Model.Capacity = "当前容量  " + s.Capacity + " 格 · 升级 " + s.Level + " / " + InventoryUpgradeState.MaximumLevel;
            Model.Next = full ? "已完成全部背包升级" : "下一档容量  " + s.NextCapacity + " 格";
            Model.Cost = full ? "每次升级增加 10 格，原有物品与顺序保留" : "升级费用  " + s.NextCost + " 金币 · 增加 10 格";
            Model.Gold = "持有金币  " + economy.Gold;
            var check = s.Check(inventory.State, economy.State); Model.CanUpgrade = check == InventoryUpgradeResult.Success;
            Model.Button = full ? "已达上限" : Model.CanUpgrade ? "升级背包" : "金币不足";
            Model.Feedback = inventory.UpgradeFeedback ?? "扩容即时生效，费用记入当天经营支出"; view.Render(Model);
        }
        public void Dispose()
        { if (view == null) return; view.UpgradeButton.onClick.RemoveListener(upgrade); view.CloseButton.onClick.RemoveListener(close); view.SaveButton.onClick.RemoveListener(save); }
    }
}
