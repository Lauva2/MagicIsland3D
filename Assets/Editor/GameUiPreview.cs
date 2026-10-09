using System.IO;
using UnityEngine;

namespace MagicIsland.EditorTools
{
    public static class GameUiPreview
    {
        public static void CaptureInventory(Camera camera)
        {
            var canvases = Object.FindObjectsOfType<Canvas>();
            var modes = new RenderMode[canvases.Length];
            var cameras = new Camera[canvases.Length];
            var distances = new float[canvases.Length];
            RenderTexture oldTarget = camera.targetTexture;
            RenderTexture oldActive = RenderTexture.active;
            RenderTexture target = new RenderTexture(1280, 720, 24);
            Texture2D output = null;
            try
            {
                camera.targetTexture = target;
                for (int i = 0; i < canvases.Length; i++)
                {
                    modes[i] = canvases[i].renderMode;
                    cameras[i] = canvases[i].worldCamera;
                    distances[i] = canvases[i].planeDistance;
                    canvases[i].renderMode = RenderMode.ScreenSpaceCamera;
                    canvases[i].worldCamera = camera;
                    canvases[i].planeDistance = 1f;
                }
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                output = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                output.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                output.Apply();
                Directory.CreateDirectory("Logs");
                File.WriteAllBytes("Logs/InventoryPreview.png", output.EncodeToPNG());
            }
            finally
            {
                for (int i = 0; i < canvases.Length; i++)
                {
                    canvases[i].renderMode = modes[i];
                    canvases[i].worldCamera = cameras[i];
                    canvases[i].planeDistance = distances[i];
                }
                camera.targetTexture = oldTarget;
                RenderTexture.active = oldActive;
                if (output != null) Object.DestroyImmediate(output);
                target.Release();
                Object.DestroyImmediate(target);
                Canvas.ForceUpdateCanvases();
            }
        }
    }
}
