using MagicIsland.Configuration;
using MagicIsland.Stamina;
using UnityEngine;

namespace MagicIsland.Managers
{
    public sealed class StaminaManager : MonoBehaviour
    {
        public StaminaState State { get; private set; }
        public void Initialize(GameConfig config) => State = new StaminaState(config.MaxStamina);
        public void Restore(int current) => State.Restore(current);
        public void Refill() => State.Refill();
        public void Clear() => State = null;
    }
}
