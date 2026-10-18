using System;
using System.IO;
using System.Linq;
using System.Reflection;
using MagicIsland.Interaction;
using MagicIsland.Managers;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    public static class BeachCompositionVerification
    {
        static void Require(bool ok,string why){if(!ok)throw new InvalidOperationException("Beach composition verification: "+why);}
        public static void BakeAndVerify()
        {
            Require(Application.dataPath.Replace('\\','/').Contains("/Logs/"),"isolated project required");
            var protectedPaths=Directory.GetFiles("Assets/Art","*",SearchOption.AllDirectories)
                .Where(p=>p!=TerrainSetup.PrefabPath&&p.Replace('\\','/')!=TerrainSetup.PrefabPath).ToArray();
            var originals=protectedPaths.Select(File.ReadAllBytes).ToArray();
            var prefab=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);
            string[] colliders;
            try{colliders=prefab.GetComponentsInChildren<Collider>(true).Select(c=>c.name+":"+EditorJsonUtility.ToJson(c)).ToArray();}
            finally{PrefabUtility.UnloadPrefabContents(prefab);}
            BeachCompositionSetup.Rebuild();VerifyAssets();
            string[] watched={TerrainSetup.PrefabPath,BeachCompositionSetup.MeshPath};var bytes=watched.Select(File.ReadAllBytes).ToArray();
            BeachCompositionSetup.Rebuild();PrototypeAddressablesSetup.EnsureSetup();
            for(int i=0;i<watched.Length;i++)Require(bytes[i].SequenceEqual(File.ReadAllBytes(watched[i])),"repeat/Setup retains composition");
            try
            {
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(BeachCompositionSetup.MeshPath);var colors=mesh.colors;colors[0]=new Color(.6f,.5f,.4f,1);mesh.colors=colors;
                var vertices=mesh.vertices;vertices[0]+=Vector3.up*.01f;mesh.vertices=vertices;EditorUtility.SetDirty(mesh);AssetDatabase.SaveAssets();
                var manual=watched.Select(File.ReadAllBytes).ToArray();BeachCompositionSetup.Rebuild();PrototypeAddressablesSetup.EnsureSetup();
                for(int i=0;i<watched.Length;i++)Require(manual[i].SequenceEqual(File.ReadAllBytes(watched[i])),"later manual shape/paint retained");
            }
            finally{for(int i=0;i<watched.Length;i++){File.WriteAllBytes(watched[i],bytes[i]);AssetDatabase.ImportAsset(watched[i],ImportAssetOptions.ForceSynchronousImport);}}
            prefab=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);
            try{Require(colliders.SequenceEqual(prefab.GetComponentsInChildren<Collider>(true).Select(c=>c.name+":"+EditorJsonUtility.ToJson(c))),"all original colliders preserved");}
            finally{PrefabUtility.UnloadPrefabContents(prefab);}
            for(int i=0;i<protectedPaths.Length;i++)Require(originals[i].SequenceEqual(File.ReadAllBytes(protectedPaths[i])),"unrelated art resource "+protectedPaths[i]);
            Directory.CreateDirectory("Logs/BeachComposition");File.WriteAllText("Logs/BeachComposition/Bake.txt","PASS: four pockets / one renderer; Blender normals, complete footprints, every seeded pickup interval, all original colliders/resources, repeat/Setup and manual edits preserved.\n");
            Debug.Log("Beach composition bake verification PASSED.");
        }
        public static void VerifyAssets()
        {
            BeachCompositionSetup.ValidateSites();var root=AssetDatabase.LoadAssetAtPath<GameObject>(TerrainSetup.PrefabPath);
            var group=root.transform.Find(BeachCompositionSetup.Group);Require(group!=null,"authored group");
            Require(group.GetComponentsInChildren<MonoBehaviour>(true).Length==0&&group.GetComponentsInChildren<Collider>(true).Length==0
                &&group.GetComponentsInChildren<Light>(true).Length==0&&group.GetComponentsInChildren<Renderer>(true).Length==1,"one collider/script/light-free batch");
            var mesh=group.GetComponent<MeshFilter>().sharedMesh;Require(mesh!=null&&AssetDatabase.GetAssetPath(mesh)==BeachCompositionSetup.MeshPath,"native AA mesh");
            Require(group.GetComponent<Renderer>().sharedMaterial==AssetDatabase.LoadAssetAtPath<Material>(BeachCompositionSetup.MaterialPath),"shared material");
            Require(mesh.normals.Length==mesh.vertexCount&&mesh.normals.All(n=>n.sqrMagnitude>.9f)&&mesh.colors.Length==mesh.vertexCount,"normal/colour channels");
            var v=mesh.vertices;var tri=mesh.triangles;
            for(int i=0;i<tri.Length;i+=3)Require(Vector3.Cross(v[tri[i+1]]-v[tri[i]],v[tri[i+2]]-v[tri[i]]).sqrMagnitude>1e-14f,"non-degenerate faces");
            foreach(var p in v)Require(!IslandLayout.CanPlace(Mathf.RoundToInt(p.x),Mathf.RoundToInt(p.z)),"all vertices avoid complete buildable cells");
            Require(!ShaderUtil.ShaderHasError(group.GetComponent<Renderer>().sharedMaterial.shader),"shared shader compiles");
        }
        public static void VerifyRuntime(GameManager g)
        {
            var group=g.Terrain.Terrain.transform.Find(BeachCompositionSetup.Group);Require(group!=null&&group.gameObject.activeInHierarchy,"Packed AA group visible");
            var actual=group.GetComponent<MeshFilter>().sharedMesh;var expected=AssetDatabase.LoadAssetAtPath<Mesh>(BeachCompositionSetup.MeshPath);
            Func<Mesh,Vector3[]> points=m=>m.vertices.OrderBy(p=>p.x).ThenBy(p=>p.y).ThenBy(p=>p.z).ToArray();
            Require(actual.vertexCount==expected.vertexCount&&points(actual).SequenceEqual(points(expected)),"Packed geometry matches baked source");
            Require(actual.colors.Length==actual.vertexCount&&group.GetComponent<Renderer>().sharedMaterial.shader.name=="MagicIsland/Island Biome Details","Packed shared palette");
            foreach(var pickup in Object.FindObjectsOfType<BeachPickup>())foreach(var site in BeachCompositionSetup.Sites)
                Require(Vector2.Distance(new Vector2(pickup.transform.position.x,pickup.transform.position.z),site)>BeachCompositionSetup.Radius+.58f,"live pickups remain separate");
            var player=g.Players.Player;var camera=player.ViewCamera;var position=player.transform.position;var rotation=camera.transform.rotation;
            var move=player.GetType().GetMethod("TickMovement",BindingFlags.NonPublic|BindingFlags.Instance);
            try
            {
                camera.transform.rotation=Quaternion.Euler(45,0,0);g.Players.RestorePosition(new Vector3(1,.05f,-14));
                foreach(var goal in new[]{new Vector2(1,-11.7f),new Vector2(10.4f,-11.7f),new Vector2(10.4f,-8.2f),new Vector2(17.3f,-8.2f)})
                {
                    int steps=0;while(Vector2.Distance(new Vector2(player.transform.position.x,player.transform.position.z),goal)>.25f&&steps++<800)
                    {var direction=goal-new Vector2(player.transform.position.x,player.transform.position.z);move.Invoke(player,new object[]{direction.normalized,.04f});}
                    Require(steps<800,"actual spawn-to-dock movement "+goal);
                }
            }
            finally{g.Players.RestorePosition(position);camera.transform.rotation=rotation;}
            Debug.Log("Beach composition runtime verification PASSED: Packed mesh, live pickups and actual spawn-to-dock walking.");
        }
    }
}
