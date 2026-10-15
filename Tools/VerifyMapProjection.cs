using System;
using MagicIsland.Interaction;
internal static class VerifyMapProjection
{
    static int checks;
    static void Check(bool ok, string why) { checks++; if (!ok) throw new Exception(why); }
    static void Reject(double coordinate) { bool rejected = false; try { MapProjection.Normalize(coordinate); } catch (ArgumentOutOfRangeException) { rejected = true; } Check(rejected, "Reject non-finite map coordinate"); }
    public static int Main()
    {
        Check(MapProjection.Normalize(0) == .5, "Island origin is map centre");
        Check(MapProjection.Normalize(-24) == 0 && MapProjection.Normalize(24) == 1, "World bounds map exactly to edges");
        Check(MapProjection.Normalize(-25) == 0 && MapProjection.Normalize(25) == 1, "External points remain inside panel");
        for (int i = -240; i <= 240; i++)
        {
            double value = i / 10.0; double mapped = MapProjection.Normalize(value);
            Check(mapped >= 0 && mapped <= 1, "Coordinate lies within map bounds");
            Check(Math.Abs(MapProjection.Normalize(-value) + mapped - 1) < 1e-12, "East/west and north/south projection stays symmetric");
        }
        for (int i = 0; i < 360; i++)
        {
            double angle = i * Math.PI / 180, radius = IslandLayout.CoastRadius(angle) + 1.5;
            double x = MapProjection.Normalize(Math.Cos(angle) * radius), z = MapProjection.Normalize(Math.Sin(angle) * radius);
            Check(x > 0 && x < 1 && z > 0 && z < 1, "Entire shoreline and shallows stay within map");
        }
        Reject(double.NaN); Reject(double.PositiveInfinity); Reject(double.NegativeInfinity);
        Console.WriteLine("PASS: " + checks + " map projection and shoreline assertions"); return 0;
    }
}
