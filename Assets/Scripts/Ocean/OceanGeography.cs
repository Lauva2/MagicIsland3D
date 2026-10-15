using System;
using System.Collections.Generic;
using MagicIsland.Interaction;

namespace MagicIsland.Ocean
{
    public enum OceanBiome { SandFlat, SeagrassMeadow, CoralGarden, Transition, DeepSea, ReefSlope }
    public sealed class OceanShoal
    {
        public double X { get; internal set; }
        public double Z { get; internal set; }
        public double Width { get; internal set; }
        public double Length { get; internal set; }
        public double Rotation { get; internal set; }
    }
    // Baked geography uses its own seed, independent of gameplay and weather randomness.
    public sealed class OceanGeography
    {
        public const double SeaLevel = -.22;
        public const int HalfExtent = 120;
        private readonly int seed;
        private readonly OceanShoal[] shoals;
        public IReadOnlyList<OceanShoal> Shoals => Array.AsReadOnly(shoals);
        public OceanGeography(int seed)
        {
            if(seed<1)throw new ArgumentException("Ocean geography needs a positive seed");
            this.seed=seed;var random=new Random(seed);shoals=new OceanShoal[18];
            for(int i=0;i<shoals.Length;i++)
            {
                double angle=random.NextDouble()*Math.PI*2,radius=IslandLayout.CoastRadius(angle)+8+random.NextDouble()*24;
                shoals[i]=new OceanShoal{X=Math.Cos(angle)*radius,Z=Math.Sin(angle)*radius,Width=2.5+random.NextDouble()*3.5,Length=2+random.NextDouble()*3,Rotation=random.NextDouble()*Math.PI};
            }
        }
        private double Hash(int x,int z)
        {
            uint h;unchecked{h=(uint)x*0x9e3779b9u^(uint)z*0x85ebca6bu^(uint)seed;h^=h>>16;h*=0x7feb352du;h^=h>>15;h*=0x846ca68bu;h^=h>>16;}
            return h/4294967295.0;
        }
        public double Noise(double x,double z)
        {
            int a=(int)Math.Floor(x),b=(int)Math.Floor(z);double u=x-a,v=z-b;u=u*u*(3-2*u);v=v*v*(3-2*v);
            return Lerp(Lerp(Hash(a,b),Hash(a+1,b),u),Lerp(Hash(a,b+1),Hash(a+1,b+1),u),v);
        }
        // A narrow sandy shelf leads into a separate, deeper coral terrace.
        public double ShelfWidth(double angle) => 11+2.4*Math.Sin(angle*3+seed*.001)+1.1*Math.Sin(angle*7-1.4)+2*(Noise(Math.Cos(angle)*2+8,Math.Sin(angle)*2+8)-.5);
        public double ReefWidth(double angle) => 10+1.8*Math.Sin(angle*4+.7)+1.2*Math.Sin(angle*9-1.2);
        public double Height(double x,double z)
        {
            if(double.IsNaN(x)||double.IsInfinity(x)||double.IsNaN(z)||double.IsInfinity(z)||Math.Abs(x)>220||Math.Abs(z)>220)throw new ArgumentException("Invalid ocean coordinate");
            double angle=Math.Atan2(z,x),distance=Math.Sqrt(x*x+z*z)-IslandLayout.CoastRadius(angle);
            double original=IslandLayout.Height(x,z)-.008;
            if(distance<=.65)return original;
            double shelf=ShelfWidth(angle)+1.2*(Noise(x*.10,z*.10)-.5),reef=shelf+ReefWidth(angle);
            double depth=.56+.30*Smooth(0,shelf,distance)+.24*Noise(x*.09+6,z*.09-4);
            depth+=(1.2+.45*Noise(x*.10-8,z*.10+7))*Smooth(shelf-1,shelf+4,distance);
            depth+=12.5*Smooth(reef,reef+14,distance);
            double offshore=Smooth(reef+3,reef+14,distance);
            depth+=2.3*Noise(x*.045+20,z*.045-9)*offshore;
            double channel=1-Smooth(.08,.28,Math.Abs(Math.Sin(x*.105+z*.071+Noise(x*.07,z*.07)*2.8)));
            depth+=channel*2.6*offshore;
            foreach(var shoal in shoals)
            {
                double dx=x-shoal.X,dz=z-shoal.Z,c=Math.Cos(shoal.Rotation),s=Math.Sin(shoal.Rotation);
                double a=(dx*c+dz*s)/shoal.Width,b=(-dx*s+dz*c)/shoal.Length;
                double q=a*a+b*b;
                if(q>3)continue;
                double influence=1-Smooth(.05,1.8,q+.18*(Noise(x*.35,z*.35)-.5));
                depth=Lerp(depth,.70+.55*Noise(x*.16+6,z*.16-4),influence);
            }
            return Lerp(original,SeaLevel-depth,Smooth(.65,3.2,distance));
        }
        public double Depth(double x,double z) => Math.Max(0,SeaLevel-Height(x,z));
        public OceanBiome Biome(double x,double z)
        {
            double depth=Depth(x,z);
            if(depth<.75)return OceanBiome.SandFlat;
            if(depth<1.45)return Noise(x*.11+13,z*.11-7)>.49?OceanBiome.SeagrassMeadow:OceanBiome.CoralGarden;
            if(depth<3.8)return OceanBiome.ReefSlope;
            return depth<8?OceanBiome.Transition:OceanBiome.DeepSea;
        }
        private static double Lerp(double a,double b,double t)=>a+(b-a)*t;
        private static double Smooth(double a,double b,double v){double t=Math.Max(0,Math.Min(1,(v-a)/(b-a)));return t*t*(3-2*t);}
    }
}
