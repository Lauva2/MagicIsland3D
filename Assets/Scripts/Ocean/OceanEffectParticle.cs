using UnityEngine;

namespace MagicIsland.Ocean
{
    public enum OceanEffectKind { Ripple, Droplet, FishWake, ReefDust, ShoreGrain, ShorePuff }

    // A pre-baked pool slot. OceanManager supplies all time; there is no independent Update.
    public sealed class OceanEffectParticle : MonoBehaviour
    {
        public OceanEffectKind Kind;
        public Renderer View;
        public bool Active { get; private set; }
        public float Age { get; private set; }
        public float Lifetime { get; private set; }
        private Vector3 origin,velocity;
        private float size,opacity,phase;
        private MaterialPropertyBlock properties;
        public void Begin(Vector3 position,Vector3 speed,float scale,float duration,float strength,float delay,float seed,OceanSurface ocean)
        {
            origin=position;velocity=speed;size=scale;Lifetime=Mathf.Max(.05f,duration);opacity=strength;Age=-delay;phase=seed;
            Active=true;gameObject.SetActive(true);Render(ocean);
        }
        public void Step(float seconds,OceanSurface ocean)
        {
            if(!Active||seconds<=0)return;
            Age+=seconds;if(Age>=Lifetime){Clear();return;}Render(ocean);
        }
        public void Clear(){Active=false;gameObject.SetActive(false);}
        private void Render(OceanSurface ocean)
        {
            float age=Mathf.Max(0,Age),p=Mathf.Clamp01(age/Lifetime);
            bool ring=Kind==OceanEffectKind.Ripple||Kind==OceanEffectKind.FishWake;
            bool grain=Kind==OceanEffectKind.Droplet||Kind==OceanEffectKind.ShoreGrain;
            float fade=Age<0?0:grain?1-p*p:Mathf.Sin(p*Mathf.PI);
            var position=origin+velocity*age;
            if(grain)position.y-=age*age*(Kind==OceanEffectKind.Droplet?2.8f:1.7f);
            if(ring)position.y=ocean.HeightAt(position)+.018f;
            transform.position=position;transform.rotation=Quaternion.Euler(0,phase*Mathf.Rad2Deg,grain?phase*19+age*110:0);
            float scale=ring?Mathf.Lerp(.12f,1.25f,p):grain?1-p*.55f:Mathf.Lerp(.45f,1.7f,p);
            transform.localScale=Vector3.one*(size*scale);
            if(Kind==OceanEffectKind.Droplet)transform.localScale=Vector3.Scale(transform.localScale,new Vector3(.7f,1.6f,.7f));
            if(properties==null)properties=new MaterialPropertyBlock();
            properties.SetFloat("_Opacity",opacity*fade);properties.SetFloat("_Daylight",ocean.Daylight);properties.SetFloat("_Phase",phase);
            properties.SetFloat("_OnWater",ring?1:0);properties.SetFloat("_WaterLevel",ocean.Water.transform.position.y);
            properties.SetFloat("_WaveHeight",ocean.Water.sharedMaterial.GetFloat("_WaveHeight"));properties.SetFloat("_WaveTime",ocean.AnimationTime*ocean.Water.sharedMaterial.GetFloat("_WaveSpeed"));
            View.SetPropertyBlock(properties);
        }
    }
}
