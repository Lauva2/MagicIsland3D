using MagicIsland.Economy;
using UnityEngine;
namespace MagicIsland.Managers
{
    public sealed class EconomyManager : MonoBehaviour
    {
        public EconomyState State { get; private set; }
        public long Gold => State?.Gold ?? 0;
        public void Initialize(long gold) => State = new EconomyState(gold);
        public bool TrySpend(long amount) => State != null && State.TrySpend(amount);
        public bool TryEarn(long amount) => State != null && State.TryEarn(amount);
        public void Restore(long gold) => State.Restore(gold);
        public void Clear() => State = null;
    }
}
