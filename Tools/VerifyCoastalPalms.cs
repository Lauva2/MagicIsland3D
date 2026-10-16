using System;
using System.Linq;
using MagicIsland.Interaction;
using MagicIsland.TerrainSystem;

class VerifyCoastalPalms
{
    static int checks;
    static void Require(bool value,string why){checks++;if(!value)throw new Exception(why);}
    static int Main()
    {
        for(int seed=0;seed<256;seed++)
        {
            var settings=new CoastalPalmScatter{Seed=seed};var sites=settings.CreatePlan();var again=settings.CreatePlan();
            Require(sites.Length>=settings.MinimumCount&&sites.Length<=settings.MaximumCount,"Inclusive count range");
            Require(sites.SequenceEqual(again),"Rebuild is deterministic and does not depend on other random users");
            foreach(var site in sites)
            {
                Require(CoastalPalmScatter.ClearSite(site.X,site.Z,.29*site.Scale+.35),"Trunk footprint clears shore, village, pier and NPC approach");
                double inset=IslandLayout.CoastRadius(Math.Atan2(site.Z,site.X))-Math.Sqrt(site.X*site.X+site.Z*site.Z);
                Require(inset>=settings.MinimumInset-.00001&&inset<=settings.MaximumInset+.00001,"Continuous depth stays in authored band");
                Require(site.Scale>=settings.MinimumScale&&site.Scale<=settings.MaximumScale&&site.Yaw>=0&&site.Yaw<360,"Authored size and orientation");
                Require(Math.Abs(IslandLayout.Height(site.X,site.Z))<.00001,"Trunks stand on flat dry ground");
            }
            for(int i=0;i<sites.Length;i++)for(int j=i+1;j<sites.Length;j++)
                Require(Math.Pow(sites[i].X-sites[j].X,2)+Math.Pow(sites[i].Z-sites[j].Z,2)>=settings.MinimumSpacing*settings.MinimumSpacing,"Clumps preserve minimum trunk spacing");
        }
        var defaults=new CoastalPalmScatter();var plan=defaults.CreatePlan();
        var depths=plan.Select(s=>IslandLayout.CoastRadius(Math.Atan2(s.Z,s.X))-Math.Sqrt(s.X*s.X+s.Z*s.Z)).ToArray();
        var angles=plan.Select(s=>(Math.Atan2(s.Z,s.X)+Math.PI*2)%(Math.PI*2)).OrderBy(a=>a).ToArray();
        var gaps=angles.Select((a,i)=>i==0?a+Math.PI*2-angles[angles.Length-1]:a-angles[i-1]).ToArray();
        Require(depths.Max()-depths.Min()>1.8,"Default layout has depth, rather than a narrow ring");
        Require(gaps.Max()>gaps.Average()*2&&gaps.Min()<gaps.Average()*.5,"Default layout has clearings and small clumps");
        Require(plan.Select(s=>s.Scale).Distinct().Count()==plan.Length,"No repeating three-size cycle");
        Require(!plan.SequenceEqual(new CoastalPalmScatter{Seed=defaults.Seed+1}.CreatePlan()),"Seed changes layout");
        var invalid=new[]{new CoastalPalmScatter{MinimumInset=float.NaN},new CoastalPalmScatter{MaximumScale=float.PositiveInfinity},
            new CoastalPalmScatter{MinimumCount=28},new CoastalPalmScatter{MinimumSpacing=0}};
        foreach(var settings in invalid)
        {
            bool rejected=false;try{settings.CreatePlan();}catch(ArgumentException){rejected=true;}
            Require(rejected,"Invalid configuration is rejected before changing scenery");
        }
        Console.WriteLine("PASS: "+checks+" coastal palm assertions across 256 seeds; default count="+plan.Length
            +", depth range="+(depths.Max()-depths.Min()).ToString("F2")+", gap ratio="+(gaps.Max()/gaps.Min()).ToString("F1"));
        return 0;
    }
}
