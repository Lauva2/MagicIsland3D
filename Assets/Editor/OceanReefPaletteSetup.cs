using System;
using UnityEditor;
using UnityEngine;
using MagicIsland.Ocean;

namespace MagicIsland.EditorTools
{
    public static class OceanReefPaletteSetup
    {
        public const string MaterialPath="Assets/Art/Materials/Ocean/Reef Palette.mat";

        [MenuItem("Magic Island/Art/Apply Reef Color Palette (Explicit)")]
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            var original=AssetDatabase.LoadAssetAtPath<Material>(OceanBiomeSetup.MaterialPath);
            var reef=AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if(original==null||original.shader==null||original.shader.name!="MagicIsland/Ocean Ecology")
                throw new InvalidOperationException("Missing original ocean ecosystem material; reef preserved.");
            var root=PrefabUtility.LoadPrefabContents(OceanSetup.PrefabPath);
            try
            {
                var patches=root.GetComponentsInChildren<OceanHabitatPatch>(true);
                if(patches.Length!=84)throw new InvalidOperationException("Unexpected reef layout; palette not applied.");
                if(reef==null)
                {
                    reef=new Material(original){name="Reef Palette"};
                    reef.SetFloat("_VertexSaturation",2.2f);reef.SetFloat("_VertexBrightness",1.10f);
                    AssetDatabase.CreateAsset(reef,MaterialPath);
                }
                foreach(var patch in patches)
                {
                    var renderer=patch.GetComponentInChildren<MeshRenderer>(true);
                    if(renderer!=null&&renderer.sharedMaterial==original)renderer.sharedMaterial=reef;
                }
                PrefabUtility.SaveAsPrefabAsset(root,OceanSetup.PrefabPath);AssetDatabase.SaveAssets();
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
    }
}
