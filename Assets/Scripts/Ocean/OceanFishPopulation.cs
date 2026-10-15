using System;
using UnityEngine;

namespace MagicIsland.Ocean
{
    [Serializable] public sealed class OceanFishPopulation
    {
        public ShallowFishSpecies Species;
        [Range(1,32)] public int Minimum=3;
        [Range(1,32)] public int Maximum=5;
        public OceanFishPopulation(ShallowFishSpecies species,int minimum,int maximum)
        {Species=species;Minimum=minimum;Maximum=maximum;}
        public bool IsValid=>Minimum>=1&&Maximum>=Minimum&&Maximum<=32;
    }
}
