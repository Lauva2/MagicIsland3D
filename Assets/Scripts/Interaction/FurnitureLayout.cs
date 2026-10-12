using System;
namespace MagicIsland.Interaction
{
    // Keep a continuous route between the default room's entrance, spawn and bed approach.
    public static class FurnitureLayout
    {
        public static bool Overlaps(int x, int z, double minX, double maxX, double minZ, double maxZ)
            => x + .5 > minX && x - .5 < maxX && z + .5 > minZ && z - .5 < maxZ;
        public static bool Reserved(int x, int z)
            => Overlaps(x, z, -.4, .4, -4.8, -2.1)
            || Overlaps(x, z, -1.9, .4, -2.9, -2.1)
            || Overlaps(x, z, -1.9, -1.1, -2.9, 2.4);
    }
}
