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
    public static class GrasslandFlowerVisitVerification
    {
        const string Folder="Logs/GrasslandFlowerVisits";
        static void Check(bool ok,string why){if(!ok)throw new InvalidOperationException("Flower visit verification: "+why);}
        static string Hash(string p){using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(p)));}
        static Vector4[] Data(Renderer r,string key){var b=new MaterialPropertyBlock();r.GetPropertyBlock(b);return b.GetVectorArray(key)??Array.Empty<Vector4>();}
        public static void BakeAndVerify()
        {
            Check(Application.dataPath.Replace('\\','/').Contains("/Logs/"),"isolated project required");Directory.CreateDirectory(Folder);
            var root=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);
            try
            {
                var t=root.GetComponent<MagicIsland.TerrainSystem.IslandTerrain>();var e=t.World.GetComponentInChildren<WorldWaterEcology>(true);
                var plan=GrasslandFlowerVisitSetup.Plan(t);Check(plan.Length==3,"three flower bands");
                e.FlowerVisitVersion=-1;bool refused=false;try{GrasslandFlowerVisitSetup.Plan(t);}catch(InvalidOperationException){refused=true;}Check(refused,"partial link state refused");e.FlowerVisitVersion=0;
                e.FlowerVisits=plan;e.FlowerVisitVersion=1;Check(e.IsValid,"complete linked data valid");
                int index=plan[0].Vertex;plan[0].Vertex=int.MaxValue;Check(!e.IsValid,"invalid source vertex rejected");plan[0].Vertex=index;
                int slot=plan[1].Slot;plan[1].Slot=plan[0].Slot;Check(!e.IsValid,"duplicate slots rejected");plan[1].Slot=slot;
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            var paths=Directory.GetFiles("Assets/Art","*",SearchOption.AllDirectories).Select(p=>p.Replace('\\','/')).ToArray();var hashes=paths.Select(Hash).ToArray();
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(GrasslandIslandMeadowSetup.Folder+"/Island meadow Butterfly.asset");
            var vertices=mesh.vertices;var normals=mesh.normals;var colors=mesh.colors;var faces=mesh.triangles;var uv=new[]{mesh.uv,mesh.uv2,mesh.uv3};
            GrasslandFlowerVisitSetup.Rebuild();
            Check(mesh.vertices.SequenceEqual(vertices)&&mesh.normals.SequenceEqual(normals)&&mesh.colors.SequenceEqual(colors)&&mesh.triangles.SequenceEqual(faces)
                &&mesh.uv.SequenceEqual(uv[0])&&mesh.uv2.SequenceEqual(uv[1])&&mesh.uv3.SequenceEqual(uv[2]),"all insect geometry/channels retained");
            for(int i=0;i<paths.Length;i++)if(paths[i]!=TerrainSetup.PrefabPath&&paths[i]!=AssetDatabase.GetAssetPath(mesh))Check(Hash(paths[i])==hashes[i],"all unowned art protected "+paths[i]);
            var art=paths.Select(Hash).ToArray();GrasslandFlowerVisitSetup.Rebuild();PrototypeAddressablesSetup.EnsureSetup();
            for(int i=0;i<paths.Length;i++)Check(Hash(paths[i])==art[i],"repeat/ordinary Setup preserve linked art "+paths[i]);
            foreach(string name in new[]{"MagicIsland/Meadow Insects",GrasslandFloraSetup.ShaderName})Check(!ShaderUtil.ShaderHasError(Shader.Find(name)),"compiled production shader "+name);
            Debug.Log("Grassland flower visit bake verification PASSED: 3 existing insects linked to actual flower vertices; original geometry and all other art protected; invalid/duplicate/partial refusal and idempotent Setup.");
        }
        public static void After()=>GrasslandShrubWindVerification.After();
        public static void Visit(GameManager g)
        {
            var e=g.Terrain.Terrain.World.GetComponentInChildren<WorldWaterEcology>(true);if(e.FlowerVisitVersion!=1)return;
            Check(e.Bound&&e.FlowerVisits.Length==3,"Packed AA optional flower links");Directory.CreateDirectory(Folder+"/Sequence");
            var surface=g.Ocean.Surface;var r=e.GrasslandAmbient.Single(a=>a.name=="Island meadow Butterfly");var links=e.FlowerVisits;
            var camera=g.Players.Player.ViewCamera;var follow=camera.GetComponent<MagicIsland.CameraSystem.CameraController>();bool enabled=follow.enabled;
            var position=camera.transform.position;var rotation=camera.transform.rotation;float size=camera.orthographicSize;var minute=g.Time.Clock.Minute;var weather=g.Weather.State.Kind;
            var canvases=Object.FindObjectsOfType<Canvas>();var canvasEnabled=canvases.Select(c=>c.enabled).ToArray();var rng=JsonUtility.ToJson(UnityEngine.Random.state);
            var material=r.sharedMaterial;var bounds=r.GetComponent<MeshFilter>().sharedMesh.bounds;
            float time=0;Action<float,Vector3?,bool> tick=(dt,p,outside)=>{if(outside)time+=dt;e.Tick(time,dt,1,false,0,p,null,false,outside);};
            try
            {
                e.Clear();var unboundHomes=Data(r,"_MeadowVisitingHomes");var unboundResponses=Data(r,"_MeadowResponses");
                e.Bind(surface.FishAnimation);tick(0,null,true);var homes=Data(r,"_MeadowVisitingHomes");var flowers=Data(r,"_MeadowFlowers");
                Check(homes.Count(h=>h.w==1)==3,"only three relocated butterflies; others preserve original habitats");
                foreach(var link in links)
                {
                    var m=link.Source.GetComponent<MeshFilter>().sharedMesh;var point=link.Source.transform.TransformPoint(m.vertices[link.Vertex]);
                    Check(Vector3.Distance((Vector3)flowers[link.Slot],point+Vector3.up*.025f)<.00001f,"lands on actual Packed flower vertex");
                    Check(bounds.Contains(r.transform.InverseTransformPoint((Vector3)homes[link.Slot])+Vector3.one*1.1f)
                        &&bounds.Contains(r.transform.InverseTransformPoint((Vector3)homes[link.Slot])-Vector3.one*1.1f),"complete body/flight/startle culling bounds");
                }
                var home=(Vector3)homes[links[0].Slot];var near=home+Vector3.down*.4f;
                float phase=r.GetComponent<MeshFilter>().sharedMesh.uv2.First(p=>
                {int site=Mathf.FloorToInt((p.y+.0001f)/2.399963f);return site*2+Mathf.RoundToInt((p.y-site*2.399963f)/1.83f)==links[0].Slot;}).y;
                float individual=Mathf.Repeat(Mathf.Sin(phase*17.13f+1.7f)*437.17f,1),period=11+individual*4;
                float restTime=Mathf.Repeat(period*.08f-phase*2,period);
                tick(.1f,near,true);Check(Data(r,"_MeadowResponses")[links[0].Slot].w>0&&Data(r,"_MeadowResponses")[links[0].Slot].w<.5f,"continuous local startle");
                for(int n=0;n<30;n++)tick(.05f,near,true);
                Check(Data(r,"_MeadowResponses")[links[0].Slot].w>.9f&&links.Skip(1).All(l=>Data(r,"_MeadowResponses")[l.Slot].w==0),"nearby flower only, other bands undisturbed");
                var frozen=Data(r,"_MeadowResponses");tick(0,home+Vector3.right*15,true);Check(frozen.SequenceEqual(Data(r,"_MeadowResponses")),"pause ignores moved player");
                tick(10,null,false);Check(!e.gameObject.activeSelf&&frozen.SequenceEqual(Data(r,"_MeadowResponses")),"indoor hides and freezes");tick(0,null,true);
                tick(.2f,null,true);Check(Data(r,"_MeadowResponses")[links[0].Slot].w>.7f,"return does not snap");tick(8,null,true);Check(Data(r,"_MeadowResponses")[links[0].Slot].w<.01f,"returns to flower after departure");
                // Simulate the existing cover owner's committed construction cut, then restore it exactly.
                var source=links[0].Source;var saved=new MaterialPropertyBlock();source.GetPropertyBlock(saved);var cut=new MaterialPropertyBlock();source.GetPropertyBlock(cut);
                var root=source.GetComponent<MeshFilter>().sharedMesh.uv2[links[0].Vertex];var worldRoot=source.transform.TransformPoint(new Vector3(root.x,0,root.y));
                try
                {
                    cut.SetFloat("_MeadowCutCount",1);cut.SetVectorArray("_MeadowCuts",new[]{MagicIsland.TerrainSystem.MeadowCoverPresentation.Cut(worldRoot)});source.SetPropertyBlock(cut);tick(0,null,true);
                    Check(Data(r,"_MeadowVisitingHomes")[links[0].Slot].w== -1,"covered flower does not retain floating butterfly");
                    e.Bind(surface.FishAnimation);Check(Data(r,"_MeadowVisitingHomes")[links[0].Slot].w== -1,"old construction cut respected on rebind");
                }
                finally{source.SetPropertyBlock(saved);tick(0,null,true);}
                Check(Data(r,"_MeadowVisitingHomes")[links[0].Slot].w==1,"recovery restores visiting insect");
                // A later cover update must reach the insect before rendering, without advancing its state.
                var touch=new MaterialPropertyBlock();source.GetPropertyBlock(touch);
                try
                {
                    touch.SetFloat("_MeadowTouchCount",0);touch.SetVectorArray("_MeadowTouches",new[]{new Vector4(home.x,home.z,home.y,1)});source.SetPropertyBlock(touch);
                    e.Present(camera);var latest=new MaterialPropertyBlock();r.GetPropertyBlock(latest);
                    Check(latest.GetFloat("_MeadowTouchCount")==0,"stale backing array does not resurrect recovered grass contact");
                    touch.SetFloat("_MeadowTouchCount",1);source.SetPropertyBlock(touch);var response=Data(r,"_MeadowResponses");e.Present(camera);r.GetPropertyBlock(latest);
                    Check(latest.GetFloat("_MeadowTouchCount")==1&&response.SequenceEqual(Data(r,"_MeadowResponses")),"late cover sync does not advance startle");
                }
                finally{source.SetPropertyBlock(saved);tick(0,null,true);}
                typeof(WeatherState).GetProperty("Kind").SetValue(g.Weather.State,WeatherKind.Sunny);AtmospherePolishVerification.Advance(g.Weather,40);LightingFlowVerification.SetTime(g,720);
                follow.enabled=false;foreach(var c in canvases)c.enabled=false;
                GrasslandTouchVerification.Frame(camera,home);camera.orthographicSize=3;g.Terrain.Terrain.SeasonalDetails.TickTouch(0,null,true,true);
                e.Bind(surface.FishAnimation);time=restTime;tick(0,null,true);GrasslandTouchVerification.Capture(camera,Folder+"/Rest.png");
                time=restTime+period*.45f;tick(0,null,true);GrasslandTouchVerification.Capture(camera,Folder+"/Flight.png");
                Check(!File.ReadAllBytes(Folder+"/Rest.png").SequenceEqual(File.ReadAllBytes(Folder+"/Flight.png")),"real GPU merged butterfly pose changes between rest and flight");
                var renderers=Object.FindObjectsOfType<Renderer>();var shown=renderers.Select(a=>a.enabled).ToArray();
                try
                {
                    foreach(var other in renderers)other.enabled=other==r;
                    GrasslandTouchVerification.Capture(camera,Folder+"/FlightIsolated.png");
                    tick(0,near,true);GrasslandTouchVerification.Capture(camera,Folder+"/PausedIsolated.png");
                    Check(File.ReadAllBytes(Folder+"/FlightIsolated.png").SequenceEqual(File.ReadAllBytes(Folder+"/PausedIsolated.png")),"actual merged-insect GPU paused frame remains exact");
                }
                finally{for(int n=0;n<renderers.Length;n++)if(renderers[n]!=null)renderers[n].enabled=shown[n];}
                e.Bind(surface.FishAnimation);time=restTime;tick(0,null,true);
                for(int frame=0;frame<150;frame++)
                {
                    tick(.08f,frame>=42&&frame<70?(Vector3?)near:null,true);
                    GrasslandTouchVerification.Capture(camera,Folder+"/Sequence/"+frame.ToString("000")+".png");
                }
                var a=Data(r,"_MeadowResponses");tick(0,near,true);Check(a.SequenceEqual(Data(r,"_MeadowResponses")),"recording frozen on zero seconds");
                e.Tick(time,4,1,true,0,null,null,false,true);var block=new MaterialPropertyBlock();r.GetPropertyBlock(block);Check(block.GetFloat("_MeadowRain")==1,"rain uses original sheltering");
                e.Clear();Check(Data(r,"_MeadowVisitingHomes").SequenceEqual(unboundHomes)&&Data(r,"_MeadowResponses").SequenceEqual(unboundResponses),"Clear restores binding-time original MPB arrays");
                e.Bind(surface.FishAnimation);Check(Data(r,"_MeadowResponses").All(v=>v==Vector4.zero),"rebind has no old startle");
                e.Clear();e.FlowerVisits=Array.Empty<MeadowFlowerVisit>();e.FlowerVisitVersion=0;e.Bind(surface.FishAnimation);
                Check(Data(r,"_MeadowVisitingHomes").All(v=>v==Vector4.zero),"unlinked original art retains its ambient habitat path");
                e.Clear();e.FlowerVisits=links;e.FlowerVisitVersion=1;e.Bind(surface.FishAnimation);
                for(int i=0;i<links.Length;i++)
                {
                    var center=(Vector3)Data(r,"_MeadowVisitingHomes")[links[i].Slot];GrasslandTouchVerification.Frame(camera,center);camera.orthographicSize=4.5f;
                    tick(0,null,true);GrasslandTouchVerification.Capture(camera,Folder+"/Band"+i+".png");
                }
                Check(material==r.sharedMaterial&&rng==JsonUtility.ToJson(UnityEngine.Random.state),"shared material and gameplay RNG retained");
                Check(!File.Exists(g.Saves.SavePath),"cosmetic visit and recording never save progress");
            }
            finally
            {
                e.FlowerVisits=links;e.FlowerVisitVersion=1;
                typeof(WeatherState).GetProperty("Kind").SetValue(g.Weather.State,weather);AtmospherePolishVerification.Advance(g.Weather,40);LightingFlowVerification.SetTime(g,minute);
                e.Bind(surface.FishAnimation);follow.enabled=enabled;camera.transform.SetPositionAndRotation(position,rotation);camera.orthographicSize=size;
                for(int i=0;i<canvases.Length;i++)if(canvases[i]!=null)canvases[i].enabled=canvasEnabled[i];
            }
            Debug.Log("Grassland flower visit Packed verification PASSED: actual AA flower vertices, three independent short-flight habitats, local startle/gradual return, pause/indoors/rain, construction cut/recovery/rebind, culling bounds, material/RNG preservation, cleanup and no daytime save; 150 real frames.");
        }
    }
}
