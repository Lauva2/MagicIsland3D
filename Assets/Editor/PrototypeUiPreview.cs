using System.IO;
using MagicIsland.Loading;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MagicIsland.EditorTools
{
    public static class PrototypeUiPreview
    {
        // Render the actual prefab in an isolated preview scene, preserving open scenes.
        [MenuItem("Magic Island/Render Main Menu Preview")]
        public static void RenderMenu()
        {
            PrototypeAddressablesSetup.EnsureSetup();
            Scene scene = EditorSceneManager.NewPreviewScene();
            RenderTexture target = new RenderTexture(1280, 720, 24);
            Texture2D output = null;
            RenderTexture previous = RenderTexture.active;
            try
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrototypeUiPrefabBuilder.Folder + "/MainMenuUI.prefab");
                GameObject menu = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                var cameraObject = new GameObject("Preview Camera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.scene = scene;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.targetTexture = target;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 100f;
                Canvas canvas = menu.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                output = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                output.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                output.Apply();
                Directory.CreateDirectory("Logs");
                File.WriteAllBytes("Logs/MainMenuPreview.png", output.EncodeToPNG());
                Debug.Log("MainMenuUI prefab preview rendered to Logs/MainMenuPreview.png");
            }
            finally
            {
                RenderTexture.active = previous;
                EditorSceneManager.ClosePreviewScene(scene);
                if (output != null) Object.DestroyImmediate(output);
                target.Release();
                Object.DestroyImmediate(target);
            }
        }

        public static void BuildAndRender()
        {
            PrototypeAddressablesSetup.BuildContent();
            RenderMenu();
        }
    }
}
