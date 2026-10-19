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
    public static class GrasslandGroveVerification
    {
        const string Key="MagicIsland.GrasslandGrove.Before";
        static void Check(bool ok,string why){if(!ok)throw new InvalidOperationException("Grove verification: "+why);}
        static string Hash(string p){using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(p)));}
        static Vector4[] UV(Mesh mesh,int ch){var list=new List<Vector4>();mesh.GetUVs(ch,list);return list.ToArray();}
        public static void Before(){SessionState.SetBool(Key,true);BiomeIslandsVerification.Run();}
        public static void After(){SessionState.SetBool(Key,false);VerifyAssets();BiomeIslandsVerification.Run();}
        public static void BakeAndVerify()
        {
            Check(Application.dataPath.Replace('\\','/').Contains("/Logs/"),"isolated validation project");
            var root=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);GrasslandGroveSetup.CrownEdit[] plan;
            try{plan=GrasslandGroveSetup.Crowns(root.GetComponent<IslandTerrain>());}finally{PrefabUtility.UnloadPrefabContents(root);}
            var paths=plan.Select(p=>AssetDatabase.GetAssetPath(p.Mesh)).ToArray();var verts=plan.Select(p=>p.Mesh.vertices).ToArray();var normals=plan.Select(p=>p.Mesh.normals).ToArray();var colors=plan.Select(p=>p.Mesh.colors).ToArray();var tri=plan.Select(p=>p.Mesh.triangles).ToArray();
            var uv=plan.Select(p=>Enumerable.Range(0,8).Select(ch=>UV(p.Mesh,ch)).ToArray()).ToArray();var guids=paths.Select(AssetDatabase.AssetPathToGUID).ToArray();
            var exceptions=new HashSet<string>(paths.SelectMany(p=>new[]{p,p+".meta"}).Concat(new[]{TerrainSetup.PrefabPath}));var all=Directory.GetFiles("Assets/Art","*",SearchOption.AllDirectories).Select(p=>p.Replace('\\','/')).ToArray();var protect=all.Where(p=>!exceptions.Contains(p)).ToArray();var hashes=protect.Select(Hash).ToArray();
            var importer=AssetImporter.GetAtPath(paths[0]);string metadata=importer.userData;
            try
            {
                importer.userData="Artist.Custom";root=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);bool refused=false;
                try{GrasslandGroveSetup.Crowns(root.GetComponent<IslandTerrain>());}catch(InvalidOperationException){refused=true;}finally{PrefabUtility.UnloadPrefabContents(root);}
                Check(refused,"foreign metadata rejected before writes");
            }
            finally{importer.userData=metadata;}
            GrasslandGroveSetup.Rebuild();int changed=0;
            for(int p=0;p<paths.Length;p++)
            {
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(paths[p]);Check(AssetDatabase.AssetPathToGUID(paths[p])==guids[p]&&mesh.triangles.SequenceEqual(tri[p]),"GUID/topology retained");var v=mesh.vertices;var n=mesh.normals;var c=mesh.colors;
                for(int ch=0;ch<8;ch++)Check(UV(mesh,ch).SequenceEqual(uv[p][ch]),"all original UV channels retained");
                for(int i=0;i<v.Length;i++)
                {
                    if(plan[p].Changed[i]){changed++;Check(v[i]==plan[p].Vertices[i]&&c[i]==plan[p].Colors[i],"planned crown only");}
                    else Check(v[i]==verts[p][i]&&n[i]==normals[p][i]&&c[i]==colors[p][i],"original trunks / other biomes / unselected vertices retained");
                }
            }
            for(int i=0;i<protect.Length;i++)Check(Hash(protect[i])==hashes[i],"original ground/collision/ecology/art retained "+protect[i]);VerifyAssets();
            // Save a later artist edit, then ensure neither this entry nor normal Setup replaces it.
            string manual=GrasslandGroveSetup.Folder+"/"+AssetDatabase.LoadAssetAtPath<GameObject>(TerrainSetup.PrefabPath).GetComponent<IslandTerrain>().World.Scenery.Find(GrasslandGroveSetup.Group).GetChild(0).name+".asset";
            var bytes=File.ReadAllBytes(manual);
            try
            {
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(manual);var c=mesh.colors;c[0].r*=.97f;mesh.colors=c;EditorUtility.SetDirty(mesh);AssetDatabase.SaveAssets();var files=Directory.GetFiles("Assets/Art","*",SearchOption.AllDirectories);var saved=files.Select(Hash).ToArray();
                GrasslandGroveSetup.Rebuild();PrototypeAddressablesSetup.EnsureSetup();for(int i=0;i<files.Length;i++)Check(saved[i]==Hash(files[i]),"repeat/normal Setup preserves hand edits "+files[i]);
            }
            finally{File.WriteAllBytes(manual,bytes);AssetDatabase.ImportAsset(manual,ImportAssetOptions.ForceUpdate);}
            Debug.Log("Grassland grove bake verification PASSED: "+changed+" crown vertices; original trunks, geometry, collision, all UVs, GUIDs, other islands, metadata refusal and manual/repeat Setup.");
        }
        public static void VerifyAssets()
        {
            var root=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);
            try
            {
                var t=root.GetComponent<IslandTerrain>();var group=t.World.Scenery.Find(GrasslandGroveSetup.Group);Check(t.IsValid&&group!=null,"AA owner references");Check(group.GetComponentsInChildren<Collider>(true).Length==0,"pure combined visuals");
                foreach(var r in group.GetComponentsInChildren<Renderer>(true))
                {
                    Check(t.SeasonalDetails.MeadowBatches.Contains(r)&&(r.sharedMaterial.shader.name=="MagicIsland/Meadow Cover"||r.sharedMaterial.shader.name==GrasslandFloraSetup.ShaderName),"existing seasonal/cut owner");var m=r.GetComponent<MeshFilter>().sharedMesh;var v=m.vertices;var roots=m.uv2;
                    Check(roots.Length==v.Length&&m.uv.Length==v.Length&&m.uv3.Length==v.Length,"all wind/root/season channels");
                    for(int i=0;i<v.Length;i++)Check(Vector2.Distance(new Vector2(v[i].x,v[i].z),roots[i])<1.181f&&GrasslandIslandMeadowSetup.RouteDistance(roots[i])>=3.25f,"full cut envelope and road clearance");
                }
                Check(t.SeasonalDetails.MeadowBatches.Length<=64,"bounded batches");
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        public static void Visit(GameManager g)
        {
            bool before=SessionState.GetBool(Key,false);var terrain=g.Terrain.Terrain;var group=terrain.World.Scenery.Find(GrasslandGroveSetup.Group);if(!before&&group==null)return;
            string folder="Logs/GrasslandGrove/"+(before?"Before":"After");Directory.CreateDirectory(folder);var camera=g.Players.Player.ViewCamera;var pos=camera.transform.position;var rot=camera.transform.rotation;float size=camera.orthographicSize,far=camera.farClipPlane;
            try
            {
                camera.farClipPlane=3200;g.Weather.Tick(720,true);g.Ocean.Surface.Tick(0,1,false,g.Weather.Sun);
                if(!before&&Directory.Exists("Assets/Editor/GroveBaseline"))OriginalViews(g,folder.Replace("After","Before"));
                Shot(camera,new Vector3(-126,14,532),18,folder+"/Grove.png");Shot(camera,new Vector3(-176,13,570),18,folder+"/WestSlope.png");Shot(camera,new Vector3(-79,13,548),17,folder+"/FlowerSlope.png");Shot(camera,new Vector3(-112,14,537),13,folder+"/Clearing.png");Shot(camera,new Vector3(-108,14,600),20,folder+"/Woodland.png");
                GameViews(camera,folder);
                if(!before)
                {
                    var renderers=group.GetComponentsInChildren<Renderer>(true);Check(renderers.All(r=>r.enabled),"new understory visible outside");terrain.SeasonalDetails.Present(g.Calendar.Date.Season,false);Check(renderers.All(r=>!r.enabled),"new understory hidden indoors");terrain.SeasonalDetails.Present(g.Calendar.Date.Season,true);
                    foreach(var f in terrain.World.Scenery.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.name.StartsWith("Landscape ",StringComparison.Ordinal)))
                    {
                        string path=GrasslandGroveSetup.Folder+"/Art "+f.name+".asset";var source=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(source==null||AssetImporter.GetAtPath(path).userData!=GrasslandGroveSetup.Marker)continue;
                        Check(source.vertices.SequenceEqual(f.sharedMesh.vertices)&&source.colors.SequenceEqual(f.sharedMesh.colors),"real Packed crown data");
                    }
                    Check(!File.Exists(g.Saves.SavePath),"groves do not save in daytime");Debug.Log("Grassland grove Packed presentation PASSED: actual crown meshes, understory, indoor visibility and no daytime writes.");
                }
            }
            finally{camera.transform.SetPositionAndRotation(pos,rot);camera.orthographicSize=size;camera.farClipPlane=far;}
        }
        static void OriginalViews(GameManager g,string folder)
        {
            Check(Application.dataPath.Replace('\\','/').Contains("/Logs/"),"isolated original-mesh comparison");Directory.CreateDirectory(folder);
            var group=g.Terrain.Terrain.World.Scenery.Find(GrasslandGroveSetup.Group);var renderers=group.GetComponentsInChildren<Renderer>(true);var visible=renderers.Select(r=>r.enabled).ToArray();
            var originals=new List<(MeshFilter filter,Mesh mesh)>();
            try
            {
                foreach(var f in g.Terrain.Terrain.World.Scenery.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.name.StartsWith("Landscape ",StringComparison.Ordinal)))
                {
                    var old=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Editor/GroveBaseline/Art "+f.name+".asset");if(old==null)continue;var mesh=f.sharedMesh;
                    // Swap only this test instance, so later explicit crown topology remains intact.
                    originals.Add((f,mesh));f.sharedMesh=old;
                }
                foreach(var r in renderers)r.enabled=false;GameViews(g.Players.Player.ViewCamera,folder);
            }
            finally
            {
                foreach(var p in originals)p.filter.sharedMesh=p.mesh;
                for(int i=0;i<renderers.Length;i++)renderers[i].enabled=visible[i];
            }
        }
        static void GameViews(Camera camera,string folder)
        {
            GameShot(camera,new Vector3(-126,13,532),folder+"/GameGrove.png");GameShot(camera,new Vector3(-176,13,570),folder+"/GameSlope.png");GameShot(camera,new Vector3(-112,13,537),folder+"/GameClearing.png");
        }
        static void GameShot(Camera camera,Vector3 point,string path)=>Shot(camera,point,7,path,18,42);
        static void Shot(Camera camera,Vector3 focus,float size,string path,float distance=55,float pitch=45)
        {
            camera.transform.rotation=Quaternion.Euler(pitch,0,0);camera.transform.position=focus-camera.transform.forward*distance;camera.orthographicSize=size;var rt=new RenderTexture(1280,800,24);var old=camera.targetTexture;var active=RenderTexture.active;var texture=new Texture2D(1280,800,TextureFormat.RGB24,false);
            try{camera.targetTexture=rt;camera.Render();camera.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,1280,800),0,0);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());}
            finally{camera.targetTexture=old;RenderTexture.active=active;Object.DestroyImmediate(rt);Object.DestroyImmediate(texture);}
        }
    }
}
