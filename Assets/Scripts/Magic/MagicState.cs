using System;
using MagicIsland.Farming;
using MagicIsland.Inventory;
using MagicIsland.Stamina;

namespace MagicIsland.Magic
{
    public enum MagicStage { Unstudied, GrowingMoonflowers, OfferingMoonflower, RainLearned }
    public enum MagicResult { Success, Busy, WrongStage, BagFull, MissingFlower, Locked, InvalidPlot, NothingToWater, InsufficientStamina }
    [Serializable] public sealed class SavedMagic { public MagicStage Stage; }

    public sealed class MagicDefinition
    {
        public string CropId { get; }
        public string SeedId { get; }
        public string FlowerId { get; }
        public string QualityFlowerId { get; }
        public int GiftSeeds { get; }
        public int RainRadius { get; }
        public int RainCost { get; }
        public MagicDefinition(string cropId, string seedId, string flowerId, string qualityFlowerId, int giftSeeds = 3, int rainRadius = 1, int rainCost = 6)
        {
            if (string.IsNullOrWhiteSpace(cropId) || string.IsNullOrWhiteSpace(seedId) || string.IsNullOrWhiteSpace(flowerId)
                || string.IsNullOrWhiteSpace(qualityFlowerId) || seedId == flowerId || flowerId == qualityFlowerId || seedId == qualityFlowerId
                || giftSeeds < 1 || giftSeeds > 99 || rainRadius < 1 || rainRadius > 2 || rainCost < 1 || rainCost > 100)
                throw new ArgumentException("Invalid magic definition");
            CropId = cropId; SeedId = seedId; FlowerId = flowerId; QualityFlowerId = qualityFlowerId;
            GiftSeeds = giftSeeds; RainRadius = rainRadius; RainCost = rainCost;
        }
    }

    // The caller owns range, input and action-lock checks. No notifications precede the full commit.
    public sealed class MagicState
    {
        public MagicStage Stage { get; private set; }
        public bool RainLearned => Stage == MagicStage.RainLearned;
        public MagicDefinition Definition { get; }
        private bool committing;
        public event Action Changed;
        public MagicState(MagicDefinition definition) => Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        public SavedMagic Capture() => new SavedMagic { Stage = Stage };
        public static bool Valid(SavedMagic saved) => saved != null && Enum.IsDefined(typeof(MagicStage), saved.Stage);
        public void Restore(SavedMagic saved)
        {
            if (committing || !Valid(saved)) throw new ArgumentException("Invalid saved magic progress");
            Stage = saved.Stage;
        }
        public MagicResult CheckStudy(InventoryState inventory)
        {
            if (committing || inventory == null || !inventory.CanSort) return MagicResult.Busy;
            if (Stage == MagicStage.Unstudied) return inventory.CanAdd(Definition.SeedId, Definition.GiftSeeds) ? MagicResult.Success : MagicResult.BagFull;
            if (Stage != MagicStage.OfferingMoonflower) return MagicResult.WrongStage;
            return inventory.GetCount(Definition.FlowerId) > 0 || inventory.GetCount(Definition.QualityFlowerId) > 0 ? MagicResult.Success : MagicResult.MissingFlower;
        }
        public MagicResult Study(InventoryState inventory)
        {
            var result = CheckStudy(inventory); if (result != MagicResult.Success) return result;
            committing = true;
            try
            {
                if (Stage == MagicStage.Unstudied)
                {
                    if (!inventory.TryAdd(Definition.SeedId, Definition.GiftSeeds, false)) return MagicResult.BagFull;
                    Stage = MagicStage.GrowingMoonflowers;
                }
                else
                {
                    string item = inventory.GetCount(Definition.FlowerId) > 0 ? Definition.FlowerId : Definition.QualityFlowerId;
                    if (!inventory.TryRemove(item, 1, false)) return MagicResult.MissingFlower;
                    Stage = MagicStage.RainLearned;
                }
                inventory.NotifyChanged(); Changed?.Invoke(); return MagicResult.Success;
            }
            finally { committing = false; }
        }
        // Called inside the successful harvest commit, before inventory observers. Publish after the farm action returns.
        internal void RecordHarvest(string cropId)
        { if (!committing && Stage == MagicStage.GrowingMoonflowers && cropId == Definition.CropId) Stage = MagicStage.OfferingMoonflower; }
        internal void NotifyChanged() => Changed?.Invoke();
        public MagicResult CheckRain(FarmState farm, StaminaState stamina, int center)
        {
            if (committing) return MagicResult.Busy;
            if (!RainLearned) return MagicResult.Locked;
            if (farm == null || center < 0 || center >= farm.Cells.Length || !farm.Cells[center].Tilled) return MagicResult.InvalidPlot;
            if (CountDry(farm, center) == 0) return MagicResult.NothingToWater;
            return stamina != null && stamina.CanSpend(Definition.RainCost) ? MagicResult.Success : MagicResult.InsufficientStamina;
        }
        private bool Within(FarmState farm, int center, int index) => Math.Abs(center % farm.Size - index % farm.Size) <= Definition.RainRadius
            && Math.Abs(center / farm.Size - index / farm.Size) <= Definition.RainRadius;
        private static bool Dry(FarmCell cell) => cell.Planted && !cell.Mature && !cell.Watered;
        public int CountDry(FarmState farm, int center)
        {
            int count = 0; for (int i = 0; i < farm.Cells.Length; i++) if (Within(farm, center, i) && Dry(farm.Cells[i])) count++; return count;
        }
        public MagicResult CastRain(FarmState farm, StaminaState stamina, int center, out int watered)
        {
            watered = 0; var result = CheckRain(farm, stamina, center); if (result != MagicResult.Success) return result;
            committing = true;
            try
            {
                for (int i = 0; i < farm.Cells.Length; i++) if (Within(farm, center, i) && Dry(farm.Cells[i])) { farm.Cells[i].Watered = true; watered++; }
                stamina.TrySpend(Definition.RainCost, false);
                stamina.NotifyChanged(); Changed?.Invoke(); return MagicResult.Success;
            }
            finally { committing = false; }
        }
    }
}
