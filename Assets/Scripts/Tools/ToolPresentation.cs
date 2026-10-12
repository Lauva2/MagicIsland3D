using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;
namespace MagicIsland.Tools
{
    // Procedural placeholder art. The manager owns all objects and materials.
    public sealed class ToolPresentation
    {
        public Transform Grip { get; }
        public Transform EffectRoot { get; }
        public int ActiveEffectCount => particles.Count;
        private readonly GameObject[] models = new GameObject[7];
        public Transform FishingRodRoot => models[(int)ToolActionKind.FishingRod].transform;
        private readonly List<Material> materials = new List<Material>();
        private readonly List<Particle> particles = new List<Particle>();
        private readonly Material earth, seed, water, sparkle;
        private sealed class Particle { public Transform Transform; public Vector3 Velocity; public float Age; }
        public ToolPresentation(Transform visual, Transform world)
        {
            Grip = new GameObject("Tool Grip").transform; Grip.SetParent(visual, false);
            Grip.localPosition = new Vector3(0.55f, 0.95f, 0.15f);
            EffectRoot = new GameObject("Tool Effects").transform; EffectRoot.SetParent(world, false);
            Material wood = Material(new Color(0.53f, 0.33f, 0.18f));
            Material metal = Material(new Color(0.35f, 0.4f, 0.38f));
            earth = Material(new Color(0.56f, 0.35f, 0.21f)); seed = Material(new Color(0.93f, 0.73f, 0.33f));
            water = Material(new Color(0.34f, 0.7f, 0.91f)); sparkle = Material(new Color(0.77f, 0.91f, 0.43f));
            for (int i = 0; i < models.Length; i++)
            { models[i] = new GameObject(((ToolActionKind)i).ToString()); models[i].transform.SetParent(Grip, false); }
            Part("Handle", models[0].transform, PrimitiveType.Cube, new Vector3(0, -0.13f, 0), new Vector3(0.085f, 0.8f, 0.085f), wood);
            Part("Hoe Head", models[0].transform, PrimitiveType.Cube, new Vector3(0, 0.26f, 0.09f), new Vector3(0.45f, 0.09f, 0.26f), metal);
            Part("Seed Bag", models[1].transform, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.3f, 0.2f, 0.3f), seed);
            Part("Bag Tie", models[1].transform, PrimitiveType.Sphere, Vector3.up * 0.24f, new Vector3(0.13f, 0.1f, 0.13f), wood);
            Part("Watering Can", models[2].transform, PrimitiveType.Cube, Vector3.zero, new Vector3(0.36f, 0.32f, 0.3f), water);
            Part("Spout", models[2].transform, PrimitiveType.Cube, new Vector3(0, -0.01f, 0.27f), new Vector3(0.1f, 0.1f, 0.32f), metal);
            Part("Handle", models[2].transform, PrimitiveType.Cube, new Vector3(0, 0.23f, 0), new Vector3(0.2f, 0.08f, 0.08f), metal);
            Part("Axe Handle", models[3].transform, PrimitiveType.Cube, new Vector3(0, -0.13f, 0), new Vector3(0.09f, 0.82f, 0.09f), wood);
            Part("Axe Blade", models[3].transform, PrimitiveType.Cube, new Vector3(0.16f, 0.25f, 0), new Vector3(0.35f, 0.25f, 0.09f), metal);
            Part("Pickaxe Handle", models[4].transform, PrimitiveType.Cube, new Vector3(0, -0.13f, 0), new Vector3(0.09f, 0.82f, 0.09f), wood);
            Part("Pickaxe Head", models[4].transform, PrimitiveType.Cube, new Vector3(0, 0.25f, 0), new Vector3(0.62f, 0.09f, 0.09f), metal);
            Part("Harvest Basket", models[5].transform, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.4f, 0.15f, 0.4f), wood);
            Part("Harvest", models[5].transform, PrimitiveType.Sphere, Vector3.up * 0.14f, Vector3.one * 0.25f, sparkle);
            Show(ToolActionKind.Hoe);
        }
        private Material Material(Color color)
        { var material = new Material(Shader.Find("Standard")) { color = color }; material.SetFloat("_Glossiness", 0); materials.Add(material); return material; }
        private static GameObject Part(string name, Transform parent, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(type); part.name = name; part.transform.SetParent(parent, false);
            part.transform.localPosition = position; part.transform.localScale = scale;
            Collider collider = part.GetComponent<Collider>(); collider.enabled = false; Object.Destroy(collider);
            part.GetComponent<Renderer>().sharedMaterial = material; return part;
        }
        public void Show(ToolActionKind kind)
        { for (int i = 0; i < models.Length; i++) models[i].SetActive(i == (int)kind); Grip.localRotation = Quaternion.identity; }
        public void Animate(ToolActionKind kind, float progress)
        {
            float motion = Mathf.Sin(progress * Mathf.PI);
            float angle = kind == ToolActionKind.Hoe || kind == ToolActionKind.Axe || kind == ToolActionKind.Pickaxe ? -110f : kind == ToolActionKind.Water ? 65f : 35f;
            Grip.localRotation = Quaternion.Euler(angle * motion, 0, kind == ToolActionKind.Seed ? -25f * motion : 0);
        }
        public void Burst(ToolActionKind kind, Vector3 target)
        {
            Material material = kind == ToolActionKind.Hoe || kind == ToolActionKind.Axe || kind == ToolActionKind.Pickaxe ? earth : kind == ToolActionKind.Seed ? seed : kind == ToolActionKind.Water ? water : sparkle;
            for (int i = 0; i < 8; i++)
            {
                if (particles.Count >= 32) { Object.Destroy(particles[0].Transform.gameObject); particles.RemoveAt(0); }
                float angle = i * Mathf.PI / 4;
                GameObject part = Part("Action Particle", EffectRoot, PrimitiveType.Sphere, Vector3.zero,
                    Vector3.one * (kind == ToolActionKind.Water ? 0.07f : 0.1f), material);
                part.transform.position = target + Vector3.up * (kind == ToolActionKind.Water || kind == ToolActionKind.Seed ? 0.7f : 0.15f);
                particles.Add(new Particle { Transform = part.transform,
                    Velocity = new Vector3(Mathf.Cos(angle) * 0.7f, kind == ToolActionKind.Water || kind == ToolActionKind.Seed ? -0.4f : 1.4f, Mathf.Sin(angle) * 0.7f) });
            }
        }
        public void TickEffects(float seconds)
        {
            for (int i = particles.Count - 1; i >= 0; i--)
            {
                Particle particle = particles[i]; particle.Age += seconds;
                if (particle.Age >= 0.65f) { Object.Destroy(particle.Transform.gameObject); particles.RemoveAt(i); continue; }
                particle.Velocity += Vector3.down * 3f * seconds;
                particle.Transform.position += particle.Velocity * seconds;
                particle.Transform.localScale *= Mathf.Max(0, 1 - seconds * 2);
            }
        }
        public void Clear()
        {
            if (Grip != null) Object.Destroy(Grip.gameObject);
            if (EffectRoot != null) Object.Destroy(EffectRoot.gameObject);
            foreach (Material material in materials) Object.Destroy(material);
            materials.Clear(); particles.Clear();
        }
    }
}
