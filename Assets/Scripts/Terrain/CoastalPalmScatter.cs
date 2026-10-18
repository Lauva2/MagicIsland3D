using System;
using System.Collections.Generic;
using MagicIsland.Interaction;

namespace MagicIsland.TerrainSystem
{
    // Editor-baked scenery only: independent of gameplay random numbers and progress saves.
    [Serializable]
    public sealed class CoastalPalmScatter
    {
        public int Seed = 20261017;
        public int MinimumCount = 16, MaximumCount = 20;
        public float MinimumInset = 1.9f, MaximumInset = 4.8f;
        public float MinimumSpacing = 1.9f;
        public float CrownSpacing = 1.55f;
        public float MinimumScale = .80f, MaximumScale = 1.22f;

        public PalmSite[] CreatePlan()
        {
            if(MinimumCount<8||MaximumCount<MinimumCount||MaximumCount>64
                ||!Finite(MinimumInset)||!Finite(MaximumInset)||MinimumInset<1.8f||MaximumInset<MinimumInset||MaximumInset>6
                ||!Finite(MinimumSpacing)||MinimumSpacing<1.5f||MinimumSpacing>5
                ||!Finite(CrownSpacing)||CrownSpacing<1||CrownSpacing>2
                ||!Finite(MinimumScale)||!Finite(MaximumScale)||MinimumScale<.6f||MaximumScale<MinimumScale||MaximumScale>1.5f)
                throw new ArgumentException("Invalid coastal palm scatter settings; existing scenery preserved.");
            var random=new Random(Seed);
            int count=random.Next(MinimumCount,MaximumCount+1),clusters=random.Next(4,7);
            var angles=new double[clusters];var insets=new double[clusters];
            var used=new int[clusters];var limits=new int[clusters];
            for(int i=0;i<clusters;i++)
            {
                bool found=false;
                for(int attempt=0;attempt<4000&&!found;attempt++)
                {
                    double angle=random.NextDouble()*Math.PI*2,inset=Range(random,MinimumInset,MaximumInset);
                    double radius=IslandLayout.CoastRadius(angle)-inset,x=Math.Cos(angle)*radius,z=Math.Sin(angle)*radius;
                    if(!ClearSite(x,z,.29*MaximumScale+.35))continue;
                    found=true;
                    for(int j=0;j<i;j++)
                    {
                        double r=IslandLayout.CoastRadius(angles[j])-insets[j];
                        if(Math.Pow(x-Math.Cos(angles[j])*r,2)+Math.Pow(z-Math.Sin(angles[j])*r,2)<7.5*7.5){found=false;break;}
                    }
                    if(found){angles[i]=angle;insets[i]=inset;limits[i]=random.Next(2,4);}
                }
                if(!found)throw new InvalidOperationException("Cannot reserve separated palm groves; existing scenery preserved.");
            }
            var sites=new List<PalmSite>();
            for(int attempt=0;attempt<16000&&sites.Count<count;attempt++)
            {
                double angle,inset;int group=-1;
                if(random.NextDouble()<.65)
                {
                    int cluster=random.Next(clusters);
                    if(used[cluster]>=limits[cluster])continue;
                    group=cluster;
                    angle=angles[cluster]+(random.NextDouble()+random.NextDouble()-1)*.22;
                    inset=Math.Max(MinimumInset,Math.Min(MaximumInset,insets[cluster]+(random.NextDouble()+random.NextDouble()-1)*1.5));
                }
                else {angle=random.NextDouble()*Math.PI*2;inset=Range(random,MinimumInset,MaximumInset);}
                double radius=IslandLayout.CoastRadius(angle)-inset,x=Math.Cos(angle)*radius,z=Math.Sin(angle)*radius;
                double scale=Range(random,MinimumScale,MaximumScale);
                // Include the bent trunk's offset and radius plus walking clearance.
                double clearance=.29*scale+.35;
                if(!ClearSite(x,z,clearance))continue;
                bool clear=true;
                foreach(var site in sites)
                {
                    double spacing=Math.Max(MinimumSpacing,CrownSpacing*(site.Scale+scale));
                    if((site.X-x)*(site.X-x)+(site.Z-z)*(site.Z-z)<spacing*spacing){clear=false;break;}
                }
                if(!clear)continue;
                sites.Add(new PalmSite(x,z,random.NextDouble()*360,scale));
                if(group>=0)used[group]++;
            }
            if(sites.Count!=count)throw new InvalidOperationException("Coastal palms cannot fit the requested count and spacing; existing scenery preserved.");
            return sites.ToArray();
        }

        public static bool ClearSite(double x,double z,double clearance)
        {
            if(!Finite(x)||!Finite(z)||!Finite(clearance)||clearance<0)return false;
            if(Math.Abs(x)<12.7+clearance&&Math.Abs(z)<12.7+clearance)return false;
            if(x>11.5-clearance&&x<19.3+clearance&&Math.Abs(z-IslandLayout.PierZ)<2+clearance)return false;
            double dx=x-14.15,dz=z+6.2;
            if(dx*dx+dz*dz<(1.7+clearance)*(1.7+clearance))return false;
            return IslandLayout.ContainsLand(x,z,.5+clearance);
        }
        static double Range(Random random,double min,double max)=>min+(max-min)*random.NextDouble();
        static bool Finite(double n)=>!double.IsNaN(n)&&!double.IsInfinity(n);
    }

    public readonly struct PalmSite
    {
        public readonly double X,Z,Yaw,Scale;
        public PalmSite(double x,double z,double yaw,double scale){X=x;Z=z;Yaw=yaw;Scale=scale;}
    }
}
