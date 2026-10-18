using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using MagicIsland.Managers;
using MagicIsland.Ocean;
using MagicIsland.TerrainSystem;
using MagicIsland.WeatherSystem;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    public static class MeadowRabbitVerification
    {
        static void Check(bool ok,string why){if(!ok)throw new InvalidOperationException("Meadow rabbit verification: "+why);}
        static WorldWaterEcology Owner()=>AssetDatabase.LoadAssetAtPath<GameObject>(TerrainSetup.PrefabPath).GetComponent<IslandTerrain>().World.GetComponentInChildren<WorldWaterEcology>(true);
        static Vector3 RotateX(Vector3 p,float a)=>Quaternion.AngleAxis(a*Mathf.Rad2Deg,Vector3.right)*p;
        static Vector3 RotateZ(Vector3 p,float a)=>Quaternion.AngleAxis(a*Mathf.Rad2Deg,Vector3.forward)*p;
        public static void ShapeEnvelope()
        {
            var d=MeadowRabbitSetup.ReadSource();float radius=0,height=0,minY=100;
            foreach(float hop in new[]{0f,.3f,.7f,1f})foreach(float forage in new[]{0f,.75f})foreach(float look in new[]{-.12f,0,.12f})foreach(float alert in new[]{0f,1f})foreach(float flick in new[]{0f,.04f})
                for(int i=0;i<d.parts.Length;i++)
                {
                    var p=new Vector3(d.positions[i*3],d.positions[i*3+1],d.positions[i*3+2]);var pivot=new Vector3(d.pivots[i*3],d.pivots[i*3+1],d.pivots[i*3+2]);int part=d.parts[i];
                    if(part==2||part==3)p=pivot+RotateZ(p-pivot,(part==2?-1:1)*(.10f+.13f*alert+.06f*look+flick));
                    if(part>=1&&part<=3){var neck=new Vector3(0,.40f,.17f);p=neck+RotateZ(RotateX(p-neck,forage*.42f),look);}
                    if(part>=4)p=pivot+RotateX(p-pivot,hop*(part>=6?-.48f:.45f));p.y+=hop*.18f;
                    radius=Mathf.Max(radius,new Vector2(p.x,p.z).magnitude);height=Mathf.Max(height,p.y);minY=Mathf.Min(minY,p.y);
                }
            Check(radius<MeadowRabbitSetup.Radius&&height<MeadowRabbitSetup.Height&&minY>=-.001f,"all rotated head/ear/limb/hop shapes fit query volume: "+radius+" / "+height+" / "+minY);
        }
        public static void BakeAndVerify()
        {
            Check(Application.dataPath.Replace('\\','/').Contains("/Logs/"),"isolated project only");ShapeEnvelope();
            var old=Directory.GetFiles("Assets/Art","*",SearchOption.AllDirectories).Select(p=>p.Replace('\\','/')).Where(p=>p!=TerrainSetup.PrefabPath).ToArray();var oldBytes=old.Select(File.ReadAllBytes).ToArray();
            var terrain=AssetDatabase.LoadAssetAtPath<GameObject>(TerrainSetup.PrefabPath).GetComponent<IslandTerrain>();var collisions=terrain.GetComponentsInChildren<Collider>(true).Select(c=>EditorJsonUtility.ToJson(c)).ToArray();
            MeadowRabbitSetup.Rebuild();terrain=AssetDatabase.LoadAssetAtPath<GameObject>(TerrainSetup.PrefabPath).GetComponent<IslandTerrain>();var owner=Owner();
            Check(owner.IsValid&&owner.RabbitHabitats.Length==4&&owner.MeadowRabbits!=null,"four valid baked habitats");
            Check(collisions.SequenceEqual(terrain.GetComponentsInChildren<Collider>(true).Select(c=>EditorJsonUtility.ToJson(c))),"all original collision records retained");
            var group=owner.transform.Find(MeadowRabbitSetup.Group);Check(group.GetComponentsInChildren<Transform>(true).Length==1&&group.GetComponentsInChildren<MonoBehaviour>(true).Length==0&&group.GetComponentsInChildren<Collider>(true).Length==0&&group.GetComponentsInChildren<Light>(true).Length==0,"one merged batch without individual animal objects/components");
            foreach(var h in owner.RabbitHabitats)foreach(var p in h.Track)Check(MeadowRabbitSetup.Reserved(new Vector2(p.x,p.z)),"complete body preserves all construction cells and paths");
            var files=new[]{TerrainSetup.PrefabPath,MeadowRabbitSetup.MeshPath,MeadowRabbitSetup.MaterialPath};var bytes=files.Select(File.ReadAllBytes).ToArray();MeadowRabbitSetup.Rebuild();PrototypeAddressablesSetup.EnsureSetup();
            for(int i=0;i<files.Length;i++)Check(bytes[i].SequenceEqual(File.ReadAllBytes(files[i])),"repeat/Setup idempotent");
            try
            {
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(MeadowRabbitSetup.MeshPath);var c=mesh.colors;c[0]=Color.magenta;mesh.colors=c;var v=mesh.vertices;v[0].y+=.01f;mesh.vertices=v;EditorUtility.SetDirty(mesh);
                var mat=AssetDatabase.LoadAssetAtPath<Material>(MeadowRabbitSetup.MaterialPath);mat.renderQueue=2460;EditorUtility.SetDirty(mat);AssetDatabase.SaveAssets();var authored=files.Select(File.ReadAllBytes).ToArray();MeadowRabbitSetup.Rebuild();PrototypeAddressablesSetup.EnsureSetup();
                for(int i=0;i<files.Length;i++)Check(authored[i].SequenceEqual(File.ReadAllBytes(files[i])),"manual mesh and material preserved");
            }
            finally{for(int i=0;i<files.Length;i++){File.WriteAllBytes(files[i],bytes[i]);AssetDatabase.ImportAsset(files[i],ImportAssetOptions.ForceSynchronousImport);}}
            for(int i=0;i<old.Length;i++)Check(oldBytes[i].SequenceEqual(File.ReadAllBytes(old[i])),"unrelated art retained "+old[i]);
            Debug.Log("Meadow rabbit bake verification PASSED: Blender shape envelopes, four reserved strips, one merged batch, all old art/colliders, repeat/Setup and manual edits retained.");
        }
        static Vector4[] Data(Renderer r,string key){var b=new MaterialPropertyBlock();r.GetPropertyBlock(b);return b.GetVectorArray(key)??Array.Empty<Vector4>();}
        static float[] Visibility(Renderer r){var b=new MaterialPropertyBlock();r.GetPropertyBlock(b);return b.GetFloatArray("_RabbitVisible")??Array.Empty<float>();}
        static void CheckPausedPixels(byte[] before,byte[] after)
        {
            Check(before.Length==after.Length,"pause image dimensions");int changed=0;
            for(int i=0;i<before.Length;i++)if(before[i]!=after[i]){Check(Math.Abs(before[i]-after[i])<=1,"pause exceeds GPU color rounding");changed++;}
            Check(changed<=8,"pause changes more than eight rounded color channels");
        }
        static byte[] Shot(Camera camera,string name)
        {
            var rt=new RenderTexture(960,540,24);var texture=new Texture2D(960,540,TextureFormat.RGB24,false);var active=RenderTexture.active;var target=camera.targetTexture;
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,960,540),0,0);texture.Apply();File.WriteAllBytes("Logs/MeadowRabbits/"+name+".png",texture.EncodeToPNG());return texture.GetRawTextureData<byte>().ToArray();}
            finally{camera.targetTexture=target;RenderTexture.active=active;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(texture);}
        }
        public static void VerifyRuntime(GameManager game)
        {
            Directory.CreateDirectory("Logs/MeadowRabbits");var surface=game.Ocean.Surface;var e=surface.WorldEcology;Check(e.Bound&&e.MeadowRabbits!=null&&e.RabbitHabitats.Length==4,"Packed rabbit population bound before input");
            var r=e.MeadowRabbits;var matrix=r.transform.localToWorldMatrix;var material=EditorJsonUtility.ToJson(r.sharedMaterial);var random=JsonUtility.ToJson(UnityEngine.Random.state);
            e.Bind(surface.FishAnimation);var track=e.RabbitHabitats[0].Track;var home=e.transform.TransformPoint(track[track.Length/2]);var direction=e.transform.TransformDirection((track[track.Length-1]-track[0]).normalized);float clock=0;
            Action<float,Vector3?> tick=(dt,p)=>{clock+=dt;e.Tick(clock,dt,1,false,0,p,null,false,true);};
            bool hopping=false,foraging=false,observing=false;var previous=Data(r,"_RabbitPose");
            for(int frame=0;frame<600;frame++)
            {
                float dt=frame%19==0?.25f:.03333334f;tick(dt,null);var poses=Data(r,"_RabbitPose");var actions=Data(r,"_RabbitActions");
                for(int i=0;i<4;i++)
                {
                    Check(Vector3.Distance((Vector3)poses[i],(Vector3)previous[i])<e.RabbitHabitats[i].EscapeSpeed*1.5f*(dt+.04f),"bounded continuous motion at mixed frame rates");
                    var local=e.transform.InverseTransformPoint((Vector3)poses[i]);var h=e.RabbitHabitats[i];var a=h.Track[0];var b=h.Track[h.Track.Length-1];var t=Mathf.Clamp01(Vector3.Dot(local-a,b-a)/(b-a).sqrMagnitude);Check(Vector3.Distance(local,Vector3.Lerp(a,b,t))<.05f,"stays on baked supported strip");
                    Check(MeadowRabbitSetup.Reserved(new Vector2(local.x,local.z)),"full body remains outside construction and main road");
                    hopping|=actions[i].x>.6f;foraging|=actions[i].y>.35f;observing|=Mathf.Abs(actions[i].z)>.07f;
                }
                previous=poses;
            }
            Check(hopping&&foraging&&observing,"independent hopping, foraging and observing");
            e.Bind(surface.FishAnimation);clock=0;var near=home-direction*1.4f;tick(.05f,near);var start=Data(r,"_RabbitPose")[0];for(int i=0;i<65;i++)tick(.03333334f,near);
            Check(Vector3.Distance((Vector3)Data(r,"_RabbitPose")[0],near)>Vector3.Distance((Vector3)start,near)+.2f,"flees away from nearby player");
            var alarm=Data(r,"_RabbitActions");Check(alarm[0].w>.9f&&alarm[1].w<.4f,"local threat affects only nearby animal");
            var paused=Data(r,"_RabbitPose");var pausedActions=Data(r,"_RabbitActions");tick(0,home+direction*30);Check(paused.SequenceEqual(Data(r,"_RabbitPose"))&&pausedActions.SequenceEqual(Data(r,"_RabbitActions")),"all pose/action state frozen at zero seconds");
            e.Tick(clock+10,10,1,false,0,null,null,false,false);Check(paused.SequenceEqual(Data(r,"_RabbitPose"))&&!e.gameObject.activeSelf,"indoors hidden without advancement");tick(0,null);
            for(int i=0;i<900;i++)tick(.03333334f,null);Check(Vector3.Distance((Vector3)Data(r,"_RabbitPose")[0],home)<1.0f,"returns to home strip center after release");
            GameObject obstacle=null;
            try
            {
                e.Bind(surface.FishAnimation);clock=0;
                obstacle=new GameObject("Rabbit verification old placement",typeof(BoxCollider));obstacle.transform.SetParent(game.Terrain.Terrain.transform.parent,false);obstacle.transform.position=home;obstacle.GetComponent<BoxCollider>().size=new Vector3(8,4,8);Physics.SyncTransforms();
                e.Bind(surface.FishAnimation);Check(Visibility(r).Take(4).First()==0,"covered old construction keeps rabbit hidden, leaves placement intact");
                Object.DestroyImmediate(obstacle);obstacle=null;Physics.SyncTransforms();for(int i=0;i<50;i++)tick(.03333334f,null);Check(Visibility(r)[0]>.9f,"uncovered strip resumes without stale obstacle references");
                e.Bind(surface.FishAnimation);clock=0;obstacle=new GameObject("Rabbit verification new obstacle",typeof(BoxCollider));obstacle.transform.SetParent(game.Terrain.Terrain.transform.parent,false);obstacle.transform.position=home+direction*1.15f+Vector3.up*.6f;obstacle.GetComponent<BoxCollider>().size=new Vector3(1,1.2f,1);Physics.SyncTransforms();
                for(int i=0;i<90;i++)tick(.03333334f,near);var p=(Vector3)Data(r,"_RabbitPose")[0];Check(Vector3.Dot(p-home,direction)<.4f,"new obstacle blocks the swept route without teleport");
            }
            finally{if(obstacle!=null)Object.DestroyImmediate(obstacle);Physics.SyncTransforms();}
            var camera=game.Players.Player.ViewCamera;var controller=camera.GetComponent<MagicIsland.CameraSystem.CameraController>();bool follow=controller.enabled;var cameraPosition=camera.transform.position;float size=camera.orthographicSize;double minute=game.Time.Clock.Minute;var weather=game.Weather.State.Kind;
            var canvases=Object.FindObjectsOfType<Canvas>();var canvasEnabled=canvases.Select(c=>c.enabled).ToArray();
            try
            {
                typeof(WeatherState).GetProperty("Kind").SetValue(game.Weather.State,WeatherKind.Sunny);AtmospherePolishVerification.Advance(game.Weather,40);LightingFlowVerification.SetTime(game,750);
                controller.enabled=false;foreach(var c in canvases)c.enabled=false;camera.orthographicSize=2.4f;camera.transform.position=home+Vector3.up*.5f-camera.transform.forward*20;
                e.Bind(surface.FishAnimation);clock=0;tick(0,null);Shot(camera,"Rest");Directory.CreateDirectory("Logs/MeadowRabbits/Sequence");
                bool head=false,hop=false;
                for(int frame=0;frame<96;frame++)
                {
                    tick(.12f,frame>=30&&frame<52?(Vector3?)near:null);Shot(camera,"Sequence/"+frame.ToString("00"));var action=Data(r,"_RabbitActions")[0];
                    if(!head&&action.y>.35f){Shot(camera,"Forage");head=true;}if(!hop&&action.x>.6f){Shot(camera,"Hop");hop=true;}
                }
                var actual=Shot(camera,"BeforePause");var finalPose=Data(r,"_RabbitPose");var finalActions=Data(r,"_RabbitActions");tick(0,home-direction*2);
                Check(finalPose.SequenceEqual(Data(r,"_RabbitPose"))&&finalActions.SequenceEqual(Data(r,"_RabbitActions")),"actual GPU pose and joints exactly frozen");CheckPausedPixels(actual,Shot(camera,"Paused"));
                Check(head&&hop,"real GPU head/ear/limb animation sampled");
                e.Clear();var b=new MaterialPropertyBlock();r.GetPropertyBlock(b);Check(b.GetFloat("_RabbitBound")==0&&Data(r,"_RabbitPose").Length==0,"Clear restores complete original rabbit property block");e.Bind(surface.FishAnimation);Check(Data(r,"_RabbitActions").All(a=>a==Vector4.zero),"rebind clears old habits/threat");
                Check(matrix==r.transform.localToWorldMatrix&&material==EditorJsonUtility.ToJson(r.sharedMaterial)&&random==JsonUtility.ToJson(UnityEngine.Random.state),"no shared material/transform/RNG changes");
            }
            finally
            {
                foreach(var c in canvases.Select((value,i)=>new{value,i}))if(c.value!=null)c.value.enabled=canvasEnabled[c.i];controller.enabled=follow;camera.transform.position=cameraPosition;camera.orthographicSize=size;
                typeof(WeatherState).GetProperty("Kind").SetValue(game.Weather.State,weather);AtmospherePolishVerification.Advance(game.Weather,40);LightingFlowVerification.SetTime(game,minute);
            }
            Debug.Log("Meadow rabbit runtime verification PASSED: Packed batch, safe supported movement, grazing/observing/hops, local player escape and return, old/new obstacles, exactly frozen pose/joints and GPU pause within 1/255 rounding, indoor freeze, RNG/material preservation and AA property cleanup.");
        }
    }
}
