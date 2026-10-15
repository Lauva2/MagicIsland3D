using UnityEngine;
namespace MagicIsland.Interaction
{
    public sealed class OrchardActor : MonoBehaviour
    {
        public GameObject Sapling, Young, Mature, Fruit;
        public bool IsValid => Sapling != null && Young != null && Mature != null && Fruit != null;
        public void Render(int stage, bool ready) { Sapling.SetActive(stage == 0); Young.SetActive(stage == 1); Mature.SetActive(stage == 2); Fruit.SetActive(stage == 2 && ready); }
    }
}
