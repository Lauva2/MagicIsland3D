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
    public static class LakeSnowTrailVerification
    {
        static void Require(bool ok,string why){if(!ok)throw new InvalidOperationException("Lake snow trail verification: "+why);}
        static Dictionary<string,string> Hierarchy(GameObject root)
        {
            string Key(Transform t)=>t.parent==null?t.name:Key(t.parent)+"/"+t.name+":"+t.GetSiblingIndex();
            return root.GetComponentsInChildren<Transform>(true).ToDictionary(Key,t=>t.localPosition.ToString("R")+t.localRotation.ToString("R")+t.localScale.ToString("R")+t.gameObject.layer+"/"+t.gameObject.activeSelf+"/"+string.Join(",",t.GetComponents<Component>().Select(c=>c.GetType().FullName)));
        }
        static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
        static bool Covered(Mesh mesh,Vector2 p)
        {
            var v=mesh.vertices;var triangles=mesh.triangles;
            for(int i=0;i<triangles.Length;i+=3)
            {
                var a=new Vector2(v[triangles[i]].x,v[triangles[i]].z);var b=new Vector2(v[triangles[i+1]].x,v[triangles[i+1]].z);var c=new Vector2(v[triangles[i+2]].x,v[triangles[i+2]].z);
                float ab=Cross(b-a,p-a),bc=Cross(c-b,p-b),ca=Cross(a-c,p-c);
                if(ab>=-.00005f&&bc>=-.00005f&&ca>=-.00005f||ab<=.00005f&&bc<=.00005f&&ca<=.00005f)return true;
            }
            return false;
        }
        public static void BakeAndVerify()
        {
            Require(Application.dataPath.Replace('\\','/').Contains("/Logs/"),"use an isolated project");
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(TerrainSetup.PrefabPath);var terrain=prefab.GetComponent<IslandTerrain>();
            var hierarchy=Hierarchy(prefab);var old=AssetDatabase.LoadAssetAtPath<Mesh>(LakeSnowTrailSetup.OldPath);var plan=LakeSnowTrailSetup.Prepare(old);
            var vertices=old.vertices;var colors=old.colors;var normals=old.normals;var uv=old.uv;var bounds=old.bounds;
            string guid=AssetDatabase.AssetPathToGUID(LakeSnowTrailSetup.OldPath);
            var colliders=terrain.GetComponentsInChildren<Collider>(true).Select(c=>c.name+":"+AssetDatabase.GetAssetPath((c as MeshCollider)?.sharedMesh)).ToArray();
            string[] protect={OceanSetup.PrefabPath,CloudSetup.PrefabPath,CloudSetup.CookiePath,MapPanelSetup.Path,ArrivalRouteSetup.GroundPath,VillageArtSetup.PathsPath,"Assets/Art/Materials/Terrain/Biome Details.mat","Assets/Art/Materials/Terrain/Biome Ground.mat","Assets/Scripts/Interaction/StarterIslandLayout.cs"};
            var protectedBytes=protect.Select(File.ReadAllBytes).ToArray();
            LakeSnowTrailSetup.Refine();
            prefab=AssetDatabase.LoadAssetAtPath<GameObject>(TerrainSetup.PrefabPath);terrain=prefab.GetComponent<IslandTerrain>();var current=Hierarchy(prefab);
            foreach(var entry in hierarchy)Require(current.TryGetValue(entry.Key,out var value)&&value==entry.Value,"old objects retained: "+entry.Key);
            Require(colliders.SequenceEqual(terrain.GetComponentsInChildren<Collider>(true).Select(c=>c.name+":"+AssetDatabase.GetAssetPath((c as MeshCollider)?.sharedMesh))),"entire collision set retained");
            old=AssetDatabase.LoadAssetAtPath<Mesh>(LakeSnowTrailSetup.OldPath);
            Require(vertices.SequenceEqual(old.vertices)&&colors.SequenceEqual(old.colors)&&normals.SequenceEqual(old.normals)&&uv.SequenceEqual(old.uv)&&bounds==old.bounds,"all original path vertex channels and bounds retained");
            Require(plan.Remainder.SequenceEqual(old.triangles)&&guid==AssetDatabase.AssetPathToGUID(LakeSnowTrailSetup.OldPath),"only selected faces removed; original GUID and other faces retained");
            var node=terrain.World.transform.Find(LakeSnowTrailSetup.GroupName);var mesh=node.GetComponent<MeshFilter>().sharedMesh;
            Require(node.gameObject.layer==2&&node.GetComponent<Collider>()==null&&node.GetComponents<MonoBehaviour>().Length==0,"one baked non-blocking ribbon");
            Require(node.GetComponent<Renderer>().sharedMaterial==AssetDatabase.LoadAssetAtPath<Material>(LakeSnowTrailSetup.MaterialPath)&&mesh.normals.All(n=>n.y>0),"shared material and upward faces");
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
            int samples=0;
            // Cover both turns and both old/new boundaries, including the old triangular wedges.
            foreach(var point in plan.Points)
                for(float dx=-.8f;dx<=.8f;dx+=.2f)for(float dz=-.8f;dz<=.8f;dz+=.2f)
                {var p=point+new Vector2(dx,dz);Require(Covered(mesh,p)||Covered(old,p),"connected corner and end surface at "+p);samples++;}
            string[] paths={LakeSnowTrailSetup.OldPath,LakeSnowTrailSetup.MeshPath,LakeSnowTrailSetup.MaterialPath,TerrainSetup.PrefabPath};var bytes=paths.Select(File.ReadAllBytes).ToArray();
            LakeSnowTrailSetup.Refine();PrototypeAddressablesSetup.EnsureSetup();
            VerifyWorldRebuildPreservation();
            for(int i=0;i<paths.Length;i++)Require(bytes[i].SequenceEqual(File.ReadAllBytes(paths[i])),"repeat and ordinary Setup retain result");
            for(int i=0;i<protect.Length;i++)Require(protectedBytes[i].SequenceEqual(File.ReadAllBytes(protect[i])),"protected world/village/cloud/material: "+protect[i]);
            try
            {
                var painted=mesh.colors;painted[0]=Color.magenta;mesh.colors=painted;EditorUtility.SetDirty(mesh);
                var material=AssetDatabase.LoadAssetAtPath<Material>(LakeSnowTrailSetup.MaterialPath);material.SetFloat("_EdgeSoftness",.34f);EditorUtility.SetDirty(material);AssetDatabase.SaveAssets();
                var manual=paths.Select(File.ReadAllBytes).ToArray();LakeSnowTrailSetup.Refine();PrototypeAddressablesSetup.EnsureSetup();
                for(int i=0;i<paths.Length;i++)Require(manual[i].SequenceEqual(File.ReadAllBytes(paths[i])),"later manual mesh/material edits retained");
            }
            finally{for(int i=0;i<paths.Length;i++){File.WriteAllBytes(paths[i],bytes[i]);AssetDatabase.ImportAsset(paths[i],ImportAssetOptions.ForceSynchronousImport);}}
            Directory.CreateDirectory("Logs/LakeSnowTrail");File.WriteAllText("Logs/LakeSnowTrail/Bake.txt","PASS: "+plan.RemovedQuads.Length+" selected old quads replaced; "+v.Length+" ribbon vertices; "+samples+" corner/end coverage samples; lift "+minLift+".."+maxLift+"; original geometry/collisions, repeat/Setup/manual preservation.\n");
            Debug.Log("Lake snow trail bake verification PASSED: "+plan.RemovedQuads.Length+" selected quads, "+v.Length+" vertices, "+samples+" corner/end samples; triangle lift "+minLift+".."+maxLift+".");
        }
        public static void VerifyRuntime(GameManager g)
        {
            var node=g.Terrain.Terrain.World.transform.Find(LakeSnowTrailSetup.GroupName);
            Require(node!=null&&node.GetComponent<Collider>()==null&&node.GetComponent<Renderer>().sharedMaterial.shader.isSupported,"actual Packed ribbon loaded");
            SnowFrontVerification.VerifyRuntime(g);
            VerifyWorldRebuildPreservation();
            Debug.Log("Lake snow trail actual Packed walking verification PASSED.");
        }
        static void VerifyWorldRebuildPreservation()
        {
            var terrain=AssetDatabase.LoadAssetAtPath<GameObject>(TerrainSetup.PrefabPath).GetComponent<IslandTerrain>();
            var mesh=(Mesh)typeof(StarterIslandWorldSetup).GetMethod("Paths",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
            try
            {
                var v=mesh.vertices;var plan=LakeSnowTrailSetup.Prepare(mesh);
                LakeSnowTrailSetup.PreserveOnWorldRebuild(terrain.World.transform,mesh);
                Require(v.SequenceEqual(mesh.vertices)&&plan.Remainder.SequenceEqual(mesh.triangles),"whole-world rebuild keeps the authored ribbon without duplicate legacy faces");
            }
            finally{UnityEngine.Object.DestroyImmediate(mesh);}
        }
    }
}
