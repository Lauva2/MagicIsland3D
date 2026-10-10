using MagicIsland.Configuration;
using MagicIsland.Farming;
using MagicIsland.Interaction;
using UnityEngine;

namespace MagicIsland.Managers
{
    public sealed class InteractionManager : MonoBehaviour
    {
        public InteractionController Controller { get; private set; }
        public void Initialize(InteractionController controller, GameConfig config, FarmManager farm, ToolManager tools)
        {
            Controller = controller;
            controller.Reach = config.InteractionReach;
            controller.FarmManager = farm;
            controller.Tools = tools;
        }
        public void SelectTool(FarmTool tool) { if (Controller != null) Controller.SelectTool(tool); }
        public void SetInputEnabled(bool enabled) { if (Controller != null) Controller.enabled = enabled; }
        public void Clear() => Controller = null;
        public void SetFeedback(string message) { if (Controller != null) Controller.SetFeedback(message); }
    }
}
