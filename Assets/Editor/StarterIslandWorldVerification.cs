using System;
using System.Linq;
using MagicIsland.Interaction;
using MagicIsland.TerrainSystem;
using MagicIsland.Ocean;
using MagicIsland.UI.Views;
using UnityEditor;
using UnityEngine;

namespace MagicIsland.EditorTools
{
    public static class StarterIslandWorldVerification
    {
        static void Check(bool b,string why){if(!b)throw new InvalidOperationException("Starter world: "+why);}
        public static void Run()
        {
            if(AssetDatabase.LoadAssetAtPath<GameObject>(TerrainSetup.PrefabPath).GetComponent<IslandTerrain>().World?.ArtVersion==3)
            {BiomeIslandsVerification.VerifyAssets();return;}
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(TerrainSetup.PrefabPath);var t=prefab.GetComponent<IslandTerrain>();
            Check(t.IsValid&&t.World!=null&&t.World.IsValid,"baked AA terrain references");
            Check(t.Ground.sharedMesh==AssetDatabase.LoadAssetAtPath<Mesh>(TerrainSetup.MeshFolder+"/Island Land.asset"),"original ground collision preserved");
            Check(!t.BiomeScenery.gameObject.activeSelf,"tiny prototype biome scenery retired");
            Check(t.World.Grounds.Length==120,"complete chunk/crossing collision set");
            Check(t.World.ArtVersion==2&&t.World.Scenery.Find("Trail waystones")!=null,"first exploration route art baked");
            var paths=t.World.transform.Find("Exploration paths").GetComponent<MeshFilter>().sharedMesh;
            Check(paths.normals.All(n=>n.y>0),"exploration path faces point above ground");
            foreach(var detail in t.World.Scenery.GetComponentsInChildren<Renderer>(true).Where(r=>r.name.StartsWith("Route verge")||r.name.StartsWith("Lake bank details")||r.name.StartsWith("Coastal outcrops")||r.name=="Trail waystones"))
                Check(detail.gameObject.layer==2&&detail.GetComponent<Collider>()==null,"route dressing does not block pointers or movement");
            Check(t.World.Waters.Length==3&&t.World.GetComponentsInChildren<Light>(true).Length==0,"shared water surfaces, no extra lights");
            foreach(var mesh in t.World.GetComponentsInChildren<MeshFilter>(true))
            {
                Check(mesh.sharedMesh!=null&&AssetDatabase.Contains(mesh.sharedMesh),"mesh baked as an asset");
                Check(mesh.sharedMesh.vertices.All(p=>!float.IsNaN(p.y)&&!float.IsInfinity(p.y)),"finite baked geometry");
                Check(mesh.GetComponent<Renderer>().sharedMaterial!=null&&!mesh.GetComponent<Renderer>().sharedMaterial.shader.name.Contains("Error"),"shared valid material");
            }
            Check(t.World.transform.Find("Baked landscape/Amber ruins")!=null&&t.World.transform.Find("Baked landscape/Luminous ancient tree")!=null
                &&t.World.transform.Find("Baked landscape/Moonflower grove")!=null&&t.World.transform.Find("Baked landscape/Frost circle")!=null,"four landmarks");
            var map=AssetDatabase.LoadAssetAtPath<GameObject>(MapPanelSetup.Path).GetComponent<MapView>();Check(map.IsValid&&map.ScaleButton!=null,"map scale controls");
            var ocean=AssetDatabase.LoadAssetAtPath<GameObject>(OceanSetup.PrefabPath).GetComponent<OceanSurface>();Check(ocean.IsValid,"original ocean ecology remains valid");
            Check(ocean.Water.bounds.size.x>=3000&&ocean.Water.bounds.max.z>StarterIslandLayout.MaxZ,"full island water coverage");
            Debug.Log("Starter island asset verification PASSED.");
        }
    }
}
