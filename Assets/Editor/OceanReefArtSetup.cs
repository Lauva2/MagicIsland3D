using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using MagicIsland.Ocean;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;
namespace MagicIsland.EditorTools
{
    // Restores the actual authored placement of each member in existing combined habitat meshes.
    public static class OceanReefArtSetup
    {
        public const string DataPath="Assets/Art/Meshes/Ocean/Reef.meshdata.json",LayoutPath="Assets/Art/Meshes/Ocean/ReefLayout.json";
        const string Marker="OceanReefArt:1:";
        public static readonly string[] Names={"Branch Coral","Fan Coral","Table Coral","Boulder Coral","Seagrass Blades","Tube Sponge","Sea Star","Clam Shell","Reef Rock"};
        public static string SourcePath(string n)=>(n=="Branch Coral"||n=="Seagrass Blades"||n=="Clam Shell"||n=="Reef Rock"?CoastArtSetup.MeshFolder:OceanBiomeSetup.MeshFolder)+"/"+n+".asset";
        [Serializable] public sealed class Source { public string name; public float radius,bottom,top; public int triangles; }
        [Serializable] public sealed class Member { public int source; public Vector3 position; public float yaw,scale; public Color tint; }
        [Serializable] public sealed class Group { public string path; public Bounds bounds; public Member[] members; }
        [Serializable] public sealed class Layout { public int version=1; public Source[] sources; public Group[] groups; public string originalDigest; }
        static void Require(bool b,string why){if(!b)throw new InvalidOperationException(why);}
        static string Hash(string s){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(s))).Replace("-","").ToLowerInvariant();}
        static string Normalize(string path)=>string.Join("\n",File.ReadAllLines(path).Select(s=>s.TrimEnd()))+"\n";
        public static string Digest(Layout layout)=>Hash(string.Join("\n",Names.Select(SourcePath).Concat(layout.groups.Select(g=>g.path)).OrderBy(p=>p,StringComparer.Ordinal).Select(p=>p+":"+Hash(Normalize(p)))));
        [MenuItem("Magic Island/Art/Capture Existing Reef Layout (Explicit)")]
        public static void CaptureLayout()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode&&!File.Exists(LayoutPath),"Capture a missing reef layout in Edit Mode; preserve an existing artist layout.");
            var meshes=Names.Select(n=>AssetDatabase.LoadAssetAtPath<Mesh>(SourcePath(n))).ToArray();Require(meshes.All(m=>m!=null),"Missing reef source mesh.");
            var layout=new Layout {sources=meshes.Select((m,i)=>new Source{name=Names[i],radius=m.vertices.Max(v=>new Vector2(v.x,v.z).magnitude),bottom=m.vertices.Min(v=>v.y),top=m.vertices.Max(v=>v.y),triangles=m.triangles.Length/3}).ToArray()};
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(OceanSetup.PrefabPath);var groups=new List<Group>();
            foreach(var patch in prefab.GetComponentsInChildren<OceanHabitatPatch>(true))
            {
                var mesh=patch.GetComponentInChildren<MeshFilter>().sharedMesh;var vertices=mesh.vertices;var colors=mesh.colors;var triangles=mesh.triangles;
                int cursor=0,triangleCursor=0;var members=new List<Member>();
                while(cursor<vertices.Length)
                {
                    Member found=null;int count=0;
                    for(int i=0;i<meshes.Length;i++)
                    {
                        var sv=meshes[i].vertices;if(cursor+sv.Length>vertices.Length)continue;
                        int probe=0;float longest=0;
                        for(int j=1;j<sv.Length;j++){var d=sv[j]-sv[0];float length=d.x*d.x+d.z*d.z;if(length>longest){longest=length;probe=j;}}
                        var a=sv[probe]-sv[0];var b=vertices[cursor+probe]-vertices[cursor];float horizontal=new Vector2(b.x,b.z).magnitude;
                        float scale=horizontal/Mathf.Sqrt(longest);if(scale<.001f||scale>5)continue;
                        float yaw=Mathf.Atan2(b.x*a.z-b.z*a.x,b.x*a.x+b.z*a.z)*Mathf.Rad2Deg;var rotation=Quaternion.Euler(0,yaw,0);
                        Vector3 position=vertices[cursor]-rotation*(sv[0]*scale);bool same=true;
                        for(int j=0;j<sv.Length;j++)if(Vector3.Distance(vertices[cursor+j],position+rotation*(sv[j]*scale))>.00004f){same=false;break;}
                        if(!same)continue;
                        var idx=meshes[i].triangles;if(triangleCursor+idx.Length>triangles.Length)continue;
                        for(int j=0;j<idx.Length;j++)if(triangles[triangleCursor+j]!=idx[j]+cursor){same=false;break;}
                        if(!same)continue;
                        var sc=meshes[i].colors;Color c=sc.Length==sv.Length?sc[0]:Color.white;Color target=colors[cursor];
                        var tint=new Color(c.r>.001f?target.r/c.r:1,c.g>.001f?target.g/c.g:1,c.b>.001f?target.b/c.b:1,1);
                        for(int j=0;j<sv.Length;j++)
                        {
                            var sourceColor=sc.Length==sv.Length?sc[j]:Color.white;var expected=sourceColor*tint;
                            expected.a=i==4?Mathf.Clamp01((sv[j].y-meshes[i].bounds.min.y)/Mathf.Max(.01f,meshes[i].bounds.size.y)):sc.Length==sv.Length?sc[j].a:0;
                            var actual=colors[cursor+j];if(Mathf.Abs(expected.r-actual.r)>.00002f||Mathf.Abs(expected.g-actual.g)>.00002f||Mathf.Abs(expected.b-actual.b)>.00002f||Mathf.Abs(expected.a-actual.a)>.00002f){same=false;break;}
                        }
                        Require(same,"Preserve custom vertex colours in "+mesh.name+"; source layout cannot be inferred safely.");
                        found=new Member{source=i,position=position,yaw=yaw,scale=scale,tint=tint};count=sv.Length;triangleCursor+=idx.Length;break;
                    }
                    Require(found!=null,"Preserve edited habitat "+mesh.name+" at vertex "+cursor+"; it no longer matches its source pieces.");
                    members.Add(found);cursor+=count;
                }
                Require(triangleCursor==triangles.Length,"Unmatched reef triangles.");groups.Add(new Group{path=AssetDatabase.GetAssetPath(mesh),bounds=mesh.bounds,members=members.ToArray()});
            }
            Require(groups.Count==84,"Expected all 84 authored habitats.");layout.groups=groups.ToArray();layout.originalDigest=Digest(layout);
            File.WriteAllText(LayoutPath,JsonUtility.ToJson(layout));AssetDatabase.ImportAsset(LayoutPath,ImportAssetOptions.ForceSynchronousImport);
            Directory.CreateDirectory("Logs/ReefArt");File.WriteAllText("Logs/ReefArt/LayoutCapture.txt","PASS: "+groups.Count+" exact existing groups, "+groups.Sum(g=>g.members.Length)+" matched members; no resource, prefab or placement writes.\n");
            Debug.Log("Captured existing reef layout without rebaking: "+groups.Sum(g=>g.members.Length)+" members.");
        }
        [MenuItem("Magic Island/Art/Apply Blender Reef Models")]
        public static void Apply()=>Import(false);
        [MenuItem("Magic Island/Art/Rebuild Blender Reef Models (Explicit)")]
        public static void Rebuild()=>Import(true);
        public static Mesh Create(OceanWildlifeArtSetup.Part part)
        {
            var mesh=OceanWildlifeArtSetup.Create(part);var colors=mesh.colors;
            for(int i=0;i<colors.Length;i++)colors[i].a=part.colors[i*4+3];mesh.colors=colors;return mesh;
        }
        static Mesh Combine(Group group,Mesh[] sources)
        {
            var v=new List<Vector3>();var n=new List<Vector3>();var c=new List<Color>();var t=new List<Vector4>();var indices=new List<int>();
            foreach(var member in group.members)
            {
                var mesh=sources[member.source];var rotation=Quaternion.Euler(0,member.yaw,0);var sv=mesh.vertices;var sn=mesh.normals;var sc=mesh.colors;var st=mesh.tangents;int offset=v.Count;
                for(int j=0;j<sv.Length;j++)
                {
                    v.Add(rotation*(sv[j]*member.scale)+member.position);n.Add(rotation*sn[j]);var color=(sc.Length==sv.Length?sc[j]:Color.white)*member.tint;
                    color.a=member.source==4?Mathf.Clamp01((sv[j].y-mesh.bounds.min.y)/Mathf.Max(.01f,mesh.bounds.size.y)):sc.Length==sv.Length?sc[j].a:0;c.Add(color);
                    var tangent=st.Length==sv.Length?rotation*(Vector3)st[j]:Vector3.right;t.Add(new Vector4(tangent.x,tangent.y,tangent.z,1));
                }
                indices.AddRange(mesh.triangles.Select(i=>i+offset));
            }
            var result=new Mesh{indexFormat=v.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};result.SetVertices(v);result.SetNormals(n);result.SetColors(c);result.SetTangents(t);result.SetTriangles(indices,0);
            result.SetUVs(1,new Vector4[v.Count]);result.SetUVs(2,new Vector4[v.Count]);result.RecalculateBounds();var b=result.bounds;b.Expand(new Vector3(.12f,0,.12f));result.bounds=b;return result;
        }
        static void Import(bool rebuild)
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling,"Apply reef art in Edit Mode after compilation.");
            var data=JsonUtility.FromJson<OceanWildlifeArtSetup.Data>(File.ReadAllText(DataPath));var layout=JsonUtility.FromJson<Layout>(File.ReadAllText(LayoutPath));
            Require(data.version==1&&data.coordinateSpace=="Unity local metres"&&data.parts.Select(p=>p.name).SequenceEqual(Names.Take(8))&&layout.version==1&&layout.sources.Select(s=>s.name).SequenceEqual(Names)&&layout.groups.Length==84,"Invalid reef source/layout contract.");
            AssetDatabase.ImportAsset(DataPath,ImportAssetOptions.ForceSynchronousImport);var importer=AssetImporter.GetAtPath(DataPath);string signature=Hash(File.ReadAllText(DataPath))+":"+Hash(File.ReadAllText(LayoutPath));var old=importer.userData;
            if(!rebuild&&old.StartsWith(Marker,StringComparison.Ordinal)){Require(old.StartsWith(Marker+signature+":",StringComparison.Ordinal),"Reef art source changed; use explicit Rebuild.");Debug.Log("Reef art edits preserved.");return;}
            Require(rebuild,"Existing reef art requires an explicit Rebuild.");
            Require(Digest(layout)==(old.StartsWith(Marker,StringComparison.Ordinal)?old.Split(':').Last():layout.originalDigest),"Authored reef meshes changed; preserve them and recapture their exact member layout before rebuilding.");
            var prepared=new List<Mesh>();var targets=new List<Mesh>();
            try
            {
                for(int i=0;i<8;i++)
                {
                    var mesh=Create(data.parts[i]);prepared.Add(mesh);var limit=layout.sources[i];
                    Require(mesh.vertices.All(v=>new Vector2(v.x,v.z).magnitude<=limit.radius+.00001f&&v.y>=limit.bottom-.00001f&&v.y<=limit.top+.00001f),"Reef model exceeds its original obstacle envelope: "+limit.name);
                    targets.Add(AssetDatabase.LoadAssetAtPath<Mesh>(SourcePath(limit.name)));
                }
                var sources=prepared.Concat(new[]{AssetDatabase.LoadAssetAtPath<Mesh>(SourcePath("Reef Rock"))}).ToArray();
                foreach(var group in layout.groups){var mesh=Combine(group,sources);var target=AssetDatabase.LoadAssetAtPath<Mesh>(group.path);mesh.name=target.name;targets.Add(target);prepared.Add(mesh);}
                Require(targets.All(m=>m!=null)&&targets.Distinct().Count()==targets.Count,"Reef targets must exist and retain independent GUIDs.");
                for(int i=0;i<targets.Count;i++){EditorUtility.CopySerialized(prepared[i],targets[i]);EditorUtility.SetDirty(targets[i]);}
                AssetDatabase.SaveAssets();importer.userData=Marker+signature+":"+Digest(layout);importer.SaveAndReimport();
                Debug.Log("Blender reef art applied: eight source meshes and 84 combined habitats; existing placements, obstacles, animals, prefab and materials retained.");
            }
            finally{foreach(var mesh in prepared)if(mesh!=null)Object.DestroyImmediate(mesh);}
        }
    }
}
