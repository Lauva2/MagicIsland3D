using MagicIsland.CameraSystem;
using MagicIsland.Configuration;
using UnityEngine;

namespace MagicIsland.Managers
{
    [DefaultExecutionOrder(100)]
    public sealed class CameraManager : MonoBehaviour
    {
        private CameraController controller;
        private Camera view;
        private bool occlusionEnabled;
        private float minimumOpacity, fadeDuration, margin;
        public void Initialize(CameraController camera, GameConfig config)
        {
            Clear();
            controller = camera;
            view = camera.GetComponent<Camera>();
            minimumOpacity = config.CameraOccludedOpacity;
            fadeDuration = config.CameraFadeDuration;
            margin = config.CameraOcclusionMargin;
            controller.Configure(config.CameraPitch, config.CameraSize);
            controller.SnapToTarget();
        }
        public void SetInputEnabled(bool enabled) { if (controller != null) controller.enabled = enabled; }
        public void SetOcclusionEnabled(bool enabled)
        {
            occlusionEnabled = enabled;
            if (!enabled) RestoreOccluders();
        }
        private void LateUpdate() => UpdateOcclusion(Time.unscaledDeltaTime);
        internal void UpdateOcclusion(float deltaTime)
        {
            if (!occlusionEnabled || view == null || !view.isActiveAndEnabled || controller == null
                || controller.Target == null || !controller.Target.gameObject.activeInHierarchy)
            { RestoreOccluders(); return; }
            Vector3 feet = controller.Target.position;
            foreach (var occluder in CameraOccluder.Active)
            {
                if (occluder == null) continue;
                bool blocked = !occluder.transform.IsChildOf(controller.Target)
                    && (occluder.Blocks(view, feet + Vector3.up * .4f, margin)
                        || occluder.Blocks(view, feet + Vector3.up * .95f, margin)
                        || occluder.Blocks(view, feet + Vector3.up * 1.5f, margin));
                occluder.Fade(blocked, minimumOpacity, fadeDuration, Mathf.Max(0, deltaTime));
            }
            foreach (var crown in LandscapeCrownOccluder.Active)
                if (crown != null) crown.Tick(view, controller.Target, minimumOpacity, fadeDuration, margin, deltaTime);
        }
        public void RestoreOccluders()
        {
            foreach (var occluder in CameraOccluder.Active) if (occluder != null) occluder.Restore();
            foreach (var crown in LandscapeCrownOccluder.Active) if (crown != null) crown.Restore();
        }
        public void Clear() { RestoreOccluders(); occlusionEnabled = false; controller = null; view = null; }
        private void OnDisable() => RestoreOccluders();
        private void OnDestroy() => Clear();
    }
}
