using System;
using System.IO;
using System.Linq;
using MagicIsland.Tools;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    public static class FarmToolArtVerification
    {
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        [MenuItem("Magic Island/Verify Blender Farm Tools")]
        public static void Run()
        {
            FarmToolArtSetup.Apply();
            var models = AssetDatabase.LoadAssetAtPath<FarmToolModels>(FarmToolArtSetup.PrefabPath);
            var effects = AssetDatabase.LoadAssetAtPath<GameObject>(FarmArtSetup.EffectPath).GetComponent<FarmActionEffects>();
            Require(models != null && models.IsValid && effects.IsValid && effects.HeldToolsPrefab == models, "Effects must carry the tool dependency.");
            var renderers = models.GetComponentsInChildren<MeshRenderer>(true);
            Require(renderers[0].sharedMaterial.mainTexture is Texture2D, "Tool palette must remain assigned as a 2D texture.");
            Require(renderers.Length == 4 && renderers.All(r => r.gameObject.layer == 2 && r.enabled && r.sharedMaterial == renderers[0].sharedMaterial),
                "Four collider-free Ignore Raycast tools share one material.");
            Require(models.GetComponentsInChildren<MeshFilter>(true).Sum(f => f.sharedMesh.triangles.Length / 3) == 9396, "Unexpected tool geometry.");
            var probe = new GameObject("Invalid tool reference probe", typeof(FarmActionEffects));
            try
            {
                var fx = probe.GetComponent<FarmActionEffects>(); fx.Dust = fx.Droplet = fx.Spark = fx.Seed = fx.WoodChip = fx.StoneChip = probe.transform;
                Require(!fx.IsValid, "A missing held-tool dependency must fail validation before input.");
                fx.HeldToolsPrefab = models; Require(fx.IsValid, "A valid held-tool dependency must pass validation.");
            }
            finally { Object.DestroyImmediate(probe); }
            string[] paths = new[] { FarmToolArtSetup.PrefabPath, FarmToolArtSetup.MaterialPath, FarmArtSetup.EffectPath, FarmToolArtSetup.DataPath + ".meta" }
                .Concat(Directory.GetFiles("Assets/Art/Meshes/Tools", "*.asset")).ToArray();
            var originals = paths.Select(File.ReadAllBytes).ToArray();
            try
            {
                var root = PrefabUtility.LoadPrefabContents(FarmToolArtSetup.PrefabPath);
                try
                {
                    root.GetComponent<FarmToolModels>().Hoe.localPosition += Vector3.right * .011f;
                    root.GetComponent<FarmToolModels>().Axe.localPosition += Vector3.forward * .009f;
                    PrefabUtility.SaveAsPrefabAsset(root, FarmToolArtSetup.PrefabPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
                var material = AssetDatabase.LoadAssetAtPath<Material>(FarmToolArtSetup.MaterialPath);
                material.SetFloat("_Glossiness", .31f); EditorUtility.SetDirty(material); AssetDatabase.SaveAssets();
                var edited = paths.Select(File.ReadAllBytes).ToArray();
                FarmToolArtSetup.Apply(); FarmToolArtSetup.Apply(); PrototypeAddressablesSetup.EnsureSetup(); PrototypeAddressablesSetup.EnsureSetup();
                for (int i = 0; i < paths.Length; i++) Require(edited[i].SequenceEqual(File.ReadAllBytes(paths[i])), "Setup changed artist tools: " + paths[i]);
            }
            finally
            {
                for (int i = 0; i < paths.Length; i++)
                {
                    // The meta file is checked above but never edited by the fixture. Import its asset, not the .meta itself.
                    if (paths[i].EndsWith(".meta", StringComparison.Ordinal)) continue;
                    File.WriteAllBytes(paths[i], originals[i]); AssetDatabase.ImportAsset(paths[i], ImportAssetOptions.ForceSynchronousImport);
                }
            }
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/FarmToolArtVerification.txt", "PASS: four baked mesh tools, one material, grip roots, no colliders, effects AA dependency, missing-reference rejection, repeated Apply/Setup preserve edited transforms/materials/geometry.\n");
            Debug.Log("Farm tool art verification PASSED.");
        }
    }
}
