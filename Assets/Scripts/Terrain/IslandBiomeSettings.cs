using System;
using MagicIsland.Interaction;
using UnityEngine;

namespace MagicIsland.TerrainSystem
{
    public enum IslandBiome { Grassland, Desert, Snow, Beach }

    // Authored in the terrain AA prefab. Region weights never alter land, weather or save rules.
    [Serializable] public sealed class IslandBiomeSettings
    {
        public Vector2 DesertCenter = new Vector2(-13, -1), DesertRadii = new Vector2(8, 13);
        public Vector2 SnowCenter = new Vector2(2, 14), SnowRadii = new Vector2(13, 8);
        [Range(.15f, .7f)] public float EdgeBlend = .45f;
        public Color DesertColor = new Color(.91f, .73f, .42f), SnowColor = new Color(.89f, .94f, .98f);
        public Vector3 NewGameSpawn = new Vector3(1, .05f, -15.8f);
        public int ScenerySeed = 60317;
        [Range(0, 40)] public int DesertPatches = 18, SnowPatches = 22;
        public bool IsValid => Finite(DesertCenter.x) && Finite(DesertCenter.y) && Finite(SnowCenter.x) && Finite(SnowCenter.y)
            && Radii(DesertRadii) && Radii(SnowRadii) && EdgeBlend >= .15f && EdgeBlend <= .7f
            && ColorValid(DesertColor) && ColorValid(SnowColor) && Finite(NewGameSpawn.x) && Finite(NewGameSpawn.y) && Finite(NewGameSpawn.z)
            && Mathf.Abs(NewGameSpawn.y) < 1 && IslandLayout.ContainsLand(NewGameSpawn.x, NewGameSpawn.z, .65)
            && IslandLayout.Height(NewGameSpawn.x, NewGameSpawn.z) >= -.03 && At(NewGameSpawn.x, NewGameSpawn.z) == IslandBiome.Beach
            && ScenerySeed > 0 && DesertPatches >= 0 && DesertPatches <= 40 && SnowPatches >= 0 && SnowPatches <= 40 && DesertPatches + SnowPatches <= 45;
        static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
        static bool Radii(Vector2 v) => Finite(v.x) && Finite(v.y) && v.x >= 3 && v.y >= 3 && v.x <= 30 && v.y <= 30;
        static bool ColorValid(Color c) => Finite(c.r) && Finite(c.g) && Finite(c.b) && Finite(c.a) && c.r >= 0 && c.g >= 0 && c.b >= 0 && c.a >= 0 && c.r <= 1 && c.g <= 1 && c.b <= 1 && c.a <= 1;
        static float Smooth(float a, float b, float v) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(a, b, v));
        float Region(Vector2 p, Vector2 center, Vector2 radii)
        { var d = p - center; return 1 - Smooth(1 - EdgeBlend, 1, new Vector2(d.x / radii.x, d.y / radii.y).magnitude); }
        // X=grass, Y=desert, Z=snow. The original farm/village core and outer beach stay clear.
        public Vector3 Weights(float x, float z)
        {
            float inset = (float)IslandLayout.CoastRadius(Math.Atan2(z, x)) - new Vector2(x, z).magnitude;
            float inland = Smooth(1.8f, 3.3f, inset) * Smooth(7.8f, 10.8f, Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)));
            float desert = Region(new Vector2(x, z), DesertCenter, DesertRadii) * inland;
            float snow = Region(new Vector2(x, z), SnowCenter, SnowRadii) * inland;
            float sum = Mathf.Max(1, desert + snow); desert /= sum; snow /= sum;
            return new Vector3(1 - desert - snow, desert, snow);
        }
        public Color Blend(Color original, float x, float z)
        { var w = Weights(x, z); return original * w.x + DesertColor * w.y + SnowColor * w.z; }
        public IslandBiome At(float x, float z)
        {
            float inset = (float)IslandLayout.CoastRadius(Math.Atan2(z, x)) - new Vector2(x, z).magnitude;
            if (inset < 2.8f) return IslandBiome.Beach;
            var w = Weights(x, z); return w.y > .5f ? IslandBiome.Desert : w.z > .5f ? IslandBiome.Snow : IslandBiome.Grassland;
        }
        public float Weight(IslandBiome biome, float x, float z)
        { var w = Weights(x, z); return biome == IslandBiome.Desert ? w.y : biome == IslandBiome.Snow ? w.z : biome == IslandBiome.Beach ? (At(x, z) == biome ? 1 : 0) : w.x; }
        public static string Name(IslandBiome biome) => biome == IslandBiome.Desert ? "沙漠" : biome == IslandBiome.Snow ? "雪地" : biome == IslandBiome.Beach ? "沙滩" : "草原";
    }
}
