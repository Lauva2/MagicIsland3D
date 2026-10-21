using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MagicIsland.TerrainSystem;
using UnityEditor;
using UnityEngine;

namespace MagicIsland.EditorTools
{
    public static class GrasslandShrubWindSetup
    {
        public const string Marker="MagicIsland.GrasslandShrubWind.v1",Pending="MagicIsland.GrasslandShrubWind.Pending.v1";
        static readonly HashSet<string> Known=new HashSet<string>{GrasslandRevitalizationSetup.Marker,GrasslandIslandMeadowSetup.Marker,
            GrasslandGroveSetup.Marker,GrasslandFloraSetup.Marker,GrasslandGroundLayersSetup.Marker,GrasslandShrubStoneSetup.Marker,
            GrasslandTouchSetup.Marker,GrasslandReadabilitySetup.Marker,GrasslandPatchSetup.Marker,GrasslandBlendSetup.Marker,
            GrasslandWildflowerSetup.Marker,GrasslandEdgeSetup.Marker,GrasslandPathEdgeSetup.Marker,GrasslandSampleFinishSetup.Marker,
            GrasslandSpatialSetup.Marker,GrasslandShrubDetailSetup.Marker};
        public sealed class Edit{public Mesh Mesh;public Vector2[] UV;public int[] Kind;public int Roots,Lobes,Tips,Branches;public string Metadata;}
        public static bool Done(string data)=>data.Split(';').Contains(Marker);
        static void Check(bool ok,string why){if(!ok)throw new InvalidOperationException("Shrub wind: "+why);}
        public static bool Marked(Vector2 uv)=>uv.y<=-128&&uv.y>=-134.284f&&(uv.x<=-8&&uv.x>=-9||uv.x<=-10&&uv.x>=-11||uv.x<=-12&&uv.x>=-13);
        static Bounds Box(int[] part,Vector3[] v){var b=new Bounds(v[part[0]],Vector3.zero);foreach(int i in part)b.Encapsulate(v[i]);return b;}
        public static Edit[] Plan(IslandTerrain t)
        {
            var s=t.SeasonalDetails.TouchSample;Check(t.IsValid&&s.ArtVersion==1&&s.Renderers.Length==6
                &&t.transform.lossyScale==Vector3.one&&t.transform.rotation==Quaternion.identity,"original complete sample");
            var collisions=new HashSet<Mesh>(t.GetComponentsInChildren<MeshCollider>(true).Select(c=>c.sharedMesh));var result=new List<Edit>();
            foreach(var r in t.SeasonalDetails.MeadowBatches)
                Check(!r.GetComponent<MeshFilter>().sharedMesh.uv.Any(Marked),"preserve preexisting reserved wind data");
            foreach(var r in s.Renderers)
            {
                var m=r.GetComponent<MeshFilter>().sharedMesh;string p=AssetDatabase.GetAssetPath(m);string data=AssetImporter.GetAtPath(p).userData;
                Check(p.StartsWith(BiomeIslandsSetup.Folder+"/",StringComparison.Ordinal)&&!collisions.Contains(m)
                    &&r.transform.localToWorldMatrix==t.transform.localToWorldMatrix&&m.subMeshCount==1&&m.tangents.Length==0
                    &&m.blendShapeCount==0&&m.bindposes.Length==0&&m.boneWeights.Length==0,"owned original visual mesh");
                Check(r.sharedMaterials.Length==1&&r.sharedMaterial.shader.name==GrasslandFloraSetup.ShaderName
                    &&GrasslandShrubDetailSetup.Done(data)&&data.Split(';').All(Known.Contains),"preserve custom material / metadata");
                Check(Enumerable.Range(0,3).All(ch=>m.GetVertexAttributeDimension((UnityEngine.Rendering.VertexAttribute)
                    ((int)UnityEngine.Rendering.VertexAttribute.TexCoord0+ch))==2),"preserve custom UV dimensions");
                Check(Enumerable.Range(3,5).All(ch=>{var a=new List<Vector4>();m.GetUVs(ch,a);return a.Count==0;}),"preserve custom UV channels");
                var v=m.vertices;var original=m.uv;var roots=m.uv2;var floor=m.uv3;var faces=m.triangles;
                Check(new[]{original.Length,roots.Length,floor.Length,m.colors.Length,m.normals.Length}.All(n=>n==v.Length),"complete original attributes");
                var e=new Edit{Mesh=m,UV=(Vector2[])original.Clone(),Kind=new int[v.Length],Metadata=data};result.Add(e);
                if(!r.name.StartsWith("Meadow undergrowth ",StringComparison.Ordinal))continue;
                foreach(var g in faces.Distinct().GroupBy(i=>roots[i]))
                {
                    var ids=g.ToArray();if(Vector2.Distance(g.Key,s.Center)+1.3f>=s.AreaRadius
                        ||!ids.Any(i=>original[i].x>=-5&&original[i].x<=-4)||!ids.Any(i=>original[i].x>0))continue;
                    var parts=GrasslandShrubDetailSetup.LeafLobes(ids,faces,v,original);var lobes=parts.Where(a=>a.Length>=70).ToArray();
                    var tips=parts.Where(a=>a.Length==5).ToArray();var branches=parts.Where(a=>a.Length==10).ToArray();
                    Check((lobes.Length==3||lobes.Length==7)&&tips.Length==12&&branches.Length==2
                        &&parts.Length==lobes.Length+tips.Length+branches.Length,"complete refined shrub topology "+g.Key);
                    var boxes=lobes.Select(a=>Box(a,v)).ToArray();var phases=boxes.Select(b=>Mathf.Repeat(b.center.x*1.19f+b.center.z*.83f+b.center.y*.61f,Mathf.PI*2)).ToArray();
                    void Encode(int i,int kind,float weight,float phase)
                    {
                        Check(original[i].x>=0&&Vector2.Distance(new Vector2(v[i].x,v[i].z),g.Key)<=1.16f,"only original shrub geometry inside envelope");
                        e.Kind[i]=kind;e.UV[i]=new Vector2(-(kind==1?8:kind==2?10:12)-Mathf.Clamp01(weight),-128-phase);
                    }
                    float Height(int i)=>Mathf.Clamp01((v[i].y-floor[i].x-.08f)/1.4f);
                    for(int l=0;l<lobes.Length;l++)foreach(int i in lobes[l])Encode(i,1,Height(i),phases[l]);
                    foreach(var part in branches)foreach(int i in part)Encode(i,3,Height(i),Mathf.Repeat(g.Key.x*.71f+g.Key.y*.43f,Mathf.PI*2));
                    foreach(var part in tips)
                    {
                        int first=part.Min();var anchor=v[first];
                        float Distance(Bounds b){var d=anchor-b.center;return new Vector3(d.x/b.extents.x,d.y/b.extents.y,d.z/b.extents.z).sqrMagnitude;}
                        int lobe=Enumerable.Range(0,lobes.Length).OrderBy(i=>Distance(boxes[i])).First();
                        Check(Distance(boxes[lobe])<1.25f,"leaf base remains embedded in an actual lobe");
                        var end=part.OrderByDescending(i=>(v[i]-anchor).sqrMagnitude).First();var axis=v[end]-anchor;float length=axis.magnitude;axis/=length;
                        Check(length>.06f&&length<.3f,"known pointed leaf length");
                        foreach(int i in part)Encode(i,2,Vector3.Dot(v[i]-anchor,axis)/length,phases[lobe]);
                    }
                    e.Roots++;e.Lobes+=lobes.Length;e.Tips+=tips.Length;e.Branches+=branches.Length;
                }
            }
            Check(result.Sum(e=>e.Roots)==13&&result.Sum(e=>e.Lobes)==67&&result.Sum(e=>e.Tips)==156&&result.Sum(e=>e.Branches)==26,"all original refined shrubs planned");return result.ToArray();
        }
        static void Clean(string p)=>File.WriteAllText(p,string.Join("\n",File.ReadAllLines(p).Select(l=>l.TrimEnd()))+"\n");
        [MenuItem("Magic Island/Art/Prepare Grassland Shrub Wind Sample (Explicit)")]
        public static void Rebuild()
        {
            Check(!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling,"Edit Mode required");
            var root=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);var backups=new Dictionary<string,byte[]>();bool saved=false;
            try
            {
                var t=root.GetComponent<IslandTerrain>();Check(t.IsValid&&t.SeasonalDetails.TouchSample.Renderers.Length==6,"complete sample");
                var paths=t.SeasonalDetails.TouchSample.Renderers.Select(r=>AssetDatabase.GetAssetPath(r.GetComponent<MeshFilter>().sharedMesh)).ToArray();var data=paths.Select(p=>AssetImporter.GetAtPath(p).userData).ToArray();
                Check(!data.Any(d=>d.Split(';').Contains(Pending)),"preserve interrupted write");
                if(data.Any(Done)){Check(data.All(Done),"preserve partial migration");saved=true;Debug.Log("Grassland shrub wind and later manual edits retained.");return;}
                var plan=Plan(t);foreach(string p in paths.SelectMany(p=>new[]{p,p+".meta"}))backups.Add(p,File.ReadAllBytes(p));
                var first=AssetImporter.GetAtPath(paths[0]);first.userData=data[0]+";"+Pending;first.SaveAndReimport();
                foreach(var e in plan.Where(e=>e.Kind.Any(k=>k!=0))){e.Mesh.SetUVs(0,e.UV);EditorUtility.SetDirty(e.Mesh);}AssetDatabase.SaveAssets();
                foreach(var e in plan.Skip(1).Concat(plan.Take(1)))
                {string p=AssetDatabase.GetAssetPath(e.Mesh);var importer=AssetImporter.GetAtPath(p);importer.userData=e.Metadata+";"+Marker;importer.SaveAndReimport();Clean(p);Clean(p+".meta");}
                saved=true;Debug.Log("Grassland shrub wind baked: 13 shrubs, 67 lobe phases, 156 attached leaf tips and 26 anchored branches; original UV0 only.");
            }
            finally{PrefabUtility.UnloadPrefabContents(root);if(!saved){foreach(var p in backups)File.WriteAllBytes(p.Key,p.Value);foreach(string p in backups.Keys.Where(p=>!p.EndsWith(".meta",StringComparison.Ordinal)))AssetDatabase.ImportAsset(p,ImportAssetOptions.ForceUpdate);}}
        }
    }
}
