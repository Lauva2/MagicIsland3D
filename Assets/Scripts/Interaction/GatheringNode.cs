using UnityEngine;
namespace MagicIsland.Interaction
{
    public sealed class GatheringNode : MonoBehaviour
    {
        public GameObject WholeVisual, DepletedVisual;
        public int Index { get; private set; } = -1;
        public void Initialize(int index) => Index = index;
        public void Refresh(bool depleted) { WholeVisual.SetActive(!depleted); DepletedVisual.SetActive(depleted); }
    }
}
