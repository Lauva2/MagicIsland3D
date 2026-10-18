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
    public static class GrasslandCompositionVerification
    {
        static void Require(bool ok,string why){if(!ok)throw new InvalidOperationException("Grassland composition verification: "+why);}
        static Vector2[] UV(Mesh m,int channel){var uv=new List<Vector2>();m.GetUVs(channel,uv);return uv.ToArray();}
        [Serializable] public sealed class Report{public string[] editedMeshes,newMeshes;public int pockets,crownVertices,triangles;}
        public static void BakeAndVerify()
        {
            Require(Application.dataPath.Replace('\\','/').Contains("/Logs/"),"isolated project required");
            var root=AssetDatabase.LoadAssetAtPath<GameObject>(TerrainSetup.PrefabPath);var terrain=root.GetComponent<IslandTerrain>();
            var plan=GrasslandCompositionSetup.PlanCrowns(terrain);Require(plan.Length>0,"unrefined crown baseline");
            var edited=plan.Select(e=>AssetDatabase.GetAssetPath(e.Mesh)).ToArray();
            var v=plan.Select(e=>e.Mesh.vertices).ToArray();var n=plan.Select(e=>e.Mesh.normals).ToArray();var c=plan.Select(e=>e.Mesh.colors).ToArray();
            var tri=plan.Select(e=>e.Mesh.triangles).ToArray();var uv=plan.Select(e=>Enumerable.Range(0,6).Select(ch=>UV(e.Mesh,ch)).ToArray()).ToArray();
            var protectedPaths=Directory.GetFiles("Assets/Art","*",SearchOption.AllDirectories).Select(p=>p.Replace('\\','/')).Where(p=>p!=TerrainSetup.PrefabPath&&!edited.Contains(p)).ToArray();
            var protectedBytes=protectedPaths.Select(File.ReadAllBytes).ToArray();
            var collision=terrain.GetComponentsInChildren<Collider>(true).Select(a=>a.name+":"+EditorJsonUtility.ToJson(a)).ToArray();
            try
            {
                plan[0].Mesh.SetUVs(6,Enumerable.Repeat(Vector2.one,plan[0].Mesh.vertexCount).ToArray());bool rejected=false;
                try{GrasslandCompositionSetup.PlanCrowns(terrain);}catch(InvalidOperationException){rejected=true;}
                Require(rejected,"foreign UV6 protected before writes");
            }
            finally{plan[0].Mesh.SetUVs(6,Array.Empty<Vector2>());}
            GrasslandCompositionSetup.Rebuild();
            for(int i=0;i<plan.Length;i++)
            {
                var m=AssetDatabase.LoadAssetAtPath<Mesh>(edited[i]);var a=m.vertices;var b=m.normals;var paint=m.colors;var stamp=UV(m,6);
                Require(tri[i].SequenceEqual(m.triangles),"exact topology");for(int ch=0;ch<6;ch++)Require(uv[i][ch].SequenceEqual(UV(m,ch)),"existing UV channel "+ch);
                for(int j=0;j<a.Length;j++)
                {
                    Require(paint[j].a==c[i][j].a,"authored alpha");
                    if(stamp[j].x==0)Require(a[j]==v[i][j]&&b[j]==n[i][j]&&paint[j]==c[i][j],"all original trunks/unselected vertices retained");
                    else Require(c[i][j].g-c[i][j].r>.065f&&Vector3.Distance(a[j],v[i][j])<1.5f,"bounded original leaf edit");
                }
            }
            root=AssetDatabase.LoadAssetAtPath<GameObject>(TerrainSetup.PrefabPath);terrain=root.GetComponent<IslandTerrain>();
            Require(collision.SequenceEqual(terrain.GetComponentsInChildren<Collider>(true).Select(a=>a.name+":"+EditorJsonUtility.ToJson(a))),"entire original collision set");
            VerifyAssets();var filters=terrain.World.Scenery.Find(GrasslandCompositionSetup.Group).GetComponentsInChildren<MeshFilter>(true);
            var newMeshes=filters.Select(f=>AssetDatabase.GetAssetPath(f.sharedMesh)).ToArray();var watched=edited.Concat(newMeshes).Concat(new[]{TerrainSetup.PrefabPath}).ToArray();
            var bytes=watched.Select(File.ReadAllBytes).ToArray();
            GrasslandCompositionSetup.Rebuild();PrototypeAddressablesSetup.EnsureSetup();
            for(int i=0;i<watched.Length;i++)Require(bytes[i].SequenceEqual(File.ReadAllBytes(watched[i])),"repeat/Setup retain art "+watched[i]);
            try
            {
                foreach(var path in new[]{edited[0],newMeshes[0]})
                {
                    var m=AssetDatabase.LoadAssetAtPath<Mesh>(path);var manual=m.colors;manual[0]=Color.magenta;m.colors=manual;var shape=m.vertices;shape[0]+=Vector3.up*.01f;m.vertices=shape;EditorUtility.SetDirty(m);
                }
                AssetDatabase.SaveAssets();var manualBytes=watched.Select(File.ReadAllBytes).ToArray();GrasslandCompositionSetup.Rebuild();PrototypeAddressablesSetup.EnsureSetup();
                for(int i=0;i<watched.Length;i++)Require(manualBytes[i].SequenceEqual(File.ReadAllBytes(watched[i])),"later manual paint/shape retained");
            }
            finally{for(int i=0;i<watched.Length;i++){File.WriteAllBytes(watched[i],bytes[i]);AssetDatabase.ImportAsset(watched[i],ImportAssetOptions.ForceSynchronousImport);}}
            for(int i=0;i<protectedPaths.Length;i++)Require(protectedBytes[i].SequenceEqual(File.ReadAllBytes(protectedPaths[i])),"unrelated art retained "+protectedPaths[i]);
            var report=new Report{editedMeshes=edited,newMeshes=newMeshes,pockets=GrasslandCompositionSetup.Sites().Length,crownVertices=plan.Sum(e=>e.Stamp.Count(s=>s.x==1)),triangles=filters.Sum(f=>f.sharedMesh.triangles.Length/3)};
            Directory.CreateDirectory("Logs/GrasslandComposition");File.WriteAllText("Logs/GrasslandComposition/Bake.json",JsonUtility.ToJson(report,true));
            Debug.Log("Grassland composition bake verification PASSED: topology/UV/trunks/collision, full footprints, shader, unrelated assets, foreign UV protection, repeat/Setup/manual preservation.");
        }
        public static void VerifyAssets()
        {
            foreach(var p in GrasslandCompositionSetup.Sites())Require(GrasslandCompositionSetup.Clear(p),"full reserved shoulder envelope");
            var root=AssetDatabase.LoadAssetAtPath<GameObject>(TerrainSetup.PrefabPath);var terrain=root.GetComponent<IslandTerrain>();var group=terrain.World.Scenery.Find(GrasslandCompositionSetup.Group);
            Require(group!=null&&group.GetComponentsInChildren<Collider>(true).Length==0&&group.GetComponentsInChildren<MonoBehaviour>(true).Length==0&&group.GetComponentsInChildren<Light>(true).Length==0,"baked visual-only group");
            foreach(var f in group.GetComponentsInChildren<MeshFilter>(true))
            {
                Require(GrasslandEcologySetup.IsFoliageMaterial(f.GetComponent<Renderer>().sharedMaterial),"shared AA material");
                var m=f.sharedMesh;Require(m!=null&&m.normals.Length==m.vertexCount&&m.normals.All(a=>a.sqrMagnitude>.9f),"authored normals");
                foreach(var p in m.vertices)Require(StarterIslandLayout.RouteDistance(p.x,p.z)>3&&!StarterIslandLayout.CanPlace(Mathf.RoundToInt(p.x),Mathf.RoundToInt(p.z)),"all model vertices preserve paths/buildable cells");
                var v=m.vertices;var t=m.triangles;
                for(int i=0;i<t.Length;i+=3)Require(Vector3.Cross(v[t[i+1]]-v[t[i]],v[t[i+2]]-v[t[i]]).sqrMagnitude>1e-14f,"non-degenerate triangles");
                Require(!ShaderUtil.ShaderHasError(f.GetComponent<Renderer>().sharedMaterial.shader),"shared shader compiles");
            }
        }
        public static void VerifyRuntime(GameManager g)
        {
            var group=g.Terrain.Terrain.World.Scenery.Find(GrasslandCompositionSetup.Group);Require(group!=null&&group.gameObject.activeInHierarchy,"Packed AA scenery");
            foreach(var f in group.GetComponentsInChildren<MeshFilter>(true))
            {
                var expected=AssetDatabase.LoadAssetAtPath<Mesh>(GrasslandCompositionSetup.Folder+"/"+f.name+".asset");
                Func<Mesh,Vector3[]> points=m=>m.vertices.OrderBy(p=>p.x).ThenBy(p=>p.y).ThenBy(p=>p.z).ToArray();
                Require(points(expected).SequenceEqual(points(f.sharedMesh)),"Packed geometry matches authored mesh");
            }
            var player=g.Players.Player;var camera=player.ViewCamera;var position=player.transform.position;var rotation=camera.transform.rotation;
            var move=player.GetType().GetMethod("TickMovement",BindingFlags.NonPublic|BindingFlags.Instance);
            try
            {
                camera.transform.rotation=Quaternion.Euler(45,0,0);g.Players.RestorePosition(new Vector3(0,(float)StarterIslandLayout.Ground(0,185)+.1f,185));
                foreach(var goal in GrasslandCompositionSetup.Route.Skip(1))
                {
                    int steps=0;while(Vector2.Distance(new Vector2(player.transform.position.x,player.transform.position.z),goal)>.3f&&steps++<3000)
                    {var direction=goal-new Vector2(player.transform.position.x,player.transform.position.z);move.Invoke(player,new object[]{direction.normalized,.04f});}
                    Require(steps<3000,"actual grassland route walking "+goal);
                }
            }
            finally{g.Players.RestorePosition(position);camera.transform.rotation=rotation;}
            Debug.Log("Grassland composition runtime verification PASSED: Packed AA geometry and actual boardwalk-to-lake walking.");
        }
    }
}
