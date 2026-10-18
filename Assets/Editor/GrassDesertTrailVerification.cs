using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using MagicIsland.Interaction;
using MagicIsland.Managers;
using MagicIsland.TerrainSystem;
using UnityEditor;
using UnityEngine;

namespace MagicIsland.EditorTools
{
    public static class GrassDesertTrailVerification
    {
        static void Require(bool ok,string why){if(!ok)throw new InvalidOperationException("Grass desert trail verification: "+why);}
        static Dictionary<string,string> Hierarchy(GameObject root)
        {
            string Key(Transform t)=>t.parent==null?t.name:Key(t.parent)+"/"+t.name+":"+t.GetSiblingIndex();
            return root.GetComponentsInChildren<Transform>(true).ToDictionary(Key,t=>t.localPosition.ToString("R")+t.localRotation.ToString("R")+t.localScale.ToString("R")+t.gameObject.layer+"/"+t.gameObject.activeSelf+"/"+string.Join(",",t.GetComponents<Component>().Select(c=>c.GetType().FullName)));
        }
        static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
        static bool Covered(Vector3[] v,int[] triangles,Vector2 p)
        {
            for(int i=0;i<triangles.Length;i+=3)
            {
                var a=new Vector2(v[triangles[i]].x,v[triangles[i]].z);var b=new Vector2(v[triangles[i+1]].x,v[triangles[i+1]].z);var c=new Vector2(v[triangles[i+2]].x,v[triangles[i+2]].z);
                float ab=Cross(b-a,p-a),bc=Cross(c-b,p-b),ca=Cross(a-c,p-c);
                if(ab>=-.00005f&&bc>=-.00005f&&ca>=-.00005f||ab<=.00005f&&bc<=.00005f&&ca<=.00005f)return true;
            }
            return false;
        }
        static int StrictCoverage(Vector3[] v,int[] triangles,Vector2 p)
        {
            int count=0;
            for(int i=0;i<triangles.Length;i+=3)
            {
                var a=new Vector2(v[triangles[i]].x,v[triangles[i]].z);var b=new Vector2(v[triangles[i+1]].x,v[triangles[i+1]].z);var c=new Vector2(v[triangles[i+2]].x,v[triangles[i+2]].z);
                float ab=Cross(b-a,p-a),bc=Cross(c-b,p-b),ca=Cross(a-c,p-c);
                if(ab>.000002f&&bc>.000002f&&ca>.000002f||ab<-.000002f&&bc<-.000002f&&ca<-.000002f)count++;
            }
            return count;
        }
        public static void BakeAndVerify()
        {
            Require(Application.dataPath.Replace('\\','/').Contains("/Logs/"),"use an isolated project");
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(TerrainSetup.PrefabPath);var terrain=prefab.GetComponent<IslandTerrain>();
            var hierarchy=Hierarchy(prefab);var old=AssetDatabase.LoadAssetAtPath<Mesh>(GrassDesertTrailSetup.OldPath);var plan=GrassDesertTrailSetup.Prepare(old);
            var vertices=old.vertices;var colors=old.colors;var normals=old.normals;var uv=old.uv;var bounds=old.bounds;
            string guid=AssetDatabase.AssetPathToGUID(GrassDesertTrailSetup.OldPath);
            var colliders=terrain.GetComponentsInChildren<Collider>(true).Select(c=>c.name+":"+AssetDatabase.GetAssetPath((c as MeshCollider)?.sharedMesh)).ToArray();
            string[] protect={LakeSnowTrailSetup.MeshPath,LakeSnowTrailSetup.MaterialPath,"Assets/Art/Shaders/ExplorationTrail.shader",OceanSetup.PrefabPath,CloudSetup.PrefabPath,CloudSetup.CookiePath,MapPanelSetup.Path,ArrivalRouteSetup.GroundPath,VillageArtSetup.PathsPath,"Assets/Art/Materials/Terrain/Biome Details.mat","Assets/Art/Materials/Terrain/Biome Ground.mat","Assets/Scripts/Interaction/StarterIslandLayout.cs"};
            var protectedBytes=protect.Select(File.ReadAllBytes).ToArray();
            GrassDesertTrailSetup.Refine();
            prefab=AssetDatabase.LoadAssetAtPath<GameObject>(TerrainSetup.PrefabPath);terrain=prefab.GetComponent<IslandTerrain>();var current=Hierarchy(prefab);
            foreach(var entry in hierarchy)Require(current.TryGetValue(entry.Key,out var value)&&value==entry.Value,"old objects retained: "+entry.Key);
            Require(colliders.SequenceEqual(terrain.GetComponentsInChildren<Collider>(true).Select(c=>c.name+":"+AssetDatabase.GetAssetPath((c as MeshCollider)?.sharedMesh))),"entire collision set retained");
            old=AssetDatabase.LoadAssetAtPath<Mesh>(GrassDesertTrailSetup.OldPath);
            Require(vertices.SequenceEqual(old.vertices)&&colors.SequenceEqual(old.colors)&&normals.SequenceEqual(old.normals)&&uv.SequenceEqual(old.uv)&&bounds==old.bounds,"all original path vertex channels and bounds retained");
            Require(plan.Remainder.SequenceEqual(old.triangles)&&guid==AssetDatabase.AssetPathToGUID(GrassDesertTrailSetup.OldPath),"only selected faces removed; original GUID and other faces retained");
            var node=terrain.World.transform.Find(GrassDesertTrailSetup.GroupName);var mesh=node.GetComponent<MeshFilter>().sharedMesh;
            Require(node.gameObject.layer==2&&node.GetComponent<Collider>()==null&&node.GetComponents<MonoBehaviour>().Length==0,"one baked non-blocking ribbon");
            Require(node.GetComponent<Renderer>().sharedMaterial==AssetDatabase.LoadAssetAtPath<Material>(GrassDesertTrailSetup.MaterialPath)&&mesh.normals.All(n=>n.y>0),"shared material and upward faces");
            var v=mesh.vertices;var faces=mesh.triangles;
            Require(v.All(p=>StarterIslandLayout.RouteDistance(p.x,p.z)<2.4&&!StarterIslandLayout.CanPlace(Mathf.RoundToInt(p.x),Mathf.RoundToInt(p.z))),"complete footprint inside original non-buildable route");
            float minLift=float.MaxValue,maxLift=float.MinValue;
            var floorRoot=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);
            try
            {
                var world=floorRoot.GetComponent<IslandTerrain>().World;
                for(int i=0;i<faces.Length;i+=3)
                {
                    var p=(v[faces[i]]+v[faces[i+1]]+v[faces[i+2]])/3;
                    Require(world.Raycast(new Ray(new Vector3(p.x,300,p.z),Vector3.down),out var hit,350),"triangle has physical ground");
                    float lift=p.y-hit.point.y;minLift=Mathf.Min(minLift,lift);maxLift=Mathf.Max(maxLift,lift);
                    Require(lift>.005f&&lift<.20f,"road triangle stays above real ground without floating: "+lift+" at "+p);
                }
            }
            finally{PrefabUtility.UnloadPrefabContents(floorRoot);}
            int samples=0;var remaining=old.triangles;
            // Cover both turns and both old/new boundaries, including the old triangular wedges.
            foreach(var point in plan.Points)
                for(float dx=-.8f;dx<=.8f;dx+=.2f)for(float dz=-.8f;dz<=.8f;dz+=.2f)
                {var p=point+new Vector2(dx,dz);Require(Covered(v,faces,p)||Covered(vertices,remaining,p),"connected corner and end surface at "+p);samples++;}
            foreach(var center in plan.Points.Take(2))
                for(float dx=-1.5f;dx<1.5f;dx+=.25f)for(float dz=-1.5f;dz<1.5f;dz+=.25f)
                {var p=center+new Vector2(dx+.017f,dz+.023f);if(StarterIslandLayout.RouteDistance(p.x,p.y)>1.2)continue;Require(StrictCoverage(v,faces,p)<=1,"junction has no overlapping transparent faces at "+p);}
            string[] paths={GrassDesertTrailSetup.OldPath,GrassDesertTrailSetup.MeshPath,GrassDesertTrailSetup.MaterialPath,TerrainSetup.PrefabPath};var bytes=paths.Select(File.ReadAllBytes).ToArray();
            GrassDesertTrailSetup.Refine();PrototypeAddressablesSetup.EnsureSetup();
            VerifyWorldRebuildPreservation();
            for(int i=0;i<paths.Length;i++)Require(bytes[i].SequenceEqual(File.ReadAllBytes(paths[i])),"repeat and ordinary Setup retain result");
            for(int i=0;i<protect.Length;i++)Require(protectedBytes[i].SequenceEqual(File.ReadAllBytes(protect[i])),"protected world/village/cloud/material: "+protect[i]);
            try
            {
                var painted=mesh.colors;painted[0]=Color.magenta;mesh.colors=painted;EditorUtility.SetDirty(mesh);
                var material=AssetDatabase.LoadAssetAtPath<Material>(GrassDesertTrailSetup.MaterialPath);material.SetFloat("_EdgeSoftness",.34f);EditorUtility.SetDirty(material);AssetDatabase.SaveAssets();
                var manual=paths.Select(File.ReadAllBytes).ToArray();GrassDesertTrailSetup.Refine();PrototypeAddressablesSetup.EnsureSetup();
                for(int i=0;i<paths.Length;i++)Require(manual[i].SequenceEqual(File.ReadAllBytes(paths[i])),"later manual mesh/material edits retained");
            }
            finally{for(int i=0;i<paths.Length;i++){File.WriteAllBytes(paths[i],bytes[i]);AssetDatabase.ImportAsset(paths[i],ImportAssetOptions.ForceSynchronousImport);}}
            Directory.CreateDirectory("Logs/GrassDesertTrail");File.WriteAllText("Logs/GrassDesertTrail/Bake.txt","PASS: "+plan.RemovedQuads.Length+" selected old quads replaced; "+v.Length+" ribbon vertices; "+samples+" corner/end coverage samples; lift "+minLift+".."+maxLift+"; original geometry/collisions, repeat/Setup/manual preservation.\n");
            Debug.Log("Grass desert trail bake verification PASSED: "+plan.RemovedQuads.Length+" selected quads, "+v.Length+" vertices, "+samples+" corner/end samples; triangle lift "+minLift+".."+maxLift+".");
        }
        public static void VerifyRuntime(GameManager g)
        {
            var node=g.Terrain.Terrain.World.transform.Find(GrassDesertTrailSetup.GroupName);
            Require(node!=null&&node.GetComponent<Collider>()==null&&node.GetComponent<Renderer>().sharedMaterial.shader.isSupported,"actual Packed ribbon loaded");
            Walk(g);
            VerifyWorldRebuildPreservation();
            Debug.Log("Grass desert trail actual Packed walking verification PASSED.");
        }
        static void Walk(GameManager g)
        {
            var player=g.Players.Player;var method=player.GetType().GetMethod("TickMovement",BindingFlags.Instance|BindingFlags.NonPublic);var camera=player.ViewCamera;
            var position=player.transform.position;var rotation=camera.transform.rotation;
            try
            {
                camera.transform.rotation=Quaternion.Euler(45,0,0);
                Require(g.Terrain.TryResolve(new Vector3(0,0,240),out var start),"actual first junction floor");g.Players.RestorePosition(start);
                foreach(var goal in new[]{new Vector2(-150,370),new Vector2(-280,500),new Vector2(-390,565)})
                {
                    int steps=0;
                    while(Vector2.Distance(new Vector2(player.transform.position.x,player.transform.position.z),goal)>.6f&&steps++<2200)
                    {var direction=goal-new Vector2(player.transform.position.x,player.transform.position.z);method.Invoke(player,new object[]{direction.normalized,.05f});}
                    Require(steps<2200,"actual player traverses west route to "+goal);
                }
                Require(g.Terrain.TryResolve(new Vector3(-150,0,370),out start),"actual lake branch junction floor");g.Players.RestorePosition(start);
                int branchSteps=0;var branch=new Vector2(-100,520);
                while(Vector2.Distance(new Vector2(player.transform.position.x,player.transform.position.z),branch)>.6f&&branchSteps++<2200)
                {var d=branch-new Vector2(player.transform.position.x,player.transform.position.z);method.Invoke(player,new object[]{d.normalized,.05f});}
                Require(branchSteps<2200,"original lake branch stays walkable");
            }
            finally{g.Players.RestorePosition(position);camera.transform.rotation=rotation;}
        }
        static void VerifyWorldRebuildPreservation()
        {
            var terrain=AssetDatabase.LoadAssetAtPath<GameObject>(TerrainSetup.PrefabPath).GetComponent<IslandTerrain>();
            var mesh=(Mesh)typeof(StarterIslandWorldSetup).GetMethod("Paths",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
            try
            {
                var v=mesh.vertices;LakeSnowTrailSetup.PreserveOnWorldRebuild(terrain.World.transform,mesh);var plan=GrassDesertTrailSetup.Prepare(mesh);
                GrassDesertTrailSetup.PreserveOnWorldRebuild(terrain.World.transform,mesh);
                Require(v.SequenceEqual(mesh.vertices)&&plan.Remainder.SequenceEqual(mesh.triangles),"whole-world rebuild keeps the authored ribbon without duplicate legacy faces");
            }
            finally{UnityEngine.Object.DestroyImmediate(mesh);}
        }
    }
}
