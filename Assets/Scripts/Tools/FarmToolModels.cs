using System.Linq;
using UnityEngine;

namespace MagicIsland.Tools
{
    // Loaded with FarmActionEffects. ToolPresentation owns the instance under the hand grip.
    public sealed class FarmToolModels : MonoBehaviour
    {
        public Transform Hoe, WateringCan, Axe, Pickaxe;
        public bool IsValid
        {
            get
            {
                var tools = new[] { Hoe, WateringCan, Axe, Pickaxe };
                return tools.All(t => t != null && t != transform && t.IsChildOf(transform) && ValidMesh(t))
                    && tools.Distinct().Count() == tools.Length
                    && tools.All(a => tools.All(b => a == b || !a.IsChildOf(b)))
                    && GetComponentsInChildren<Collider>(true).Length == 0;
            }
        }

        private static bool ValidMesh(Transform tool) => tool.GetComponentsInChildren<MeshFilter>(true)
            .Any(f => f.sharedMesh != null && f.GetComponent<MeshRenderer>() != null
                && f.GetComponent<MeshRenderer>().sharedMaterial != null);
    }
}
