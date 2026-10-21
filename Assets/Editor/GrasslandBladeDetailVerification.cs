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
    public static class GrasslandBladeDetailVerification
    {
        const string Folder="Logs/GrasslandBladeDetail",Baseline="Assets/Editor/GrasslandBladeDetailBaseline";
        static void Check(bool ok,string why){if(!ok)throw new InvalidOperationException("Blade detail verification: "+why);}
        static string Hash(string p){using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(p)));}
        static Vector4[] UV(Mesh m,int ch){var a=new List<Vector4>();m.GetUVs(ch,a);return a.ToArray();}
        public static void BakeAndVerify()
        {
            Check(Application.dataPath.Replace('\\','/').Contains("/Logs/"),"isolated project required");Directory.CreateDirectory(Folder);
            var root=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);GrasslandBladeDetailSetup.Edit[] plan;MeadowTouchSettings settings;
            try
            {
                var t=root.GetComponent<IslandTerrain>();settings=t.SeasonalDetails.TouchSample;plan=GrasslandBladeDetailSetup.Plan(t);
                var r=settings.Renderers[0];var original=r.sharedMaterial;var custom=new Material(Shader.Find("Standard"));
                try{r.sharedMaterial=custom;bool refused=false;try{GrasslandBladeDetailSetup.Plan(t);}catch(InvalidOperationException){refused=true;}Check(refused,"custom material refused");}
                finally{r.sharedMaterial=original;Object.DestroyImmediate(custom);}
                var imp=AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(plan[0].Mesh));string data=imp.userData;
                try
                {
                    imp.userData=data+";Artist.Custom";bool refused=false;try{GrasslandBladeDetailSetup.Plan(t);}catch(InvalidOperationException){refused=true;}Check(refused,"custom metadata refused");
                    foreach(string marker in new[]{GrasslandBladeDetailSetup.Pending,GrasslandBladeDetailSetup.Marker})
                    {imp.userData=data+";"+marker;refused=false;try{GrasslandBladeDetailSetup.Rebuild();}catch(InvalidOperationException){refused=true;}Check(refused,"pending/partial refused");}
                }
                finally{imp.userData=data;}
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            var paths=plan.Select(e=>AssetDatabase.GetAssetPath(e.Mesh)).ToArray();
            var v0=plan.Select(e=>e.Mesh.vertices).ToArray();var n0=plan.Select(e=>e.Mesh.normals).ToArray();var c0=plan.Select(e=>e.Mesh.colors).ToArray();
            var faces0=plan.Select(e=>e.Mesh.triangles).ToArray();var channels=plan.Select(e=>Enumerable.Range(0,8).Select(ch=>UV(e.Mesh,ch)).ToArray()).ToArray();
            var guids=paths.Select(AssetDatabase.AssetPathToGUID).ToArray();var formats=plan.Select(e=>e.Mesh.indexFormat).ToArray();
            if(!AssetDatabase.IsValidFolder(Baseline))AssetDatabase.CreateFolder("Assets/Editor","GrasslandBladeDetailBaseline");
            foreach(string p in paths)
            {
                string target=Baseline+"/"+Path.GetFileName(p);
                if(File.Exists(target))
                {
                    var a=AssetDatabase.LoadAssetAtPath<Mesh>(p);var b=AssetDatabase.LoadAssetAtPath<Mesh>(target);
                    Check(a.vertices.SequenceEqual(b.vertices)&&a.normals.SequenceEqual(b.normals)&&a.colors.SequenceEqual(b.colors)
                        &&a.triangles.SequenceEqual(b.triangles)&&a.bounds==b.bounds&&a.indexFormat==b.indexFormat
                        &&Enumerable.Range(0,8).All(ch=>UV(a,ch).SequenceEqual(UV(b,ch))),"preserve exact original fixture");
                }
                else Check(AssetDatabase.CopyAsset(p,target),"original fixture");
            }
            var excluded=new HashSet<string>(paths.SelectMany(p=>new[]{p,p+".meta"}));
            var files=Directory.GetFiles("Assets/Art","*",SearchOption.AllDirectories).Select(p=>p.Replace('\\','/')).Where(p=>!excluded.Contains(p)).ToArray();var hashes=files.Select(Hash).ToArray();
            GrasslandBladeDetailSetup.Rebuild();
            for(int e=0;e<plan.Length;e++)
            {
                var m=plan[e].Mesh;var v=m.vertices;var n=m.normals;var c=m.colors;var roots=m.uv2;var weights=m.uv;
                Check(v.SequenceEqual(plan[e].Vertices)&&n.SequenceEqual(plan[e].Normals)&&c.SequenceEqual(plan[e].Colors)
                    &&m.triangles.SequenceEqual(plan[e].Indices)&&m.bounds==plan[e].Bounds&&m.indexFormat==formats[e]
                    &&AssetDatabase.AssetPathToGUID(paths[e])==guids[e],"exact plan, original bounds/format/GUID");
                Check(v.Length-v0[e].Length==plan[e].Triangles*4,"only two new sections per original triangular leaf");
                for(int ch=0;ch<8;ch++)Check(UV(m,ch).Take(channels[e][ch].Length).SequenceEqual(channels[e][ch])
                    &&(ch<3||UV(m,ch).Length==0),"all original UV values and dimensions retained");
                var retainedFaces=new List<int>();var oldRetained=new List<int>();
                for(int i=0;i<faces0[e].Length;i+=3)
                    if(Enumerable.Range(0,3).All(j=>!plan[e].Changed[faces0[e][i+j]]))oldRetained.AddRange(faces0[e].Skip(i).Take(3));
                var faces=m.triangles;
                for(int i=0;i<faces.Length;i+=3)
                    if(Enumerable.Range(0,3).All(j=>faces[i+j]<v0[e].Length&&!plan[e].Changed[faces[i+j]]))retainedFaces.AddRange(faces.Skip(i).Take(3));
                Check(retainedFaces.SequenceEqual(oldRetained),"nonselected face indices and ordering retained");
                for(int i=0;i<v.Length;i++)
                {
                    if(i<v0[e].Length&&!plan[e].Changed[i])
                        Check(v[i]==v0[e][i]&&n[i]==n0[e][i]&&c[i]==c0[e][i],"anchors, orphan vertices, flowers, shrubs and nonselected grass retained");
                    else
                    {
                        Check(weights[i].x< -4&&weights[i].x>=-5&&n[i].sqrMagnitude>.99f&&n[i].y>.85f
                            &&Vector2.Distance(new Vector2(v[i].x,v[i].z),roots[i])<=1.161f
                            &&Vector2.Distance(roots[i],settings.Center)+1.3f<settings.AreaRadius
                            &&GrasslandIslandMeadowSetup.RouteDistance(roots[i])>=3.65f,"bounded grass only, soft positive normals, road and complete motion clearance");
                        if(i<v0[e].Length)Check(c[i].a==c0[e][i].a&&Vector3.Distance(v[i],v0[e][i])<.65f
                            &&v[i].y<=v0[e][i].y+.035f,"original alpha and low grass height retained");
                    }
                }
                foreach(var part in BiomePolishSetup.Components(m).Where(a=>a.Length>1&&a.All(i=>weights[i].x>=-5&&weights[i].x<=-4)))
                    Check(part.Length==3||part.Length==5||part.Length==7,"no broken or connected neighboring leaves");
            }
            for(int i=0;i<files.Length;i++)Check(Hash(files[i])==hashes[i],"all other art protected "+files[i]);
            var restore=paths.SelectMany(p=>new[]{p,p+".meta"}).ToDictionary(p=>p,File.ReadAllBytes);
            try
            {
                var m=plan[0].Mesh;var paint=m.colors;paint[Array.FindIndex(plan[0].Changed,b=>b)].g*=.99f;m.colors=paint;EditorUtility.SetDirty(m);AssetDatabase.SaveAssets();
                var all=Directory.GetFiles("Assets/Art","*",SearchOption.AllDirectories);var manual=all.Select(Hash).ToArray();
                GrasslandBladeDetailSetup.Rebuild();GrasslandGroundDetailSetup.Rebuild();GrasslandFlowerVisitSetup.Rebuild();GrasslandShrubWindSetup.Rebuild();
                GrasslandSpatialSetup.Rebuild();GrasslandPatchSetup.Rebuild();GrasslandReadabilitySetup.Rebuild();PrototypeAddressablesSetup.EnsureSetup();
                for(int i=0;i<all.Length;i++)Check(Hash(all[i])==manual[i],"repeat/previous entries/Setup retain manual shape and paint "+all[i]);
            }
            finally
            {
                foreach(var p in restore)File.WriteAllBytes(p.Key,p.Value);
                foreach(string p in paths)AssetDatabase.ImportAsset(p,ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);
            }
            File.WriteAllText(Folder+"/Stats.json",JsonUtility.ToJson(new Stats{Blades=plan.Sum(e=>e.Blades),TriangularLeaves=plan.Sum(e=>e.Triangles),ExistingStrips=plan.Sum(e=>e.Strips),AddedVertices=plan.Sum(e=>e.Triangles)*4},true));
            Debug.Log("Grassland blade detail bake verification PASSED: curved triangle replacements and rolled strips, soft normals; exact plan, original indices/UV/anchors/flowers/bounds/GUIDs, other art, custom/pending/partial refusal and manual Setup.");
        }
        [Serializable] sealed class Stats{public int Blades,TriangularLeaves,ExistingStrips,AddedVertices;}
        public static void After()=>GrasslandGroundDetailVerification.After();
        public static void Visit(GameManager g)
        {
            var rs=g.Terrain.Terrain.SeasonalDetails.TouchSample.Renderers;
            if(!rs.All(r=>GrasslandBladeDetailSetup.Done(AssetImporter.GetAtPath(BiomeIslandsSetup.Folder+"/"+r.GetComponent<MeshFilter>().sharedMesh.name+".asset").userData)))return;
            GrasslandReadabilityVerification.Compare(g,Folder,Baseline,"blade detail");
            Check(g.Terrain.TryResolve(new Vector3(-133.5f,0,448),out var focus),"close ground");
            GrasslandReadabilityVerification.Compare(g,Folder+"/Close",Baseline,"blade detail close",false,before=>
            {GrasslandTouchVerification.Frame(g.Players.Player.ViewCamera,focus);g.Players.Player.ViewCamera.orthographicSize=3.2f;});
            GrasslandReadabilityVerification.Compare(g,Folder+"/Reverse",Baseline,"blade detail reverse",false,before=>
            {
                var camera=g.Players.Player.ViewCamera;GrasslandTouchVerification.Frame(camera,focus);camera.orthographicSize=3.2f;
                camera.transform.RotateAround(focus,Vector3.up,180);
            });
        }
    }
}
