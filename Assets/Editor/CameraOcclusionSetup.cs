using System;
using System.IO;
using System.Linq;
using MagicIsland.CameraSystem;
using MagicIsland.Configuration;
using UnityEditor;
using UnityEngine;

namespace MagicIsland.EditorTools
{
    public static class CameraOcclusionSetup
    {
        public static readonly string[] PrefabPaths = {
            "Assets/Art/Prefabs/Buildings/IslandShop.prefab",
            "Assets/Art/Prefabs/Gathering/GatheringTree.prefab",
            "Assets/Art/Prefabs/Buildings/OrderHall.prefab",
            "Assets/Art/Prefabs/Buildings/PlayerHouse.prefab",
            "Assets/Art/Prefabs/Buildings/PlayerHouseExpansion.prefab",
            "Assets/Art/Prefabs/Buildings/ChickenCoop.prefab",
            "Assets/Art/Prefabs/Buildings/PlayerHomeInterior.prefab",
            "Assets/Art/Prefabs/Buildings/PlayerHomeExpansion.prefab"
        };

        public static void Ensure()
        {
            foreach (string path in PrefabPaths)
            {
                if (!File.Exists(path)) continue;
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    bool changed = false;
                    string name = Path.GetFileNameWithoutExtension(path);
                    if (name == "GatheringTree")
                    {
                        var whole = root.GetComponent<MagicIsland.Interaction.GatheringNode>().WholeVisual;
                        changed = Add(whole, StandardRenderers(whole));
                    }
                    else if (name == "PlayerHomeInterior" || name == "PlayerHomeExpansion")
                    {
                        foreach (var renderer in StandardRenderers(root))
                            if ((renderer.name.Contains("Wall") || renderer.name.StartsWith("Window", StringComparison.Ordinal)
                                || renderer.name == "Reading Shelf" || renderer.name == "Back Skirting") && renderer.enabled)
                                changed |= Add(renderer.gameObject, new[] { renderer });
                    }
                    else
                    {
                        // Keep noticeboard text, ground paths, egg markers and interaction signs opaque.
                        var renderers = StandardRenderers(root).Where(r => r.GetComponentInParent<MagicIsland.Interaction.OrderBoard>() == null
                            && (r.transform.parent == null || r.transform.parent.name != "Ready Eggs") && r.name != "Nest"
                            && r.name != "Foundation" && r.name != "Step" && r.name != "Door Path").ToArray();
                        changed = Add(root, renderers);
                    }
                    if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            const string configPath = "Assets/Config/GameConfig.asset";
            if (File.Exists(configPath) && !File.ReadAllText(configPath).Contains("CameraOccludedOpacity:"))
                EditorUtility.SetDirty(AssetDatabase.LoadAssetAtPath<GameConfig>(configPath));
        }

        private static Renderer[] StandardRenderers(GameObject root) => root.GetComponentsInChildren<Renderer>(true)
            .Where(r => r.sharedMaterials.Any(m => m != null && m.shader != null && m.shader.name == "Standard")).ToArray();
        private static bool Add(GameObject root, Renderer[] renderers)
        {
            // Presence is the migration marker: retain later artist edits, exclusions and AllowFade settings.
            if (root.GetComponent<CameraOccluder>() != null || renderers.Length == 0) return false;
            root.AddComponent<CameraOccluder>().Renderers = renderers; return true;
        }
    }
}
