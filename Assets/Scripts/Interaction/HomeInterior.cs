using UnityEngine;
namespace MagicIsland.Interaction
{
    public sealed class HomeInterior : MonoBehaviour
    {
        public Transform SpawnPoint;
        public SleepBed Bed;
        public HomePortal Exit;
        public GameObject[] OriginalWalls;
        public Transform[] Windows;
        public Transform Skirting;
    }
}
