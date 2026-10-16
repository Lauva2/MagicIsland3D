using System;
using System.IO;
using System.Linq;
using MagicIsland.Managers;
using MagicIsland.WeatherSystem;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    public static class OutdoorNightLightingVerification
    {
        static void Require(bool ok,string why){if(!ok)throw new Exception("Outdoor lighting: "+why);}
        public static void Prepare()
        {
            string[] paths={OutdoorNightLightingSetup.HousePath,OutdoorNightLightingSetup.PierPath};
            var collisions=paths.Select(p=>AssetDatabase.LoadAssetAtPath<GameObject>(p).GetComponentsInChildren<Collider>(true).Length).ToArray();
            OutdoorNightLightingSetup.Ensure();AssetDatabase.SaveAssets();
            var bytes=paths.Select(File.ReadAllBytes).ToArray();
            var material=File.ReadAllBytes(OutdoorNightLightingSetup.GlowPath);
            OutdoorNightLightingSetup.Ensure();AssetDatabase.SaveAssets();
            for(int i=0;i<paths.Length;i++)
            {
                Require(bytes[i].SequenceEqual(File.ReadAllBytes(paths[i])),"repeat Setup preserves prefab bytes");
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]);
                Require(prefab.GetComponentsInChildren<Collider>(true).Length==collisions[i],"preserve original collisions");
                var instance=Object.Instantiate(prefab);
                try
                {
                    var patch=instance.GetComponentInChildren<OutdoorNightLighting>(true);
                    Require(patch!=null&&patch.IsValid&&patch.Lamps.Length==(i==0?1:2),"authored references and bounded lamp count");
                    Require(patch.GetComponentsInChildren<Collider>(true).Length==0,"no decoration collisions");
                    Require(patch.Lamps.All(l=>l.shadows==LightShadows.None),"no realtime shadow passes");
                    var lamp=patch.Lamps[0];float intensity=lamp.intensity;int mask=lamp.cullingMask;bool enabled=lamp.enabled;
                    var block=new MaterialPropertyBlock();block.SetFloat("_NightVerification",.37f);patch.GlowRenderers[0].SetPropertyBlock(block);
                    patch.Present(1,false,true,8);Require(patch.Level==0&&patch.Lamps.All(l=>!l.enabled),"daytime lamps off");
                    patch.Present(.3f,false,true,8);float dusk=patch.Level;Require(dusk>0&&dusk<1,"smooth dusk level");
                    patch.Present(.3f,true,true,8);Require(patch.Level>dusk,"rain lights earlier");
                    patch.Present(0,false,true,8);Require(patch.Level==1&&lamp.enabled&&lamp.intensity==intensity&&(lamp.cullingMask&(1<<8))==0,"night and interior exclusion");
                    patch.Present(0,false,true,8);Require(lamp.intensity==intensity,"frozen time does not accumulate intensity");
                    patch.Present(0,false,false,8);Require(patch.Level==0&&!lamp.enabled,"indoors switches off exterior lamps");
                    patch.Restore();patch.GlowRenderers[0].GetPropertyBlock(block);
                    Require(lamp.intensity==intensity&&lamp.cullingMask==mask&&lamp.enabled==enabled&&block.GetFloat("_NightVerification")==.37f&&block.GetFloat("_NightGlow")==0,"restore exact light and property block state");
                }
                finally{Object.DestroyImmediate(instance);}
            }
            Require(material.SequenceEqual(File.ReadAllBytes(OutdoorNightLightingSetup.GlowPath)),"shared glow material unchanged");
            // Work on one isolated asset, then restore exact bytes even if the assertion fails.
            // This exercises the ordinary Setup path with deliberately edited legal values.
            Require(Application.dataPath.Replace('\\','/').Contains("/Logs/PrototypeValidationProject/"),"manual-edit verification requires isolated project");
            try
            {
                var root=PrefabUtility.LoadPrefabContents(paths[0]);
                try
                {
                    var patch=root.GetComponentInChildren<OutdoorNightLighting>(true);
                    patch.GlowStrength=.43f;patch.Lamps[0].intensity=.73f;patch.Lamps[0].transform.localPosition+=new Vector3(.07f,.02f,0);
                    PrefabUtility.SaveAsPrefabAsset(root,paths[0]);
                }
                finally{PrefabUtility.UnloadPrefabContents(root);}
                var tuned=File.ReadAllBytes(paths[0]);OutdoorNightLightingSetup.Ensure();AssetDatabase.SaveAssets();
                Require(tuned.SequenceEqual(File.ReadAllBytes(paths[0])),"Setup preserves edited brightness, light and transform");
            }
            finally{File.WriteAllBytes(paths[0],bytes[0]);AssetDatabase.ImportAsset(paths[0],ImportAssetOptions.ForceUpdate);}
            Debug.Log("Outdoor lighting preparation PASSED.");
        }
        public static void Verify(GameManager g)
        {
            Require(g.Weather.OutdoorLightingCount==2,"late AA cottage and pier both bound");
            var patches=new[]{g.Housing.Entrance.GetComponentInChildren<OutdoorNightLighting>(true),g.Fishing.Spot.GetComponentInChildren<OutdoorNightLighting>(true)};
            Require(patches.All(p=>p!=null&&p.IsValid)&&patches.Sum(p=>p.Lamps.Length)==3,"packed AA references and three lamps");
            var home=g.Housing.Entrance.transform.position+Vector3.up*.6f;
            var pier=g.Fishing.Spot.transform.position+Vector3.up*.6f;
            LightingFlowVerification.SetTime(g,750);LightingFlowVerification.Capture(g,"OutdoorHouseDay",home,7);
            Require(patches.All(p=>p.Level==0),"actual daytime manager level");
            LightingFlowVerification.SetTime(g,1130);LightingFlowVerification.Capture(g,"OutdoorHouseDusk",home,7);
            Require(patches.All(p=>p.Level>0&&p.Level<1),"actual dusk manager level");
            LightingFlowVerification.SetTime(g,1320);
            float lit=LightingFlowVerification.Capture(g,"OutdoorHouseNight",home,7);
            foreach(var p in patches)p.Present(0,false,false,g.Configuration.Housing.InteriorLayer);
            float dark=LightingFlowVerification.Capture(g,"OutdoorHouseUnlit",home,7);
            Require(lit>dark+.0001f,"real local house illumination increases rendered light: "+lit+"/"+dark);
            LightingFlowVerification.SetTime(g,1320);
            lit=LightingFlowVerification.Capture(g,"OutdoorPierNight",pier,7);
            foreach(var p in patches)p.Present(0,false,false,g.Configuration.Housing.InteriorLayer);
            dark=LightingFlowVerification.Capture(g,"OutdoorPierUnlit",pier,7);
            Require(lit>dark+.0001f,"real pier illumination increases rendered light: "+lit+"/"+dark);
            LightingFlowVerification.SetTime(g,1320);
            Require(patches.All(p=>p.Lamps.All(l=>l.enabled&&(l.cullingMask&(1<<g.Configuration.Housing.InteriorLayer))==0)),"restored night lamps exclude interior");
            File.WriteAllText("Logs/OutdoorLightingVerification.txt","PASS: packed AA late binding; normal camera day/dusk/night; three local lights; rendered house and pier improvement; interior exclusion.\n");
        }
        public static void VerifyInside(GameManager g)
        {
            Require(g.Weather.OutdoorLightingCount==2,"indoor ownership retained");
            Require(g.Housing.Entrance.GetComponentInChildren<OutdoorNightLighting>(true).Level==0&&g.Fishing.Spot.GetComponentInChildren<OutdoorNightLighting>(true).Level==0,"indoor exterior light level zero");
        }
    }
}
