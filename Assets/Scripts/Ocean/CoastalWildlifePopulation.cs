using System;
using System.Collections.Generic;
using MagicIsland.Interaction;

namespace MagicIsland.Ocean
{
    [Serializable]
    public sealed class CoastalWildlifePopulation
    {
        public int Seed=31847;
        public int MinimumBirds=6,MaximumBirds=10,MinimumCrabs=5,MaximumCrabs=9;
        public bool IsValid=>Seed>0&&MinimumBirds>=3&&MinimumBirds<=MaximumBirds&&MaximumBirds<=24
            &&MinimumCrabs>=1&&MinimumCrabs<=MaximumCrabs&&MaximumCrabs<=24;
        public struct Site
        {
            public double Angle,Radius,Phase,Scale,Speed,Travel,Period,Rest;
            public int Activity;
            public double X=>Math.Cos(Angle)*Radius;
            public double Z=>Math.Sin(Angle)*Radius;
        }
        public sealed class Plan { public Site[] Birds,Crabs; }
        // Editor-only caller commits a complete plan. Independent RNG never touches gameplay
        // randomness; failure returns no partial population or resource writes.
        public Plan CreatePlan()
        {
            if(!IsValid)throw new ArgumentException("Invalid coastal wildlife population ranges");
            var random=new Random(Seed);
            int birds=random.Next(MinimumBirds,MaximumBirds+1),crabs=random.Next(MinimumCrabs,MaximumCrabs+1);
            var birdSites=new List<Site>();var crabSites=new List<Site>();
            for(int i=0;i<birds;i++)
            {
                int activity=i<3?i:random.Next(3);
                birdSites.Add(Sample(random,birdSites,activity,false));
            }
            for(int i=0;i<crabs;i++)crabSites.Add(Sample(random,crabSites,0,true));
            return new Plan{Birds=birdSites.ToArray(),Crabs=crabSites.ToArray()};
        }
        static double Between(Random r,double a,double b)=>a+(b-a)*r.NextDouble();
        static Site Sample(Random random,List<Site> previous,int activity,bool crab)
        {
            for(int attempt=0;attempt<10000;attempt++)
            {
                double angle=Between(random,0,Math.PI*2),edge=IslandLayout.CoastRadius(angle);
                var site=new Site{Angle=angle,Radius=edge+(crab||activity==2?-Between(random,1.12,1.42):Between(random,5,9)),
                    Activity=activity,Phase=Between(random,0,Math.PI*2),Scale=Between(random,.82,1.12),
                    Speed=Between(random,activity==1?.24:.13,activity==1?.36:.23),Travel=Between(random,crab?.3:2.4,crab?.62:3.8),
                    Period=Between(random,crab?6:38,crab?11:65),Rest=Between(random,crab?.32:.48,crab?.60:.72)};
                bool valid=true;
                double spacing=crab?6.1:10;
                foreach(var other in previous)if((site.X-other.X)*(site.X-other.X)+(site.Z-other.Z)*(site.Z-other.Z)<spacing*spacing){valid=false;break;}
                if(!valid)continue;
                if(crab||activity==2)
                {
                    // Full crab excursion (wander + alarm + body) and a perched bird's landing
                    // footprint avoid both the sea edge and the real pier entrance.
                    double reach=crab?site.Travel+1.9+.4:.65;
                    for(int j=-12;j<=12;j++)
                    {
                        double a=angle+reach*(j/12.0)/site.Radius,r=IslandLayout.CoastRadius(a)-(edge-site.Radius);
                        double x=Math.Cos(a)*r,z=Math.Sin(a)*r;
                        if(!IslandLayout.ContainsLand(x,z,.4)||IslandLayout.PierReserved(x,z)){valid=false;break;}
                    }
                }
                if(valid)return site;
            }
            throw new InvalidOperationException("Cannot fit coastal wildlife safely; reduce population range");
        }
    }
}
