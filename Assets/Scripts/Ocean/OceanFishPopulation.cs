using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MagicIsland.Ocean
{
    [Serializable] public sealed class OceanFishPopulation
    {
        public ShallowFishSpecies Species;
        [Range(1,32)] public int Minimum=3;
        [Range(1,32)] public int Maximum=5;
        [Range(1,16)] public int MinimumSchools=6,MaximumSchools=10;
        public OceanFishPopulation(ShallowFishSpecies species,int minimum,int maximum,int minimumSchools=6,int maximumSchools=10)
        {Species=species;Minimum=minimum;Maximum=maximum;MinimumSchools=minimumSchools;MaximumSchools=maximumSchools;}
        public bool IsValid=>(int)Species>=0&&(int)Species<4&&Minimum>=1&&Maximum>=Minimum&&Maximum<=32
            &&MinimumSchools>=1&&MaximumSchools>=MinimumSchools&&MaximumSchools<=16;

        // Editor bake planning only. Independent RNG, stable species order, inclusive endpoints.
        // Validates all ranges before drawing; returns new values without mutating config or gameplay.
        public static FishSchoolPlan[] CreatePlan(OceanFishPopulation[] populations,int seed)
        {
            if(seed<1||populations==null||populations.Length!=4||populations.Any(p=>p==null||!p.IsValid)
                ||populations.Select(p=>p.Species).Distinct().Count()!=4)throw new ArgumentException("Invalid fish population plan");
            var sorted=populations.OrderBy(p=>(int)p.Species).ToArray();var counts=new int[4];
            var groups=new System.Random(seed);var members=new System.Random(unchecked(seed^0x52EA));
            for(int s=0;s<4;s++)counts[s]=groups.Next(sorted[s].MinimumSchools,sorted[s].MaximumSchools+1);
            var result=new List<FishSchoolPlan>();
            for(int round=0;round<counts.Max();round++)for(int s=0;s<4;s++)if(round<counts[s])
                result.Add(new FishSchoolPlan(sorted[s].Species,members.Next(sorted[s].Minimum,sorted[s].Maximum+1)));
            return result.ToArray();
        }
    }
    public readonly struct FishSchoolPlan
    {
        public ShallowFishSpecies Species {get;}
        public int Members {get;}
        public FishSchoolPlan(ShallowFishSpecies species,int members){Species=species;Members=members;}
    }
}
