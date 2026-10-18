using System;

namespace MagicIsland.Interaction
{
    // Shared prototype footprint used by baked AA terrain, placement and save validation.
    public static class IslandLayout
    {
        public const double PierX = 14.8, PierZ = -8.2;
        public static double CoastRadius(double angle)
        {
            double c=Math.Cos(angle),s=Math.Sin(angle);
            double rounded=17/Math.Pow(c*c*c*c+s*s*s*s,.25);
            double organic=17.3+.65*Math.Sin(angle*3+.7)+.4*Math.Sin(angle*7);
            // Smooth union rounds the old square corners while retaining its entire footprint.
            return .5*(rounded+organic+Math.Sqrt((rounded-organic)*(rounded-organic)+.36));
        }
        public static bool ContainsLand(double x,double z,double margin=0)
        {
            if(!Finite(x)||!Finite(z)||!Finite(margin)||margin<0)return false;
            return Math.Sqrt(x*x+z*z)+margin<=CoastRadius(Math.Atan2(z,x))-.45 || StarterIslandLayout.Contains(x,z,margin);
        }
        public static bool ContainsPier(double x,double z,double margin=0)
            => Finite(x)&&Finite(z)&&Finite(margin)&&margin>=0&&x>=PierX-2+margin&&x<=PierX+3.4-margin&&Math.Abs(z-PierZ)<=1.2-margin;
        public static bool Contains(double x,double z,double margin=0) => ContainsLand(x,z,margin)||ContainsPier(x,z,margin);
        public static bool CanPlace(int x,int z)
        {
            if(z>23)return StarterIslandLayout.CanPlace(x,z);
            // Keep complete 1m cells on land; leave a clear strip along the water.
            foreach(double dx in new[]{-.5,.5})foreach(double dz in new[]{-.5,.5})if(!ContainsLand(x+dx,z+dz,.4))return false;
            return true;
        }
        public static bool PierReserved(double x,double z)
            => x+.5>PierX-2.5&&x-.5<PierX+3.7&&z+.5>PierZ-1.6&&z-.5<PierZ+1.6;
        public static double Height(double x,double z)
            => z>18 && StarterIslandLayout.Contains(x,z) ? StarterIslandLayout.Ground(x,z) : LegacyHeight(x,z);
        public static double LegacyHeight(double x,double z)
        {
            double radius=Math.Sqrt(x*x+z*z),edge=CoastRadius(Math.Atan2(z,x));
            if(radius<=edge-1.7)return 0;
            double t=(radius-edge+1.7)/9.2;return -.02-4.4*t*t+.004;
        }
        public static bool Gate(double x,double z) => x>10&&Math.Abs(z-PierZ)<1.18;
        private static bool Finite(double n)=>!double.IsNaN(n)&&!double.IsInfinity(n);
    }
}
