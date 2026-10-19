using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using MagicIsland.Managers;
using MagicIsland.TerrainSystem;
using MagicIsland.WeatherSystem;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    public static class GrasslandCrownVerification
    {
        const string Folder="Logs/GrasslandCrowns",Baseline="Assets/Editor/GrasslandCrownBaseline";
        [Serializable]public sealed class ViewsData{public View[] Trees;}
        [Serializable]public sealed class View{public Vector3 Root;public int Kind;}
        static void Check(bool ok,string why){if(!ok)throw new InvalidOperationException("Crown verification: "+why);}
        static string Hash(string p){using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(p)));}
        static Vector4[] UV(Mesh m,int channel){var a=new List<Vector4>();m.GetUVs(channel,a);return a.ToArray();}
        public static void BakeAndVerify()
        {
            Check(Application.dataPath.Replace('\\','/').Contains("/Logs/"),"isolated validation project");var root=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);GrasslandCrownSetup.Edit[] plan;
            try{plan=GrasslandCrownSetup.Plan(root.GetComponent<IslandTerrain>());}finally{PrefabUtility.UnloadPrefabContents(root);}
            var paths=plan.Select(e=>AssetDatabase.GetAssetPath(e.Mesh)).ToArray();var oldV=plan.Select(e=>e.Mesh.vertices).ToArray();var oldN=plan.Select(e=>e.Mesh.normals).ToArray();var oldC=plan.Select(e=>e.Mesh.colors).ToArray();var oldT=plan.Select(e=>e.Mesh.triangles).ToArray();var guids=paths.Select(AssetDatabase.AssetPathToGUID).ToArray();
            var oldUV=plan.Select(e=>Enumerable.Range(0,8).Select(ch=>UV(e.Mesh,ch)).ToArray()).ToArray();var bounds=plan.Select(e=>e.Mesh.bounds).ToArray();
            Check(!Directory.Exists(Baseline),"fresh pre-refinement fixtures");AssetDatabase.CreateFolder("Assets/Editor","GrasslandCrownBaseline");foreach(string p in paths)Check(AssetDatabase.CopyAsset(p,Baseline+"/"+Path.GetFileName(p)),"original mesh fixture");
            Directory.CreateDirectory(Folder);var trees=plan.SelectMany(e=>e.Roots.Select((r,i)=>new View{Root=r,Kind=e.Kinds[i]})).GroupBy(v=>v.Root).Select(g=>g.First()).ToArray();
            File.WriteAllText(Folder+"/Views.json",JsonUtility.ToJson(new ViewsData{Trees=trees}));Check(trees.Select(t=>t.Kind).Distinct().Count()==3,"three authored tree proportions");
            var exceptions=new HashSet<string>(paths.SelectMany(p=>new[]{p,p+".meta"}));var protect=Directory.GetFiles("Assets/Art","*",SearchOption.AllDirectories).Select(p=>p.Replace('\\','/')).Where(p=>!exceptions.Contains(p)).ToArray();var hashes=protect.Select(Hash).ToArray();
            var importer=AssetImporter.GetAtPath(paths[0]);string metadata=importer.userData;
            try
            {
                importer.userData="Artist.Custom";root=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);bool refused=false;
                try{GrasslandCrownSetup.Plan(root.GetComponent<IslandTerrain>());}catch(InvalidOperationException){refused=true;}finally{PrefabUtility.UnloadPrefabContents(root);}Check(refused,"foreign metadata refused before writes");
            }
            finally{importer.userData=metadata;}
            GrasslandCrownSetup.Rebuild();int vertices=0,faces=0;
            for(int p=0;p<paths.Length;p++)
            {
                var m=AssetDatabase.LoadAssetAtPath<Mesh>(paths[p]);var e=plan[p];Check(AssetDatabase.AssetPathToGUID(paths[p])==guids[p]&&m.bounds==bounds[p],"original GUID and conservative renderer bounds");
                Check(m.vertices.SequenceEqual(e.Vertices)&&m.normals.SequenceEqual(e.Normals)&&m.colors.SequenceEqual(e.Colors)&&m.triangles.SequenceEqual(e.Triangles),"exact planned crown art");
                Check(m.vertices.Take(oldV[p].Length).SequenceEqual(oldV[p])&&m.normals.Take(oldV[p].Length).SequenceEqual(oldN[p])&&m.colors.Take(oldV[p].Length).SequenceEqual(oldC[p]),"all original vertices retained, including trunks and unrelated detail");
                for(int ch=0;ch<8;ch++){var uv=UV(m,ch);Check(uv.SequenceEqual(e.UV[ch])&&uv.Take(oldUV[p][ch].Length).SequenceEqual(oldUV[p][ch]),"all original UV0-7 and appended inherited data");}
                var keep=new List<int>();for(int i=0;i<oldT[p].Length;i+=3)if(!e.Retired[oldT[p][i]])keep.AddRange(oldT[p].Skip(i).Take(3));
                Check(m.triangles.Take(keep.Count).SequenceEqual(keep)&&m.triangles.All(i=>i>=oldV[p].Length||!e.Retired[i]),"only original selected crown faces retired");
                var newV=m.vertices.Skip(oldV[p].Length).ToArray();var newN=m.normals.Skip(oldV[p].Length).ToArray();
                for(int i=0;i<newV.Length;i++)
                {
                    Check(newN[i].sqrMagnitude>.999f&&newN[i].sqrMagnitude<1.001f,"smooth unit normals");
                    Check(e.Crowns.Any(b=>{var d=newV[i]-b.center;return new Vector3(d.x/b.extents.x,d.y/b.extents.y,d.z/b.extents.z).sqrMagnitude<=1.001f;}),"new foliage inside original crown ellipsoids");
                }
                vertices+=newV.Length;faces+=(m.triangles.Length-keep.Count)/3;
            }
            for(int i=0;i<protect.Length;i++)Check(Hash(protect[i])==hashes[i],"prefab, original collision, ground, materials, other biomes and ecology retained "+protect[i]);
            byte[] bytes=File.ReadAllBytes(paths[0]);
            try
            {
                var m=AssetDatabase.LoadAssetAtPath<Mesh>(paths[0]);var c=m.colors;c[c.Length-1].r*=.97f;m.colors=c;EditorUtility.SetDirty(m);AssetDatabase.SaveAssets();var all=Directory.GetFiles("Assets/Art","*",SearchOption.AllDirectories);var saved=all.Select(Hash).ToArray();
                GrasslandCrownSetup.Rebuild();GrasslandGroveSetup.Rebuild();GrasslandShrubStoneSetup.Rebuild();PrototypeAddressablesSetup.EnsureSetup();for(int i=0;i<all.Length;i++)Check(saved[i]==Hash(all[i]),"repeat/previous entries/normal Setup preserve hand edits "+all[i]);
            }
            finally{File.WriteAllBytes(paths[0],bytes);AssetDatabase.ImportAsset(paths[0],ImportAssetOptions.ForceUpdate);}
            Debug.Log("Grassland crown bake verification PASSED: "+trees.Length+" anchored trees, "+vertices+" new crown vertices, "+faces+" crown faces; original vertices/UVs/trunks/non-crown topology/GUIDs/bounds/prefab/materials/other art retained; custom metadata and later manual edits protected.");
        }
        public static void After(){BiomeIslandsVerification.Run();}
        public static void Visit(GameManager g)
        {
            var filters=g.Terrain.Terrain.World.Scenery.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.name.StartsWith("Landscape ",StringComparison.Ordinal)).Where(f=>
            {
                var importer=AssetImporter.GetAtPath(BiomeIslandsSetup.Folder+"/"+f.sharedMesh.name+".asset");return importer!=null&&GrasslandCrownSetup.Done(importer.userData);
            }).ToArray();if(filters.Length==0)return;
            var meshes=filters.Select(f=>f.sharedMesh).ToArray();var materials=filters.Select(f=>f.GetComponent<Renderer>().sharedMaterial).ToArray();var camera=g.Players.Player.ViewCamera;var position=camera.transform.position;var rotation=camera.transform.rotation;float size=camera.orthographicSize,far=camera.farClipPlane;
            var state=g.Weather.State;float rain=g.Weather.RainAmount;var stateProperty=typeof(WeatherManager).GetProperty("State");var rainProperty=typeof(WeatherManager).GetProperty("RainAmount");
            try
            {
                camera.farClipPlane=3200;
                foreach(var m in meshes){var source=AssetDatabase.LoadAssetAtPath<Mesh>(BiomeIslandsSetup.Folder+"/"+m.name+".asset");Check(source!=null&&source.vertices.SequenceEqual(m.vertices)&&source.normals.SequenceEqual(m.normals)&&source.colors.SequenceEqual(m.colors)&&source.triangles.SequenceEqual(m.triangles),"actual Packed rounded crowns");}
                if(Directory.Exists(Baseline)&&File.Exists(Folder+"/Views.json"))
                {
                    var trees=JsonUtility.FromJson<ViewsData>(File.ReadAllText(Folder+"/Views.json")).Trees;
                    foreach(string light in new[]{"Day","Dusk","Rain"})
                    {
                        bool wet=light=="Rain";stateProperty.SetValue(g.Weather,new WeatherState(state.Seed,0,state.Day,wet?WeatherKind.Rainy:WeatherKind.Sunny));rainProperty.SetValue(g.Weather,wet?1f:0f);g.Weather.Tick(light=="Dusk"?1100:720,true);g.Ocean.Surface.Tick(0,light=="Dusk"?.4f:1,wet,g.Weather.Sun);
                        for(int i=0;i<filters.Length;i++){filters[i].sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Baseline+"/"+meshes[i].name+".asset");Check(filters[i].sharedMesh!=null,"original crown fixtures");}Views(camera,trees,Folder+"/Before/"+light);for(int i=0;i<filters.Length;i++)filters[i].sharedMesh=meshes[i];
                        Views(camera,trees,Folder+"/After/"+light);
                    }
                }
                for(int i=0;i<filters.Length;i++)Check(filters[i].GetComponent<Renderer>().sharedMaterial==materials[i],"shared material ownership unchanged");Check(!File.Exists(g.Saves.SavePath),"no daytime save");
                Debug.Log("Grassland crowns Packed verification PASSED: actual refined topology/color/normals, three tree proportions at default pitch/size, day/dusk/rain comparison, shared renderer/material ownership and no daytime saves.");
            }
            finally
            {
                for(int i=0;i<filters.Length;i++)filters[i].sharedMesh=meshes[i];stateProperty.SetValue(g.Weather,state);rainProperty.SetValue(g.Weather,rain);g.Weather.Tick(720,true);g.Ocean.Surface.Tick(0,1,false,g.Weather.Sun);camera.transform.SetPositionAndRotation(position,rotation);camera.orthographicSize=size;camera.farClipPlane=far;
            }
        }
        static void Views(Camera camera,View[] trees,string folder)
        {
            Directory.CreateDirectory(folder);for(int i=0;i<3;i++)
            {
                var tree=trees.Where(t=>t.Kind==i).OrderBy(t=>(t.Root-new Vector3(-112,12,537)).sqrMagnitude).First();Frame(camera,tree.Root+Vector3.up*4.8f,7);Capture(camera,folder+"/Tree"+i+".png");Frame(camera,tree.Root+Vector3.up*.8f,7);Capture(camera,folder+"/Game"+i+".png");
            }
        }
        static void Frame(Camera camera,Vector3 focus,float size){camera.transform.rotation=Quaternion.Euler(42,0,0);camera.transform.position=focus-camera.transform.forward*18;camera.orthographicSize=size;}
        static void Capture(Camera camera,string path)
        {
            var rt=new RenderTexture(1280,800,24);var old=camera.targetTexture;var active=RenderTexture.active;var image=new Texture2D(1280,800,TextureFormat.RGB24,false);
            try{camera.targetTexture=rt;camera.Render();camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1280,800),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());}
            finally{camera.targetTexture=old;RenderTexture.active=active;Object.DestroyImmediate(rt);Object.DestroyImmediate(image);}
        }
    }
}
