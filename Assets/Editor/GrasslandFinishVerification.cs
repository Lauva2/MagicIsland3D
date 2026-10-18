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
    public static class GrasslandFinishVerification
    {
        static void Require(bool ok,string why){if(!ok)throw new InvalidOperationException("Grassland finish verification: "+why);}
        static Vector2[] UV(Mesh m,int channel){var uv=new List<Vector2>();m.GetUVs(channel,uv);return uv.ToArray();}
        [Serializable] public sealed class Report{public string[] editedMeshes,newMeshes;public int groundMeshes,crownMeshes,vergeMeshes,groundVertices,crownVertices,removedTriangles,sites,batches;}
        public static void BakeAndVerify()
        {
            Require(Application.dataPath.Replace('\\','/').Contains("/Logs/"),"isolated project required");
            var root=AssetDatabase.LoadAssetAtPath<GameObject>(TerrainSetup.PrefabPath);var terrain=root.GetComponent<IslandTerrain>();var plan=GrasslandFinishSetup.Plan(terrain);Require(plan.Length>0,"unrefined baseline");
            var paths=plan.Select(e=>AssetDatabase.GetAssetPath(e.Mesh)).ToArray();var v=plan.Select(e=>e.Mesh.vertices).ToArray();var n=plan.Select(e=>e.Mesh.normals).ToArray();var c=plan.Select(e=>e.Mesh.colors).ToArray();
            var tri=plan.Select(e=>e.Mesh.triangles).ToArray();var bounds=plan.Select(e=>e.Mesh.bounds).ToArray();var uv=plan.Select(e=>Enumerable.Range(0,7).Select(ch=>UV(e.Mesh,ch)).ToArray()).ToArray();
            var protectedPaths=Directory.GetFiles("Assets/Art","*",SearchOption.AllDirectories).Select(p=>p.Replace('\\','/')).Where(p=>p!=TerrainSetup.PrefabPath&&!paths.Contains(p)).ToArray();var protectedBytes=protectedPaths.Select(File.ReadAllBytes).ToArray();
            var colliders=terrain.GetComponentsInChildren<Collider>(true).Select(a=>a.name+":"+EditorJsonUtility.ToJson(a)).ToArray();
            var physics=terrain.GetComponentsInChildren<MeshCollider>(true).Select(a=>a.sharedMesh).Distinct().ToArray();var pv=physics.Select(a=>a.vertices).ToArray();var pt=physics.Select(a=>a.triangles).ToArray();var pb=physics.Select(a=>a.bounds).ToArray();
            try
            {
                plan[0].Mesh.SetUVs(7,Enumerable.Repeat(Vector2.one,plan[0].Mesh.vertexCount).ToArray());bool rejected=false;
                try{GrasslandFinishSetup.Plan(terrain);}catch(InvalidOperationException){rejected=true;}Require(rejected,"foreign UV7 protected before writes");
            }
            finally{plan[0].Mesh.SetUVs(7,Array.Empty<Vector2>());}
            GrasslandFinishSetup.Rebuild();int ground=0,crown=0,removed=0;
            for(int i=0;i<plan.Length;i++)
            {
                var e=plan[i];var m=AssetDatabase.LoadAssetAtPath<Mesh>(paths[i]);var a=m.vertices;var b=m.normals;var paint=m.colors;var stamp=UV(m,7);
                for(int ch=0;ch<7;ch++)Require(uv[i][ch].SequenceEqual(UV(m,ch)),"original UV "+ch+" on "+paths[i]);
                if(e.Ground||e.Verge)Require(v[i].SequenceEqual(a)&&n[i].SequenceEqual(b)&&bounds[i]==m.bounds,"exact ground/verge geometry");
                if(e.Verge)
                {
                    Require(c[i].SequenceEqual(paint),"verge paint retained");var expected=new List<int>();
                    for(int j=0;j<tri[i].Length;j+=3)if(stamp[tri[i][j]].x==0)expected.AddRange(new[]{tri[i][j],tri[i][j+1],tri[i][j+2]});
                    Require(expected.SequenceEqual(m.triangles),"only selected whole components removed, original index order");removed+=(tri[i].Length-m.triangles.Length)/3;
                    // A retained bloom must retain its supporting stem and all five petals, despite disconnected topology.
                    for(int j=0;j+112<=c[i].Length;j++)
                    {
                        if(!Enumerable.Range(j,22).All(k=>c[i][k]==new Color(.39f,.57f,.30f))||c[i][j+42]!=new Color(.95f,.75f,.34f))continue;
                        Require(Enumerable.Range(j,112).All(k=>stamp[k].x==stamp[j].x),"whole flower retains/removes stem, five petals and pollen together");j+=111;
                    }
                }
                else Require(tri[i].SequenceEqual(m.triangles),"exact original triangle topology");
                for(int j=0;j<a.Length;j++)
                {
                    Require(paint[j].a==c[i][j].a,"authored alpha");
                    if(!e.Changed[j])Require(a[j]==v[i][j]&&b[j]==n[i][j]&&paint[j]==c[i][j],"unselected scenery/trunks preserved");
                    else
                    {
                        Require(GrasslandFinishSetup.Mask(v[i][j].x,v[i][j].z)>0,"bounded grassland edit");
                        if(e.Ground)ground++;else if(!e.Verge){crown++;Require(c[i][j].g-c[i][j].r>.065f&&Vector3.Distance(a[j],v[i][j])<2,"bounded leaf deformation");}
                    }
                    if(uv[i][6].Length==a.Length&&uv[i][6][j].y==-742&&uv[i][6][j].x==1)
                        Require(a[j]==v[i][j]&&b[j]==n[i][j]&&paint[j]==c[i][j],"previous route crown hand edits retained");
                }
            }
            for(int i=0;i<physics.Length;i++)Require(pv[i].SequenceEqual(physics[i].vertices)&&pt[i].SequenceEqual(physics[i].triangles)&&pb[i]==physics[i].bounds,"all physical mesh geometry exact");
            root=AssetDatabase.LoadAssetAtPath<GameObject>(TerrainSetup.PrefabPath);terrain=root.GetComponent<IslandTerrain>();
            Require(colliders.SequenceEqual(terrain.GetComponentsInChildren<Collider>(true).Select(a=>a.name+":"+EditorJsonUtility.ToJson(a))),"complete original collision set");
            VerifyAssets();var filters=terrain.World.Scenery.Find(GrasslandFinishSetup.Group).GetComponentsInChildren<MeshFilter>(true);var newMeshes=filters.Select(f=>AssetDatabase.GetAssetPath(f.sharedMesh)).ToArray();
            var watched=paths.Concat(newMeshes).Concat(new[]{TerrainSetup.PrefabPath}).ToArray();var bytes=watched.Select(File.ReadAllBytes).ToArray();
            GrasslandFinishSetup.Rebuild();PrototypeAddressablesSetup.EnsureSetup();
            for(int i=0;i<watched.Length;i++)Require(bytes[i].SequenceEqual(File.ReadAllBytes(watched[i])),"repeat/Setup preserve finish");
            try
            {
                foreach(string path in new[]{paths.First(p=>p.Contains("/Landscape ")),newMeshes[0]})
                {
                    var m=AssetDatabase.LoadAssetAtPath<Mesh>(path);var manual=m.colors;manual[0]=Color.magenta;m.colors=manual;var shape=m.vertices;shape[0]+=Vector3.up*.01f;m.vertices=shape;EditorUtility.SetDirty(m);
                }
                AssetDatabase.SaveAssets();var manualBytes=watched.Select(File.ReadAllBytes).ToArray();GrasslandFinishSetup.Rebuild();PrototypeAddressablesSetup.EnsureSetup();
                for(int i=0;i<watched.Length;i++)Require(manualBytes[i].SequenceEqual(File.ReadAllBytes(watched[i])),"manual paint and shape retained");
            }
            finally{for(int i=0;i<watched.Length;i++){File.WriteAllBytes(watched[i],bytes[i]);AssetDatabase.ImportAsset(watched[i],ImportAssetOptions.ForceSynchronousImport);}}
            for(int i=0;i<protectedPaths.Length;i++)Require(protectedBytes[i].SequenceEqual(File.ReadAllBytes(protectedPaths[i])),"unrelated art retained "+protectedPaths[i]);
            Require(ground>100&&crown>100&&removed>100,"ground, canopy and verge completion all represented");
            var report=new Report{editedMeshes=paths,newMeshes=newMeshes,groundMeshes=plan.Count(e=>e.Ground),crownMeshes=plan.Count(e=>!e.Ground&&!e.Verge),vergeMeshes=plan.Count(e=>e.Verge),groundVertices=ground,crownVertices=crown,removedTriangles=removed,sites=GrasslandFinishSetup.Sites().Length,batches=filters.Length};
            Directory.CreateDirectory("Logs/GrasslandFinish");File.WriteAllText("Logs/GrasslandFinish/Bake.json",JsonUtility.ToJson(report,true));
            Debug.Log("Grassland finish bake verification PASSED: original physics/topology/UV/trunks, protected prior art, complete footprints, whole-component thinning, repeat/Setup/manual retention.");
        }
        public static void VerifyAssets()
        {
            foreach(var p in GrasslandFinishSetup.Sites())Require(GrasslandFinishSetup.Clear(p.Point),"complete reserved shoulder footprint");
            var terrain=AssetDatabase.LoadAssetAtPath<GameObject>(TerrainSetup.PrefabPath).GetComponent<IslandTerrain>();var group=terrain.World.Scenery.Find(GrasslandFinishSetup.Group);
            Require(group!=null&&group.GetComponentsInChildren<Collider>(true).Length==0&&group.GetComponentsInChildren<MonoBehaviour>(true).Length==0&&group.GetComponentsInChildren<Light>(true).Length==0,"baked visual-only group");
            foreach(var f in group.GetComponentsInChildren<MeshFilter>(true))
            {
                var m=f.sharedMesh;Require(m!=null&&m.normals.Length==m.vertexCount&&m.normals.All(a=>a.sqrMagnitude>.9f),"authored normals");
                Require(GrasslandEcologySetup.IsFoliageMaterial(f.GetComponent<Renderer>().sharedMaterial),"shared AA material");
                foreach(var p in m.vertices)Require(StarterIslandLayout.RouteDistance(p.x,p.z)>3&&!StarterIslandLayout.CanPlace(Mathf.RoundToInt(p.x),Mathf.RoundToInt(p.z)),"all vertices preserve paths/construction cells");
                var v=m.vertices;var t=m.triangles;for(int i=0;i<t.Length;i+=3)Require(Vector3.Cross(v[t[i+1]]-v[t[i]],v[t[i+2]]-v[t[i]]).sqrMagnitude>1e-14f,"valid triangle area");
            }
        }
        public static void VerifyRuntime(GameManager g)
        {
            GrasslandCompositionVerification.VerifyRuntime(g);
            var group=g.Terrain.Terrain.World.Scenery.Find(GrasslandFinishSetup.Group);Require(group!=null&&group.gameObject.activeInHierarchy,"Packed finish loaded");
            foreach(var f in group.GetComponentsInChildren<MeshFilter>(true))
            {
                var expected=AssetDatabase.LoadAssetAtPath<Mesh>(GrasslandCompositionSetup.Folder+"/"+f.name+".asset");Func<Mesh,Vector3[]> points=m=>m.vertices.OrderBy(p=>p.x).ThenBy(p=>p.y).ThenBy(p=>p.z).ToArray();Require(points(expected).SequenceEqual(points(f.sharedMesh)),"Packed source geometry");
            }
            foreach(var f in g.Terrain.Terrain.World.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.name.StartsWith("Land ",StringComparison.Ordinal)||f.name.StartsWith("Landscape ",StringComparison.Ordinal)||f.name.StartsWith("Route verge ",StringComparison.Ordinal)))
            {
                var expected=AssetDatabase.LoadAssetAtPath<Mesh>(GrasslandCompositionSetup.Folder+"/"+f.name+".asset");if(expected==null||UV(expected,7).Length==0)continue;
                Require(expected.vertexCount==f.sharedMesh.vertexCount&&expected.triangles.Length==f.sharedMesh.triangles.Length,"Packed original mesh topology/count");
                Vector3 Average(Mesh m)=>m.colors.Aggregate(Vector3.zero,(sum,p)=>sum+new Vector3(p.r,p.g,p.b))/m.vertexCount;
                Require(Vector3.Distance(Average(expected),Average(f.sharedMesh))<.00001f,"Packed ground/crown/verge palette");
            }
            var player=g.Players.Player;var camera=player.ViewCamera;var position=player.transform.position;var rotation=camera.transform.rotation;var move=player.GetType().GetMethod("TickMovement",BindingFlags.NonPublic|BindingFlags.Instance);
            try
            {
                camera.transform.rotation=Quaternion.Euler(45,0,0);Require(g.Terrain.TryResolve(new Vector3(0,0,240),out var start),"south branch support");g.Players.RestorePosition(start);
                foreach(var goal in new[]{new Vector2(150,290),new Vector2(300,340),new Vector2(340,358.4f)})
                {
                    int steps=0;while(Vector2.Distance(new Vector2(player.transform.position.x,player.transform.position.z),goal)>.35f&&steps++<2500)
                    {var direction=goal-new Vector2(player.transform.position.x,player.transform.position.z);move.Invoke(player,new object[]{direction.normalized,.04f});}Require(steps<2500,"actual east meadow route "+goal);
                }
            }
            finally{g.Players.RestorePosition(position);camera.transform.rotation=rotation;}
            Debug.Log("Grassland finish runtime verification PASSED: Packed ground/crowns/verges and native batches, actual western and eastern routes.");
        }
    }
}
