using System.Collections.Generic;
using UnityEngine;

namespace MagicIsland.Tools
{
    // The prefab supplies editable meshes/materials; ToolManager advances the effects only during gameplay.
    public sealed class FarmActionEffects : MonoBehaviour
    {
        public Transform Dust, Droplet, Spark, Seed, WoodChip, StoneChip;
        public bool IsValid => Dust != null && Droplet != null && Spark != null && Seed != null && WoodChip != null && StoneChip != null;
        public int ActiveCount => active.Count;
        public const int MaximumParticles = 48;
        private readonly List<Particle> active = new List<Particle>();
        private readonly List<Particle> pool = new List<Particle>();
        private sealed class Particle
        {
            public Transform Template, View;
            public Vector3 Velocity, Scale;
            public float Age, Lifetime, Gravity, Spin;
            public float Ground;
            public bool Splash, Landed, Chunk;
        }
        private Particle Acquire(Transform template)
        {
            foreach (var particle in pool)
                if (particle.Template == template && !particle.View.gameObject.activeSelf) return particle;
            if (pool.Count >= MaximumParticles)
            {
                var recycled = pool.Find(p => !p.View.gameObject.activeSelf) ?? active[0]; active.Remove(recycled);
                if (recycled.Template == template) return recycled;
                pool.Remove(recycled); Destroy(recycled.View.gameObject);
            }
            var created = new Particle { Template = template, View = Instantiate(template, transform) };
            created.View.name = "Action particle"; pool.Add(created); return created;
        }
        public void Burst(ToolActionKind kind, Vector3 target)
        {
            bool pouring = kind == ToolActionKind.Water;
            bool earthen = kind == ToolActionKind.Hoe;
            bool chunks = kind == ToolActionKind.Axe || kind == ToolActionKind.Pickaxe;
            Transform template = pouring ? Droplet : earthen ? Dust : kind == ToolActionKind.Axe ? WoodChip : kind == ToolActionKind.Pickaxe ? StoneChip
                : kind == ToolActionKind.Seed || kind == ToolActionKind.Fertilizer ? Seed : Spark;
            int count = pouring ? 16 : kind == ToolActionKind.Harvest ? 12 : 10;
            for (int i = 0; i < count; i++)
            {
                var p = Acquire(template); float angle = i * 2.39996f;
                Vector3 radial = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                p.Age = 0; p.Lifetime = pouring ? .62f : chunks ? 1.1f : earthen ? .78f : .85f;
                p.Splash = pouring; p.Chunk = chunks; p.Landed = false; p.Ground = target.y - (chunks ? .5f : 0) + .018f;
                p.Gravity = pouring ? 3.5f : chunks ? 4 : earthen ? 1.7f : kind == ToolActionKind.Harvest ? .8f : 2.8f;
                p.Scale = template.localScale * (1 + (i % 3) * .13f); p.Spin = (i % 2 == 0 ? 1 : -1) * 100;
                p.View.position = target + radial * (pouring ? .08f + (i % 4) * .06f : .08f)
                    + Vector3.up * (pouring ? .55f + (i % 4) * .09f : .10f);
                p.Velocity = radial * (earthen || chunks ? .38f + (i % 3) * .12f : .35f)
                    + Vector3.up * (pouring ? -.7f : chunks ? 1.2f : kind == ToolActionKind.Harvest ? .9f : .65f);
                p.View.localScale = p.Scale; p.View.rotation = Quaternion.Euler(0, i * 31, pouring ? 0 : i * 47);
                p.View.gameObject.SetActive(true); active.Add(p);
            }
        }
        public void Tick(float seconds)
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var p = active[i]; p.Age += seconds;
                if (p.Age >= p.Lifetime) { p.View.gameObject.SetActive(false); active.RemoveAt(i); continue; }
                p.Velocity += Vector3.down * p.Gravity * seconds; p.View.position += p.Velocity * seconds;
                if ((p.Splash || p.Chunk) && !p.Landed && p.View.position.y <= p.Ground)
                {
                    p.Landed = true; p.View.position = new Vector3(p.View.position.x, p.Ground, p.View.position.z);
                    p.Velocity = Vector3.zero; p.Gravity = 0;
                    if (p.Splash) p.Scale = Vector3.Scale(p.Scale, new Vector3(2.7f, .12f, 2.7f));
                }
                p.View.Rotate(p.Chunk && !p.Landed ? p.Spin * .65f * seconds : 0, p.Landed ? 0 : p.Spin * seconds, 0, Space.Self);
                float progress = p.Age / p.Lifetime;
                p.View.localScale = p.Scale * (p.Template == Dust ? Mathf.Lerp(.65f, 1.65f, progress) : 1) * (1 - progress * progress);
            }
        }
    }
}
