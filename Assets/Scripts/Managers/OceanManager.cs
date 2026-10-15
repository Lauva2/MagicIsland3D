using System.Collections;
using MagicIsland.Configuration;
using MagicIsland.Ocean;
using MagicIsland.WeatherSystem;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace MagicIsland.Managers
{
    public sealed class OceanManager : MonoBehaviour
    {
        public OceanSurface Surface { get; private set; }
        public bool OwnsOcean => handle.IsValid();
        public string LastError { get; private set; }
        private AsyncOperationHandle<GameObject> handle;
        private int generation;
        private Camera view;
        private bool originalDepth;
        private WeatherConfig weather;
        public IEnumerator Load(string address, Transform world, Camera camera, WeatherConfig config)
        {
            Clear();
            LastError = null;
            int current = generation;
            handle = Addressables.InstantiateAsync(address, world);
            yield return handle;
            if (current != generation) yield break;
            Surface = handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null ? handle.Result.GetComponent<OceanSurface>() : null;
            if (Surface == null || !Surface.IsValid || world == null || camera == null || config == null)
            {
                LastError = "海洋 AA 预制体或镜头配置不完整";
                Release(handle);
                handle = default;
                Surface = null;
                yield break;
            }
            view = camera;
            weather = config;
            originalDepth = (view.depthTextureMode & DepthTextureMode.Depth) != 0;
            view.depthTextureMode |= DepthTextureMode.Depth;
            try { Surface.Tick(0, 1, false); }
            catch (System.Exception exception)
            {
                LastError = "海洋 AA 预制体或镜头配置不完整";
                Debug.LogException(exception);
                Clear();
            }
        }
        private void Update()
        {
            var game = GameManager.Instance;
            if (Surface == null || game == null || game.State != GameFlowState.Playing) return;
            bool outside = !game.Housing.Inside;
            if (Surface.gameObject.activeSelf != outside) Surface.gameObject.SetActive(outside);
            if (!outside) return;
            float light = (float)DaylightCycle.Evaluate(game.Time.Clock.Minute, weather.DawnMinute, weather.DaylightMinute, weather.DuskMinute, weather.NightMinute);
            bool animate=game.Players.Player.enabled || (game.UI.HasFishing && !game.UI.HasPause && !game.UI.HasSettings);
            Vector3? fishingPoint=game.UI.HasFishing&&game.Fishing.Spot!=null?(Vector3?)game.Fishing.Spot.Bobber.position:null;
            Surface.Tick(animate ? Time.deltaTime : 0, light, game.Weather.State.Kind == WeatherKind.Rainy, game.Weather.Sun,game.Players.Player.transform.position,fishingPoint);
        }
        public void DisturbWater(Vector3 point)
        {
            var game=GameManager.Instance;
            if(Surface==null||game==null||game.State!=GameFlowState.Playing||game.Housing.Inside||game.UI.HasPause||game.UI.HasSettings)return;
            Surface.DisturbWater(point);
        }
        public void Clear()
        {
            generation++;
            if (view != null && !originalDepth) view.depthTextureMode &= ~DepthTextureMode.Depth;
            if (handle.IsValid())
            {
                if (handle.IsDone) Release(handle);
                else handle.Completed += Release;
            }
            handle = default;
            Surface = null;
            view = null;
            weather = null;
        }

        private static void Release(AsyncOperationHandle<GameObject> value)
        {
            if (!value.IsValid()) return;
            if (value.Status == AsyncOperationStatus.Succeeded) Addressables.ReleaseInstance(value);
            else Addressables.Release(value);
        }

        private void OnDestroy() => Clear();
    }
}
