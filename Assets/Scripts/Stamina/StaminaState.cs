using System;

namespace MagicIsland.Stamina
{
    public sealed class StaminaState
    {
        public int Maximum { get; }
        public int Current { get; private set; }
        public event Action Changed;
        public StaminaState(int maximum)
        {
            if (maximum < 1) throw new ArgumentOutOfRangeException(nameof(maximum));
            Maximum = maximum; Current = maximum;
        }
        public bool CanSpend(int amount) => amount >= 0 && Current >= amount;
        public bool TrySpend(int amount, bool notify = true)
        {
            if (!CanSpend(amount)) return false;
            Current -= amount;
            if (notify && amount > 0) NotifyChanged();
            return true;
        }
        public void Restore(int current)
        {
            if (current < 0 || current > Maximum) throw new ArgumentOutOfRangeException(nameof(current));
            if (Current == current) return;
            Current = current; NotifyChanged();
        }
        public void Refill() => Restore(Maximum);
        public void NotifyChanged() => Changed?.Invoke();
    }
}
