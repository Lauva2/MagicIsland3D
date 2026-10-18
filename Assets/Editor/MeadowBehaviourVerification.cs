using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using MagicIsland.Managers;
using MagicIsland.Ocean;
using MagicIsland.WeatherSystem;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    public static class MeadowBehaviourVerification
    {
        static void Check(bool ok,string why){if(!ok)throw new InvalidOperationException("Meadow behaviour: "+why);}
        public static void VerifyAssets()
        {
            Check(Application.dataPath.Replace('\\','/').Contains("/Logs/"),"isolated project only");
            var files=Directory.GetFiles("Assets/Art","*",SearchOption.AllDirectories);
            Func<string,string> hash=p=>{using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(p)));};
            var before=files.Select(hash).ToArray();GrasslandEcologySetup.Rebuild();PrototypeAddressablesSetup.EnsureSetup();
            for(int i=0;i<files.Length;i++)Check(before[i]==hash(files[i]),"all original art and metadata retained: "+files[i]);
            var shader=Shader.Find("MagicIsland/Meadow Insects");Check(shader!=null&&shader.isSupported&&!ShaderUtil.ShaderHasError(shader),"compiled insect shader");
            Debug.Log("Meadow behaviour asset verification PASSED: shader compiles; ordinary Setup and explicit ecology preserve every existing art file.");
        }
        static Vector4[] Responses(Renderer renderer)
        {var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);return block.GetVectorArray("_MeadowResponses")??Array.Empty<Vector4>();}
        static byte[] Shot(Camera camera,string file)
        {
            var active=RenderTexture.active;var target=camera.targetTexture;var rt=new RenderTexture(960,540,24);var texture=new Texture2D(960,540,TextureFormat.RGB24,false);
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,960,540),0,0);texture.Apply();var data=texture.GetRawTextureData<byte>().ToArray();File.WriteAllBytes("Logs/MeadowBehaviour/"+file+".png",texture.EncodeToPNG());return data;}
            finally{camera.targetTexture=target;RenderTexture.active=active;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(texture);}
        }
        public static void VerifyRuntime(GameManager game)
        {
            Directory.CreateDirectory("Logs/MeadowBehaviour");
            var surface=game.Ocean.Surface;var ecology=surface.WorldEcology;ecology.Bind(surface.FishAnimation);
            var insects=ecology.transform.Find(GrasslandEcologySetup.Group).GetComponentsInChildren<Renderer>(true);
            var butterfly=insects.First(r=>r.name.Contains("Butterfly"));var block=new MaterialPropertyBlock();butterfly.GetPropertyBlock(block);
            Check(block.GetFloat("_MeadowMotionReady")==1,"Packed merged butterfly metadata bound");var flowers=block.GetVectorArray("_MeadowFlowers");
            var centers=new Vector3[32];var active=new bool[32];var phases=new float[32];
            foreach(var r in insects.Where(r=>r.name.Contains("Butterfly")))
            {
                var mesh=r.GetComponent<MeshFilter>().sharedMesh;var uv=mesh.uv;var height=mesh.uv2;r.GetPropertyBlock(block);Check(block.GetFloat("_MeadowMotionReady")==1,"every Packed batch ready");
                for(int v=0;v<uv.Length;v++){float phase=height[v].y;int site=Mathf.FloorToInt((phase+.0001f)/2.399963f),slot=site*2+Mathf.RoundToInt((phase-site*2.399963f)/1.83f);centers[slot]=r.transform.TransformPoint(new Vector3(uv[v].x,height[v].x,uv[v].y));active[slot]=true;phases[slot]=phase;}
            }
            Check(active.Count(v=>v)==30,"all thirty existing butterflies represented");
            var pollen=ecology.GrasslandAmbient.Where(r=>r.sharedMaterial.shader.name=="MagicIsland/Meadow Foliage").SelectMany(r=>
            {var m=r.GetComponent<MeshFilter>().sharedMesh;var c=m.colors;return m.vertices.Where((p,i)=>Mathf.Abs(c[i].r-.90f)<.002f&&Mathf.Abs(c[i].g-.59f)<.002f&&Mathf.Abs(c[i].b-.23f)<.002f).Select(r.transform.TransformPoint);}).ToArray();
            for(int i=0;i<32;i++)if(active[i])
            {
                var flower=(Vector3)flowers[i];Check(pollen.Any(p=>(p+Vector3.up*.025f-flower).sqrMagnitude<.000001f),"lands on existing pollen, rather than stone or guessed ground");
                Check(new Vector2(flower.x-centers[i].x,flower.z-centers[i].z).magnitude+.096f+.32f<1.05f,"complete perched shape and rooted wind inside original footprint");
            }
            float time=0;Action<float,Vector3?,bool> tick=(dt,p,outside)=>{if(outside)time+=dt;ecology.Tick(time,dt,1,false,0,p,null,false,outside);};
            var random=JsonUtility.ToJson(UnityEngine.Random.state);var materials=ecology.GrasslandAmbient.Select(r=>EditorJsonUtility.ToJson(r.sharedMaterial)).ToArray();
            var near=centers[0]+new Vector3(0,-.9f,-.4f);tick(.1f,near,true);var first=Responses(butterfly);Check(first[0].w>0&&first[0].w<.5f,"soft startle begins continuously");
            for(int i=0;i<20;i++)tick(.05f,near,true);var startled=Responses(butterfly);Check(startled[0].w>.9f,"near player interrupts flower rest");
            Check(Vector2.Dot(new Vector2(startled[0].x,startled[0].z),new Vector2(centers[0].x-near.x,centers[0].z-near.z))>0,"escape points away from player");
            Check(Vector2.Distance(new Vector2(startled[0].x,startled[0].z),new Vector2(startled[1].x,startled[1].z))>.3f,"neighbours spread independently");
            for(int i=2;i<32;i++)if(active[i])Check(startled[i].w==0,"distant flower habitats undisturbed");
            tick(0,centers[20],true);Check(startled.SequenceEqual(Responses(butterfly)),"paused player motion cannot change avoidance");
            tick(10,null,false);Check(startled.SequenceEqual(Responses(butterfly))&&!ecology.gameObject.activeSelf,"indoors stops state and hides subtree");tick(0,near,true);
            tick(.2f,null,true);Check(Responses(butterfly)[0].w>.7f,"departure retains a gentle return instead of snapping");
            tick(7,null,true);Check(Responses(butterfly)[0].w<.02f,"returns to flower activity after departure");
            // Low frame rate and moving player: every offset remains inside the already baked bounds.
            for(int n=0;n<120;n++)
            {
                tick(n%4==0?.7f:.016f,centers[0]+new Vector3(Mathf.Sin(n*.31f),-.9f,Mathf.Cos(n*.31f)),true);
                foreach(var p in Responses(butterfly))Check(new Vector2(p.x,p.z).magnitude<=.5501f&&p.w>=0&&p.w<=1&&p.y<=.2801f,"bounded response at mixed frame rates");
            }
            var camera=game.Players.Player.ViewCamera;var controller=camera.GetComponent<MagicIsland.CameraSystem.CameraController>();bool follow=controller.enabled;var position=camera.transform.position;float size=camera.orthographicSize;
            double originalMinute=game.Time.Clock.Minute;var originalWeather=game.Weather.State.Kind;
            var all=Object.FindObjectsOfType<Renderer>();var enabled=all.Select(r=>r.enabled).ToArray();var canvases=Object.FindObjectsOfType<Canvas>();var canvasEnabled=canvases.Select(c=>c.enabled).ToArray();
            try
            {
                typeof(WeatherState).GetProperty("Kind").SetValue(game.Weather.State,WeatherKind.Sunny);AtmospherePolishVerification.Advance(game.Weather,40);LightingFlowVerification.SetTime(game,750);
                controller.enabled=false;foreach(var canvas in canvases)canvas.enabled=false;camera.orthographicSize=2.2f;camera.transform.position=centers[0]-camera.transform.forward*20;
                ecology.Bind(surface.FishAnimation);time=0;tick(0,null,true);Shot(camera,"Perched");
                time=6;tick(0,null,true);var flying=Shot(camera,"FlowerFlight");
                for(int i=0;i<20;i++)tick(.05f,near,true);var fleeing=Shot(camera,"Startled");Check(!flying.SequenceEqual(fleeing),"actual GPU startle changes merged insects");
                tick(0,centers[20],true);Check(fleeing.SequenceEqual(Shot(camera,"Paused")),"actual GPU paused response frame exact");
                tick(8,null,true);Shot(camera,"Returned");
                ecology.Bind(surface.FishAnimation);time=0;Directory.CreateDirectory("Logs/MeadowBehaviour/Sequence");
                for(int frame=0;frame<48;frame++){tick(.16f,frame>=10&&frame<23?(Vector3?)near:null,true);Shot(camera,"Sequence/"+frame.ToString("00"));}
                foreach(var r in all)r.enabled=false;foreach(var r in insects)r.enabled=r.name.Contains("Butterfly");
                time=0;tick(0,null,true);var rest=Shot(camera,"RestIsolated");time=6;tick(0,null,true);Check(!rest.SequenceEqual(Shot(camera,"FlightIsolated")),"actual GPU flower rest and short flight differ");
                foreach(var r in insects)r.enabled=r.name.Contains("Firefly");LightingFlowVerification.SetTime(game,1260);ecology.Tick(3,0,0,false,0,null,null,false,true);var night=Shot(camera,"FirefliesA");ecology.Tick(4.2f,0,0,false,0,null,null,false,true);Check(!night.SequenceEqual(Shot(camera,"FirefliesB")),"actual GPU independent nocturnal movement and glow");
                for(int i=0;i<ecology.GrasslandAmbient.Length;i++)Check(materials[i]==EditorJsonUtility.ToJson(ecology.GrasslandAmbient[i].sharedMaterial),"shared materials unchanged");
                Check(random==JsonUtility.ToJson(UnityEngine.Random.state),"no gameplay RNG");
                ecology.Clear();Check(Responses(butterfly).Length==0,"Clear restores original property arrays");ecology.Bind(surface.FishAnimation);Check(Responses(butterfly).All(p=>p==Vector4.zero),"rebind has no old fear");
            }
            finally
            {
                typeof(WeatherState).GetProperty("Kind").SetValue(game.Weather.State,originalWeather);AtmospherePolishVerification.Advance(game.Weather,40);LightingFlowVerification.SetTime(game,originalMinute);
                for(int i=0;i<all.Length;i++)if(all[i]!=null)all[i].enabled=enabled[i];for(int i=0;i<canvases.Length;i++)if(canvases[i]!=null)canvases[i].enabled=canvasEnabled[i];
                controller.enabled=follow;camera.transform.position=position;camera.orthographicSize=size;
            }
            Debug.Log("Meadow behaviour runtime verification PASSED: thirty Packed butterflies land on authored flowers; soft local startle, distinct escape directions, gradual return, low frame rate bounds, exact paused GPU frame, indoor stop, nocturnal GPU motion, RNG/material preservation and Clear/rebind.");
        }
    }
}
