using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MagicIsland.Interaction;
using MagicIsland.TerrainSystem;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    public static class GrasslandIslandCoastSetup
    {
        public const string Marker="MagicIsland.GrasslandIslandCoast.v1";
        public sealed class Edit
        {
            public Mesh Source,Result;
            public string Path,Name;
            public int X,Z;
        }
        static void Check(bool ok,string why){if(!ok)throw new InvalidOperationException("Grassland coast: "+why);}
        public static Edit[] Plan(IslandTerrain terrain)
        {
            Check(terrain!=null&&terrain.World?.ArtVersion==3,"requires the authored biome islands");
            if(terrain.World.GrasslandCoastArtVersion==1)return Array.Empty<Edit>();
            Check(terrain.World.GrasslandCoastArtVersion==0,"unknown coast art version");
            var group=terrain.World.transform.Find("Grassland island");Check(group!=null,"grassland ground group");
            var colliders=group.GetComponentsInChildren<MeshCollider>(true).ToDictionary(c=>c.name);
            var plan=new List<Edit>();
            var visited=new HashSet<string>();
            try
            {
                for(int x=-448;x<448;x+=64)for(int z=-64;z<1088;z+=64)
                {
                    string name="Grassland "+x+" "+z,path=BiomeIslandsSetup.Folder+"/"+name+".asset";
                    colliders.TryGetValue(name,out var collider);Mesh source=collider?.sharedMesh;
                    if(source!=null)
                    {
                        visited.Add(name);
                        Check(AssetDatabase.GetAssetPath(source)==path&&collider.GetComponent<MeshFilter>()?.sharedMesh==source,"ground ownership "+name);
                        Check(collider.transform.localPosition==Vector3.zero&&collider.transform.localRotation==Quaternion.identity&&collider.transform.localScale==Vector3.one,"preserve transformed ground "+name);
                        Check(string.IsNullOrEmpty(AssetImporter.GetAtPath(path).userData),"preserve custom mesh metadata "+name);
                    }
                    else Check(!File.Exists(path),"unattached existing mesh "+name);
                    var result=Build(source,x,z);
                    if(result!=null)plan.Add(new Edit{Source=source,Result=result,Path=path,Name=name,X=x,Z=z});
                }
                Check(colliders.Keys.All(visited.Contains),"all original grassland chunks retained");
                return plan.ToArray();
            }
            catch{foreach(var edit in plan)Object.DestroyImmediate(edit.Result);throw;}
        }
        [MenuItem("Magic Island/Art/Refine Grassland Island Coast (Explicit)")]
        public static void Rebuild()
        {
            Check(!EditorApplication.isPlaying,"stop Play before authoring");
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(TerrainSetup.PrefabPath).GetComponent<IslandTerrain>();
            var plan=Plan(asset);if(plan.Length==0){Debug.Log("Grassland island coast already authored; retaining manual edits.");return;}
            var originals=new Dictionary<string,byte[]>();var created=new List<string>();bool committed=false;
            foreach(string path in plan.Where(p=>p.Source!=null).SelectMany(p=>new[]{p.Path,p.Path+".meta"}).Concat(new[]{TerrainSetup.PrefabPath}))originals.Add(path,File.ReadAllBytes(path));
            GameObject root=null;
            try
            {
                root=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);var terrain=root.GetComponent<IslandTerrain>();var world=terrain.World;
                var group=world.transform.Find("Grassland island");var grounds=world.Grounds.ToList();
                var material=group.GetComponentInChildren<MeshRenderer>(true).sharedMaterial;
                foreach(var edit in plan)
                {
                    Mesh mesh;
                    if(edit.Source!=null){EditorUtility.CopySerialized(edit.Result,edit.Source);EditorUtility.SetDirty(edit.Source);mesh=edit.Source;}
                    else
                    {
                        edit.Result.name=edit.Name;created.Add(edit.Path);AssetDatabase.CreateAsset(edit.Result,edit.Path);mesh=edit.Result;
                        var go=new GameObject(edit.Name,typeof(MeshFilter),typeof(MeshRenderer),typeof(MeshCollider));go.transform.SetParent(group,false);
                        go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=material;
                        var collider=go.GetComponent<MeshCollider>();collider.sharedMesh=mesh;grounds.Add(collider);
                    }
                    var importer=AssetImporter.GetAtPath(edit.Path);importer.userData=Marker;EditorUtility.SetDirty(importer);importer.SaveAndReimport();
                }
                world.Grounds=grounds.ToArray();world.GrasslandCoastArtVersion=1;
                Check(terrain.IsValid,"all AA references before publishing");
                Check(PrefabUtility.SaveAsPrefabAsset(root,TerrainSetup.PrefabPath)!=null,"save terrain prefab");AssetDatabase.SaveAssets();committed=true;
                Debug.Log("Grassland island coast baked: "+plan.Count(p=>p.Source!=null)+" original ground chunks refined, "+created.Count+" additional coastal/seabed chunks; interior art and other islands retained.");
            }
            finally
            {
                if(root!=null)PrefabUtility.UnloadPrefabContents(root);
                if(!committed)
                {
                    foreach(string path in created)AssetDatabase.DeleteAsset(path);
                    foreach(var entry in originals)File.WriteAllBytes(entry.Key,entry.Value);
                    foreach(string path in originals.Keys.Where(p=>!p.EndsWith(".meta",StringComparison.Ordinal)))AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
                }
                foreach(var edit in plan)if(edit.Result!=null&&!EditorUtility.IsPersistent(edit.Result))Object.DestroyImmediate(edit.Result);
            }
        }
        static Mesh Build(Mesh source,int x,int z)
        {
            const int n=17;var v=source!=null?source.vertices:new Vector3[n*n];var colors=source!=null?source.colors:new Color[n*n];
            var uv=new List<Vector4>();if(source!=null)source.GetUVs(0,uv);else for(int i=0;i<n*n;i++)uv.Add(Vector4.zero);
            Check(v.Length==n*n&&colors.Length==v.Length&&uv.Count==v.Length,"preserve custom vertex layout");
            var triangles=new List<int>();var oldTriangles=new List<int>();bool changed=source==null;
            for(int i=0;i<n;i++)for(int j=0;j<n;j++)
            {
                int k=i*n+j;float px=x+i*4,pz=z+j*4;
                double old=BiomeIslandLayout.LegacyDistance(IslandBiome.Grassland,px,pz);
                if(source!=null)
                {
                    Check(v[k].x==px&&v[k].z==pz,"preserve custom ground coordinates");
                    if(old< -3)
                    {
                        Check(Mathf.Abs(v[k].y-(float)BiomeIslandLayout.LegacyHeight(px,pz))<.0001f,"preserve custom seabed height");
                        if(!SupportsOldConstruction(x+i*4,z+j*4))
                        {float y=(float)GrasslandIslandCoast.Height(px,pz);changed|=v[k].y!=y;v[k].y=y;}
                    }
                }
                else v[k]=new Vector3(px,(float)GrasslandIslandCoast.Height(px,pz),pz);
                if(old<68)
                {
                    float before=Mathf.SmoothStep(0,1,Mathf.InverseLerp(3,24,(float)old)),after=(float)GrasslandIslandCoast.GrassWeight(px,pz);
                    Color previous=Tint(px,pz,before),next=Tint(px,pz,after);
                    colors[k]=source!=null?colors[k]+(next-previous):next;
                    var a=uv[k];a.x=source!=null?a.x+(after-before):after;uv[k]=a;changed|=Mathf.Abs(after-before)>.000001f;
                }
            }
            for(int i=0;i<n-1;i++)for(int j=0;j<n-1;j++)
            {
                int a=i*n+j,b=a+n;int[] quad={a,a+1,b,b,a+1,b+1};
                if(BiomeIslandLayout.LegacyDistance(IslandBiome.Grassland,x+i*4+2,z+j*4+2)>=-28)oldTriangles.AddRange(quad);
                // Keep the legacy village/reef habitat entirely outside the new seabed.
                if(z+j*4+2>=100&&GrasslandIslandCoast.Distance(x+i*4+2,z+j*4+2)>=-112)triangles.AddRange(quad);
            }
            if(source!=null)Check(source.subMeshCount==1&&source.triangles.SequenceEqual(oldTriangles),"preserve custom original ground topology");
            if(triangles.Count==0||!changed)return null;
            var mesh=source!=null?Object.Instantiate(source):new Mesh();mesh.name="Grassland "+x+" "+z;mesh.vertices=v;mesh.colors=colors;
            int dimension=source!=null?source.GetVertexAttributeDimension(UnityEngine.Rendering.VertexAttribute.TexCoord0):2;
            if(dimension==2)mesh.SetUVs(0,uv.Select(p=>new Vector2(p.x,p.y)).ToList());
            else if(dimension==3)mesh.SetUVs(0,uv.Select(p=>new Vector3(p.x,p.y,p.z)).ToList());else mesh.SetUVs(0,uv);
            mesh.triangles=triangles.ToArray();mesh.RecalculateNormals();
            if(source!=null)
            {
                var original=source.normals;var normals=mesh.normals;
                if(original.Length==normals.Length)for(int i=0;i<v.Length;i++)
                    if(BiomeIslandLayout.LegacyDistance(IslandBiome.Grassland,v[i].x,v[i].z)>=68)normals[i]=original[i];
                mesh.normals=normals;
            }
            mesh.RecalculateBounds();return mesh;
        }
        static bool SupportsOldConstruction(int x,int z)
        {
            // Original construction can use the complete old triangle beneath a cell corner.
            // Retain its vertices, including the occasional wet vertex across the triangle diagonal.
            if(BiomeIslandLayout.LegacyDistance(IslandBiome.Grassland,x,z)<-10)return false;
            for(int dx=-4;dx<=4;dx++)for(int dz=-4;dz<=4;dz++)
                if(BiomeIslandLayout.LegacyDistance(IslandBiome.Grassland,x+dx,z+dz)>=5&&BiomeIslandLayout.CanPlace(x+dx,z+dz))return true;
            return false;
        }
        static Color Tint(float x,float z,float grass)
        {
            float variation=(Mathf.PerlinNoise(x*.025f+43,z*.025f+18)-.5f)*.045f;
            return Color.Lerp(new Color(.88f,.79f,.60f),new Color(.60f,.72f,.45f),grass)+new Color(variation,variation,variation,0);
        }
    }
}
