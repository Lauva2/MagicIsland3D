using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using MagicIsland.Ocean;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
namespace MagicIsland.EditorTools
{
    public static class OceanWildlifeArtVerification
    {
        static void Require(bool ok,string why){if(!ok)throw new InvalidOperationException(why);}
        public static void Run()
        {
            OceanWildlifeArtSetup.Apply();
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(OceanSetup.PrefabPath);Require(prefab.GetComponent<OceanSurface>().IsValid,"Ocean art must retain a valid prefab.");
            var data=JsonUtility.FromJson<OceanWildlifeArtSetup.Data>(File.ReadAllText(OceanWildlifeArtSetup.DataPath));
            foreach(var p in data.parts)
            {
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(OceanBiomeSetup.MeshFolder+"/"+p.name+".asset");
                Require(mesh.vertexCount==p.positions.Length/3&&mesh.triangles.Length==p.triangles.Length,"Baked mesh matches Blender source: "+p.name);
                Require(mesh.colors.All(c=>c.a==0),"Animal vertices must not enter seaweed wind animation.");
            }
            var paths=Directory.GetFiles(OceanSetup.MeshFolder,"*.asset",SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(OceanSetup.MaterialFolder,"*.mat")).Concat(new[]{OceanSetup.PrefabPath,OceanWildlifeArtSetup.DataPath+".meta"}).ToArray();
            var before=paths.Select(File.ReadAllBytes).ToArray();
            var crab=AssetDatabase.LoadAssetAtPath<Mesh>(OceanBiomeSetup.MeshFolder+"/Shore Crab.asset");var copy=Object.Instantiate(crab);
            try
            {
                var vertices=crab.vertices;vertices[0]+=Vector3.up*.0002f;crab.vertices=vertices;EditorUtility.SetDirty(crab);AssetDatabase.SaveAssets();
                var edited=paths.Select(File.ReadAllBytes).ToArray();
                OceanWildlifeArtSetup.Apply();OceanWildlifeArtSetup.Apply();PrototypeAddressablesSetup.EnsureSetup();PrototypeAddressablesSetup.EnsureSetup();
                for(int i=0;i<paths.Length;i++)Require(edited[i].SequenceEqual(File.ReadAllBytes(paths[i])),"Ordinary setup changed authored ocean art: "+paths[i]);
            }
            finally
            {
                Object.DestroyImmediate(copy);
                for(int i=0;i<paths.Length;i++)if(!paths[i].EndsWith(".meta",StringComparison.Ordinal)&&!before[i].SequenceEqual(File.ReadAllBytes(paths[i])))
                {File.WriteAllBytes(paths[i],before[i]);AssetDatabase.ImportAsset(paths[i],ImportAssetOptions.ForceSynchronousImport);}
            }
            CoastalCrabIdleVerification.Verify(prefab);CoastalBirdIdleVerification.Verify(prefab);
            File.WriteAllText("Logs/OceanWildlifeArtVerification.txt","PASS: eight Blender meshes, animal vertex colours, annotated crab joints, existing AA prefab, original movement volumes and planted feet, repeated Apply/Setup preserve artist geometry, shared material and ecology.\n");
            Debug.Log("Ocean wildlife art verification PASSED.");
        }
    }
}
