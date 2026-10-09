using System;

namespace MagicIsland.Farming
{
    public enum FarmTool { Hoe, Seeds, WateringCan }

    [Serializable]
    public sealed class FarmCell
    {
        public bool Tilled;
        public bool Planted;
        public bool Watered;
        public int Growth;
        public int RequiredGrowthDays = FarmState.GrowthDays;
        public bool Mature => Planted && Growth >= RequiredGrowthDays;
    }

    // Pure gameplay state: no scene objects, input or visual dependencies.
    public sealed class FarmState
    {
        public const int GrowthDays = 3;
        public int Size { get; }
        public int Day { get; private set; } = 1;
        public FarmCell[] Cells { get; }

        public FarmState(int size, int growthDays = GrowthDays)
        {
            if (size <= 0) throw new ArgumentOutOfRangeException(nameof(size));
            if (growthDays < 1) throw new ArgumentOutOfRangeException(nameof(growthDays));
            Size = size;
            Cells = new FarmCell[size * size];
            for (int i = 0; i < Cells.Length; i++) Cells[i] = new FarmCell { RequiredGrowthDays = growthDays };
        }

        public string Describe(int index, FarmTool tool)
        {
            FarmCell cell = Cells[index];
            if (cell.Mature) return "收获";
            switch (tool)
            {
                case FarmTool.Hoe: return cell.Tilled ? "土地已耕作" : "耕地";
                case FarmTool.Seeds:
                    if (!cell.Tilled) return "先用锄头耕地";
                    if (cell.Planted) return "已有作物";
                    return "播种";
                default:
                    if (!cell.Planted) return "先种植作物";
                    return cell.Watered ? "今天已浇水" : "浇水";
            }
        }

        public bool TryAct(int index, FarmTool tool, out string message)
        {
            message = Describe(index, tool);
            FarmCell cell = Cells[index];
            if (cell.Mature)
            {
                cell.Planted = false;
                cell.Watered = false;
                cell.Growth = 0;
                return true;
            }
            if (tool == FarmTool.Hoe && !cell.Tilled)
            {
                cell.Tilled = true;
                return true;
            }
            if (tool == FarmTool.Seeds && cell.Tilled && !cell.Planted)
            {
                cell.Planted = true;
                return true;
            }
            if (tool == FarmTool.WateringCan && cell.Planted && !cell.Watered)
            {
                cell.Watered = true;
                return true;
            }
            return false;
        }

        public void AdvanceDay()
        {
            foreach (FarmCell cell in Cells)
            {
                if (cell.Planted && cell.Watered && !cell.Mature) cell.Growth++;
                cell.Watered = false;
            }
            Day++;
        }

        public void Restore(int day, FarmCell[] saved)
        {
            if (day < 1 || saved == null || saved.Length != Cells.Length) throw new ArgumentException("Invalid saved farm");
            for (int i = 0; i < saved.Length; i++)
            {
                FarmCell cell = saved[i];
                if (cell == null || cell.RequiredGrowthDays != Cells[i].RequiredGrowthDays
                    || cell.Growth < 0 || cell.Growth > cell.RequiredGrowthDays
                    || (cell.Planted && !cell.Tilled) || (cell.Watered && !cell.Planted)
                    || (!cell.Planted && cell.Growth != 0)) throw new ArgumentException("Invalid saved farm cell");
            }
            Day = day;
            for (int i = 0; i < saved.Length; i++)
            {
                Cells[i].Tilled = saved[i].Tilled;
                Cells[i].Planted = saved[i].Planted;
                Cells[i].Watered = saved[i].Watered;
                Cells[i].Growth = saved[i].Growth;
            }
        }
    }
}
