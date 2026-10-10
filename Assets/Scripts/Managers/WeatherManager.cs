using MagicIsland.Configuration;
using MagicIsland.WeatherSystem;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace MagicIsland.Managers
{
    public sealed class WeatherManager : MonoBehaviour
    {
        public WeatherState State { get; private set; }
        public string DisplayName => State?.Kind == WeatherKind.Rainy ? "雨天" : "晴天";
        public ParticleSystem Rain { get; private set; }
        private WeatherConfig config;
        private FarmManager farm;
        private CalendarManager calendar;
        private Light sun;
        private Camera view;
        private Scene scene;
        private Color previousAmbient, previousBackground, previousSunColor;
        private AmbientMode previousAmbientMode;
        private float previousIntensity;
        private Quaternion previousRotation;

        public void Initialize(WeatherConfig config, FarmManager farm, Light sun, Camera view, Transform world,
            int seed, WeatherKind kind = WeatherKind.Sunny, CalendarManager calendar = null)
        {
            Clear();
            this.config = config; this.farm = farm; this.sun = sun; this.view = view;
            this.calendar = calendar;
            State = new WeatherState(seed, config.RainChance, farm.State.Day, kind);
            scene = world.gameObject.scene;
            previousAmbientMode = RenderSettings.ambientMode; previousAmbient = RenderSettings.ambientLight;
            previousBackground = view.backgroundColor; previousSunColor = sun.color;
            previousIntensity = sun.intensity; previousRotation = sun.transform.rotation;
            var root = new GameObject("Rain"); root.transform.SetParent(world, false); root.transform.localPosition = Vector3.up * 7.5f;
            Rain = root.AddComponent<ParticleSystem>(); Rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = Rain.main; main.playOnAwake = false; main.loop = true; main.startLifetime = 0.75f;
            main.startSpeed = 0; main.startSize = 0.07f; main.maxParticles = 2000;
            main.startColor = new Color(0.65f, 0.82f, 0.95f, 0.6f); main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = Rain.emission; emission.rateOverTime = config.RainParticlesPerSecond;
            var shape = Rain.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(24, 0.1f, 24);
            var velocity = Rain.velocityOverLifetime; velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = -0.8f; velocity.y = -10; velocity.z = 0;
            var renderer = Rain.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = config.RainMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Stretch; renderer.velocityScale = 0.06f; renderer.lengthScale = 3;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            ApplyRain();
        }

        public void NextDay()
        {
            if (calendar != null) State.NextDay(farm.State.Day, calendar.RainChance(farm.State.Day));
            else State.NextDay(farm.State.Day);
            ApplyRain();
        }

        public void ApplyRain()
        {
            if (State?.Kind == WeatherKind.Rainy) farm.ApplyRain();
        }

        public void Tick(double minute, bool paused)
        {
            if (State == null || sun == null || view == null) return;
            ApplyRain();
            float light = (float)DaylightCycle.Evaluate(minute, config.DawnMinute, config.DaylightMinute, config.DuskMinute, config.NightMinute);
            bool raining = State.Kind == WeatherKind.Rainy;
            float dim = raining ? config.RainLightMultiplier : 1;
            sun.intensity = Mathf.Lerp(config.NightIntensity, config.DayIntensity, light) * dim;
            sun.color = Color.Lerp(Color.Lerp(config.NightSun, config.DaySun, light), config.TwilightSun, 4 * light * (1 - light) * 0.7f);
            sun.transform.rotation = Quaternion.Euler(Mathf.Lerp(15, 65, light), -35, 0);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.Lerp(config.NightAmbient, config.DayAmbient, light) * (raining ? 0.85f : 1);
            view.backgroundColor = Color.Lerp(config.NightSky, raining ? config.RainSky : config.DaySky, light);
            if (!raining) { if (!Rain.isStopped) Rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); }
            else if (paused) { if (Rain.isPlaying) Rain.Pause(true); }
            else if (!Rain.isPlaying) Rain.Play(true);
        }

        public void Clear()
        {
            if (State != null && scene.IsValid() && scene.isLoaded && UnityEngine.SceneManagement.SceneManager.GetActiveScene() == scene)
            { RenderSettings.ambientMode = previousAmbientMode; RenderSettings.ambientLight = previousAmbient; }
            if (sun != null) { sun.color = previousSunColor; sun.intensity = previousIntensity; sun.transform.rotation = previousRotation; }
            if (view != null) view.backgroundColor = previousBackground;
            if (Rain != null) { Rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); Destroy(Rain.gameObject); }
            Rain = null; State = null; config = null; farm = null; calendar = null; sun = null; view = null; scene = default;
        }
        private void OnDestroy() => Clear();
    }
}
