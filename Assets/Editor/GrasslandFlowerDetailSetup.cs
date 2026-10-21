using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MagicIsland.Ocean;
using MagicIsland.TerrainSystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MagicIsland.EditorTools
{
    // Explicit, offline flower shaping inside the original AA batches; no runtime topology work.
    public static class GrasslandFlowerDetailSetup
    {
        public const string Marker="MagicIsland.GrasslandFlowerDetail.v1",Pending="MagicIsland.GrasslandFlowerDetail.Pending.v1";
        static readonly HashSet<string> Known=new HashSet<string>
        {
            GrasslandRevitalizationSetup.Marker,GrasslandIslandMeadowSetup.Marker,GrasslandGroveSetup.Marker,
            GrasslandFloraSetup.Marker,GrasslandGroundLayersSetup.Marker,GrasslandShrubStoneSetup.Marker,
            GrasslandTouchSetup.Marker,GrasslandReadabilitySetup.Marker,GrasslandPatchSetup.Marker,
            GrasslandBlendSetup.Marker,GrasslandWildflowerSetup.Marker,GrasslandEdgeSetup.Marker,GrasslandPathEdgeSetup.Marker,
            GrasslandSampleFinishSetup.Marker,GrasslandSpatialSetup.Marker,GrasslandShrubDetailSetup.Marker,
            GrasslandShrubWindSetup.Marker,GrasslandGroundDetailSetup.Marker,GrasslandBladeDetailSetup.Marker
        };
        public sealed class Edit
        {
            public Mesh Mesh;public Vector3[] Vertices,Normals;public Color[] Colors;public Vector2[][] Channels;
            public int[] Indices;public bool[] Changed;public Bounds Bounds;public string Metadata;
            public int Flowers,Petals,Leaves;
        }
        public static bool Done(string data)=>data.Split(';').Contains(Marker);
        public static void Check(bool ok,string why){if(!ok)throw new InvalidOperationException("Flower detail: "+why);}
        static bool Flower(float weight)=>weight>=-3&&weight<=-2;
        static bool Pollen(Color c)=>c.r>.72f&&c.g<.82f&&c.b<.55f;
        static Vector2 XZ(Vector3 p)=>new Vector2(p.x,p.z);
        // Weld only known petal seams. Stem, pollen, neighboring branches and inactive art never participate.
        static int[][] Petals(int[] ids,int[] starts,int[] faces,Vector3[] v,Vector3[] n)
        {
            var members=ids.ToHashSet();var keys=new Dictionary<(Vector3,Vector3),int>();var weld=new Dictionary<int,int>();
            foreach(int i in ids){var key=(v[i],n[i]);if(!keys.TryGetValue(key,out int id)){id=i;keys.Add(key,id);}weld.Add(i,id);}
            var parent=keys.Values.ToDictionary(i=>i,i=>i);
            int Find(int i){while(parent[i]!=i){parent[i]=parent[parent[i]];i=parent[i];}return i;}
            foreach(int start in starts)
            {
                var tri=new[]{faces[start],faces[start+1],faces[start+2]};int count=tri.Count(members.Contains);
                Check(count==0||count==3,"whole source petal faces");if(count==0)continue;
                int root=Find(weld[tri[0]]);parent[Find(weld[tri[1]])]=root;parent[Find(weld[tri[2]])]=root;
            }
            return ids.GroupBy(i=>Find(weld[i])).Select(g=>g.ToArray()).ToArray();
        }
        public static Edit[] Plan(IslandTerrain terrain)
        {
            var s=terrain.SeasonalDetails.TouchSample;
            Check(terrain.IsValid&&s.ArtVersion==1&&s.Renderers.Length==6&&terrain.transform.localToWorldMatrix==Matrix4x4.identity,"complete original sample and transform");
            var ecology=terrain.World.GetComponentInChildren<WorldWaterEcology>(true);
            Check(ecology.FlowerVisitVersion==1&&ecology.IsValid&&ecology.FlowerVisits.Length==3,"valid existing flower visits");
            var collisions=new HashSet<Mesh>(terrain.GetComponentsInChildren<MeshCollider>(true).Select(c=>c.sharedMesh));
            var result=new List<Edit>();
            foreach(var renderer in s.Renderers)
            {
                var m=renderer.GetComponent<MeshFilter>().sharedMesh;string path=AssetDatabase.GetAssetPath(m);string data=AssetImporter.GetAtPath(path).userData;
                Check(path.StartsWith(BiomeIslandsSetup.Folder+"/",StringComparison.Ordinal)&&!collisions.Contains(m)
                    &&renderer.transform.localToWorldMatrix==terrain.transform.localToWorldMatrix&&renderer.sharedMaterials.Length==1
                    &&renderer.sharedMaterial.shader.name==GrasslandFloraSetup.ShaderName,"preserve custom visual mesh/material/transform");
                Check(GrasslandBladeDetailSetup.Done(data)&&data.Split(';').All(Known.Contains),"preserve custom metadata or incomplete migration");
                Check(m.subMeshCount==1&&m.tangents.Length==0&&m.blendShapeCount==0&&m.bindposes.Length==0&&m.boneWeights.Length==0
                    &&Enumerable.Range(0,3).All(ch=>m.GetVertexAttributeDimension((VertexAttribute)((int)VertexAttribute.TexCoord0+ch))==2)
                    &&Enumerable.Range(3,5).All(ch=>{var a=new List<Vector4>();m.GetUVs(ch,a);return a.Count==0;}),"preserve custom attributes");
                var v=m.vertices;var n=m.normals;var c=m.colors;var uv=m.uv;var roots=m.uv2;var season=m.uv3;var faces=m.triangles;
                Check(new[]{n.Length,c.Length,uv.Length,roots.Length,season.Length}.All(l=>l==v.Length),"complete original channels");
                var points=v.ToList();var normals=n.ToList();var colors=c.ToList();var channels=new[]{uv.ToList(),roots.ToList(),season.ToList()};
                var edit=new Edit{Mesh=m,Changed=new bool[v.Length],Bounds=m.bounds,Metadata=data};
                var startsByVertex=new List<int>[v.Length];var active=new HashSet<int>(faces);
                for(int k=0;k<faces.Length;k+=3)for(int j=0;j<3;j++)
                {int id=faces[k+j];if(startsByVertex[id]==null)startsByVertex[id]=new List<int>();startsByVertex[id].Add(k);}
                var replacements=new Dictionary<int,int[]>();var removed=new HashSet<int>();var appended=new List<int>();
                foreach(var group in Enumerable.Range(0,v.Length).Where(i=>Flower(uv[i].x)).GroupBy(i=>roots[i]))
                {
                    var root=group.Key;float fade=GrasslandGroundDetailSetup.Mask(root,s);
                    if(fade<.12f||Vector2.Distance(root,s.Center)+1.3f>=s.AreaRadius||GrasslandIslandMeadowSetup.RouteDistance(root)<3.65f)continue;
                    var all=group.OrderBy(i=>i).ToArray();int stride=all.Length==1995?285:96;
                    Check(all.Length%stride==0,"known complete flower branches");
                    for(int begin=0;begin<all.Length;begin+=stride)
                    {
                        var branch=all.Skip(begin).Take(stride).ToArray();if(branch.All(i=>!active.Contains(i)))continue;
                        Check(branch.All(active.Contains)&&branch.SequenceEqual(Enumerable.Range(branch[0],stride)),"complete active source branch");
                        var branchSet=branch.ToHashSet();var starts=branch.SelectMany(i=>startsByVertex[i]).Distinct().OrderBy(i=>i).ToArray();
                        Check(starts.All(k=>branchSet.Contains(faces[k])&&branchSet.Contains(faces[k+1])&&branchSet.Contains(faces[k+2])),"no cross-branch faces");
                        var pollen=branch.Where(i=>season[i].y==0&&Pollen(c[i])).ToArray();
                        var petal=branch.Where(i=>season[i].y==0&&!Pollen(c[i])).ToArray();var green=branch.Where(i=>season[i].y==1).ToArray();
                        Check(pollen.Length==(stride==285?144:26)&&petal.Length==(stride==285?90:25)&&green.Length==stride-pollen.Length-petal.Length,"known petal/pollen/stem roles");
                        var petals=Petals(petal,starts,faces,v,n);
                        Check(petals.Length==5&&petals.All(a=>a.Select(i=>(v[i],n[i])).Distinct().Count()==5),"five known five-point petals");
                        var center=new Vector3(pollen.Average(i=>v[i].x),pollen.Average(i=>v[i].y),pollen.Average(i=>v[i].z));
                        var rootIds=petals.Select(a=>a.OrderBy(i=>(XZ(v[i])-XZ(center)).sqrMagnitude).First()).ToArray();
                        var pivot=new Vector3(rootIds.Average(i=>v[i].x),rootIds.Average(i=>v[i].y),rootIds.Average(i=>v[i].z));
                        float minY=green.Min(i=>v[i].y);var baseIds=green.Where(i=>v[i].y<=minY+.006f).ToArray();
                        var anchor=new Vector3(baseIds.Average(i=>v[i].x),baseIds.Average(i=>v[i].y),baseIds.Average(i=>v[i].z));
                        float height=pivot.y-anchor.y;Check(height>.10f&&height<1.5f,"bounded source flower height");
                        float phase=uv[branch[0]].y+anchor.x*1.13f+anchor.z*.71f;
                        float variation=.5f+.5f*Mathf.Sin(phase*1.39f);
                        float heightScale=Mathf.Lerp(1,.86f+.14f*variation,fade),headScale=Mathf.Lerp(1,.88f+.13f*(.5f+.5f*Mathf.Sin(phase*.93f)),fade);
                        var rotation=Quaternion.Euler(Mathf.Sin(phase)*7*fade,Mathf.Sin(phase*.81f)*17*fade,Mathf.Cos(phase*1.17f)*6*fade);
                        var bend=new Vector3(Mathf.Sin(phase*.67f),0,Mathf.Cos(phase*.89f))*(.025f*fade);
                        Vector3 Stem(Vector3 p)
                        {float t=Mathf.Clamp01((p.y-anchor.y)/height);return new Vector3(p.x,anchor.y+(p.y-anchor.y)*heightScale,p.z)+bend*t*t;}
                        var nextPivot=Stem(pivot);
                        Vector3 Head(Vector3 p)=>nextPivot+rotation*(p-pivot)*headScale;
                        void Validate(Vector3 p)
                        {Check(XZ(p-root.ToVector3()).magnitude<=1.16f&&edit.Bounds.Contains(p)&&p.y>=season[branch[0]].x-.025f,"original static/culling/ground envelope");}
                        foreach(int i in green)
                        {
                            if(baseIds.Contains(i))continue;
                            float t=Mathf.Clamp01((v[i].y-anchor.y)/height);
                            points[i]=Vector3.Lerp(Stem(v[i]),Head(v[i]),Mathf.SmoothStep(0,1,Mathf.InverseLerp(.78f,.98f,t)));
                            var normal=n[i];if(normal.y<0)normal=-normal;
                            normals[i]=(Vector3.Lerp(n[i],normal,fade)+Vector3.up*(.85f*fade)).normalized;
                            var tint=new Color(.43f,.57f,.36f,c[i].a);colors[i]=Color.Lerp(c[i],tint,.70f*fade*Mathf.SmoothStep(0,1,t*5));
                            edit.Changed[i]=true;Validate(points[i]);
                        }
                        foreach(int i in pollen)
                        {points[i]=Head(v[i]);normals[i]=rotation*n[i];colors[i]=Color.Lerp(c[i],new Color(.91f,.75f,.40f,c[i].a),.38f*fade);edit.Changed[i]=true;Validate(points[i]);}
                        foreach(var part in petals)
                        {
                            var unique=part.GroupBy(i=>(v[i],n[i])).Select(g=>g.First()).ToArray();
                            int inner=unique.OrderBy(i=>(XZ(v[i])-XZ(center)).sqrMagnitude).First();
                            int tip=unique.OrderByDescending(i=>Vector2.Distance(XZ(v[i]),XZ(v[inner]))).First();
                            int ridge=unique.Where(i=>i!=inner&&i!=tip).OrderByDescending(i=>n[i].y).First();
                            var sides=unique.Where(i=>i!=inner&&i!=tip&&i!=ridge).ToArray();
                            var axis=(XZ(v[tip])-XZ(v[inner])).normalized;var side=new Vector2(-axis.y,axis.x);
                            int left=Vector2.Dot(XZ(v[sides[0]]-v[inner]),side)>0?sides[0]:sides[1],right=left==sides[0]?sides[1]:sides[0];
                            float length=Vector2.Distance(XZ(v[tip]),XZ(v[inner])),width=Vector2.Distance(XZ(v[left]),XZ(v[right]))*.5f;
                            Check(length>.07f&&length<.6f&&width>.02f&&width<.3f,"recognizable source petal dimensions");
                            var origin=v[inner];float rise=Mathf.Max(.008f,v[ridge].y-origin.y);
                            Vector3 Local(float x,float z,float y)=>new Vector3(origin.x+axis.x*x+side.x*z,origin.y+y,origin.z+axis.y*x+side.y*z);
                            var shape=new Dictionary<int,Vector3>{{inner,origin},{left,Local(length*.44f,width*1.03f,rise*.35f)},
                                {right,Local(length*.44f,-width*1.03f,rise*.35f)},{tip,Local(length*.86f,0,rise*.70f)},{ridge,Local(length*.46f,0,rise*.90f)}};
                            foreach(int i in part)
                            {
                                int source=unique.First(j=>v[j]==v[i]&&n[j]==n[i]);float across=Vector2.Dot(XZ(shape[source]-origin),side)/width;
                                points[i]=Head(Vector3.Lerp(v[i],shape[source],fade));
                                normals[i]=rotation*new Vector3(-axis.x*.13f+side.x*across*.18f,1,-axis.y*.13f+side.y*across*.18f).normalized;
                                edit.Changed[i]=true;Validate(points[i]);
                            }
                            int first=points.Count;
                            foreach(float sign in new[]{1f,-1f})
                            {
                                var p=Head(Local(length*.76f,width*.76f*sign,rise*.62f));Validate(p);points.Add(p);
                                normals.Add(rotation*new Vector3(-axis.x*.13f+side.x*sign*.15f,1,-axis.y*.13f+side.y*sign*.15f).normalized);
                                colors.Add(Color.Lerp(c[left],c[tip],.65f));channels[0].Add(Vector2.Lerp(uv[left],uv[tip],.65f));channels[1].Add(root);channels[2].Add(season[tip]);
                            }
                            var ring=new[]{inner,left,first,tip,first+1,right};var triangles=new List<int>();
                            for(int k=0;k<ring.Length;k++)
                            {
                                int a=ridge,b=ring[k],d=ring[(k+1)%ring.Length];if(Vector3.Cross(points[b]-points[a],points[d]-points[a]).y<0){int swap=b;b=d;d=swap;}
                                triangles.AddRange(new[]{a,b,d,a,d,b});
                            }
                            var set=part.ToHashSet();var petalStarts=starts.Where(k=>set.Contains(faces[k])).ToArray();
                            Check(petalStarts.Length==6&&petalStarts.All(k=>set.Contains(faces[k+1])&&set.Contains(faces[k+2])),"six original petal faces");
                            replacements.Add(petalStarts[0],triangles.ToArray());foreach(int k in petalStarts.Skip(1))removed.Add(k);edit.Petals++;
                        }
                        if(Mathf.Sin(phase*1.91f)>.35f)
                        {
                            int first=points.Count;var direction=new Vector3(Mathf.Cos(phase+1.8f),.38f,Mathf.Sin(phase+1.8f)).normalized;
                            var side=new Vector3(-direction.z,0,direction.x).normalized;float size=Mathf.Lerp(.09f,.15f,variation);
                            var attach=Vector3.Lerp(anchor,pivot,.43f);
                            var leaf=new[]{attach,attach+direction*(size*.48f)+side*(size*.19f),attach+direction*(size*.52f)+Vector3.up*(size*.08f),
                                attach+direction*(size*.48f)-side*(size*.19f),attach+direction*size};
                            foreach(var p in leaf)
                            {
                                var q=Stem(p);Validate(q);points.Add(q);normals.Add((Vector3.up-direction*.2f).normalized);colors.Add(new Color(.45f,.59f,.37f,1));
                                float t=Mathf.Clamp01((p.y-anchor.y)/height);channels[0].Add(new Vector2(Mathf.Lerp(-2,uv[pollen[0]].x,t),uv[branch[0]].y));
                                channels[1].Add(root);channels[2].Add(new Vector2(season[branch[0]].x,1));
                            }
                            var pattern=new[]{0,1,2,0,2,3,1,4,2,2,4,3};
                            for(int k=0;k<pattern.Length;k+=3){int a=first+pattern[k],b=first+pattern[k+1],d=first+pattern[k+2];appended.AddRange(new[]{a,b,d,a,d,b});}
                            edit.Leaves++;
                        }
                        edit.Flowers++;
                    }
                }
                var indices=new List<int>();for(int k=0;k<faces.Length;k+=3)
                {if(replacements.TryGetValue(k,out var added))indices.AddRange(added);else if(!removed.Contains(k))indices.AddRange(new[]{faces[k],faces[k+1],faces[k+2]});}
                indices.AddRange(appended);edit.Vertices=points.ToArray();edit.Normals=normals.ToArray();edit.Colors=colors.ToArray();
                edit.Channels=channels.Select(a=>a.ToArray()).ToArray();edit.Indices=indices.ToArray();
                Check(m.indexFormat==IndexFormat.UInt32||points.Count<=65535,"preserve original UInt16 capacity");result.Add(edit);
            }
            Check(result.Select(e=>e.Mesh).Distinct().Count()==6&&result.Sum(e=>e.Flowers)>100&&result.Sum(e=>e.Petals)==result.Sum(e=>e.Flowers)*5&&result.Sum(e=>e.Leaves)>10,"bounded original flowers and sparse leaves");
            foreach(var link in ecology.FlowerVisits)
            {
                var e=result.Single(p=>p.Mesh==link.Source.GetComponent<MeshFilter>().sharedMesh);
                Check(e.Changed[link.Vertex]&&e.Indices.Contains(link.Vertex)&&Pollen(e.Colors[link.Vertex]),"linked pollen keeps its original active index");
            }
            return result.ToArray();
        }
        static Vector3 ToVector3(this Vector2 p)=>new Vector3(p.x,0,p.y);
        static void Clean(string p)=>File.WriteAllText(p,string.Join("\n",File.ReadAllLines(p).Select(l=>l.TrimEnd()))+"\n");
        [MenuItem("Magic Island/Art/Refine Grassland Flower Detail Sample (Explicit)")]
        public static void Rebuild()
        {
            Check(!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling,"Edit Mode required");
            var root=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);var backups=new Dictionary<string,byte[]>();bool saved=false;
            try
            {
                var t=root.GetComponent<IslandTerrain>();Check(t.IsValid&&t.SeasonalDetails.TouchSample.Renderers.Length==6,"complete original sample");
                var paths=t.SeasonalDetails.TouchSample.Renderers.Select(r=>AssetDatabase.GetAssetPath(r.GetComponent<MeshFilter>().sharedMesh)).ToArray();var data=paths.Select(p=>AssetImporter.GetAtPath(p).userData).ToArray();
                Check(!data.Any(d=>d.Split(';').Contains(Pending)),"preserve interrupted write");
                if(data.Any(Done)){Check(data.All(Done),"preserve partial migration");saved=true;Debug.Log("Grassland flower detail and later manual edits retained.");return;}
                var plan=Plan(t);foreach(string p in paths.SelectMany(p=>new[]{p,p+".meta"}))backups.Add(p,File.ReadAllBytes(p));
                var first=AssetImporter.GetAtPath(paths[0]);first.userData=data[0]+";"+Pending;first.SaveAndReimport();
                foreach(var e in plan.Where(e=>e.Flowers>0))
                {
                    e.Mesh.vertices=e.Vertices;e.Mesh.normals=e.Normals;e.Mesh.colors=e.Colors;
                    for(int ch=0;ch<3;ch++)e.Mesh.SetUVs(ch,e.Channels[ch]);e.Mesh.SetTriangles(e.Indices,0,false);e.Mesh.bounds=e.Bounds;EditorUtility.SetDirty(e.Mesh);
                }
                AssetDatabase.SaveAssets();
                foreach(var e in plan.Skip(1).Concat(plan.Take(1)))
                {string p=AssetDatabase.GetAssetPath(e.Mesh);var importer=AssetImporter.GetAtPath(p);importer.userData=e.Metadata+";"+Marker;importer.SaveAndReimport();Clean(p);Clean(p+".meta");}
                saved=true;Debug.Log("Grassland flower detail baked: "+plan.Sum(e=>e.Flowers)+" existing flowers, "+plan.Sum(e=>e.Petals)+" rounded petals, "+plan.Sum(e=>e.Leaves)+" small leaves; original roots and visit indices retained.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
                if(!saved){foreach(var p in backups)File.WriteAllBytes(p.Key,p.Value);foreach(string p in backups.Keys.Where(p=>!p.EndsWith(".meta",StringComparison.Ordinal)))AssetDatabase.ImportAsset(p,ImportAssetOptions.ForceUpdate);}
            }
        }
    }
}
