using System;
using System.Collections.Generic;
using System.Linq;

namespace MagicIsland.Interaction
{
    public readonly struct SchedulePoint
    {
        public readonly double X, Z;
        public SchedulePoint(double x, double z) { X = x; Z = z; }
        public static double Distance(SchedulePoint a, SchedulePoint b)
        { double x = a.X - b.X, z = a.Z - b.Z; return Math.Sqrt(x * x + z * z); }
    }

    public sealed class NPCScheduleDefinition
    {
        public string Id { get; }
        public double Speed { get; }
        public int CoastMinute { get; }
        public int ReturnMinute { get; }
        public double Length { get; }
        private readonly SchedulePoint[] points;
        private readonly double[] distances;
        public IReadOnlyList<SchedulePoint> Points => Array.AsReadOnly(points);
        public NPCScheduleDefinition(string id, double speed, int coastMinute, int returnMinute, IEnumerable<SchedulePoint> route)
        {
            if (string.IsNullOrWhiteSpace(id) || !Finite(speed) || speed < .2 || speed > 4
                || coastMinute < 1 || returnMinute <= coastMinute || returnMinute > 1440 || route == null)
                throw new ArgumentException("Invalid NPC schedule");
            var list = new List<SchedulePoint>();
            foreach (var point in route)
            {
                if (!Finite(point.X) || !Finite(point.Z) || !IslandLayout.ContainsLand(point.X,point.Z,.4))
                    throw new ArgumentException("Schedule point outside island");
                if (list.Count == 0 || SchedulePoint.Distance(list[list.Count - 1], point) > .00001) list.Add(point);
            }
            if (list.Count < 2 || list.Count > 16) throw new ArgumentException("Schedule needs a route");
            points = list.ToArray(); distances = new double[points.Length];
            for (int i = 1; i < points.Length; i++) distances[i] = distances[i - 1] + SchedulePoint.Distance(points[i - 1], points[i]);
            Id = id; Speed = speed; CoastMinute = coastMinute; ReturnMinute = returnMinute; Length = distances[distances.Length - 1];
        }
        public double Target(double minute) => minute >= CoastMinute && minute < ReturnMinute ? Length : 0;
        public SchedulePoint Position(double distance)
        {
            if (!Finite(distance) || distance < 0 || distance > Length) throw new ArgumentException("Invalid route distance");
            for (int i = 1; i < points.Length; i++) if (distance <= distances[i])
            {
                double t = (distance - distances[i - 1]) / (distances[i] - distances[i - 1]);
                return new SchedulePoint(points[i - 1].X + (points[i].X - points[i - 1].X) * t,
                    points[i - 1].Z + (points[i].Z - points[i - 1].Z) * t);
            }
            return points[points.Length - 1];
        }
        internal double NextCorner(double distance, bool forward)
        {
            if (forward) { foreach (double value in distances) if (value > distance + .0000001) return value; return Length; }
            for (int i = distances.Length - 1; i >= 0; i--) if (distances[i] < distance - .0000001) return distances[i];
            return 0;
        }
        public bool AvoidsFarm(int size)
        {
            double half = size * .6 + .5;
            for (int i = 1; i < points.Length; i++)
            {
                // The default corridor uses orthogonal legs; require the entire leg to stay outside the farm clearance.
                var a = points[i - 1]; var b = points[i];
                if (!(Math.Min(a.X, b.X) > half || Math.Max(a.X, b.X) < -half
                    || Math.Min(a.Z, b.Z) > half || Math.Max(a.Z, b.Z) < -half)) return false;
            }
            return true;
        }
        internal static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }

    [Serializable] public sealed class SavedNPCSchedule { public string Id; public double Distance; }

    public sealed class NPCScheduleState
    {
        private readonly NPCScheduleDefinition[] definitions;
        private readonly Dictionary<string, double> progress = new Dictionary<string, double>(StringComparer.Ordinal);
        public IReadOnlyList<NPCScheduleDefinition> Definitions => Array.AsReadOnly(definitions);
        public double Minute { get; private set; }
        public NPCScheduleState(IEnumerable<NPCScheduleDefinition> definitions, double minute = 360)
        {
            if (definitions == null) throw new ArgumentException("Missing schedules");
            this.definitions = definitions.ToArray();
            if (this.definitions.Length < 1 || this.definitions.Length > 8 || this.definitions.Any(d => d == null)
                || this.definitions.Select(d => d.Id).Distinct(StringComparer.Ordinal).Count() != this.definitions.Length)
                throw new ArgumentException("Invalid schedules");
            Reset(minute);
        }
        public double Distance(string id) => progress[id];
        public SchedulePoint Position(string id) => Find(id).Position(progress[id]);
        public bool Moving(string id) => Math.Abs(progress[id] - Find(id).Target(Minute)) > .0000001;
        private NPCScheduleDefinition Find(string id) => definitions.First(d => d.Id == id);
        public void Reset(double minute)
        {
            ValidateMinute(minute); Minute = minute;
            foreach (var d in definitions) progress[d.Id] = d.Target(minute);
        }
        public void Tick(double minute, double seconds, bool paused = false,
            Func<string, SchedulePoint, SchedulePoint, bool> canMove = null, Func<string, bool> focused = null)
        {
            ValidateMinute(minute);
            if (!NPCScheduleDefinition.Finite(seconds) || seconds < 0) throw new ArgumentException("Invalid elapsed time");
            Minute = minute; if (paused || seconds == 0) return;
            foreach (var d in definitions)
            {
                if (focused != null && focused(d.Id)) continue;
                double current = progress[d.Id], target = d.Target(minute), budget = seconds * d.Speed;
                while (Math.Abs(target - current) > .0000001 && budget > .0000001)
                {
                    bool forward = target > current;
                    double step = Math.Min(budget, Math.Abs(d.NextCorner(current, forward) - current));
                    if (step <= .0000001) break;
                    double next = current + (forward ? step : -step);
                    next = Math.Max(0, Math.Min(d.Length, next));
                    if (canMove != null && !canMove(d.Id, d.Position(current), d.Position(next))) break;
                    budget -= Math.Abs(next - current); current = next; progress[d.Id] = current;
                }
            }
        }
        public SavedNPCSchedule[] Capture() => definitions.Select(d => new SavedNPCSchedule { Id = d.Id, Distance = progress[d.Id] }).ToArray();
        public void Restore(SavedNPCSchedule[] saved)
        {
            if (saved == null || saved.Length != definitions.Length) throw new ArgumentException("Schedule save count mismatch");
            for (int i = 0; i < definitions.Length; i++)
                if (saved[i] == null || saved[i].Id != definitions[i].Id || !NPCScheduleDefinition.Finite(saved[i].Distance)
                    || saved[i].Distance < 0 || saved[i].Distance > definitions[i].Length) throw new ArgumentException("Invalid schedule progress");
            for (int i = 0; i < definitions.Length; i++) progress[saved[i].Id] = saved[i].Distance;
        }
        private static void ValidateMinute(double minute)
        { if (!NPCScheduleDefinition.Finite(minute) || minute < 0 || minute > 1440) throw new ArgumentException("Invalid schedule clock"); }
    }
}
