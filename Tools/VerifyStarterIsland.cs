using System;
using MagicIsland.Interaction;
static class VerifyStarterIsland
{
    static int checks;
    static void Check(bool b,string why){checks++;if(!b)throw new Exception(why);}
    static void Main()
    {
        for(int x=-16;x<=16;x++)for(int z=-16;z<=16;z++)
        {
            double radius=Math.Sqrt(x*x+z*z);
            if(radius+.8<=IslandLayout.CoastRadius(Math.Atan2(z,x))-.45)
            {Check(IslandLayout.ContainsLand(x,z,.8),"lost old land");Check(Math.Abs(IslandLayout.LegacyHeight(x,z)-IslandLayout.Height(x,z))<1e-8,"old height changed");}
        }
        Check(!StarterIslandLayout.Contains(160,630),"lake walkable");Check(!StarterIslandLayout.CanPlace(160,630),"lake buildable");
        Check(!StarterIslandLayout.Contains(420,477),"river walkable");Check(StarterIslandLayout.Contains(490,440,.5),"bridge blocked");
        Check(StarterIslandLayout.Contains(-8,700,.5),"tree isle blocked");
        Check(IslandLayout.ContainsPier(17,-8.2,.3)&&IslandLayout.PierReserved(13,-8),"pier changed");
        var r=StarterIslandLayout.Route;
        for(int i=1;i<r.GetLength(0);i++)
        {
            double ax=r[i-1,0],az=r[i-1,1],bx=r[i,0],bz=r[i,1];int count=(int)Math.Ceiling(Math.Sqrt((bx-ax)*(bx-ax)+(bz-az)*(bz-az)));
            double previous=IslandLayout.Height(ax,az);
            for(int j=0;j<=count;j++)
            {
                double x=ax+(bx-ax)*j/count,z=az+(bz-az)*j/count,h=IslandLayout.Height(x,z);
                Check(IslandLayout.ContainsLand(x,z,.5),"route enters water at "+x+","+z);
                if(j>0)Check(Math.Abs(h-previous)<.8,"route too steep at "+x+","+z+" change "+(h-previous));
                Check(HousingState.ValidPosition(HousingState.Island,x,h+.05,z),"new save coordinate rejected");previous=h;
            }
        }
        Check(!HousingState.ValidPosition(HousingState.Island,0,10000,500),"invalid height accepted");
        Check(!IslandLayout.ContainsLand(double.NaN,0)&&!IslandLayout.ContainsLand(0,double.PositiveInfinity),"nonfinite coordinate accepted");
        for(int x=-800;x<=800;x+=16)for(int z=32;z<=1248;z+=16)
        {
            if(StarterIslandLayout.OriginalContains(x,z,.5))
            {
                Check(StarterIslandLayout.Contains(x,z,.5),"old mainland coordinate removed");
                Check(Math.Abs(StarterIslandLayout.Ground(x,z)-StarterIslandLayout.OriginalGround(x,z))<1e-8,"old dry-ground height changed");
            }
            if(StarterIslandLayout.OriginalCanPlace(x,z))Check(StarterIslandLayout.CanPlace(x,z),"old construction cell rejected");
        }
        for(int i=0;i<360;i++)
        {
            double a=i*Math.PI*2/360,coast=StarterIslandLayout.CoastRadius(a),lake=StarterIslandLayout.LakeRadius(a);
            double x=Math.Cos(a)*740*coast,z=650+Math.Sin(a)*540*coast;
            Check(Math.Abs(StarterIslandLayout.CoastDistance(x,z))<1e-6,"coast mesh equation differs");
            Check(x>-832&&x<832&&z>32&&z<1248,"headland outside baked chunk coverage");
            Check(Math.Abs(StarterIslandLayout.LakeDistance(160+Math.Cos(a)*220*lake,630+Math.Sin(a)*190*lake))<1e-6,"lake mesh equation differs");
        }
        Console.WriteLine("Starter island rules PASSED: "+checks+" checks.");
    }
}
