using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using MagicIsland.Interaction;
using MagicIsland.Managers;
using MagicIsland.TerrainSystem;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    public static class GrasslandRevitalizationVerification
    {
        static void Check(bool ok,string why)=>GrasslandRevitalizationSetup.Check(ok,"verification: "+why);
        static string Hash(string p){using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(p)));}
        static Vector4[] UV(Mesh m,int ch){var a=new List<Vector4>();m.GetUVs(ch,a);return a.ToArray();}
        [Serializable] public sealed class Report {public string[] editedMeshes,newFiles;public int clumps,flowers,batches,triangles,treeVertices,removedTriangles;}
        public static void BakeAndVerify()
        {
            Check(Application.dataPath.Replace('\\','/').Contains("/Logs/"),"isolated validation project required");
            var terrain=AssetDatabase.LoadAssetAtPath<GameObject>(TerrainSetup.PrefabPath).GetComponent<IslandTerrain>();var plan=GrasslandRevitalizationSetup.Plan(terrain);
            Check(plan.Length>0,"original unrefined baseline");var edited=plan.Select(e=>AssetDatabase.GetAssetPath(e.Mesh)).ToArray();var roadPath=AssetDatabase.GetAssetPath(terrain.World.transform.Find("Exploration paths").GetComponent<MeshFilter>().sharedMesh);
            var v=plan.Select(e=>e.Mesh.vertices).ToArray();var n=plan.Select(e=>e.Mesh.normals).ToArray();var c=plan.Select(e=>e.Mesh.colors).ToArray();var tri=plan.Select(e=>e.Mesh.triangles).ToArray();
            var uv=plan.Select(e=>Enumerable.Range(0,8).Select(ch=>UV(e.Mesh,ch)).ToArray()).ToArray();
            var files=Directory.GetFiles("Assets/Art","*",SearchOption.AllDirectories).Select(p=>p.Replace('\\','/')).ToArray();
            var protectedPaths=files.Where(p=>p!=TerrainSetup.PrefabPath&&p!=roadPath&&p!=roadPath+".meta"&&!edited.Contains(p)&&!edited.Select(e=>e+".meta").Contains(p)).ToArray();var hashes=protectedPaths.Select(Hash).ToArray();
            var collisions=terrain.GetComponentsInChildren<Collider>(true).Select(o=>o.name+":"+EditorJsonUtility.ToJson(o)).ToArray();
            var importer=AssetImporter.GetAtPath(edited[0]);string data=importer.userData;
            try{importer.userData="Artist.Custom";bool refused=false;try{GrasslandRevitalizationSetup.Plan(terrain);}catch(InvalidOperationException){refused=true;}Check(refused,"foreign metadata rejected before writes");}
            finally{importer.userData=data;}
            GrasslandRevitalizationSetup.Rebuild();
            int changed=0,removed=0;
            for(int i=0;i<edited.Length;i++)
            {
                var m=AssetDatabase.LoadAssetAtPath<Mesh>(edited[i]);var shape=m.vertices;var normals=m.normals;var paint=m.colors;
                Check(shape.Length==v[i].Length,"original vertex count");for(int ch=0;ch<8;ch++)Check(uv[i][ch].SequenceEqual(UV(m,ch)),"all original UV0-7 retained");
                Check(plan[i].Triangles.SequenceEqual(m.triangles),"only planned whole-plant index removal");removed+=(tri[i].Length-m.triangles.Length)/3;
                for(int j=0;j<shape.Length;j++)
                {
                    if(!plan[i].Changed[j])Check(shape[j]==v[i][j]&&normals[j]==n[i][j]&&paint[j]==c[i][j],"original trunks and unselected art retained");
                    else if(edited[i].Contains("Landscape")){Check(c[i][j].g-c[i][j].r>.065f&&Vector3.Distance(shape[j],v[i][j])<.7f,"bounded leaf-only shape change");changed++;}
                    else Check(shape[j]==v[i][j]&&normals[j]==n[i][j]&&paint[j]==c[i][j],"thinning retains all source channels");
                }
                Check(AssetImporter.GetAtPath(edited[i]).userData.StartsWith(GrasslandRevitalizationSetup.Marker,StringComparison.Ordinal),"completion marker without allocating UV");
            }
            terrain=AssetDatabase.LoadAssetAtPath<GameObject>(TerrainSetup.PrefabPath).GetComponent<IslandTerrain>();
            Check(collisions.SequenceEqual(terrain.GetComponentsInChildren<Collider>(true).Select(o=>o.name+":"+EditorJsonUtility.ToJson(o))),"entire original physical world unchanged");
            int[] stats=VerifyAssets();var newFiles=Directory.GetFiles("Assets/Art","*",SearchOption.AllDirectories).Select(p=>p.Replace('\\','/')).Except(files).ToArray();
            var watched=edited.Concat(edited.Select(p=>p+".meta")).Concat(newFiles).Concat(new[]{TerrainSetup.PrefabPath,roadPath,roadPath+".meta"}).ToArray();var bytes=watched.Select(File.ReadAllBytes).ToArray();
            GrasslandRevitalizationSetup.Rebuild();PrototypeAddressablesSetup.EnsureSetup();
            for(int i=0;i<watched.Length;i++)Check(bytes[i].SequenceEqual(File.ReadAllBytes(watched[i])),"repeat and Setup preserve authored files "+watched[i]);
            try
            {
                foreach(string path in new[]{edited[0],newFiles.First(p=>p.EndsWith(".asset",StringComparison.Ordinal))})
                {var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);var shape=mesh.vertices;shape[0]+=Vector3.up*.01f;mesh.vertices=shape;var paint=mesh.colors;paint[0]=Color.magenta;mesh.colors=paint;EditorUtility.SetDirty(mesh);}
                AssetDatabase.SaveAssets();var manual=watched.Select(File.ReadAllBytes).ToArray();GrasslandRevitalizationSetup.Rebuild();PrototypeAddressablesSetup.EnsureSetup();
                for(int i=0;i<watched.Length;i++)Check(manual[i].SequenceEqual(File.ReadAllBytes(watched[i])),"later manual shape/palette retained");
            }
            finally{for(int i=0;i<watched.Length;i++)File.WriteAllBytes(watched[i],bytes[i]);foreach(string path in watched.Where(p=>!p.EndsWith(".meta",StringComparison.Ordinal)))AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);}
            for(int i=0;i<protectedPaths.Length;i++)Check(hashes[i]==Hash(protectedPaths[i]),"unrelated existing art remains "+protectedPaths[i]);
            var report=new Report{editedMeshes=edited.Concat(new[]{roadPath}).ToArray(),newFiles=newFiles,clumps=stats[0],flowers=stats[1],batches=stats[2],triangles=stats[3],treeVertices=changed,removedTriangles=removed};
            Directory.CreateDirectory("Logs/LivingGrassland");File.WriteAllText("Logs/LivingGrassland/Bake.json",JsonUtility.ToJson(report,true));
            Debug.Log("Living grassland bake verification PASSED: dense bounded clumps, canopy/verge edits, original geometry/UV/trunks/collision, custom metadata guard, unrelated art, repeat/Setup/manual retention.");
        }
        public static int[] VerifyAssets()
        {
            var root=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);
            try
            {
                var terrain=root.GetComponent<IslandTerrain>();var group=terrain.World.Scenery.Find(GrasslandRevitalizationSetup.Group);
                Check(group!=null&&group.GetComponentsInChildren<Collider>(true).Length==0&&group.GetComponentsInChildren<MonoBehaviour>(true).Length==0,"merged visual-only geometry");
                var road=terrain.World.transform.Find("Exploration paths");var roadMesh=road.GetComponent<MeshFilter>().sharedMesh;var roadMaterials=road.GetComponent<Renderer>().sharedMaterials;
                Check(roadMesh.subMeshCount==2&&roadMaterials.Length==2&&roadMaterials[0]==AssetDatabase.LoadAssetAtPath<Material>(BeachCompositionSetup.MaterialPath)&&roadMaterials[1].shader.name=="MagicIsland/Meadow Trail","only grassland road faces change material");
                var roadPoints=roadMesh.vertices;Check(roadMesh.GetTriangles(1).All(i=>GrasslandRevitalizationSetup.Mask(roadPoints[i].x,roadPoints[i].z)>.15f),"all reassigned road faces inside grassland");
                var batches=terrain.SeasonalDetails.MeadowBatches;Check(batches.Length>0&&batches.Length<=64&&batches.Distinct().Count()==batches.Length,"bounded distinct renderer ownership");
                var roots=new HashSet<Vector2>();int triangles=0,flowers=0;
                foreach(var renderer in batches)
                {
                    Check(renderer.transform.IsChildOf(group)&&renderer.gameObject.layer==2&&(renderer.sharedMaterial.shader.name=="MagicIsland/Meadow Cover"||renderer.sharedMaterial.shader.name==GrasslandFloraSetup.ShaderName)&&!ShaderUtil.ShaderHasError(renderer.sharedMaterial.shader),"AA shared material and noninteractive hierarchy");
                    var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;var points=mesh.vertices;var homes=mesh.uv2;var floor=mesh.uv3;var weights=mesh.uv;
                    Check(mesh.colors.Length==points.Length&&homes.Length==points.Length&&floor.Length==points.Length&&weights.Length==points.Length&&mesh.normals.All(p=>p.sqrMagnitude>.9f),"plant/wind/cut metadata and normals");
                    for(int i=0;i<points.Length;i++)
                    {
                        var p=points[i];Check(!float.IsNaN(p.x)&&!float.IsNaN(p.y)&&!float.IsNaN(p.z)&&!float.IsInfinity(p.y),"finite geometry");roots.Add(homes[i]);
                        Check(Vector2.Distance(homes[i],new Vector2(p.x,p.z))<=1.2f&&GrasslandRevitalizationSetup.Mask(p.x,p.z)>.1f&&StarterIslandLayout.RouteDistance(p.x,p.z)>2.45f,"full clump footprint respects water/path envelope");
                        Check(weights[i].x>=0&&weights[i].x<=1,"bounded shared wind");
                    }
                    if(renderer.name.StartsWith("Living meadow ",StringComparison.Ordinal))flowers+=homes.GroupBy(p=>p).Count(g=>g.Count()>1000);
                    triangles+=mesh.triangles.Length/3;
                }
                Check(roots.Count>1200,"grassland interior filled with independent baked roots");
                var texture=(TextureImporter)AssetImporter.GetAtPath(GrasslandRevitalizationSetup.MapPath);Check(!texture.sRGBTexture&&!texture.mipmapEnabled&&texture.wrapMode==TextureWrapMode.Clamp,"world field sampled as linear clamped data");
                for(int i=0;i<128;i++){var cut=MeadowCoverPresentation.Cut(new Vector3(i*4,0,i*3));Check(MeadowCoverPresentation.Covered(new Vector2(i*4+1.7f,i*3),cut)&&!MeadowCoverPresentation.Covered(new Vector2(i*4+2,i*3),cut),"complete plant clearance and bounded edge");}
                return new[]{roots.Count,flowers,batches.Length,triangles};
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        public static void VerifyRuntime(GameManager g)
        {
            var cover=g.Terrain.Terrain.SeasonalDetails;Check(cover.IsValid&&cover.MeadowBatches.Length>0&&cover.MeadowBatches.All(r=>r.enabled),"Packed AA cover bound and visible");
            foreach(var renderer in cover.MeadowBatches)
            {
                var expected=AssetDatabase.LoadAssetAtPath<Mesh>(StarterIslandWorldSetup.Folder+"/"+renderer.name+".asset");var actual=renderer.GetComponent<MeshFilter>().sharedMesh;
                Func<Mesh,Vector3[]> points=m=>m.vertices.OrderBy(p=>p.x).ThenBy(p=>p.y).ThenBy(p=>p.z).ToArray();
                Check(expected.vertexCount==actual.vertexCount&&expected.triangles.Length==actual.triangles.Length&&points(expected).SequenceEqual(points(actual)),"actual Packed geometry equals baked source");
            }
            var sample=cover.MeadowBatches[0];var block=new MaterialPropertyBlock();sample.GetPropertyBlock(block);float count=block.GetFloat("_MeadowCutCount");
            cover.Present(g.Calendar.Date.Season,false);Check(cover.MeadowBatches.All(r=>!r.enabled),"inside hides entire cover");
            cover.Present(g.Calendar.Date.Season,true);Check(cover.MeadowBatches.All(r=>r.enabled),"outside restores cover");sample.GetPropertyBlock(block);Check(count==block.GetFloat("_MeadowCutCount"),"indoor and pause do not mutate cuts");
            GrasslandCompositionVerification.VerifyRuntime(g);GrasslandFinishVerification.VerifyRuntime(g);
            Debug.Log("Living grassland Packed verification PASSED: source meshes, route walking, indoor/outdoor and cut state.");
        }
        static byte[] saveBefore;
        public static void VerifyManualVisibility()
        {
            var go=new GameObject("Meadow visibility fixture",typeof(MeshRenderer));
            try
            {
                var renderer=go.GetComponent<Renderer>();renderer.enabled=false;var block=new MaterialPropertyBlock();block.SetFloat("_ArtistTag",19);renderer.SetPropertyBlock(block);
                var presentation=new MeadowCoverPresentation(new[]{renderer});presentation.Present(true);Check(!renderer.enabled,"authored disabled batch remains disabled outside");
                presentation.Present(false);presentation.Clear();renderer.GetPropertyBlock(block);Check(!renderer.enabled&&block.GetFloat("_ArtistTag")==19,"original disabled state and complete artist properties restored");
                presentation.Present(true);Check(!renderer.enabled,"cleared presentation cannot revive renderer");
            }
            finally{Object.DestroyImmediate(go);}
            Debug.Log("Living grassland manual visibility verification PASSED.");
        }
        public static void BeginPlacement(GameManager g)
        {
            saveBefore=File.Exists(g.Saves.SavePath)?File.ReadAllBytes(g.Saves.SavePath):null;
            string item=SeasonalGroundVerification.Placeable(g);Check(g.Inventory.TryAdd(item,1),"fixture inventory");g.Placements.StartCoroutine(g.Placements.Begin(item));
        }
        static byte[] Shot(Camera camera,Vector3 center)
        {
            var position=camera.transform.position;float size=camera.orthographicSize;var target=camera.targetTexture;var active=RenderTexture.active;
            var rt=new RenderTexture(640,360,24);var texture=new Texture2D(640,360,TextureFormat.RGB24,false);
            try{camera.orthographicSize=2.2f;camera.transform.position=center-camera.transform.forward*35;camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,640,360),0,0);texture.Apply();return texture.EncodeToPNG();}
            finally{camera.transform.position=position;camera.orthographicSize=size;camera.targetTexture=target;RenderTexture.active=active;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(texture);}
        }
        public static void VerifyPlacement(GameManager g)
        {
            var cover=g.Terrain.Terrain.SeasonalDetails;var player=g.Players.Player;var oldPosition=player.transform.position;
            Vector2 chosen=default;Renderer batch=null;
            foreach(var r in cover.MeadowBatches.Where(r=>r.name.StartsWith("Living meadow ",StringComparison.Ordinal)))
            {
                foreach(var p in r.GetComponent<MeshFilter>().sharedMesh.uv2.Distinct())
                {
                    int x=Mathf.RoundToInt(p.x),z=Mathf.RoundToInt(p.y);if(!StarterIslandLayout.CanPlace(x,z))continue;
                    g.Players.RestorePosition(new Vector3(x,(float)StarterIslandLayout.Ground(x,z)+.1f,z-1.7f));Physics.SyncTransforms();g.Placements.SetCandidate(x,z);
                    if(g.Placements.CheckCandidate()!=PlacementResult.Success)continue;chosen=p;batch=r;break;
                }
                if(batch!=null)break;
            }
            Check(batch!=null,"actual grass-covered buildable target");var original=new MaterialPropertyBlock();batch.GetPropertyBlock(original);
            Check(g.Placements.Confirm(),"actual committed placement");var block=new MaterialPropertyBlock();batch.GetPropertyBlock(block);var cuts=block.GetVectorArray("_MeadowCuts");
            Check(block.GetFloat("_MeadowCutCount")>=1&&cuts.Take((int)block.GetFloat("_MeadowCutCount")).Any(c=>MeadowCoverPresentation.Covered(chosen,c)),"commit hides entire intersecting clump");
            var actor=g.Placements.Objects.Values.Single();var renderers=actor.GetComponentsInChildren<Renderer>();var enabled=renderers.Select(r=>r.enabled).ToArray();
            try
            {
                foreach(var r in renderers)r.enabled=false;
                var center=new Vector3(chosen.x,(float)StarterIslandLayout.Ground(chosen.x,chosen.y)+.4f,chosen.y);byte[] hidden=Shot(player.ViewCamera,center);
                var control=new MaterialPropertyBlock();batch.GetPropertyBlock(control);control.SetFloat("_MeadowCutCount",0);batch.SetPropertyBlock(control);byte[] visible=Shot(player.ViewCamera,center);batch.SetPropertyBlock(block);
                Check(!hidden.SequenceEqual(visible),"actual GPU removes plants with fixed object/camera/clock");
                File.WriteAllBytes("Logs/LivingGrassland/PlacementHidden.png",hidden);File.WriteAllBytes("Logs/LivingGrassland/PlacementVisible.png",visible);
            }
            finally{batch.SetPropertyBlock(block);for(int i=0;i<renderers.Length;i++)renderers[i].enabled=enabled[i];}
            cover.Bind(g.Placements,g.Gathering.State,g.Terrain.Terrain.Paths,g.Terrain.Terrain,g.Animals);cover.Present(g.Calendar.Date.Season,true);batch.GetPropertyBlock(block);
            Check(block.GetFloat("_MeadowCutCount")>=1,"rebinding restored construction keeps cuts");
            Check(g.Placements.Recover(actor),"actual recovery succeeds");batch.GetPropertyBlock(block);Check(block.GetFloat("_MeadowCutCount")==0,"recover restores cover immediately");
            var originals=cover.MeadowBatches.Select(r=>{var b=new MaterialPropertyBlock();r.GetPropertyBlock(b);return b;}).ToArray();
            cover.Unbind();Check(cover.MeadowBatches.All(r=>r.enabled),"unbind restores authored renderer state");
            for(int i=0;i<cover.MeadowBatches.Length;i++){var b=new MaterialPropertyBlock();cover.MeadowBatches[i].GetPropertyBlock(b);Check(b.isEmpty,"unbind restores original property blocks");}
            cover.Bind(g.Placements,g.Gathering.State,g.Terrain.Terrain.Paths,g.Terrain.Terrain,g.Animals);cover.Present(g.Calendar.Date.Season,true);g.Players.RestorePosition(oldPosition);
            Check(saveBefore==null?!File.Exists(g.Saves.SavePath):saveBefore.SequenceEqual(File.ReadAllBytes(g.Saves.SavePath)),"daytime scenery/placement does not save");Debug.Log("Living grassland construction verification PASSED: real commit/recovery, GPU plant masking, old construction bind, property cleanup and no daytime save.");
        }
    }
}
