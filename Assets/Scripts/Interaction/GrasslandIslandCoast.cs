using System;
using MagicIsland.TerrainSystem;

namespace MagicIsland.Interaction
{
    // Editor-baked coastline and runtime footprint share these deterministic coordinates.
    // Positive lobes reclaim sea only; all previous dry-land heights remain unchanged.
    public static class GrasslandIslandCoast
    {
        public static double Angle(double x,double z)=>Math.Atan2((z-470)/290,(x+20)/215);
        public static double Extension(double x,double z)
        {
            double a=Angle(x,z);
            return 2+29*Lobe(a,-.23,.23)+23*Lobe(a,.74,.20)+27*Lobe(a,2.68,.27)
                +14*Lobe(a,-2.63,.22)+5*Lobe(a,-1.54,.25);
        }
        public static double Distance(double x,double z)=>BiomeIslandLayout.LegacyDistance(IslandBiome.Grassland,x,z)+Extension(x,z);
        public static double ShelfWidth(double x,double z)
        {
            double a=Angle(x,z);
            return 21+16*Lobe(a,-.65,.32)+20*Lobe(a,1.2,.36)+14*Lobe(a,-2.2,.3)
                +4*(.5+.5*Math.Sin(a*5+.7));
        }
        public static double Height(double x,double z)
        {
            double old=BiomeIslandLayout.LegacyDistance(IslandBiome.Grassland,x,z),d=Distance(x,z);
            if(old>=-3)return BiomeIslandLayout.LegacyHeight(x,z);
            double shelf=ShelfWidth(x,z);
            // The ocean rests at -0.22m. Dry fingers rise gently, then join the old wet sand.
            if(d>=0)
            {
                double beach=-.30+(.48+.04*Math.Sin(x*.043)*Math.Sin(z*.031))*Smooth(0,6,d);
                // Do not blend through the old submerged floor: that leaves a thin moat
                // between the original beach and each reclaimed headland.
                return beach*(1-Smooth(-10,-3,old))+Math.Max(-.17,BiomeIslandLayout.LegacyHeight(x,z))*Smooth(-10,-3,old);
            }
            double depth=-d;
            return -.30-1.25*Smooth(0,shelf,depth)-10.75*Smooth(shelf,shelf+58,depth)
                +.10*Math.Sin(x*.049+.4)*Math.Sin(z*.037)*Smooth(4,18,depth)*(1-Smooth(65,100,depth));
        }
        public static double GrassWeight(double x,double z)
        {
            double a=Angle(x,z),width=5+5*(.5+.5*Math.Sin(a*3+.9))+5*Lobe(a,1.2,.4);
            double grain=1.6*Math.Sin(x*.12+.6)*Math.Sin(z*.095);
            return Smooth(width,width+13,Distance(x,z)+grain);
        }
        static double Lobe(double a,double center,double width){double d=Math.Atan2(Math.Sin(a-center),Math.Cos(a-center))/width;return Math.Exp(-d*d*2);}
        static double Smooth(double a,double b,double v){double t=Math.Max(0,Math.Min(1,(v-a)/(b-a)));return t*t*(3-2*t);}
    }
}
