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
    public static class MeadowRabbitSetup
    {
        public const string Group="Grassland rabbits",Source="SourceArt/MeadowRabbit/MeadowRabbit.meshdata.json";
        public const string MeshPath=GrasslandCompositionSetup.Folder+"/Meadow Rabbits.asset",MaterialPath=GrasslandEcologySetup.MaterialFolder+"Meadow Rabbits.mat";
        public const float Radius=.46f,Height=1.15f;
        [Serializable] public sealed class Export{public int version;public float[] positions,normals,colors,pivots;public int[] parts,triangles;}
        public static void Require(bool ok,string why){if(!ok)throw new InvalidOperationException("Meadow rabbits: "+why);}
        public static Export ReadSource()
        {
            var d=JsonUtility.FromJson<Export>(File.ReadAllText(Source));Require(d!=null&&d.version==1&&d.parts!=null&&d.parts.Length>0,"Blender template");
            Require(d.positions.Length==d.parts.Length*3&&d.normals.Length==d.positions.Length&&d.pivots.Length==d.positions.Length&&d.colors.Length==d.parts.Length*4
                &&d.parts.All(p=>p>=0&&p<=7)&&d.triangles.Length%3==0&&d.triangles.All(i=>i>=0&&i<d.parts.Length)
                &&d.positions.Concat(d.normals).Concat(d.colors).Concat(d.pivots).All(v=>!float.IsNaN(v)&&!float.IsInfinity(v)),"complete source channels");return d;
        }
        static Bounds Triangle(Vector3 a,Vector3 b,Vector3 c){var box=new Bounds(a,Vector3.zero);box.Encapsulate(b);box.Encapsulate(c);return box;}
        public static Bounds[] Obstacles(IslandTerrain terrain)
        {
            var boxes=new List<Bounds>();
            foreach(var c in terrain.GetComponentsInChildren<Collider>(true))
            {
                if(!c.enabled||c.isTrigger||terrain.IsGround(c)||c==terrain.Boundary)continue;
                if(c is MeshCollider meshCollider)
                {
                    var mesh=meshCollider.sharedMesh;if(mesh==null)continue;var v=mesh.vertices.Select(c.transform.TransformPoint).ToArray();var indices=mesh.triangles;
                    for(int i=0;i<indices.Length;i+=3)boxes.Add(Triangle(v[indices[i]],v[indices[i+1]],v[indices[i+2]]));
                }
                else boxes.Add(c.bounds);
            }
            // Decorative low rocks/shrubs have no collider. Retain their original silhouettes too.
            foreach(var f in terrain.World.Scenery.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.name.StartsWith("Landscape ",StringComparison.Ordinal)))
            {
                var mesh=f.sharedMesh;var v=mesh.vertices.Select(f.transform.TransformPoint).ToArray();var indices=mesh.triangles;
                for(int i=0;i<indices.Length;i+=3)boxes.Add(Triangle(v[indices[i]],v[indices[i+1]],v[indices[i+2]]));
            }
            return boxes.ToArray();
        }
        public static bool Reserved(Vector2 p)
        {
            if(StarterIslandLayout.OriginalRegion(p.x,p.y)!=0||!BiomePolishSetup.Clear(p,Radius))return false;
            for(int x=Mathf.RoundToInt(p.x-Radius);x<=Mathf.RoundToInt(p.x+Radius);x++)for(int z=Mathf.RoundToInt(p.y-Radius);z<=Mathf.RoundToInt(p.y+Radius);z++)if(StarterIslandLayout.CanPlace(x,z))return false;
            return true;
        }
        // Preflight the full strip before any resource write. Used by the optional composition pass too.
        public static Vector3[] SafeTrack(IslandTerrain terrain,Vector2 center,Vector2 direction,Bounds[] obstacles,Vector2[] flowers,float clearance=1.9f)
        {
            var ecology=terrain.World.GetComponentInChildren<WorldWaterEcology>(true);var points=new Vector3[25];
            for(int i=0;i<points.Length;i++)
            {
                var p=center+direction*Mathf.Lerp(-1.8f,1.8f,i/24f);
                if(!Reserved(p)||flowers.Any(f=>Vector2.Distance(f,p)<clearance))return null;
                float y=GrasslandEcologySetup.Ground(terrain,p);var world=terrain.transform.TransformPoint(new Vector3(p.x,y+.015f,p.y));points[i]=ecology.transform.InverseTransformPoint(world);
                var box=new Bounds(world+Vector3.up*(Height*.5f+.025f),new Vector3(Radius*2,Height,Radius*2));
                if(obstacles.Any(b=>b.Intersects(box)))return null;
                foreach(var offset in new[]{new Vector2(Radius,0),new Vector2(-Radius,0),new Vector2(0,Radius),new Vector2(0,-Radius)})
                    if(Mathf.Abs(GrasslandEcologySetup.Ground(terrain,p+offset)-y)>.08f)return null;
            }
            return points;
        }
        public static MeadowRabbitHabitat[] Plan(IslandTerrain terrain)
        {
            var obstacles=Obstacles(terrain);
            var flowers=GrasslandCompositionSetup.Sites().Concat(GrasslandFinishSetup.Sites().Select(s=>s.Point)).Concat(BiomePolishSetup.Sites(1).Select(s=>s.Point)).ToArray();
            var segments=new[]{new[]{new Vector2(0,240),new Vector2(-150,370)},new[]{new Vector2(-150,370),new Vector2(-100,520)},new[]{new Vector2(-100,520),new Vector2(-100,635)},new[]{new Vector2(300,340),new Vector2(350,363)}};
            var result=new List<MeadowRabbitHabitat>();
            foreach(var segment in segments)
            {
                var direction=(segment[1]-segment[0]).normalized;var side=new Vector2(-direction.y,direction.x);bool found=false;
                for(int attempt=0;attempt<60&&!found;attempt++)
                {
                    float fraction=.2f+(attempt/2)*.02f;var center=Vector2.Lerp(segment[0],segment[1],fraction)+side*(attempt%2==0?4.3f:-4.3f);
                    var points=SafeTrack(terrain,center,direction,obstacles,flowers);
                    if(points==null)continue;
                    var h=new MeadowRabbitHabitat{Track=points,Phase=result.Count*2.399963f};Require(h.IsValid,"safe track");result.Add(h);found=true;
                }
                Require(found,"no complete safe strip on segment "+segment[0]+" to "+segment[1]);
            }
            return result.ToArray();
        }
        public static Mesh Prepare(Export source,MeadowRabbitHabitat[] habitats)
        {
            var v=new List<Vector3>();var n=new List<Vector3>();var c=new List<Color>();var uv=new List<Vector2>();var pivots=new List<Vector3>();var homes=new List<Vector3>();var triangles=new List<int>();
            for(int rabbit=0;rabbit<habitats.Length;rabbit++)
            {
                int first=v.Count;
                for(int i=0;i<source.parts.Length;i++)
                {
                    var home=habitats[rabbit].Track[habitats[rabbit].Track.Length/2];homes.Add(home);v.Add(home+new Vector3(source.positions[i*3],source.positions[i*3+1],source.positions[i*3+2]));n.Add(new Vector3(source.normals[i*3],source.normals[i*3+1],source.normals[i*3+2]));
                    var color=new Color(source.colors[i*4],source.colors[i*4+1],source.colors[i*4+2],1);
                    if(source.parts[i]==0&&color.r<.95f)color=Color.Lerp(color,rabbit%2==0?new Color(.90f,.84f,.72f):new Color(.70f,.66f,.60f),.4f);
                    c.Add(color);uv.Add(new Vector2(source.parts[i],rabbit));pivots.Add(new Vector3(source.pivots[i*3],source.pivots[i*3+1],source.pivots[i*3+2]));
                }
                triangles.AddRange(source.triangles.Select(i=>i+first));
            }
            var mesh=new Mesh{name="Meadow Rabbits",indexFormat=v.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};mesh.SetVertices(v);mesh.SetNormals(n);mesh.SetColors(c);mesh.SetUVs(0,uv);mesh.SetUVs(1,pivots);mesh.SetUVs(2,homes);mesh.SetTriangles(triangles,0);
            var bounds=new Bounds(habitats[0].Track[0],Vector3.zero);foreach(var h in habitats)foreach(var p in h.Track)bounds.Encapsulate(p);bounds.Expand(new Vector3(2*Radius,2*Height,2*Radius));mesh.bounds=bounds;return mesh;
        }
        static void Clean(string path)=>File.WriteAllText(path,string.Join("\n",File.ReadAllLines(path).Select(l=>l.TrimEnd()))+"\n");
        [MenuItem("Magic Island/Art/Add Grassland Rabbits (Explicit)")]
        public static void Rebuild()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling,"Edit Mode required");
            var root=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);Mesh mesh=null;
            try
            {
                var terrain=root.GetComponent<IslandTerrain>();var ecology=terrain.World.GetComponentInChildren<WorldWaterEcology>(true);Require(ecology!=null,"existing ecology owner");
                var group=ecology.transform.Find(Group);
                if(group!=null){Require(ecology.IsValid&&ecology.MeadowRabbits==group.GetComponent<Renderer>(),"preserve incomplete/custom rabbit group");Debug.Log("Existing grassland rabbits and manual edits retained.");return;}
                Require(ecology.MeadowRabbits==null&&ecology.RabbitHabitats.Length==0,"preserve existing custom population");
                foreach(string path in new[]{MeshPath,MaterialPath})Require(!File.Exists(path)&&!File.Exists(path+".meta"),"preserve orphan asset "+path);
                var source=ReadSource();var habitats=Plan(terrain);var shader=Shader.Find("MagicIsland/Meadow Rabbit");Require(shader!=null&&!ShaderUtil.ShaderHasError(shader),"compiled shader");mesh=Prepare(source,habitats);
                var material=new Material(shader){name="Meadow Rabbits"};AssetDatabase.CreateAsset(material,MaterialPath);AssetDatabase.CreateAsset(mesh,MeshPath);
                var go=new GameObject(Group,typeof(MeshFilter),typeof(MeshRenderer));go.layer=2;go.transform.SetParent(ecology.transform,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;
                ecology.MeadowRabbits=renderer;ecology.RabbitHabitats=habitats;PrefabUtility.SaveAsPrefabAsset(root,TerrainSetup.PrefabPath);AssetDatabase.SaveAssets();
                foreach(string path in new[]{TerrainSetup.PrefabPath,MeshPath,MaterialPath})Clean(path);
                Debug.Log("Grassland rabbits baked: "+habitats.Length+" safe strips, one shared batch, "+mesh.triangles.Length/3+" triangles.");
            }
            finally{if(mesh!=null&&!AssetDatabase.Contains(mesh))Object.DestroyImmediate(mesh);PrefabUtility.UnloadPrefabContents(root);}
        }
    }
}
