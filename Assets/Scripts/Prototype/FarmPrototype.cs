using MagicIsland.CameraSystem;
using MagicIsland.Farming;
using MagicIsland.Interaction;
using MagicIsland.Player;
using UnityEngine;

namespace MagicIsland.Prototype
{
    // Scene-local camera, light and farm composition; managers own AA world resources.
    public sealed class FarmPrototype : MonoBehaviour
    {
        public FarmGrid Grid { get; private set; }
        public PlayerMovement Player { get; private set; }
        public CameraController CameraController { get; private set; }
        public InteractionController Interaction { get; private set; }
        public bool IsCreated { get; private set; }
        public Transform Bed { get; private set; }
        public Light Sun { get; private set; }

        public void CreateWorld(int farmSize = 5, PlayerMovement player = null)
        {
            if (IsCreated) return;
            if (player == null) throw new System.InvalidOperationException("Load the AA player before binding the farm world");
            IsCreated = true;

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
            cameraController.Target = player.transform;
            cameraController.SnapToTarget();
            Player = player;
            player.enabled = false;
            player.ViewCamera = view;

            Light sun = null;
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                sun = root.GetComponentInChildren<Light>(true);
                if (sun != null) break;
            }
            if (sun == null)
            {
                sun = new GameObject("Sun").AddComponent<Light>();
                sun.transform.SetParent(transform, false);
            }
            Sun = sun;
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

    }
}
