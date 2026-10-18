using System;

namespace MagicIsland.Interaction
{
    // Authored world coordinates. The original village, pier and all old save coordinates stay fixed.
    // CoastRadius remains the legacy coastal ecology contract; this domain extends it to the north.
    public static class StarterIslandLayout
    {
        public const double CenterZ = 650, RadiusX = 740, RadiusZ = 540, LakeLevel = 12;
        public const double MinX = -820, MaxX = 820, MinZ = -60, MaxZ = 1260;
        public static readonly double[,] Route = {
            {0,16},{0,160},{0,240},{-150,370},{-280,500},{-390,565},
            {-420,760},{-260,910},{-40,980},{60,985},{140,985},{200,920},{220,870},
            {350,820},{470,720},{480,650},{475,520},{510,475},{504,459},{476,421},{300,340},{0,240},
            {-150,370},{-100,520},{-100,700},{-75,700},{-32,700},{-18,690},{-8,690}
        };
        public static double CoastDistance(double x, double z)
        {
            double dx=x/RadiusX,dz=(z-CenterZ)/RadiusZ,a=Math.Atan2(dz,dx);
            return (CoastRadius(a)-Math.Sqrt(dx*dx+dz*dz))*RadiusZ;
        }
        // Add headlands around the first mainland approach; retain every original land coordinate.
        public static double CoastRadius(double a)=>OriginalCoastRadius(a)+.12*Lobe(a,-2.12,.23)+.105*Lobe(a,-1.05,.19)
            +.075*Lobe(a,.02,.18)+.045*Lobe(a,2.8,.18);
        static double OriginalCoastRadius(double a)=>1+.035*Math.Sin(a*3+.3)+.018*Math.Sin(a*7-1);
        public static double OriginalCoastDistance(double x,double z)
        {
            double dx=x/RadiusX,dz=(z-CenterZ)/RadiusZ,a=Math.Atan2(dz,dx);
            return (OriginalCoastRadius(a)-Math.Sqrt(dx*dx+dz*dz))*RadiusZ;
        }
        public static double LakeDistance(double x,double z)
        {
            double dx=(x-160)/220,dz=(z-630)/190,a=Math.Atan2(dz,dx);
            return (Math.Sqrt(dx*dx+dz*dz)-LakeRadius(a))*190;
        }
        public static double LakeRadius(double a)=>1+.045*Math.Sin(a*5)-.16*Lobe(a,-1.95,.29)
            -.12*Lobe(a,.35,.23)-.09*Lobe(a,1.6,.20);
        public static double OriginalLakeDistance(double x,double z)
        {
            double dx=(x-160)/220,dz=(z-630)/190,a=Math.Atan2(dz,dx);
            return (Math.Sqrt(dx*dx+dz*dz)-(1+.045*Math.Sin(a*5)))*190;
        }
        public static double SegmentDistance(double x,double z,double ax,double az,double bx,double bz)
        {
            double dx=bx-ax,dz=bz-az,t=Clamp(((x-ax)*dx+(z-az)*dz)/(dx*dx+dz*dz));
            dx=x-ax-t*dx;dz=z-az-t*dz;return Math.Sqrt(dx*dx+dz*dz);
        }
        public static double OutletDistance(double x,double z) => Math.Min(SegmentDistance(x,z,340,520,490,440),SegmentDistance(x,z,490,440,720,270))-9;
        public static double InletDistance(double x,double z) => SegmentDistance(x,z,130,790,90,950)-5;
        public static bool Bridge(double x,double z,double margin=0)
        {
            // Across the outlet, perpendicular to its southeast flow.
            double along=(x-490)*.8+(z-440)*-.6,across=(x-490)*.6+(z-440)*.8;
            return Math.Abs(along)<3.5-margin&&Math.Abs(across)<24-margin;
        }
        public static bool TreeIsle(double x,double z,double margin=0) =>
            (margin<24&&(x+8)*(x+8)+(z-700)*(z-700)<(24-margin)*(24-margin))
            ||(x>=-80+margin&&x<=-8&&Math.Abs(z-700)<3.5-margin);
        public static bool Corridor(double x,double z,double margin=0) => z>=14&&z<=180&&Math.Abs(x)<=5.5-margin;
        public static bool Contains(double x,double z,double margin=0)
            =>ContainsCore(x,z,margin,false);
        public static bool OriginalContains(double x,double z,double margin=0)
            =>ContainsCore(x,z,margin,true);
        static bool ContainsCore(double x,double z,double margin,bool original)
        {
            if(double.IsNaN(x)||double.IsNaN(z)||double.IsInfinity(x)||double.IsInfinity(z)||margin<0||margin>100)return false;
            if(Corridor(x,z,margin))return true;
            if((original?OriginalCoastDistance(x,z):CoastDistance(x,z))<margin+2)return false;
            if(TreeIsle(x,z,margin)||Bridge(x,z,margin))return true;
            return (original?OriginalLakeDistance(x,z):LakeDistance(x,z))>margin+1&&OutletDistance(x,z)>margin+1&&InletDistance(x,z)>margin+1;
        }
        public static double RouteDistance(double x,double z)
        {
            double best=double.MaxValue;
            for(int i=1;i<Route.GetLength(0);i++)best=Math.Min(best,SegmentDistance(x,z,Route[i-1,0],Route[i-1,1],Route[i,0],Route[i,1]));
            return best;
        }
        public static double Ground(double x,double z)
        {
            double oldCoast=OriginalCoastDistance(x,z),coast=CoastDistance(x,z),oldLake=OriginalLakeDistance(x,z),lake=LakeDistance(x,z);
            // Original dry-ground heights and saved construction stay stable. Sculpt only reclaimed shores.
            if(oldCoast<0&&coast>=0)return 2.8*Smooth(0,18,-oldCoast)*Smooth(0,24,coast)*Smooth(110,200,z);
            if(oldLake<0&&lake>=0&&!TreeIsle(x,z)&&!Bridge(x,z))return 12.35+.35*Smooth(0,18,lake);
            return OriginalGround(x,z);
        }
        public static double OriginalGround(double x,double z)
        {
            double h=NaturalGround(x,z);
            foreach(var p in new[]{new[]{-420d,590d,28d,70d},new[]{480d,650d,27d,60d},new[]{-40d,1000d,28d,110d}})
            {
                double distance=Math.Sqrt((x-p[0])*(x-p[0])+(z-p[1])*(z-p[1]));
                if(distance<p[3]){double w=Smooth(p[2],p[3],distance);h=NaturalGround(p[0],p[1])*(1-w)+h*w;}
            }
            return h;
        }
        static double NaturalGround(double x,double z)
        {
            if(z<110&&Math.Abs(x)<6)return 0;
            double inland=Smooth(0,32,OriginalCoastDistance(x,z));
            double meadow=12+3*Math.Sin(x*.012)*Math.Sin(z*.009);
            double snow=Smooth(800,1100,z)*(60+65*Math.Pow(Math.Sin(x*.009+.8),2));
            double dunes=x<-260?Smooth(-240,-500,x)*(5+4*Math.Sin(x*.035+z*.018)):0;
            double h=(meadow+snow+dunes)*inland;
            // Flat freshwater basin; banks ease into hills without a vertical collision seam.
            double lake=OriginalLakeDistance(x,z),water=Math.Min(OutletDistance(x,z),InletDistance(x,z));
            double riverLevel=12*Smooth(0,70,OriginalCoastDistance(x,z));
            h=h*(Smooth(0,55,lake))+12*(1-Smooth(0,55,lake));
            h=h*Smooth(0,28,water)+riverLevel*(1-Smooth(0,28,water));
            if(lake<0)h=12-10*Smooth(0,35,-lake);
            if(water<0)h=riverLevel-2;
            if(TreeIsle(x,z))h=12.6;
            if(Bridge(x,z))h=riverLevel+.65*Smooth(24,14,Math.Abs((x-490)*.6+(z-440)*.8));
            return h*Smooth(110,200,z);
        }
        public static double DesertWeight(double x,double z)=>Smooth(-220,-300,x+18*Math.Sin(z*.013)+11*Math.Sin(z*.032));
        public static double ForestWeight(double x,double z)=>Smooth(340,440,x+24*Math.Sin(z*.011)+12*Math.Sin(z*.027));
        public static double SnowWeight(double x,double z)=>Smooth(820,900,z+28*Math.Sin(x*.009)+12*Math.Sin(x*.023));
        public static int OriginalRegion(double x,double z)=>z>860?2:x<-260?1:x>390?3:0;
        public static int Region(double x,double z) => SnowWeight(x,z)>.5?2:DesertWeight(x,z)>.5?1:ForestWeight(x,z)>.5?3:0;
        public static string RegionName(double x,double z) => z<100?"初始生活区":CoastDistance(x,z)<22?"沙滩":new[]{"中央草原","西部沙漠","北境雪山","东部林地"}[Region(x,z)];
        public static bool CanPlace(int x,int z)
            =>OriginalCanPlace(x,z)||CanPlaceCore(x,z,false);
        public static bool OriginalCanPlace(int x,int z)=>CanPlaceCore(x,z,true);
        static bool CanPlaceCore(int x,int z,bool original)
        {
            if(!(original?OriginalContains(x,z,1.5):Contains(x,z,1.5))||RouteDistance(x,z)<6||TreeIsle(x,z, -3)||Bridge(x,z,-3))return false;
            double y=original?OriginalGround(x,z):Ground(x,z);
            foreach(double dx in new[]{-.5,.5})foreach(double dz in new[]{-.5,.5})if(Math.Abs((original?OriginalGround(x+dx,z+dz):Ground(x+dx,z+dz))-y)>.12)return false;
            foreach(var p in new[]{new[]{-420d,590d},new[]{-8d,700d},new[]{480d,650d},new[]{-40d,1000d}})
                if((x-p[0])*(x-p[0])+(z-p[1])*(z-p[1])<900)return false;
            return true;
        }
        static double Clamp(double x)=>Math.Max(0,Math.Min(1,x));
        static double Lobe(double a,double center,double width){double d=Math.Atan2(Math.Sin(a-center),Math.Cos(a-center))/width;return Math.Exp(-d*d*2);}
        static double Smooth(double a,double b,double x){double t=Clamp((x-a)/(b-a));return t*t*(3-2*t);}
    }
}
