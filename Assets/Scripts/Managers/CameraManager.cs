using MagicIsland.CameraSystem;
using MagicIsland.Configuration;
using UnityEngine;

namespace MagicIsland.Managers
{
    public sealed class CameraManager : MonoBehaviour
    {
        private CameraController controller;
        public void Initialize(CameraController camera, GameConfig config)
        {
            controller = camera;
            controller.Configure(config.CameraPitch, config.CameraSize);
            controller.SnapToTarget();
        }
        public void SetInputEnabled(bool enabled) { if (controller != null) controller.enabled = enabled; }
        public void Clear() => controller = null;
    }
}
