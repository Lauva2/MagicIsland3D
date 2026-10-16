using System;
using System.Linq;
using System.Collections.Generic;
using MagicIsland.Ocean;

class VerifyOceanFishPopulation
{
    static int checks;
    static void Check(bool value,string why){checks++;if(!value)throw new Exception(why);}
    static void Reject(Action action){bool rejected=false;try{action();}catch(ArgumentException){rejected=true;}Check(rejected,"Invalid plan rejected");}
    public static int Main()
    {
        var ranges=new[]{new OceanFishPopulation(ShallowFishSpecies.SilverMinnow,6,10),new OceanFishPopulation(ShallowFishSpecies.Clownfish,3,5),new OceanFishPopulation(ShallowFishSpecies.Butterflyfish,3,5),new OceanFishPopulation(ShallowFishSpecies.SpottedRay,1,2,4,6)};
        var totals=new HashSet<int>();var observed=new HashSet<int>[4];for(int s=0;s<4;s++)observed[s]=new HashSet<int>();
        for(int seed=1;seed<=1000;seed++)
        {
            var plan=OceanFishPopulation.CreatePlan(ranges,seed);var repeat=OceanFishPopulation.CreatePlan(ranges.Reverse().ToArray(),seed);
            Check(plan.Select(p=>p.Species+":"+p.Members).SequenceEqual(repeat.Select(p=>p.Species+":"+p.Members)),"Seed is repeatable and ignores config order");totals.Add(plan.Length);
            foreach(var range in ranges)
            {
                var schools=plan.Where(p=>p.Species==range.Species).ToArray();observed[(int)range.Species].Add(schools.Length);
                Check(schools.Length>=range.MinimumSchools&&schools.Length<=range.MaximumSchools,"School count in inclusive range");
                Check(schools.All(p=>p.Members>=range.Minimum&&p.Members<=range.Maximum),"Member count in inclusive range");
            }
        }
        Check(totals.Count>5,"Different seeds vary school total");
        for(int s=0;s<4;s++)Check(observed[s].Contains(ranges[s].MinimumSchools)&&observed[s].Contains(ranges[s].MaximumSchools),"Both school range endpoints are sampled");
        var fixedRanges=ranges.Select(p=>new OceanFishPopulation(p.Species,1,1,1,1)).ToArray();Check(OceanFishPopulation.CreatePlan(fixedRanges,1).Length==4,"Equal endpoints remain legal");
        Reject(()=>OceanFishPopulation.CreatePlan(ranges,0));Reject(()=>OceanFishPopulation.CreatePlan(null,1));
        ranges[0].MinimumSchools=0;Reject(()=>OceanFishPopulation.CreatePlan(ranges,1));ranges[0].MinimumSchools=6;
        ranges[0].MaximumSchools=17;Reject(()=>OceanFishPopulation.CreatePlan(ranges,1));ranges[0].MaximumSchools=10;
        ranges[0].Maximum=33;Reject(()=>OceanFishPopulation.CreatePlan(ranges,1));ranges[0].Maximum=10;
        ranges[0].Species=ranges[1].Species;Reject(()=>OceanFishPopulation.CreatePlan(ranges,1));
        Console.WriteLine("PASS: "+checks+" seeded fish school/member planning assertions");return 0;
    }
}
