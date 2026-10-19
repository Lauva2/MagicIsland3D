// Frozen independent rule fixture: biome islands before grassland coast refinement, 2026-10-19.
using System;
using MagicIsland.TerrainSystem;

namespace MagicIsland.Interaction
{
    // One shared footprint for baked art, navigation, construction, map and debug travel.
    // The beach is the existing village island; the four other islands have no connecting land.
    public static class BiomeIslandsBeforeCoast
    {
        public const double MinX=-820,MaxX=900,MinZ=-60,MaxZ=1360;
        public static readonly IslandBiome[] Biomes = { IslandBiome.Beach, IslandBiome.Grassland, IslandBiome.Desert, IslandBiome.Forest, IslandBiome.Snow };
        public static void Shape(IslandBiome biome, out double x, out double z, out double rx, out double rz)
        {
            switch (biome)
            {
                case IslandBiome.Grassland: x=-20; z=470; rx=215; rz=290; break;
                case IslandBiome.Desert: x=-530; z=590; rx=200; rz=225; break;
                case IslandBiome.Forest: x=545; z=650; rx=180; rz=210; break;
                case IslandBiome.Snow: x=-20; z=1080; rx=280; rz=190; break;
                case IslandBiome.Beach: x=z=0; rx=rz=18; break;
                default: throw new ArgumentOutOfRangeException(nameof(biome));
            }
        }
        public static double Distance(IslandBiome biome, double x, double z)
        {
            if (!Finite(x)||!Finite(z)) return double.NegativeInfinity;
            if (biome==IslandBiome.Beach) return IslandLayout.CoastRadius(Math.Atan2(z,x))-Math.Sqrt(x*x+z*z);
            Shape(biome,out var cx,out var cz,out var rx,out var rz);
            double dx=(x-cx)/rx,dz=(z-cz)/rz,a=Math.Atan2(dz,dx);
            double edge=1+.036*Math.Sin(a*3+(int)biome)+.021*Math.Sin(a*7-.7*(int)biome);
            // Northern headland keeps the complete ancient-tree grove on this island.
            if(biome==IslandBiome.Grassland){double da=Math.Atan2(Math.Sin(a-Math.PI*.5),Math.Cos(a-Math.PI*.5));edge+=.20*Math.Exp(-da*da/.38);}
            return (edge-Math.Sqrt(dx*dx+dz*dz))*Math.Min(rx,rz);
        }
        public static IslandBiome At(double x,double z)
        {
            IslandBiome best=IslandBiome.Beach; double distance=Distance(best,x,z);
            foreach(var biome in Biomes) { double d=Distance(biome,x,z); if(d>distance){best=biome;distance=d;} }
            return best;
        }
        public static double CoastDistance(double x,double z)=>Distance(At(x,z),x,z);
        public static bool Contains(double x,double z,double margin=0)
            =>Finite(x)&&Finite(z)&&Finite(margin)&&margin>=0&&z>23&&CoastDistance(x,z)>=margin+2;
        public static double GroundHeight(double x,double z)
        {
            double d=CoastDistance(x,z);
            // Keep the authored interior heights, with a continuous sloping beach into the sea.
            double h=Math.Max(1.3,StarterIslandLayout.Ground(x,z));
            if(d<=0)return -4*Smooth(0,24,-d);
            return h*Smooth(0,42,d);
        }
        public static bool CanPlace(int x,int z)
        {
            if(!Contains(x,z,3)||StarterIslandLayout.RouteDistance(x,z)<6)return false;
            double y=GroundHeight(x,z);
            foreach(double dx in new[]{-.5,.5})foreach(double dz in new[]{-.5,.5})
                if(!Contains(x+dx,z+dz,2)||Math.Abs(GroundHeight(x+dx,z+dz)-y)>.12)return false;
            foreach(var p in new[]{new[]{-420d,590d},new[]{-8d,700d},new[]{480d,650d},new[]{-40d,1000d}})
                if((x-p[0])*(x-p[0])+(z-p[1])*(z-p[1])<900)return false;
            return true;
        }
        public static void Landing(IslandBiome biome,out double x,out double z)
        {
            Shape(biome,out x,out z,out _,out _);
            if(biome==IslandBiome.Grassland){x=-120;z=440;}
            if(biome==IslandBiome.Beach){x=1;z=-15.8;}
            if(biome==IslandBiome.Desert){x=-435;z=575;}
            if(biome==IslandBiome.Forest){x=465;z=610;}
            if(biome==IslandBiome.Snow){x=-120;z=1000;}
        }
        public static string Name(IslandBiome biome)=>IslandBiomeSettings.Name(biome)+"岛";
        static bool Finite(double v)=>!double.IsNaN(v)&&!double.IsInfinity(v);
        static double Smooth(double a,double b,double v){double t=Math.Max(0,Math.Min(1,(v-a)/(b-a)));return t*t*(3-2*t);}
    }
}
