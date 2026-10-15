using System;
using System.Linq;
using MagicIsland.Ocean;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    public static class OceanEffectsVerification
    {
        static void Require(bool yes,string why){if(!yes)throw new Exception(why);}
        public static void Verify(GameObject prefab)
        {
            var root=Object.Instantiate(prefab);root.SetActive(false);
            try
            {
                var ocean=root.GetComponent<OceanSurface>();var effects=ocean.Effects;
                Require(ocean.EffectsArtVersion==OceanInteractionEffectsSetup.Version&&effects!=null&&effects.IsValid,"Ocean carries valid reusable interaction effects prefab");
                Require(!ShaderUtil.ShaderHasError(Shader.Find("MagicIsland/Ocean Interaction"))&&effects.Particles.All(p=>p.View.sharedMaterial.shader.isSupported),"Interaction shader compiles for splash, water disturbance and sand");
                var materials=effects.Particles.Select(p=>p.View.sharedMaterial).Distinct().ToArray();var saved=materials.Select(m=>EditorJsonUtility.ToJson(m)).ToArray();
                ocean.Tick(0,1,false);var point=ocean.transform.TransformPoint(ocean.Schools[0].Center);
                ocean.DisturbWater(point);
                Require(effects.Particles.Count(p=>p.Active&&p.Kind==OceanEffectKind.Ripple)==2&&effects.Particles.Count(p=>p.Active&&p.Kind==OceanEffectKind.Droplet)==8,"Landing bobber creates two rings and eight small droplets");
                var ring=effects.Particles.First(p=>p.Active&&p.Kind==OceanEffectKind.Ripple);var drop=effects.Particles.First(p=>p.Active&&p.Kind==OceanEffectKind.Droplet);var scale=ring.transform.localScale;var position=drop.transform.position;
                ocean.Tick(.12f,1,false);
                Require(ring.Age>.1f&&ring.transform.localScale.x>scale.x&&drop.transform.position.y>position.y,"Ripple expands and drops travel upward before falling");
                Require(Mathf.Abs(ring.transform.position.y-ocean.HeightAt(ring.transform.position)-.018f)<.002f,"Ripple anchor follows actual wave height");
                float age=ring.Age;scale=ring.transform.localScale;position=ring.transform.position;int emissions=effects.EmissionCount;
                ocean.Tick(0,1,false);Require(ring.Age==age&&ring.transform.position==position&&ring.transform.localScale==scale&&effects.EmissionCount==emissions,"Pause freezes particle lifetime, shape and emission timers");
                for(int i=0;i<100;i++)ocean.DisturbWater(point);
                Require(effects.ActiveCount<=84&&effects.transform.childCount==84&&effects.GetComponentsInChildren<OceanEffectParticle>(true).Length==84,"Repeated splashes reuse the fixed pool without creating objects");
                ocean.Tick(1.7f,1,false,null,new Vector3(1000,0,1000));
                Require(effects.Particles.Where(p=>p.Kind==OceanEffectKind.Ripple||p.Kind==OceanEffectKind.Droplet).All(p=>!p.Active),"Splash and ripple lifetimes recycle all slots");
                foreach(var particle in effects.Particles)particle.Clear();emissions=effects.EmissionCount;
                ocean.Tick(.8f,1,false,null,new Vector3(1000,0,1000));Require(effects.EmissionCount==emissions,"Distant wildlife does not emit effects");
                ocean.DisturbWater(ocean.Schools[0].transform.position);ocean.Tick(.3f,1,false);
                Require(effects.Particles.Any(p=>p.Active&&p.Kind==OceanEffectKind.FishWake),"A moving startled school emits a light water disturbance");
                var crab=ocean.Crabs[0];var player=ocean.transform.TransformPoint(crab.Center-crab.Tangent*.4f);
                for(int i=0;i<4;i++)ocean.Tick(.1f,1,false,null,player);
                Require(effects.Particles.Any(p=>p.Active&&p.Kind==OceanEffectKind.ShoreGrain)&&effects.Particles.Any(p=>p.Active&&p.Kind==OceanEffectKind.ShorePuff),"A fleeing crab leaves small grains and a short-lived sand puff");
                ocean.Tick(.7f,1,false);
                var reef=effects.Particles.Where(p=>p.Active&&p.Kind==OceanEffectKind.ReefDust).ToArray();Require(reef.Length>0,"Cruising rays stir faint bottom sand");
                foreach(var dust in reef)Require(effects.TryBed(dust.transform.position,out float floor)&&dust.transform.position.y>floor-.01f&&dust.transform.position.y<floor+.20f,"Ray sand anchors to actual seabed triangles");
                ocean.Tick(.1f,0,false);
                var block=new MaterialPropertyBlock();ring.View.GetPropertyBlock(block);
                foreach(var p in effects.Particles.Where(p=>p.Active)){p.View.GetPropertyBlock(block);Require(block.GetFloat("_Daylight")==0,"Night dims active interaction effects");}
                for(int i=0;i<materials.Length;i++)Require(saved[i]==EditorJsonUtility.ToJson(materials[i]),"Runtime effect animation preserves shared AA materials");
            }
            finally{Object.DestroyImmediate(root);}
        }
    }
}
