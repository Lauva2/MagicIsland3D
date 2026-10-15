using System;
using System.IO;
using System.Linq;
using MagicIsland.Managers;
using MagicIsland.WeatherSystem;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    /// <summary>Checks called by the combined native play-mode flow after entering a sunny island.</summary>
    public static class CloudFlowVerification
    {
        private static void Require(bool ok, string why) { if (!ok) throw new Exception("Cloud verification: " + why); }

        public static void VerifyAssets()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CloudSetup.PrefabPath);
            var field = prefab == null ? null : prefab.GetComponent<CloudField>();
            Require(field != null && field.IsValid, "authored cloud prefab and cookie");
            Require(prefab.GetComponentsInChildren<Collider>(true).Length == 0, "sky clouds never obstruct interaction rays");
            Require(field.Clouds.All(t => t.localPosition.y >= 40), "clouds remain above the close gameplay camera");
            var entry = AddressableAssetSettingsDefaultObject.Settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(CloudSetup.PrefabPath));
            Require(entry != null && entry.address == CloudField.Address, "AA address matches prefab name");
            var pixels = field.ShadowCookie.GetPixels();
            Require(pixels.Min(p => p.a) >= .77f && pixels.Min(p => p.a) < .82f, "mild sunlight attenuation");
            Require(pixels.Count(p => p.a > .99f) > pixels.Length * .55f, "most of the sky admits full sunlight");
            Require(field.ShadowCookie.wrapMode == TextureWrapMode.Repeat, "seamless repeating cookie");
        }

        public static void VerifyRuntime(GameManager game)
        {
            var weather = game.Weather; var clouds = weather.Clouds; var sun = weather.Sun;
            Require(weather.LastError == null && weather.OwnsClouds && clouds != null && clouds.IsValid, "owned AA clouds loaded");
            Require(Object.FindObjectsOfType<CloudField>(true).Length == 1, "single cloud instance");
            double minute = game.Time.Clock.Minute;
            weather.Tick(750, true);
            Require(clouds.ShadowsActive && sun.cookie == clouds.ShadowCookie, "sunlight receives cloud cookie");
            float intensity = sun.intensity; Quaternion direction = sun.transform.rotation;
            float time = clouds.AnimationTime; Vector3 cloudPosition = clouds.Clouds[0].localPosition, cookiePosition = sun.transform.position;
            clouds.Tick(4, 1, false, true);
            Require(clouds.AnimationTime > time + 3.9f && Vector3.Distance(cloudPosition, clouds.Clouds[0].localPosition) > .5f
                && Vector3.Distance(cookiePosition, sun.transform.position) > .5f, "sky and shadows drift together");
            Require(Mathf.Approximately(sun.intensity, intensity) && Quaternion.Angle(sun.transform.rotation, direction) < .001f,
                "clouds preserve solar intensity and direction");
            time = clouds.AnimationTime; cloudPosition = clouds.Clouds[0].localPosition; cookiePosition = sun.transform.position;
            weather.Tick(750, true); weather.Tick(750, true);
            Require(time == clouds.AnimationTime && cloudPosition == clouds.Clouds[0].localPosition && cookiePosition == sun.transform.position, "pause freezes cloud drift and shadows");
            clouds.Tick(1, 1, false, false);
            Require(!clouds.gameObject.activeSelf && !clouds.ShadowsActive && sun.cookie == null && clouds.AnimationTime == time, "indoors hides sky and releases outdoor attenuation");
            clouds.Tick(0, 1, true, true);
            Require(!clouds.ShadowsActive && sun.cookie == null, "overcast rain has no separate moving sunny cloud patches");
            weather.Tick(1320, true);
            Require(sun.intensity == 0 && !clouds.ShadowsActive && sun.cookie == null, "night remains dark with no sun cookie");
            weather.Tick(minute, true);
            Debug.Log("Cloud runtime verification PASSED.");
        }

        public static void CapturePreviews(GameManager game)
        {
            var camera = game.Players.Player.ViewCamera; var sun = game.Weather.Sun;
            Vector3 position = camera.transform.position; Quaternion rotation = camera.transform.rotation;
            float size = camera.orthographicSize; bool orthographic = camera.orthographic; float fov = camera.fieldOfView;
            var canvases = Object.FindObjectsOfType<Canvas>(); var enabled = canvases.Select(c => c.enabled).ToArray();
            double minute = game.Time.Clock.Minute;
            try
            {
                foreach (var canvas in canvases) canvas.enabled = false;
                game.Weather.Tick(750, true);
                // Keep the normal gameplay camera direction, framing the farm and nearby shore.
                camera.orthographic = true; camera.orthographicSize = 13;
                camera.transform.position = new Vector3(0, .6f, 0) - camera.transform.forward * 32;
                Color[] shadow = Capture(camera, "Logs/CloudShadowPreview.png");
                Texture cookie = sun.cookie; sun.cookie = null;
                Color[] clear;
                try { clear = Capture(camera, "Logs/CloudSunPreview.png"); }
                finally { sun.cookie = cookie; }
                int changed = 0; float maximum = 0;
                for (int i = 0; i < shadow.Length; i++)
                {
                    float delta = clear[i].grayscale - shadow[i].grayscale;
                    if (delta > .003f) changed++;
                    maximum = Mathf.Max(maximum, delta);
                }
                Require(changed > shadow.Length * .002f && maximum > .01f, "cookie produces visible soft terrain shadows in the gameplay render");
                camera.orthographic = false; camera.fieldOfView = 62;
                Vector3 focus = game.Weather.Clouds.Clouds[0].position;
                camera.transform.position = focus + new Vector3(-27, -23, -56);
                camera.transform.rotation = Quaternion.LookRotation(focus - camera.transform.position, Vector3.up);
                Capture(camera, "Logs/SkyCloudPreview.png");
                Debug.Log("Cloud preview verification PASSED. Shadow affected pixels=" + changed + ", max=" + maximum);
            }
            finally
            {
                for (int i = 0; i < canvases.Length; i++) canvases[i].enabled = enabled[i];
                camera.transform.SetPositionAndRotation(position, rotation); camera.orthographic = orthographic;
                camera.orthographicSize = size; camera.fieldOfView = fov;
                game.Weather.Tick(minute, true);
            }
        }

        private static Color[] Capture(Camera camera, string path)
        {
            RenderTexture oldTarget = camera.targetTexture, oldActive = RenderTexture.active;
            var target = new RenderTexture(1280, 720, 24); var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllBytes(path, image.EncodeToPNG());
                return image.GetPixels();
            }
            finally
            {
                camera.targetTexture = oldTarget; RenderTexture.active = oldActive;
                target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(image);
            }
        }
    }
}
