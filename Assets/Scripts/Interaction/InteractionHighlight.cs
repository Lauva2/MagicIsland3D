using TMPro;
using UnityEngine;

namespace MagicIsland.Interaction
{
    [DefaultExecutionOrder(120)]
    public sealed class InteractionHighlight : MonoBehaviour
    {
        public LineRenderer Ring;
        public Color ReadyColor = new Color(.63f, .86f, .51f, .85f);
        public Color PrepareColor = new Color(.94f, .75f, .40f, .8f);
        public Color FarColor = new Color(.73f, .8f, .77f, .6f);
        private InteractionTarget target = InteractionTarget.None;
        private Renderer[] renderers;
        public Transform Target => target.Root;
        public void Show(InteractionTarget value)
        {
            if (value.Root != target.Root) renderers = value.Root.GetComponentsInChildren<Renderer>(true);
            target = value; gameObject.SetActive(true); gameObject.layer = value.Root.gameObject.layer;
            Draw();
        }
        public void Hide() { target = InteractionTarget.None; renderers = null; gameObject.SetActive(false); }
        private void LateUpdate() => Draw();
        private void Draw()
        {
            if (!target.Visible) { Hide(); return; }
            Bounds bounds = new Bounds(target.Root.position, Vector3.zero); bool found = false;
            foreach (var renderer in renderers)
            {
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy
                    || renderer is LineRenderer || renderer.GetComponent<TMP_Text>() != null) continue;
                if (!found) { bounds = renderer.bounds; found = true; } else bounds.Encapsulate(renderer.bounds);
            }
            Color color = !target.InRange ? FarColor : target.Ready ? ReadyColor : PrepareColor;
            color.a *= .85f + .15f * Mathf.Sin(Time.unscaledTime * 3f);
            Ring.startColor = Ring.endColor = color;
            float x = Mathf.Clamp(bounds.extents.x + .10f, .35f, 2.1f), z = Mathf.Clamp(bounds.extents.z + .10f, .35f, 2.1f);
            for (int i = 0; i < Ring.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2 / Ring.positionCount;
                Ring.SetPosition(i, new Vector3(bounds.center.x + Mathf.Cos(angle) * x, target.Root.position.y + .045f, bounds.center.z + Mathf.Sin(angle) * z));
            }
        }
    }
}
