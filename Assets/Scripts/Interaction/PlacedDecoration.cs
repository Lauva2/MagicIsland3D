using UnityEngine;
namespace MagicIsland.Interaction
{
    public sealed class PlacedDecoration : MonoBehaviour
    {
        public int Id { get; private set; }
        public void Initialize(int id) => Id = id;
    }
}
