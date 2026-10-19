using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using MagicIsland.Managers;
using MagicIsland.TerrainSystem;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    public static class GrasslandTouchVerification
    {
        const string Folder="Logs/GrasslandTouch",Baseline="Assets/Editor/GrasslandTouchBaseline";
        static void Check(bool ok,string why){if(!ok)throw new InvalidOperationException("Grassland touch verification: "+why);}
        static string Hash(string p){using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(p)));}
        static Vector4[] UV(Mesh m,int channel){var a=new List<Vector4>();m.GetUVs(channel,a);return a.ToArray();}
        public static void BakeAndVerify()
        {
            Check(Application.dataPath.Replace('\\','/').Contains("/Logs/"),"isolated project required");var root=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);GrasslandTouchSetup.Edit[] plan;
            try
            {
                var terrain=root.GetComponent<IslandTerrain>();plan=GrasslandTouchSetup.Plan(terrain);var r=plan[0].Renderer;var material=r.sharedMaterial;var custom=new Material(Shader.Find("Standard"));
                try{r.sharedMaterial=custom;bool refused=false;try{GrasslandTouchSetup.Plan(terrain);}catch(InvalidOperationException){refused=true;}Check(refused,"custom material refused before writes");}
                finally{r.sharedMaterial=material;Object.DestroyImmediate(custom);}
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            var paths=plan.Select(e=>AssetDatabase.GetAssetPath(e.Mesh)).ToArray();var vertices=plan.Select(e=>e.Mesh.vertices).ToArray();var normals=plan.Select(e=>e.Mesh.normals).ToArray();var colors=plan.Select(e=>e.Mesh.colors).ToArray();var faces=plan.Select(e=>e.Mesh.triangles).ToArray();var bounds=plan.Select(e=>e.Mesh.bounds).ToArray();var guids=paths.Select(AssetDatabase.AssetPathToGUID).ToArray();var channels=plan.Select(e=>Enumerable.Range(0,8).Select(ch=>UV(e.Mesh,ch)).ToArray()).ToArray();
            if(!AssetDatabase.IsValidFolder(Baseline))AssetDatabase.CreateFolder("Assets/Editor","GrasslandTouchBaseline");
            foreach(var path in paths){string target=Baseline+"/"+Path.GetFileName(path);if(File.Exists(target))AssetDatabase.DeleteAsset(target);Check(AssetDatabase.CopyAsset(path,target),"source comparison fixture");}
            var excluded=new HashSet<string>(paths.SelectMany(p=>new[]{p,p+".meta"}).Concat(new[]{TerrainSetup.PrefabPath}));var protect=Directory.GetFiles("Assets/Art","*",SearchOption.AllDirectories).Select(p=>p.Replace('\\','/')).Where(p=>!excluded.Contains(p)).ToArray();var hashes=protect.Select(Hash).ToArray();
            GrasslandTouchSetup.Rebuild();
            for(int e=0;e<plan.Length;e++)
            {
                var m=plan[e].Mesh;Check(guids[e]==AssetDatabase.AssetPathToGUID(paths[e])&&m.vertices.SequenceEqual(plan[e].Vertices)&&m.normals.SequenceEqual(plan[e].Normals)&&m.colors.SequenceEqual(colors[e])&&m.triangles.SequenceEqual(faces[e]),"exact planned local geometry, colors/topology/GUID retained");
                for(int ch=0;ch<8;ch++)Check(UV(m,ch).SequenceEqual(channels[e][ch]),"all original UV channels retained");
                Check(m.bounds.center==bounds[e].center&&m.bounds.size==bounds[e].size+Vector3.one*.5f,"conservative full wind and touch bounds");
                var actualV=m.vertices;var actualN=m.normals;for(int i=0;i<m.vertexCount;i++)if(!plan[e].Changed[i])Check(actualV[i]==vertices[e][i]&&actualN[i]==normals[e][i],"every original anchored base, other plant and outside sample retained");
            }
            for(int i=0;i<protect.Length;i++)Check(Hash(protect[i])==hashes[i],"unrelated art/ground/collisions/materials retained "+protect[i]);
            root=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);MeadowTouchSettings settings;
            try{var t=root.GetComponent<IslandTerrain>();settings=t.SeasonalDetails.TouchSample;Check(t.IsValid&&settings.IsValid&&settings.ArtVersion==1&&settings.Renderers.Length==plan.Length,"AA sample settings and original renderer references");Simulation(settings);Owner(settings,plan[0].Mesh,settings.Renderers[0].sharedMaterial);}
            finally{PrefabUtility.UnloadPrefabContents(root);}
            byte[] prefab=File.ReadAllBytes(TerrainSetup.PrefabPath),meshBytes=File.ReadAllBytes(paths[0]);
            try
            {
                root=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);
                try{var t=root.GetComponent<IslandTerrain>();var s=t.SeasonalDetails.TouchSample;s.BendDistance=.16f;t.SeasonalDetails.TouchSample=s;PrefabUtility.SaveAsPrefabAsset(root,TerrainSetup.PrefabPath);}
                finally{PrefabUtility.UnloadPrefabContents(root);}
                var paint=plan[0].Mesh.colors;paint[0].r*=.99f;plan[0].Mesh.colors=paint;EditorUtility.SetDirty(plan[0].Mesh);AssetDatabase.SaveAssets();var all=Directory.GetFiles("Assets/Art","*",SearchOption.AllDirectories);var saved=all.Select(Hash).ToArray();
                GrasslandTouchSetup.Rebuild();GrasslandFloraSetup.Rebuild();PrototypeAddressablesSetup.EnsureSetup();for(int i=0;i<all.Length;i++)Check(Hash(all[i])==saved[i],"repeat/old flora entry/Setup preserve legal manual edits "+all[i]);
            }
            finally{File.WriteAllBytes(TerrainSetup.PrefabPath,prefab);File.WriteAllBytes(paths[0],meshBytes);AssetDatabase.ImportAsset(TerrainSetup.PrefabPath,ImportAssetOptions.ForceUpdate);AssetDatabase.ImportAsset(paths[0],ImportAssetOptions.ForceUpdate);}
            Debug.Log("Grassland touch bake verification PASSED: "+plan.Length+" original meshes, "+plan.Sum(e=>e.Blades)+" refined blades; anchors/all UV/other geometry/collisions/materials retained, custom refusal, fixed trail/pause/recovery/warp, single MPB owner/cut preservation, repeat/manual Setup.");
        }
        static void Simulation(MeadowTouchSettings settings)
        {
            var p=new Vector3(settings.Center.x,14,settings.Center.y);var sim=new MeadowTouchSimulation(settings,settings.Center);Check(sim.Count==0,"fresh trail has no phantom contacts");
            sim.Tick(.04f,p,false,true);Check(sim.Count==1&&sim.Contact(0).w>0&&sim.Contact(0).w<1,"gentle initial contact");
            sim.Tick(.3f,p+Vector3.right*1.2f,false,true);Check(sim.Count>3&&sim.Count<=MeadowTouchSimulation.Capacity,"low-frame sweep fills bounded contact trail");
            var frozen=Enumerable.Range(0,MeadowTouchSimulation.Capacity).Select(sim.Contact).ToArray();sim.Tick(5,p+Vector3.right*1.2f,true,true);Check(Enumerable.Range(0,MeadowTouchSimulation.Capacity).All(i=>sim.Contact(i)==frozen[i]),"pause freezes all visual contact ages/positions");
            sim.Tick(.7f,p+Vector3.right*3.9f,false,true);float before=sim.Contact(1).w;sim.Tick(.2f,p+Vector3.right*3.9f,false,true);Check(sim.Contact(1).w<before&&sim.Count<=MeadowTouchSimulation.Capacity,"rebound progresses without exceeding fixed capacity");
            sim.Tick(settings.RecoverySeconds+ .1f,p+Vector3.right*3.9f,false,true);Check(Enumerable.Range(1,MeadowTouchSimulation.Capacity-1).All(i=>sim.Contact(i).w==0),"old footsteps fully rebound while standing elsewhere");
            sim.Tick(.01f,p+Vector3.right*100,true,true);Check(sim.Count==0,"paused teleport clears stale outdoor trail");sim.Tick(.1f,p,false,true);sim.Tick(1,p,true,false);Check(sim.Count==0,"indoor/exit clears outdoor pressure");
        }
        static void Owner(MeadowTouchSettings settings,Mesh mesh,Material material)
        {
            var fixture=new GameObject("Single meadow MPB owner fixture");MeadowCoverPresentation owner=null;
            try
            {
                fixture.AddComponent<MeshFilter>().sharedMesh=mesh;var r=fixture.AddComponent<MeshRenderer>();r.sharedMaterial=material;var original=new MaterialPropertyBlock();original.SetFloat("_TouchSentinel",17);original.SetFloat("_MeadowCutCount",5);r.SetPropertyBlock(original);
                settings.Renderers=new Renderer[]{r};owner=new MeadowCoverPresentation(settings.Renderers,settings);owner.Present(true);owner.TickTouch(.2f,new Vector3(settings.Center.x,14,settings.Center.y),false,true);owner.Refresh(null);
                var block=new MaterialPropertyBlock();r.GetPropertyBlock(block);Check(block.GetFloat("_MeadowTouchCount")>0&&block.GetFloat("_MeadowCutCount")==0&&block.GetFloat("_TouchSentinel")==17,"one owner preserves pressure during building-cut refresh");owner.Clear();r.GetPropertyBlock(block);
                Check(r.enabled&&r.sharedMaterial==material&&block.GetFloat("_MeadowCutCount")==5&&block.GetFloat("_TouchSentinel")==17&&block.GetFloat("_MeadowTouchCount")==0,"clear restores exact original MPB and renderer without material clones");
            }
            finally{owner?.Clear();Object.DestroyImmediate(fixture);}
        }
        public static void After()=>GrasslandCrownWindVerification.After();
        static readonly MethodInfo tick=typeof(WeatherManager).GetMethod("TickMeadowTouch",BindingFlags.Instance|BindingFlags.NonPublic);
        static void Tick(GameManager g,float dt,bool paused=false)=>tick.Invoke(g.Weather,new object[]{dt,paused});
        public static IEnumerable<Vector2> PlacementRoots(Renderer r,MeadowTouchSettings settings)
        {
            var m=r.GetComponent<MeshFilter>().sharedMesh;var uv=m.uv;var roots=m.uv2;
            return Enumerable.Range(0,uv.Length).Where(i=>(uv[i].x<=-4&&uv[i].x>=-5||uv[i].x<=-2&&uv[i].x>=-3)&&Vector2.Distance(roots[i],settings.Center)<settings.AreaRadius-settings.Feather).Select(i=>roots[i]).Distinct();
        }
        public static void Visit(GameManager g)
        {
            var cover=g.Terrain.Terrain.SeasonalDetails;var settings=cover.TouchSample;if(settings.ArtVersion==0)return;
            Check(settings.IsValid&&settings.ArtVersion==1,"real Packed sample settings");Directory.CreateDirectory(Folder);
            Simulation(settings);
            var renderers=settings.Renderers;var filters=renderers.Select(r=>r.GetComponent<MeshFilter>()).ToArray();var meshes=filters.Select(f=>f.sharedMesh).ToArray();var layers=renderers.Select(r=>r.gameObject.layer).ToArray();
            var camera=g.Players.Player.ViewCamera;var position=g.Players.Player.transform.position;var cameraPos=camera.transform.position;var cameraRot=camera.transform.rotation;float size=camera.orthographicSize,far=camera.farClipPlane;int mask=camera.cullingMask;
            float wind=Shader.GetGlobalFloat("_IslandWindTime"),strength=Shader.GetGlobalFloat("_IslandWindStrength");var candidates=new List<Vector2>();
            foreach(var f in filters)
            {
                var m=f.sharedMesh;var authored=AssetDatabase.LoadAssetAtPath<Mesh>(BiomeIslandsSetup.Folder+"/"+m.name+".asset");Check(authored!=null&&m.vertices.SequenceEqual(authored.vertices)&&m.uv.SequenceEqual(authored.uv)&&f.GetComponent<Renderer>().sharedMaterial.shader.isSupported,"actual AA sample mesh, original metadata and supported shader");
                candidates.AddRange(PlacementRoots(f.GetComponent<Renderer>(),settings));
            }
            var character=g.Players.Player.GetComponent<CharacterController>();
            bool Free(Vector3 point)
            {
                var center=point+character.center;float radius=character.radius+character.skinWidth,half=Mathf.Max(0,character.height*.5f-radius);
                return Physics.OverlapCapsule(center-Vector3.up*half,center+Vector3.up*half,radius,~0,QueryTriggerInteraction.Ignore).All(c=>g.Terrain.Terrain.IsGround(c)||c.transform.IsChildOf(character.transform));
            }
            Vector3 focus=default;bool found=false;Physics.SyncTransforms();
            foreach(var p in candidates.OrderBy(p=>Vector2.Distance(p,settings.Center)))
            {
                if(!g.Terrain.TryResolve(new Vector3(p.x,0,p.y),out var floor)||!Free(floor))continue;bool safe=true;
                for(int i=-8;i<=8&&safe;i++){safe=g.Terrain.TryResolve(floor+Vector3.right*(i*.5f),out var step)&&Free(step);}
                if(safe){focus=floor;found=true;break;}
            }
            Check(found,"actual ground and complete free capsule across the sample path");
            try
            {
                g.Players.RestorePosition(focus);g.Cameras.RestoreOccluders();g.Weather.Tick(720,true);g.Ocean.Surface.Tick(0,1,false,g.Weather.Sun);
                Frame(camera,focus);camera.farClipPlane=3200;cover.TickTouch(0,null,false,true);Shader.SetGlobalFloat("_IslandWindTime",12);Shader.SetGlobalFloat("_IslandWindStrength",.65f);
                var properties=new MaterialPropertyBlock();var current=new MaterialPropertyBlock[renderers.Length];
                for(int i=0;i<renderers.Length;i++)
                {
                    current[i]=new MaterialPropertyBlock();renderers[i].GetPropertyBlock(current[i]);renderers[i].GetPropertyBlock(properties);properties.SetVector("_MeadowTouchArea",Vector4.zero);renderers[i].SetPropertyBlock(properties);
                    filters[i].sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Baseline+"/"+meshes[i].name+".asset");Check(filters[i].sharedMesh!=null,"original sample comparison fixture");
                }
                Capture(camera,Folder+"/Before.png");for(int i=0;i<renderers.Length;i++){filters[i].sharedMesh=meshes[i];renderers[i].SetPropertyBlock(current[i]);}Capture(camera,Folder+"/After.png");
                // Isolate the actual combined grass: player/water/animals cannot satisfy this GPU assertion.
                Shader.SetGlobalFloat("_IslandWindStrength",0);foreach(var r in renderers)r.gameObject.layer=30;camera.cullingMask=1<<30;
                var unpressed=Capture(camera,Folder+"/IsolatedRest.png");Tick(g,.2f);var pressed=Capture(camera,Folder+"/IsolatedPressed.png");Check(GrasslandCrownWindVerification.FrameChanges(unpressed,pressed,out _)>16,"actual GPU grass responds to the real player's position with zero wind");
                renderers[0].GetPropertyBlock(properties);var contacts=properties.GetVectorArray("_MeadowTouches");Check(properties.GetFloat("_MeadowTouchCount")>0&&contacts.Length==MeadowTouchSimulation.Capacity,"fixed-size per-instance contact data from WeatherManager");
                double clock=g.Weather.Wind.Time;Tick(g,4,true);var frozen=Capture(camera,Folder+"/IsolatedPaused.png");Check(GrasslandCrownWindVerification.StableFrame(pressed,frozen)&&g.Weather.Wind.Time==clock,"modal pause freezes actual grass frame and shared wind clock");
                cover.TickTouch(0,null,false,true);var restored=Capture(camera,Folder+"/IsolatedRestored.png");Check(GrasslandCrownWindVerification.StableFrame(unpressed,restored),"clearing contact restores the unpressed GPU frame within rounding tolerance");
                camera.cullingMask=mask;for(int i=0;i<renderers.Length;i++)renderers[i].gameObject.layer=layers[i];Tick(g,.2f);Capture(camera,Folder+"/Pressed.png");
                Shader.SetGlobalFloat("_IslandWindStrength",.9f);Shader.SetGlobalFloat("_IslandWindTime",2);var waveA=Capture(camera,Folder+"/WaveA.png");Shader.SetGlobalFloat("_IslandWindTime",7);var waveB=Capture(camera,Folder+"/WaveB.png");Check(!waveA.SequenceEqual(waveB),"combined contact and traveling grass wave still use shared wind globals");
                Check(g.Terrain.TryResolve(focus-Vector3.right*3,out var start),"walk start");g.Players.RestorePosition(start);cover.TickTouch(0,null,false,true);
                var move=typeof(MagicIsland.Player.PlayerMovement).GetMethod("TickMovement",BindingFlags.Instance|BindingFlags.NonPublic);
                var windTick=typeof(WeatherManager).GetMethod("TickWind",BindingFlags.Instance|BindingFlags.NonPublic);
                for(int step=0;step<32;step++)
                {
                    var before=g.Players.Player.transform.position;move.Invoke(g.Players.Player,new object[]{Vector2.right,.04f});Tick(g,.04f);windTick.Invoke(g.Weather,new object[]{.04f,false});
                    Check(g.Players.Player.transform.position.x>before.x+.001f,"actual player walks through sample without new collisions");
                    if(step%2==0){Frame(camera,g.Players.Player.transform.position);Capture(camera,Folder+"/Walk"+(step/2).ToString("D2")+".png");}
                }
                Tick(g,settings.RecoverySeconds+.1f);Check(!File.Exists(g.Saves.SavePath),"visual interaction never writes daytime progress");
                Debug.Log("Grassland touch Packed verification PASSED: actual AA sample, safe default-camera walk, isolated zero-wind GPU pressure, exact pause/clear frame, shared wind waves, bounded contacts and no daytime save. Sample focus "+focus);
            }
            finally
            {
                for(int i=0;i<filters.Length;i++){filters[i].sharedMesh=meshes[i];renderers[i].gameObject.layer=layers[i];}
                cover.TickTouch(0,null,false,true);g.Players.RestorePosition(position);camera.cullingMask=mask;camera.transform.SetPositionAndRotation(cameraPos,cameraRot);camera.orthographicSize=size;camera.farClipPlane=far;Shader.SetGlobalFloat("_IslandWindTime",wind);Shader.SetGlobalFloat("_IslandWindStrength",strength);
            }
        }
        public static void Inside(GameManager g)
        {
            var cover=g.Terrain.Terrain.SeasonalDetails;if(cover.TouchSample.ArtVersion==0)return;Tick(g,1);
            var block=new MaterialPropertyBlock();foreach(var r in cover.TouchSample.Renderers){r.GetPropertyBlock(block);Check((!r.enabled||!r.gameObject.activeInHierarchy||(g.Players.Player.ViewCamera.cullingMask&(1<<r.gameObject.layer))==0)&&block.GetFloat("_MeadowTouchCount")==0,"indoor presentation hides and clears outdoor contacts");}
        }
        public static void Released(GameManager g) { Check(g.Terrain.Terrain==null,"terrain AA owner releases all meadow contact references"); }
        static void Frame(Camera camera,Vector3 point){camera.transform.rotation=Quaternion.Euler(42,0,0);camera.orthographicSize=7;camera.transform.position=point+Vector3.up*.6f-camera.transform.forward*18;}
        static byte[] Capture(Camera camera,string path)
        {
            var rt=new RenderTexture(1280,800,24);var old=camera.targetTexture;var active=RenderTexture.active;var image=new Texture2D(1280,800,TextureFormat.RGB24,false);
            try{camera.targetTexture=rt;camera.Render();camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1280,800),0,0);image.Apply();var bytes=image.EncodeToPNG();File.WriteAllBytes(path,bytes);return bytes;}
            finally{camera.targetTexture=old;RenderTexture.active=active;Object.DestroyImmediate(rt);Object.DestroyImmediate(image);}
        }
    }
}
