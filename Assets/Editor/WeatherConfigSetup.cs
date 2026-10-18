using MagicIsland.Configuration;
using MagicIsland.Interaction;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace MagicIsland.EditorTools
{
    public static class WeatherConfigSetup
    {
        public static void Ensure(AddressableAssetSettings settings, AddressableAssetGroup group)
        {
            const string path = "Assets/Config/WeatherConfig.asset";
            WeatherConfig config = AssetDatabase.LoadAssetAtPath<WeatherConfig>(path);
            if (config == null)
            {
                const string materialPath = "Assets/Art/Materials/WeatherRain.mat";
                System.IO.Directory.CreateDirectory("Assets/Art/Materials");
                Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null)
                {
                    material = new Material(Shader.Find("Sprites/Default"));
                    AssetDatabase.CreateAsset(material, materialPath);
                }
                config = ScriptableObject.CreateInstance<WeatherConfig>(); config.RainMaterial = material;
                AssetDatabase.CreateAsset(config, path);
            }
            else if (!System.IO.File.ReadAllText(path).Contains("WeatherTransitionSeconds:")) EditorUtility.SetDirty(config);
            string guid = AssetDatabase.AssetPathToGUID(path);
            var entry = settings.FindAssetEntry(guid) ?? settings.CreateOrMoveEntry(guid, group);
            if (entry.address != WeatherConfig.Address) entry.SetAddress(WeatherConfig.Address);
        }

        public static void UpgradeHud()
        {
            string path = PrototypeUiPrefabBuilder.Folder + "/FarmHudPanel.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                PrototypeHud view = root.GetComponent<PrototypeHud>();
                if (view.WeatherText != null) return;
                Transform panel = view.DayText.transform.parent;
                view.DayText.rectTransform.sizeDelta = new Vector2(130, 36);
                view.WeatherText = PrototypeUiPrefabBuilder.Label("Weather", panel, new Vector2(180, -12), new Vector2(110, 32), "晴天", 22);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
