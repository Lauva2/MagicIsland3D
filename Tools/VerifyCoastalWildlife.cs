using System;
using MagicIsland.Ocean;
using MagicIsland.Interaction;
class VerifyCoastalWildlife
{
    static int assertions;
    static void Check(bool ok,string why){assertions++;if(!ok)throw new Exception(why);}
    static int Main()
    {
        var settings=new CoastalWildlifePopulation();bool lowBird=false,highBird=false,lowCrab=false,highCrab=false;
        for(int seed=1;seed<=1024;seed++)
        {
            settings.Seed=seed;var a=settings.CreatePlan();var b=settings.CreatePlan();
            Check(a.Birds.Length>=settings.MinimumBirds&&a.Birds.Length<=settings.MaximumBirds,"Bird count range");
            Check(a.Crabs.Length>=settings.MinimumCrabs&&a.Crabs.Length<=settings.MaximumCrabs,"Crab count range");
            lowBird|=a.Birds.Length==settings.MinimumBirds;highBird|=a.Birds.Length==settings.MaximumBirds;
            lowCrab|=a.Crabs.Length==settings.MinimumCrabs;highCrab|=a.Crabs.Length==settings.MaximumCrabs;
            foreach(bool crab in new[]{false,true})
            {
                var sites=crab?a.Crabs:a.Birds;var repeat=crab?b.Crabs:b.Birds;
                for(int i=0;i<sites.Length;i++)
                {
                    var site=sites[i];Check(site.X==repeat[i].X&&site.Z==repeat[i].Z&&site.Phase==repeat[i].Phase,"Reproducible continuous position and phase");
                    Check(!double.IsNaN(site.X)&&!double.IsInfinity(site.Z),"Finite positions");
                    for(int j=0;j<i;j++)Check((site.X-sites[j].X)*(site.X-sites[j].X)+(site.Z-sites[j].Z)*(site.Z-sites[j].Z)>=(crab?6.1*6.1:100),"Sparse home spacing");
                    if(!crab&&site.Activity!=2)continue;
                    double homeEdge=IslandLayout.CoastRadius(site.Angle),reach=crab?site.Travel+2.3:.65;
                    for(int j=-40;j<=40;j++)
                    {
                        double angle=site.Angle+reach*j/40/site.Radius,r=IslandLayout.CoastRadius(angle)-(homeEdge-site.Radius);
                        double x=Math.Cos(angle)*r,z=Math.Sin(angle)*r;
                        Check(IslandLayout.ContainsLand(x,z,.4)&&!IslandLayout.PierReserved(x,z),"Entire dry excursion avoids pier");
                    }
                }
            }
        }
        Check(lowBird&&highBird&&lowCrab&&highCrab,"Both endpoints of inclusive count ranges occur");
        settings.MinimumBirds=settings.MaximumBirds=3;settings.MinimumCrabs=settings.MaximumCrabs=1;
        var fixedPlan=settings.CreatePlan();Check(fixedPlan.Birds.Length==3&&fixedPlan.Crabs.Length==1,"Equal min/max supported");
        settings.MinimumCrabs=0;Check(!settings.IsValid,"Reject invalid range");
        bool rejected=false;try{settings.CreatePlan();}catch(ArgumentException){rejected=true;}Check(rejected,"Invalid input cannot produce partial plan");
        Console.WriteLine("PASS: "+assertions+" coastal wildlife population assertions");return 0;
    }
}
