using System;
using MagicIsland.Interaction;
using MagicIsland.TerrainSystem;

// Standalone harness types; production Unity definitions are not compiled into this executable.
namespace MagicIsland.TerrainSystem
{
    public enum IslandBiome { Grassland,Desert,Snow,Beach,Forest }
    public static class IslandBiomeSettings { public static string Name(IslandBiome value)=>value.ToString(); }
}
static class VerifyGrasslandIslandCoast
{
    static long checks;static int oldCells,newCells;
    static void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
    static void Main()
    {
        for(int x=-800;x<=800;x+=2)for(int z=26;z<=1320;z+=2)
        {
            bool before=BiomeIslandsBeforeCoast.Contains(x,z,.5);
            if(before)
            {
                Check(BiomeIslandLayout.Contains(x,z,.5),"lost old land at "+x+","+z);
                Check(Math.Abs(BiomeIslandLayout.Height(x,z)-BiomeIslandsBeforeCoast.GroundHeight(x,z))<1e-10,"old dry height changed");
            }
            if(BiomeIslandsBeforeCoast.CanPlace(x,z)){oldCells++;Check(BiomeIslandLayout.CanPlace(x,z),"old construction cell lost");}
            else if(BiomeIslandLayout.CanPlace(x,z))newCells++;
            if(BiomeIslandsBeforeCoast.At(x,z)!=IslandBiome.Grassland&&before)
                Check(BiomeIslandLayout.At(x,z)==BiomeIslandsBeforeCoast.At(x,z),"other biome changed");
        }
        double min=100,max=0;
        for(int i=0;i<720;i++)
        {
            double a=i*Math.PI/360;
            // Solve the new shore along each normalized radial ray, then inspect cross-shore depth.
            double lo=.7,hi=1.7;
            for(int j=0;j<35;j++){double t=(lo+hi)*.5;if(GrasslandIslandCoast.Distance(-20+Math.Cos(a)*215*t,470+Math.Sin(a)*290*t)>0)lo=t;else hi=t;}
            double r=(lo+hi)*.5,x=-20+Math.Cos(a)*215*r,z=470+Math.Sin(a)*290*r;
            double e=GrasslandIslandCoast.Extension(x,z);min=Math.Min(min,e);max=Math.Max(max,e);
            Check(e>=2&&e<34,"bounded additive headland");
            Check(GrasslandIslandCoast.ShelfWidth(x,z)>20&&GrasslandIslandCoast.ShelfWidth(x,z)<65,"variable shelf width");
            for(int k=1;k<=112;k++)
            {
                double px=-20+Math.Cos(a)*215*(r+k/215d),pz=470+Math.Sin(a)*290*(r+k/215d);
                double h=GrasslandIslandCoast.Height(px,pz);Check(!double.IsNaN(h)&&!double.IsInfinity(h)&&h<1,"bounded seabed");
            }
        }
        Check(max-min>20,"headlands and recessed bays have distinct silhouettes");
        Check(!BiomeIslandLayout.Contains(0,80)&&!BiomeIslandLayout.Contains(-275,590)&&!BiomeIslandLayout.Contains(270,650)&&!BiomeIslandLayout.Contains(0,850),"islands remain separated");
        Check(!BiomeIslandLayout.Contains(double.NaN,470)&&!BiomeIslandLayout.Contains(0,double.PositiveInfinity),"non-finite positions rejected");
        Console.WriteLine("Grassland coast rules PASSED: "+checks+" checks; "+oldCells+" sampled old construction cells retained; "+newCells+" added cells.");
    }
}
