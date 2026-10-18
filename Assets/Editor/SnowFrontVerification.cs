using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MagicIsland.Interaction;
using MagicIsland.Managers;
using MagicIsland.TerrainSystem;
using UnityEditor;
using UnityEngine;

namespace MagicIsland.EditorTools
{
    public static class SnowFrontVerification
    {
        static void Require(bool ok,string why){if(!ok)throw new InvalidOperationException("Snow approach verification: "+why);}
        [Serializable] public sealed class Report{public string[] editedMeshes,newMeshes;public int groundVertices,landscapeVertices,sites,batches;}
        static Dictionary<string,string> Hierarchy(GameObject root)
        {
            string Key(Transform t)=>t.parent==null?t.name:Key(t.parent)+"/"+t.name+":"+t.GetSiblingIndex();
            return root.GetComponentsInChildren<Transform>(true).ToDictionary(Key,t=>t.localPosition.ToString("R")+t.localRotation.ToString("R")+t.localScale.ToString("R")+t.gameObject.layer+"/"+t.gameObject.activeSelf+"/"+string.Join(",",t.GetComponents<Component>().Select(c=>c.GetType().FullName)));
        }
        public static void BakeAndVerify()
        {
            Require(Application.dataPath.Replace('\\','/').Contains("/Logs/"),"use an isolated project");
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(TerrainSetup.PrefabPath);var terrain=prefab.GetComponent<IslandTerrain>();
            var hierarchy=Hierarchy(prefab);var plan=SnowFrontSetup.PlanEdits(terrain);Require(plan.Length>0,"start from the unrefined baseline");
            var vertices=plan.Select(e=>e.Mesh.vertices).ToArray();var normals=plan.Select(e=>e.Mesh.normals).ToArray();var triangles=plan.Select(e=>e.Mesh.triangles).ToArray();
            var colors=plan.Select(e=>e.Mesh.colors).ToArray();var grass=plan.Select(e=>e.Mesh.uv).ToArray();var paths=plan.Select(e=>AssetDatabase.GetAssetPath(e.Mesh)).ToArray();
            var uv1=plan.Select(e=>e.Mesh.uv2).ToArray();var uv2=plan.Select(e=>e.Mesh.uv3).ToArray();var bounds=plan.Select(e=>e.Mesh.bounds).ToArray();
            string[] protect={OceanSetup.PrefabPath,OceanSeabedSlopeSetup.FloorPath,OceanSeabedSlopeSetup.FloorPath+".meta",ArrivalRouteSetup.GroundPath,VillageArtSetup.PathsPath,MapPanelSetup.Path,"Assets/Config/GameConfig.asset","Assets/Art/Materials/Terrain/Biome Ground.mat","Assets/Art/Materials/Terrain/Biome Details.mat"};
            var protectedBytes=protect.Select(File.ReadAllBytes).ToArray();var colliders=terrain.GetComponentsInChildren<Collider>(true).Select(c=>c.name+":"+AssetDatabase.GetAssetPath((c as MeshCollider)?.sharedMesh)).ToArray();
            SnowFrontSetup.Rebuild();
            prefab=AssetDatabase.LoadAssetAtPath<GameObject>(TerrainSetup.PrefabPath);terrain=prefab.GetComponent<IslandTerrain>();var current=Hierarchy(prefab);
            foreach(var pair in hierarchy)Require(current.TryGetValue(pair.Key,out var value)&&value==pair.Value,"original object retained: "+pair.Key);
            Require(colliders.SequenceEqual(terrain.GetComponentsInChildren<Collider>(true).Select(c=>c.name+":"+AssetDatabase.GetAssetPath((c as MeshCollider)?.sharedMesh))),"complete original collision set");
            int groundCount=0,landscapeCount=0;
            for(int i=0;i<plan.Length;i++)
            {
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(paths[i]);
                Require(vertices[i].SequenceEqual(mesh.vertices)&&normals[i].SequenceEqual(mesh.normals)&&triangles[i].SequenceEqual(mesh.triangles),"exact original geometry: "+paths[i]);
                Require(bounds[i]==mesh.bounds&&uv1[i].SequenceEqual(mesh.uv2)&&uv2[i].SequenceEqual(mesh.uv3),"original bounds and unrelated UV channels: "+paths[i]);
                var edited=mesh.colors;var uv=mesh.uv;
                for(int j=0;j<vertices[i].Length;j++)
                {
                    var p=vertices[i][j];bool changed=edited[j]!=colors[i][j]||(grass[i].Length>0&&uv[j]!=grass[i][j]);
                    if(!changed)continue;
                    Require(SnowFrontSetup.Mask(p.x,p.z)>0&&SnowFrontSetup.Snow(p.x,p.z)>.001,"changes restricted to authored snow approach");
                    if(paths[i].Contains("/Land "))groundCount++;else landscapeCount++;
                    Require(new[]{edited[j].r,edited[j].g,edited[j].b}.All(v=>!float.IsNaN(v)&&!float.IsInfinity(v)&&v>=0&&v<=1)
                        &&edited[j].a==colors[i][j].a,"finite RGB and original authored alpha: "+mesh.name+" "+edited[j]);
                }
            }
            var group=terrain.World.Scenery.Find(SnowFrontSetup.GroupName);var filters=group.GetComponentsInChildren<MeshFilter>(true);
            Require(group.GetComponentsInChildren<Collider>(true).Length==0&&group.GetComponentsInChildren<MonoBehaviour>(true).Length==0,"merged scenery has no runtime scripts or collision");
            foreach(var filter in filters)
            {
                Require(filter.gameObject.layer==2&&filter.GetComponent<Renderer>().sharedMaterial==AssetDatabase.LoadAssetAtPath<Material>(IslandBiomeSetup.MaterialFolder+"/Biome Details.mat"),"shared AA material and mouse filtering");
                foreach(var vertex in filter.sharedMesh.vertices)
                    Require(StarterIslandLayout.RouteDistance(vertex.x,vertex.z)>2.3&&!StarterIslandLayout.CanPlace(Mathf.RoundToInt(vertex.x),Mathf.RoundToInt(vertex.z)),"entire decoration footprint stays outside route centre and buildable tiles");
            }
            var newMeshes=filters.Select(f=>AssetDatabase.GetAssetPath(f.sharedMesh)).ToArray();var all=paths.Concat(newMeshes).Concat(new[]{TerrainSetup.PrefabPath}).ToArray();
            var bytes=all.Select(File.ReadAllBytes).ToArray();SnowFrontSetup.Rebuild();PrototypeAddressablesSetup.EnsureSetup();
            for(int i=0;i<all.Length;i++)Require(bytes[i].SequenceEqual(File.ReadAllBytes(all[i])),"repeat bake and Setup preserve authored result: "+all[i]);
            for(int i=0;i<protect.Length;i++)Require(protectedBytes[i].SequenceEqual(File.ReadAllBytes(protect[i])),"protected coast/village/map/material retained");
            VerifyManualEdit(all,bytes,paths[0]);
            var report=new Report{editedMeshes=paths,newMeshes=newMeshes,groundVertices=groundCount,landscapeVertices=landscapeCount,sites=SnowFrontSetup.PlanSites().Length,batches=filters.Length};
            Directory.CreateDirectory("Logs/SnowFront");File.WriteAllText("Logs/SnowFront/Bake.json",JsonUtility.ToJson(report,true));
            Debug.Log("Snow approach bake verification PASSED: "+groundCount+" ground / "+landscapeCount+" scenery vertices recoloured; "+report.sites+" sites in "+report.batches+" batches; all original geometry/collision retained.");
        }
        static void VerifyManualEdit(string[] paths,byte[][] bytes,string meshPath)
        {
            try
            {
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);var colors=mesh.colors;colors[0]=Color.magenta;mesh.colors=colors;EditorUtility.SetDirty(mesh);AssetDatabase.SaveAssets();
                var tuned=paths.Select(File.ReadAllBytes).ToArray();SnowFrontSetup.Rebuild();PrototypeAddressablesSetup.EnsureSetup();
                for(int i=0;i<paths.Length;i++)Require(tuned[i].SequenceEqual(File.ReadAllBytes(paths[i])),"baked marker and ordinary Setup preserve later artist colours");
            }
            finally{for(int i=0;i<paths.Length;i++){File.WriteAllBytes(paths[i],bytes[i]);AssetDatabase.ImportAsset(paths[i],ImportAssetOptions.ForceSynchronousImport);}}
        }
        public static void VerifyRuntime(GameManager g)
        {
            var terrain=g.Terrain.Terrain;var group=terrain.World.Scenery.Find(SnowFrontSetup.GroupName);
            Require(group!=null&&group.GetComponentsInChildren<Collider>(true).Length==0,"Packed AA snow batches are loaded without new collision");
            var player=g.Players.Player;var method=player.GetType().GetMethod("TickMovement",BindingFlags.Instance|BindingFlags.NonPublic);var camera=player.ViewCamera;
            var position=player.transform.position;var rotation=camera.transform.rotation;
            try
            {
                camera.transform.rotation=Quaternion.Euler(45,0,0);
                Require(g.Terrain.TryResolve(new Vector3(350,0,820),out var start),"actual northeast grassland approach floor");g.Players.RestorePosition(start);
                foreach(var goal in new[]{new Vector2(220,870),new Vector2(200,920),new Vector2(140,985)})
                {
                    int steps=0;
                    while(Vector2.Distance(new Vector2(player.transform.position.x,player.transform.position.z),goal)>.6f&&steps++<1800)
                    {var direction=goal-new Vector2(player.transform.position.x,player.transform.position.z);method.Invoke(player,new object[]{direction.normalized,.05f});}
                    Require(steps<1800,"actual CharacterController can walk across the new transition to "+goal);
                }
            }
            finally{g.Players.RestorePosition(position);camera.transform.rotation=rotation;}
            Debug.Log("Snow approach runtime walking verification PASSED.");
        }
    }
}
