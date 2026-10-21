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
    public static class GrasslandSampleFinishVerification
    {
        const string Folder="Logs/GrasslandSampleFinish",Baseline="Assets/Editor/GrasslandSampleFinishBaseline";
        static void Check(bool ok,string why){if(!ok)throw new InvalidOperationException("Sample finish verification: "+why);}
        static string Hash(string p){using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(p)));}
        static Vector4[] UV(Mesh m,int ch){var a=new List<Vector4>();m.GetUVs(ch,a);return a.ToArray();}
        public static void BakeAndVerify()
        {
            Check(Application.dataPath.Replace('\\','/').Contains("/Logs/"),"isolated project required");
            var root=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);GrasslandSampleFinishSetup.Edit[] plan;MeadowTouchSettings settings;
            try
            {
                var t=root.GetComponent<IslandTerrain>();settings=t.SeasonalDetails.TouchSample;plan=GrasslandSampleFinishSetup.Plan(t);
                var r=settings.Renderers[0];var material=r.sharedMaterial;var custom=new Material(Shader.Find("Standard"));
                try{r.sharedMaterial=custom;bool refused=false;try{GrasslandSampleFinishSetup.Plan(t);}catch(InvalidOperationException){refused=true;}Check(refused,"custom material refused");}
                finally{r.sharedMaterial=material;Object.DestroyImmediate(custom);}
                var importer=AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(plan[0].Mesh));string metadata=importer.userData;
                try
                {
                    importer.userData=metadata+";Artist.Custom";bool refused=false;
                    try{GrasslandSampleFinishSetup.Plan(t);}catch(InvalidOperationException){refused=true;}Check(refused,"unknown metadata refused");
                    foreach(string marker in new[]{GrasslandSampleFinishSetup.Pending,GrasslandSampleFinishSetup.Marker})
                    {
                        importer.userData=metadata+";"+marker;refused=false;
                        try{GrasslandSampleFinishSetup.Rebuild();}catch(InvalidOperationException){refused=true;}Check(refused,"pending/partial migration refused");
                    }
                }
                finally{importer.userData=metadata;}
                var m=plan[0].Mesh;var a=m.uv;
                try{m.SetUVs(0,a.Select(p=>new Vector4(p.x,p.y,.03f,0)).ToArray());bool refused=false;try{GrasslandSampleFinishSetup.Plan(t);}catch(InvalidOperationException){refused=true;}Check(refused,"custom UV dimension refused");}
                finally{m.SetUVs(0,a);}
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            var paths=plan.Select(e=>AssetDatabase.GetAssetPath(e.Mesh)).ToArray();
            var vertices=plan.Select(e=>e.Mesh.vertices).ToArray();var normals=plan.Select(e=>e.Mesh.normals).ToArray();
            var colors=plan.Select(e=>e.Mesh.colors).ToArray();var faces=plan.Select(e=>e.Mesh.triangles).ToArray();
            var channels=plan.Select(e=>Enumerable.Range(0,8).Select(ch=>UV(e.Mesh,ch)).ToArray()).ToArray();
            var bounds=plan.Select(e=>e.Mesh.bounds).ToArray();var guids=paths.Select(AssetDatabase.AssetPathToGUID).ToArray();
            var formats=plan.Select(e=>e.Mesh.indexFormat).ToArray();
            if(!AssetDatabase.IsValidFolder(Baseline))AssetDatabase.CreateFolder("Assets/Editor","GrasslandSampleFinishBaseline");
            foreach(string p in paths)
            {
                string target=Baseline+"/"+Path.GetFileName(p);
                if(File.Exists(target))
                {
                    var f=AssetDatabase.LoadAssetAtPath<Mesh>(target);var m=AssetDatabase.LoadAssetAtPath<Mesh>(p);
                    Check(f!=null&&f.vertices.SequenceEqual(m.vertices)&&f.normals.SequenceEqual(m.normals)&&f.colors.SequenceEqual(m.colors)
                        &&f.triangles.SequenceEqual(m.triangles)&&Enumerable.Range(0,8).All(ch=>UV(f,ch).SequenceEqual(UV(m,ch))),"exact original fixture");
                }
                else Check(AssetDatabase.CopyAsset(p,target),"original comparison fixture");
            }
            var excluded=new HashSet<string>(paths.SelectMany(p=>new[]{p,p+".meta"}));
            var protectedFiles=Directory.GetFiles("Assets/Art","*",SearchOption.AllDirectories).Select(p=>p.Replace('\\','/')).Where(p=>!excluded.Contains(p)).ToArray();
            var hashes=protectedFiles.Select(Hash).ToArray();GrasslandSampleFinishSetup.Rebuild();
            for(int e=0;e<plan.Length;e++)
            {
                var mesh=plan[e].Mesh;var paint=mesh.colors;var roots=mesh.uv2;var uv=mesh.uv;
                Check(AssetDatabase.AssetPathToGUID(paths[e])==guids[e]&&mesh.indexFormat==formats[e]&&mesh.bounds==bounds[e]
                    &&mesh.vertices.SequenceEqual(vertices[e])&&mesh.normals.SequenceEqual(normals[e])&&paint.SequenceEqual(plan[e].Colors)
                    &&mesh.triangles.SequenceEqual(plan[e].Indices),"planned colors/faces only; original geometry, bounds, GUID and format");
                for(int ch=0;ch<8;ch++)Check(UV(mesh,ch).SequenceEqual(channels[e][ch]),"all original UV channels including root ownership");
                var retained=new List<int>();
                for(int i=0;i<faces[e].Length;i+=3)
                {
                    bool cut=plan[e].Removed[faces[e][i]];
                    Check(cut==plan[e].Removed[faces[e][i+1]]&&cut==plan[e].Removed[faces[e][i+2]],"whole grass faces");
                    if(!cut){retained.Add(faces[e][i]);retained.Add(faces[e][i+1]);retained.Add(faces[e][i+2]);}
                }
                Check(mesh.triangles.SequenceEqual(retained),"retained face order");
                for(int i=0;i<paint.Length;i++)
                {
                    Check(paint[i].a==colors[e][i].a,"original alpha retained");
                    if(!plan[e].Changed[i])Check(paint[i]==colors[e][i],"outside and untouched paint retained");
                    else Check(Vector2.Distance(roots[i],settings.Center)<settings.AreaRadius
                        &&Vector2.Distance(new Vector2(vertices[e][i].x,vertices[e][i].z),settings.Center)<settings.AreaRadius,"sample-only palette");
                    if(plan[e].Removed[i])Check(uv[i].x>=-5&&uv[i].x<=-4&&Vector2.Distance(roots[i],settings.Center)<settings.AreaRadius-settings.Feather,"inner pure-grass thinning only");
                }
            }
            for(int i=0;i<protectedFiles.Length;i++)Check(Hash(protectedFiles[i])==hashes[i],"protected art "+protectedFiles[i]);
            string edited=paths.First();byte[] bytes=File.ReadAllBytes(edited);
            try
            {
                var m=plan[0].Mesh;var c=m.colors;c[0].r*=.99f;m.colors=c;EditorUtility.SetDirty(m);AssetDatabase.SaveAssets();
                var all=Directory.GetFiles("Assets/Art","*",SearchOption.AllDirectories);var manual=all.Select(Hash).ToArray();
                GrasslandSampleFinishSetup.Rebuild();GrasslandPathEdgeSetup.Rebuild();GrasslandEdgeSetup.Rebuild();
                GrasslandWildflowerSetup.Rebuild();GrasslandBlendSetup.Rebuild();GrasslandPatchSetup.Rebuild();
                GrasslandReadabilitySetup.Rebuild();GrasslandTouchSetup.Rebuild();PrototypeAddressablesSetup.EnsureSetup();
                for(int i=0;i<all.Length;i++)Check(Hash(all[i])==manual[i],"repeat / old tools / Setup preserve manual edits "+all[i]);
            }
            finally{File.WriteAllBytes(edited,bytes);AssetDatabase.ImportAsset(edited,ImportAssetOptions.ForceUpdate);}
            Directory.CreateDirectory(Folder);File.WriteAllText(Folder+"/Stats.json",JsonUtility.ToJson(new Stats{
                Meshes=plan.Length,Painted=plan.Sum(e=>e.Changed.Count(b=>b)),Thinned=plan.Sum(e=>e.Thinned)},true));
            Debug.Log("Grassland sample finish bake verification PASSED: bounded palette and whole-grass thinning; geometry/UV/GUID/bounds/alpha retained; protected art, custom/pending/partial refusal and manual Setup.");
        }
        [Serializable] sealed class Stats{public int Meshes,Painted,Thinned;}
        public static void After()=>GrasslandPathEdgeVerification.After();
        public static void Visit(GameManager g)
        {
            if(!g.Terrain.Terrain.SeasonalDetails.TouchSample.Renderers.All(r=>GrasslandSampleFinishSetup.Done(
                AssetImporter.GetAtPath(BiomeIslandsSetup.Folder+"/"+r.GetComponent<MeshFilter>().sharedMesh.name+".asset").userData)))return;
            GrasslandReadabilityVerification.Compare(g,Folder,Baseline,"sample finish");
            // Actual CharacterController movement, sampled at 25 fps; no interpolated or synthetic frames.
            var camera=g.Players.Player.ViewCamera;var position=g.Players.Player.transform.position;
            var cameraPos=camera.transform.position;var rotation=camera.transform.rotation;float size=camera.orthographicSize,far=camera.farClipPlane;
            float wind=Shader.GetGlobalFloat("_IslandWindTime"),strength=Shader.GetGlobalFloat("_IslandWindStrength");
            var cover=g.Terrain.Terrain.SeasonalDetails;
            try
            {
                Check(g.Terrain.TryResolve(new Vector3(-129.61f,0,447.51f),out var start),"walk actual start");
                g.Players.RestorePosition(start);g.Cameras.RestoreOccluders();cover.TickTouch(0,null,false,true);camera.farClipPlane=3200;
                var move=typeof(MagicIsland.Player.PlayerMovement).GetMethod("TickMovement",BindingFlags.Instance|BindingFlags.NonPublic);
                var windTick=typeof(WeatherManager).GetMethod("TickWind",BindingFlags.Instance|BindingFlags.NonPublic);
                for(int frame=0;frame<100;frame++)
                {
                    float direction=frame<50?1:-1;var before=g.Players.Player.transform.position;
                    move.Invoke(g.Players.Player,new object[]{Vector2.right*direction,.04f});
                    typeof(WeatherManager).GetMethod("TickMeadowTouch",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(g.Weather,new object[]{.04f,false});
                    windTick.Invoke(g.Weather,new object[]{.04f,false});
                    Check((g.Players.Player.transform.position.x-before.x)*direction>.001f,"actual unobstructed walk");
                    GrasslandTouchVerification.Frame(camera,g.Players.Player.transform.position);
                    GrasslandTouchVerification.Capture(camera,Folder+"/Walk"+frame.ToString("D3")+".png");
                }
                Check(!File.Exists(g.Saves.SavePath),"walk does not save progress");
                Debug.Log("Grassland sample finish walk verification PASSED: 100 actual CharacterController frames at 25 fps, shared touch/wind, no daytime save.");
            }
            finally
            {
                cover.TickTouch(0,null,false,true);g.Players.RestorePosition(position);camera.transform.SetPositionAndRotation(cameraPos,rotation);
                camera.orthographicSize=size;camera.farClipPlane=far;Shader.SetGlobalFloat("_IslandWindTime",wind);Shader.SetGlobalFloat("_IslandWindStrength",strength);
            }
        }
    }
}
