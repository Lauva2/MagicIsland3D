using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MagicIsland.TerrainSystem;
using UnityEditor;
using UnityEngine;

namespace MagicIsland.EditorTools
{
    public static class GrasslandTouchSetup
    {
        public const string Marker="MagicIsland.GrasslandTouch.v1";
        public static readonly Vector2 Center=new Vector2(-124,447);
        public const float Radius=24;
        public sealed class Edit {public Renderer Renderer;public Mesh Mesh;public Vector3[] Vertices,Normals;public bool[] Changed;public string Metadata;public int Blades;}
        public static void Check(bool ok,string why){if(!ok)throw new InvalidOperationException("Grassland touch: "+why);}
        public static bool Done(string value)=>value.Split(';').Contains(Marker);
        static readonly HashSet<string> known=new HashSet<string>{GrasslandRevitalizationSetup.Marker,GrasslandIslandMeadowSetup.Marker,GrasslandGroveSetup.Marker,GrasslandFloraSetup.Marker,GrasslandGroundLayersSetup.Marker,GrasslandShrubStoneSetup.Marker};
        public static MeadowTouchSettings Settings(Renderer[] renderers)=>new MeadowTouchSettings{ArtVersion=1,Center=Center,AreaRadius=Radius,Feather=6,InfluenceRadius=1.05f,BendDistance=.18f,RecoverySeconds=.7f,Spacing=.25f,FlowerFactor=.22f,EnvelopeRadius=1.3f,WaveAmplitude=.065f,WaveLength=18,WaveSpeed=.57f,Renderers=renderers};
        public static Edit[] Plan(IslandTerrain terrain)
        {
            Check(terrain.IsValid&&terrain.World.ArtVersion==3&&terrain.SeasonalDetails.TouchSample.ArtVersion==0,"original islands and fresh sample required");
            Check(terrain.transform.lossyScale==Vector3.one&&terrain.transform.rotation==Quaternion.identity,"preserve custom terrain scale or rotation");
            var result=new List<Edit>();var collisions=new HashSet<Mesh>(terrain.GetComponentsInChildren<MeshCollider>(true).Select(c=>c.sharedMesh));
            var world=new GrasslandRevitalizationSetup.AuthoringWorld(terrain,new Vector4(-180,400,120,100));
            foreach(var renderer in terrain.SeasonalDetails.MeadowBatches)
            {
                var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;var roots=mesh.uv2;var uv=mesh.uv;var v=mesh.vertices;var n=mesh.normals;
                Check(roots.Length==v.Length&&uv.Length==v.Length&&n.Length==v.Length,"complete source root/wind/normal data");
                bool Species(int i)=>(uv[i].x>=-5&&uv[i].x<=-4)||(uv[i].x>=-3&&uv[i].x<=-2);
                if(!Enumerable.Range(0,v.Length).Any(i=>Species(i)&&Vector2.Distance(roots[i],Center)<Radius))continue;
                string path=AssetDatabase.GetAssetPath(mesh),metadata=AssetImporter.GetAtPath(path).userData;
                Check(path.StartsWith(BiomeIslandsSetup.Folder+"/",StringComparison.Ordinal)&&!collisions.Contains(mesh)&&renderer.transform.localToWorldMatrix==terrain.transform.localToWorldMatrix,"owned original visual mesh "+path);
                Check(renderer.sharedMaterials.Length==1&&renderer.sharedMaterial.shader.name==GrasslandFloraSetup.ShaderName,"preserve custom material");
                Check(!string.IsNullOrEmpty(metadata)&&metadata.Split(';').All(known.Contains)&&!Done(metadata),"preserve unknown metadata or partial migration "+path);
                foreach(int i in Enumerable.Range(0,v.Length).Where(i=>Species(i)&&Vector2.Distance(roots[i],Center)<Radius))
                    Check(Vector2.Distance(new Vector2(v[i].x,v[i].z),roots[i])<=1.182f,"known original plant footprint");
                var edit=new Edit{Renderer=renderer,Mesh=mesh,Vertices=v.ToArray(),Normals=n.ToArray(),Changed=new bool[v.Length],Metadata=metadata};
                foreach(var part in BiomePolishSetup.Components(mesh))
                {
                    if(part.Length!=5||part.Any(i=>uv[i].x> -4||uv[i].x< -5))continue;
                    var root=roots[part[0]];if(part.Any(i=>roots[i]!=root)||Vector2.Distance(root,Center)>=Radius-1)continue;
                    var bases=part.Where(i=>Mathf.Abs(uv[i].x+4)<.0001f).ToArray();Check(bases.Length==2,"recognizable anchored folded leaf");
                    var anchor=(v[bases[0]]+v[bases[1]])*.5f;var side=(v[bases[1]]-v[bases[0]]).normalized;
                    float phase=uv[part[0]].y+anchor.x*3.71f+anchor.z*2.13f;
                    float blend=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(Radius-7,Radius-1,Vector2.Distance(root,Center)));
                    float heightScale=Mathf.Lerp(1,.82f+.44f*(.5f+.5f*Mathf.Sin(phase)),blend);
                    float widthScale=Mathf.Lerp(1,.78f+.25f*(.5f+.5f*Mathf.Sin(phase*1.73f)),blend);
                    float maximum=part.Max(i=>Mathf.Max(0,v[i].y-world.Floor(new Vector2(v[i].x,v[i].z))-.018f));Check(maximum>.03f&&maximum<.6f,"known low-grass height");
                    float oldRadius=part.Max(i=>Vector2.Distance(new Vector2(v[i].x,v[i].z),root));
                    foreach(int i in part)
                    {
                        if(bases.Contains(i))continue;var before=v[i];float height=Mathf.Max(0,before.y-world.Floor(new Vector2(before.x,before.z))-.018f),weight=Mathf.Clamp01(height/maximum);
                        var relative=before-anchor;var offset=side*(Vector3.Dot(relative,side)*(widthScale-1)+Mathf.Sin(phase*1.31f)*.045f*weight*weight*blend);
                        var next=new Vector2(before.x+offset.x,before.z+offset.z);var radial=next-root;if(radial.magnitude>oldRadius)next=root+radial.normalized*oldRadius;
                        edit.Vertices[i]=new Vector3(next.x,world.Floor(next)+.018f+height*heightScale,next.y);
                        edit.Normals[i]=new Vector3(n[i].x/widthScale,n[i].y/heightScale,n[i].z/widthScale).normalized;edit.Changed[i]=true;
                        Check(Vector3.Dot(edit.Normals[i],n[i])>.9f,"preserve original double-sided shading");
                    }
                    edit.Blades++;
                }
                result.Add(edit);
            }
            Check(result.Count>0&&result.Sum(e=>e.Blades)>50&&result.Select(e=>e.Mesh).Distinct().Count()==result.Count,"bounded sample with existing low grass");return result.ToArray();
        }
        static void Clean(string path)=>File.WriteAllText(path,string.Join("\n",File.ReadAllLines(path).Select(l=>l.TrimEnd()))+"\n");
        [MenuItem("Magic Island/Art/Prepare Grassland Touch Sample (Explicit)")]
        public static void Rebuild()
        {
            Check(!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling,"Edit Mode required");
            var root=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);var backups=new Dictionary<string,byte[]>();bool saved=false;
            try
            {
                var terrain=root.GetComponent<IslandTerrain>();var settings=terrain.SeasonalDetails.TouchSample;
                if(settings.ArtVersion!=0)
                {
                    Check(settings.ArtVersion==1&&terrain.IsValid&&settings.Renderers.All(r=>Done(AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(r.GetComponent<MeshFilter>().sharedMesh)).userData)),"preserve unknown/incomplete sample settings");
                    Debug.Log("Grassland touch sample and valid manual edits retained.");saved=true;return;
                }
                Check(!terrain.SeasonalDetails.MeadowBatches.Any(r=>Done(AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(r.GetComponent<MeshFilter>().sharedMesh)).userData)),"preserve partial mesh migration");
                var plan=Plan(terrain);
                foreach(string path in plan.Select(e=>AssetDatabase.GetAssetPath(e.Mesh)).SelectMany(p=>new[]{p,p+".meta"}).Concat(new[]{TerrainSetup.PrefabPath}))backups.Add(path,File.ReadAllBytes(path));
                foreach(var e in plan){e.Mesh.vertices=e.Vertices;e.Mesh.normals=e.Normals;var bounds=e.Mesh.bounds;bounds.Expand(.5f);e.Mesh.bounds=bounds;EditorUtility.SetDirty(e.Mesh);}
                terrain.SeasonalDetails.TouchSample=Settings(plan.Select(e=>e.Renderer).ToArray());Check(terrain.IsValid,"complete sample references");
                Check(PrefabUtility.SaveAsPrefabAsset(root,TerrainSetup.PrefabPath)!=null,"prefab saved");AssetDatabase.SaveAssets();
                foreach(var e in plan){string path=AssetDatabase.GetAssetPath(e.Mesh);var importer=AssetImporter.GetAtPath(path);importer.userData=e.Metadata+";"+Marker;importer.SaveAndReimport();Clean(path);Clean(path+".meta");}
                Clean(TerrainSetup.PrefabPath);saved=true;
                Directory.CreateDirectory("Logs/GrasslandTouch");File.WriteAllText("Logs/GrasslandTouch/Stats.json",JsonUtility.ToJson(new Stats{Meshes=plan.Length,Blades=plan.Sum(e=>e.Blades),Vertices=plan.Sum(e=>e.Changed.Count(b=>b))}));
                Debug.Log("Grassland touch sample prepared: "+plan.Length+" original meshes, "+plan.Sum(e=>e.Blades)+" refined low-grass blades; no new objects or materials.");
            }
            finally{PrefabUtility.UnloadPrefabContents(root);if(!saved){foreach(var p in backups)File.WriteAllBytes(p.Key,p.Value);foreach(string p in backups.Keys.Where(p=>!p.EndsWith(".meta",StringComparison.Ordinal)))AssetDatabase.ImportAsset(p,ImportAssetOptions.ForceUpdate);}}
        }
        [Serializable]sealed class Stats{public int Meshes,Blades,Vertices;}
    }
}
