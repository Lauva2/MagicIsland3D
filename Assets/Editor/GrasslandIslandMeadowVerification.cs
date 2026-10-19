using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Security.Cryptography;
using MagicIsland.Interaction;
using MagicIsland.Managers;
using MagicIsland.Ocean;
using MagicIsland.TerrainSystem;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    public static class GrasslandIslandMeadowVerification
    {
        const string Key="MagicIsland.GrasslandIslandMeadow.Before";
        static void Check(bool ok,string why){if(!ok)throw new InvalidOperationException("Island meadow verification: "+why);}
        public static void Before(){SessionState.SetBool(Key,true);BiomeIslandsVerification.Run();}
        public static void After(){SessionState.SetBool(Key,false);VerifyAssets();BiomeIslandsVerification.Run();}
        // Final fitting pass is restricted to disposable validation output, never artists' assets.
        public static void FitTrailAndRun()
        {
            Check(Application.dataPath.Replace('\\','/').Contains("/Logs/"),"isolated generated output required");
            string path=GrasslandIslandMeadowSetup.Folder+"/Island meadow trail.asset";Check(AssetImporter.GetAtPath(path).userData==GrasslandIslandMeadowSetup.Marker,"known generated trail");
            var bytes=File.ReadAllBytes(path);var root=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);Mesh fresh=null;
            try
            {
                var terrain=root.GetComponent<IslandTerrain>();Physics.SyncTransforms();fresh=GrasslandIslandMeadowSetup.Trail(new GrasslandRevitalizationSetup.AuthoringWorld(terrain,new Vector4(-300,200,600,650)));
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);EditorUtility.CopySerialized(fresh,mesh);EditorUtility.SetDirty(mesh);AssetDatabase.SaveAssets();
                File.WriteAllText(path,string.Join("\n",File.ReadAllLines(path).Select(l=>l.TrimEnd()))+"\n");
            }
            catch{File.WriteAllBytes(path,bytes);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);throw;}
            finally{if(fresh!=null)Object.DestroyImmediate(fresh);PrefabUtility.UnloadPrefabContents(root);}
            Debug.Log("Island meadow terrain-aligned trail fitting PASSED; original terrain/collisions and generated trail GUID retained.");After();
        }
        static string Hash(string path){using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path)));}
        public static void BakeAndVerify()
        {
            Check(Application.dataPath.Replace('\\','/').Contains("/Logs/"),"isolated validation project");
            var terrain=AssetDatabase.LoadAssetAtPath<GameObject>(TerrainSetup.PrefabPath).GetComponent<IslandTerrain>();
            var roads=GrasslandIslandMeadowSetup.RoadPlan(terrain);var paths=roads.Select(r=>AssetDatabase.GetAssetPath(r.Mesh)).ToArray();
            var vertex=roads.Select(r=>r.Mesh.vertices).ToArray();var normals=roads.Select(r=>r.Mesh.normals).ToArray();var colors=roads.Select(r=>r.Mesh.colors).ToArray();var bounds=roads.Select(r=>r.Mesh.bounds).ToArray();
            var uv=roads.Select(r=>Enumerable.Range(0,8).Select(ch=>UV(r.Mesh,ch)).ToArray()).ToArray();var guids=paths.Select(AssetDatabase.AssetPathToGUID).ToArray();
            var originalFaces=roads.Select(r=>Enumerable.Range(0,r.Mesh.subMeshCount).Select(s=>r.Mesh.GetTriangles(s)).ToArray()).ToArray();
            var exceptions=new HashSet<string>(paths.SelectMany(p=>new[]{p,p+".meta"}).Concat(new[]{TerrainSetup.PrefabPath}));
            var all=Directory.GetFiles("Assets/Art","*",SearchOption.AllDirectories).Select(p=>p.Replace('\\','/')).ToArray();var protectedPaths=all.Where(p=>!exceptions.Contains(p)).ToArray();var hashes=protectedPaths.Select(Hash).ToArray();
            var importer=AssetImporter.GetAtPath(paths[0]);string metadata=importer.userData;
            try
            {
                importer.userData="Artist.Custom";bool refused=false;try{GrasslandIslandMeadowSetup.RoadPlan(terrain);}catch(InvalidOperationException){refused=true;}
                Check(refused,"foreign road metadata rejected before writes");
            }
            finally{importer.userData=metadata;}
            GrasslandIslandMeadowSetup.Rebuild();
            Debug.Log("Island meadow bake audit: checking original road faces and protected art.");
            for(int i=0;i<roads.Length;i++)
            {
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(paths[i]);Check(AssetDatabase.AssetPathToGUID(paths[i])==guids[i],"road GUID retained");
                Check(vertex[i].SequenceEqual(mesh.vertices)&&normals[i].SequenceEqual(mesh.normals)&&colors[i].SequenceEqual(mesh.colors)&&bounds[i]==mesh.bounds,"all old road vertices/normals/paint/bounds retained");
                for(int ch=0;ch<8;ch++)Check(uv[i][ch].SequenceEqual(UV(mesh,ch)),"all old UV channels retained");
                for(int s=0;s<mesh.subMeshCount;s++)
                {
                    var remaining=mesh.GetTriangles(s);Check(remaining.SequenceEqual(roads[i].Indices[s]),"exact selected face removal");
                    var faceSet=new HashSet<string>(Enumerable.Range(0,remaining.Length/3).Select(k=>remaining[k*3]+","+remaining[k*3+1]+","+remaining[k*3+2]));
                    var v=vertex[i];var t=originalFaces[i][s];
                    for(int j=0;j<t.Length;j+=3)
                    {
                        bool other=new[]{t[j],t[j+1],t[j+2]}.Any(k=>BiomeIslandLayout.At(v[k].x,v[k].z)!=IslandBiome.Grassland);
                        if(other)Check(faceSet.Contains(t[j]+","+t[j+1]+","+t[j+2]),"other island face retained");
                    }
                }
            }
            for(int i=0;i<protectedPaths.Length;i++)Check(Hash(protectedPaths[i])==hashes[i],"unrelated art / all terrain and collisions retained "+protectedPaths[i]);
            VerifyAssets();
            Debug.Log("Island meadow bake audit: checking repeat/manual authoring preservation.");
            string manual=GrasslandIslandMeadowSetup.Folder+"/Island meadow trail.asset";var saved=File.ReadAllBytes(manual);
            try
            {
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(manual);var c=mesh.colors;c[0].r*=.97f;mesh.colors=c;EditorUtility.SetDirty(mesh);AssetDatabase.SaveAssets();
                var list=Directory.GetFiles("Assets/Art","*",SearchOption.AllDirectories);var before=list.Select(Hash).ToArray();
                GrasslandIslandMeadowSetup.Rebuild();BiomeIslandsSetup.Rebuild();PrototypeAddressablesSetup.EnsureSetup();
                for(int i=0;i<list.Length;i++)Check(Hash(list[i])==before[i],"repeat / Setup retains later manual edits "+list[i]);
            }
            finally{File.WriteAllBytes(manual,saved);AssetDatabase.ImportAsset(manual,ImportAssetOptions.ForceUpdate);}
            Debug.Log("Island meadow bake verification PASSED: old ground, collision, road channels/GUIDs, other islands, custom metadata refusal and repeat/manual Setup.");
        }
        static Vector4[] UV(Mesh mesh,int channel){var list=new List<Vector4>();mesh.GetUVs(channel,list);return list.ToArray();}
        public static void VerifyAssets()
        {
            var root=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);
            try
            {
                var terrain=root.GetComponent<IslandTerrain>();var e=terrain.World.GetComponentInChildren<WorldWaterEcology>(true);Physics.SyncTransforms();
                Check(terrain.IsValid&&e.IsValid&&e.RabbitHabitats.Length==4&&e.MeadowRabbits.name=="Island meadow rabbits","complete AA ecology references");
                var group=terrain.World.Scenery.Find(GrasslandIslandMeadowSetup.Group);Check(group!=null&&group.GetComponentsInChildren<Collider>(true).Length==0,"pure baked visual subtree");
                var trail=group.Find("Island meadow trail").GetComponent<MeshFilter>().sharedMesh;var v=trail.vertices;var triangles=trail.triangles;var faces=new HashSet<string>();
                for(int i=0;i<triangles.Length;i+=3)
                {
                    var points=new[]{v[triangles[i]],v[triangles[i+1]],v[triangles[i+2]]};string key=string.Join(",",new[]{triangles[i],triangles[i+1],triangles[i+2]}.OrderBy(n=>n));Check(faces.Add(key),"union has no duplicate faces");
                    var box=new Bounds(points[0],Vector3.zero);foreach(var p in points)box.Encapsulate(p);
                    for(int x=Mathf.CeilToInt(box.min.x-.5f);x<=Mathf.FloorToInt(box.max.x+.5f);x++)for(int z=Mathf.CeilToInt(box.min.z-.5f);z<=Mathf.FloorToInt(box.max.z+.5f);z++)Check(!BiomeIslandLayout.CanPlace(x,z),"complete path footprint stays in old reserve");
                    if(i%33==0)
                    {
                        var center=(points[0]+points[1]+points[2])/3;
                        Check(terrain.World.Raycast(new Ray(new Vector3(center.x,300,center.z),Vector3.down),out var hit,350)&&Mathf.Abs(center.y-hit.point.y-.025f)<.004f,"road surface follows actual terrain triangles without holes");
                    }
                }
                Check(e.GrasslandAmbient.Count(r=>r.name.StartsWith("Island meadow",StringComparison.Ordinal))==3,"flower/insect references bound to existing clock");
                Check(terrain.SeasonalDetails.MeadowBatches.Length<=64,"bounded merged cover batches");
                foreach(var renderer in terrain.SeasonalDetails.MeadowBatches.Where(r=>r.name.StartsWith("Island meadow cover",StringComparison.Ordinal)))
                {
                    var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;var vertices=mesh.vertices;var roots=mesh.uv2;var wind=mesh.uv;
                    Check(roots.Length==vertices.Length&&wind.Length==vertices.Length&&mesh.uv3.Length==vertices.Length,"new plants retain full root/wind/season channels");
                    for(int i=0;i<vertices.Length;i++)Check(Vector2.Distance(new Vector2(vertices[i].x,vertices[i].z),roots[i])<=1.181f,"whole new plant fits original placement cut envelope");
                }
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        public static void Visit(GameManager g)
        {
            bool before=SessionState.GetBool(Key,false);
            string folder="Logs/IslandMeadow/"+(before?"Before":"After");Directory.CreateDirectory(folder);
            Check(g.TryTestBiomeTravel(IslandBiome.Grassland),"grassland travel");
            var terrain=g.Terrain.Terrain;var ecology=terrain.World.GetComponentInChildren<WorldWaterEcology>(true);
            Debug.Log("Island meadow inventory: rabbits="+ecology.RabbitHabitats.Length+", meadow batches="+terrain.SeasonalDetails.MeadowBatches.Length+", ambient="+ecology.GrasslandAmbient.Length);
            File.WriteAllLines(folder+"/Inventory.txt",terrain.World.GetComponentsInChildren<MeshFilter>(true).Select(f=>f.name+" | "+f.sharedMesh.vertexCount+" | "+f.GetComponent<Renderer>()?.sharedMaterial?.name)
                .Concat(ecology.GrasslandAmbient.Where(r=>r.sharedMaterial.shader.name=="MagicIsland/Meadow Insects").Select(r=>"Insects "+r.name+": "+string.Join(",",r.GetComponent<MeshFilter>().sharedMesh.uv2.Select(p=>p.y).Distinct()))));
            var camera=g.Players.Player.ViewCamera;var pos=camera.transform.position;var rot=camera.transform.rotation;float size=camera.orthographicSize,far=camera.farClipPlane;
            try
            {
                camera.farClipPlane=3200;g.Weather.Tick(720,true);g.Ocean.Surface.Tick(0,1,false,g.Weather.Sun);
                Frame(camera,new Vector3(-20,6,470),65,325);Capture(camera,folder+"/Overview.png");
                Frame(camera,new Vector3(-120,18,440),45,15);Capture(camera,folder+"/Arrival.png");
                Frame(camera,new Vector3(-103,17,526),45,18);Capture(camera,folder+"/Meadow.png");
                Frame(camera,new Vector3(-90,18,697),45,18);Capture(camera,folder+"/Bend.png");
                Frame(camera,new Vector3(-8,20,700),45,35);Capture(camera,folder+"/Tree.png");
                Frame(camera,new Vector3(-190,15,548),45,22);Capture(camera,folder+"/West.png");
            }
            finally{camera.transform.SetPositionAndRotation(pos,rot);camera.orthographicSize=size;camera.farClipPlane=far;}
            if(!before){VerifyWalk(g);VerifyEcology(g,folder);}
            Check(!File.Exists(g.Saves.SavePath),"daytime scenery checks do not save");
        }
        static void VerifyWalk(GameManager g)
        {
            var player=g.Players.Player;var original=player.transform.position;var camera=player.ViewCamera;var rotation=camera.transform.rotation;
            var tick=player.GetType().GetMethod("TickMovement",BindingFlags.Instance|BindingFlags.NonPublic);var controller=player.GetComponent<CharacterController>();int steps=0;
            try
            {
                camera.transform.rotation=Quaternion.Euler(45,0,0);
                foreach(var route in GrasslandIslandMeadowSetup.Routes())
                {
                    Check(g.Terrain.TryResolve(new Vector3(route[0].x,0,route[0].y),out var start),"trail start supported");g.Players.RestorePosition(start);
                    foreach(var waypoint in route.Skip(1))
                    {
                        int count=0;
                        while(Vector2.Distance(new Vector2(player.transform.position.x,player.transform.position.z),waypoint)>.13f&&count++<3000)
                        {
                            var direction=(waypoint-new Vector2(player.transform.position.x,player.transform.position.z)).normalized;
                            var previous=player.transform.position;tick.Invoke(player,new object[]{direction,.02f});steps++;
                            Check(Vector3.ProjectOnPlane(player.transform.position-previous,Vector3.up).magnitude>.001f,"actual path walking blocked at "+player.transform.position);
                            bool supported=g.Terrain.TryResolve(player.transform.position,out var floor);
                            Check(g.Terrain.Contains(player.transform.position,controller.radius)&&supported&&Mathf.Abs(player.transform.position.y-floor.y)<.3f,"actual capsule follows dry terrain at "+player.transform.position+", floor="+floor+", step="+steps);
                        }
                        Check(count<3000,"path waypoint reached");
                    }
                }
            }
            finally{g.Players.RestorePosition(original);camera.transform.rotation=rotation;Physics.SyncTransforms();}
            Debug.Log("Island meadow Packed walk PASSED: "+steps+" actual CharacterController steps, arrival/meadow/tree bends and side clearing.");
        }
        static Vector4[] Data(Renderer r,string name){var b=new MaterialPropertyBlock();r.GetPropertyBlock(b);return b.GetVectorArray(name)??Array.Empty<Vector4>();}
        static Vector3 placementPlayer;
        public static bool BeginPlacement(GameManager g)
        {
            if(SessionState.GetBool(Key,false))return false;
            placementPlayer=g.Players.Player.transform.position;string item=SeasonalGroundVerification.Placeable(g);Check(g.Inventory.TryAdd(item,1),"placement fixture inventory");
            g.Placements.StartCoroutine(g.Placements.Begin(item));return true;
        }
        public static void VerifyPlacement(GameManager g)
        {
            var cover=g.Terrain.Terrain.SeasonalDetails;Renderer chosen=null;Vector2 root=default;
            bool touch=cover.TouchSample.ArtVersion==1;bool shrubs=cover.MeadowBatches.Any(GrasslandShrubStoneVerification.Authored);bool layers=cover.MeadowBatches.Any(r=>r.name.StartsWith("Meadow undergrowth",StringComparison.Ordinal));bool groves=cover.MeadowBatches.Any(r=>r.name.StartsWith("Grove understory",StringComparison.Ordinal));
            foreach(var renderer in cover.MeadowBatches.Where(r=>touch?Array.IndexOf(cover.TouchSample.Renderers,r)>=0:shrubs?GrasslandShrubStoneVerification.Authored(r):r.name.StartsWith(layers?"Meadow undergrowth":groves?"Grove understory":"Island meadow cover",StringComparison.Ordinal)))
            {
                foreach(var p in touch?GrasslandTouchVerification.PlacementRoots(renderer,cover.TouchSample):shrubs?GrasslandShrubStoneVerification.PlacementRoots(renderer):renderer.GetComponent<MeshFilter>().sharedMesh.uv2.Distinct())
                {
                    int x=Mathf.RoundToInt(p.x),z=Mathf.RoundToInt(p.y);if(!BiomeIslandLayout.CanPlace(x,z)||!g.Terrain.TryResolve(new Vector3(x,0,z-1.7f),out var player))continue;
                    g.Players.RestorePosition(player);Physics.SyncTransforms();g.Placements.SetCandidate(x,z);if(g.Placements.CheckCandidate()!=PlacementResult.Success)continue;
                    chosen=renderer;root=p;break;
                }
                if(chosen!=null)break;
            }
            Check(chosen!=null&&g.Placements.Confirm(),"actual new meadow construction transaction");var block=new MaterialPropertyBlock();chosen.GetPropertyBlock(block);
            Check(block.GetVectorArray("_MeadowCuts").Take((int)block.GetFloat("_MeadowCutCount")).Any(c=>MeadowCoverPresentation.Covered(root,c)),"committed building hides new clump root");
            var actor=g.Placements.Objects.Values.Single();var renders=actor.GetComponentsInChildren<Renderer>();var enabled=renders.Select(r=>r.enabled).ToArray();var camera=g.Players.Player.ViewCamera;
            var position=camera.transform.position;var rotation=camera.transform.rotation;float size=camera.orthographicSize,far=camera.farClipPlane;
            try
            {
                foreach(var r in renders)r.enabled=false;Check(g.Terrain.TryResolve(new Vector3(root.x,0,root.y),out var point),"masked clump real floor");camera.farClipPlane=3200;Frame(camera,point+Vector3.up*.35f,45,2.3f);
                Capture(camera,"Logs/IslandMeadow/After/PlacementHidden.png");var visible=new MaterialPropertyBlock();chosen.GetPropertyBlock(visible);visible.SetFloat("_MeadowCutCount",0);chosen.SetPropertyBlock(visible);
                Capture(camera,"Logs/IslandMeadow/After/PlacementVisible.png");chosen.SetPropertyBlock(block);
                Check(!File.ReadAllBytes("Logs/IslandMeadow/After/PlacementHidden.png").SequenceEqual(File.ReadAllBytes("Logs/IslandMeadow/After/PlacementVisible.png")),"actual GPU removes intersecting new plants");
            }
            finally{chosen.SetPropertyBlock(block);for(int i=0;i<renders.Length;i++)renders[i].enabled=enabled[i];camera.transform.SetPositionAndRotation(position,rotation);camera.orthographicSize=size;camera.farClipPlane=far;}
            cover.Bind(g.Placements,g.Gathering.State,g.Terrain.Terrain.Paths,g.Terrain.Terrain,g.Animals);cover.Present(g.Calendar.Date.Season,true);chosen.GetPropertyBlock(block);Check(block.GetFloat("_MeadowCutCount")>=1,"old construction rebind hides new cover");
            Check(g.Placements.Recover(actor),"construction recovery");chosen.GetPropertyBlock(block);Check(block.GetFloat("_MeadowCutCount")==0,"recovery restores grass");
            cover.Present(g.Calendar.Date.Season,false);Check(cover.MeadowBatches.Where(r=>r.name.StartsWith("Island meadow cover",StringComparison.Ordinal)).All(r=>!r.enabled),"new batches hidden indoors");cover.Present(g.Calendar.Date.Season,true);
            g.Players.RestorePosition(placementPlayer);Check(!File.Exists(g.Saves.SavePath),"scenery and placement do not save in daytime");
            Debug.Log("Island meadow Packed construction PASSED: real placement/recovery, whole-plant GPU cuts, old construction rebind and indoor visibility.");
        }
        static float[] Visible(Renderer r){var b=new MaterialPropertyBlock();r.GetPropertyBlock(b);return b.GetFloatArray("_RabbitVisible")??Array.Empty<float>();}
        static void VerifyEcology(GameManager g,string folder)
        {
            var terrain=g.Terrain.Terrain;var e=terrain.World.GetComponentInChildren<WorldWaterEcology>(true);var r=e.MeadowRabbits;Check(e.Bound&&e.RabbitHabitats.Length==4,"Packed AA rabbit population");
            var source=AssetDatabase.LoadAssetAtPath<Mesh>(GrasslandIslandMeadowSetup.Folder+"/Island meadow rabbits.asset");Check(source.vertices.SequenceEqual(r.GetComponent<MeshFilter>().sharedMesh.vertices),"real Packed rabbit mesh");
            var all=e.GrasslandAmbient.Concat(new[]{r}).ToArray();var original=all.Select(_=>new MaterialPropertyBlock()).ToArray();for(int i=0;i<all.Length;i++)all[i].GetPropertyBlock(original[i]);
            var camera=g.Players.Player.ViewCamera;var pos=camera.transform.position;var rotation=camera.transform.rotation;float size=camera.orthographicSize,far=camera.farClipPlane;GameObject obstacle=null;
            float clock=0;Action<float,Vector3?> tick=(seconds,p)=>{clock+=seconds;e.Tick(clock,seconds,1,false,0,p,null,false,true);};
            try
            {
                e.Clear();
                for(int i=0;i<all.Length;i++){var b=new MaterialPropertyBlock();all[i].GetPropertyBlock(b);b.SetFloat("_IslandMeadowSentinel",23.5f);b.SetVector("_IslandMeadowVector",new Vector4(1,2,3,4));all[i].SetPropertyBlock(b);}
                e.Bind(g.Ocean.Surface.FishAnimation);tick(0,null);var home=e.transform.TransformPoint(e.RabbitHabitats[0].Track[12]);
                camera.farClipPlane=3200;g.Weather.Tick(720,true);Frame(camera,home+Vector3.up*.4f,45,5);Capture(camera,folder+"/RabbitRest.png");
                bool hopping=false,foraging=false;var first=Data(r,"_RabbitPose");
                for(int i=0;i<600;i++)
                {
                    tick(1f/30,null);var poses=Data(r,"_RabbitPose");var actions=Data(r,"_RabbitActions");
                    for(int n=0;n<4;n++)
                    {
                        var local=e.transform.InverseTransformPoint((Vector3)poses[n]);var h=e.RabbitHabitats[n];var a=h.Track[0];var b=h.Track.Last();float t=Mathf.Clamp01(Vector3.Dot(local-a,b-a)/(b-a).sqrMagnitude);
                        Check(Vector3.Distance(local,Vector3.Lerp(a,b,t))<.05f,"rabbit stays on actual supported safe strip");hopping|=actions[n].x>.5f;foraging|=actions[n].y>.3f;
                    }
                }
                Check(hopping&&foraging&&!first.SequenceEqual(Data(r,"_RabbitPose")),"independent rest/forage/hop activity");Capture(camera,folder+"/RabbitActivity.png");
                var frozen=Data(r,"_RabbitPose");var frozenActions=Data(r,"_RabbitActions");tick(0,home);Check(frozen.SequenceEqual(Data(r,"_RabbitPose"))&&frozenActions.SequenceEqual(Data(r,"_RabbitActions")),"modal pause freezes rabbit state");
                e.Tick(clock+10,10,1,false,0,null,null,false,false);Check(!e.gameObject.activeSelf&&frozen.SequenceEqual(Data(r,"_RabbitPose")),"indoor hide/freeze");tick(0,null);
                var butterfly=e.GrasslandAmbient.Single(a=>a.name=="Island meadow Butterfly");var pollen=e.GrasslandAmbient.Single(a=>a.name=="Island meadow pollen");var centers=butterfly.GetComponent<MeshFilter>().sharedMesh.uv;var heights=butterfly.GetComponent<MeshFilter>().sharedMesh.uv2;
                var flowerHome=butterfly.transform.TransformPoint(new Vector3(centers[0].x,heights[0].x,centers[0].y));tick(0,null);var block=new MaterialPropertyBlock();butterfly.GetPropertyBlock(block);Check(block.GetFloat("_MeadowMotionReady")==1,"new phases use bounded existing motion slots");
                var flowers=Data(butterfly,"_MeadowFlowers");Check(flowers.Length==32,"existing fixed capacity");
                Frame(camera,flowerHome,45,3.5f);Capture(camera,folder+"/ButterflyRest.png");
                for(int i=0;i<45;i++)tick(1f/30,flowerHome);Check(Data(butterfly,"_MeadowResponses").Any(v=>v.w>.9f),"approach startles local butterfly");Capture(camera,folder+"/ButterflyFlight.png");
                var responses=Data(butterfly,"_MeadowResponses");tick(0,null);Check(responses.SequenceEqual(Data(butterfly,"_MeadowResponses")),"modal pause freezes insect response");
                e.Tick(clock,3,1,true,0,null,null,false,true);butterfly.GetPropertyBlock(block);Check(block.GetFloat("_MeadowRain")>0,"rain shelters insects through original clock");
                g.Weather.Tick(1320,true);g.Ocean.Surface.Tick(0,0,false,g.Weather.Sun);e.Tick(clock+4,4,0,false,0,null,null,false,true);Capture(camera,folder+"/Fireflies.png");
                obstacle=new GameObject("Island meadow old construction verification",typeof(BoxCollider));obstacle.transform.SetParent(terrain.transform.parent,false);obstacle.transform.position=home;obstacle.GetComponent<BoxCollider>().size=new Vector3(8,4,8);Physics.SyncTransforms();
                e.Bind(g.Ocean.Surface.FishAnimation);Check(Visible(r)[0]==0,"old construction keeps covered rabbit hidden");Object.DestroyImmediate(obstacle);obstacle=null;Physics.SyncTransforms();
                e.Clear();foreach(var renderer in all){renderer.GetPropertyBlock(block);Check(block.GetFloat("_IslandMeadowSentinel")==23.5f&&block.GetVector("_IslandMeadowVector")==new Vector4(1,2,3,4),"complete original MPB restored");}
                Debug.Log("Island meadow Packed ecology PASSED: four animated rabbits, six flower habitats, local butterfly response, pause/indoor/rain, old construction priority and MPB/AA cleanup.");
            }
            finally
            {
                if(obstacle!=null)Object.DestroyImmediate(obstacle);e.Clear();for(int i=0;i<all.Length;i++)all[i].SetPropertyBlock(original[i]);e.Bind(g.Ocean.Surface.FishAnimation);
                g.Weather.Tick(720,true);g.Ocean.Surface.Tick(0,1,false,g.Weather.Sun);camera.transform.SetPositionAndRotation(pos,rotation);camera.orthographicSize=size;camera.farClipPlane=far;Physics.SyncTransforms();
            }
        }
        static void Frame(Camera camera,Vector3 point,float pitch,float size)
        {camera.transform.rotation=Quaternion.Euler(pitch,0,0);camera.transform.position=point-camera.transform.forward*1600;camera.orthographicSize=size;}
        static void Capture(Camera camera,string path)
        {
            var rt=new RenderTexture(1280,800,24);var old=camera.targetTexture;var active=RenderTexture.active;var texture=new Texture2D(1280,800,TextureFormat.RGB24,false);
            try{camera.targetTexture=rt;camera.Render();camera.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,1280,800),0,0);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());}
            finally{camera.targetTexture=old;RenderTexture.active=active;Object.DestroyImmediate(rt);Object.DestroyImmediate(texture);}
        }
    }
}
