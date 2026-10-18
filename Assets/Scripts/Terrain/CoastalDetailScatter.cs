using System;
using System.Collections.Generic;
using MagicIsland.Interaction;

namespace MagicIsland.TerrainSystem
{
    // A complete, deterministic plan is prepared before the editor changes any instances.
    [Serializable]
    public sealed class CoastalDetailScatter
    {
        public int Seed=20261018;
        public int Shrubs=35, Shells=28, Pebbles=11;

        public CoastalDetailSite[] CreatePlan(PalmSite[] palms)
        {
            if(Shrubs<0||Shrubs>80||Shells<0||Shells>80||Pebbles<0||Pebbles>40)
                throw new ArgumentException("Invalid coastal detail counts; existing scenery preserved.");
            var random=new Random(Seed);var sites=new List<CoastalDetailSite>();
            int[] counts={Shrubs,Shells,Pebbles};
            for(int kind=0;kind<3;kind++)
            {
                // Shared pockets and long clearings, with scattered singles outside each pocket.
                int clusters=kind==0?9:7;var anchors=new double[clusters];
                for(int c=0;c<clusters;c++)anchors[c]=random.NextDouble()*Math.PI*2;
                int added=0;
                for(int attempt=0;attempt<20000&&added<counts[kind];attempt++)
                {
                    double angle=random.NextDouble()<.78?anchors[random.Next(clusters)]+(random.NextDouble()+random.NextDouble()-1)*.20:random.NextDouble()*Math.PI*2;
                    double inset=kind==0?Range(random,2.4,4.8):Range(random,.9,2.7);
                    double scale=kind==0?Range(random,.58,1.02):kind==1?Range(random,.15,.26):Range(random,.27,.49);
                    double radius=IslandLayout.CoastRadius(angle)-inset,x=Math.Cos(angle)*radius,z=Math.Sin(angle)*radius;
                    double footprint=kind==0?.55*scale:kind==1?.16:.38;
                    if(!CoastalPalmScatter.ClearSite(x,z,footprint))continue;
                    bool clear=true;
                    if(kind==0&&palms!=null)foreach(var p in palms)
                        if(Distance2(x,z,p.X,p.Z)<Math.Pow(.45*p.Scale+footprint,2)){clear=false;break;}
                    if(!clear)continue;
                    foreach(var s in sites)
                    {
                        double spacing=kind==0&&s.Kind==0?.9*(scale+s.Scale):kind==s.Kind?.42:.30;
                        if(Distance2(x,z,s.X,s.Z)<spacing*spacing){clear=false;break;}
                    }
                    if(!clear)continue;
                    sites.Add(new CoastalDetailSite(kind,x,z,random.NextDouble()*360,scale));added++;
                }
                if(added!=counts[kind])throw new InvalidOperationException("Cannot fit coastal details; existing scenery preserved.");
            }
            return sites.ToArray();
        }
        static double Distance2(double x,double z,double a,double b)=>(x-a)*(x-a)+(z-b)*(z-b);
        static double Range(Random random,double min,double max)=>min+(max-min)*random.NextDouble();
    }
    public readonly struct CoastalDetailSite
    {
        public readonly int Kind;
        public readonly double X,Z,Yaw,Scale;
        public CoastalDetailSite(int kind,double x,double z,double yaw,double scale){Kind=kind;X=x;Z=z;Yaw=yaw;Scale=scale;}
    }
}
