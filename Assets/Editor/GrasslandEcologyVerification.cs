using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using MagicIsland.Interaction;
using MagicIsland.Managers;
using MagicIsland.Ocean;
using MagicIsland.TerrainSystem;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    public static class GrasslandEcologyVerification
    {
        static void Check(bool ok,string why){if(!ok)throw new InvalidOperationException("Grassland ecology verification: "+why);}
        public static void StageWeather(WorldWaterEcology ecology,bool rain)=>typeof(WorldWaterEcology).GetField("meadowRain",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(ecology,rain?1f:0f);
        [Serializable] public sealed class Report{public string[] editedMeshes,newMeshes;public int habitats,butterflies,fireflies,foliageBatches,insectBatches;}
        static Dictionary<Mesh,Vector2[]> WindPlan()
        {
            var root=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);
            try{return GrasslandEcologySetup.WindPlan(root.GetComponent<IslandTerrain>());}
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        public static void BakeAndVerify()
        {
            Check(Application.dataPath.Replace('\\','/').Contains("/Logs/"),"isolated project only");
            var terrain=AssetDatabase.LoadAssetAtPath<GameObject>(TerrainSetup.PrefabPath).GetComponent<IslandTerrain>();var plan=WindPlan();
            var paths=plan.Keys.Select(AssetDatabase.GetAssetPath).ToArray();var vertices=plan.Keys.Select(m=>m.vertices).ToArray();var normals=plan.Keys.Select(m=>m.normals).ToArray();var colors=plan.Keys.Select(m=>m.colors).ToArray();var indices=plan.Keys.Select(m=>m.triangles).ToArray();
            var oldUV=plan.Keys.Select(m=>Enumerable.Range(1,7).Select(channel=>{var uv=new List<Vector4>();m.GetUVs(channel,uv);return uv.ToArray();}).ToArray()).ToArray();
            var colliders=terrain.GetComponentsInChildren<Collider>(true).Select(c=>EditorJsonUtility.ToJson(c)).ToArray();
            var protectedFiles=Directory.GetFiles("Assets/Art","*",SearchOption.AllDirectories).Select(p=>p.Replace('\\','/')).Where(p=>p!=TerrainSetup.PrefabPath&&!paths.Contains(p)).ToArray();var protectedBytes=protectedFiles.Select(File.ReadAllBytes).ToArray();
            var first=plan.Keys.First();try{first.uv=Enumerable.Repeat(Vector2.one,first.vertexCount).ToArray();bool rejected=false;try{WindPlan();}catch(InvalidOperationException){rejected=true;}Check(rejected,"custom UV0 rejected before writes");}finally{first.uv=Array.Empty<Vector2>();}
            GrasslandEcologySetup.Rebuild();terrain=AssetDatabase.LoadAssetAtPath<GameObject>(TerrainSetup.PrefabPath).GetComponent<IslandTerrain>();
            Check(colliders.SequenceEqual(terrain.GetComponentsInChildren<Collider>(true).Select(c=>EditorJsonUtility.ToJson(c))),"complete original collision set");
            for(int i=0;i<paths.Length;i++)
            {
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(paths[i]);Check(vertices[i].SequenceEqual(mesh.vertices)&&normals[i].SequenceEqual(mesh.normals)&&colors[i].SequenceEqual(mesh.colors)&&indices[i].SequenceEqual(mesh.triangles),"original foliage geometry/color/topology");
                for(int channel=1;channel<8;channel++){var uv=new List<Vector4>();mesh.GetUVs(channel,uv);Check(oldUV[i][channel-1].SequenceEqual(uv),"authored UV1-7 retained");}
                var points=mesh.vertices;var weights=mesh.uv;var homes=GrasslandCompositionSetup.Sites().Concat(GrasslandFinishSetup.Sites().Select(p=>p.Point)).ToArray();
                for(int j=0;j<points.Length;j++)
                {
                    var p=new Vector2(points[j].x,points[j].z);var home=GrasslandEcologySetup.Home(p,homes);float amplitude=.096f*weights[j].x*weights[j].x;
                    if(weights[j].x>0)Check(Vector2.Distance(p,home)+amplitude<1.05f,"complete wind envelope inside reserved footprint");
                    if(colors[i][j]==new Color(.66f,.69f,.56f)||colors[i][j]==new Color(.48f,.61f,.32f))Check(weights[j].x==0,"stone and moss fixed");
                }
            }
            var ecology=terrain.World.GetComponentInChildren<WorldWaterEcology>(true);var group=ecology.transform.Find(GrasslandEcologySetup.Group);Check(ecology.IsValid&&group!=null,"baked unified owner");
            Check(group.GetComponentsInChildren<Collider>(true).Length==0&&group.GetComponentsInChildren<MonoBehaviour>(true).Length==0&&group.GetComponentsInChildren<Light>(true).Length==0,"visual batches without per-insect objects/scripts/lights");
            var filters=group.GetComponentsInChildren<MeshFilter>(true);var newPaths=filters.Select(f=>AssetDatabase.GetAssetPath(f.sharedMesh)).ToArray();
            foreach(var f in filters)
            {
                var mesh=f.sharedMesh;Check(mesh.vertexCount>0&&mesh.normals.All(n=>n.sqrMagnitude>.9f),"valid source normals");Check(!ShaderUtil.ShaderHasError(f.GetComponent<Renderer>().sharedMaterial.shader),"insect shader compiles");
                var p=mesh.vertices;var centers=mesh.uv;var height=mesh.uv2;
                foreach(var center in centers.Distinct())Check(BiomePolishSetup.Clear(center,1.05f),"whole airborne habitat above existing reserved shoulder");
                for(int i=0;i<p.Length;i++)
                {
                    var center=new Vector3(centers[i].x,height[i].x,centers[i].y);Check(Vector3.Distance(p[i],center)<.32f,"bounded insect shape");
                    // Maximum planar rotation + wing fold + trajectory stays in the original square.
                    Check(Vector3.Distance(p[i],center)+new Vector2(.44f,.38f).magnitude<1.05f,"all animated insect positions bounded");
                }
            }
            var materials=new[]{"Meadow Butterflies","Meadow Fireflies","Meadow Foliage"}.Select(n=>GrasslandEcologySetup.MaterialFolder+n+".mat").ToArray();
            var watched=paths.Concat(newPaths).Concat(materials).Concat(new[]{TerrainSetup.PrefabPath}).ToArray();var bytes=watched.Select(File.ReadAllBytes).ToArray();GrasslandEcologySetup.Rebuild();PrototypeAddressablesSetup.EnsureSetup();
            for(int i=0;i<watched.Length;i++)Check(bytes[i].SequenceEqual(File.ReadAllBytes(watched[i])),"repeat/Setup retains authored assets");
            try
            {
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(newPaths[0]);var c=mesh.colors;c[0]=Color.magenta;mesh.colors=c;var v=mesh.vertices;v[0].y+=.01f;mesh.vertices=v;EditorUtility.SetDirty(mesh);
                var material=AssetDatabase.LoadAssetAtPath<Material>(materials[0]);material.SetFloat("_EcologyMode",.05f);EditorUtility.SetDirty(material);AssetDatabase.SaveAssets();var manual=watched.Select(File.ReadAllBytes).ToArray();GrasslandEcologySetup.Rebuild();PrototypeAddressablesSetup.EnsureSetup();
                for(int i=0;i<watched.Length;i++)Check(manual[i].SequenceEqual(File.ReadAllBytes(watched[i])),"manual paint, shape and material retained");
            }
            finally{for(int i=0;i<watched.Length;i++){File.WriteAllBytes(watched[i],bytes[i]);AssetDatabase.ImportAsset(watched[i],ImportAssetOptions.ForceSynchronousImport);}}
            for(int i=0;i<protectedFiles.Length;i++)Check(protectedBytes[i].SequenceEqual(File.ReadAllBytes(protectedFiles[i])),"unrelated art retained "+protectedFiles[i]);
            var report=new Report{editedMeshes=paths,newMeshes=newPaths,habitats=GrasslandEcologySetup.Sites().Length,butterflies=GrasslandEcologySetup.Sites().Length*2,fireflies=GrasslandEcologySetup.Sites().Length*4,foliageBatches=paths.Length,insectBatches=newPaths.Length};
            Directory.CreateDirectory("Logs/GrasslandEcology");File.WriteAllText("Logs/GrasslandEcology/Bake.json",JsonUtility.ToJson(report,true));Debug.Log("Grassland ecology bake verification PASSED: original geometry, colliders, protected art, complete animation footprints, custom UV rejection and repeat/Setup/manual preservation.");
        }
        static byte[] Shot(Camera camera,string file)
        {
            var active=RenderTexture.active;var target=camera.targetTexture;var rt=new RenderTexture(640,360,24);var texture=new Texture2D(640,360,TextureFormat.RGB24,false);
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,640,360),0,0);texture.Apply();var data=texture.GetRawTextureData<byte>().ToArray();File.WriteAllBytes(file,texture.EncodeToPNG());return data;}
            finally{camera.targetTexture=target;RenderTexture.active=active;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(texture);}
        }
        public static void VerifyRuntime(GameManager game)
        {
            GrasslandFinishVerification.VerifyRuntime(game);var surface=game.Ocean.Surface;var ecology=surface.WorldEcology;Check(ecology.Bound&&ecology.GrasslandAmbient.Length>12,"Packed grassland bound before input");
            var group=ecology.transform.Find(GrasslandEcologySetup.Group);Check(group!=null&&group.gameObject.activeInHierarchy,"Packed habitat loaded");var renderers=group.GetComponentsInChildren<Renderer>(true);
            foreach(var f in group.GetComponentsInChildren<MeshFilter>(true))
            {
                var native=AssetDatabase.LoadAssetAtPath<Mesh>(GrasslandEcologySetup.Folder+"/"+f.name+".asset");Func<Mesh,Vector3[]> points=m=>m.vertices.OrderBy(p=>p.x).ThenBy(p=>p.y).ThenBy(p=>p.z).ToArray();Check(points(native).SequenceEqual(points(f.sharedMesh)),"Packed insect geometry");
            }
            var random=JsonUtility.ToJson(UnityEngine.Random.state);var transforms=ecology.GrasslandAmbient.Select(r=>r.transform.localToWorldMatrix).ToArray();var materials=ecology.GrasslandAmbient.Select(r=>EditorJsonUtility.ToJson(r.sharedMaterial)).ToArray();
            var camera=game.Players.Player.ViewCamera;var position=camera.transform.position;float size=camera.orthographicSize;var background=camera.backgroundColor;var clear=camera.clearFlags;
            float windTime=Shader.GetGlobalFloat("_IslandWindTime"),windStrength=Shader.GetGlobalFloat("_IslandWindStrength");var windDirection=Shader.GetGlobalVector("_IslandWindDirection");
            var all=Object.FindObjectsOfType<Renderer>();var enabled=all.Select(r=>r.enabled).ToArray();var canvases=Object.FindObjectsOfType<Canvas>();var canvasEnabled=canvases.Select(c=>c.enabled).ToArray();var controller=camera.GetComponent<MagicIsland.CameraSystem.CameraController>();bool follow=controller.enabled;
            Directory.CreateDirectory("Logs/GrasslandEcology/Motion");
            try
            {
                controller.enabled=false;foreach(var canvas in canvases)canvas.enabled=false;foreach(var r in all)r.enabled=false;
                foreach(var r in renderers.Where(r=>r.name.Contains("Butterfly")))r.enabled=true;
                var p=GrasslandEcologySetup.Sites()[1];camera.orthographicSize=1.7f;camera.backgroundColor=new Color(.15f,.19f,.18f);camera.clearFlags=CameraClearFlags.SolidColor;
                camera.transform.position=new Vector3(p.x,GrasslandEcologySetup.Ground(game.Terrain.Terrain,p)+.85f,p.y)-camera.transform.forward*20;
                StageWeather(ecology,false);surface.Tick(.12f,1,false,game.Weather.Sun);var a=Shot(camera,"Logs/GrasslandEcology/Motion/DayA.png");surface.Tick(.17f,1,false,game.Weather.Sun);var b=Shot(camera,"Logs/GrasslandEcology/Motion/DayB.png");Check(!a.SequenceEqual(b),"actual GPU butterfly flight/wing motion");
                surface.Tick(0,1,false,game.Weather.Sun);var paused=Shot(camera,"Logs/GrasslandEcology/Motion/Paused.png");Check(b.SequenceEqual(paused),"actual GPU pause frame exact");
                var block=new MaterialPropertyBlock();ecology.GrasslandAmbient[0].GetPropertyBlock(block);float rain=block.GetFloat("_MeadowRain");surface.Tick(0,1,true,game.Weather.Sun);ecology.GrasslandAmbient[0].GetPropertyBlock(block);Check(block.GetFloat("_MeadowRain")==rain,"rain transition frozen while paused");
                surface.Tick(.2f,1,true,game.Weather.Sun);ecology.GrasslandAmbient[0].GetPropertyBlock(block);Check(block.GetFloat("_MeadowRain")>rain&&block.GetFloat("_MeadowRain")<1,"gradual rain shelter");
                surface.Tick(3,1,true,game.Weather.Sun);var wet=Shot(camera,"Logs/GrasslandEcology/Motion/RainHidden.png");Check(!wet.SequenceEqual(b),"rain conceals butterflies");
                StageWeather(ecology,false);surface.Tick(0,0,false,game.Weather.Sun);var hidden=Shot(camera,"Logs/GrasslandEcology/Motion/DayHiddenAtNight.png");
                Check(wet.SequenceEqual(hidden),"day butterflies fully hidden at night/rain");foreach(var r in renderers)r.enabled=r.name.Contains("Firefly");var night=Shot(camera,"Logs/GrasslandEcology/Motion/Night.png");Check(!hidden.SequenceEqual(night),"night fireflies actually visible");
                surface.Tick(.35f,0,false,game.Weather.Sun);Check(!night.SequenceEqual(Shot(camera,"Logs/GrasslandEcology/Motion/NightB.png")),"independent firefly drift and pulses");
                foreach(var r in renderers)r.enabled=false;foreach(var r in ecology.GrasslandAmbient.Where(r=>r.sharedMaterial.shader.name=="MagicIsland/Meadow Foliage"))r.enabled=true;
                Shader.SetGlobalFloat("_IslandWindStrength",.8f);Shader.SetGlobalVector("_IslandWindDirection",new Vector4(1,0,0,0));Shader.SetGlobalFloat("_IslandWindTime",28);
                var windA=Shot(camera,"Logs/GrasslandEcology/Motion/WindA.png");Shader.SetGlobalFloat("_IslandWindTime",30);Check(!windA.SequenceEqual(Shot(camera,"Logs/GrasslandEcology/Motion/WindB.png")),"actual GPU rooted flower/leaf wind");
                ecology.Tick(surface.AnimationTime,0,0,false,0,null,null,false,false);Check(!group.gameObject.activeInHierarchy,"indoor hides habitats");ecology.Tick(surface.AnimationTime,0,0,false,0,null,null,false,true);
                for(int i=0;i<ecology.GrasslandAmbient.Length;i++)Check(transforms[i]==ecology.GrasslandAmbient[i].transform.localToWorldMatrix&&materials[i]==EditorJsonUtility.ToJson(ecology.GrasslandAmbient[i].sharedMaterial),"static transforms/shared materials");
                Check(random==JsonUtility.ToJson(UnityEngine.Random.state),"no gameplay RNG consumed");
                ecology.Clear();var sentinel=new MaterialPropertyBlock();sentinel.SetFloat("_OwnerSentinel",.73f);ecology.GrasslandAmbient[0].SetPropertyBlock(sentinel);ecology.Bind(surface.FishAnimation);surface.Tick(.1f,1,false);ecology.Clear();ecology.GrasslandAmbient[0].GetPropertyBlock(sentinel);Check(sentinel.GetFloat("_OwnerSentinel")==.73f&&sentinel.GetFloat("_OceanTime")==0,"Clear restores original property block");ecology.GrasslandAmbient[0].SetPropertyBlock(null);ecology.Bind(surface.FishAnimation);
            }
            finally
            {
                for(int i=0;i<all.Length;i++)if(all[i]!=null)all[i].enabled=enabled[i];for(int i=0;i<canvases.Length;i++)if(canvases[i]!=null)canvases[i].enabled=canvasEnabled[i];
                controller.enabled=follow;camera.transform.position=position;camera.orthographicSize=size;camera.backgroundColor=background;camera.clearFlags=clear;
                Shader.SetGlobalFloat("_IslandWindTime",windTime);Shader.SetGlobalFloat("_IslandWindStrength",windStrength);Shader.SetGlobalVector("_IslandWindDirection",windDirection);
            }
            Debug.Log("Grassland ecology runtime verification PASSED: Packed models, GPU flight/wing motion, night fireflies, gradual rain, exact paused frame, indoor hiding, property cleanup and gameplay RNG preservation.");
        }
    }
}
