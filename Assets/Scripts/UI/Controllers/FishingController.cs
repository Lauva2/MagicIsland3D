using System;
using MagicIsland.FishingSystem;
using MagicIsland.Managers;
using MagicIsland.UI.Models;
using MagicIsland.UI.Views;
using UnityEngine.Events;
namespace MagicIsland.UI.Controllers
{
    public sealed class FishingController : IUIController
    {
        private readonly FishingView view;
        private readonly FishingManager fishing;
        private readonly UnityAction cast, reel, save, close;
        public FishingModel Model { get; } = new FishingModel();
        public FishingController(FishingView view, FishingManager fishing, Action cast, Action reel, Action save, Action close)
        {
            this.view = view; this.fishing = fishing; this.cast = () => cast(); this.reel = () => reel(); this.save = () => save(); this.close = () => close();
            view.CastButton.onClick.AddListener(this.cast); view.ReelButton.onClick.AddListener(this.reel); view.SaveButton.onClick.AddListener(this.save); view.CloseButton.onClick.AddListener(this.close); Tick();
        }
        public void Tick()
        {
            if (view == null || fishing.State == null) return;
            var state = fishing.State; bool bite = state.Phase == FishingPhase.Bite;
            Model.Status = bite ? "有鱼咬钩！" : state.Phase == FishingPhase.Waiting ? "等待鱼儿靠近" : state.Phase == FishingPhase.Caught ? "鱼已放入背包" : state.Phase == FishingPhase.Missed ? "这次没有钓到鱼" : "准备垂钓";
            Model.Hint = "成功捕获消耗 " + state.StaminaCost + " 体力 · 失败与取消不消耗\n咬钩后 " + state.BiteWindow.ToString("0.#") + " 秒内收竿";
            Model.Timer = bite ? "收竿时间 " + state.Remaining.ToString("0.0") + " 秒" : state.Active ? "观察浮漂，等咬钩提示" : "体力 " + fishing.Stamina.State.Current + " / " + fishing.Stamina.State.Maximum;
            Model.Progress = state.Active ? (float)(state.Remaining / (bite ? state.BiteWindow : state.WaitDuration)) : 0;
            Model.CanCast = fishing.CheckCast() == FishingResult.Success; Model.CanReel = state.Active;
            Model.ReelLabel = bite ? "立即收竿" : state.Active ? "提前收竿会失败" : "收竿";
            Model.Feedback = fishing.Feedback ?? "走近海岸，用鱼竿开始今天的垂钓"; view.Render(Model);
        }
        public void Dispose()
        { if (view == null) return; view.CastButton.onClick.RemoveListener(cast); view.ReelButton.onClick.RemoveListener(reel); view.SaveButton.onClick.RemoveListener(save); view.CloseButton.onClick.RemoveListener(close); }
    }
}
