using UnityEngine;
using MagicIsland.Gathering;
namespace MagicIsland.Interaction
{
    public sealed class GatheringNode : MonoBehaviour
    {
        public GameObject WholeVisual, DepletedVisual;
        public int Index { get; private set; } = -1;
        public GatheringArtVisual Art { get; private set; }
        public void Initialize(int index) { Index = index; Art = GetComponent<GatheringArtVisual>(); Art?.Initialize(); }
        public void Refresh(bool depleted)
        {
            if (WholeVisual.activeSelf == depleted) Art?.ResetPose();
            WholeVisual.SetActive(!depleted); DepletedVisual.SetActive(depleted);
        }
    }
}
