using System;
namespace MagicIsland.Economy
{
    public sealed class EconomyState
    {
        public long Gold { get; private set; }
        public event Action Changed;
        private Action<long> ledger;
        internal void SetLedger(Action<long> recorder) => ledger = recorder;
        public EconomyState(long gold) { if (gold < 0) throw new ArgumentOutOfRangeException(nameof(gold)); Gold = gold; }
        public bool CanEarn(long amount) => amount > 0 && Gold <= long.MaxValue - amount;
        public bool TrySpend(long amount, bool notify = true)
        { if (amount <= 0 || Gold < amount) return false; Gold -= amount; ledger?.Invoke(-amount); if (notify) NotifyChanged(); return true; }
        public bool TryEarn(long amount, bool notify = true)
        { if (!CanEarn(amount)) return false; Gold += amount; ledger?.Invoke(amount); if (notify) NotifyChanged(); return true; }
        public void Restore(long gold) { if (gold < 0) throw new ArgumentOutOfRangeException(nameof(gold)); Gold = gold; NotifyChanged(); }
        public void NotifyChanged() => Changed?.Invoke();
    }
}
