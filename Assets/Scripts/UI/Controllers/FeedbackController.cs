using MagicIsland.Managers;
using MagicIsland.Tools;
using MagicIsland.UI.Models;
using MagicIsland.UI.Views;
using UnityEngine;
namespace MagicIsland.UI.Controllers
{
    public sealed class FeedbackController : IUIController
    {
        public FeedbackModel Model { get; } = new FeedbackModel();
        private readonly FeedbackView view;
        private readonly ToolManager tools;
        private float remaining;
        public FeedbackController(FeedbackView view, ToolManager tools)
        { this.view = view; this.tools = tools; tools.Feedback += Show; view.Render(Model); }
        private void Show(ToolFeedback feedback)
        {
            Model.Message = feedback.Message; Model.Detail = feedback.Detail; Model.Success = feedback.Success;
            remaining = 2.2f; Model.Alpha = 1; view.Render(Model);
        }
        public void Tick()
        {
            if (view == null || remaining <= 0) return;
            remaining = Mathf.Max(0, remaining - Time.unscaledDeltaTime);
            float alpha = Mathf.Clamp01(remaining / 0.35f);
            if (Model.Alpha == alpha) return;
            Model.Alpha = alpha; view.Render(Model);
        }
        public void Dismiss()
        {
            remaining = 0; Model.Alpha = 0; view.Render(Model);
        }
        public void Dispose() => tools.Feedback -= Show;
    }
}
