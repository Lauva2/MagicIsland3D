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
    // Explicit visual replacement only. School records, sites and the Ocean prefab are never rewritten.
    public static class OceanWildlifeArtSetup
    {
        public const string DataPath="Assets/Art/Meshes/Ocean/Wildlife.meshdata.json";
        const string Marker="OceanWildlifeArt:1:";
        public static readonly string[] Names={"SilverMinnow","Clownfish","Butterflyfish","SpottedRay","Seagull Body","Seagull Left Wing","Seagull Right Wing","Shore Crab"};
        [Serializable] public sealed class Data { public int version; public string coordinateSpace; public Part[] parts; }
        [Serializable] public sealed class Part { public string name; public float[] positions,normals,colors,hinges,gait; public int[] triangles; }
        static void Require(bool ok,string why){if(!ok)throw new InvalidOperationException(why);}
        static bool Finite(float f)=>!float.IsNaN(f)&&!float.IsInfinity(f);
        [MenuItem("Magic Island/Art/Apply Blender Ocean Wildlife")]
        public static void Apply()=>Import(false);
        [MenuItem("Magic Island/Art/Rebuild Blender Ocean Wildlife (Explicit)")]
        public static void Rebuild()=>Import(true);
        public static Mesh Create(Part p)
        {
            Require(p!=null&&p.positions!=null&&p.normals!=null&&p.colors!=null&&p.hinges!=null&&p.gait!=null&&p.triangles!=null,"Incomplete ocean art part.");
            int n=p.positions.Length/3;
            Require(n>2&&n<65536&&p.positions.Length==n*3&&p.normals.Length==n*3&&p.colors.Length==n*4&&p.hinges.Length==n*4&&p.gait.Length==n*4
                &&p.positions.All(Finite)&&p.normals.All(Finite)&&p.colors.All(f=>Finite(f)&&f>=0&&f<=1)&&p.hinges.All(Finite)&&p.gait.All(Finite)
                &&p.triangles.Length>2&&p.triangles.Length%3==0&&p.triangles.All(i=>i>=0&&i<n),"Invalid ocean art geometry: "+p.name);
            var v=new Vector3[n];var normals=new Vector3[n];var colors=new Color[n];var h=new Vector4[n];var gait=new Vector4[n];var tangent=new Vector4[n];
            for(int i=0;i<n;i++)
            {
                v[i]=new Vector3(p.positions[i*3],p.positions[i*3+1],p.positions[i*3+2]);normals[i]=new Vector3(p.normals[i*3],p.normals[i*3+1],p.normals[i*3+2]);
                Require(Mathf.Abs(normals[i].sqrMagnitude-1)<.01f,"Non-unit ocean art normal.");
                colors[i]=new Color(p.colors[i*4],p.colors[i*4+1],p.colors[i*4+2],0);
                h[i]=new Vector4(p.hinges[i*4],p.hinges[i*4+1],p.hinges[i*4+2],p.hinges[i*4+3]);gait[i]=new Vector4(p.gait[i*4],p.gait[i*4+1],p.gait[i*4+2],p.gait[i*4+3]);
                var t=Vector3.Cross(Mathf.Abs(normals[i].y)>.9f?Vector3.right:Vector3.up,normals[i]).normalized;tangent[i]=new Vector4(t.x,t.y,t.z,1);
            }
            var mesh=new Mesh{name=p.name,vertices=v,normals=normals,colors=colors,tangents=tangent,triangles=p.triangles};mesh.SetUVs(1,h);mesh.SetUVs(2,gait);mesh.RecalculateBounds();return mesh;
        }
        static Mesh Combine(OceanSchool school,Mesh oldSource,Mesh source)
        {
            var previous=school.GetComponent<MeshFilter>().sharedMesh;var originalVertices=previous.vertices;var originalColors=previous.colors;var originalSource=oldSource.vertices;
            var pivots=new List<Vector4>();var shapes=new List<Vector4>();previous.GetUVs(1,pivots);previous.GetUVs(2,shapes);
            Require(pivots.Count==originalVertices.Length&&shapes.Count==originalVertices.Length,"Missing school member metadata.");
            var vertices=new List<Vector3>();var normals=new List<Vector3>();var colors=new List<Color>();var hinges=new List<Vector4>();var gait=new List<Vector4>();var triangles=new List<int>();var tangents=new List<Vector4>();
            var sv=source.vertices;var sn=source.normals;var sc=source.colors;var st=source.tangents;var si=source.triangles;
            for(int member=0;member<school.Members;member++)
            {
                int first=pivots.FindIndex(h=>Mathf.RoundToInt((h.w-1)/2.399963f)==member);
                Require(first>=0&&first+originalSource.Length<=originalVertices.Length,"Unknown school layout.");var pivot=pivots[first];var shape=shapes[first];
                int probe=Array.FindIndex(originalSource,p=>p.sqrMagnitude>.001f);
                float scale=(originalVertices[first+probe]-(Vector3)pivot).magnitude/originalSource[probe].magnitude;
                Require(scale>.1f&&scale<3,"Invalid authored fish scale.");
                var rotation=Quaternion.Euler(0,shape.x*Mathf.Rad2Deg,0);var tint=Color.white;var oldColors=oldSource.colors;
                for(int i=0;i<originalSource.Length;i++)Require(Vector3.Distance(originalVertices[first+i],rotation*(originalSource[i]*scale)+(Vector3)pivot)<.00001f
                    &&pivots[first+i]==pivot&&shapes[first+i]==shape,"Edited school geometry does not match its source; preserve it and export the authored member layout first.");
                if(oldColors.Length>0){var c=oldColors[probe];var o=originalColors[first+probe];tint=new Color(c.r>.001f?o.r/c.r:1,c.g>.001f?o.g/c.g:1,c.b>.001f?o.b/c.b:1,1);}
                int offset=vertices.Count;
                for(int i=0;i<sv.Length;i++)
                {
                    vertices.Add(rotation*(sv[i]*scale)+(Vector3)pivot);normals.Add(rotation*sn[i]);var c=sc[i]*tint;c.a=0;colors.Add(c);hinges.Add(pivot);gait.Add(shape);
                    var t=rotation*(Vector3)st[i];tangents.Add(new Vector4(t.x,t.y,t.z,st[i].w));
                }
                triangles.AddRange(si.Select(i=>i+offset));
            }
            var mesh=new Mesh{name=previous.name,indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetColors(colors);mesh.SetTangents(tangents);mesh.SetTriangles(triangles,0);mesh.SetUVs(1,hinges);mesh.SetUVs(2,gait);mesh.bounds=previous.bounds;return mesh;
        }
        static void Import(bool rebuild)
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling,"Apply ocean art after compilation in Edit Mode.");
            var data=JsonUtility.FromJson<Data>(File.ReadAllText(DataPath));
            Require(data!=null&&data.version==1&&data.coordinateSpace=="Unity local metres"&&data.parts!=null&&data.parts.Select(p=>p.name).SequenceEqual(Names),"Invalid ocean wildlife source contract.");
            AssetDatabase.ImportAsset(DataPath,ImportAssetOptions.ForceSynchronousImport);var importer=AssetImporter.GetAtPath(DataPath);string marker;
            using(var sha=SHA256.Create())marker=Marker+BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(DataPath))).Replace("-","").ToLowerInvariant();
            if(!rebuild&&importer.userData.StartsWith(Marker,StringComparison.Ordinal))
            {Require(importer.userData==marker,"Ocean wildlife source changed; use explicit Rebuild.");Debug.Log("Ocean wildlife artist edits preserved.");return;}
            Require(rebuild,"Existing ocean resources require explicit Rebuild for their first Blender replacement.");
            var prepared=new List<Mesh>();var targets=new List<Mesh>();GameObject root=null;var originals=new List<Mesh>();
            try
            {
                for(int i=0;i<Names.Length;i++)
                {
                    var target=AssetDatabase.LoadAssetAtPath<Mesh>(OceanBiomeSetup.MeshFolder+"/"+Names[i]+".asset");Require(target!=null,"Missing original ocean mesh: "+Names[i]);
                    originals.Add(Object.Instantiate(target));targets.Add(target);prepared.Add(Create(data.parts[i]));
                    if(i<4)
                    {
                        var before=target.vertices;var after=prepared[i].vertices;
                        Require(after.Max(v=>new Vector2(v.x,v.z).magnitude)<=before.Max(v=>new Vector2(v.x,v.z).magnitude)+.00001f
                            &&after.Min(v=>v.y)>=before.Min(v=>v.y)-.00001f&&after.Max(v=>v.y)<=before.Max(v=>v.y)+.00001f,"New fish exceeds the original safety envelope.");
                    }
                }
                root=PrefabUtility.LoadPrefabContents(OceanSetup.PrefabPath);var surface=root.GetComponent<OceanSurface>();
                foreach(var school in surface.Schools)
                {
                    int species=(int)school.Species;var filter=school.GetComponent<MeshFilter>();targets.Add(filter.sharedMesh);
                    var combined=Combine(school,originals[species],prepared[species]);prepared.Add(combined);filter.sharedMesh=combined;
                    surface.ConfigureFishHabitat(school);school.Render(0); // Reject unsafe full-body geometry before writing assets.
                }
                for(int i=0;i<targets.Count;i++){EditorUtility.CopySerialized(prepared[i],targets[i]);EditorUtility.SetDirty(targets[i]);}
                AssetDatabase.SaveAssets();importer.userData=marker;importer.SaveAndReimport();
                Debug.Log("Blender ocean wildlife applied: eight shared meshes and existing combined schools; prefab, sites, records, coral and materials preserved.");
            }
            finally
            {
                if(root!=null)PrefabUtility.UnloadPrefabContents(root);
                foreach(var mesh in prepared.Concat(originals))if(mesh!=null)Object.DestroyImmediate(mesh);
            }
        }
    }
}
