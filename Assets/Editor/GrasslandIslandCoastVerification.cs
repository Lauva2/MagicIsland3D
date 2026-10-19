using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using MagicIsland.Interaction;
using MagicIsland.Managers;
using MagicIsland.TerrainSystem;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    public static class GrasslandIslandCoastVerification
    {
        const string BeforeKey="MagicIsland.GrasslandCoast.Before";
        static void Check(bool b,string why){if(!b)throw new InvalidOperationException("Grassland coast verification: "+why);}
        static string Hash(string path){using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path)));}
        public static void Before(){SessionState.SetBool(BeforeKey,true);BiomeIslandsVerification.Run();}
        public static void After(){SessionState.SetBool(BeforeKey,false);VerifyAssets();BiomeIslandsVerification.Run();}
        public static void BakeAndRun(){BakeAndVerify();After();}
        [Serializable] public sealed class Report {public string[] editedMeshes,newFiles;public int retainedConstructionTriangles,changedVertices,newGrounds;}
        public static void BakeAndVerify()
        {
            Check(Application.dataPath.Replace('\\','/').Contains("/Logs/"),"isolated validation project required");
            var terrain=AssetDatabase.LoadAssetAtPath<GameObject>(TerrainSetup.PrefabPath).GetComponent<IslandTerrain>();
            var plan=GrasslandIslandCoastSetup.Plan(terrain);Check(plan.Length>0,"unrefined baseline");
            var edits=plan.Where(p=>p.Source!=null).ToArray();var paths=edits.Select(p=>p.Path).ToArray();
            var oldV=edits.Select(p=>p.Source.vertices).ToArray();var oldT=edits.Select(p=>p.Source.triangles).ToArray();
            var oldC=edits.Select(p=>p.Source.colors).ToArray();var oldUv=edits.Select(p=>Enumerable.Range(0,8).Select(ch=>UV(p.Source,ch)).ToArray()).ToArray();
            var guids=paths.Select(AssetDatabase.AssetPathToGUID).ToArray();int oldGrounds=terrain.World.Grounds.Length;
            foreach(var p in plan)Object.DestroyImmediate(p.Result);
            var all=Directory.GetFiles("Assets/Art","*",SearchOption.AllDirectories).Select(p=>p.Replace('\\','/')).ToArray();
            var exceptions=new HashSet<string>(paths.Concat(paths.Select(p=>p+".meta")).Concat(new[]{TerrainSetup.PrefabPath}));
            var protectedPaths=all.Where(p=>!exceptions.Contains(p)).ToArray();var hashes=protectedPaths.Select(Hash).ToArray();
            var importer=AssetImporter.GetAtPath(paths[0]);string metadata=importer.userData;
            try
            {
                importer.userData="Artist.Custom";bool refused=false;
                try{var unexpected=GrasslandIslandCoastSetup.Plan(terrain);foreach(var p in unexpected)Object.DestroyImmediate(p.Result);}catch(InvalidOperationException){refused=true;}
                Check(refused,"foreign artist metadata rejected before writes");
            }
            finally{importer.userData=metadata;}
            GrasslandIslandCoastSetup.Rebuild();int changed=0,retained=0;
            for(int p=0;p<paths.Length;p++)
            {
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(paths[p]);var v=mesh.vertices;var colors=mesh.colors;
                Check(AssetDatabase.AssetPathToGUID(paths[p])==guids[p],"original mesh GUID retained");
                Check(v.Length==oldV[p].Length,"original grid vertex count retained");
                for(int ch=1;ch<8;ch++)Check(oldUv[p][ch].SequenceEqual(UV(mesh,ch)),"UV1-7 retained");
                var uv=UV(mesh,0);
                for(int i=0;i<v.Length;i++)
                {
                    Check(v[i].x==oldV[p][i].x&&v[i].z==oldV[p][i].z,"original grid XZ retained");
                    double d=BiomeIslandLayout.LegacyDistance(IslandBiome.Grassland,v[i].x,v[i].z);
                    if(d>=-3)Check(v[i]==oldV[p][i],"all old dry terrain and wet margin retained");
                    if(d>=68)Check(colors[i]==oldC[p][i]&&uv[i]==oldUv[p][0][i],"interior hand paint retained");
                    if(v[i]!=oldV[p][i])changed++;
                }
                var triangles=mesh.triangles;var faces=new HashSet<string>();
                for(int i=0;i<triangles.Length;i+=3)faces.Add(triangles[i]+","+triangles[i+1]+","+triangles[i+2]);
                for(int i=0;i<oldT[p].Length;i+=3)
                {
                    int a=oldT[p][i],b=oldT[p][i+1],c=oldT[p][i+2];Check(faces.Contains(a+","+b+","+c),"all original faces retained");
                    var box=new Bounds(oldV[p][a],Vector3.zero);box.Encapsulate(oldV[p][b]);box.Encapsulate(oldV[p][c]);
                    bool used=false;
                    for(int x=Mathf.CeilToInt(box.min.x-.5f);x<=Mathf.FloorToInt(box.max.x+.5f)&&!used;x++)
                        for(int z=Mathf.CeilToInt(box.min.z-.5f);z<=Mathf.FloorToInt(box.max.z+.5f)&&!used;z++)
                            used=BiomeIslandLayout.LegacyDistance(IslandBiome.Grassland,x,z)>=5&&BiomeIslandLayout.CanPlace(x,z);
                    if(used){retained++;Check(v[a]==oldV[p][a]&&v[b]==oldV[p][b]&&v[c]==oldV[p][c],"complete old construction support triangle retained");}
                }
            }
            for(int i=0;i<protectedPaths.Length;i++)Check(Hash(protectedPaths[i])==hashes[i],"protected art unchanged "+protectedPaths[i]);
            VerifyAssets();
            var manual=AssetDatabase.LoadAssetAtPath<Mesh>(paths[0]);byte[] saved=File.ReadAllBytes(paths[0]);
            try
            {
                var paint=manual.colors;paint[0].r*=.97f;manual.colors=paint;EditorUtility.SetDirty(manual);AssetDatabase.SaveAssets();
                var checkPaths=Directory.GetFiles("Assets/Art","*",SearchOption.AllDirectories).Select(s=>s.Replace('\\','/')).ToArray();var checkHashes=checkPaths.Select(Hash).ToArray();
                GrasslandIslandCoastSetup.Rebuild();BiomeIslandsSetup.Rebuild();PrototypeAddressablesSetup.EnsureSetup();
                for(int i=0;i<checkPaths.Length;i++)Check(Hash(checkPaths[i])==checkHashes[i],"repeat / ordinary Setup retain manual edits "+checkPaths[i]);
            }
            finally{File.WriteAllBytes(paths[0],saved);AssetDatabase.ImportAsset(paths[0],ImportAssetOptions.ForceUpdate);}
            terrain=AssetDatabase.LoadAssetAtPath<GameObject>(TerrainSetup.PrefabPath).GetComponent<IslandTerrain>();
            var report=new Report{editedMeshes=paths,newFiles=Directory.GetFiles("Assets/Art","*",SearchOption.AllDirectories).Select(p=>p.Replace('\\','/')).Except(all).ToArray(),changedVertices=changed,retainedConstructionTriangles=retained,newGrounds=terrain.World.Grounds.Length-oldGrounds};
            Directory.CreateDirectory("Logs/GrasslandCoast");File.WriteAllText("Logs/GrasslandCoast/Bake.json",JsonUtility.ToJson(report,true));
            Debug.Log("Grassland coast bake verification PASSED: "+changed+" sea-side vertices, "+retained+" complete construction support faces retained; other islands/art/GUIDs, foreign metadata refusal and manual/repeat Setup verified.");
        }
        static Vector4[] UV(Mesh mesh,int channel){var values=new List<Vector4>();mesh.GetUVs(channel,values);return values.ToArray();}
        public static void VerifyAssets()
        {
            var root=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);
            try
            {
                var terrain=root.GetComponent<IslandTerrain>();Check(terrain.IsValid&&terrain.World.GrasslandCoastArtVersion==1,"coast art references");Physics.SyncTransforms();
                foreach(double angle in new[]{-.23,.74,2.68,-2.63})
                {
                    var p=ShorePoint(angle,8);Check(terrain.TryGround(p,out var supported)&&supported.y>-.20f,"dry headland real collision "+angle);
                }
                foreach(var c in terrain.World.transform.Find("Grassland island").GetComponentsInChildren<MeshCollider>(true))
                    Check(c.sharedMesh==c.GetComponent<MeshFilter>().sharedMesh&&c.sharedMesh.vertexCount==289,"visible and physical grid agree");
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        public static void Visit(GameManager g)
        {
            bool before=SessionState.GetBool(BeforeKey,false);if(!before&&g.Terrain.Terrain.World.GrasslandCoastArtVersion!=1)return;
            string folder="Logs/GrasslandCoast/"+(before?"Before":"After");Directory.CreateDirectory(folder);
            var camera=g.Players.Player.ViewCamera;var pos=camera.transform.position;var rot=camera.transform.rotation;float size=camera.orthographicSize,far=camera.farClipPlane;
            try
            {
                g.Weather.Tick(720,true);g.Ocean.Surface.Tick(0,1,false,g.Weather.Sun);camera.farClipPlane=3200;
                Frame(camera,new Vector3(-20,0,480),70,350);Capture(camera,folder+"/Overview.png");
                Frame(camera,new Vector3(220,0,410),55,40);Capture(camera,folder+"/EastCape.png");
                Frame(camera,new Vector3(-218,0,604),55,42);Capture(camera,folder+"/WestCape.png");
                Frame(camera,new Vector3(194,0,554),55,43);Capture(camera,folder+"/EastBay.png");
                Frame(camera,new Vector3(-40,0,175),55,45);Capture(camera,folder+"/SouthBeach.png");
            }
            finally{camera.transform.SetPositionAndRotation(pos,rot);camera.orthographicSize=size;camera.farClipPlane=far;}
            if(before)return;
            var original=g.Players.Player.transform.position;var method=g.Players.Player.GetType().GetMethod("TickMovement",BindingFlags.Instance|BindingFlags.NonPublic);
            try
            {
                camera.transform.rotation=Quaternion.Euler(45,0,0);
                foreach(double angle in new[]{-.23,2.68})
                {
                    var requested=ShorePoint(angle,8);Check(g.Terrain.TryResolve(requested,out var point),"new cape supported");
                    var c=g.Players.Player.GetComponent<CharacterController>();var center=point+c.center;float radius=c.radius+c.skinWidth,half=c.height*.5f-radius;
                    Check(Physics.OverlapCapsule(center-Vector3.up*half,center+Vector3.up*half,radius,~0,QueryTriggerInteraction.Ignore).All(hit=>g.Terrain.Terrain.IsGround(hit)||hit.transform.IsChildOf(c.transform)),"complete capsule clear on cape");
                    g.Players.RestorePosition(point);var direction=new Vector2(-(float)Math.Cos(angle)*215,-(float)Math.Sin(angle)*290).normalized;
                    for(int i=0;i<75;i++)method.Invoke(g.Players.Player,new object[]{direction,.02f});
                    var after=g.Players.Player.transform.position;Check(Vector3.ProjectOnPlane(after-point,Vector3.up).magnitude>4,"actual character walks new cape");
                    Check(g.Terrain.Contains(after,radius)&&g.Terrain.TryResolve(after,out var floor)&&Mathf.Abs(after.y-floor.y)<.25f,"walking remains on dry real ground");
                    Check(!g.Terrain.TryResolve(ShorePoint(angle,-8),out _),"water remains unwalkable");
                }
            }
            finally{g.Players.RestorePosition(original);camera.transform.SetPositionAndRotation(pos,rot);Physics.SyncTransforms();}
            Check(!File.Exists(g.Saves.SavePath),"coast checks do not save");
            Debug.Log("Grassland coast Packed runtime PASSED: real cape support, full capsule, WASD movement, sea exclusion and unchanged save files.");
        }
        static Vector3 ShorePoint(double angle,double inset)
        {
            double lo=.7,hi=1.8;
            for(int i=0;i<40;i++){double r=(lo+hi)*.5;if(GrasslandIslandCoast.Distance(-20+Math.Cos(angle)*215*r,470+Math.Sin(angle)*290*r)>inset)lo=r;else hi=r;}
            double t=(lo+hi)*.5;return new Vector3((float)(-20+Math.Cos(angle)*215*t),0,(float)(470+Math.Sin(angle)*290*t));
        }
        static void Frame(Camera camera,Vector3 focus,float pitch,float size)
        {camera.transform.rotation=Quaternion.Euler(pitch,0,0);camera.transform.position=focus-camera.transform.forward*1600;camera.orthographicSize=size;}
        static void Capture(Camera camera,string path)
        {
            var rt=new RenderTexture(1280,800,24);var old=camera.targetTexture;var active=RenderTexture.active;var image=new Texture2D(1280,800,TextureFormat.RGB24,false);
            // Warm the same target after moving an orthographic camera; water must sample
            // this view's completed depth texture rather than the preceding capture's view.
            try{camera.targetTexture=rt;camera.Render();camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1280,800),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());}
            finally{camera.targetTexture=old;RenderTexture.active=active;Object.DestroyImmediate(rt);Object.DestroyImmediate(image);}
        }
    }
}
