using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using MagicIsland.Managers;
using MagicIsland.TerrainSystem;
using MagicIsland.WeatherSystem;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    public static class GrasslandFloraVerification
    {
        const string Folder="Logs/GrasslandFlora",Baseline="Assets/Editor/FloraBaseline";
        static void Check(bool ok,string why){if(!ok)throw new InvalidOperationException("Flora verification: "+why);}
        static string Hash(string path){using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path)));}
        static Vector4[] UV(Mesh m,int ch){var a=new List<Vector4>();m.GetUVs(ch,a);return a.ToArray();}
        public static void BakeAndVerify()
        {
            Check(Application.dataPath.Replace('\\','/').Contains("/Logs/"),"isolated validation project");
            var root=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);GrasslandFloraSetup.Edit[] plan;
            try{Physics.SyncTransforms();plan=GrasslandFloraSetup.Plan(root.GetComponent<IslandTerrain>());}finally{PrefabUtility.UnloadPrefabContents(root);}
            bool fixtures=Directory.Exists(Baseline);if(!fixtures)AssetDatabase.CreateFolder("Assets/Editor","FloraBaseline");
            var paths=plan.Select(e=>AssetDatabase.GetAssetPath(e.Mesh)).ToArray();
            var vertices=plan.Select(e=>e.Mesh.vertices).ToArray();var colors=plan.Select(e=>e.Mesh.colors).ToArray();var normals=plan.Select(e=>e.Mesh.normals).ToArray();
            var triangles=plan.Select(e=>e.Mesh.triangles).ToArray();var channels=plan.Select(e=>Enumerable.Range(0,8).Select(ch=>UV(e.Mesh,ch)).ToArray()).ToArray();var guids=paths.Select(AssetDatabase.AssetPathToGUID).ToArray();
            foreach(string path in paths)
            {
                string target=Baseline+"/"+Path.GetFileName(path);
                if(!fixtures)Check(AssetDatabase.CopyAsset(path,target),"original art fixture");
                else{var a=AssetDatabase.LoadAssetAtPath<Mesh>(path);var b=AssetDatabase.LoadAssetAtPath<Mesh>(target);Check(b!=null&&a.vertices.SequenceEqual(b.vertices)&&a.normals.SequenceEqual(b.normals)&&a.colors.SequenceEqual(b.colors)&&Enumerable.Range(0,8).All(ch=>UV(a,ch).SequenceEqual(UV(b,ch))),"retained original fixture matches fresh input");}
            }
            var exceptions=new HashSet<string>(paths.SelectMany(p=>new[]{p,p+".meta"}).Concat(new[]{TerrainSetup.PrefabPath}));
            var protect=Directory.GetFiles("Assets/Art","*",SearchOption.AllDirectories).Select(p=>p.Replace('\\','/')).Where(p=>!exceptions.Contains(p)).ToArray();var hashes=protect.Select(Hash).ToArray();
            var importer=AssetImporter.GetAtPath(paths[0]);string metadata=importer.userData;
            try
            {
                importer.userData="Artist.Custom";root=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);bool refused=false;
                try{GrasslandFloraSetup.Plan(root.GetComponent<IslandTerrain>());}catch(InvalidOperationException){refused=true;}finally{PrefabUtility.UnloadPrefabContents(root);}
                Check(refused,"foreign metadata refused before writes");
            }
            finally{importer.userData=metadata;}
            GrasslandFloraSetup.Rebuild();int changed=0;
            for(int p=0;p<paths.Length;p++)
            {
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(paths[p]);Check(AssetDatabase.AssetPathToGUID(paths[p])==guids[p]&&mesh.triangles.SequenceEqual(triangles[p]),"GUID and topology retained");
                Check(GrasslandFloraSetup.Finished(AssetImporter.GetAtPath(paths[p]).userData),"migration ownership");
                for(int ch=1;ch<8;ch++)Check(UV(mesh,ch).SequenceEqual(channels[p][ch]),"all root, season and unused UV channels retained");
                var v=mesh.vertices;var c=mesh.colors;var n=mesh.normals;var a=mesh.uv;var roots=mesh.uv2;
                for(int i=0;i<v.Length;i++)
                {
                    Check(a[i].y==plan[p].Wind[i].y,"original wind phases");
                    if(plan[p].Changed[i])
                    {changed++;Check(v[i]==plan[p].Vertices[i]&&c[i]==plan[p].Colors[i]&&a[i]==plan[p].Wind[i]&&n[i]==plan[p].Normals[i],"planned flora only");Check(n[i].sqrMagnitude>.99f&&Vector3.Dot(n[i],normals[p][i])>.9f,"authored double-sided shading direction retained");Check(Vector2.Distance(new Vector2(v[i].x,v[i].z),roots[i])<=1.181f,"whole-root cut envelope");}
                    else Check(v[i]==vertices[p][i]&&c[i]==colors[p][i]&&n[i]==normals[p][i]&&a[i]==new Vector2(channels[p][0][i].x,channels[p][0][i].y),"rocks, shrubs, litter and unselected art retained");
                }
            }
            for(int i=0;i<protect.Length;i++)Check(Hash(protect[i])==hashes[i],"protected ground/collision/other biomes/ecology/material "+protect[i]);
            // A later artist edit must survive both explicit re-entry and ordinary Setup.
            byte[] bytes=File.ReadAllBytes(paths[0]);
            try
            {
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(paths[0]);var c=mesh.colors;c[0].r*=.97f;mesh.colors=c;EditorUtility.SetDirty(mesh);AssetDatabase.SaveAssets();
                var files=Directory.GetFiles("Assets/Art","*",SearchOption.AllDirectories);var saved=files.Select(Hash).ToArray();
                GrasslandFloraSetup.Rebuild();PrototypeAddressablesSetup.EnsureSetup();for(int i=0;i<files.Length;i++)Check(saved[i]==Hash(files[i]),"repeat/ordinary Setup preserves later hand edits "+files[i]);
            }
            finally{File.WriteAllBytes(paths[0],bytes);AssetDatabase.ImportAsset(paths[0],ImportAssetOptions.ForceUpdate);}
            Debug.Log("Grassland flora bake verification PASSED: "+changed+" vertices, "+plan.Sum(e=>e.Flowers)+" flower clumps, "+plan.Sum(e=>e.Grass)+" grass clumps; GUID/topology/root channels/protected art/manual edits retained.");
        }
        public static void After(){BiomeIslandsVerification.Run();}
        public static void Visit(GameManager g)
        {
            var batches=g.Terrain.Terrain.SeasonalDetails.MeadowBatches;if(!batches.Any(r=>r.sharedMaterial.shader.name==GrasslandFloraSetup.ShaderName))return;
            Check(batches.All(r=>r.sharedMaterial.shader.name==GrasslandFloraSetup.ShaderName),"one authored cover material");
            var camera=g.Players.Player.ViewCamera;var pos=camera.transform.position;var rot=camera.transform.rotation;float size=camera.orthographicSize,far=camera.farClipPlane;
            var filters=batches.Select(r=>r.GetComponent<MeshFilter>()).ToArray();var originals=filters.Select(f=>f.sharedMesh).ToArray();var materials=batches.Select(r=>r.sharedMaterial).ToArray();
            var state=g.Weather.State;float rain=g.Weather.RainAmount,windTime=Shader.GetGlobalFloat("_IslandWindTime"),strength=Shader.GetGlobalFloat("_IslandWindStrength");
            var stateProperty=typeof(WeatherManager).GetProperty("State");var rainProperty=typeof(WeatherManager).GetProperty("RainAmount");
            try
            {
                camera.farClipPlane=3200;
                for(int i=0;i<filters.Length;i++)
                {
                    var authored=AssetDatabase.LoadAssetAtPath<Mesh>(BiomeIslandsSetup.Folder+"/"+originals[i].name+".asset");Check(authored!=null&&originals[i].vertices.SequenceEqual(authored.vertices)&&originals[i].uv.SequenceEqual(authored.uv),"real Packed mesh and species channels");
                }
                foreach(string light in new[]{"Day","Dusk","Rain"})
                {
                    bool wet=light=="Rain";stateProperty.SetValue(g.Weather,new WeatherState(state.Seed,0,state.Day,wet?WeatherKind.Rainy:WeatherKind.Sunny));rainProperty.SetValue(g.Weather,wet?1f:0f);
                    g.Weather.Tick(light=="Dusk"?1100:720,true);g.Ocean.Surface.Tick(0,light=="Dusk"?.4f:1,wet,g.Weather.Sun);Shader.SetGlobalFloat("_IslandWindTime",12);Shader.SetGlobalFloat("_IslandWindStrength",wet?1.1f:.65f);
                    bool fixtures=Directory.Exists(Baseline)&&originals.All(m=>File.Exists(Baseline+"/"+m.name+".asset"));
                    if(fixtures)
                    {
                        for(int i=0;i<filters.Length;i++){filters[i].sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Baseline+"/"+originals[i].name+".asset");Check(filters[i].sharedMesh!=null,"original comparison mesh");batches[i].sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(GrasslandRevitalizationSetup.MaterialFolder+"Living Meadow Cover.mat");}
                        Views(camera,Folder+"/Before/"+light);
                        for(int i=0;i<filters.Length;i++){filters[i].sharedMesh=originals[i];batches[i].sharedMaterial=materials[i];}
                    }
                    Views(camera,Folder+"/After/"+light);
                }
                g.Weather.Tick(720,true);Shader.SetGlobalFloat("_IslandWindStrength",1);Shader.SetGlobalFloat("_IslandWindTime",2);
                Frame(camera,new Vector3(-176,13,570));var a=Capture(camera,Folder+"/WindA.png");Shader.SetGlobalFloat("_IslandWindTime",5);var b=Capture(camera,Folder+"/WindB.png");
                Check(!a.SequenceEqual(b),"actual GPU species wind animation");typeof(WeatherManager).GetMethod("TickWind",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(g.Weather,new object[]{3f,true});
                float frozen=Shader.GetGlobalFloat("_IslandWindTime");typeof(WeatherManager).GetMethod("TickWind",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(g.Weather,new object[]{3f,true});Check(Shader.GetGlobalFloat("_IslandWindTime")==frozen,"manager pause freezes shared wind clock");
                var c=Capture(camera,Folder+"/PausedA.png");typeof(WeatherManager).GetMethod("TickWind",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(g.Weather,new object[]{3f,true});var d=Capture(camera,Folder+"/PausedB.png");Check(c.SequenceEqual(d),"actual paused GPU frame stable");
                Check(!File.Exists(g.Saves.SavePath),"flora remains cosmetic / no daytime save");Debug.Log("Grassland flora Packed verification PASSED: authored mesh/species channels, default-camera day/dusk/rain comparisons, actual GPU wind and paused frame, no daytime save.");
            }
            finally
            {
                for(int i=0;i<filters.Length;i++){filters[i].sharedMesh=originals[i];batches[i].sharedMaterial=materials[i];}
                stateProperty.SetValue(g.Weather,state);rainProperty.SetValue(g.Weather,rain);g.Weather.Tick(720,true);g.Ocean.Surface.Tick(0,1,false,g.Weather.Sun);
                Shader.SetGlobalFloat("_IslandWindTime",windTime);Shader.SetGlobalFloat("_IslandWindStrength",strength);camera.transform.SetPositionAndRotation(pos,rot);camera.orthographicSize=size;camera.farClipPlane=far;
            }
        }
        static void Views(Camera camera,string folder)
        {
            Directory.CreateDirectory(folder);Frame(camera,new Vector3(-176,13,570));Capture(camera,folder+"/WhiteSlope.png");Frame(camera,new Vector3(-79,13,548));Capture(camera,folder+"/LilacSlope.png");Frame(camera,new Vector3(-87,13,414));Capture(camera,folder+"/YellowSlope.png");Frame(camera,new Vector3(-126,13,532));Capture(camera,folder+"/Grove.png");
        }
        static void Frame(Camera camera,Vector3 focus){camera.transform.rotation=Quaternion.Euler(42,0,0);camera.transform.position=focus-camera.transform.forward*18;camera.orthographicSize=7;}
        static byte[] Capture(Camera camera,string path)
        {
            var rt=new RenderTexture(1280,800,24);var old=camera.targetTexture;var active=RenderTexture.active;var image=new Texture2D(1280,800,TextureFormat.RGB24,false);
            try{camera.targetTexture=rt;camera.Render();camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1280,800),0,0);image.Apply();var bytes=image.EncodeToPNG();Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllBytes(path,bytes);return bytes;}
            finally{camera.targetTexture=old;RenderTexture.active=active;Object.DestroyImmediate(rt);Object.DestroyImmediate(image);}
        }
    }
}
