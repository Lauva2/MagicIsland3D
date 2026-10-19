using UnityEngine;

namespace MagicIsland.TerrainSystem
{
    // A fixed visual trail, driven by WeatherManager's outdoor clock. No gameplay state or allocations per tick.
    public sealed class MeadowTouchSimulation
    {
        public const int Capacity = 16;
        readonly MeadowTouchSettings settings;
        readonly Vector2 center;
        readonly Vector4[] points = new Vector4[Capacity];
        readonly float[] ages = new float[Capacity];
        Vector3 previous, anchor;
        bool initialized;
        float arrival;
        public int Count { get; private set; }
        public Vector4 Contact(int index) => points[index];
        internal Vector4[] Points => points;
        public MeadowTouchSimulation(MeadowTouchSettings settings, Vector2 worldCenter)
        { if(settings.ArtVersion!=1||!settings.IsValid)throw new System.ArgumentException("A validated AA meadow sample is required.",nameof(settings));this.settings = settings; center = worldCenter; Reset(); }
        public void Reset()
        { System.Array.Clear(points, 0, points.Length); for(int i=0;i<ages.Length;i++)ages[i]=settings.RecoverySeconds; Count = 0; initialized = false; arrival = 0; }
        public bool Tick(float seconds, Vector3? player, bool paused, bool outside)
        {
            if (!outside || !player.HasValue)
            { bool changed = Count != 0; Reset(); return changed; }
            int oldCount=Count;
            var current = player.Value;
            if (float.IsNaN(current.sqrMagnitude) || float.IsInfinity(current.sqrMagnitude)) { Reset(); return true; }
            if (initialized && ((current - previous).sqrMagnitude > 16 || Mathf.Abs(current.y - previous.y) > 1.5f)) Reset();
            if(paused)return oldCount!=Count;
            if(seconds<=0||float.IsNaN(seconds)||float.IsInfinity(seconds))return oldCount!=Count;
            if (!initialized) { anchor = previous = current; initialized = true; }
            for (int i = 1; i < Capacity; i++)
            {
                ages[i] += seconds;
                points[i].w = Inside(new Vector3(points[i].x,points[i].z,points[i].y)) ? 1-Mathf.SmoothStep(0,1,ages[i]/settings.RecoverySeconds) : 0;
            }
            var movement = current - anchor; float distance = new Vector2(movement.x, movement.z).magnitude;
            float moved=Vector3.Distance(current,previous);float speed=moved/seconds;
            float spacing=Mathf.Max(settings.Spacing,speed*settings.RecoverySeconds/(Capacity-3));
            int samples = Mathf.Min(Capacity - 1, Mathf.FloorToInt(distance / spacing));
            if(moved<.0001f)samples=0;
            for (int s = 0; s < samples; s++)
            {
                float fraction = (s + 1) * spacing / distance;
                var point = Vector3.Lerp(anchor, current, fraction);
                for (int i = Capacity - 1; i > 1; i--) { points[i] = points[i - 1]; ages[i] = ages[i - 1]; }
                ages[1] = seconds * (1 - fraction);
                float strength = 1 - Mathf.SmoothStep(0, 1, ages[1] / settings.RecoverySeconds);
                points[1] = new Vector4(point.x, point.z, point.y, Inside(point)?strength:0);
            }
            if (samples > 0) anchor = Vector3.Lerp(anchor, current, samples * spacing / distance);
            arrival = Mathf.MoveTowards(arrival, 1, seconds / .12f);
            bool inside = Inside(current);
            points[0] = new Vector4(current.x, current.z, current.y, inside ? arrival : 0);
            // Invalid/unused slots start expired, not as contacts at the world origin.
            Count = points[0].w > 0 ? 1 : 0;
            for (int i = 1; i < Capacity; i++) if (points[i].w > 0 && ages[i] < settings.RecoverySeconds) Count = i + 1;
            previous = current;
            return Count>0||oldCount>0;
        }
        bool Inside(Vector3 point)
        {float radius=settings.AreaRadius+settings.InfluenceRadius;return (new Vector2(point.x,point.z)-center).sqrMagnitude<radius*radius;}
    }
}
