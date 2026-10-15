using System;
using MagicIsland.Ocean;
using MagicIsland.Interaction;
class VerifyOceanGeography
{
    static int count;
    static void Check(bool value,string why){count++;if(!value)throw new Exception(why);}
    static void Reject(Action action){bool rejected=false;try{action();}catch(ArgumentException){rejected=true;}Check(rejected,"Invalid geography rejected");}
    static int Main()
    {
        foreach(int seed in new[]{1,77,73192,142857})
        {
            var geography=new OceanGeography(seed);var copy=new OceanGeography(seed);double min=100,max=0;
            for(int i=0;i<180;i++)
            {
                double angle=i*Math.PI/90,edge=IslandLayout.CoastRadius(angle),width=geography.ShelfWidth(angle);min=Math.Min(min,width);max=Math.Max(max,width);
                double x=Math.Cos(angle)*(edge-1),z=Math.Sin(angle)*(edge-1);
                Check(Math.Abs(geography.Height(x,z)-(IslandLayout.Height(x,z)-.008))<1e-10,"Walkable coast height preserved");
                for(double offset=2;offset<=6;offset+=.5)
                {
                    x=Math.Cos(angle)*(edge+offset);z=Math.Sin(angle)*(edge+offset);
                    Check(geography.Depth(x,z)>.15&&geography.Depth(x,z)<1.4,"Continuous raised shallow belt in every direction, without deep channels");
                }
                for(double r=edge+2;r<=110;r+=2.75)
                {
                    x=Math.Cos(angle)*r;z=Math.Sin(angle)*r;double h=geography.Height(x,z);
                    Check(!double.IsNaN(h)&&!double.IsInfinity(h)&&h<OceanGeography.SeaLevel-.15,"Submerged finite seabed");
                    Check(h==copy.Height(x,z),"Seed reproduces exact geography");
                    Check(Math.Abs(h-geography.Height(x+.001,z))<.04,"Continuous terrain without vertical steps");
                    if(r>80)Check(geography.Biome(x,z)==OceanBiome.DeepSea&&geography.Depth(x,z)>8,"Expanded outer sea stays deep");
                }
            }
            Check(max-min>3&&min>6&&max<16,"Narrower shallow shelf keeps an irregular outline");
            int reef=0;for(int i=0;i<180;i++){double angle=i*Math.PI/90,r=IslandLayout.CoastRadius(angle)+geography.ShelfWidth(angle)+6;if(geography.Biome(Math.Cos(angle)*r,Math.Sin(angle)*r)==OceanBiome.ReefSlope)reef++;}
            Check(reef>130,"A deeper reef terrace surrounds most of the narrow shallows");
            foreach(var shoal in geography.Shoals)Check(geography.Depth(shoal.X,shoal.Z)>.4&&geography.Depth(shoal.X,shoal.Z)<1.6,"Shallow sandbanks merge into shelf where they overlap");
        }
        var a=new OceanGeography(77);var b=new OceanGeography(78);Check(a.Height(35,8)!=b.Height(35,8),"Different seeds vary geography");
        Reject(()=>new OceanGeography(0));Reject(()=>a.Height(double.NaN,0));Reject(()=>a.Height(double.PositiveInfinity,0));Reject(()=>a.Height(221,0));
        Console.WriteLine("PASS: "+count+" ocean geography assertions");return 0;
    }
}
