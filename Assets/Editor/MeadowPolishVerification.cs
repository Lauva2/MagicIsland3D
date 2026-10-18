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
    public static class MeadowPolishVerification
    {
        static void Check(bool ok,string why){if(!ok)throw new InvalidOperationException("Meadow polish: "+why);}
        static readonly string[] Changed={TerrainSetup.PrefabPath,MeadowRabbitSetup.MeshPath,MeadowRabbitSetup.MeshPath+".meta"};
        public static void BakeAndVerify()
        {
            Check(Application.dataPath.Replace('\\','/').Contains("/Logs/"),"isolated project only");MeadowRabbitVerification.ShapeEnvelope();
            var files=Directory.GetFiles("Assets/Art","*",SearchOption.AllDirectories).Select(p=>p.Replace('\\','/')).Where(p=>!Changed.Contains(p)).ToArray();var bytes=files.Select(File.ReadAllBytes).ToArray();
            var prefab=File.ReadAllBytes(TerrainSetup.PrefabPath);var meshBytes=File.ReadAllBytes(MeadowRabbitSetup.MeshPath);
            var terrain=AssetDatabase.LoadAssetAtPath<GameObject>(TerrainSetup.PrefabPath).GetComponent<IslandTerrain>();var colliders=terrain.GetComponentsInChildren<Collider>(true).Select(c=>EditorJsonUtility.ToJson(c)).ToArray();
            var importer=AssetImporter.GetAtPath(MeadowRabbitSetup.MeshPath);
            if(importer.userData!=MeadowCompositionSetup.Marker)
            {
                try
                {
                    var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(MeadowRabbitSetup.MeshPath);var c=mesh.colors;c[0]=Color.magenta;mesh.colors=c;EditorUtility.SetDirty(mesh);AssetDatabase.SaveAssets();
                    bool rejected=false;try{MeadowCompositionSetup.Refine();}catch(InvalidOperationException){rejected=true;}
                    Check(rejected&&prefab.SequenceEqual(File.ReadAllBytes(TerrainSetup.PrefabPath)),"custom shape rejected before prefab writes");
                }
                finally{File.WriteAllBytes(MeadowRabbitSetup.MeshPath,meshBytes);AssetDatabase.ImportAsset(MeadowRabbitSetup.MeshPath,ImportAssetOptions.ForceSynchronousImport);}
            }
            MeadowCompositionSetup.Refine();terrain=AssetDatabase.LoadAssetAtPath<GameObject>(TerrainSetup.PrefabPath).GetComponent<IslandTerrain>();var owner=terrain.World.GetComponentInChildren<WorldWaterEcology>(true);
            Check(owner.IsValid&&owner.RabbitHabitats.Length==4,"four valid composed habitats");
            var preview=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);
            try
            {
                var baked=preview.GetComponent<IslandTerrain>();var flowers=MeadowCompositionSetup.Flowers(baked);var obstacles=MeadowRabbitSetup.Obstacles(baked);
                foreach(var h in owner.RabbitHabitats)
                {
                    var center=h.Track[h.Track.Length/2];var d=h.Track.Last()-h.Track[0];var p=new Vector2(center.x,center.z);var direction=new Vector2(d.x,d.z).normalized;
                    Check(flowers.Min(f=>Vector2.Distance(f,p))<=5.6f,"rabbit and butterflies share normal camera view");
                    Check(MeadowRabbitSetup.SafeTrack(baked,p,direction,obstacles,flowers,1.7f)!=null,"complete body/ground/flowers/construction clearance");
                }
            }
            finally{PrefabUtility.UnloadPrefabContents(preview);}
            Check(colliders.SequenceEqual(terrain.GetComponentsInChildren<Collider>(true).Select(c=>EditorJsonUtility.ToJson(c))),"all original colliders preserved");
            var authored=Changed.Select(File.ReadAllBytes).ToArray();MeadowCompositionSetup.Refine();MeadowRabbitSetup.Rebuild();PrototypeAddressablesSetup.EnsureSetup();
            for(int i=0;i<Changed.Length;i++)Check(authored[i].SequenceEqual(File.ReadAllBytes(Changed[i])),"idempotent/refine/ordinary Setup retains composition");
            try
            {
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(MeadowRabbitSetup.MeshPath);var v=mesh.vertices;v[0].y+=.01f;mesh.vertices=v;EditorUtility.SetDirty(mesh);AssetDatabase.SaveAssets();var painted=Changed.Select(File.ReadAllBytes).ToArray();
                MeadowCompositionSetup.Refine();PrototypeAddressablesSetup.EnsureSetup();for(int i=0;i<Changed.Length;i++)Check(painted[i].SequenceEqual(File.ReadAllBytes(Changed[i])),"later manual edits retained");
            }
            finally{for(int i=0;i<Changed.Length;i++){File.WriteAllBytes(Changed[i],authored[i]);if(!Changed[i].EndsWith(".meta"))AssetDatabase.ImportAsset(Changed[i],ImportAssetOptions.ForceSynchronousImport);}}
            for(int i=0;i<files.Length;i++)Check(bytes[i].SequenceEqual(File.ReadAllBytes(files[i])),"unrelated art retained "+files[i]);
            Directory.CreateDirectory("Logs/MeadowPolish");File.WriteAllText("Logs/MeadowPolish/Habitats.json",JsonUtility.ToJson(owner,true));
            Debug.Log("Meadow polish bake verification PASSED: ear/full animation envelopes, four flower-adjacent safe strips, old art/colliders/materials/GUIDs, custom edit rejection and idempotent/manual preservation.");
        }
        static Vector4[] Data(Renderer r,string key){var b=new MaterialPropertyBlock();r.GetPropertyBlock(b);return b.GetVectorArray(key)??Array.Empty<Vector4>();}
        static void Shot(Camera camera,string name)
        {
            var target=camera.targetTexture;var active=RenderTexture.active;var rt=new RenderTexture(1280,720,24);var tex=new Texture2D(1280,720,TextureFormat.RGB24,false);
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1280,720),0,0);tex.Apply();File.WriteAllBytes("Logs/MeadowPolish/"+name+".png",tex.EncodeToPNG());}
            finally{camera.targetTexture=target;RenderTexture.active=active;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(tex);}
        }
        public static void VerifyRuntime(GameManager game)
        {
            var s=game.Ocean.Surface;var e=s.WorldEcology;var r=e.MeadowRabbits;e.Bind(s.FishAnimation);var block=new MaterialPropertyBlock();r.GetPropertyBlock(block);
            Check(block.GetFloatArray("_RabbitVisible").Take(4).All(v=>v==1),"all four rabbits visible in actual world");
            float time=0;Action<float> tick=dt=>{time+=dt;e.Tick(time,dt,1,false,0,null,null,false,true);};
            var hops=new List<float>();var rests=new List<float>();var previous=Data(r,"_RabbitPose");bool lastHop=false,earDifference=false,grazed=false,watched=false;float rest=0;Vector3 hopStart=Vector3.zero;
            for(int frame=0;frame<1800;frame++)
            {
                tick(1f/30);var pose=Data(r,"_RabbitPose");var a=Data(r,"_RabbitActions");var ears=Data(r,"_RabbitEars");
                for(int i=0;i<4;i++){Check(ears[i].x>=0&&ears[i].x<=.0401f&&ears[i].y>=0&&ears[i].y<=.0401f,"bounded ears");earDifference|=Mathf.Abs(ears[i].x-ears[i].y)>.02f;}
                bool hop=a[0].x>.005f;if(hop&&!lastHop){hopStart=previous[0];if(rest>.5f)rests.Add(rest);rest=0;}
                if(!hop&&lastHop)hops.Add(Vector3.Distance(hopStart,(Vector3)pose[0]));if(!hop)rest+=1f/30;
                grazed|=a[0].y>.4f;watched|=Mathf.Abs(a[0].z)>.07f;lastHop=hop;previous=pose;
            }
            Check(hops.Count>=4&&hops.Max()-hops.Min()>.08f,"varied natural hop lengths");Check(rests.Count>=4&&rests.Max()-rests.Min()>.8f,"varied rest durations");Check(earDifference&&grazed&&watched,"asymmetric ear flicks, forage and observation");
            var before=Data(r,"_RabbitEars");tick(0);Check(before.SequenceEqual(Data(r,"_RabbitEars")),"ears frozen at zero seconds");e.Tick(time+10,10,1,true,0,null,null,false,false);Check(before.SequenceEqual(Data(r,"_RabbitEars")),"ears stop indoors");tick(0);
            var camera=game.Players.Player.ViewCamera;var controller=camera.GetComponent<MagicIsland.CameraSystem.CameraController>();var cameraPosition=camera.transform.position;float size=camera.orthographicSize;bool follow=controller.enabled;
            var player=game.Players.Player.transform.position;var canvases=Object.FindObjectsOfType<Canvas>();var enabled=canvases.Select(c=>c.enabled).ToArray();double minute=game.Time.Clock.Minute;var weather=game.Weather.State.Kind;
            try
            {
                Directory.CreateDirectory("Logs/MeadowPolish");typeof(WeatherState).GetProperty("Kind").SetValue(game.Weather.State,WeatherKind.Sunny);AtmospherePolishVerification.Advance(game.Weather,40);LightingFlowVerification.SetTime(game,750);
                controller.enabled=false;foreach(var c in canvases)c.enabled=false;e.Bind(s.FishAnimation);time=0;tick(1.2f);
                for(int i=0;i<4;i++)
                {
                    var track=e.RabbitHabitats[i].Track;var home=e.transform.TransformPoint(track[track.Length/2]);
                    // Keep the actual gameplay size/pitch. Put player on the existing route center.
                    var v=new Vector2(home.x,home.z);var d=(track.Last()-track[0]).normalized;var side=new Vector3(-d.z,0,d.x);
                    var a=home+side*4.1f;var b=home-side*4.1f;var road=MagicIsland.Interaction.StarterIslandLayout.RouteDistance(a.x,a.z)<MagicIsland.Interaction.StarterIslandLayout.RouteDistance(b.x,b.z)?a:b;
                    road.y=(float)MagicIsland.Interaction.StarterIslandLayout.Ground(road.x,road.z)+.05f;game.Players.RestorePosition(road);controller.SnapToTarget();
                    var screen=camera.WorldToViewportPoint(home+Vector3.up*.4f);Check(screen.z>0&&screen.x>.08f&&screen.x<.92f&&screen.y>.08f&&screen.y<.92f,"rabbit readable from actual path and gameplay camera");
                    Shot(camera,"Gameplay"+i);Check(camera.orthographicSize==size,"normal gameplay zoom retained");
                }
                var first=e.RabbitHabitats[0].Track;var focus=e.transform.TransformPoint(first[first.Length/2]);var direction=e.transform.TransformDirection((first.Last()-first[0]).normalized);
                camera.orthographicSize=2.8f;camera.transform.position=focus+Vector3.up*.5f-camera.transform.forward*20;e.Bind(s.FishAnimation);time=0;Directory.CreateDirectory("Logs/MeadowPolish/Sequence");
                for(int i=0;i<100;i++){float dt=.12f;time+=dt;e.Tick(time,dt,1,false,0,i>=38&&i<58?(Vector3?)(focus-direction*1.4f):null,null,false,true);Shot(camera,"Sequence/"+i.ToString("000"));}
                e.Clear();Check(Data(r,"_RabbitEars").Length==0,"ear property block cleanup");e.Bind(s.FishAnimation);Check(Data(r,"_RabbitEars").All(v=>v==Vector4.zero),"rebind resets ears");
            }
            finally{game.Players.RestorePosition(player);camera.transform.position=cameraPosition;camera.orthographicSize=size;controller.enabled=follow;for(int i=0;i<canvases.Length;i++)if(canvases[i]!=null)canvases[i].enabled=enabled[i];typeof(WeatherState).GetProperty("Kind").SetValue(game.Weather.State,weather);AtmospherePolishVerification.Advance(game.Weather,40);LightingFlowVerification.SetTime(game,minute);}
            Debug.Log("Meadow polish runtime verification PASSED: four visible rabbits, distinct bounded ears, varied hops/rests, forage/watch, exact pause/indoor and cleanup, four actual gameplay camera views and 100-frame render sequence.");
        }
    }
}
