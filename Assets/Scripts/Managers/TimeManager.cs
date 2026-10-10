using MagicIsland.TimeSystem;
using UnityEngine;

namespace MagicIsland.Managers
{
    public sealed class TimeManager : MonoBehaviour
    {
        public ClockState Clock { get; private set; }
        public Transform Bed { get; private set; }
        private float secondsPerMinute;
        public void Initialize(float rate, Transform bed) { Clock = new ClockState(); secondsPerMinute = rate; Bed = bed; }
        public bool InRange(Vector3 position, float reach) => Bed != null
            && Vector3.ProjectOnPlane(Bed.position - position, Vector3.up).sqrMagnitude <= reach * reach;
        public void Tick(float seconds, bool paused) => Clock?.Tick(seconds, secondsPerMinute, paused);
        public void Clear() { Clock = null; Bed = null; }
    }
}
