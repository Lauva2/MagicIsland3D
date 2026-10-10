using System;
using MagicIsland.Managers;
using MagicIsland.UI.Models;
using MagicIsland.UI.Views;
using UnityEngine.Events;

namespace MagicIsland.UI.Controllers
{
    public sealed class TaskController : IUIController
    {
        public TaskModel Model { get; } = new TaskModel();
        private readonly TaskView view;
        private readonly TaskManager tasks;
        private readonly UnityAction collapse, claim;
        public TaskController(TaskView view, TaskManager tasks, Action claimReward)
        {
            this.view = view; this.tasks = tasks;
            collapse = () => { Model.Collapsed = !Model.Collapsed; Tick(); };
            claim = () => claimReward();
            view.CollapseButton.onClick.AddListener(collapse); view.ClaimButton.onClick.AddListener(claim);
            Tick();
        }
        public void Tick()
        {
            if (view == null || tasks.State == null) return;
            var state = tasks.State; int index = state.CurrentIndex;
            Model.Finished = state.Finished;
            Model.Header = state.Finished ? "新手目标 · 已完成" : $"新手目标 · {index + 1} / {state.Count}";
            Model.Title = state.Finished ? "岛屿生活开始了" : tasks.Catalog.Tasks[index].Title;
            Model.Description = state.Finished ? "继续照料农田、交易作物，安排自己的岛屿生活。" : tasks.Catalog.Tasks[index].Description;
            Model.Progress = state.Finished ? "全部奖励已领取" : $"进度 {state.GetProgress(index)} / {tasks.Catalog.Tasks[index].Required} · 奖励 {tasks.Catalog.Tasks[index].GoldReward} 金币";
            Model.Feedback = tasks.Feedback;
            Model.CanClaim = state.Ready && GameManager.Instance.State == GameFlowState.Playing
                && !GameManager.Instance.UI.IsInventoryVisible && !GameManager.Instance.UI.HasShop && !GameManager.Instance.UI.HasSleep && !GameManager.Instance.UI.HasOrder && !GameManager.Instance.Tools.Busy;
            view.Render(Model);
        }
        public void Dispose()
        {
            if (view == null) return;
            view.CollapseButton.onClick.RemoveListener(collapse); view.ClaimButton.onClick.RemoveListener(claim);
        }
    }
}
