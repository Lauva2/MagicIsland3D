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
    public static class ForestEdgeVerification
    {
        static void Require(bool ok,string why){if(!ok)throw new InvalidOperationException("Forest edge verification: "+why);}
        [Serializable] public sealed class Report { public string[] editedMeshes,newMeshes;public int groundVertices,crownVertices,crownLobes,sites,batches; }
        static Dictionary<string,string> Hierarchy(GameObject root)
        {
            string Key(Transform t)=>t.parent==null?t.name:Key(t.parent)+"/"+t.name+":"+t.GetSiblingIndex();
            return root.GetComponentsInChildren<Transform>(true).ToDictionary(Key,t=>t.localPosition.ToString("R")+t.localRotation.ToString("R")+t.localScale.ToString("R")+t.gameObject.layer+"/"+t.gameObject.activeSelf+"/"+string.Join(",",t.GetComponents<Component>().Select(c=>c.GetType().FullName)));
        }
        static Vector2[] UV(Mesh mesh,int channel){var values=new List<Vector2>();mesh.GetUVs(channel,values);return values.ToArray();}
        static Dictionary<string,byte[]> CollisionFiles(IslandTerrain terrain)
            =>terrain.GetComponentsInChildren<MeshCollider>(true).Select(c=>AssetDatabase.GetAssetPath(c.sharedMesh)).Where(p=>!string.IsNullOrEmpty(p)).Distinct().ToDictionary(p=>p,File.ReadAllBytes);
        public static void BakeAndVerify()
        {
            Require(Application.dataPath.Replace('\\','/').Contains("/Logs/"),"use an isolated project");
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(TerrainSetup.PrefabPath);var terrain=prefab.GetComponent<IslandTerrain>();
            var hierarchy=Hierarchy(prefab);var plan=ForestEdgeSetup.PlanEdits(terrain);Require(plan.Length>0,"unrefined baseline");
            var paths=plan.Select(e=>AssetDatabase.GetAssetPath(e.Mesh)).ToArray();
            var vertices=plan.Select(e=>e.Mesh.vertices).ToArray();var normals=plan.Select(e=>e.Mesh.normals).ToArray();var colors=plan.Select(e=>e.Mesh.colors).ToArray();
            var triangles=plan.Select(e=>e.Mesh.triangles).ToArray();var bounds=plan.Select(e=>e.Mesh.bounds).ToArray();
            var channels=plan.Select(e=>Enumerable.Range(0,4).Select(c=>UV(e.Mesh,c)).ToArray()).ToArray();
            var collision=CollisionFiles(terrain);var colliderNames=terrain.GetComponentsInChildren<Collider>(true).Select(c=>c.name+":"+c.GetType().Name).ToArray();
            // Reject a foreign artist channel before any resource is written.
            try
            {
                plan[0].Mesh.SetUVs(4,Enumerable.Repeat(Vector2.one,plan[0].Mesh.vertexCount).ToArray());bool rejected=false;
                try{ForestEdgeSetup.PlanEdits(terrain);}catch(InvalidOperationException){rejected=true;}
                Require(rejected,"foreign UV4 is protected");
            }
            finally{plan[0].Mesh.SetUVs(4,Array.Empty<Vector2>());}
            ForestEdgeSetup.Rebuild();
            prefab=AssetDatabase.LoadAssetAtPath<GameObject>(TerrainSetup.PrefabPath);terrain=prefab.GetComponent<IslandTerrain>();var current=Hierarchy(prefab);
            foreach(var pair in hierarchy)Require(current.TryGetValue(pair.Key,out var value)&&value==pair.Value,"original hierarchy retained: "+pair.Key);
            Require(colliderNames.SequenceEqual(terrain.GetComponentsInChildren<Collider>(true).Select(c=>c.name+":"+c.GetType().Name)),"entire original collision set retained");
            // Ground meshes add only vertex colours and an unused editor UV channel; collision vertices and indices stay exact.
            int groundCount=0,crownCount=0;
            for(int i=0;i<plan.Length;i++)
            {
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(paths[i]);var v=mesh.vertices;var n=mesh.normals;var c=mesh.colors;
                Require(triangles[i].SequenceEqual(mesh.triangles),"original triangle topology");
                for(int channel=0;channel<4;channel++)Require(channels[i][channel].SequenceEqual(UV(mesh,channel)),"original UV channel "+channel);
                if(plan[i].Ground)Require(vertices[i].SequenceEqual(v)&&normals[i].SequenceEqual(n)&&bounds[i]==mesh.bounds,"exact physical floor geometry");
                else for(int first=0;first+70<=v.Length;first++)
                {
                    if(!plan[i].Changed[first])continue;
                    var originalBounds=new Bounds(vertices[i][first],Vector3.zero);var shapedBounds=new Bounds(v[first],Vector3.zero);
                    for(int j=first;j<first+70;j++){originalBounds.Encapsulate(vertices[i][j]);shapedBounds.Encapsulate(v[j]);}
                    var ratio=new Vector3(shapedBounds.size.x/originalBounds.size.x,shapedBounds.size.y/originalBounds.size.y,shapedBounds.size.z/originalBounds.size.z);
                    Require(ratio.x>=.70f&&ratio.x<=1.06f&&ratio.z>=.70f&&ratio.z<=1.06f&&ratio.y>=.80f&&ratio.y<=1.20f,"crown retains a full rounded silhouette through feathered rotation: "+ratio);
                    first+=69;
                }
                for(int j=0;j<v.Length;j++)
                {
                    Require(c[j].a==colors[i][j].a,"original authored alpha");
                    Require(new[]{v[j].x,v[j].y,v[j].z,n[j].x,n[j].y,n[j].z,c[j].r,c[j].g,c[j].b}.All(f=>!float.IsNaN(f)&&!float.IsInfinity(f)),"finite mesh channels");
                    if(!plan[i].Changed[j])Require(v[j]==vertices[i][j]&&n[j]==normals[i][j]&&c[j]==colors[i][j],"unselected scenery/trunk vertices retained");
                    else if(plan[i].Ground){groundCount++;Require(ForestEdgeSetup.Mask(v[j].x,v[j].z)>0,"bounded floor palette");}
                    else
                    {
                        crownCount++;Require(colors[i][j].g-colors[i][j].r>.065f&&colors[i][j].g-colors[i][j].b>.05f,"only original leaf vertices deform");
                        Require(Vector3.Distance(v[j],vertices[i][j])<12&&v[j].x>337&&v[j].x<583&&v[j].z>517&&v[j].z<738,"bounded local crown deformation");
                    }
                }
            }
            foreach(var pair in collision)
                if(!paths.Contains(pair.Key))Require(pair.Value.SequenceEqual(File.ReadAllBytes(pair.Key)),"all non-ground collision mesh bytes retained: "+pair.Key);
            var group=terrain.World.Scenery.Find(ForestEdgeSetup.GroupName);var filters=group.GetComponentsInChildren<MeshFilter>(true);
            Require(group.GetComponentsInChildren<Collider>(true).Length==0&&group.GetComponentsInChildren<MonoBehaviour>(true).Length==0,"no new runtime scripts or collisions");
            foreach(var site in ForestEdgeSetup.PlanSites())Require(ForestEdgeSetup.ClearFootprint(site.Point,1.05f),"full dense-sampled decoration footprint");
            foreach(var filter in filters)
            {
                Require(filter.gameObject.layer==2&&filter.GetComponent<Renderer>().sharedMaterial==AssetDatabase.LoadAssetAtPath<Material>(IslandBiomeSetup.MaterialFolder+"/Biome Details.mat"),"shared original AA material");
                Require(filter.sharedMesh.normals.All(n=>n.sqrMagnitude>.90f),"all new foliage has valid lighting normals");
                foreach(var p in filter.sharedMesh.vertices)
                    Require(StarterIslandLayout.RouteDistance(p.x,p.z)>3&&!StarterIslandLayout.CanPlace(Mathf.RoundToInt(p.x),Mathf.RoundToInt(p.z))
                        &&Vector2.Distance(new Vector2(p.x,p.z),new Vector2(480,650))>38,"whole decoration stays outside route, construction cells and landmark");
            }
            var newMeshes=filters.Select(f=>AssetDatabase.GetAssetPath(f.sharedMesh)).ToArray();var all=paths.Concat(newMeshes).Concat(new[]{TerrainSetup.PrefabPath}).ToArray();var bytes=all.Select(File.ReadAllBytes).ToArray();
            ForestEdgeSetup.Rebuild();PrototypeAddressablesSetup.EnsureSetup();
            for(int i=0;i<all.Length;i++)Require(bytes[i].SequenceEqual(File.ReadAllBytes(all[i])),"repeat and ordinary Setup preserve baked art");
            try
            {
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(paths.First(p=>p.Contains("/Landscape ")));var paint=mesh.colors;paint[0]=Color.magenta;mesh.colors=paint;
                var shape=mesh.vertices;shape[0]+=Vector3.up*.01f;mesh.vertices=shape;EditorUtility.SetDirty(mesh);AssetDatabase.SaveAssets();
                var tuned=all.Select(File.ReadAllBytes).ToArray();ForestEdgeSetup.Rebuild();PrototypeAddressablesSetup.EnsureSetup();
                for(int i=0;i<all.Length;i++)Require(tuned[i].SequenceEqual(File.ReadAllBytes(all[i])),"later manual colour/shape edits retained");
            }
            finally{for(int i=0;i<all.Length;i++){File.WriteAllBytes(all[i],bytes[i]);AssetDatabase.ImportAsset(all[i],ImportAssetOptions.ForceSynchronousImport);}}
            var report=new Report{editedMeshes=paths,newMeshes=newMeshes,groundVertices=groundCount,crownVertices=crownCount,crownLobes=plan.Sum(e=>e.Crowns),sites=ForestEdgeSetup.PlanSites().Length,batches=filters.Length};
            Directory.CreateDirectory("Logs/ForestEdge");File.WriteAllText("Logs/ForestEdge/Bake.json",JsonUtility.ToJson(report,true));
            Debug.Log("Forest edge bake verification PASSED: "+groundCount+" ground / "+crownCount+" leaf vertices; "+report.crownLobes+" crown lobes; "+report.sites+" clusters / "+report.batches+" batches; old geometry, collision, UVs, hierarchy and hand edits preserved.");
        }
        public static void VerifyRuntime(GameManager g)
        {
            var group=g.Terrain.Terrain.World.Scenery.Find(ForestEdgeSetup.GroupName);
            Require(group!=null&&group.GetComponentsInChildren<Collider>(true).Length==0,"real Packed AA forest loaded without new collision");
            var player=g.Players.Player;var method=player.GetType().GetMethod("TickMovement",BindingFlags.Instance|BindingFlags.NonPublic);var camera=player.ViewCamera;
            var position=player.transform.position;var rotation=camera.transform.rotation;
            try
            {
                camera.transform.rotation=Quaternion.Euler(45,0,0);
                Require(g.Terrain.TryResolve(new Vector3(470,0,720),out var start),"actual forest entrance floor");g.Players.RestorePosition(start);
                foreach(var goal in new[]{new Vector2(480,650),new Vector2(475,520)})
                {
                    int steps=0;
                    while(Vector2.Distance(new Vector2(player.transform.position.x,player.transform.position.z),goal)>.6f&&steps++<1800)
                    {var direction=goal-new Vector2(player.transform.position.x,player.transform.position.z);method.Invoke(player,new object[]{direction.normalized,.05f});}
                    Require(steps<1800,"actual CharacterController passes forest and Moonflower grove: "+goal);
                }
            }
            finally{g.Players.RestorePosition(position);camera.transform.rotation=rotation;}
            Debug.Log("Forest edge runtime walking verification PASSED.");
        }
    }
}
