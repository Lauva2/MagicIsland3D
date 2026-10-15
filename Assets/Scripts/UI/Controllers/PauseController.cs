using System;
using MagicIsland.Managers;
using MagicIsland.UI.Models;
using MagicIsland.UI.Views;
using UnityEngine.Events;
namespace MagicIsland.UI.Controllers
{
    public sealed class PauseController : IUIController
    {
        private readonly PauseView view;
        private readonly GameManager game;
        private readonly UnityAction resume, settings, menu;
        public PauseModel Model { get; } = new PauseModel();
        public PauseController(PauseView view, GameManager game, Action resume, Action settings, Action menu)
        {
            this.view = view ?? throw new ArgumentNullException(nameof(view)); this.game = game;
            this.resume = () => resume(); this.settings = () => settings(); this.menu = () => menu();
            view.ContinueButton.onClick.AddListener(this.resume); view.SettingsButton.onClick.AddListener(this.settings); view.ReturnMenuButton.onClick.AddListener(this.menu); Tick();
        }
        public void Tick()
        {
            if (view == null) return;
            Model.ConfirmReturn = game.ReturnConfirmation; Model.Title = Model.ConfirmReturn ? "返回主菜单？" : "休息片刻";
            Model.Message = Model.ConfirmReturn ? "返回后，当日尚未睡觉的进度将丢失。\n下次加载从最近一次睡觉存档继续。"
                : "游戏已暂停\n进度仅在每天睡觉结算后保存。";
            Model.ContinueLabel = Model.ConfirmReturn ? "留在岛上" : "继续游戏";
            Model.ReturnLabel = Model.ConfirmReturn ? "确认返回" : "返回主菜单";
            Model.CanSettings = !Model.ConfirmReturn; view.Render(Model);
        }
        public void Dispose()
        { if (view == null) return; view.ContinueButton.onClick.RemoveListener(resume); view.SettingsButton.onClick.RemoveListener(settings); view.ReturnMenuButton.onClick.RemoveListener(menu); }
    }
}
