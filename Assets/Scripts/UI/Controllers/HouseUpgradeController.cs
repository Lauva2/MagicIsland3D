using System;
using MagicIsland.Managers;
using MagicIsland.Interaction;
using MagicIsland.UI.Models;
using MagicIsland.UI.Views;
using UnityEngine.Events;
namespace MagicIsland.UI.Controllers
{
    public sealed class HouseUpgradeController : IUIController
    {
        public HouseUpgradeModel Model { get; } = new HouseUpgradeModel();
        private readonly HouseUpgradeView view;
        private readonly HousingManager housing;
        private readonly FarmManager farm;
        private readonly InventoryManager inventory;
        private readonly EconomyManager economy;
        private readonly UnityAction apply, close, save;
        public HouseUpgradeController(HouseUpgradeView view, HousingManager housing, FarmManager farm, InventoryManager inventory, EconomyManager economy, Action apply, Action close, Action save)
        {
            this.view = view; this.housing = housing; this.farm = farm; this.inventory = inventory; this.economy = economy;
            this.apply = () => apply(); this.close = () => close(); this.save = () => save();
            view.ApplyButton.onClick.AddListener(this.apply); view.CloseButton.onClick.AddListener(this.close); view.SaveButton.onClick.AddListener(this.save); Tick();
        }
        public void Tick()
        {
            if (view == null || housing.Upgrade == null) return;
            var state = housing.Upgrade; var definition = state.Definition;
            var result = state.Check(farm.State.Day, inventory.State, economy.State);
            Model.Costs = "木材 × " + definition.Wood + "    石料 × " + definition.Stone + "    金币 " + definition.Gold;
            Model.Owned = "背包持有：木材 " + inventory.GetCount("wood") + " / 石料 " + inventory.GetCount("stone") + "\n当前金币 " + economy.Gold;
            Model.Status = state.Level == 1 ? "舒适小屋 · 已完工" : state.Building ? "施工中 · 还需 " + (state.CompletionDay - farm.State.Day) + " 天"
                : "初始小屋 → 舒适小屋 · 施工 " + definition.Days + " 天";
            Model.CanApply = result == HouseUpgradeResult.Success;
            Model.ButtonLabel = state.Level == 1 ? "已完成扩建" : state.Building ? "正在施工" : "确认支付并申请";
            var feedback = housing.UpgradeFeedback;
            if (!state.Building && state.Level == 0 && (feedback == null || feedback == HousingManager.ExplainUpgrade(HouseUpgradeResult.InsufficientMaterials)
                || feedback == HousingManager.ExplainUpgrade(HouseUpgradeResult.InsufficientGold) || feedback == HousingManager.ExplainUpgrade(HouseUpgradeResult.InvalidDay)))
                feedback = result == HouseUpgradeResult.Success ? "材料和金币已备齐，可以申请扩建" : HousingManager.ExplainUpgrade(result);
            Model.Feedback = feedback ?? HousingManager.ExplainUpgrade(result); view.Render(Model);
        }
        public void Dispose()
        {
            if (view == null) return; view.ApplyButton.onClick.RemoveListener(apply); view.CloseButton.onClick.RemoveListener(close); view.SaveButton.onClick.RemoveListener(save);
        }
    }
}
