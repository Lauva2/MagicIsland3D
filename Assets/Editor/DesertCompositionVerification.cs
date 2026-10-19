using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography;
using MagicIsland.Interaction;
using MagicIsland.Managers;
using MagicIsland.TerrainSystem;
using UnityEditor;
using UnityEngine;

namespace MagicIsland.EditorTools
{
    public static class DesertCompositionVerification
    {
        static void Check(bool ok,string why)=>DesertCompositionSetup.Require(ok,"verification: "+why);
        static string Hash(string path){using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path)));}
        static Vector4[] UV(Mesh m,int channel){var list=new List<Vector4>();m.GetUVs(channel,list);return list.ToArray();}
        [Serializable] public sealed class Report {public string[] editedMeshes,newMeshes;public int pockets,coloredVertices,triangles;}
        public static void BakeAndVerify()
        {
            Check(Application.dataPath.Replace('\\','/').Contains("/Logs/"),"isolated validation project required");
            var terrain=AssetDatabase.LoadAssetAtPath<GameObject>(TerrainSetup.PrefabPath).GetComponent<IslandTerrain>();
            var plan=DesertCompositionSetup.Plan(terrain);Check(plan.Length>0,"unrefined baseline");
            var edited=plan.Select(e=>AssetDatabase.GetAssetPath(e.Mesh)).ToArray();
            var v=plan.Select(e=>e.Mesh.vertices).ToArray();var n=plan.Select(e=>e.Mesh.normals).ToArray();var colors=plan.Select(e=>e.Mesh.colors).ToArray();
            var t=plan.Select(e=>e.Mesh.triangles).ToArray();var uv=plan.Select(e=>Enumerable.Range(0,8).Select(ch=>UV(e.Mesh,ch)).ToArray()).ToArray();
            var bounds=plan.Select(e=>e.Mesh.bounds).ToArray();
            var paths=Directory.GetFiles("Assets/Art","*",SearchOption.AllDirectories).Select(p=>p.Replace('\\','/'))
                .Where(p=>p!=TerrainSetup.PrefabPath&&!edited.Contains(p)&&!edited.Select(e=>e+".meta").Contains(p)).ToArray();
            var hashes=paths.Select(Hash).ToArray();
            var collision=terrain.GetComponentsInChildren<Collider>(true).Select(c=>c.name+":"+EditorJsonUtility.ToJson(c)).ToArray();
            var importer=AssetImporter.GetAtPath(edited[0]);string original=importer.userData;
            try
            {
                importer.userData="Artist.Custom";bool refused=false;try{DesertCompositionSetup.Plan(terrain);}catch(InvalidOperationException){refused=true;}
                Check(refused,"custom importer metadata rejected before writes");
            }
            finally{importer.userData=original;}
            DesertCompositionSetup.Refine();
            for(int i=0;i<edited.Length;i++)
            {
                var m=AssetDatabase.LoadAssetAtPath<Mesh>(edited[i]);
                Check(v[i].SequenceEqual(m.vertices)&&n[i].SequenceEqual(m.normals)&&t[i].SequenceEqual(m.triangles)&&bounds[i]==m.bounds,"exact original shape/normals/topology/bounds");
                for(int ch=0;ch<8;ch++)Check(uv[i][ch].SequenceEqual(UV(m,ch)),"UV0-7 preserved");
                var actual=m.colors;for(int j=0;j<actual.Length;j++)
                {
                    Check(actual[j].a==colors[i][j].a,"alpha preserved");
                    if(!plan[i].Changed[j])Check(actual[j]==colors[i][j],"unselected original paint/trunks preserved");
                    Check(Mathf.Max(Mathf.Abs(actual[j].r-colors[i][j].r),Mathf.Abs(actual[j].g-colors[i][j].g),Mathf.Abs(actual[j].b-colors[i][j].b))<.18f,"bounded soft palette");
                }
                Check(AssetImporter.GetAtPath(edited[i]).userData==DesertCompositionSetup.Marker,"one-time importer marker");
            }
            terrain=AssetDatabase.LoadAssetAtPath<GameObject>(TerrainSetup.PrefabPath).GetComponent<IslandTerrain>();
            Check(collision.SequenceEqual(terrain.GetComponentsInChildren<Collider>(true).Select(c=>c.name+":"+EditorJsonUtility.ToJson(c))),"entire original collider set");
            int pockets=VerifyAssets();
            var filters=terrain.World.Scenery.Find(DesertCompositionSetup.Group).GetComponentsInChildren<MeshFilter>(true);
            var newMeshes=filters.Select(f=>AssetDatabase.GetAssetPath(f.sharedMesh)).ToArray();
            var watched=edited.Concat(edited.Select(p=>p+".meta")).Concat(newMeshes).Concat(newMeshes.Select(p=>p+".meta")).Concat(new[]{TerrainSetup.PrefabPath}).ToArray();
            var saved=watched.Select(File.ReadAllBytes).ToArray();
            DesertCompositionSetup.Refine();PrototypeAddressablesSetup.EnsureSetup();
            for(int i=0;i<watched.Length;i++)Check(saved[i].SequenceEqual(File.ReadAllBytes(watched[i])),"repeat and ordinary Setup preserve resources "+watched[i]);
            try
            {
                foreach(string path in new[]{edited[0],newMeshes[0]})
                {var m=AssetDatabase.LoadAssetAtPath<Mesh>(path);var c=m.colors;c[0]=Color.magenta;m.colors=c;var shape=m.vertices;shape[0]+=Vector3.up*.01f;m.vertices=shape;EditorUtility.SetDirty(m);}
                AssetDatabase.SaveAssets();var manual=watched.Select(File.ReadAllBytes).ToArray();DesertCompositionSetup.Refine();PrototypeAddressablesSetup.EnsureSetup();
                for(int i=0;i<watched.Length;i++)Check(manual[i].SequenceEqual(File.ReadAllBytes(watched[i])),"later manual shape/color retained");
            }
            finally
            {
                for(int i=0;i<watched.Length;i++)File.WriteAllBytes(watched[i],saved[i]);
                foreach(string path in watched.Where(p=>!p.EndsWith(".meta",StringComparison.Ordinal)))AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            }
            for(int i=0;i<paths.Length;i++)Check(hashes[i]==Hash(paths[i]),"unrelated art unchanged "+paths[i]);
            var report=new Report{editedMeshes=edited,newMeshes=newMeshes,pockets=pockets,coloredVertices=plan.Sum(e=>e.Changed.Count(b=>b)),triangles=filters.Sum(f=>f.sharedMesh.triangles.Length/3)};
            Directory.CreateDirectory("Logs/DesertComposition");File.WriteAllText("Logs/DesertComposition/Bake.json",JsonUtility.ToJson(report,true));
            Debug.Log("Desert composition bake verification PASSED: full shoulder footprints, original collision/geometry/UV0-7, metadata guards, repeat/Setup/manual-edit preservation and unrelated art hashes.");
        }
        public static int VerifyAssets()
        {
            var root=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);
            try
            {
                var terrain=root.GetComponent<IslandTerrain>();var group=terrain.World.Scenery.Find(DesertCompositionSetup.Group);
                Check(group!=null&&group.GetComponentsInChildren<Collider>(true).Length==0&&group.GetComponentsInChildren<MonoBehaviour>(true).Length==0&&group.GetComponentsInChildren<Light>(true).Length==0,"visual-only baked batches");
                var sites=DesertCompositionSetup.Sites(terrain);foreach(var site in sites)Check(DesertCompositionSetup.Clear(site.Point),"whole reserved non-buildable shoulder");
                foreach(var f in group.GetComponentsInChildren<MeshFilter>(true))
                {
                    var m=f.sharedMesh;Check(f.gameObject.layer==2&&f.GetComponent<Renderer>().sharedMaterial==AssetDatabase.LoadAssetAtPath<Material>(BeachCompositionSetup.MaterialPath),"shared AA material and noninteractive layer");
                    Check(m.normals.Length==m.vertexCount&&m.normals.All(q=>q.sqrMagnitude>.9f)&&m.colors.Length==m.vertexCount,"finite unit normals and colors");
                    foreach(var p in m.vertices)
                    {
                        Check(!float.IsNaN(p.x)&&!float.IsNaN(p.y)&&!float.IsNaN(p.z)&&!float.IsInfinity(p.y),"finite model geometry");
                        Check(StarterIslandLayout.RouteDistance(p.x,p.z)>3&&!StarterIslandLayout.CanPlace(Mathf.RoundToInt(p.x),Mathf.RoundToInt(p.z)),"all vertices outside road and construction cells");
                        Check(sites.Any(s=>Vector2.Distance(s.Point,new Vector2(p.x,p.z))<=DesertCompositionSetup.Radius+.001f),"all vertices inside safe pocket");
                    }
                    var v=m.vertices;var t=m.triangles;for(int i=0;i<t.Length;i+=3)Check(Vector3.Cross(v[t[i+1]]-v[t[i]],v[t[i+2]]-v[t[i]]).sqrMagnitude>1e-14f,"nondegenerate triangles");
                    Check(!ShaderUtil.ShaderHasError(f.GetComponent<Renderer>().sharedMaterial.shader),"original shader compiles");
                }
                return sites.Length;
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        public static void VerifyRuntime(GameManager g)
        {
            var group=g.Terrain.Terrain.World.Scenery.Find(DesertCompositionSetup.Group);Check(group!=null&&group.gameObject.activeInHierarchy,"actual Packed scenery");
            foreach(var f in group.GetComponentsInChildren<MeshFilter>(true))
            {
                var expected=AssetDatabase.LoadAssetAtPath<Mesh>(StarterIslandWorldSetup.Folder+"/"+f.name+".asset");
                Func<Mesh,Vector3[]> points=m=>m.vertices.OrderBy(p=>p.x).ThenBy(p=>p.y).ThenBy(p=>p.z).ToArray();
                Check(expected.vertexCount==f.sharedMesh.vertexCount&&expected.triangles.Length==f.sharedMesh.triangles.Length&&points(expected).SequenceEqual(points(f.sharedMesh)),"Packed geometry matches authored mesh");
                Func<Mesh,Color32[]> paint=m=>m.colors32.OrderBy(c=>c.r).ThenBy(c=>c.g).ThenBy(c=>c.b).ToArray();
                Check(paint(expected).SequenceEqual(paint(f.sharedMesh)),"Packed vertex palette matches authored mesh");
            }
            var player=g.Players.Player;var camera=player.ViewCamera;var position=player.transform.position;var rotation=camera.transform.rotation;
            var move=player.GetType().GetMethod("TickMovement",BindingFlags.NonPublic|BindingFlags.Instance);
            try
            {
                camera.transform.rotation=Quaternion.Euler(45,0,0);Check(g.Terrain.TryResolve(new Vector3(-180,0,400),out var start),"actual entry ground");g.Players.RestorePosition(start);
                foreach(var goal in DesertCompositionSetup.Route.Skip(1))
                {
                    int steps=0;while(Vector2.Distance(new Vector2(player.transform.position.x,player.transform.position.z),goal)>.6f&&steps++<3000)
                    {var direction=goal-new Vector2(player.transform.position.x,player.transform.position.z);move.Invoke(player,new object[]{direction.normalized,.04f});}
                    Check(steps<3000,"actual CharacterController follows desert route "+goal);
                }
            }
            finally{g.Players.RestorePosition(position);camera.transform.rotation=rotation;}
            Debug.Log("Desert composition Packed verification PASSED: authored geometry/color and real entry-to-interior route walking.");
        }
    }
}
