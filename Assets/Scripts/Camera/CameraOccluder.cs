using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MagicIsland.CameraSystem
{
    // One component represents a visual group, such as a tree or a building shell.
    [DisallowMultipleComponent]
    public sealed class CameraOccluder : MonoBehaviour
    {
        public Renderer[] Renderers = new Renderer[0];
        public bool AllowFade = true;
        public Material VegetationFadeMaterial;
        private static readonly HashSet<CameraOccluder> active = new HashSet<CameraOccluder>();
        internal static HashSet<CameraOccluder> Active => active;
        private readonly List<Surface> surfaces = new List<Surface>();
        private readonly Dictionary<Material, Material> copies = new Dictionary<Material, Material>();
        public float Opacity { get; private set; } = 1f;
        public int RuntimeMaterialCount => copies.Count;

        private sealed class Surface
        {
            public Renderer Renderer;
            public Material[] Original;
            public ShadowCastingMode Shadows;
        }

        private void OnEnable() => active.Add(this);
        private void OnDisable() { active.Remove(this); Restore(); }
        private void OnDestroy() { active.Remove(this); Restore(); }

        public bool Blocks(Camera camera, Vector3 point, float margin)
        {
            if (!AllowFade || !isActiveAndEnabled || camera == null || Renderers == null) return false;
            Vector3 screen = camera.WorldToViewportPoint(point);
            if (screen.z <= camera.nearClipPlane || screen.z >= camera.farClipPlane
                || screen.x < 0 || screen.x > 1 || screen.y < 0 || screen.y > 1) return false;
            // Viewport rays also work while an orthographic camera is still following the player.
            Ray ray = camera.ViewportPointToRay(screen);
            float distance = Vector3.Dot(point - ray.origin, ray.direction);
            if (distance <= 0) return false;
            foreach (var renderer in Renderers)
            {
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy
                    || (camera.cullingMask & (1 << renderer.gameObject.layer)) == 0
                    || renderer.shadowCastingMode == ShadowCastingMode.ShadowsOnly) continue;
                var bounds = renderer.bounds; bounds.Expand(Mathf.Max(0, margin) * 2);
                if (bounds.IntersectRay(ray, out float hit) && hit < distance - .05f) return true;
            }
            return false;
        }

        internal void Fade(bool blocked, float minimumOpacity, float duration, float deltaTime)
        {
            float target = blocked && AllowFade ? minimumOpacity : 1f;
            float next = Mathf.MoveTowards(Opacity, target, deltaTime * (1f - minimumOpacity) / duration);
            if (next >= 1f) { Restore(); return; }
            if (surfaces.Count == 0) Capture();
            Opacity = next;
            foreach (var pair in copies)
            {
                if (pair.Key == null || pair.Value == null) continue;
                Color color = pair.Key.color; color.a *= Opacity; pair.Value.color = color;
            }
        }

        private void Capture()
        {
            if (Renderers == null) return;
            foreach (var renderer in Renderers)
            {
                if (renderer == null || surfaces.Exists(s => s.Renderer == renderer)) continue;
                Material[] original = renderer.sharedMaterials;
                var faded = (Material[])original.Clone(); bool changed = false;
                for (int i = 0; i < original.Length; i++)
                {
                    Material source = original[i];
                    // Custom shaders must implement their own transparency before opting into this effect.
                    if (source == null || source.shader == null || (source.shader.name != "Standard"&&source.shader.name!="MagicIsland/Vegetation Wind")) continue;
                    bool vegetation=source.shader.name=="MagicIsland/Vegetation Wind";
                    if(vegetation&&(VegetationFadeMaterial==null||VegetationFadeMaterial.shader==null))continue;
                    if (!copies.TryGetValue(source, out Material copy))
                    {
                        copy = new Material(source) { name = source.name + " (Camera Fade)", hideFlags = HideFlags.DontSave };
                        if(vegetation)copy.shader=VegetationFadeMaterial.shader;
                        copy.SetFloat("_Mode", 2); copy.SetOverrideTag("RenderType", "Transparent");
                        copy.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                        copy.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha); copy.SetInt("_ZWrite", 0);
                        copy.DisableKeyword("_ALPHATEST_ON"); copy.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                        copy.EnableKeyword("_ALPHABLEND_ON"); copy.renderQueue = (int)RenderQueue.Transparent;
                        copies.Add(source, copy);
                    }
                    faded[i] = copy; changed = true;
                }
                if (!changed) continue;
                surfaces.Add(new Surface { Renderer = renderer, Original = original, Shadows = renderer.shadowCastingMode });
                renderer.sharedMaterials = faded; renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        public void Restore()
        {
            foreach (var surface in surfaces)
            {
                if (surface.Renderer == null) continue;
                surface.Renderer.sharedMaterials = surface.Original;
                surface.Renderer.shadowCastingMode = surface.Shadows;
            }
            surfaces.Clear();
            foreach (var material in copies.Values)
                if (material != null) { if (Application.isPlaying) Destroy(material); else DestroyImmediate(material); }
            copies.Clear(); Opacity = 1f;
        }
    }
}
