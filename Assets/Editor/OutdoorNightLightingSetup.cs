using System;
using System.IO;
using System.Linq;
using MagicIsland.WeatherSystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    public static class OutdoorNightLightingSetup
    {
        public const string HousePath="Assets/Art/Prefabs/Buildings/PlayerHouse.prefab";
        public const string PierPath="Assets/Art/Prefabs/Fishing/CoastalFishingSpot.prefab";
        public const string GlowPath="Assets/Art/Materials/Weather/Outdoor Warm Glow.mat";
        public static void Ensure()
        {
            var glow=AssetDatabase.LoadAssetAtPath<Material>(GlowPath);
            if(glow==null)
            {
                var shader=Shader.Find("MagicIsland/Outdoor Glow");if(shader==null)throw new Exception("Outdoor glow shader is missing");
                Directory.CreateDirectory(Path.GetDirectoryName(GlowPath));
                glow=new Material(shader){name="Outdoor Warm Glow",color=new Color(1,.64f,.28f)};
                AssetDatabase.CreateAsset(glow,GlowPath);
            }
            EnsurePrefab(HousePath,glow,true);EnsurePrefab(PierPath,glow,false);
        }
        static void EnsurePrefab(string path,Material glow,bool house)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(prefab==null)throw new Exception("Night lighting prefab missing: "+path);
            var existing=prefab.GetComponentInChildren<OutdoorNightLighting>(true);
            if(existing!=null&&existing.GlowRenderers!=null&&existing.Lamps!=null
                &&existing.GlowRenderers.Length>0&&existing.Lamps.Length>0
                &&existing.GlowRenderers.All(r=>r!=null)&&existing.Lamps.All(l=>l!=null))return;
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var lighting=root.GetComponentInChildren<OutdoorNightLighting>(true);
                if(lighting==null)
                {
                    var group=new GameObject("Night lighting");group.layer=2;group.transform.SetParent(root.transform,false);
                    lighting=group.AddComponent<OutdoorNightLighting>();
                    if(house)House(root.transform,group.transform,glow);else Pier(group.transform,glow);
                }
                var renderers=lighting.GetComponentsInChildren<Renderer>(true).Where(r=>r.sharedMaterial!=null&&r.sharedMaterial.shader.name=="MagicIsland/Outdoor Glow").ToArray();
                lighting.GlowRenderers=Fill(lighting.GlowRenderers,renderers);
                lighting.Lamps=Fill(lighting.Lamps,lighting.GetComponentsInChildren<Light>(true));
                if(!lighting.IsValid)throw new Exception("Invalid authored outdoor lighting: "+path);
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        static T[] Fill<T>(T[] current,T[] candidates) where T:Object
        {
            if(current==null||current.Length==0)return candidates;
            var result=(T[])current.Clone();
            for(int i=0;i<result.Length;i++)if(result[i]==null)result[i]=candidates.FirstOrDefault(c=>c!=null&&!result.Contains(c));
            return result;
        }
        static GameObject Box(Transform parent,string name,Vector3 position,Vector3 scale,Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.layer=2;go.transform.SetParent(parent,false);
            go.transform.localPosition=position;go.transform.localScale=scale;Object.DestroyImmediate(go.GetComponent<Collider>());
            var renderer=go.GetComponent<Renderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            return go;
        }
        static void Overlay(Transform parent,Transform source,string name,Vector3 face,Material glow)
        {
            if(source==null)throw new Exception("Night window or existing lantern reference missing: "+name);
            var go=Box(parent,name,Vector3.zero,Vector3.one,glow);
            go.transform.position=source.TransformPoint(face*.57f);go.transform.rotation=source.rotation;
            var scale=source.lossyScale;var parentScale=parent.lossyScale;
            scale=new Vector3(scale.x/parentScale.x,scale.y/parentScale.y,scale.z/parentScale.z)*.96f;
            if(Mathf.Abs(face.x)>.5f)scale.x*=.06f;
            else if(Mathf.Abs(face.z)>.5f)scale.z*=.06f;
            else scale*=1.07f;
            go.transform.localScale=scale;
        }
        static void Lamp(Transform parent,string name,Vector3 position,float intensity,float range)
        {
            var go=new GameObject(name,typeof(Light));go.layer=2;go.transform.SetParent(parent,false);go.transform.localPosition=position;
            go.transform.localRotation=Quaternion.Euler(90,0,0);
            var light=go.GetComponent<Light>();light.type=LightType.Spot;light.spotAngle=112;light.innerSpotAngle=50;
            light.color=new Color(1,.84f,.66f);light.range=range;light.intensity=intensity;light.shadows=LightShadows.None;
            light.renderMode=LightRenderMode.ForcePixel;light.bounceIntensity=0;light.enabled=false;
        }
        static void House(Transform house,Transform parent,Material glow)
        {
            Overlay(parent,house.Find("Door Window"),"Door window glow",Vector3.back,glow);
            Overlay(parent,house.Find("Side Window"),"Side window glow",Vector3.left,glow);
            var lantern=house.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="Lantern warm glass");
            Overlay(parent,lantern,"Porch lantern glow",Vector3.zero,glow);
            Lamp(parent,"Porch warm light",new Vector3(.45f,1.62f,-1.42f),1.05f,3.2f);
        }
        static void Pier(Transform parent,Material glow)
        {
            var wood=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Coast/Pier Honey Wood.mat");
            if(wood==null)throw new Exception("Pier material missing");
            for(int i=0;i<2;i++)
            {
                var p=new Vector3(i==0?-1.55f:2.65f,1.36f,i==0?1.08f:-1.08f);
                Box(parent,"Pier lantern post "+i,new Vector3(p.x,.66f,p.z),new Vector3(.085f,1.26f,.085f),wood);
                Box(parent,"Pier lantern cap "+i,p+Vector3.up*.17f,new Vector3(.29f,.065f,.25f),wood);
                Box(parent,"Pier lantern foot "+i,p-Vector3.up*.17f,new Vector3(.27f,.055f,.24f),wood);
                Box(parent,"Pier lantern glass "+i,p,new Vector3(.20f,.27f,.17f),glow);
                Lamp(parent,"Pier warm light "+i,p+new Vector3(0,-.12f,i==0?-.22f:.22f),1.1f,2.7f);
            }
        }
    }
}
