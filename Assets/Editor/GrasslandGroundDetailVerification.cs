using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using MagicIsland.Managers;
using MagicIsland.TerrainSystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    public static class GrasslandGroundDetailVerification
    {
        const string Folder="Logs/GrasslandGroundDetail",Baseline="Assets/Editor/GrasslandGroundDetailBaseline";
        static void Check(bool ok,string why){if(!ok)throw new InvalidOperationException("Ground detail verification: "+why);}
        static string Hash(string p){using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(p)));}
        static Vector4[] UV(Mesh m,int ch){var list=new List<Vector4>();m.GetUVs(ch,list);return list.ToArray();}
        static void WriteMap(Color32[] pixels)
        {
            var image=new Texture2D(512,512,TextureFormat.RGBA32,false,true);
            try{image.SetPixels32(pixels);image.Apply();File.WriteAllBytes(GrasslandGroundDetailSetup.MapPath,image.EncodeToPNG());}
            finally{Object.DestroyImmediate(image);}
            AssetDatabase.ImportAsset(GrasslandGroundDetailSetup.MapPath,ImportAssetOptions.ForceUpdate);
        }
        public static void BakeAndVerify()
        {
            Check(Application.dataPath.Replace('\\','/').Contains("/Logs/"),"isolated project required");Directory.CreateDirectory(Folder);
            if(!AssetDatabase.IsValidFolder(Baseline))AssetDatabase.CreateFolder("Assets/Editor","GrasslandGroundDetailBaseline");
            string shaderPath=Baseline+"/Before.shader";
            File.WriteAllText(shaderPath,File.ReadAllText(Folder+"/ShaderBefore.txt").Replace("Shader \"MagicIsland/Meadow Underlay\"","Shader \"Hidden/MagicIsland/Ground Detail Before\"")
                .Replace("\"SeasonalColor.cginc\"","\"Assets/Art/Shaders/SeasonalColor.cginc\"").Replace("\"CoastalWetSand.cginc\"","\"Assets/Art/Shaders/CoastalWetSand.cginc\""));
            AssetDatabase.ImportAsset(shaderPath,ImportAssetOptions.ForceUpdate);
            Check(!ShaderUtil.ShaderHasError(Shader.Find(GrasslandGroundLayersSetup.ShaderName))&&!ShaderUtil.ShaderHasError(AssetDatabase.LoadAssetAtPath<Shader>(shaderPath)),"original/current ground shaders compile");
            var root=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);GrasslandGroundDetailSetup.PlanData plan;MeadowTouchSettings settings;
            try
            {
                var t=root.GetComponent<IslandTerrain>();settings=t.SeasonalDetails.TouchSample;plan=GrasslandGroundDetailSetup.Plan(t);
                var r=GrasslandGroundLayersSetup.Grounds(t)[0];var original=r.sharedMaterial;var custom=new Material(Shader.Find("Standard"));
                try{r.sharedMaterial=custom;bool refused=false;try{GrasslandGroundDetailSetup.Plan(t);}catch(InvalidOperationException){refused=true;}Check(refused,"custom ground material refused");}
                finally{r.sharedMaterial=original;Object.DestroyImmediate(custom);}
                var imp=AssetImporter.GetAtPath(GrasslandGroundDetailSetup.MaterialPath);string data=imp.userData;
                try
                {
                    imp.userData=data+";Artist.Custom";bool refused=false;try{GrasslandGroundDetailSetup.Plan(t);}catch(InvalidOperationException){refused=true;}Check(refused,"custom material metadata refused");
                    foreach(string marker in new[]{GrasslandGroundDetailSetup.Pending,GrasslandGroundDetailSetup.Marker})
                    {imp.userData=data+";"+marker;refused=false;try{GrasslandGroundDetailSetup.Rebuild();}catch(InvalidOperationException){refused=true;}Check(refused,"pending/partial refusal");}
                }
                finally{imp.userData=data;}
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            var paths=plan.Meshes.Select(e=>AssetDatabase.GetAssetPath(e.Mesh)).ToArray();
            foreach(string path in paths)
            {
                string target=Baseline+"/"+Path.GetFileName(path);
                if(File.Exists(target))
                {
                    var a=AssetDatabase.LoadAssetAtPath<Mesh>(path);var b=AssetDatabase.LoadAssetAtPath<Mesh>(target);
                    Check(a.vertices.SequenceEqual(b.vertices)&&a.normals.SequenceEqual(b.normals)&&a.colors.SequenceEqual(b.colors)&&a.triangles.SequenceEqual(b.triangles)
                        &&a.bounds==b.bounds&&a.indexFormat==b.indexFormat&&Enumerable.Range(0,8).All(ch=>UV(a,ch).SequenceEqual(UV(b,ch))),"preserve custom comparison fixture");
                }
                else Check(AssetDatabase.CopyAsset(path,target),"original mesh fixture");
            }
            var vertices=plan.Meshes.Select(e=>e.Mesh.vertices).ToArray();var normals=plan.Meshes.Select(e=>e.Mesh.normals).ToArray();var colors=plan.Meshes.Select(e=>e.Mesh.colors).ToArray();
            var faces=plan.Meshes.Select(e=>e.Mesh.triangles).ToArray();var bounds=plan.Meshes.Select(e=>e.Mesh.bounds).ToArray();var formats=plan.Meshes.Select(e=>e.Mesh.indexFormat).ToArray();
            var channels=plan.Meshes.Select(e=>Enumerable.Range(0,8).Select(ch=>UV(e.Mesh,ch)).ToArray()).ToArray();var guids=paths.Select(AssetDatabase.AssetPathToGUID).ToArray();
            var material=AssetDatabase.LoadAssetAtPath<Material>(GrasslandGroundDetailSetup.MaterialPath);var oldMaterial=new Material(material);
            var owned=new HashSet<string>(paths.Concat(new[]{GrasslandGroundDetailSetup.MaterialPath}).SelectMany(p=>new[]{p,p+".meta"}));
            var files=Directory.GetFiles("Assets/Art","*",SearchOption.AllDirectories).Select(p=>p.Replace('\\','/')).Where(p=>!owned.Contains(p)).ToArray();var hashes=files.Select(Hash).ToArray();
            try
            {
                GrasslandGroundDetailSetup.Rebuild();
                material=AssetDatabase.LoadAssetAtPath<Material>(GrasslandGroundDetailSetup.MaterialPath);
                Check(AssetDatabase.GetAssetPath(AssetDatabase.LoadAssetAtPath<Material>(GrasslandGroundDetailSetup.MaterialPath).GetTexture("_GroundDetailMap"))==GrasslandGroundDetailSetup.MapPath,"saved source material points to the imported texture");
                for(int e=0;e<plan.Meshes.Length;e++)
                {
                    var m=plan.Meshes[e].Mesh;Check(m.vertices.SequenceEqual(plan.Meshes[e].Vertices)&&m.normals.SequenceEqual(plan.Meshes[e].Normals)&&m.colors.SequenceEqual(plan.Meshes[e].Colors)
                        &&m.triangles.SequenceEqual(faces[e])&&m.bounds==bounds[e]&&m.indexFormat==formats[e]&&AssetDatabase.AssetPathToGUID(paths[e])==guids[e],"exact plan, original faces/bounds/format/GUID");
                    for(int ch=0;ch<8;ch++)Check(UV(m,ch).SequenceEqual(channels[e][ch]),"all original UV channels retained");
                    var v=m.vertices;var n=m.normals;var c=m.colors;
                    for(int i=0;i<v.Length;i++)
                    {
                        Check(v[i].x==vertices[e][i].x&&v[i].z==vertices[e][i].z&&c[i].a==colors[e][i].a,"all XZ/alpha retained");
                        if(!plan.Meshes[e].Changed[i])Check(v[i]==vertices[e][i]&&n[i]==normals[e][i]&&c[i]==colors[e][i],"nonselected grass, all flowers/shrubs/stones and anchors retained");
                        else Check(v[i].y<=vertices[e][i].y&&vertices[e][i].y-v[i].y<.65f&&n[i].sqrMagnitude>.99f
                            &&Vector2.Distance(new Vector2(v[i].x,v[i].z),settings.Center)<settings.AreaRadius,"bounded lower edge grass only");
                    }
                }
                for(int i=0;i<material.shader.GetPropertyCount();i++)
                {
                    string name=material.shader.GetPropertyName(i);if(name.StartsWith("_GroundDetail",StringComparison.Ordinal))continue;
                    var type=material.shader.GetPropertyType(i);
                    if(type==ShaderPropertyType.Texture)Check(material.GetTexture(name)==oldMaterial.GetTexture(name)&&material.GetTextureScale(name)==oldMaterial.GetTextureScale(name)&&material.GetTextureOffset(name)==oldMaterial.GetTextureOffset(name),"existing material textures preserved");
                    else if(type==ShaderPropertyType.Vector||type==ShaderPropertyType.Color)Check(material.GetVector(name)==oldMaterial.GetVector(name),"existing material vector preserved");
                    else Check(material.GetFloat(name)==oldMaterial.GetFloat(name),"existing material scalar preserved");
                }
                var pixels=GrasslandBlendSetup.ReadPixels(GrasslandGroundDetailSetup.MapPath,out int width,out int height);
                Check(width==512&&height==512&&pixels.SequenceEqual(plan.Pixels),"exact planned sample data map");float reach=GrasslandBlendSetup.TexelReach(width,height,plan.Rect);
                for(int y=0;y<height;y++)for(int x=0;x<width;x++)if(pixels[y*width+x].a>0)
                {
                    var p=GrasslandBlendSetup.PixelWorld(x,y,width,height,plan.Rect);
                    Check(Vector2.Distance(p,settings.Center)+reach<settings.AreaRadius&&GrasslandIslandMeadowSetup.RouteDistance(p)-reach>2.1f,"complete bilinear support within sample and outside road");
                }
                for(int i=0;i<files.Length;i++)Check(Hash(files[i])==hashes[i],"all other art retained "+files[i]);
                var restore=paths.Concat(new[]{GrasslandGroundDetailSetup.MaterialPath,GrasslandGroundDetailSetup.MapPath}).SelectMany(p=>new[]{p,p+".meta"}).ToDictionary(p=>p,File.ReadAllBytes);
                try
                {
                    int changed=Array.FindIndex(plan.Meshes,e=>e.Changed.Any(b=>b));var mesh=plan.Meshes[changed].Mesh;var c=mesh.colors;c[Array.FindIndex(plan.Meshes[changed].Changed,b=>b)].g*=.99f;
                    mesh.colors=c;EditorUtility.SetDirty(mesh);material.SetFloat("_GroundDetailStrength",.61f);EditorUtility.SetDirty(material);AssetDatabase.SaveAssets();
                    var hand=pixels.ToArray();hand[Array.FindIndex(hand,p=>p.a>0)].r^=1;WriteMap(hand);
                    var all=Directory.GetFiles("Assets/Art","*",SearchOption.AllDirectories);var manual=all.Select(Hash).ToArray();
                    GrasslandGroundDetailSetup.Rebuild();GrasslandFlowerVisitSetup.Rebuild();GrasslandShrubWindSetup.Rebuild();GrasslandSpatialSetup.Rebuild();GrasslandBlendSetup.Rebuild();GrasslandPatchSetup.Rebuild();PrototypeAddressablesSetup.EnsureSetup();
                    for(int i=0;i<all.Length;i++)Check(Hash(all[i])==manual[i],"repeat/old entries/Setup preserve hand paint and material tuning "+all[i]);
                }
                finally
                {
                    foreach(var pair in restore)File.WriteAllBytes(pair.Key,pair.Value);
                    foreach(string p in restore.Keys.Where(p=>!p.EndsWith(".meta",StringComparison.Ordinal)).OrderBy(p=>p==GrasslandGroundDetailSetup.MaterialPath?1:0))
                        AssetDatabase.ImportAsset(p,ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);
                    Check(AssetDatabase.GetAssetPath(AssetDatabase.LoadAssetAtPath<Material>(GrasslandGroundDetailSetup.MaterialPath).GetTexture("_GroundDetailMap"))==GrasslandGroundDetailSetup.MapPath,"manual fixture restoration keeps the texture dependency");
                }
            }
            finally{Object.DestroyImmediate(oldMaterial);}
            File.WriteAllText(Folder+"/Stats.json",JsonUtility.ToJson(new Stats{Leaves=plan.Leaves,Clumps=plan.Meshes.Sum(e=>e.Clumps),Vertices=plan.Meshes.Sum(e=>e.Changed.Count(b=>b)),ActiveTexels=plan.Pixels.Count(p=>p.a>0)},true));
            Debug.Log("Grassland ground detail bake verification PASSED: exact local data map/bilinear boundary, lower grass edges, original XZ/anchors/flowers/UV/faces/bounds/GUIDs and material settings; protected art, custom/partial refusal and manual Setup.");
        }
        [Serializable] sealed class Stats{public int Leaves,Clumps,Vertices,ActiveTexels;}
        public static void After()=>GrasslandFlowerVisitVerification.After();
        static Color32[] GPU(Texture texture)
        {
            var rt=new RenderTexture(texture.width,texture.height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);var image=new Texture2D(texture.width,texture.height,TextureFormat.RGBA32,false,true);var active=RenderTexture.active;
            try{Graphics.Blit(texture,rt);RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();return image.GetPixels32();}
            finally{RenderTexture.active=active;Object.DestroyImmediate(rt);Object.DestroyImmediate(image);}
        }
        public static void Visit(GameManager g)
        {
            if(!GrasslandGroundDetailSetup.Done(AssetImporter.GetAtPath(GrasslandGroundDetailSetup.MaterialPath).userData))return;
            var grounds=GrasslandGroundLayersSetup.Grounds(g.Terrain.Terrain);var map=grounds[0].sharedMaterial.GetTexture("_GroundDetailMap");
            Check(map!=null&&GPU(map).SequenceEqual(GPU(AssetDatabase.LoadAssetAtPath<Texture2D>(GrasslandGroundDetailSetup.MapPath))),"actual Packed AA detail map and dependency");
            var original=grounds.Select(r=>{var b=new MaterialPropertyBlock();r.GetPropertyBlock(b);return b;}).ToArray();var block=new MaterialPropertyBlock();
            void Swap(bool before)
            {
                for(int i=0;i<grounds.Length;i++)
                {if(!before){grounds[i].SetPropertyBlock(original[i]);continue;}grounds[i].GetPropertyBlock(block);block.SetFloat("_GroundDetailStrength",0);grounds[i].SetPropertyBlock(block);}
            }
            try
            {
                GrasslandReadabilityVerification.Compare(g,Folder,Baseline,"ground detail",false,Swap);
                Check(g.Terrain.TryResolve(new Vector3(-133.5f,0,448),out var focus),"close ground");
                GrasslandReadabilityVerification.Compare(g,Folder+"/Close",Baseline,"ground detail close",false,before=>{Swap(before);GrasslandTouchVerification.Frame(g.Players.Player.ViewCamera,focus);g.Players.Player.ViewCamera.orthographicSize=3.2f;});
                Legacy(g,grounds);
                Check(!File.Exists(g.Saves.SavePath),"ground presentation never saves progress");
            }
            finally{Swap(false);}
        }
        static void Legacy(GameManager g,Renderer[] grounds)
        {
            var camera=g.Players.Player.ViewCamera;var pos=camera.transform.position;var rotation=camera.transform.rotation;float size=camera.orthographicSize,far=camera.farClipPlane;
            var materials=grounds.Select(r=>r.sharedMaterial).ToArray();var shader=AssetDatabase.LoadAssetAtPath<Shader>(Baseline+"/Before.shader");
            var before=new Material(materials[0]){shader=shader};var after=new Material(materials[0]);
            try
            {
                camera.farClipPlane=3200;
                foreach(bool outside in new[]{false,true})
                {
                    var p=outside?new Vector3(-176,0,570):new Vector3(-128,0,448);Check(g.Terrain.TryResolve(p,out var floor),"legacy ground");
                    GrasslandTouchVerification.Frame(camera,floor);camera.orthographicSize=3.2f;after.SetFloat("_GroundDetailStrength",outside?.86f:0);
                    foreach(var r in grounds)r.sharedMaterial=before;var a=GrasslandTouchVerification.Capture(camera,Folder+"/Legacy"+outside+"Before.png");
                    foreach(var r in grounds)r.sharedMaterial=after;var b=GrasslandTouchVerification.Capture(camera,Folder+"/Legacy"+outside+"After.png");
                    Check(GrasslandCrownWindVerification.StableFrame(a,b),"original/new ground shader at zero strength or outside sample");
                }
            }
            finally
            {
                for(int i=0;i<grounds.Length;i++)grounds[i].sharedMaterial=materials[i];Object.DestroyImmediate(before);Object.DestroyImmediate(after);
                camera.transform.SetPositionAndRotation(pos,rotation);camera.orthographicSize=size;camera.farClipPlane=far;
            }
            Debug.Log("Grassland ground detail legacy GPU verification PASSED: original/current shader at disabled sample and outside the sample; original ground appearance retained.");
        }
    }
}
