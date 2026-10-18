using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MagicIsland.Interaction;
using MagicIsland.Ocean;
using MagicIsland.TerrainSystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    // Explicit addition to existing authored flower habitats. No new geography or runtime objects.
    public static class GrasslandEcologySetup
    {
        public const string Group="Grassland insect habitats",Source="SourceArt/GrasslandEcology/GrasslandEcology.meshdata.json";
        public const string MaterialFolder="Assets/Art/Materials/Terrain/",Folder=GrasslandCompositionSetup.Folder;
        public const string FoliagePath=MaterialFolder+"Meadow Foliage.mat";
        [Serializable] public sealed class Template{public string name;public float[] positions,normals,colors,wings;public int[] triangles;}
        [Serializable] public sealed class Export{public int version;public Template[] templates;}
        public static void Require(bool ok,string why){if(!ok)throw new InvalidOperationException("Grassland ecology: "+why);}
        public static Vector2[] Sites()=>GrasslandCompositionSetup.Sites().Where((p,i)=>i%3==0)
            .Concat(GrasslandFinishSetup.Sites().Where(p=>p.Kind!=2).Where((p,i)=>i%3==0).Select(p=>p.Point)).ToArray();
        public static MeshFilter[] Foliage(IslandTerrain terrain)=>new[]{GrasslandCompositionSetup.Group,GrasslandFinishSetup.Group}
            .SelectMany(name=>terrain.World.Scenery.Find(name).GetComponentsInChildren<MeshFilter>(true)).ToArray();
        public static bool IsFoliageMaterial(Material material)=>material==AssetDatabase.LoadAssetAtPath<Material>(BeachCompositionSetup.MaterialPath)
            ||material==AssetDatabase.LoadAssetAtPath<Material>(FoliagePath);
        public static Vector2 Home(Vector2 p,Vector2[] sites)
        {var home=sites[0];float best=(home-p).sqrMagnitude;for(int i=1;i<sites.Length;i++){float d=(sites[i]-p).sqrMagnitude;if(d<best){best=d;home=sites[i];}}return home;}
        public static float Ground(IslandTerrain terrain,Vector2 p)
        {
            Require(terrain.World.Raycast(new Ray(terrain.transform.TransformPoint(new Vector3(p.x,300,p.y)),Vector3.down),out var hit,350),"baked ground at "+p);
            return terrain.transform.InverseTransformPoint(hit.point).y;
        }
        public static Dictionary<Mesh,Vector2[]> WindPlan(IslandTerrain terrain)
        {
            var sites=GrasslandCompositionSetup.Sites().Concat(GrasslandFinishSetup.Sites().Select(p=>p.Point)).ToArray();var result=new Dictionary<Mesh,Vector2[]>();
            var palette=new[]{new Color(.34f,.53f,.27f),new Color(.60f,.72f,.36f),new Color(.99f,.91f,.65f),new Color(.68f,.60f,.86f),new Color(.90f,.59f,.23f)};
            foreach(var f in Foliage(terrain))
            {
                var mesh=f.sharedMesh;Require(f.GetComponent<Renderer>().sharedMaterial==AssetDatabase.LoadAssetAtPath<Material>(BeachCompositionSetup.MaterialPath),"preserve custom foliage material "+f.name);
                Require(mesh.uv.Length==0,"preserve custom UV0 "+mesh.name);var v=mesh.vertices;var c=mesh.colors;var uv=new Vector2[v.Length];
                for(int i=0;i<v.Length;i++)
                {
                    var p=new Vector2(v[i].x,v[i].z);var home=Home(p,sites);float weight=0;
                    if(palette.Contains(c[i]))
                    {
                        float height=Mathf.Clamp01((v[i].y-Ground(terrain,p)-.025f)/1.1f);
                        // Keep the entire wind envelope inside the existing 1.05m reserved footprint.
                        weight=Mathf.Sqrt(Mathf.Min(height*height,Mathf.Max(0,(1.05f-Vector2.Distance(p,home)-.02f)/.1f)));
                    }
                    uv[i]=new Vector2(weight,home.x*.71f+home.y*.43f);
                }
                result.Add(mesh,uv);
            }
            return result;
        }
        sealed class Batch
        {
            public readonly List<Vector3> V=new List<Vector3>(),N=new List<Vector3>();public readonly List<Color>C=new List<Color>();
            public readonly List<Vector2> A=new List<Vector2>(),B=new List<Vector2>(),D=new List<Vector2>();public readonly List<int>T=new List<int>();
            public Mesh Mesh(string name)
            {
                var mesh=new Mesh{name=name,indexFormat=V.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};mesh.SetVertices(V);mesh.SetNormals(N);mesh.SetColors(C);mesh.SetUVs(0,A);mesh.SetUVs(1,B);mesh.SetUVs(2,D);mesh.SetTriangles(T,0);mesh.RecalculateBounds();
                var bounds=mesh.bounds;bounds.Expand(new Vector3(2.1f,2,2.1f));mesh.bounds=bounds;return mesh;
            }
        }
        public static Dictionary<string,Mesh> Prepare(IslandTerrain terrain)
        {
            var data=JsonUtility.FromJson<Export>(File.ReadAllText(Source));Require(data?.version==1&&data.templates?.Length==2,"Blender source");
            foreach(var t in data.templates)Require(t.positions.Length>0&&t.positions.Length%3==0&&t.normals.Length==t.positions.Length&&t.colors.Length==t.positions.Length/3*4&&t.wings.Length==t.positions.Length/3
                &&t.positions.Concat(t.normals).Concat(t.colors).Concat(t.wings).All(f=>!float.IsNaN(f)&&!float.IsInfinity(f))&&t.triangles.Length%3==0&&t.triangles.All(i=>i>=0&&i<t.wings.Length),"source channels");
            var batches=new Dictionary<string,Batch>();int site=0;
            foreach(var p in Sites())
            {
                Require(BiomePolishSetup.Clear(p,1.05f),"existing complete habitat footprint");float floor=Ground(terrain,p);
                foreach(string kind in new[]{"Butterfly","Firefly"})
                {
                    string key="Meadow "+kind+" "+Mathf.FloorToInt(p.x/256)+" "+Mathf.FloorToInt(p.y/256);if(!batches.TryGetValue(key,out var batch)){batch=new Batch();batches.Add(key,batch);}
                    var template=data.templates.Single(t=>t.name==kind);int count=kind=="Butterfly"?2:4;
                    for(int member=0;member<count;member++)
                    {
                        int first=batch.V.Count;float phase=site*2.399963f+member*1.83f;var center=new Vector3(p.x,floor+(kind=="Butterfly"?.95f:.58f)+member*.14f,p.y);
                        float scale=kind=="Butterfly"?(member==0?1:.82f):1;
                        for(int i=0;i<template.wings.Length;i++)
                        {
                            var q=new Vector3(template.positions[i*3],template.positions[i*3+1],template.positions[i*3+2])*scale;
                            Require(q.magnitude<.32f,"bounded insect model");batch.V.Add(center+q);batch.N.Add(new Vector3(template.normals[i*3],template.normals[i*3+1],template.normals[i*3+2]));
                            batch.C.Add(new Color(template.colors[i*4],template.colors[i*4+1],template.colors[i*4+2],1));
                            batch.A.Add(p);batch.B.Add(new Vector2(center.y,phase));batch.D.Add(new Vector2(template.wings[i],phase));
                        }
                        batch.T.AddRange(template.triangles.Select(i=>first+i));
                    }
                }
                site++;
            }
            return batches.ToDictionary(p=>p.Key,p=>p.Value.Mesh(p.Key));
        }
        static Material Material(string name,string shader,int mode=0)
        {
            string path=MaterialFolder+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);if(material!=null)return material;
            Require(!File.Exists(path)&&!File.Exists(path+".meta"),"preserve orphan material");var s=Shader.Find(shader);Require(s!=null&&!ShaderUtil.ShaderHasError(s),"compiled shader "+shader);
            material=new Material(s){name=name};if(material.HasProperty("_EcologyMode"))material.SetFloat("_EcologyMode",mode);AssetDatabase.CreateAsset(material,path);return material;
        }
        static void Clean(string path)=>File.WriteAllText(path,string.Join("\n",File.ReadAllLines(path).Select(s=>s.TrimEnd()))+"\n");
        [MenuItem("Magic Island/Art/Add Grassland Ecology (Explicit)")]
        public static void Rebuild()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling,"Edit Mode required");
            var root=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);Dictionary<string,Mesh> meshes=null;
            try
            {
                var terrain=root.GetComponent<IslandTerrain>();var ecology=terrain.World.GetComponentInChildren<WorldWaterEcology>(true);Require(ecology!=null,"existing unified ecology owner");
                if(ecology.transform.Find(Group)!=null){Require(ecology.GrasslandAmbient.Length>0&&ecology.GrasslandAmbient.All(r=>r!=null),"preserve incomplete/custom ecology");Debug.Log("Grassland ecology and manual edits retained.");return;}
                var wind=WindPlan(terrain);meshes=Prepare(terrain);
                foreach(string name in meshes.Keys)Require(!File.Exists(Folder+"/"+name+".asset")&&!File.Exists(Folder+"/"+name+".asset.meta"),"preserve orphan mesh "+name);
                var butterfly=Material("Meadow Butterflies","MagicIsland/Meadow Insects");var firefly=Material("Meadow Fireflies","MagicIsland/Meadow Insects",1);var foliage=Material("Meadow Foliage","MagicIsland/Meadow Foliage");
                var group=new GameObject(Group);group.layer=2;group.transform.SetParent(ecology.transform,false);var ambient=new List<Renderer>();
                foreach(var pair in meshes)
                {
                    AssetDatabase.CreateAsset(pair.Value,Folder+"/"+pair.Key+".asset");var go=new GameObject(pair.Key,typeof(MeshFilter),typeof(MeshRenderer));go.layer=2;go.transform.SetParent(group.transform,false);
                    go.GetComponent<MeshFilter>().sharedMesh=pair.Value;var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=pair.Key.Contains("Butterfly")?butterfly:firefly;renderer.shadowCastingMode=ShadowCastingMode.Off;ambient.Add(renderer);
                }
                foreach(var pair in wind){pair.Key.uv=pair.Value;var bounds=pair.Key.bounds;bounds.Expand(.22f);pair.Key.bounds=bounds;EditorUtility.SetDirty(pair.Key);}
                foreach(var f in Foliage(terrain)){var renderer=f.GetComponent<Renderer>();renderer.sharedMaterial=foliage;ambient.Add(renderer);}
                ecology.GrasslandAmbient=ambient.ToArray();PrefabUtility.SaveAsPrefabAsset(root,TerrainSetup.PrefabPath);AssetDatabase.SaveAssets();Clean(TerrainSetup.PrefabPath);
                foreach(var mesh in wind.Keys)Clean(AssetDatabase.GetAssetPath(mesh));foreach(string key in meshes.Keys)Clean(Folder+"/"+key+".asset");
                foreach(string name in new[]{"Meadow Butterflies","Meadow Fireflies","Meadow Foliage"})Clean(MaterialFolder+name+".mat");
                Debug.Log("Grassland ecology baked: "+Sites().Length+" habitats, "+meshes.Count+" insect batches and "+wind.Count+" existing foliage batches.");
            }
            finally{if(meshes!=null)foreach(var m in meshes.Values)if(!AssetDatabase.Contains(m))Object.DestroyImmediate(m);PrefabUtility.UnloadPrefabContents(root);}
        }
    }
}
