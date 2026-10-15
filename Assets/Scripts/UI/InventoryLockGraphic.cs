using UnityEngine;
using UnityEngine.UI;

namespace MagicIsland.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class InventoryLockGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear(); Rect r = rectTransform.rect;
            Quad(helper, r, .2f, .12f, .8f, .55f);
            Quad(helper, r, .3f, .5f, .38f, .82f);
            Quad(helper, r, .62f, .5f, .7f, .82f);
            Quad(helper, r, .3f, .76f, .7f, .84f);
        }
        private void Quad(VertexHelper helper, Rect rect, float left, float bottom, float right, float top)
        {
            int start = helper.currentVertCount;
            helper.AddVert(new Vector3(rect.x + left * rect.width, rect.y + bottom * rect.height), color, Vector2.zero);
            helper.AddVert(new Vector3(rect.x + left * rect.width, rect.y + top * rect.height), color, Vector2.zero);
            helper.AddVert(new Vector3(rect.x + right * rect.width, rect.y + top * rect.height), color, Vector2.zero);
            helper.AddVert(new Vector3(rect.x + right * rect.width, rect.y + bottom * rect.height), color, Vector2.zero);
            helper.AddTriangle(start, start + 1, start + 2); helper.AddTriangle(start + 2, start + 3, start);
        }
    }
}
