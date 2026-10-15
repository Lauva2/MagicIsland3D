using System;
namespace MagicIsland.Interaction
{
    public static class MapProjection
    {
        public const double Extent = 24;
        public static double Normalize(double coordinate)
        {
            if (double.IsNaN(coordinate) || double.IsInfinity(coordinate)) throw new ArgumentOutOfRangeException(nameof(coordinate));
            return Math.Max(0, Math.Min(1, .5 + coordinate / (Extent * 2)));
        }
    }
}
