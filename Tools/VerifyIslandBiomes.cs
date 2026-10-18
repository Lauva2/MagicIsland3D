using System;
using MagicIsland.Interaction;
using MagicIsland.TerrainSystem;
using UnityEngine;

class VerifyIslandBiomes
{
    static int checks;
    static void Require(bool b,string why){checks++;if(!b)throw new Exception(why);}
    static int Main()
    {
        var s=new IslandBiomeSettings();Require(s.IsValid,"valid authored defaults");
        Require(s.At(-13,0)==IslandBiome.Desert&&s.At(2,14)==IslandBiome.Snow&&s.At(0,0)==IslandBiome.Grassland,"distinct biomes");
        Require(s.At(s.NewGameSpawn.x,s.NewGameSpawn.z)==IslandBiome.Beach&&IslandLayout.ContainsLand(s.NewGameSpawn.x,s.NewGameSpawn.z,.65),"full beach spawn footprint");
        foreach(float invalid in new[]{float.NaN,float.NegativeInfinity,float.PositiveInfinity})
        {
            s.EdgeBlend=invalid;Require(!s.IsValid,"reject nonfinite blend");s=new IslandBiomeSettings();
            s.DesertCenter=new Vector2(invalid,0);Require(!s.IsValid,"reject nonfinite region");s=new IslandBiomeSettings();
            s.SnowRadii=new Vector2(8,invalid);Require(!s.IsValid,"reject nonfinite dimensions");s=new IslandBiomeSettings();
            s.NewGameSpawn=new Vector3(invalid,0,0);Require(!s.IsValid,"reject nonfinite spawn");s=new IslandBiomeSettings();
            s.SnowColor=new Color(invalid,1,1);Require(!s.IsValid,"reject nonfinite palette");s=new IslandBiomeSettings();
            s.DesertColor=new Color(1,1,1,invalid);Require(!s.IsValid,"reject nonfinite alpha");s=new IslandBiomeSettings();
        }
        s.NewGameSpawn=new Vector3(22,.05f,0);Require(!s.IsValid,"reject ocean spawn");s=new IslandBiomeSettings();
        s.NewGameSpawn=new Vector3(0,.05f,0);Require(!s.IsValid,"reject inland start in beach-start configuration");s=new IslandBiomeSettings();
        s.DesertPatches=41;s.SnowPatches=0;Require(!s.IsValid,"reject individual population above inspector bound");s=new IslandBiomeSettings();
        s.DesertPatches=40;s.SnowPatches=40;Require(!s.IsValid,"bounded total scenery");s=new IslandBiomeSettings();
        s.DesertRadii=Vector2.one;Require(!s.IsValid,"reject tiny unsupported region");s=new IslandBiomeSettings();
        s.DesertColor=new Color(1.01f,1,1);Require(!s.IsValid,"reject illegal palette");s=new IslandBiomeSettings();
        // Visual settings do not alter placement/safe-position semantics at any old island coordinate.
        for(int x=-17;x<=17;x++)for(int z=-17;z<=17;z++)
        {
            var a=s.Weights(x,z);var b=s.Weights(x,z);
            Require(a==b&&Mathf.Abs(a.x+a.y+a.z-1)<.00001f&&a.x>=-.00001f&&a.y>=0&&a.z>=0,"deterministic normalized weights");
            if(Math.Abs(x)<=12&&Math.Abs(z)<=12)Require(IslandLayout.ContainsLand(x,z,.3)&&IslandLayout.CanPlace(x,z)&&IslandLayout.Height(x,z)==0,"legacy 25x25 square retains its flat walkable / placeable footprint");
        }
        for(float x=-7.5f;x<=7.5f;x+=.25f)for(float z=-7.5f;z<=7.5f;z+=.25f)Require(s.Weights(x,z)==Vector3.right,"largest farm stays inside grassland core");
        var before=s.Blend(new Color(.56f,.72f,.34f),-13,0);s.DesertColor=new Color(.75f,.61f,.4f);
        Require(s.IsValid&&s.Blend(new Color(.56f,.72f,.34f),-13,0)!=before,"authored palette affects desert");
        Require(s.Weights(0,0)==Vector3.right&&s.At(2,14)==IslandBiome.Snow,"palette tuning does not move other regions");
        Console.WriteLine("PASS: "+checks+" biome rules, invalid settings, preserved old footprint and maximum farm, deterministic regions and authored palette assertions");return 0;
    }
}
