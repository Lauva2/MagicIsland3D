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
    // Explicit authoring operation. Existing mainland artwork remains an unreferenced source prefab.
    public static class BiomeIslandsSetup
    {
        public const string Source="Assets/Art/Prefabs/Environment/BiomeIslandSource.prefab";
        public const string Folder="Assets/Art/Meshes/Terrain/BiomeIslands";
        static readonly Dictionary<string,Mesh> pending=new Dictionary<string,Mesh>();
        [MenuItem("Magic Island/Art/Rebuild Biome Islands (Explicit)")]
        public static void Rebuild()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play before rebuilding biome islands.");
            var existing=AssetDatabase.LoadAssetAtPath<GameObject>(TerrainSetup.PrefabPath).GetComponent<IslandTerrain>();
            if(existing.World?.ArtVersion==3) { Debug.Log("Biome islands already authored; preserving manual edits."); return; }
            if(existing.World==null)throw new InvalidOperationException("The authored terrain source is required.");
            if(!File.Exists(Source)&&!AssetDatabase.CopyAsset(TerrainSetup.PrefabPath,Source))throw new IOException("Cannot preserve source terrain prefab.");
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            pending.Clear();var created=new List<string>();bool published=false;
            var root=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);
            try
            {
                var terrain=root.GetComponent<IslandTerrain>();var world=terrain.World;
                // Use the current hand-authored data, never regenerate the old continent or its bridge.
                var oldGrounds=new HashSet<GameObject>(world.Grounds.Select(c=>c.gameObject));
                var oldWaters=new HashSet<GameObject>(world.Waters.Select(r=>r.gameObject));
                var ecology=world.GetComponentInChildren<WorldWaterEcology>(true);
                var meadowNames=terrain.SeasonalDetails.MeadowBatches.Select(r=>r.name).ToHashSet();
                var trunkSites=new List<Vector2>();
                foreach(var collider in world.GetComponentsInChildren<MeshCollider>(true).Where(c=>!oldGrounds.Contains(c.gameObject)))
                {
                    if(collider.sharedMesh==null)continue;var vertices=collider.sharedMesh.vertices;
                    foreach(var component in BiomePolishSetup.Components(collider.sharedMesh))
                    { var box=BoundsOf(vertices,component,collider.transform,root.transform);trunkSites.Add(new Vector2(box.center.x,box.center.z)); }
                }
                foreach(var go in oldGrounds.Concat(oldWaters).Distinct())Object.DestroyImmediate(go);
                if(ecology!=null)
                {
                    foreach(var habitat in ecology.Habitats)
                    {if(habitat.School!=null)Object.DestroyImmediate(habitat.School.gameObject);if(habitat.Floor!=null)Object.DestroyImmediate(habitat.Floor.gameObject);}
                    ecology.Habitats=Array.Empty<WorldWaterEcology.Habitat>();ecology.LakeSurface=null;ecology.ArtVersion=2;
                }
                foreach(var filter in world.GetComponentsInChildren<MeshFilter>(true))
                {
                    if(filter==null||filter.sharedMesh==null)continue;
                    // Insect/rabbit meshes encode live motion, and require their original authored indices.
                    if(ecology!=null&&filter.transform.IsChildOf(ecology.transform))continue;
                    var mesh=Filter(filter.sharedMesh,filter.transform,root.transform,trunkSites);
                    var collision=filter.GetComponent<MeshCollider>();
                    if(mesh==null){Object.DestroyImmediate(filter.gameObject);continue;}
                    filter.sharedMesh=Store("Art "+filter.name,mesh);if(collision!=null)collision.sharedMesh=filter.sharedMesh;
                }
                foreach(var collider in world.GetComponentsInChildren<MeshCollider>(true).Where(c=>c.GetComponent<MeshFilter>()==null).ToArray())
                {
                    var mesh=Filter(collider.sharedMesh,collider.transform,root.transform,trunkSites);
                    if(mesh==null)Object.DestroyImmediate(collider.gameObject);else collider.sharedMesh=Store("Collision "+collider.name,mesh);
                }
                foreach(var collider in world.GetComponentsInChildren<CapsuleCollider>(true))
                {
                    var p=root.transform.InverseTransformPoint(collider.transform.position);
                    if(!BiomeIslandLayout.Contains(p.x,p.z,36))Object.DestroyImmediate(collider.gameObject);
                }
                world.name="Biome islands";world.ArtVersion=3;world.Waters=Array.Empty<Renderer>();
                var grounds=new List<MeshCollider>();
                var groundMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Terrain/Living Meadow Ground.mat")
                    ??AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Terrain/Biome Ground.mat");
                foreach(var biome in BiomeIslandLayout.Biomes.Where(b=>b!=IslandBiome.Beach))
                {
                    BiomeIslandLayout.Shape(biome,out double x,out double z,out double rx,out double rz);
                    int minX=Mathf.FloorToInt((float)(x-rx*1.3-48)/64)*64,maxX=Mathf.CeilToInt((float)(x+rx*1.3+48)/64)*64;
                    int minZ=Mathf.FloorToInt((float)(z-rz*1.3-48)/64)*64,maxZ=Mathf.CeilToInt((float)(z+rz*1.3+48)/64)*64;
                    var group=new GameObject(biome+" island");group.transform.SetParent(world.transform,false);
                    for(int ix=minX;ix<maxX;ix+=64)for(int iz=minZ;iz<maxZ;iz+=64)
                    {
                        var mesh=Ground(biome,ix,iz);if(mesh==null)continue;
                        string name=biome+" "+ix+" "+iz;var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer),typeof(MeshCollider));go.transform.SetParent(group.transform,false);
                        var asset=Store(name,mesh);go.GetComponent<MeshFilter>().sharedMesh=asset;go.GetComponent<MeshRenderer>().sharedMaterial=groundMaterial;
                        var c=go.GetComponent<MeshCollider>();c.sharedMesh=asset;grounds.Add(c);
                    }
                }
                world.Grounds=grounds.ToArray();
                terrain.Boundary.sharedMesh=Store("Closed village boundary",TerrainSetup.BoundaryMesh());
                terrain.SeasonalDetails.MeadowBatches=world.GetComponentsInChildren<Renderer>(true).Where(r=>meadowNames.Contains(r.name)).ToArray();
                if(ecology!=null)KeepLandEcology(ecology,root.transform,world);
                if(!terrain.IsValid)throw new InvalidOperationException("Incomplete biome island references.");
                foreach(var entry in pending){AssetDatabase.CreateAsset(entry.Value,entry.Key);created.Add(entry.Key);}
                if(PrefabUtility.SaveAsPrefabAsset(root,TerrainSetup.PrefabPath)==null)throw new IOException("Cannot save biome islands prefab.");
                published=true;AssetDatabase.SaveAssets();
                Debug.Log("Biome islands baked: beach village + grassland/desert/woodland/snow, "+grounds.Count+" ground chunks; no continent or connecting bridge.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
                if(!published)foreach(string path in created)AssetDatabase.DeleteAsset(path);
                foreach(var mesh in pending.Values)if(mesh!=null&&!EditorUtility.IsPersistent(mesh))Object.DestroyImmediate(mesh);
                pending.Clear();
            }
        }
        static void KeepLandEcology(WorldWaterEcology ecology,Transform root,StarterIslandWorld world)
        {
            bool Safe(Renderer renderer)
            {
                if(renderer==null)return false;var b=renderer.bounds;
                foreach(float x in new[]{b.min.x,b.max.x})foreach(float z in new[]{b.min.z,b.max.z})
                {var p=root.InverseTransformPoint(new Vector3(x,b.center.y,z));if(!BiomeIslandLayout.Contains(p.x,p.z,48))return false;}
                return true;
            }
            ecology.Ambient=ecology.Ambient.Where(Safe).ToArray();
            ecology.GrasslandAmbient=ecology.GrasslandAmbient.Where(Safe).ToArray();
            // Keep original rabbit indices only when every complete track still has its dry habitat.
            bool rabbits=ecology.RabbitHabitats.Length>0&&ecology.RabbitHabitats.All(h=>h.Track.All(p=>
            {var w=root.InverseTransformPoint(ecology.transform.TransformPoint(p));return BiomeIslandLayout.Contains(w.x,w.z,48);}));
            if(!rabbits){if(ecology.MeadowRabbits!=null)Object.DestroyImmediate(ecology.MeadowRabbits.gameObject);ecology.MeadowRabbits=null;ecology.RabbitHabitats=Array.Empty<MeadowRabbitHabitat>();}
            var keep=new HashSet<GameObject>(ecology.Ambient.Concat(ecology.GrasslandAmbient).Select(r=>r.gameObject));
            if(ecology.MeadowRabbits!=null)keep.Add(ecology.MeadowRabbits.gameObject);
            foreach(var child in ecology.GetComponentsInChildren<MeshFilter>(true).ToArray())if(!keep.Contains(child.gameObject))Object.DestroyImmediate(child.gameObject);
            ecology.AncientTree=world.Scenery.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r=>r.name=="Luminous ancient tree");
            if(!ecology.IsValid)throw new InvalidOperationException("Invalid retained island ecology. Tree="+(ecology.AncientTree!=null)+", ambient="+ecology.Ambient.Length+", insects="+ecology.GrasslandAmbient.Length+", rabbits="+ecology.RabbitHabitats.Length);
        }
        static Bounds BoundsOf(Vector3[] vertices,int[] indices,Transform local,Transform root)
        {
            var b=new Bounds(root.InverseTransformPoint(local.TransformPoint(vertices[indices[0]])),Vector3.zero);
            foreach(int i in indices)b.Encapsulate(root.InverseTransformPoint(local.TransformPoint(vertices[i])));return b;
        }
        static Mesh Filter(Mesh source,Transform local,Transform root,List<Vector2> trunks)
        {
            if(source==null)return null;var vertices=source.vertices;var keep=new bool[vertices.Length];
            foreach(var part in BiomePolishSetup.Components(source))
            {
                var b=BoundsOf(vertices,part,local,root);var center=new Vector2(b.center.x,b.center.z);
                var anchor=center;float closest=196;
                foreach(var trunk in trunks){float d=(trunk-center).sqrMagnitude;if(d<closest){closest=d;anchor=trunk;}}
                if(!BiomeIslandLayout.Contains(anchor.x,anchor.y,56))continue;
                bool safe=true;
                foreach(int i in part)
                {var p=root.InverseTransformPoint(local.TransformPoint(vertices[i]));if(!BiomeIslandLayout.Contains(p.x,p.z,44)){safe=false;break;}}
                if(safe)foreach(int i in part)keep[i]=true;
            }
            var triangles=new List<int>[source.subMeshCount];int total=0;
            for(int sub=0;sub<triangles.Length;sub++)
            {
                var t=source.GetTriangles(sub);var output=triangles[sub]=new List<int>();
                for(int i=0;i<t.Length;i+=3)if(keep[t[i]]&&keep[t[i+1]]&&keep[t[i+2]]){output.Add(t[i]);output.Add(t[i+1]);output.Add(t[i+2]);}total+=output.Count;
            }
            if(total==0)return null;
            // Compact all original channels with a stable index map; source assets/GUIDs are untouched.
            var used=new bool[vertices.Length];foreach(var t in triangles)foreach(int i in t)used[i]=true;
            var map=new int[vertices.Length];var indices=Enumerable.Range(0,vertices.Length).Where(i=>used[i]).ToArray();for(int i=0;i<indices.Length;i++)map[indices[i]]=i;
            var mesh=new Mesh{indexFormat=indices.Length>65535?IndexFormat.UInt32:IndexFormat.UInt16};mesh.vertices=indices.Select(i=>vertices[i]).ToArray();
            var normals=source.normals;if(normals.Length==vertices.Length)mesh.normals=indices.Select(i=>normals[i]).ToArray();
            var colors=source.colors;if(colors.Length==vertices.Length)mesh.colors=indices.Select(i=>colors[i]).ToArray();
            var tangents=source.tangents;if(tangents.Length==vertices.Length)mesh.tangents=indices.Select(i=>tangents[i]).ToArray();
            for(int channel=0;channel<8;channel++){var uv=new List<Vector4>();source.GetUVs(channel,uv);if(uv.Count==vertices.Length)mesh.SetUVs(channel,indices.Select(i=>uv[i]).ToList());}
            mesh.subMeshCount=triangles.Length;for(int sub=0;sub<triangles.Length;sub++)mesh.SetTriangles(triangles[sub].Select(i=>map[i]).ToArray(),sub);
            mesh.RecalculateBounds();return mesh;
        }
        static Mesh Ground(IslandBiome biome,int x,int z)
        {
            const int n=17;var v=new Vector3[n*n];var c=new Color[v.Length];var uv=new Vector2[v.Length];var t=new List<int>();
            Color sand=new Color(.88f,.79f,.60f),grass=new Color(.60f,.72f,.45f),snow=new Color(.87f,.91f,.91f),forest=new Color(.43f,.60f,.41f);
            for(int i=0;i<n;i++)for(int j=0;j<n;j++)
            {
                int k=i*n+j;float px=x+i*4,pz=z+j*4,d=(float)BiomeIslandLayout.Distance(biome,px,pz),inland=Mathf.SmoothStep(0,1,Mathf.InverseLerp(3,24,d));
                var tint=biome==IslandBiome.Desert?sand:biome==IslandBiome.Snow?snow:biome==IslandBiome.Forest?forest:grass;
                float variation=(Mathf.PerlinNoise(px*.025f+43,pz*.025f+18)-.5f)*.045f;
                c[k]=Color.Lerp(sand,tint,inland)+new Color(variation,variation,variation,0);
                v[k]=new Vector3(px,(float)BiomeIslandLayout.Height(px,pz),pz);uv[k]=new Vector2(biome==IslandBiome.Grassland?inland:0,0);
            }
            for(int i=0;i<n-1;i++)for(int j=0;j<n-1;j++)
            {
                if(BiomeIslandLayout.Distance(biome,x+i*4+2,z+j*4+2)<-28)continue;
                int a=i*n+j,b=a+n;t.AddRange(new[]{a,a+1,b,b,a+1,b+1});
            }
            if(t.Count==0)return null;var mesh=new Mesh{vertices=v,colors=c,uv=uv,triangles=t.ToArray()};mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        static Mesh Store(string name,Mesh mesh)
        {
            mesh.name=name;string path=Folder+"/"+name+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(existing!=null)throw new InvalidOperationException("Preserve existing island artwork: "+path);
            if(pending.ContainsKey(path))throw new InvalidOperationException("Duplicate island art name: "+path);
            pending.Add(path,mesh);return mesh;
        }
    }
}
