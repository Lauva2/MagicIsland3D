using UnityEngine;
namespace MagicIsland.Ocean
{
    public sealed class OceanHabitatPatch : MonoBehaviour
    {
        public OceanBiome Biome;
        public int Rocks,Grasses,Corals,Sponges,Stars,Shells;
        public int SmallCorals,LargeCorals;
        public int Population=>Rocks+Grasses+Corals+Sponges+Stars+Shells;
    }
}
