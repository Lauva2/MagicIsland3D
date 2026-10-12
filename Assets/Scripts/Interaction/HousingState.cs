using System;
namespace MagicIsland.Interaction
{
    public sealed class HousingState
    {
        public const string Island = "Island", Home = "PlayerHome";
        public string Area { get; private set; }
        public bool Inside => Area == Home;
        public bool Busy { get; private set; }
        private string target;
        public HousingState(string area = Island)
        { if (area != Island && area != Home) throw new ArgumentException("Unknown player area"); Area = area; }
        public bool Begin(string next)
        {
            if (next != Island && next != Home) throw new ArgumentException("Unknown destination");
            if (Busy || next == Area) return false; Busy = true; target = next; return true;
        }
        public bool Complete()
        { if (!Busy) return false; Area = target; Busy = false; target = null; return true; }
        public void Abort() { Busy = false; target = null; }
        public static bool ValidPosition(string area, double x, double y, double z, int houseLevel = 0)
        {
            if (!Finite(x) || !Finite(y) || !Finite(z) || houseLevel < 0 || houseLevel > 1) return false;
            if (area == Island) return Math.Abs(x) <= 12 && Math.Abs(z) <= 12 && y >= -1 && y <= 10;
            if (area == Home) return x >= -5.8 && x <= 5.8 + houseLevel * 4 && z >= -4.8 && z <= 4.8 + houseLevel * 2 && y >= -.1 && y <= 2;
            return false;
        }
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
