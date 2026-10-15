using UnityEngine;

namespace MagicIsland.WeatherSystem
{
    /// <summary>Authored sky geometry and a reusable, sparse sunlight cookie. Driven by WeatherManager.</summary>
    public sealed class CloudField : MonoBehaviour
    {
        public const string Address = "IslandClouds";
        public Transform[] Clouds;
        public Renderer[] Renderers;
        public Texture2D ShadowCookie;
        [Min(48)] public float FieldSize = 128;
        [Min(20)] public float Altitude = 42;
        public Vector2 Wind = new Vector2(.30f, .11f);
        public float AnimationTime { get; private set; }
        public bool ShadowsActive { get; private set; }
        public bool IsValid => Clouds != null && Clouds.Length >= 12 && Renderers != null
            && Renderers.Length == Clouds.Length && ShadowCookie != null;

        private Vector3[] origins;
        private Vector2 drift;
        private MaterialPropertyBlock properties;
        private Light owner;
        private Texture previousCookie;
        private float previousCookieSize;
        private Vector3 previousSunPosition;
        private static readonly int DaylightId = Shader.PropertyToID("_Daylight");
        private static readonly int RainId = Shader.PropertyToID("_Rain");

        public void Bind(Light sunlight)
        {
            Detach();
            owner = sunlight;
            if (owner != null)
            {
                previousCookie = owner.cookie;
                previousCookieSize = owner.cookieSize;
                previousSunPosition = owner.transform.position;
            }
            origins = new Vector3[Clouds.Length];
            for (int i = 0; i < Clouds.Length; i++) origins[i] = Clouds[i].localPosition;
            properties = new MaterialPropertyBlock();
            AnimationTime=0;drift=Vector2.zero;
        }

        public void Tick(float seconds, float daylight, bool raining, bool outside,float windMultiplier=1,Vector2? windDirection=null)
        {
            if (origins == null || !IsValid) return;
            if (gameObject.activeSelf != outside) gameObject.SetActive(outside);
            if (outside)
            {
                float dt=Mathf.Max(0,seconds);AnimationTime+=dt;
                Vector2 velocity=windDirection.HasValue?windDirection.Value.normalized*Wind.magnitude:Wind;
                drift+=velocity*dt*Mathf.Max(0,windMultiplier);
            }
            Vector2 offset = drift;
            for (int i = 0; i < Clouds.Length; i++)
            {
                Vector3 origin = origins[i];
                Clouds[i].localPosition = new Vector3(Wrap(origin.x + offset.x), origin.y, Wrap(origin.z + offset.y));
                properties.Clear();
                properties.SetFloat(DaylightId, Mathf.Clamp01(daylight));
                properties.SetFloat(RainId, raining ? 1 : 0);
                Renderers[i].SetPropertyBlock(properties);
            }
            // Only the direct sun is attenuated. Ambient light and moonlight retain their existing values.
            ShadowsActive = owner != null && outside && !raining && daylight > .02f && owner.intensity > .01f;
            if (owner == null) return;
            owner.cookie = ShadowsActive ? ShadowCookie : previousCookie;
            owner.cookieSize = ShadowsActive ? FieldSize : previousCookieSize;
            owner.transform.position = ShadowsActive
                ? previousSunPosition + owner.transform.right * Mathf.Repeat(offset.x, FieldSize)
                    + owner.transform.up * Mathf.Repeat(offset.y, FieldSize)
                : previousSunPosition;
        }

        private float Wrap(float value) => Mathf.Repeat(value + FieldSize * .5f, FieldSize) - FieldSize * .5f;

        public void Detach()
        {
            if (owner != null)
            {
                owner.cookie = previousCookie;
                owner.cookieSize = previousCookieSize;
                owner.transform.position = previousSunPosition;
            }
            ShadowsActive = false;
            owner = null;
        }

        private void OnDestroy() => Detach();
    }
}
