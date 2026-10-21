using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MagicIsland.TerrainSystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MagicIsland.EditorTools
{
    // Offline leaf shaping. Original vertex indices, anchors and wind channels retain their ownership.
    public static class GrasslandBladeDetailSetup
    {
        public const string Marker="MagicIsland.GrasslandBladeDetail.v1", Pending="MagicIsland.GrasslandBladeDetail.Pending.v1";
        static readonly HashSet<string> Known=new HashSet<string>
        {
            GrasslandRevitalizationSetup.Marker,GrasslandIslandMeadowSetup.Marker,GrasslandGroveSetup.Marker,
            GrasslandFloraSetup.Marker,GrasslandGroundLayersSetup.Marker,GrasslandShrubStoneSetup.Marker,
            GrasslandTouchSetup.Marker,GrasslandReadabilitySetup.Marker,GrasslandPatchSetup.Marker,
            GrasslandBlendSetup.Marker,GrasslandWildflowerSetup.Marker,GrasslandEdgeSetup.Marker,GrasslandPathEdgeSetup.Marker,
            GrasslandSampleFinishSetup.Marker,GrasslandSpatialSetup.Marker,GrasslandShrubDetailSetup.Marker,
            GrasslandShrubWindSetup.Marker,GrasslandGroundDetailSetup.Marker
        };
        public sealed class Edit
        {
            public Mesh Mesh; public Vector3[] Vertices,Normals; public Color[] Colors;
            public Vector2[][] Channels; public int[] Indices; public bool[] Changed;
            public Bounds Bounds; public string Metadata; public int Blades,Triangles,Strips;
        }
        public static bool Done(string data)=>data.Split(';').Contains(Marker);
        public static void Check(bool ok,string why){if(!ok)throw new InvalidOperationException("Blade detail: "+why);}
        static Vector2 XZ(Vector3 p)=>new Vector2(p.x,p.z);
        static Vector2 Rotate(Vector2 p,float a)=>new Vector2(p.x*Mathf.Cos(a)-p.y*Mathf.Sin(a),p.x*Mathf.Sin(a)+p.y*Mathf.Cos(a));
        public static Edit[] Plan(IslandTerrain terrain)
        {
            var s=terrain.SeasonalDetails.TouchSample;
            Check(terrain.IsValid&&terrain.World.ArtVersion==3&&s.ArtVersion==1&&s.Renderers.Length==6
                &&terrain.transform.localToWorldMatrix==Matrix4x4.identity,"complete original sample and transform");
            var collision=new HashSet<Mesh>(terrain.GetComponentsInChildren<MeshCollider>(true).Select(c=>c.sharedMesh));
            var edits=new List<Edit>();
            foreach(var r in s.Renderers)
            {
                var m=r.GetComponent<MeshFilter>().sharedMesh;string path=AssetDatabase.GetAssetPath(m);
                string data=AssetImporter.GetAtPath(path).userData;
                Check(path.StartsWith(BiomeIslandsSetup.Folder+"/",StringComparison.Ordinal)&&!collision.Contains(m)
                    &&r.transform.localToWorldMatrix==terrain.transform.localToWorldMatrix&&r.sharedMaterials.Length==1
                    &&r.sharedMaterial.shader.name==GrasslandFloraSetup.ShaderName,"preserve custom mesh/material/transform");
                Check(GrasslandGroundDetailSetup.Done(data)&&data.Split(';').All(Known.Contains),"preserve custom metadata or interrupted migration");
                Check(m.subMeshCount==1&&m.tangents.Length==0&&m.blendShapeCount==0&&m.bindposes.Length==0&&m.boneWeights.Length==0
                    &&Enumerable.Range(0,3).All(ch=>m.GetVertexAttributeDimension((VertexAttribute)((int)VertexAttribute.TexCoord0+ch))==2)
                    &&Enumerable.Range(3,5).All(ch=>{var a=new List<Vector4>();m.GetUVs(ch,a);return a.Count==0;}),"preserve custom channels/topology");
                var v=m.vertices;var n=m.normals;var c=m.colors;var uv=m.uv;var roots=m.uv2;var floor=m.uv3;var faces=m.triangles;
                Check(new[]{n.Length,c.Length,uv.Length,roots.Length,floor.Length}.All(l=>l==v.Length),"complete original attributes");
                var points=v.ToList();var normals=n.ToList();var colors=c.ToList();var channels=new[]{uv.ToList(),roots.ToList(),floor.ToList()};
                var edit=new Edit{Mesh=m,Changed=new bool[v.Length],Bounds=m.bounds,Metadata=data};
                var replacements=new Dictionary<int,int[]>();var removed=new HashSet<int>();
                var active=new HashSet<int>(faces);var faceStarts=new List<int>[v.Length];
                for(int i=0;i<faces.Length;i+=3)foreach(int id in new[]{faces[i],faces[i+1],faces[i+2]})
                {if(faceStarts[id]==null)faceStarts[id]=new List<int>();faceStarts[id].Add(i);}
                foreach(var part in BiomePolishSetup.Components(m))
                {
                    if(part.Length<3||!active.Contains(part[0])||part.Any(i=>uv[i].x< -5||uv[i].x> -4))continue;
                    var p=roots[part[0]];
                    if(Vector2.Distance(p,s.Center)+1.30f>=s.AreaRadius||GrasslandIslandMeadowSetup.RouteDistance(p)<3.65f)continue;
                    float fade=GrasslandGroundDetailSetup.Mask(p,s);if(fade<=.10f)continue;
                    Check((part.Length==3||part.Length==5||part.Length==7)&&part.SequenceEqual(Enumerable.Range(part[0],part.Length)),"known contiguous leaf strip");
                    Check(part.All(i=>roots[i]==p&&floor[i]==floor[part[0]]&&Mathf.Abs(uv[i].y-uv[part[0]].y)<.00001f),"one anchored leaf and phase");
                    Check(Mathf.Abs(uv[part[0]].x+4)<.00001f&&Mathf.Abs(uv[part[1]].x+4)<.00001f
                        &&part.Skip(2).All(i=>uv[i].x< -4),"recognizable two fixed bases");
                    int left=part[0],right=part[1],tip=part.Last();var anchor=(v[left]+v[right])*.5f;
                    float height=part.Max(i=>v[i].y)-anchor.y,width=Vector2.Distance(XZ(v[left]),XZ(v[right]))*.5f;
                    Check(height>.025f&&height<.8f&&width>.005f&&width<.2f,"known leaf dimensions");
                    var side=(XZ(v[right])-XZ(v[left])).normalized;var direction=new Vector2(-side.y,side.x);
                    var oldReach=XZ(v[tip])-XZ(anchor);if(Vector2.Dot(direction,oldReach)<0)direction=-direction;
                    float phase=uv[tip].y+anchor.x*1.731f+anchor.z*.917f;
                    float variation=.5f+.5f*Mathf.Sin(phase*1.37f);
                    float turn=Mathf.Sin(phase*.83f)*.42f*fade;
                    // Each blade has its own arch and rolled cross-section, while the root and low gaps remain fixed.
                    Vector3 Shape(float t,float lateral,Vector3 old)
                    {
                        float curve=t*t*(3-2*t);float arch=Mathf.Sin(t*Mathf.PI);
                        var center=XZ(anchor)+Vector2.Lerp(oldReach*t,oldReach*(t*.2f+t*t*.8f),.72f*fade)
                            +direction*(height*(.11f+.10f*variation)*arch*fade)
                            +side*(height*.10f*Mathf.Sin(phase)*t*t*fade);
                        var section=Rotate(side,turn*t);
                        float taper=1-.20f*t*fade;
                        var horizontal=center+section*lateral*taper;
                        float y=old.y-height*(.035f+.035f*variation)*t*t*fade
                            +lateral*Mathf.Sin(turn*1.4f)*t*.5f;
                        var next=new Vector3(horizontal.x,y,horizontal.y);
                        // Protect original root envelope and renderer bounds; never enlarge into a neighboring plant.
                        var radial=XZ(next)-p;
                        if(radial.magnitude>1.16f){var q=p+radial.normalized*1.16f;next.x=q.x;next.z=q.y;}
                        Check(edit.Bounds.Contains(next)&&next.y>=floor[tip].x-.02f,"original culling and ground envelope");return next;
                    }
                    Vector3 Normal(float t,float lateral)
                    {
                        float roll=lateral/width;var bent=Rotate(direction,turn*t);
                        return new Vector3(-bent.x*(.10f+.18f*t)+side.x*roll*.24f,1,
                            -bent.y*(.10f+.18f*t)+side.y*roll*.24f).normalized;
                    }
                    // All original indices remain stable, notably the linked pollen vertices used by butterflies.
                    for(int j=2;j<part.Length;j++)
                    {
                        int id=part[j];bool end=j==part.Length-1;
                        float t=end?1:part.Length==5?.54f:j<4?.40f:.77f;
                        float lateral=end?0:(j%2==0?-1:1)*Vector2.Distance(XZ(v[part[j-j%2]]),XZ(v[part[j-j%2+1]]))*.5f;
                        points[id]=Shape(t,lateral,v[id]);normals[id]=Vector3.Lerp(n[id],Normal(t,lateral),fade).normalized;
                        // A small tip lift in albedo avoids uniform grey strips; retain root paint and alpha exactly.
                        var tint=c[id];float lift=1+.045f*t*fade;colors[id]=new Color(tint.r*lift,tint.g*lift,tint.b*lift,tint.a);
                        edit.Changed[id]=true;
                    }
                    if(part.Length==3)
                    {
                        var starts=part.SelectMany(i=>faceStarts[i]).Distinct().OrderBy(i=>i).ToArray();
                        Check((starts.Length==1||starts.Length==2)&&starts.All(i=>new[]{faces[i],faces[i+1],faces[i+2]}.OrderBy(a=>a).SequenceEqual(part)),"original single/double-sided triangle");
                        int first=points.Count;
                        foreach(float t in new[]{.38f,.74f})foreach(float sign in new[]{-1f,1f})
                        {
                            float lateral=width*(t<.5f?.86f:.43f)*sign;
                            var old=Vector3.Lerp(anchor,v[tip],t);
                            points.Add(Shape(t,lateral,old));normals.Add(Normal(t,lateral));
                            var tint=Color.Lerp((c[left]+c[right])*.5f,c[tip],t);float lift=1+.045f*t*fade;
                            colors.Add(new Color(tint.r*lift,tint.g*lift,tint.b*lift,tint.a));
                            channels[0].Add(new Vector2(Mathf.Lerp(-4,uv[tip].x,t),uv[tip].y));channels[1].Add(p);channels[2].Add(floor[tip]);
                        }
                        var ids=new[]{left,right,first,first+1,first+2,first+3,tip};var pattern=new[]{0,2,1,1,2,3,2,4,3,3,4,5,4,6,5};var added=new List<int>();
                        for(int j=0;j<pattern.Length;j+=3)
                        {int a=ids[pattern[j]],b=ids[pattern[j+1]],d=ids[pattern[j+2]];added.AddRange(new[]{a,b,d,a,d,b});}
                        replacements.Add(starts[0],added.ToArray());if(starts.Length==2)removed.Add(starts[1]);edit.Triangles++;
                    }
                    else edit.Strips++;
                    edit.Blades++;
                }
                var nextFaces=new List<int>();
                for(int i=0;i<faces.Length;i+=3)
                {if(replacements.TryGetValue(i,out var added))nextFaces.AddRange(added);else if(!removed.Contains(i))nextFaces.AddRange(new[]{faces[i],faces[i+1],faces[i+2]});}
                edit.Vertices=points.ToArray();edit.Normals=normals.ToArray();edit.Colors=colors.ToArray();edit.Channels=channels.Select(a=>a.ToArray()).ToArray();edit.Indices=nextFaces.ToArray();
                Check(m.indexFormat==IndexFormat.UInt32||points.Count<=65535,"preserve UInt16 index format without overflow");edits.Add(edit);
            }
            Check(edits.Select(e=>e.Mesh).Distinct().Count()==6&&edits.All(e=>e.Blades>0)&&edits.Sum(e=>e.Triangles)>500&&edits.Sum(e=>e.Strips)>1000,"bounded sample leaf detail");return edits.ToArray();
        }
        static void Clean(string p)=>File.WriteAllText(p,string.Join("\n",File.ReadAllLines(p).Select(l=>l.TrimEnd()))+"\n");
        [MenuItem("Magic Island/Art/Refine Grassland Blade Detail Sample (Explicit)")]
        public static void Rebuild()
        {
            Check(!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling,"Edit Mode required");
            var root=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);var backups=new Dictionary<string,byte[]>();bool saved=false;
            try
            {
                var t=root.GetComponent<IslandTerrain>();Check(t.IsValid&&t.SeasonalDetails.TouchSample.Renderers.Length==6,"complete original sample");
                var paths=t.SeasonalDetails.TouchSample.Renderers.Select(r=>AssetDatabase.GetAssetPath(r.GetComponent<MeshFilter>().sharedMesh)).ToArray();
                var data=paths.Select(p=>AssetImporter.GetAtPath(p).userData).ToArray();
                Check(!data.Any(d=>d.Split(';').Contains(Pending)),"preserve interrupted write");
                if(data.Any(Done)){Check(data.All(Done),"preserve partial migration");saved=true;Debug.Log("Grassland blade detail and later manual edits retained.");return;}
                var plan=Plan(t);foreach(string p in paths.SelectMany(p=>new[]{p,p+".meta"}))backups.Add(p,File.ReadAllBytes(p));
                var first=AssetImporter.GetAtPath(paths[0]);first.userData=data[0]+";"+Pending;first.SaveAndReimport();
                foreach(var e in plan)
                {
                    e.Mesh.vertices=e.Vertices;e.Mesh.normals=e.Normals;e.Mesh.colors=e.Colors;
                    for(int ch=0;ch<3;ch++)e.Mesh.SetUVs(ch,e.Channels[ch]);e.Mesh.SetTriangles(e.Indices,0,false);
                    e.Mesh.bounds=e.Bounds;EditorUtility.SetDirty(e.Mesh);
                }
                AssetDatabase.SaveAssets();
                foreach(var e in plan.Skip(1).Concat(plan.Take(1)))
                {string p=AssetDatabase.GetAssetPath(e.Mesh);var importer=AssetImporter.GetAtPath(p);importer.userData=e.Metadata+";"+Marker;importer.SaveAndReimport();Clean(p);Clean(p+".meta");}
                saved=true;Debug.Log("Grassland blade detail baked: "+plan.Sum(e=>e.Triangles)+" triangles replaced by curved seven-vertex blades, "+plan.Sum(e=>e.Strips)+" existing strips rolled; original roots, flowers, wind and index formats retained.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
                if(!saved){foreach(var p in backups)File.WriteAllBytes(p.Key,p.Value);foreach(string p in backups.Keys.Where(p=>!p.EndsWith(".meta",StringComparison.Ordinal)))AssetDatabase.ImportAsset(p,ImportAssetOptions.ForceUpdate);}
            }
        }
    }
}
