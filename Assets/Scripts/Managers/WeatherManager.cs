using System.Collections;
using MagicIsland.Configuration;
using MagicIsland.WeatherSystem;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace MagicIsland.Managers
{
    public sealed class WeatherManager : MonoBehaviour
    {
        public WeatherState State { get; private set; }
        public string DisplayName => State?.Kind == WeatherKind.Rainy ? "雨天" : "晴天";
        public ParticleSystem Rain { get; private set; }
        public Light Moon { get; private set; }
        public Light Sun => sun;
        public WindSimulation Wind { get; private set; }
        public float WindFactor => Wind==null?1:(float)Wind.Strength/.65f;
        private Vector4 previousWindDirection;
        private float previousWindTime,previousWindStrength;
        public CloudField Clouds { get; private set; }
        public string LastError { get; private set; }
        public bool OwnsClouds => cloudHandle.IsValid();
        private AsyncOperationHandle<GameObject> cloudHandle;
        private int cloudGeneration;
        private double lastMinute = 480;
        private WeatherConfig config;
        private FarmManager farm;
        private CalendarManager calendar;
        private Light sun;
        private Camera view;
        private Scene scene;
        private Color previousAmbient, previousBackground, previousSunColor;
        private Color previousSky, previousEquator, previousGround;
        private AmbientMode previousAmbientMode;
        private float previousIntensity, previousReflection, previousShadowStrength, previousBias, previousNormalBias;
        private Quaternion previousRotation;

        public IEnumerator LoadClouds(Transform world)
        {
            ClearClouds(); LastError = null;
            if (State == null || sun == null || world == null)
            { LastError = "云层加载前需要初始化天气和岛屿"; yield break; }
            int current = cloudGeneration;
            cloudHandle = Addressables.InstantiateAsync(CloudField.Address, world);
            yield return cloudHandle;
            if (current != cloudGeneration) yield break;
            Clouds = cloudHandle.Status == AsyncOperationStatus.Succeeded && cloudHandle.Result != null
                ? cloudHandle.Result.GetComponent<CloudField>() : null;
            if (Clouds == null || !Clouds.IsValid || world == null || sun == null)
            {
                LastError = "云层 AA 预制体或云影资源不完整";
                ClearClouds(); yield break;
            }
            Clouds.Bind(sun);
            float light = (float)DaylightCycle.Evaluate(lastMinute, config.DawnMinute, config.DaylightMinute, config.DuskMinute, config.NightMinute);
            Clouds.Tick(0, light, State.Kind == WeatherKind.Rainy, IsOutside());
        }

        public void Initialize(WeatherConfig config, FarmManager farm, Light sun, Camera view, Transform world,
            int seed, WeatherKind kind = WeatherKind.Sunny, CalendarManager calendar = null)
        {
            Clear();
            this.config = config; this.farm = farm; this.sun = sun; this.view = view;
            this.calendar = calendar;
            State = new WeatherState(seed, config.RainChance, farm.State.Day, kind);
            previousWindDirection=Shader.GetGlobalVector("_IslandWindDirection");previousWindTime=Shader.GetGlobalFloat("_IslandWindTime");previousWindStrength=Shader.GetGlobalFloat("_IslandWindStrength");
            Wind=new WindSimulation(config.SunnyWindStrength,config.RainWindStrength,config.WindGustAmount,config.WindChangeSeconds,seed,kind==WeatherKind.Rainy);
            TickWind(0,true);
            scene = world.gameObject.scene;
            previousAmbientMode = RenderSettings.ambientMode; previousAmbient = RenderSettings.ambientLight;
            previousSky = RenderSettings.ambientSkyColor; previousEquator = RenderSettings.ambientEquatorColor; previousGround = RenderSettings.ambientGroundColor;
            previousReflection = RenderSettings.reflectionIntensity;
            previousBackground = view.backgroundColor; previousSunColor = sun.color;
            previousIntensity = sun.intensity; previousRotation = sun.transform.rotation;
            previousShadowStrength = sun.shadowStrength; previousBias = sun.shadowBias; previousNormalBias = sun.shadowNormalBias;
            sun.shadowStrength = .85f; sun.shadowBias = .03f; sun.shadowNormalBias = .25f;
            var moonObject = new GameObject("Moonlight"); moonObject.transform.SetParent(world, false);
            Moon = moonObject.AddComponent<Light>(); Moon.type = LightType.Directional; Moon.color = config.NightSun;
            Moon.transform.rotation = Quaternion.Euler(32, -35, 0); Moon.shadows = LightShadows.Soft;
            Moon.shadowStrength = .45f; Moon.shadowBias = .03f; Moon.shadowNormalBias = .25f; Moon.intensity = 0;
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
            lastMinute = minute;
            ApplyRain();
            float light = (float)DaylightCycle.Evaluate(minute, config.DawnMinute, config.DaylightMinute, config.DuskMinute, config.NightMinute);
            bool raining = State.Kind == WeatherKind.Rainy;
            float dim = raining ? config.RainLightMultiplier : 1;
            float solar = (float)DaylightCycle.SolarProgress(minute, config.DawnMinute, config.NightMinute);
            float altitude = Mathf.Sin(solar * Mathf.PI);
            sun.intensity = config.DayIntensity * light * Mathf.Lerp(.6f, 1, altitude) * dim;
            sun.shadowStrength = raining ? .25f : .85f;
            sun.color = Color.Lerp(config.TwilightSun, config.DaySun, Mathf.SmoothStep(0, 1, light));
            sun.transform.rotation = Quaternion.Euler(altitude * config.SunPeakElevation, Mathf.Lerp(config.SunriseAzimuth, config.SunsetAzimuth, solar), 0);
            Moon.cullingMask = sun.cullingMask;
            Moon.color = config.NightSun; Moon.intensity = config.NightIntensity * (1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01(light / .45f))) * dim;
            float fill = raining ? .88f : 1;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Color.Lerp(config.NightAmbient, config.DayAmbient, light) * fill;
            RenderSettings.ambientEquatorColor = Color.Lerp(config.NightEquator, config.DayEquator, light) * fill;
            RenderSettings.ambientGroundColor = Color.Lerp(config.NightGround, config.DayGround, light) * fill;
            RenderSettings.reflectionIntensity = Mathf.Lerp(config.NightReflection, config.DayReflection, light) * fill;
            view.backgroundColor = Color.Lerp(config.NightSky, raining ? config.RainSky : config.DaySky, light);
            if (!raining) { if (!Rain.isStopped) Rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); }
            else if (paused) { if (Rain.isPlaying) Rain.Pause(true); }
            else if (!Rain.isPlaying) Rain.Play(true);
            TickWind(UnityEngine.Time.deltaTime,paused);
            if (Clouds != null) Clouds.Tick(paused ? 0 : UnityEngine.Time.deltaTime, light, raining, IsOutside(),WindFactor,config.WindDirection);
        }

        internal void TickWind(float seconds,bool paused)
        {
            if(Wind==null)return;
            if(!paused&&IsOutside())Wind.Tick(seconds,State.Kind==WeatherKind.Rainy);
            Vector2 direction=config.WindDirection.normalized;
            Shader.SetGlobalVector("_IslandWindDirection",new Vector4(direction.x,direction.y,0,0));
            Shader.SetGlobalFloat("_IslandWindTime",(float)Wind.Time);Shader.SetGlobalFloat("_IslandWindStrength",(float)Wind.Strength);
        }

        private static bool IsOutside()
        {
            var game = GameManager.Instance;
            return game == null || game.Housing == null || !game.Housing.Inside;
        }

        private void ClearClouds()
        {
            cloudGeneration++;
            if (Clouds != null) Clouds.Detach();
            if (cloudHandle.IsValid())
            {
                if (cloudHandle.IsDone) ReleaseClouds(cloudHandle);
                else cloudHandle.Completed += ReleaseClouds;
            }
            cloudHandle = default; Clouds = null;
        }

        private static void ReleaseClouds(AsyncOperationHandle<GameObject> handle)
        {
            if (!handle.IsValid()) return;
            if (handle.Status == AsyncOperationStatus.Succeeded) Addressables.ReleaseInstance(handle);
            else Addressables.Release(handle);
        }

        public void Clear()
        {
            ClearClouds(); LastError = null;
            if(Wind!=null){Shader.SetGlobalVector("_IslandWindDirection",previousWindDirection);Shader.SetGlobalFloat("_IslandWindTime",previousWindTime);Shader.SetGlobalFloat("_IslandWindStrength",previousWindStrength);Wind=null;}
            if (State != null && scene.IsValid() && scene.isLoaded && UnityEngine.SceneManagement.SceneManager.GetActiveScene() == scene)
            {
                RenderSettings.ambientMode = previousAmbientMode; RenderSettings.ambientLight = previousAmbient;
                RenderSettings.ambientSkyColor = previousSky; RenderSettings.ambientEquatorColor = previousEquator; RenderSettings.ambientGroundColor = previousGround;
                RenderSettings.reflectionIntensity = previousReflection;
            }
            if (sun != null) { sun.color = previousSunColor; sun.intensity = previousIntensity; sun.transform.rotation = previousRotation; sun.shadowStrength = previousShadowStrength; sun.shadowBias = previousBias; sun.shadowNormalBias = previousNormalBias; }
            if (view != null) view.backgroundColor = previousBackground;
            if (Rain != null) { Rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); Destroy(Rain.gameObject); }
            if (Moon != null) { Moon.enabled = false; Destroy(Moon.gameObject); }
            Moon = null; Rain = null; State = null; config = null; farm = null; calendar = null; sun = null; view = null; scene = default;
        }
        private void OnDestroy() => Clear();
    }
}
