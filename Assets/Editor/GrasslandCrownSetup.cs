using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MagicIsland.Interaction;
using MagicIsland.TerrainSystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MagicIsland.EditorTools
{
    // Explicit mesh authoring only. The existing landscape renderer and solid trunks stay owned by AA.
    public static class GrasslandCrownSetup
    {
        public const string Marker="MagicIsland.GrasslandCrown.v1";
        public sealed class Edit
        {
            public Mesh Mesh;public List<Vector3> Vertices,Normals;public List<Color> Colors;
            public List<Vector4> Tangents;public List<int> Triangles;public List<Vector4>[] UV;
            public int[] Dimensions;public bool[] Retired;public Bounds[] Crowns;public Vector3[] Roots;
            public int[] Kinds;public int Lobes;public string Metadata;
        }
        public static void Check(bool ok,string why){if(!ok)throw new InvalidOperationException("Grassland crowns: "+why);}
        public static bool Done(string metadata)=>metadata.Split(';').Contains(Marker);
        static Bounds Box(int[] part,Vector3[] vertices){var b=new Bounds(vertices[part[0]],Vector3.zero);foreach(int i in part)b.Encapsulate(vertices[i]);return b;}
        static bool Crown(int[] part,Vector3[] vertices,Color[] colors)
        {
            if(part.Length!=70||colors.Length!=vertices.Length||part.Any(i=>colors[i].g-colors[i].r<.065f))return false;var b=Box(part,vertices);
            return b.size.y>=2&&b.center.z>=270&&b.center.z<=760&&part.All(i=>BiomeIslandLayout.At(vertices[i].x,vertices[i].z)==IslandBiome.Grassland&&BiomeIslandLayout.Contains(vertices[i].x,vertices[i].z,35));
        }
        public static MeshFilter[] Candidates(IslandTerrain t)=>t.World.Scenery.GetComponentsInChildren<MeshFilter>(true)
            .Where(f=>f.name.StartsWith("Landscape ",StringComparison.Ordinal)).Where(f=>
            {
                var m=f.sharedMesh;var importer=AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(m));
                if(importer==null)return false;if(importer.userData.Split(';').Contains(GrasslandGroveSetup.Marker))return true;var v=m.vertices;var c=m.colors;return BiomePolishSetup.Components(m).Any(p=>Crown(p,v,c));
            }).ToArray();
        static float Sample(Vector3 p,float salt)=>Mathf.PerlinNoise(p.x*.073f+salt,p.z*.061f-salt);
        public static Edit[] Plan(IslandTerrain terrain)
        {
            var edits=new List<Edit>();var collision=new HashSet<Mesh>(terrain.GetComponentsInChildren<MeshCollider>(true).Select(c=>c.sharedMesh));
            foreach(var f in Candidates(terrain))
            {
                var m=f.sharedMesh;string path=AssetDatabase.GetAssetPath(m),metadata=AssetImporter.GetAtPath(path).userData;
                Check(metadata==GrasslandGroveSetup.Marker,"preserve unknown or completed metadata "+path);
                Check(path.StartsWith(BiomeIslandsSetup.Folder+"/Art Landscape ",StringComparison.Ordinal)&&f.transform.localToWorldMatrix==terrain.transform.localToWorldMatrix&&!collision.Contains(m),"original visual-only landscape ownership");
                var renderer=f.GetComponent<Renderer>();Check(renderer!=null&&renderer.sharedMaterials.Length==1&&renderer.sharedMaterial.shader.name=="MagicIsland/Island Biome Details","preserve custom landscape material");
                Check(m.subMeshCount==1&&m.blendShapeCount==0&&m.bindposes.Length==0&&m.boneWeights.Length==0,"original static landscape mesh");
                var vertices=m.vertices;var normals=m.normals;var colors=m.colors;Check(vertices.Length==normals.Length&&vertices.Length==colors.Length,"complete original shading");
                var parts=BiomePolishSetup.Components(m);
                var trunks=parts.Where(p=>p.Length==22&&p.All(i=>colors[i].r>colors[i].g+.04f)).Select(p=>Box(p,vertices)).Where(b=>b.size.y>4&&b.size.x<1.1f&&b.size.z<1.1f).ToArray();
                var e=new Edit{Mesh=m,Vertices=vertices.ToList(),Normals=normals.ToList(),Colors=colors.ToList(),Tangents=m.tangents.ToList(),Triangles=new List<int>(),
                    UV=new List<Vector4>[8],Dimensions=new int[8],Retired=new bool[vertices.Length],Metadata=metadata};
                for(int ch=0;ch<8;ch++){e.UV[ch]=new List<Vector4>();m.GetUVs(ch,e.UV[ch]);e.Dimensions[ch]=m.HasVertexAttribute((VertexAttribute)((int)VertexAttribute.TexCoord0+ch))?m.GetVertexAttributeDimension((VertexAttribute)((int)VertexAttribute.TexCoord0+ch)):0;Check(e.UV[ch].Count==0||e.UV[ch].Count==vertices.Length,"complete original UV channels");}
                Check(e.Tangents.Count==0||e.Tangents.Count==vertices.Length,"complete original tangents");
                var crowns=new List<Bounds>();var roots=new List<Vector3>();var kinds=new List<int>();
                foreach(var part in parts)
                {
                    if(!Crown(part,vertices,colors))continue;var b=Box(part,vertices);var p=b.center;
                    Check(trunks.Length>0,"original solid tree anchor required");var trunk=trunks.OrderBy(q=>new Vector2(q.center.x-p.x,q.center.z-p.z).sqrMagnitude).First();
                    var root=new Vector3(trunk.center.x,trunk.min.y,trunk.center.z);Check(Vector2.Distance(new Vector2(root.x,root.z),new Vector2(p.x,p.z))<b.extents.magnitude,"crown belongs to an original trunk");
                    int kind=Mathf.Min(2,Mathf.FloorToInt(Sample(root,17)*3));var shape=kind==0?new Vector3(.88f,1,.88f):kind==1?new Vector3(1,.85f,1):new Vector3(.91f,.93f,.94f);
                    float angle=Sample(root,43)*Mathf.PI*2;var rotation=Quaternion.Euler(0,angle*Mathf.Rad2Deg,0);
                    var offsets=new[]{new Vector3(-.12f,.12f,0),new Vector3(.37f,-.12f,.10f),new Vector3(-.23f,-.13f,-.35f)};
                    var sizes=new[]{new Vector3(.66f,.76f,.69f),new Vector3(.56f,.57f,.54f),new Vector3(.53f,.53f,.54f)};
                    for(int lobe=0;lobe<offsets.Length;lobe++)Puff(e,part[0],b,shape,rotation,offsets[lobe],sizes[lobe],root,lobe);
                    foreach(int i in part)e.Retired[i]=true;crowns.Add(b);roots.Add(root);kinds.Add(kind);
                }
                if(crowns.Count==0)continue;
                var old=m.triangles;var keep=new List<int>();for(int i=0;i<old.Length;i+=3)
                {
                    if(e.Retired[old[i]]){Check(e.Retired[old[i+1]]&&e.Retired[old[i+2]],"complete crown faces only");continue;}
                    Check(!e.Retired[old[i+1]]&&!e.Retired[old[i+2]],"separate crown connectivity");keep.Add(old[i]);keep.Add(old[i+1]);keep.Add(old[i+2]);
                }
                e.Triangles.InsertRange(0,keep);e.Crowns=crowns.ToArray();e.Roots=roots.ToArray();e.Kinds=kinds.ToArray();edits.Add(e);
            }
            Check(edits.Count>0&&edits.Sum(e=>e.Crowns.Length)>0,"authored original grassland crowns required");return edits.ToArray();
        }
        static void Puff(Edit e,int original,Bounds b,Vector3 shape,Quaternion rotation,Vector3 offset,Vector3 size,Vector3 root,int lobe)
        {
            const int sides=20,rings=12;int first=e.Vertices.Count;var radii=Vector3.Scale(b.extents,shape);
            float variation=Sample(root,61),leaf=Sample(root,83);var light=new Vector3(-.45f,.75f,-.35f).normalized;
            for(int j=0;j<=rings;j++)for(int i=0;i<sides;i++)
            {
                float a=j*Mathf.PI/rings,f=i*Mathf.PI*2/sides;var unit=new Vector3(Mathf.Sin(a)*Mathf.Cos(f),Mathf.Cos(a),Mathf.Sin(a)*Mathf.Sin(f));
                var local=rotation*(offset+Vector3.Scale(unit,size));Check(local.magnitude<1.001f,"contained original crown envelope");var p=b.center+Vector3.Scale(local,radii);
                var normal=rotation*new Vector3(unit.x/size.x,unit.y/size.y,unit.z/size.z);normal=new Vector3(normal.x/radii.x,normal.y/radii.y,normal.z/radii.z).normalized;
                e.Vertices.Add(p);e.Normals.Add(normal);
                float tint=Mathf.Clamp01(.42f+Vector3.Dot(normal,light)*.19f+unit.y*.10f+variation*.10f+(lobe==0?.04f:-.02f));
                var color=Color.Lerp(new Color(.33f,.48f,.30f),new Color(.61f,.71f,.43f),tint);color=Color.Lerp(color,new Color(.51f,.65f,.39f),leaf*.15f);color.a=e.Colors[original].a;e.Colors.Add(color);
                for(int ch=0;ch<8;ch++)if(e.UV[ch].Count>0)e.UV[ch].Add(e.UV[ch][original]);if(e.Tangents.Count>0)e.Tangents.Add(e.Tangents[original]);
            }
            for(int j=0;j<rings;j++)for(int i=0;i<sides;i++)
            {
                int a=first+j*sides+i,c=first+j*sides+(i+1)%sides;
                if(j>0){e.Triangles.Add(a);e.Triangles.Add(c);e.Triangles.Add(a+sides);}
                if(j<rings-1){e.Triangles.Add(a+sides);e.Triangles.Add(c);e.Triangles.Add(c+sides);}
            }
            e.Lobes++;
        }
        public static void Apply(Edit e)
        {
            var m=e.Mesh;var bounds=m.bounds;if(e.Vertices.Count>65535)m.indexFormat=IndexFormat.UInt32;
            m.SetVertices(e.Vertices);m.SetNormals(e.Normals);m.SetColors(e.Colors);if(e.Tangents.Count>0)m.SetTangents(e.Tangents);
            for(int ch=0;ch<8;ch++)if(e.Dimensions[ch]>0)
            {
                if(e.Dimensions[ch]==2)m.SetUVs(ch,e.UV[ch].Select(v=>new Vector2(v.x,v.y)).ToList());
                else if(e.Dimensions[ch]==3)m.SetUVs(ch,e.UV[ch].Select(v=>new Vector3(v.x,v.y,v.z)).ToList());else m.SetUVs(ch,e.UV[ch]);
            }
            m.SetTriangles(e.Triangles,0,false);m.bounds=bounds;EditorUtility.SetDirty(m);
        }
        static void Clean(string p)=>File.WriteAllText(p,string.Join("\n",File.ReadAllLines(p).Select(l=>l.TrimEnd()))+"\n");
        [MenuItem("Magic Island/Art/Refine Grassland Tree Crowns (Explicit)")]
        public static void Rebuild()
        {
            Check(!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling,"Edit Mode required");var root=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);var backup=new Dictionary<string,byte[]>();bool saved=false;
            try
            {
                var t=root.GetComponent<IslandTerrain>();Check(t.IsValid&&t.World.ArtVersion==3,"authored biome islands");var candidates=Candidates(t);Check(candidates.Length>0,"original grove meshes");var done=candidates.Select(f=>Done(AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(f.sharedMesh)).userData)).ToArray();
                if(done.Any(v=>v)){Check(done.All(v=>v),"preserve incomplete/custom migration");Debug.Log("Grassland crown art and later hand edits retained.");return;}
                var plan=Plan(t);Check(plan.Length==candidates.Length,"all original grove meshes planned before writes");
                foreach(string p in plan.Select(e=>AssetDatabase.GetAssetPath(e.Mesh)).SelectMany(p=>new[]{p,p+".meta"}))backup.Add(p,File.ReadAllBytes(p));
                foreach(var e in plan)Apply(e);AssetDatabase.SaveAssets();
                foreach(var e in plan){string p=AssetDatabase.GetAssetPath(e.Mesh);var importer=AssetImporter.GetAtPath(p);importer.userData=e.Metadata+";"+Marker;importer.SaveAndReimport();Clean(p);Clean(p+".meta");}saved=true;
                Directory.CreateDirectory("Logs/GrasslandCrowns");File.WriteAllText("Logs/GrasslandCrowns/Stats.json",JsonUtility.ToJson(new Report{Crowns=plan.Sum(e=>e.Crowns.Length),Lobes=plan.Sum(e=>e.Lobes),Trees=plan.SelectMany(e=>e.Roots).Distinct().Count(),Meshes=plan.Length}));
                Debug.Log("Grassland crowns baked: "+plan.Sum(e=>e.Crowns.Length)+" original crowns, "+plan.Sum(e=>e.Lobes)+" rounded leaf masses in "+plan.Length+" existing meshes; original trunks/renderers/materials retained.");
            }
            finally{PrefabUtility.UnloadPrefabContents(root);if(!saved){foreach(var p in backup)File.WriteAllBytes(p.Key,p.Value);foreach(string p in backup.Keys.Where(p=>!p.EndsWith(".meta",StringComparison.Ordinal)))AssetDatabase.ImportAsset(p,ImportAssetOptions.ForceUpdate);}}
        }
        [Serializable]sealed class Report{public int Crowns,Lobes,Trees,Meshes;}
    }
}
