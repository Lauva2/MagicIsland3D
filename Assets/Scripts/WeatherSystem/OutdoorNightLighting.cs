using System.Collections.Generic;
using UnityEngine;

namespace MagicIsland.WeatherSystem
{
    // AA owns the authored renderers and lights. WeatherManager owns binding and time;
    // this component only applies presentation and restores the original instance state.
    public sealed class OutdoorNightLighting : MonoBehaviour
    {
        public Renderer[] GlowRenderers=System.Array.Empty<Renderer>();
        public Light[] Lamps=System.Array.Empty<Light>();
        [Range(.1f,1)] public float BeginDaylight=.55f;
        [Range(0,.5f)] public float FullDaylight=.08f;
        [Range(.5f,1)] public float RainDaylight=.72f;
        [Range(0,2)] public float GlowStrength=.85f;
        public float Level { get; private set; }
        public static readonly HashSet<OutdoorNightLighting> Active=new HashSet<OutdoorNightLighting>();
        public static event System.Action<OutdoorNightLighting> Registered;
        MaterialPropertyBlock[] originals,blocks;
        float[] intensities;
        int[] masks;
        bool[] enabledStates;
        static readonly int glowId=Shader.PropertyToID("_NightGlow");
        public bool IsValid=>Finite(BeginDaylight)&&Finite(FullDaylight)&&FullDaylight>=0&&BeginDaylight<=1&&BeginDaylight>FullDaylight
            &&Finite(RainDaylight)&&RainDaylight>=.5f&&RainDaylight<=1&&Finite(GlowStrength)&&GlowStrength>=0&&GlowStrength<=2
            &&GlowRenderers!=null&&GlowRenderers.Length>0&&Lamps!=null&&Lamps.Length>0&&Lamps.Length<=2
            &&System.Array.TrueForAll(GlowRenderers,r=>r!=null&&r.sharedMaterial!=null&&r.sharedMaterial.shader!=null&&r.sharedMaterial.shader.name=="MagicIsland/Outdoor Glow")
            &&System.Array.TrueForAll(Lamps,l=>l!=null&&Finite(l.intensity)&&l.intensity>=0&&Finite(l.range)&&l.range>0);
        static bool Finite(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v);
        void OnEnable(){Active.Add(this);Registered?.Invoke(this);}
        void OnDisable(){Active.Remove(this);Restore();}
        void Capture()
        {
            if(blocks!=null)return;
            originals=new MaterialPropertyBlock[GlowRenderers.Length];blocks=new MaterialPropertyBlock[GlowRenderers.Length];
            for(int i=0;i<blocks.Length;i++)if(GlowRenderers[i]!=null)
            {originals[i]=new MaterialPropertyBlock();blocks[i]=new MaterialPropertyBlock();GlowRenderers[i].GetPropertyBlock(originals[i]);}
            intensities=new float[Lamps.Length];masks=new int[Lamps.Length];enabledStates=new bool[Lamps.Length];
            for(int i=0;i<Lamps.Length;i++)if(Lamps[i]!=null){intensities[i]=Lamps[i].intensity;masks[i]=Lamps[i].cullingMask;enabledStates[i]=Lamps[i].enabled;}
        }
        // Requires validated AA references and the owning world's interior layer. No events,
        // random source, gameplay transaction or resource allocation after first binding.
        public void Present(float daylight,bool raining,bool outside,int interiorLayer)
        {
            if(blocks==null&&!IsValid)return;
            Capture();
            float perceived=Mathf.Clamp01(daylight)*(raining?RainDaylight:1);
            Level=outside?1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(FullDaylight,BeginDaylight,perceived)):0;
            for(int i=0;i<GlowRenderers.Length;i++)if(GlowRenderers[i]!=null)
            {GlowRenderers[i].GetPropertyBlock(blocks[i]);blocks[i].SetFloat(glowId,Level*GlowStrength);GlowRenderers[i].SetPropertyBlock(blocks[i]);}
            for(int i=0;i<Lamps.Length;i++)if(Lamps[i]!=null)
            {Lamps[i].intensity=intensities[i]*Level;Lamps[i].enabled=Level>.001f;Lamps[i].cullingMask=interiorLayer>=0?masks[i]&~(1<<interiorLayer):masks[i];}
        }
        public void Restore()
        {
            if(blocks==null)return;
            for(int i=0;i<originals.Length;i++)if(GlowRenderers[i]!=null)GlowRenderers[i].SetPropertyBlock(originals[i]);
            for(int i=0;i<intensities.Length;i++)if(Lamps[i]!=null){Lamps[i].intensity=intensities[i];Lamps[i].enabled=enabledStates[i];Lamps[i].cullingMask=masks[i];}
            originals=null;blocks=null;intensities=null;masks=null;enabledStates=null;Level=0;
        }
    }
}
