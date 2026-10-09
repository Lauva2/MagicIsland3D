using System.Collections.Generic;
using MagicIsland.CameraSystem;
using MagicIsland.Farming;
using MagicIsland.Interaction;
using MagicIsland.Player;
using UnityEngine;
using UnityEngine.Rendering;

namespace MagicIsland.Prototype
{
    // Scene-local composition root. Placeholder geometry is deliberately procedural;
    // real prefab/material assets can later be instantiated via Addressables here.
    public sealed class FarmPrototype : MonoBehaviour
    {
        private readonly List<Material> materials = new List<Material>();
        public FarmGrid Grid { get; private set; }
        public PlayerMovement Player { get; private set; }
        public CameraController CameraController { get; private set; }
        public InteractionController Interaction { get; private set; }
        public bool IsCreated { get; private set; }

        public void CreateWorld()
        {
            if (IsCreated) return;
            IsCreated = true;
            Material grass = Material(new Color(0.48f, 0.65f, 0.34f));
            Material wood = Material(new Color(0.55f, 0.36f, 0.22f));
            Material body = Material(new Color(0.3f, 0.58f, 0.8f));
            Material skin = Material(new Color(0.96f, 0.78f, 0.57f));
            Material hat = Material(new Color(0.95f, 0.76f, 0.35f));
            Primitive("Island ground", PrimitiveType.Cube, new Vector3(0f, -0.25f, 0f), new Vector3(24f, 0.5f, 24f), grass, true);
            Primitive("North boundary", PrimitiveType.Cube, new Vector3(0f, 0.5f, 12f), new Vector3(24f, 1f, 0.25f), wood, true);
            Primitive("South boundary", PrimitiveType.Cube, new Vector3(0f, 0.5f, -12f), new Vector3(24f, 1f, 0.25f), wood, true);
            Primitive("East boundary", PrimitiveType.Cube, new Vector3(12f, 0.5f, 0f), new Vector3(0.25f, 1f, 24f), wood, true);
            Primitive("West boundary", PrimitiveType.Cube, new Vector3(-12f, 0.5f, 0f), new Vector3(0.25f, 1f, 24f), wood, true);

            GameObject playerRoot = new GameObject("Player");
            playerRoot.transform.SetParent(transform, false);
            playerRoot.transform.position = new Vector3(0f, 0.1f, -4.6f);
            CharacterController character = playerRoot.AddComponent<CharacterController>();
            character.height = 1.6f;
            character.radius = 0.28f;
            character.center = Vector3.up * 0.8f;
            character.stepOffset = 0.2f;
            Transform visual = new GameObject("Visual").transform;
            visual.SetParent(playerRoot.transform, false);
            AttachVisual(visual, "Body", PrimitiveType.Capsule, new Vector3(0f, 0.68f, 0f), new Vector3(0.55f, 0.5f, 0.55f), body);
            AttachVisual(visual, "Head", PrimitiveType.Sphere, new Vector3(0f, 1.38f, 0f), Vector3.one * 0.5f, skin);
            AttachVisual(visual, "Hat", PrimitiveType.Cylinder, new Vector3(0f, 1.62f, 0f), new Vector3(0.7f, 0.055f, 0.7f), hat);
            AttachVisual(visual, "Facing marker", PrimitiveType.Cube, new Vector3(0f, 1.36f, 0.27f), new Vector3(0.12f, 0.08f, 0.1f), wood);

            Camera view = null;
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                view = root.GetComponentInChildren<Camera>(true);
                if (view != null) break;
            }
            if (view == null)
            {
                GameObject cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                view = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
                cameraObject.transform.SetParent(transform, false);
            }
            view.clearFlags = CameraClearFlags.SolidColor;
            view.backgroundColor = new Color(0.62f, 0.79f, 0.84f);
            CameraController cameraController = view.gameObject.AddComponent<CameraController>();
            CameraController = cameraController;
            cameraController.enabled = false;
            cameraController.Target = playerRoot.transform;
            cameraController.SnapToTarget();
            PlayerMovement player = playerRoot.AddComponent<PlayerMovement>();
            Player = player;
            player.enabled = false;
            player.ViewCamera = view;
            player.Visual = visual;

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.7f, 0.73f, 0.7f);
            Light sun = null;
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                sun = root.GetComponentInChildren<Light>(true);
                if (sun != null) break;
            }
            if (sun == null) sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
            sun.intensity = 1.1f;
            sun.shadows = LightShadows.Soft;

            GameObject field = new GameObject("Farm 5 x 5");
            field.transform.SetParent(transform, false);
            FarmGrid farm = field.AddComponent<FarmGrid>();
            Grid = farm;
            InteractionController interaction = gameObject.AddComponent<InteractionController>();
            Interaction = interaction;
            interaction.enabled = false;
            interaction.ViewCamera = view;
            interaction.Player = player;
            interaction.Farm = farm;
        }

        private Material Material(Color color)
        {
            Material material = new Material(Shader.Find("Standard")) { color = color };
            material.SetFloat("_Glossiness", 0f);
            materials.Add(material);
            return material;
        }

        private GameObject Primitive(string objectName, PrimitiveType type, Vector3 position, Vector3 scale, Material material, bool collision)
        {
            GameObject item = GameObject.CreatePrimitive(type);
            item.name = objectName;
            item.transform.SetParent(transform, false);
            item.transform.position = position;
            item.transform.localScale = scale;
            item.GetComponent<Renderer>().sharedMaterial = material;
            if (!collision)
            {
                Collider collider = item.GetComponent<Collider>();
                collider.enabled = false;
                Destroy(collider);
            }
            return item;
        }

        private void AttachVisual(Transform parent, string objectName, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            GameObject item = Primitive(objectName, type, Vector3.zero, scale, material, false);
            item.transform.SetParent(parent, false);
            item.transform.localPosition = position;
        }

        private void OnDestroy()
        {
            foreach (Material material in materials) Destroy(material);
        }
    }
}
