using System.IO;
using System.Linq;
using MagicIsland.Configuration;
using MagicIsland.CameraSystem;
using MagicIsland.WeatherSystem;
using UnityEditor;
using UnityEngine;

namespace MagicIsland.EditorTools
{
    public static class VegetationWindSetup
    {
        public const string MaterialFolder="Assets/Art/Materials/Vegetation";
        public static readonly string[] Prefabs={
            "Assets/Art/Prefabs/Environment/Scenery/CoastalPalm.prefab",
            "Assets/Art/Prefabs/Environment/Scenery/CoastalShrub.prefab",
            "Assets/Art/Prefabs/Gathering/GatheringTree.prefab",
            "Assets/Art/Prefabs/Orchard/AppleTree.prefab",
            "Assets/Art/Prefabs/Orchard/OrangeTree.prefab"};
        private static readonly string[] FoliageNames={"Palm crown","Young fronds","Leaf clusters","Oak crown","Sunlit crown","Left crown","Front leaf cluster","Young foliage","Leaf crown"};
        public static void Prepare(){PrototypeAddressablesSetup.EnsureSetup();AssetDatabase.SaveAssets();}
        public static void Ensure()
        {
            Directory.CreateDirectory(MaterialFolder);AssetDatabase.Refresh();
            foreach(string path in Prefabs)
            {
                if(!File.Exists(path))continue;
                var root=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    bool changed=false;
                    foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        if(!FoliageNames.Contains(renderer.name)||renderer.GetComponent<VegetationWind>()!=null)continue;
                        var originals=renderer.sharedMaterials;
                        if(originals.Length==0||originals.Any(m=>m==null||m.shader.name!="Standard"))continue;
                        var replacements=originals.Select(WindMaterial).ToArray();renderer.sharedMaterials=replacements;
                        var wind=renderer.gameObject.AddComponent<VegetationWind>();wind.Target=renderer;
                        wind.Radial=renderer.name=="Palm crown"||renderer.name=="Young fronds";
                        wind.Bend=wind.Radial?.14f:renderer.name=="Leaf clusters"?.065f:.085f;
                        wind.Flutter=wind.Radial?.045f:.018f;changed=true;
                    }
                    foreach(var occluder in root.GetComponentsInChildren<CameraOccluder>(true))
                        if(occluder.VegetationFadeMaterial==null&&occluder.Renderers.Any(r=>r!=null&&r.GetComponent<VegetationWind>()!=null))
                        {occluder.VegetationFadeMaterial=FadeMaterial();changed=true;}
                    if(changed)PrefabUtility.SaveAsPrefabAsset(root,path);
                }
                finally{PrefabUtility.UnloadPrefabContents(root);}
            }
            const string weatherPath="Assets/Config/WeatherConfig.asset";
            var weather=AssetDatabase.LoadAssetAtPath<WeatherConfig>(weatherPath);
            if(weather!=null&&!File.ReadAllText(weatherPath).Contains("SunnyWindStrength:"))EditorUtility.SetDirty(weather);
        }
        private static Material WindMaterial(Material original)
        {
            string name=Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(original))+" Wind",path=MaterialFolder+"/"+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);if(material!=null)return material;
            var shader=Shader.Find("MagicIsland/Vegetation Wind");if(shader==null)throw new System.InvalidOperationException("Vegetation wind shader missing");
            material=new Material(original){name=name,shader=shader};AssetDatabase.CreateAsset(material,path);return material;
        }
        private static Material FadeMaterial()
        {
            string path=MaterialFolder+"/Vegetation Fade.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);if(material!=null)return material;
            var shader=Shader.Find("MagicIsland/Vegetation Wind Fade");if(shader==null)throw new System.InvalidOperationException("Vegetation fade shader missing");
            material=new Material(shader){name="Vegetation Fade"};AssetDatabase.CreateAsset(material,path);return material;
        }
    }
}
