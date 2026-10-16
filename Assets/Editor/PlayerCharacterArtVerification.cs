using System;
using System.IO;
using System.Linq;
using MagicIsland.Player;
using UnityEditor;
using UnityEngine;

namespace MagicIsland.EditorTools
{
    public static class PlayerCharacterArtVerification
    {
        private const string PrefabPath = "Assets/Art/Prefabs/Characters/PlayerCharacter.prefab";
        private const string MaterialPath = "Assets/Art/Materials/Player/IslandFarmer.mat";

        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }

        [MenuItem("Magic Island/Verify Blender Player Art")]
        public static void Run()
        {
            string guid = AssetDatabase.AssetPathToGUID(PrefabPath);
            PlayerCharacterArtSetup.Apply();
            Require(AssetDatabase.AssetPathToGUID(PrefabPath) == guid, "Player prefab GUID changed.");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var renderers = prefab.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.enabled).ToArray();
            Require(renderers.Length == 6 && renderers.All(r => r.name == "IslandFarmer Art" && r.gameObject.layer == 2),
                "The six art meshes must use Ignore Raycast.");
            Require(renderers.All(r => r.sharedMaterial == renderers[0].sharedMaterial), "Player palette material is not shared.");
            Require(renderers.Select(r => r.GetComponent<MeshFilter>().sharedMesh.triangles.Length / 3).Sum() == 18618,
                "Unexpected first revision geometry.");
            Require(prefab.GetComponentsInChildren<Collider>(true).Length == 1 && prefab.GetComponent<CharacterController>() != null,
                "Player art changed physical collision.");
            var rig = prefab.GetComponent<PlayerAnimation>();
            Require(rig != null && rig.ToolSocket != null && rig.Animator != null && !rig.Animator.applyRootMotion,
                "Player art changed the rig contract.");

            // Deliberately edit imported assets, then prove both setup entry points preserve them.
            byte[] originalPrefab = File.ReadAllBytes(PrefabPath), originalMaterial = File.ReadAllBytes(MaterialPath);
            try
            {
                var root = PrefabUtility.LoadPrefabContents(PrefabPath);
                try
                {
                    root.GetComponentInChildren<Animator>().transform.Find("Bob/Head/IslandFarmer Art").localPosition += new Vector3(.007f, .003f, 0);
                    PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
                var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
                material.SetFloat("_Glossiness", .23f); EditorUtility.SetDirty(material); AssetDatabase.SaveAssets();
                string[] paths = Directory.GetFiles("Assets/Art/Meshes/Player").Where(p => !p.EndsWith(".meta")).Concat(
                    PlayerCharacterSetup.States.Select(s => PlayerCharacterSetup.AnimationFolder + "/" + s + ".anim"))
                    .Concat(new[] { PrefabPath, MaterialPath, PlayerCharacterSetup.ControllerPath }).ToArray();
                var bytes = paths.Select(File.ReadAllBytes).ToArray();
                PlayerCharacterArtSetup.Apply(); PlayerCharacterArtSetup.Apply(); PrototypeAddressablesSetup.EnsureSetup();
                for (int i = 0; i < paths.Length; i++)
                    Require(bytes[i].SequenceEqual(File.ReadAllBytes(paths[i])), "Repeated setup overwrote artist edits: " + paths[i]);
            }
            finally
            {
                File.WriteAllBytes(PrefabPath, originalPrefab); File.WriteAllBytes(MaterialPath, originalMaterial);
                AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.ImportAsset(MaterialPath, ImportAssetOptions.ForceSynchronousImport);
            }
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/PlayerCharacterArtVerification.txt", "PASS: six shared-material joint meshes, geometry budget, Ignore Raycast, root collision, rig/socket, GUID preservation and repeated Apply/Setup preserve artist edits.\n");
            Debug.Log("Player character art verification PASSED.");
        }
    }
}
