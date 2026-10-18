using System;
using System.IO;
using System.Linq;
using MagicIsland.CameraSystem;
using MagicIsland.Managers;
using MagicIsland.Prototype;
using MagicIsland.UI.Views;
using MagicIsland.WeatherSystem;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    [InitializeOnLoad] public static class LightingFlowVerification
    {
        const string Key = "MagicIsland.LightingVerification";
        static LightingFlowVerification() { if (SessionState.GetBool(Key, false)) EditorApplication.update += Tick; }
        public static void Run()
        {
            PrototypeAddressablesSetup.EnsureSetup();
            PrototypeAddressablesSetup.ValidateUiPrefabs();
            var before = File.ReadAllBytes("Assets/Config/WeatherConfig.asset");
            PrototypeAddressablesSetup.EnsureSetup();
            Require(before.SequenceEqual(File.ReadAllBytes("Assets/Config/WeatherConfig.asset")), "Setup preserves tuned lighting configuration");
            EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity"); PrototypeAddressablesSetup.BuildContent();
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            settings.ActivePlayModeDataBuilderIndex = settings.DataBuilders.FindIndex(b => b is BuildScriptPackedPlayMode);
            SessionState.SetBool(Key, true); SessionState.SetFloat(Key + ".Start", (float)EditorApplication.timeSinceStartup); Phase(0);
            EditorApplication.update -= Tick; EditorApplication.update += Tick; EditorApplication.isPlaying = true;
        }
        static void Require(bool ok, string why) { if (!ok) throw new Exception(why); }
        static void Phase(int p) { SessionState.SetInt(Key + ".Phase", p); SessionState.SetFloat(Key + ".PhaseTime", (float)EditorApplication.timeSinceStartup); Debug.Log("Lighting phase " + p); }
        static FarmPrototype World => Object.FindObjectOfType<FarmPrototype>();
        internal static void SetTime(GameManager g, double minute)
        {
            g.Time.Clock.Restore(minute); g.Weather.Tick(minute, true); g.Housing.UpdatePresentation();
            var c = g.Configuration.Weather;
            g.Ocean.Surface.Tick(0, (float)DaylightCycle.Evaluate(minute, c.DawnMinute, c.DaylightMinute, c.DuskMinute, c.NightMinute), g.Weather.State.Kind == WeatherKind.Rainy);
        }
        internal static float Capture(GameManager g, string name, Vector3 focus, float size = 10.5f, float extraDistance = 0)
        {
            var camera = g.Players.Player.ViewCamera; var position = camera.transform.position; float before = camera.orthographicSize;
            camera.GetComponent<CameraController>().SnapToTarget(); camera.orthographicSize = size;
            camera.transform.position += focus - (g.Players.Player.transform.position + Vector3.up * .6f);
            // Enlarging an orthographic overview keeps the 18 m gameplay offset, which clips
            // the foreground ocean against the near plane. Move only the capture camera back.
            float pitch=Mathf.Asin(Mathf.Abs(camera.transform.forward.y));
            camera.transform.position-=camera.transform.forward*(Mathf.Max(0,size/Mathf.Tan(pitch)-15f)+extraDistance);
            var canvases = Object.FindObjectsOfType<Canvas>(); var enabled = canvases.Select(c => c.enabled).ToArray();
            bool ring = g.Interactions.Highlight.Ring.enabled; g.Cameras.SetOcclusionEnabled(false);
            try
            {
                foreach (var c in canvases) c.enabled = false; g.Interactions.Highlight.Ring.enabled = false;
                GameUiPreview.CaptureInventory(camera); string path = "Logs/" + name + ".png"; File.Copy("Logs/InventoryPreview.png", path, true);
                var image = new Texture2D(2, 2);
                try { image.LoadImage(File.ReadAllBytes(path)); return image.GetPixels(image.width / 4, image.height / 4, image.width / 2, image.height / 2).Average(p => p.r * .2126f + p.g * .7152f + p.b * .0722f); }
                finally { Object.DestroyImmediate(image); }
            }
            finally
            {
                for (int i = 0; i < canvases.Length; i++) canvases[i].enabled = enabled[i]; g.Interactions.Highlight.Ring.enabled = ring;
                camera.orthographicSize = before; camera.transform.position = position; g.Cameras.SetOcclusionEnabled(true);
            }
        }
        static void Move(GameManager g, Vector3 p) { g.Players.RestorePosition(p); g.Players.Player.ViewCamera.GetComponent<CameraController>().SnapToTarget(); Physics.SyncTransforms(); }
        static void Tick()
        {
            if (!SessionState.GetBool(Key, false)) return; int p = SessionState.GetInt(Key + ".Phase", 0);
            if (p == 20 && !EditorApplication.isPlaying) { SessionState.SetBool(Key, false); EditorApplication.Exit(0); return; }
            if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + ".PhaseTime", 0) < .25) return;
            try
            {
                if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + ".Start", 0) > 240) throw new Exception("Lighting timeout " + p);
                if (!EditorApplication.isPlaying) return; var g = GameManager.Instance; if (g == null) return;
                Require(g.State != GameFlowState.Failed, "Lighting startup");
                if (p == 0 && g.State == GameFlowState.MainMenu)
                {
                    string dir = Path.GetFullPath("Logs/LightingSaves/" + Guid.NewGuid().ToString("N"));
                    g.Saves.Initialize(g.Configuration.Config, g.Configuration.Items, g.Configuration.Crops, dir,
                        tasks:g.Configuration.Tasks, orders:g.Configuration.Orders, seasons:g.Configuration.Seasons, gathering:g.Configuration.Gathering,
                        toolUpgrades:g.Configuration.ToolUpgrades, storage:g.Configuration.Storage, npcs:g.Configuration.NPCs, relationships:g.Configuration.Relationships,
                        stories:g.Configuration.Stories, schedules:g.Configuration.NPCSchedules, fishing:g.Configuration.Fishing, crafting:g.Configuration.Crafting, animals:g.Configuration.Animals);
                    g.EnterGame(); Phase(1);
                }
                else if (p == 1 && g.State == GameFlowState.Playing)
                {
                    Require(g.Configuration.Weather.IsValid && g.Weather.Moon != null && Object.FindObjectsOfType<Light>().Count(l => l.name == "Moonlight") == 1, "AA config and single owned moonlight");
                    Require(Object.FindObjectsOfType<Light>().Where(l => l.gameObject.scene.name == "MainMenu").All(l => !l.enabled), "Retained menu light cannot illuminate gameplay");
                    AtmospherePolishVerification.Verify(g);
                    g.OpenPause(); SetTime(g, 480); Phase(2);
                }
                else if (p == 2 && Object.FindObjectOfType<PauseView>() != null)
                {
                    var focus = new Vector3(0, .6f, 0);
                    var morning = World.Sun.transform.forward; SessionState.SetVector3(Key + ".MorningDirection", morning); Capture(g, "LightingMorning", focus);
                    SetTime(g, 750); Phase(30);
                }
                else if (p == 30)
                {
                    Require(RenderSettings.ambientMode == AmbientMode.Trilight && RenderSettings.ambientSkyColor.b > RenderSettings.ambientGroundColor.b && g.Weather.Moon.intensity == 0, "Sky/ground fill and no daytime moonlight");
                    float day = Capture(g, "LightingDay", new Vector3(0, .6f, 0)); SessionState.SetFloat(Key + ".Day", day);
                    Require(Vector3.Angle(SessionState.GetVector3(Key + ".MorningDirection", Vector3.zero), World.Sun.transform.forward) > 15, "Morning to noon sun direction changes");
                    SetTime(g, 1020); Phase(31);
                }
                else if (p == 31)
                {
                    Require(Vector3.Angle(SessionState.GetVector3(Key + ".MorningDirection", Vector3.zero), World.Sun.transform.forward) > 50, "Sun direction moves during daytime plateau");
                    SetTime(g, 1110); Phase(32);
                }
                else if (p == 32)
                {
                    Require(World.Sun.color.r > World.Sun.color.b, "Warm sunset"); Capture(g, "LightingSunset", new Vector3(0, .6f, 0));
                    SetTime(g, 1320); Phase(33);
                }
                else if (p == 33)
                {
                    Require(World.Sun.intensity < .0001f && g.Weather.Moon.intensity > 0 && g.Weather.Moon.intensity < g.Configuration.Weather.DayIntensity * .2f && RenderSettings.reflectionIntensity < .03f, "Sun below horizon, faint moon and dark reflections");
                    float night = Capture(g, "LightingNight", new Vector3(0, .6f, 0)); SessionState.SetFloat(Key + ".Night", night);
                    float day = SessionState.GetFloat(Key + ".Day", 0); Require(night < day * .35f && night > .025f, "Rendered land at night is substantially darker but retains silhouettes: " + day + "/" + night);
                    Capture(g, "LightingCoastNight", new Vector3(10, .6f, -5));
                    OutdoorNightLightingVerification.Verify(g);
                    SetTime(g, 750); Phase(34);
                }
                else if (p == 34)
                {
                    Capture(g, "LightingCoastDay", new Vector3(10, .6f, -5)); SessionState.SetFloat(Key + ".SunIntensity", World.Sun.intensity);
                    g.Weather.State.NextDay(g.Weather.State.Day + 1, 1); AtmospherePolishVerification.Advance(g.Weather,g.Configuration.Weather.WeatherTransitionSeconds); SetTime(g, 750); Phase(35);
                }
                else if (p == 35)
                {
                    Require(World.Sun.intensity < SessionState.GetFloat(Key + ".SunIntensity", 0) * .5f && !g.Weather.Rain.isPlaying, "Rain diffuses sunlight and paused rain stays frozen");
                    Capture(g, "LightingRain", new Vector3(0, .6f, 0));
                    SetTime(g,1320);
                    Capture(g,"OutdoorHouseRainNight",g.Housing.Entrance.transform.position+Vector3.up*.6f,7);
                    Capture(g,"OutdoorPierRainNight",g.Fishing.Spot.transform.position+Vector3.up*.6f,7);
                    g.Weather.State.NextDay(g.Weather.State.Day + 1, 0); AtmospherePolishVerification.Advance(g.Weather,g.Configuration.Weather.WeatherTransitionSeconds); SetTime(g, 1320); g.ResumeFromPause();
                    Require(g.Inventory.TryAdd("garden_lamp", 1), "Lamp fixture"); Move(g, new Vector3(-5, .1f, 4.6f));
                    g.Placements.StartCoroutine(g.Placements.Begin("garden_lamp")); Phase(3);
                }
                else if (p == 3 && g.Placements.Ready)
                {
                    g.Placements.SetCandidate(-5, 6); Require(g.Placements.Confirm(), "AA garden lamp placement"); g.Placements.Cancel();
                    SetTime(g, 1320); var lights = g.Placements.Objects.Values.Single().GetComponentsInChildren<Light>();
                    Require(lights.Any(l => l.enabled && l.intensity > 0), "Night preserves local warm lamps");
                    Capture(g, "LightingLampNight", new Vector3(-5, .6f, 6), 3.8f);
                    Move(g, g.Housing.Entrance.Position + new Vector3(0, .1f, -1.4f)); g.EnterHome(); Phase(4);
                }
                else if (p == 4 && g.State == GameFlowState.Playing && g.Housing.Inside)
                {
                    SetTime(g, 1320);
                    OutdoorNightLightingVerification.VerifyInside(g);
                    Require((g.Weather.Moon.cullingMask & (1 << g.Configuration.Housing.InteriorLayer)) == 0 && (World.Sun.cullingMask & (1 << g.Configuration.Housing.InteriorLayer)) == 0, "Exterior sun/moon exclude interior layer");
                    float room = Capture(g, "LightingInteriorNight", g.Configuration.Housing.InteriorOrigin + new Vector3(0, .6f, 0), 7);
                    Require(room > SessionState.GetFloat(Key + ".Night", 1) * 2, "Room lights keep indoor night readable");
                    g.ReturnToMainMenu(); Phase(5);
                }
                else if (p == 5 && Object.FindObjectOfType<PauseView>() != null) { Object.FindObjectOfType<PauseView>().ReturnMenuButton.onClick.Invoke(); Phase(6); }
                else if (p == 6 && g.State == GameFlowState.MainMenu)
                {
                    Require(g.Weather.OutdoorLightingCount==0,"Outdoor lighting bindings released on return");
                    Require(g.Weather.Moon == null && g.Weather.Rain == null && Object.FindObjectsOfType<Light>().All(l => l.name != "Moonlight") && !File.Exists(g.Saves.SavePath), "Release weather lights without daytime save");
                    Require(Object.FindObjectsOfType<Light>().Any(l => l.gameObject.scene.name == "MainMenu" && l.enabled), "Returning to menu restores its lighting");
                    g.EnterGame(); Phase(7);
                }
                else if (p == 7 && g.State == GameFlowState.Playing)
                {
                    Require(g.Weather.OutdoorLightingCount==2,"Re-entry has exactly two outdoor lighting bindings");
                    Require(Object.FindObjectsOfType<Light>().Count(l => l.name == "Moonlight") == 1, "Re-entry has one moon without stale lighting state");
                    File.WriteAllText("Logs/LightingVerification.txt", "PASS: packed-AA preserved WeatherConfig, moving solar direction, sky/equator/ground ambient, warm dusk, zero nighttime sunlight, faint separate moon, reduced reflections and ocean night tint, rendered night/day contrast, rainy sunlight, paused particles, AA local lamp, interior layer isolation and readable room, menu cleanup and re-entry, no daytime save.\nDay luminance " + SessionState.GetFloat(Key + ".Day", 0) + ", night " + SessionState.GetFloat(Key + ".Night", 0) + "\n");
                    Debug.Log("Lighting flow verification PASSED."); Phase(20); EditorApplication.isPlaying = false;
                }
            }
            catch (Exception e) { Debug.LogException(e); SessionState.SetBool(Key, false); EditorApplication.isPlaying = false; EditorApplication.Exit(1); }
        }
    }
}
