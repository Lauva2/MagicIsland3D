using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;
namespace MagicIsland.Tools
{
    // ToolManager owns the held-tool models and the AA effect prefab dependency.
    public sealed class ToolPresentation
    {
        public Transform Grip { get; }
        public Transform EffectRoot { get; private set; }
        public int ActiveEffectCount => effects != null ? effects.ActiveCount : 0;
        private readonly GameObject[] models = new GameObject[8];
        public Transform FishingRodRoot => models[(int)ToolActionKind.FishingRod].transform;
        private readonly List<Material> materials = new List<Material>();
        private FarmActionEffects effects;
        private FarmToolModels toolModels;
        private ToolActionKind shownKind;
        private readonly Material earth, seed, sparkle;
        public ToolPresentation(Transform visual, Transform world, Transform toolSocket = null)
        {
            Grip = new GameObject("Tool Grip").transform; Grip.SetParent(toolSocket != null ? toolSocket : visual, false);
            Grip.localPosition = toolSocket != null ? Vector3.zero : new Vector3(0.55f, 0.95f, 0.15f);
            EffectRoot = new GameObject("Tool Effects").transform; EffectRoot.SetParent(world, false);
            Material wood = Material(new Color(0.53f, 0.33f, 0.18f));
            earth = Material(new Color(0.56f, 0.35f, 0.21f)); seed = Material(new Color(0.93f, 0.73f, 0.33f));
            sparkle = Material(new Color(0.77f, 0.91f, 0.43f));
            for (int i = 0; i < models.Length; i++)
            {
                // The four polished tools arrive with the AA effects dependency before input is enabled.
                if (i == (int)ToolActionKind.Hoe || i == (int)ToolActionKind.Water
                    || i == (int)ToolActionKind.Axe || i == (int)ToolActionKind.Pickaxe) continue;
                models[i] = new GameObject(((ToolActionKind)i).ToString()); models[i].transform.SetParent(Grip, false);
            }
            Part("Seed Bag", models[1].transform, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.3f, 0.2f, 0.3f), seed);
            Part("Bag Tie", models[1].transform, PrimitiveType.Sphere, Vector3.up * 0.24f, new Vector3(0.13f, 0.1f, 0.13f), wood);
            Part("Harvest Basket", models[5].transform, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.4f, 0.15f, 0.4f), wood);
            Part("Harvest", models[5].transform, PrimitiveType.Sphere, Vector3.up * 0.14f, Vector3.one * 0.25f, sparkle);
            Part("Fertilizer Bag", models[7].transform, PrimitiveType.Cube, Vector3.zero, new Vector3(.30f, .40f, .22f), earth);
            Part("Fertilizer Tie", models[7].transform, PrimitiveType.Sphere, Vector3.up * .23f, new Vector3(.14f, .08f, .14f), seed);
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
        {
            shownKind = kind;
            for (int i = 0; i < models.Length; i++) if (models[i] != null) models[i].SetActive(i == (int)kind);
            Grip.localRotation = Quaternion.identity;
        }
        public void Animate(ToolActionKind kind, float progress)
        {
            float motion = Mathf.Sin(progress * Mathf.PI);
            float angle = kind == ToolActionKind.Hoe || kind == ToolActionKind.Axe || kind == ToolActionKind.Pickaxe ? -110f : kind == ToolActionKind.Water ? 65f : 35f;
            Grip.localRotation = Quaternion.Euler(angle * motion, 0, kind == ToolActionKind.Seed ? -25f * motion : 0);
        }
        public void BindEffects(GameObject prefab)
        {
            Transform parent = EffectRoot.parent;
            Object.Destroy(EffectRoot.gameObject);
            effects = Object.Instantiate(prefab, parent).GetComponent<FarmActionEffects>();
            EffectRoot = effects.transform;
            if (toolModels != null) Object.Destroy(toolModels.gameObject);
            toolModels = Object.Instantiate(effects.HeldToolsPrefab, Grip, false);
            toolModels.name = "Farm Tool Models";
            models[(int)ToolActionKind.Hoe] = toolModels.Hoe.gameObject;
            models[(int)ToolActionKind.Water] = toolModels.WateringCan.gameObject;
            models[(int)ToolActionKind.Axe] = toolModels.Axe.gameObject;
            models[(int)ToolActionKind.Pickaxe] = toolModels.Pickaxe.gameObject;
            Show(shownKind);
        }
        public void Burst(ToolActionKind kind, Vector3 target) => effects?.Burst(kind, target);
        public void TickEffects(float seconds) => effects?.Tick(seconds);
        public void Clear()
        {
            if (Grip != null) Object.Destroy(Grip.gameObject);
            if (EffectRoot != null) Object.Destroy(EffectRoot.gameObject);
            foreach (Material material in materials) Object.Destroy(material);
            materials.Clear(); effects = null; toolModels = null;
        }
    }
}
