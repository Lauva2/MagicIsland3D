using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MagicIsland.CameraSystem
{
    // One owner per merged renderer. CameraManager drives this component, without another clock.
    [DisallowMultipleComponent]
    public sealed class LandscapeCrownOccluder : MonoBehaviour
    {
        public const int MaximumCrowns = 32;
        [Serializable] public struct Crown { public float Phase; public Bounds Bounds; }
        public Renderer Surface;
        public Crown[] Crowns = Array.Empty<Crown>();
        public bool AllowFade = true;
        [HideInInspector] public int ArtVersion;
        static readonly HashSet<LandscapeCrownOccluder> active = new HashSet<LandscapeCrownOccluder>();
        internal static HashSet<LandscapeCrownOccluder> Active => active;
        static readonly int DataId = Shader.PropertyToID("_LandscapeCrownFades");
        static readonly int CountId = Shader.PropertyToID("_LandscapeCrownFadeCount");
        readonly Vector4[] data = new Vector4[MaximumCrowns];
        MaterialPropertyBlock original, working;
        bool captured;
        public float Opacity(int index) => captured ? data[index].y : 1f;
        void OnEnable() { Restore(); active.Add(this); }
        void OnDisable() { active.Remove(this); Restore(); }
        void OnDestroy() { active.Remove(this); Restore(); }

        internal bool Blocks(int index, Camera camera, Vector3 point, float margin)
        {
            if (!AllowFade || !isActiveAndEnabled || Surface == null || !Surface.enabled
                || !Surface.gameObject.activeInHierarchy || Surface.shadowCastingMode == ShadowCastingMode.ShadowsOnly
                || (camera.cullingMask & (1 << Surface.gameObject.layer)) == 0) return false;
            var viewport = camera.WorldToViewportPoint(point);
            if (viewport.z <= camera.nearClipPlane || viewport.z >= camera.farClipPlane
                || viewport.x < 0 || viewport.x > 1 || viewport.y < 0 || viewport.y > 1) return false;
            var ray = camera.ViewportPointToRay(viewport);
            var origin = Surface.transform.InverseTransformPoint(ray.origin);
            var delta = Surface.transform.InverseTransformPoint(point) - origin;
            float distance = delta.magnitude;
            var scale = Surface.transform.lossyScale;
            float minimumScale = Mathf.Min(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            if (distance < .001f || minimumScale < .0001f) return false;
            var bounds = Crowns[index].Bounds;
            bounds.Expand(2 * Mathf.Max(0, margin) / minimumScale);
            return bounds.IntersectRay(new Ray(origin, delta / distance), out float hit)
                && hit < distance - .05f / minimumScale;
        }

        // Captures the instance MPB on first use; never edits shared AA material or mesh data.
        internal void Tick(Camera camera, Transform target, float minimum, float duration, float margin, float deltaTime)
        {
            if (Surface == null || Crowns == null || Crowns.Length == 0 || Crowns.Length > MaximumCrowns)
            { Restore(); return; }
            bool changed = false, faded = false;
            var feet = target.position;
            for (int i = 0; i < Crowns.Length; i++)
            {
                bool blocked = !transform.IsChildOf(target)
                    && (Blocks(i, camera, feet + Vector3.up * .4f, margin)
                        || Blocks(i, camera, feet + Vector3.up * .95f, margin)
                        || Blocks(i, camera, feet + Vector3.up * 1.5f, margin));
                float before = captured ? data[i].y : 1f;
                float next = Mathf.MoveTowards(before, blocked ? minimum : 1f,
                    Mathf.Max(0, deltaTime) * (1 - minimum) / Mathf.Max(.001f, duration));
                if (next < 1 && !captured) Capture();
                data[i] = new Vector4(Crowns[i].Phase, next, 0, 0);
                changed |= before != next; faded |= next < 1;
            }
            if (!faded) { Restore(); return; }
            if (!changed) return;
            Surface.GetPropertyBlock(working);
            working.SetFloat(CountId, Crowns.Length);
            working.SetVectorArray(DataId, data);
            Surface.SetPropertyBlock(working);
        }
        void Capture()
        {
            if (original == null) { original = new MaterialPropertyBlock(); working = new MaterialPropertyBlock(); }
            Surface.GetPropertyBlock(original);
            for (int i = 0; i < MaximumCrowns; i++) data[i] = new Vector4(i < Crowns.Length ? Crowns[i].Phase : -1, 1, 0, 0);
            captured = true;
        }
        public void Restore()
        {
            if (captured && Surface != null) Surface.SetPropertyBlock(original.isEmpty ? null : original);
            captured = false;
        }
    }
}
