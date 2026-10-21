using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MagicIsland.TerrainSystem;
using UnityEditor;
using UnityEngine;

namespace MagicIsland.EditorTools
{
    // One explicit art migration. Original roots retain ownership of every leaf, branch and ground detail.
    public static class GrasslandShrubDetailSetup
    {
        public const string Marker="MagicIsland.GrasslandShrubDetail.v1",Pending="MagicIsland.GrasslandShrubDetail.Pending.v1";
        static readonly HashSet<string> Known=new HashSet<string>
        {
            GrasslandRevitalizationSetup.Marker,GrasslandIslandMeadowSetup.Marker,GrasslandGroveSetup.Marker,
            GrasslandFloraSetup.Marker,GrasslandGroundLayersSetup.Marker,GrasslandShrubStoneSetup.Marker,
            GrasslandTouchSetup.Marker,GrasslandReadabilitySetup.Marker,GrasslandPatchSetup.Marker,
            GrasslandBlendSetup.Marker,GrasslandWildflowerSetup.Marker,GrasslandEdgeSetup.Marker,
            GrasslandPathEdgeSetup.Marker,GrasslandSampleFinishSetup.Marker,GrasslandSpatialSetup.Marker
        };
        public sealed class Edit
        {
            public Mesh Mesh;public Vector3[] Vertices,Normals;public Color[] Colors;public Vector2[][] Channels;
            public int[] Indices;public bool[] Changed;public Bounds Bounds;public string Metadata;
            public Vector2[] Roots;public int Lobes,Leaves,Branches,Flowers;
        }
        public static bool Done(string data)=>data.Split(';').Contains(Marker);
        static void Check(bool ok,string why){if(!ok)throw new InvalidOperationException("Shrub detail: "+why);}
        static bool Grass(float w)=>w>=-5&&w<=-4;

        // Join exact positional seams only inside one original root. Positive-weight seeds distinguish leaf lobes
        // from adjacent zero-weight rocks, even though the source uses split normals and separate triangle vertices.
        public static int[][] LeafLobes(int[] ids,int[] faces,Vector3[] v,Vector2[] uv)
        {
            var parent=new Dictionary<int,int>();foreach(int i in ids.Where(i=>uv[i].x>=0))parent.Add(i,i);
            int Find(int i){int a=i;while(parent[a]!=a)a=parent[a];while(parent[i]!=i){int next=parent[i];parent[i]=a;i=next;}return a;}
            var positions=new Dictionary<Vector3,int>();
            foreach(int i in parent.Keys.ToArray())
            {if(positions.TryGetValue(v[i],out int first))parent[Find(i)]=Find(first);else positions.Add(v[i],i);}
            for(int i=0;i<faces.Length;i+=3)
                if(parent.ContainsKey(faces[i])&&parent.ContainsKey(faces[i+1])&&parent.ContainsKey(faces[i+2]))
                {int a=Find(faces[i]);parent[Find(faces[i+1])]=a;parent[Find(faces[i+2])]=a;}
            return parent.Keys.ToArray().GroupBy(Find).Where(g=>g.Any(i=>uv[i].x>0)).Select(g=>g.OrderBy(i=>i).ToArray()).OrderBy(a=>a[0]).ToArray();
        }

        public static Edit[] Plan(IslandTerrain t)
        {
            var s=t.SeasonalDetails.TouchSample;
            Check(t.IsValid&&s.ArtVersion==1&&s.Renderers.Length==6&&t.transform.lossyScale==Vector3.one&&t.transform.rotation==Quaternion.identity,"original complete sample");
            var colliders=new HashSet<Mesh>(t.GetComponentsInChildren<MeshCollider>(true).Select(c=>c.sharedMesh));
            foreach(var r in s.Renderers)
            {
                var m=r.GetComponent<MeshFilter>().sharedMesh;string path=AssetDatabase.GetAssetPath(m);string data=AssetImporter.GetAtPath(path).userData;
                Check(path.StartsWith(BiomeIslandsSetup.Folder+"/",StringComparison.Ordinal)&&!colliders.Contains(m)
                    &&r.transform.localToWorldMatrix==t.transform.localToWorldMatrix&&m.subMeshCount==1&&m.tangents.Length==0
                    &&m.blendShapeCount==0&&m.bindposes.Length==0&&m.boneWeights.Length==0,"original static visual mesh");
                Check(r.sharedMaterials.Length==1&&r.sharedMaterial.shader.name==GrasslandFloraSetup.ShaderName
                    &&GrasslandSpatialSetup.Done(data)&&data.Split(';').All(Known.Contains),"preserve custom material / metadata");
                Check(Enumerable.Range(0,3).All(ch=>m.GetVertexAttributeDimension((UnityEngine.Rendering.VertexAttribute)
                    ((int)UnityEngine.Rendering.VertexAttribute.TexCoord0+ch))==2),"preserve custom UV dimensions");
                Check(Enumerable.Range(3,5).All(ch=>{var a=new List<Vector4>();m.GetUVs(ch,a);return a.Count==0;}),"preserve custom UV channels");
            }
            var world=new GrasslandRevitalizationSetup.AuthoringWorld(t,new Vector4(s.Center.x-26,s.Center.y-26,52,52));
            var flower=JsonUtility.FromJson<BeachCompositionSetup.Export>(File.ReadAllText(GrasslandCompositionSetup.Source)).templates.Single(a=>a.name=="Flowers");
            Check(flower.positions.Length==1995*3,"original whole-branch flower template");
            var result=new List<Edit>();
            foreach(var renderer in s.Renderers)
            {
                var m=renderer.GetComponent<MeshFilter>().sharedMesh;var v=m.vertices;var n=m.normals;var c=m.colors;
                var uv=m.uv;var roots=m.uv2;var season=m.uv3;var faces=m.triangles;
                Check(new[]{n.Length,c.Length,uv.Length,roots.Length,season.Length}.All(a=>a==v.Length),"complete original attributes");
                var e=new Edit{Mesh=m,Changed=new bool[v.Length],Bounds=m.bounds,Metadata=AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(m)).userData};
                var points=v.ToList();var normals=n.ToList();var colors=c.ToList();var channels=new[]{uv.ToList(),roots.ToList(),season.ToList()};var indices=faces.ToList();var editedRoots=new List<Vector2>();
                if(renderer.name.StartsWith("Meadow undergrowth ",StringComparison.Ordinal))
                foreach(var group in faces.Distinct().GroupBy(i=>roots[i]))
                {
                    var root=group.Key;var ids=group.ToArray();
                    if(Vector2.Distance(root,s.Center)+1.3f>=s.AreaRadius||!ids.Any(i=>Grass(uv[i].x))||!ids.Any(i=>uv[i].x>0))continue;
                    float floor=world.Floor(root);
                    if(!world.Clear(root,floor,1.3f,1.7f)||GrasslandIslandMeadowSetup.RouteDistance(root)<3.65f)continue;
                    var lobes=LeafLobes(ids,faces,v,uv);
                    Check(lobes.Length==3||lobes.Length==7,"recognizable complete three/seven leaf lobes "+root+" "+lobes.Length);
                    float phase=uv[ids[0]].y;int order=editedRoots.Count;editedRoots.Add(root);e.Lobes+=lobes.Length;
                    var crowns=new List<Bounds>();
                    for(int lobe=0;lobe<lobes.Length;lobe++)
                    {
                        var part=lobes[lobe];var box=new Bounds(v[part[0]],Vector3.zero);foreach(int i in part)box.Encapsulate(v[i]);
                        Check(part.Length>=70&&box.size.x>.10f&&box.size.y>.1f&&box.size.z>.1f,"whole original rounded lobe");
                        float a=phase+lobe*2.399963f;var outward=new Vector2(box.center.x-root.x,box.center.z-root.y).normalized;
                        var center=box.center+new Vector3(outward.x*.055f,Mathf.Sin(a*1.31f)*.055f,outward.y*.055f);
                        var scale=new Vector3(.83f+.06f*Mathf.Sin(a),.90f+.14f*Mathf.Sin(a*.73f),.88f+.06f*Mathf.Cos(a));
                        var rotation=Quaternion.Euler(0,Mathf.Sin(a)*12,0);var extent=Vector3.Scale(box.extents,scale);
                        foreach(int i in part)
                        {
                            var delta=Vector3.Scale(v[i]-box.center,scale);
                            float angle=Mathf.Atan2(delta.z,delta.x);float scallop=1+.035f*Mathf.Sin(angle*5+a)*Mathf.Sin(Mathf.PI*Mathf.InverseLerp(box.min.y,box.max.y,v[i].y));
                            delta.x*=scallop;delta.z*=scallop;var point=center+rotation*delta;
                            Check(Vector2.Distance(new Vector2(point.x,point.z),root)<=1.16f&&point.y>=floor-.08f&&point.y<=floor+1.6f,"bounded reshaped leaf lobe");
                            var smooth=rotation*new Vector3(delta.x/(extent.x*extent.x),delta.y/(extent.y*extent.y),delta.z/(extent.z*extent.z)).normalized;
                            float cap=Mathf.InverseLerp(box.min.y,box.max.y,v[i].y);
                            float light=Mathf.Clamp01(cap*.73f+Mathf.Max(0,smooth.y)*.15f+.06f*Mathf.Sin(a));
                            var paint=Color.Lerp(new Color(.34f,.48f,.29f),new Color(.64f,.73f,.44f),light);paint.a=c[i].a;
                            points[i]=point;normals[i]=Vector3.Slerp(n[i],smooth,.82f).normalized;colors[i]=paint;e.Changed[i]=true;
                        }
                        var crown=new Bounds(points[part[0]],Vector3.zero);foreach(int i in part)crown.Encapsulate(points[i]);crowns.Add(crown);
                    }
                    var weld=new Dictionary<(Vector3,Vector3,Color,Vector2,Vector2),int>();
                    int Vertex(Vector3 q,Vector3 normal,Color paint,float weight,float seasonal)
                    {
                        Check(new Vector2(q.x,q.z).magnitude<=1.16f&&q.y>=0&&q.y<1.55f,"complete detail inside original envelope");
                        var p=root+new Vector2(q.x,q.z);var point=new Vector3(p.x,world.Floor(p)+.018f+q.y,p.y);normal.Normalize();paint.a=1;
                        var a=new Vector2(weight,phase);var b=new Vector2(floor,seasonal);var key=(point,normal,paint,a,b);
                        if(weld.TryGetValue(key,out int id))return id;
                        id=points.Count;weld.Add(key,id);points.Add(point);normals.Add(normal);colors.Add(paint);channels[0].Add(a);channels[1].Add(root);channels[2].Add(b);return id;
                    }
                    void Branch(Vector3 start,Vector3 end,float width)
                    {
                        var axis=(end-start).normalized;var side=Vector3.Cross(axis,Vector3.forward).normalized;var cross=Vector3.Cross(axis,side).normalized;
                        var a=new int[5];var b=new int[5];
                        for(int k=0;k<5;k++)
                        {
                            float angle=k*Mathf.PI*2/5;var normal=side*Mathf.Cos(angle)+cross*Mathf.Sin(angle);
                            a[k]=Vertex(start+normal*width,normal,new Color(.44f,.36f,.25f),Mathf.Clamp01(start.y),0);
                            b[k]=Vertex(end+normal*width*.38f,normal,new Color(.52f,.43f,.29f),Mathf.Clamp01(end.y),0);
                        }
                        for(int k=0;k<5;k++){int next=(k+1)%5;indices.AddRange(new[]{a[k],b[k],a[next],a[next],b[k],b[next]});}e.Branches++;
                    }
                    var reach=new Vector3(Mathf.Cos(phase)*.27f,.50f,Mathf.Sin(phase)*.27f);
                    Branch(new Vector3(0,.03f,0),reach,.025f);Branch(reach*.55f,new Vector3(-reach.x*.85f,.43f,-reach.z*.85f),.018f);
                    for(int leaf=0;leaf<12;leaf++)
                    {
                        float a=phase+leaf*2.399963f;var direction=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));var side=new Vector3(-direction.z,0,direction.x);
                        var crown=crowns[leaf%crowns.Count];
                        var anchor=crown.center-new Vector3(root.x,floor+.018f,root.y)
                            +new Vector3(direction.x*crown.extents.x*.84f,crown.extents.y*(.10f+(leaf%3)*.13f),direction.z*crown.extents.z*.84f);
                        float length=.13f+(leaf%3)*.02f,width=.045f+(leaf%2)*.012f;var tip=anchor+direction*length+Vector3.up*.035f;
                        // The base is embedded in an actual lobe; only the pointed tip extends beyond its silhouette.
                        var middle=Vector3.Lerp(anchor,tip,.52f)+Vector3.up*.025f;var normal=(Vector3.up*.75f+direction*.25f).normalized;
                        var paint=Color.Lerp(new Color(.48f,.61f,.34f),new Color(.66f,.75f,.46f),(leaf%4)/3f);
                        int b=Vertex(anchor,normal,paint,Mathf.Clamp01(anchor.y),1),left=Vertex(middle-side*width,normal,paint,Mathf.Clamp01(middle.y),1);
                        int center=Vertex(middle+Vector3.up*.018f,normal,paint,Mathf.Clamp01(middle.y),1),right=Vertex(middle+side*width,normal,paint,Mathf.Clamp01(middle.y),1),top=Vertex(tip,normal,paint,Mathf.Clamp01(tip.y),1);
                        foreach(var tri in new[]{new[]{b,left,center},new[]{left,top,center},new[]{top,right,center},new[]{right,b,center}})
                            indices.AddRange(new[]{tri[0],tri[1],tri[2],tri[0],tri[2],tri[1]});e.Leaves++;
                    }
                    for(int blade=0;blade<4;blade++)
                    {
                        float a=phase+blade*1.63f;var direction=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));var side=new Vector3(-direction.z,0,direction.x)*.045f;
                        var anchor=direction*(.64f+(blade%2)*.12f);var middle=anchor+direction*.045f+Vector3.up*.11f;var tip=anchor+direction*.12f+Vector3.up*.20f;
                        var paint=new Color(.48f,.59f,.36f);int b=Vertex(anchor-side,Vector3.up,paint,-4,1),right=Vertex(anchor+side,Vector3.up,paint,-4,1);
                        int l=Vertex(middle-side*.6f,Vector3.up,paint,-4.2f,1),r=Vertex(middle+side*.6f,Vector3.up,paint,-4.2f,1),top=Vertex(tip,Vector3.up,paint,-4.4f,1);
                        indices.AddRange(new[]{b,l,right,right,l,r,l,top,r});
                    }
                    if(order%3==0)
                    {
                        int start=((order*2)%7)*285;float bottom=Enumerable.Range(start,285).Min(i=>flower.positions[i*3+1]);
                        var baseIds=Enumerable.Range(start,285).Where(i=>flower.positions[i*3+1]<bottom+.001f).ToArray();
                        var sourceRoot=new Vector3(baseIds.Average(i=>flower.positions[i*3]),0,baseIds.Average(i=>flower.positions[i*3+2]));var map=new Dictionary<int,int>();
                        foreach(int i in Enumerable.Range(start,285))
                        {
                            var q=(new Vector3(flower.positions[i*3],flower.positions[i*3+1],flower.positions[i*3+2])-sourceRoot)*.43f+new Vector3(.12f,0,-.73f);
                            var paint=new Color(flower.colors[i*4],flower.colors[i*4+1],flower.colors[i*4+2]);bool petal=paint.b>paint.g||paint.r>.95f,pollen=paint.r>.85f&&paint.g<.7f;
                            if(petal)paint=new Color(.94f,.92f,.81f);else if(pollen)paint=new Color(.88f,.72f,.39f);
                            map[i]=Vertex(q,new Vector3(flower.normals[i*3],flower.normals[i*3+1],flower.normals[i*3+2]),paint,-2-Mathf.Clamp01(q.y),petal||pollen?0:1);
                        }
                        for(int i=0;i<flower.triangles.Length;i+=3)if(map.ContainsKey(flower.triangles[i]))
                        {Check(map.ContainsKey(flower.triangles[i+1])&&map.ContainsKey(flower.triangles[i+2]),"whole original flower branch");indices.AddRange(new[]{map[flower.triangles[i]],map[flower.triangles[i+1]],map[flower.triangles[i+2]]});}e.Flowers++;
                    }
                }
                Check(m.indexFormat==UnityEngine.Rendering.IndexFormat.UInt32||points.Count<=65535,"preserve original index format "+m.name+" "+points.Count);
                e.Vertices=points.ToArray();e.Normals=normals.ToArray();e.Colors=colors.ToArray();e.Channels=channels.Select(a=>a.ToArray()).ToArray();e.Indices=indices.ToArray();e.Roots=editedRoots.ToArray();
                foreach(var point in points.Where((p,i)=>i>=v.Length||e.Changed[i])){e.Bounds.Encapsulate(point+Vector3.one*.35f);e.Bounds.Encapsulate(point-Vector3.one*.35f);}result.Add(e);
            }
            Check(result.Sum(e=>e.Roots.Length)>=8,"bounded original shrub selection");return result.ToArray();
        }
        static void Clean(string p)=>File.WriteAllText(p,string.Join("\n",File.ReadAllLines(p).Select(l=>l.TrimEnd()))+"\n");
        [MenuItem("Magic Island/Art/Refine Grassland Shrub Detail Sample (Explicit)")]
        public static void Rebuild()
        {
            Check(!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling,"Edit Mode required");
            var root=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);var backups=new Dictionary<string,byte[]>();bool saved=false;
            try
            {
                var t=root.GetComponent<IslandTerrain>();Check(t.IsValid&&t.SeasonalDetails.TouchSample.Renderers.Length==6,"complete sample");
                var paths=t.SeasonalDetails.TouchSample.Renderers.Select(r=>AssetDatabase.GetAssetPath(r.GetComponent<MeshFilter>().sharedMesh)).ToArray();var data=paths.Select(p=>AssetImporter.GetAtPath(p).userData).ToArray();
                Check(!data.Any(d=>d.Split(';').Contains(Pending)),"preserve interrupted write");
                if(data.Any(Done)){Check(data.All(Done),"preserve partial migration");saved=true;Debug.Log("Grassland shrub detail and later manual edits retained.");return;}
                Physics.SyncTransforms();var plan=Plan(t);
                foreach(string p in paths.SelectMany(p=>new[]{p,p+".meta"}))backups.Add(p,File.ReadAllBytes(p));
                var first=AssetImporter.GetAtPath(paths[0]);first.userData=data[0]+";"+Pending;first.SaveAndReimport();
                foreach(var e in plan)
                {e.Mesh.SetVertices(e.Vertices);e.Mesh.SetNormals(e.Normals);e.Mesh.SetColors(e.Colors);for(int ch=0;ch<3;ch++)e.Mesh.SetUVs(ch,e.Channels[ch]);e.Mesh.SetTriangles(e.Indices,0,false);e.Mesh.bounds=e.Bounds;EditorUtility.SetDirty(e.Mesh);}
                AssetDatabase.SaveAssets();
                foreach(var e in plan.Skip(1).Concat(plan.Take(1)))
                {string p=AssetDatabase.GetAssetPath(e.Mesh);var importer=AssetImporter.GetAtPath(p);importer.userData=e.Metadata+";"+Marker;importer.SaveAndReimport();Clean(p);Clean(p+".meta");}
                saved=true;Debug.Log("Grassland shrub detail baked: "+plan.Sum(e=>e.Roots.Length)+" original shrubs, "+plan.Sum(e=>e.Lobes)+" varied leaf lobes, "+plan.Sum(e=>e.Leaves)+" pointed leaves, "+plan.Sum(e=>e.Branches)+" fine branches, "+plan.Sum(e=>e.Flowers)+" base flowers.");
            }
            finally
            {PrefabUtility.UnloadPrefabContents(root);if(!saved){foreach(var p in backups)File.WriteAllBytes(p.Key,p.Value);foreach(string p in backups.Keys.Where(p=>!p.EndsWith(".meta",StringComparison.Ordinal)))AssetDatabase.ImportAsset(p,ImportAssetOptions.ForceUpdate);}}
        }
    }
}
