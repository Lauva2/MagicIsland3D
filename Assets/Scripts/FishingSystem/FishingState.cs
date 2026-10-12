using System;
using System.Collections.Generic;
using System.Linq;
using MagicIsland.Inventory;
using MagicIsland.Stamina;
namespace MagicIsland.FishingSystem
{
    public sealed class FishDefinition
    {
        public string ItemId { get; }
        public int Weight { get; }
        public FishDefinition(string id, int weight)
        { if (string.IsNullOrWhiteSpace(id) || weight < 1 || weight > 10000) throw new ArgumentException("Invalid fish"); ItemId = id; Weight = weight; }
    }
    public enum FishingPhase { Idle, Waiting, Bite, Caught, Missed, Cancelled }
    public enum FishingResult { Success, Busy, NotActive, TooEarly, TooLate, BagFull, StaminaLow }
    // A session is temporary. Only a successful catch changes saved inventory and stamina.
    public sealed class FishingState
    {
        public IReadOnlyList<FishDefinition> Fish { get; }
        public FishingPhase Phase { get; private set; }
        public double Remaining { get; private set; }
        public double WaitDuration { get; private set; }
        public double BiteWindow { get; }
        public int StaminaCost { get; }
        public string CaughtItemId { get; private set; }
        public bool Active => Phase == FishingPhase.Waiting || Phase == FishingPhase.Bite;
        private string pendingFish;
        private bool committing;
        public event Action Changed;
        public FishingState(IEnumerable<FishDefinition> fish, double biteWindow = 2, int staminaCost = 5)
        {
            var values = fish?.ToArray();
            if (values == null || values.Length < 1 || values.Length > 8 || values.Any(f => f == null) || values.Select(f => f.ItemId).Distinct().Count() != values.Length
                || !Finite(biteWindow) || biteWindow <= 0 || staminaCost < 1) throw new ArgumentException("Invalid fishing rules");
            Fish = Array.AsReadOnly(values); BiteWindow = biteWindow; StaminaCost = staminaCost;
        }
        public int Pick(double sample)
        {
            if (!Finite(sample) || sample < 0 || sample >= 1) throw new ArgumentException("Invalid fishing sample");
            double target = sample * Fish.Sum(f => f.Weight); int total = 0;
            for (int i = 0; i < Fish.Count; i++) { total += Fish[i].Weight; if (target < total) return i; }
            return Fish.Count - 1;
        }
        public FishingResult CheckCast(InventoryState bag, StaminaState stamina)
        {
            if (committing || Active) return FishingResult.Busy;
            if (!stamina.CanSpend(StaminaCost)) return FishingResult.StaminaLow;
            // Reserve enough potential capacity for either fish, without revealing or rerolling the catch.
            return Fish.Any(f => !bag.CanAdd(f.ItemId, 1)) ? FishingResult.BagFull : FishingResult.Success;
        }
        public FishingResult Cast(int fishIndex, double wait, InventoryState bag, StaminaState stamina)
        {
            if (fishIndex < 0 || fishIndex >= Fish.Count || !Finite(wait) || wait <= 0) throw new ArgumentException("Invalid cast");
            var result = CheckCast(bag, stamina); if (result != FishingResult.Success) return result;
            pendingFish = Fish[fishIndex].ItemId; CaughtItemId = null; WaitDuration = Remaining = wait; Phase = FishingPhase.Waiting; Changed?.Invoke(); return result;
        }
        public void Tick(double seconds)
        {
            if (!Finite(seconds) || seconds < 0) throw new ArgumentException("Invalid fishing time");
            if (committing || !Active || seconds == 0) return;
            Remaining -= seconds;
            if (Phase == FishingPhase.Waiting && Remaining <= 0) { Phase = FishingPhase.Bite; Remaining += BiteWindow; }
            if (Phase == FishingPhase.Bite && Remaining <= 0) { Phase = FishingPhase.Missed; Remaining = 0; pendingFish = null; }
            Changed?.Invoke();
        }
        public FishingResult Reel(InventoryState bag, StaminaState stamina)
        {
            if (committing) return FishingResult.Busy;
            if (!Active) return FishingResult.NotActive;
            if (Phase == FishingPhase.Waiting) { Phase = FishingPhase.Missed; Remaining = 0; pendingFish = null; Changed?.Invoke(); return FishingResult.TooEarly; }
            if (Remaining <= 0) return FishingResult.TooLate;
            var result = !stamina.CanSpend(StaminaCost) ? FishingResult.StaminaLow : !bag.CanAdd(pendingFish, 1) ? FishingResult.BagFull : FishingResult.Success;
            committing = true;
            try
            {
                Phase = result == FishingResult.Success ? FishingPhase.Caught : FishingPhase.Missed; Remaining = 0;
                if (result == FishingResult.Success)
                {
                    bag.TryAdd(pendingFish, 1, false); stamina.TrySpend(StaminaCost, false); CaughtItemId = pendingFish;
                    pendingFish = null; bag.NotifyChanged(); stamina.NotifyChanged();
                }
                else pendingFish = null;
                Changed?.Invoke(); return result;
            }
            finally { committing = false; }
        }
        public void Cancel()
        { if (committing || !Active) return; Phase = FishingPhase.Cancelled; Remaining = 0; pendingFish = null; Changed?.Invoke(); }
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
