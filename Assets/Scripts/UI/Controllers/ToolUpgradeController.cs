using System;
using MagicIsland.Farming;
using MagicIsland.Managers;
using MagicIsland.Tools;
using MagicIsland.UI.Models;
using MagicIsland.UI.Views;
using UnityEngine.Events;
namespace MagicIsland.UI.Controllers
{
    public sealed class ToolUpgradeController : IUIController
    {
        private readonly ToolUpgradeView view;
        private readonly ToolUpgradeManager upgrades;
        private readonly UnityAction[] actions;
        private readonly UnityAction close, save;
        public ToolUpgradeModel Model { get; } = new ToolUpgradeModel();
        public ToolUpgradeController(ToolUpgradeView view, ToolUpgradeManager upgrades, Action<FarmTool> upgrade, Action close, Action save)
        {
            this.view = view; this.upgrades = upgrades; this.close = () => close(); this.save = () => save(); actions = new UnityAction[2];
            for (int i = 0; i < 2; i++) { var tool = upgrades.Catalog.Upgrades[i].Tool; actions[i] = () => upgrade(tool); view.UpgradeButtons[i].onClick.AddListener(actions[i]); }
            view.CloseButton.onClick.AddListener(this.close); view.SaveButton.onClick.AddListener(this.save); Tick();
        }
        public void Tick()
        {
            if (view == null || upgrades.State == null) return;
            Model.Balance = "木材 " + upgrades.Inventory.GetCount("wood") + " · 石料 " + upgrades.Inventory.GetCount("stone") + " · 金币 " + upgrades.Economy.Gold;
            Model.Feedback = upgrades.Feedback ?? "在工作台强化工具，轻松采集岛屿材料";
            for (int i = 0; i < 2; i++)
            {
                var config = upgrades.Catalog.Upgrades[i]; bool upgraded = upgrades.State.Level(config.Tool) == 1;
                Model.Titles[i] = config.DisplayName + (upgraded ? " · 强化" : " · 普通");
                Model.Costs[i] = "木材 " + config.Wood + " · 石料 " + config.Stone + " · " + config.Gold + " 金币";
                Model.Effects[i] = config.Tool == FarmTool.Axe ? "砍树 3 次 → 2 次 · 每次 4 体力" : "采石 2 次 → 1 次 · 每次 5 体力";
                var result = upgrades.Check(config.Tool); Model.CanUpgrade[i] = result == UpgradeResult.Success;
                Model.Labels[i] = upgraded ? "已升级" : Model.CanUpgrade[i] ? "升级工具" : ToolUpgradeManager.Explain(result);
            }
            view.Render(Model);
        }
        public void Dispose()
        {
            if (view == null) return;
            for (int i = 0; i < 2; i++) view.UpgradeButtons[i].onClick.RemoveListener(actions[i]);
            view.CloseButton.onClick.RemoveListener(close); view.SaveButton.onClick.RemoveListener(save);
        }
    }
}
