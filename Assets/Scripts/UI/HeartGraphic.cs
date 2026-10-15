using UnityEngine;
using UnityEngine.UI;
namespace MagicIsland.UI
{
    // A small vector graphic shared by the UI and the chicken's world feedback mesh.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class HeartGraphic : MaskableGraphic
    {
        public const int Segments = 48;
        public static Vector2 Center => new Vector2(0, 2.5f / 34);
        public static Vector2 Point(float angle)
        { float s = Mathf.Sin(angle); return new Vector2(16 * s * s * s / 32, (13 * Mathf.Cos(angle) - 5 * Mathf.Cos(2 * angle) - 2 * Mathf.Cos(3 * angle) - Mathf.Cos(4 * angle) + 2.5f) / 34); }
        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear(); var rect = rectTransform.rect;
            helper.AddVert(rect.center + Vector2.Scale(Center, rect.size), color, Vector2.zero);
            for (int i = 0; i < Segments; i++) helper.AddVert(rect.center + Vector2.Scale(Point(i * Mathf.PI * 2 / Segments), rect.size), color, Vector2.zero);
            for (int i = 0; i < Segments; i++) helper.AddTriangle(0, i + 1, (i + 1) % Segments + 1);
        }
    }
}
