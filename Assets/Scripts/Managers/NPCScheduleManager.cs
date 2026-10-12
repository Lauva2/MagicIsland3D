using System;
using System.Collections.Generic;
using MagicIsland.Configuration;
using MagicIsland.Interaction;
using UnityEngine;

namespace MagicIsland.Managers
{
    public sealed class NPCScheduleManager : MonoBehaviour
    {
        public NPCScheduleState State { get; private set; }
        public NPCScheduleCatalog Catalog { get; private set; }
        public string LastError { get; private set; }
        public bool Bound { get; private set; }
        private NPCManager npcs;
        private readonly Dictionary<string, CapsuleCollider> capsules = new Dictionary<string, CapsuleCollider>();
        public void Initialize(NPCScheduleCatalog catalog, GameConfig game, NPCManager npcs, FishingCatalog fishing,
            double minute, SavedNPCSchedule[] saved = null)
        {
            Clear(); Catalog = catalog; this.npcs = npcs;
            State = new NPCScheduleState(catalog.Definitions(game, npcs.Catalog, fishing), minute);
            if (saved != null) State.Restore(saved);
        }
        public bool Bind()
        {
            if (Bound) return true;
            LastError = null; Physics.SyncTransforms();
            try
            {
                foreach (var d in State.Definitions)
                {
                    if (!npcs.Actors.TryGetValue(d.Id, out var actor)) throw new InvalidOperationException("Missing scheduled NPC: " + d.Id);
                    var c = actor.GetComponent<CapsuleCollider>(); var scale = actor.transform.lossyScale;
                    if (c == null || !c.enabled || c.isTrigger || c.direction != 1 || c.center.x != 0 || c.center.z != 0
                        || c.radius <= 0 || c.radius * Mathf.Max(scale.x, scale.z) > .35f || c.height < c.radius * 2
                        || scale.x <= 0 || scale.y <= 0 || scale.z <= 0)
                        throw new InvalidOperationException("Scheduled NPC needs an upright capsule with radius <= .35: " + d.Id);
                    capsules.Add(d.Id, c);
                    for (int i = 1; i < d.Points.Count; i++)
                        if (!ClearPath(d.Id, d.Points[i - 1], d.Points[i], true)) throw new InvalidOperationException("NPC schedule route blocked: " + d.Id);
                }
                Bound = true; Apply(true); return true;
            }
            catch (Exception error) { LastError = error.Message; capsules.Clear(); Bound = false; return false; }
        }
        public void Tick(double minute, double seconds, bool paused)
        {
            if (!Bound) return;
            if (paused || seconds == 0) { State.Tick(minute, seconds, paused); return; }
            Physics.SyncTransforms();
            var before = State.Capture();
            State.Tick(minute, seconds, paused, (id, a, b) => ClearPath(id, a, b, false), npcs.IsFocused);
            for (int i = 0; i < before.Length; i++)
            {
                var id = before[i].Id; if (npcs.IsFocused(id)) continue;
                var actor = npcs.Actors[id]; var d = State.Definitions[i];
                var previous = d.Position(before[i].Distance); var point = State.Position(id);
                actor.transform.localPosition = new Vector3((float)point.X, 0, (float)point.Z);
                var direction = new Vector3((float)(point.X - previous.X), 0, (float)(point.Z - previous.Z));
                if (!State.Moving(id)) SetRestRotation(id);
                else if (direction.sqrMagnitude > .000001f) SetTravelRotation(d);
            }
        }
        public void ResetMorning(double minute)
        { if (State == null) return; State.Reset(minute); if (Bound) Apply(true); }
        private void Apply(bool rotate)
        {
            foreach (var d in State.Definitions)
            {
                var p = State.Position(d.Id); npcs.Actors[d.Id].transform.localPosition = new Vector3((float)p.X, 0, (float)p.Z);
                if (rotate) { if (State.Moving(d.Id)) SetTravelRotation(d); else SetRestRotation(d.Id); }
            }
            Physics.SyncTransforms();
        }
        private void SetRestRotation(string id)
        {
            var config = Catalog.Find(id);
            npcs.Actors[id].transform.localRotation = Quaternion.Euler(0, State.Distance(id) == 0 ? config.HomeYaw : config.CoastYaw, 0);
        }
        private void SetTravelRotation(NPCScheduleDefinition d)
        {
            double distance = State.Distance(d.Id);
            var point = d.Position(distance); var next = d.Position(d.NextCorner(distance, d.Target(State.Minute) > distance));
            var direction = new Vector3((float)(next.X - point.X), 0, (float)(next.Z - point.Z));
            if (direction.sqrMagnitude > .0000001f) npcs.Actors[d.Id].transform.localRotation = Quaternion.LookRotation(direction);
        }
        private bool ClearPath(string id, SchedulePoint from, SchedulePoint to, bool staticOnly)
        {
            var actor = npcs.Actors[id]; var c = capsules[id]; var scale = actor.transform.lossyScale;
            var world = actor.transform.parent;
            var start = world.TransformPoint(new Vector3((float)from.X, 0, (float)from.Z));
            var end = world.TransformPoint(new Vector3((float)to.X, 0, (float)to.Z));
            float radius = c.radius * Mathf.Max(scale.x, scale.z) + .015f;
            float height = Mathf.Max(radius * 2, c.height * scale.y);
            var center = start + Vector3.up * (c.center.y * scale.y);
            var bottom = center - Vector3.up * (height * .5f - radius);
            var top = center + Vector3.up * (height * .5f - radius);
            foreach (var hit in Physics.OverlapCapsule(bottom, top, radius, ~0, QueryTriggerInteraction.Ignore))
                if (Blocks(hit, actor, start.y, staticOnly)) return false;
            var delta = end - start;
            if (delta.sqrMagnitude > .0000001f)
                foreach (var hit in Physics.CapsuleCastAll(bottom, top, radius, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
                    if (Blocks(hit.collider, actor, start.y, staticOnly)) return false;
            foreach (var hit in Physics.OverlapCapsule(bottom + delta, top + delta, radius, ~0, QueryTriggerInteraction.Ignore))
                if (Blocks(hit, actor, end.y, staticOnly)) return false;
            return true;
        }
        private static bool Blocks(Collider collider, NPCActor actor, float footY, bool staticOnly)
        {
            if (collider == null || collider.transform.IsChildOf(actor.transform) || collider.bounds.max.y <= footY + .05f) return false;
            if (staticOnly && (collider is CharacterController || collider.GetComponentInParent<NPCActor>() != null)) return false;
            return true;
        }
        public string Describe(string id)
        {
            if (State == null) return "";
            bool coast = State.Minute >= Catalog.Find(id).CoastStartMinute && State.Minute < Catalog.Find(id).ReturnMinute;
            return State.Moving(id) ? (coast ? "正在前往海岸" : "正在返回办事处") : (coast ? "在海岸休息" : "在办事处");
        }
        public void Clear() { Bound = false; State = null; Catalog = null; npcs = null; capsules.Clear(); LastError = null; }
    }
}
