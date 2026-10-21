using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using MagicIsland.Managers;
using MagicIsland.TerrainSystem;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    public static class GrasslandPathEdgeVerification
    {
        const string Folder="Logs/GrasslandPathEdge",Baseline="Assets/Editor/GrasslandPathEdgeBaseline";
        static void Check(bool ok,string why){if(!ok)throw new InvalidOperationException("Path edge verification: "+why);}
        static string Hash(string p){using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(p)));}
        static Vector4[] UV(Mesh m,int ch){var a=new List<Vector4>();m.GetUVs(ch,a);return a.ToArray();}
        public static void BakeAndVerify()
        {
            Check(Application.dataPath.Replace('\\','/').Contains("/Logs/"),"isolated project required");
            string oldShader=File.ReadAllText(Folder+"/ShaderBefore.txt").Replace("Shader \"MagicIsland/Meadow Trail\"","Shader \"Hidden/MagicIsland/Path Edge Baseline\"")
                .Replace("#include \"CoastalWetSand.cginc\"","#include \"Assets/Art/Shaders/CoastalWetSand.cginc\"");
            if(!AssetDatabase.IsValidFolder(Baseline))AssetDatabase.CreateFolder("Assets/Editor","GrasslandPathEdgeBaseline");
            string shaderFixture=Baseline+"/MeadowTrailBefore.shader";
            if(File.Exists(shaderFixture))Check(File.ReadAllText(shaderFixture)==oldShader,"preserve original shader fixture");
            else File.WriteAllText(shaderFixture,oldShader);
            AssetDatabase.ImportAsset(shaderFixture,ImportAssetOptions.ForceUpdate);
            Check(!ShaderUtil.ShaderHasError(AssetDatabase.LoadAssetAtPath<Shader>(shaderFixture)),"original shader fixture compiles");
            var root=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);GrasslandPathEdgeSetup.Edit[] plan;MeadowTouchSettings settings;
            try
            {
                var t=root.GetComponent<IslandTerrain>();settings=t.SeasonalDetails.TouchSample;plan=GrasslandPathEdgeSetup.Plan(t);
                foreach(var f in GrasslandPathEdgeSetup.Filters(t).Take(2))
                {
                    var r=f.GetComponent<Renderer>();var original=r.sharedMaterial;var custom=new Material(Shader.Find("Standard"));
                    try{r.sharedMaterial=custom;bool refused=false;try{GrasslandPathEdgeSetup.Plan(t);}catch(InvalidOperationException){refused=true;}Check(refused,"custom road/grass material refused");}
                    finally{r.sharedMaterial=original;Object.DestroyImmediate(custom);}
                }
                var importer=AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(plan[0].Mesh));string metadata=importer.userData;
                try
                {
                    importer.userData=metadata+";Artist.Custom";bool refused=false;
                    try{GrasslandPathEdgeSetup.Plan(t);}catch(InvalidOperationException){refused=true;}Check(refused,"custom metadata refused before writes");
                    foreach(string marker in new[]{GrasslandPathEdgeSetup.Pending,GrasslandPathEdgeSetup.Marker})
                    {
                        importer.userData=metadata+";"+marker;refused=false;
                        try{GrasslandPathEdgeSetup.Rebuild();}catch(InvalidOperationException){refused=true;}Check(refused,"pending/partial migration refused before writes");
                    }
                }
                finally{importer.userData=metadata;}
                var mesh=plan[0].Mesh;var originalUV=mesh.uv;
                try
                {
                    mesh.SetUVs(0,originalUV.Select(p=>new Vector4(p.x,p.y,.03f,0)).ToArray());bool refused=false;
                    try{GrasslandPathEdgeSetup.Plan(t);}catch(InvalidOperationException){refused=true;}Check(refused,"custom UV dimensions refused before writes");
                }
                finally{mesh.SetUVs(0,originalUV);}
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            var paths=plan.Select(e=>AssetDatabase.GetAssetPath(e.Mesh)).ToArray();var guids=paths.Select(AssetDatabase.AssetPathToGUID).ToArray();
            var v=plan.Select(e=>e.Mesh.vertices).ToArray();var n=plan.Select(e=>e.Mesh.normals).ToArray();var colors=plan.Select(e=>e.Mesh.colors).ToArray();
            var faces=plan.Select(e=>e.Mesh.triangles).ToArray();var bounds=plan.Select(e=>e.Mesh.bounds).ToArray();var formats=plan.Select(e=>e.Mesh.indexFormat).ToArray();
            var channels=plan.Select(e=>Enumerable.Range(0,8).Select(ch=>UV(e.Mesh,ch)).ToArray()).ToArray();
            if(!AssetDatabase.IsValidFolder(Baseline))AssetDatabase.CreateFolder("Assets/Editor","GrasslandPathEdgeBaseline");
            foreach(string p in paths)
            {
                string target=Baseline+"/"+Path.GetFileName(p);
                if(File.Exists(target))
                {
                    var a=AssetDatabase.LoadAssetAtPath<Mesh>(p);var b=AssetDatabase.LoadAssetAtPath<Mesh>(target);
                    Check(b!=null&&a.vertices.SequenceEqual(b.vertices)&&a.normals.SequenceEqual(b.normals)&&a.colors.SequenceEqual(b.colors)
                        &&a.triangles.SequenceEqual(b.triangles)&&Enumerable.Range(0,8).All(ch=>UV(a,ch).SequenceEqual(UV(b,ch))),"preserve exact original fixture");
                }
                else Check(AssetDatabase.CopyAsset(p,target),"original comparison fixture");
            }
            var excluded=new HashSet<string>(paths.SelectMany(p=>new[]{p,p+".meta"}));
            var protectedFiles=Directory.GetFiles("Assets/Art","*",SearchOption.AllDirectories).Select(p=>p.Replace('\\','/')).Where(p=>!excluded.Contains(p)).ToArray();
            var hashes=protectedFiles.Select(Hash).ToArray();GrasslandPathEdgeSetup.Rebuild();
            for(int e=0;e<plan.Length;e++)
            {
                var m=plan[e].Mesh;var points=m.vertices;var normals=m.normals;var paint=m.colors;
                var uv=m.uv;var roots=m.uv2;var season=m.uv3;
                Check(AssetDatabase.AssetPathToGUID(paths[e])==guids[e]&&m.indexFormat==formats[e]&&points.SequenceEqual(plan[e].Vertices)
                    &&normals.SequenceEqual(plan[e].Normals)&&paint.SequenceEqual(plan[e].Colors)&&m.triangles.SequenceEqual(plan[e].Indices)&&m.bounds==plan[e].Bounds,"exact final plan and original GUID/index format");
                if(plan[e].Road)
                {
                    Check(points.SequenceEqual(v[e])&&normals.SequenceEqual(n[e])&&m.triangles.SequenceEqual(faces[e])&&m.bounds==bounds[e],"all road geometry/normals/faces/bounds retained");
                    Check(UV(m,0).SequenceEqual(channels[e][0]),"entire original road UV0 retained");
                    Check(UV(m,1).SequenceEqual(plan[e].Channels[1].Select(p=>new Vector4(p.x,p.y,0,0))),"exact baked continuous shoulder mask");
                    for(int ch=2;ch<8;ch++)Check(UV(m,ch).SequenceEqual(channels[e][ch]),"all other road UV channels retained");
                    for(int i=0;i<points.Length;i++)
                    {
                        Check(uv[i].y==channels[e][0][i].y&&paint[i].a==colors[e][i].a,"road snow and alpha retained");
                        Check(roots[i].x==channels[e][1][i].x,"road blend retained");
                        if(!plan[e].Changed[i])Check(paint[i]==colors[e][i]&&UVValue(roots[i])==channels[e][1][i],"road center/boundary/outside sample retained");
                        else Check(roots[i].y>=-914&&roots[i].y<-913,"only authored vertices opt in to continuous noise");
                    }
                    for(int i=0;i<faces[e].Length;i+=3)
                    {
                        var ids=new[]{faces[e][i],faces[e][i+1],faces[e][i+2]};if(!ids.Any(j=>plan[e].Changed[j]))continue;
                        Check(ids.All(j=>Vector2.Distance(new Vector2(points[j].x,points[j].z),settings.Center)<settings.AreaRadius-.75f
                            &&GrasslandIslandMeadowSetup.RouteDistance(new Vector2(points[j].x,points[j].z))>=GrasslandPathEdgeSetup.ClearCenter),"complete interpolation triangle preserves sample and central clear strip");
                    }
                }
                else
                {
                    Check(points.Take(v[e].Length).SequenceEqual(v[e])&&normals.Take(n[e].Length).SequenceEqual(n[e])
                        &&paint.Take(colors[e].Length).SequenceEqual(colors[e])&&m.triangles.Take(faces[e].Length).SequenceEqual(faces[e]),"all original grass/flowers/shrubs/rocks and face prefix retained");
                    for(int ch=0;ch<8;ch++)Check(UV(m,ch).Take(channels[e][ch].Length).SequenceEqual(channels[e][ch])
                        &&(ch<3||UV(m,ch).Length==0),"all original grass UV channels retained");
                    Check(GrasslandPathEdgeSetup.StaticRadius+settings.BendDistance+1.13f*settings.WaveAmplitude*1.6f<GrasslandPathEdgeSetup.MotionRadius,"complete worst-case shader wind plus contact envelope");
                    for(int i=v[e].Length;i<points.Length;i++)
                    {
                        Check(Vector2.Distance(new Vector2(points[i].x,points[i].z),roots[i])<=GrasslandPathEdgeSetup.StaticRadius
                            &&Vector2.Distance(roots[i],settings.Center)+GrasslandPathEdgeSetup.MotionRadius<settings.AreaRadius-1
                            &&GrasslandIslandMeadowSetup.RouteDistance(roots[i])-GrasslandPathEdgeSetup.MotionRadius>1.47f,"complete new plant envelope preserves clear path and sample");
                        Check(uv[i].x>=-5&&uv[i].x<=0&&season[i].y>=0&&season[i].y<=1,"known original species and season encoding");
                    }
                }
            }
            for(int i=0;i<protectedFiles.Length;i++)Check(Hash(protectedFiles[i])==hashes[i],"prefab, ground/map/collision, materials/shaders, ecology and other islands retained "+protectedFiles[i]);
            byte[] bytes=File.ReadAllBytes(paths[0]);
            try
            {
                var mesh=plan[0].Mesh;var c=mesh.colors;c[Array.FindIndex(plan[0].Changed,b=>b)].g*=.99f;mesh.colors=c;EditorUtility.SetDirty(mesh);AssetDatabase.SaveAssets();
                var all=Directory.GetFiles("Assets/Art","*",SearchOption.AllDirectories);var manual=all.Select(Hash).ToArray();
                GrasslandPathEdgeSetup.Rebuild();GrasslandEdgeSetup.Rebuild();GrasslandWildflowerSetup.Rebuild();GrasslandBlendSetup.Rebuild();
                GrasslandPatchSetup.Rebuild();GrasslandReadabilitySetup.Rebuild();GrasslandTouchSetup.Rebuild();GrasslandIslandMeadowSetup.Rebuild();PrototypeAddressablesSetup.EnsureSetup();
                for(int i=0;i<all.Length;i++)Check(Hash(all[i])==manual[i],"repeat/old entrances/normal Setup preserve manual art "+all[i]);
            }
            finally{File.WriteAllBytes(paths[0],bytes);AssetDatabase.ImportAsset(paths[0],ImportAssetOptions.ForceUpdate);}
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Folder+"/Stats.json",JsonUtility.ToJson(new Stats{RoadVertices=plan[0].Changed.Count(b=>b),Groups=plan.Sum(e=>e.Sites.Length),
                Blades=plan.Sum(e=>e.Sites.Sum(s=>s.Blades)),PebbleGroups=plan.Sum(e=>e.Sites.Count(s=>s.Pebbles)),
                AddedVertices=plan.Select((e,i)=>e.Vertices.Length-v[i].Length).Sum()},true));
            Debug.Log("Grassland path edge bake verification PASSED: exact local road colors/mask, original UV0 and blend, central and boundary triangles, all original geometry and grass prefixes retained; complete low shoulder envelopes; protected art, custom/pending/partial refusal, repeat/manual Setup.");
        }
        static Vector4 UVValue(Vector2 p)=>new Vector4(p.x,p.y,0,0);
        [Serializable] sealed class Stats { public int RoadVertices,Groups,Blades,PebbleGroups,AddedVertices; }
        public static void After()=>GrasslandEdgeVerification.After();
        public static void Visit(GameManager g)
        {
            var t=g.Terrain.Terrain;var road=GrasslandPathEdgeSetup.Road(t);var mesh=road.sharedMesh;
            string path=BiomeIslandsSetup.Folder+"/"+mesh.name+".asset";
            if(!GrasslandPathEdgeSetup.Done(AssetImporter.GetAtPath(path).userData))return;
            var authored=AssetDatabase.LoadAssetAtPath<Mesh>(path);var original=AssetDatabase.LoadAssetAtPath<Mesh>(Baseline+"/"+mesh.name+".asset");
            Check(original!=null&&mesh.vertices.SequenceEqual(authored.vertices)&&mesh.normals.SequenceEqual(authored.normals)
                &&mesh.colors.SequenceEqual(authored.colors)&&mesh.triangles.SequenceEqual(authored.triangles)
                &&Enumerable.Range(0,8).All(ch=>UV(mesh,ch).SequenceEqual(UV(authored,ch))),"actual AA contains final complete road data");
            var renderer=road.GetComponent<Renderer>();var material=renderer.sharedMaterial;
            var baselineMaterial=new Material(material){shader=AssetDatabase.LoadAssetAtPath<Shader>(Baseline+"/MeadowTrailBefore.shader")};int regression=0;
            try
            {
                // Every other road and the historical source must remain unmarked with the shared shader.
                foreach(var terrain in new[]{t,AssetDatabase.LoadAssetAtPath<GameObject>(BiomeIslandsSetup.Source).GetComponent<IslandTerrain>()})
                    foreach(var f in terrain.GetComponentsInChildren<MeshFilter>(true).Where(f=>f!=road&&f.GetComponent<Renderer>()?.sharedMaterial?.shader.name=="MagicIsland/Meadow Trail"))
                        Check(f.sharedMesh.uv2.All(p=>p.y>-913||p.y< -914),"no marker collision in other roads or historical source");
                GrasslandReadabilityVerification.Compare(g,Folder,Baseline,"path edge",true,before=>
                {
                    road.sharedMesh=before?original:mesh;renderer.sharedMaterial=before?baselineMaterial:material;
                    if(!before)return;
                    var camera=g.Players.Player.ViewCamera;int mask=camera.cullingMask,layer=road.gameObject.layer;
                    var flags=camera.clearFlags;var background=camera.backgroundColor;
                    try
                    {
                        road.gameObject.layer=30;camera.cullingMask=1<<30;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.gray;
                        var old=GrasslandTouchVerification.Capture(camera,Folder+"/ZeroMaskOld"+regression+".png");
                        renderer.sharedMaterial=material;
                        var current=GrasslandTouchVerification.Capture(camera,Folder+"/ZeroMaskNew"+regression+".png");
                        Check(GrasslandCrownWindVerification.StableFrame(old,current),"zero-mask GPU rendering retains original shader output");
                    }
                    finally{road.gameObject.layer=layer;camera.cullingMask=mask;camera.clearFlags=flags;camera.backgroundColor=background;}
                    regression++;renderer.sharedMaterial=baselineMaterial;
                });
                Check(regression==3,"day/dusk/rain zero-mask shader regression");
            }
            finally{road.sharedMesh=mesh;renderer.sharedMaterial=material;Object.DestroyImmediate(baselineMaterial);}
            Check(!File.Exists(g.Saves.SavePath),"no daytime progress save");
        }
    }
}
