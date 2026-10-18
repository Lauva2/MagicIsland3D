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
    // Editor-baked union of road arms and junctions; shares the existing trail shader/material.
    public static class GrassDesertTrailSetup
    {
        public const string GroupName="Grass desert trail";
        public const string MeshPath=StarterIslandWorldSetup.Folder+"/Grass Desert Trail.asset";
        public const string OldPath=LakeSnowTrailSetup.OldPath;
        public const string MaterialPath=LakeSnowTrailSetup.MaterialPath;
        const float Width=1.95f,Cut=6;
        public static readonly int[,] Edges={{0,1},{1,2},{2,3},{0,4},{0,5},{1,6},{3,7}};
        public sealed class Plan
        {public int[] Remainder,RemovedQuads;public Vector2[] Points;public float[] Widths,EndDistance;}
        static void Require(bool ok,string why){if(!ok)throw new InvalidOperationException("Grass desert trail: "+why);}
        static Vector2 XZ(Vector3 p)=>new Vector2(p.x,p.z);
        static Vector2 Route(int i)=>new Vector2((float)StarterIslandLayout.Route[i,0],(float)StarterIslandLayout.Route[i,1]);
        public static Plan Prepare(Mesh mesh)
        {
            var v=mesh.vertices;var indices=mesh.triangles;
            Require(v.Length%4==0&&indices.Length%6==0,"preserve unsupported/manual road topology");
            var removed=new List<int>();var remaining=new List<int>();var counts=new int[3];
            var caps=new Dictionary<int,Vector2[]>();int previous=-1;
            for(int cursor=0;cursor<indices.Length;cursor+=6)
            {
                int a=indices[cursor],q=a/4;
                Require(a>=0&&a%4==0&&a+3<v.Length&&q>previous&&indices.Skip(cursor).Take(6).SequenceEqual(new[]{a,a+1,a+2,a,a+2,a+3}),"preserve custom triangulation/order");previous=q;
                var center=(XZ(v[a])+XZ(v[a+1])+XZ(v[a+2])+XZ(v[a+3]))*.25f;bool selected=false;
                foreach(int segment in new[]{2,3,4,5,6,21,23})
                {
                    var p=Route(segment-1);var b=Route(segment);
                    if(StarterIslandLayout.SegmentDistance(center.x,center.y,p.x,p.y,b.x,b.y)>.01)continue;
                    if(segment>=3&&segment<=5){selected=true;counts[segment-3]++;}
                    else if((segment==2||segment==21)&&Vector2.Distance(center,b)<8)
                    {selected=true;if(!caps.ContainsKey(segment))caps[segment]=new[]{XZ(v[a]),XZ(v[a+3])};}
                    else if((segment==6||segment==23)&&Vector2.Distance(center,p)<8)
                    {selected=true;caps[segment]=new[]{XZ(v[a+1]),XZ(v[a+2])};}
                }
                if(selected)removed.Add(q);else remaining.AddRange(indices.Skip(cursor).Take(6));
            }
            for(int i=0;i<3;i++)Require(counts[i]==Mathf.CeilToInt(Vector2.Distance(Route(i+2),Route(i+3))),"complete west route span "+(i+3));
            Require(caps.Count==4,"complete original side-road connections");
            int[] capOrder={2,21,23,6};var points=new[]{Route(2),Route(3),Route(4),Route(5)}.Concat(capOrder.Select(i=>(caps[i][0]+caps[i][1])*.5f)).ToArray();
            var widths=new[]{Width,Width,Width,Width}.Concat(capOrder.Select(i=>Vector2.Distance(caps[i][0],caps[i][1])*.5f)).ToArray();
            var distance=Enumerable.Range(0,8).Select(i=>i>=4?0:float.MaxValue).ToArray();
            for(int pass=0;pass<8;pass++)for(int e=0;e<Edges.GetLength(0);e++)
            {int a=Edges[e,0],b=Edges[e,1];float length=Vector2.Distance(points[a],points[b]);distance[a]=Mathf.Min(distance[a],distance[b]+length);distance[b]=Mathf.Min(distance[b],distance[a]+length);}
            return new Plan{Remainder=remaining.ToArray(),RemovedQuads=removed.ToArray(),Points=points,Widths=widths,EndDistance=distance};
        }
        public static void PreserveOnWorldRebuild(Transform world,Mesh freshPaths)
        {
            var local=world.Find(GroupName);if(local==null)return;
            Require(local.GetComponent<MeshFilter>()?.sharedMesh!=null,"repair incomplete authored road explicitly");
            freshPaths.SetTriangles(Prepare(freshPaths).Remainder,0,false);
        }
        static int[] Neighbours(int node)
        {
            var list=new List<int>();for(int i=0;i<Edges.GetLength(0);i++)
            {if(Edges[i,0]==node)list.Add(Edges[i,1]);if(Edges[i,1]==node)list.Add(Edges[i,0]);}return list.ToArray();
        }
        public static Mesh Build(IslandTerrain terrain,Plan plan)
        {
            var batch=new Builder(terrain,plan);
            for(int node=0;node<4;node++)
            {
                var center=plan.Points[node];var arms=Neighbours(node).Select(n=>(plan.Points[n]-center).normalized)
                    .OrderBy(d=>Mathf.Repeat(Mathf.Atan2(d.y,d.x),Mathf.PI*2)).ToArray();
                var boundary=new List<Vector2>();
                void Add(Vector2 p){if(boundary.Count==0||Vector2.Distance(boundary.Last(),p)>.0001f)boundary.Add(p);}
                for(int i=0;i<arms.Length;i++)
                {
                    var d=arms[i];var left=new Vector2(-d.y,d.x);float angle=Mathf.Repeat(Mathf.Atan2(d.y,d.x),Mathf.PI*2);
                    var next=arms[(i+1)%arms.Length];float gap=Mathf.Repeat(Mathf.Atan2(next.y,next.x)-angle,Mathf.PI*2);
                    Add(center+d*Cut-left*Width);Add(center+d*Cut+left*Width);
                    if(gap<=Mathf.PI+.0001f)
                    {
                        Require(Width/Mathf.Tan(gap*.5f)<Cut-.05f,"unsupported acute fork; preserve existing road");
                        float radius=Width/Mathf.Sin(gap*.5f),bisector=angle+gap*.5f;
                        Add(center+new Vector2(Mathf.Cos(bisector),Mathf.Sin(bisector))*radius);
                    }
                    else
                    {
                        int steps=Mathf.CeilToInt((gap-Mathf.PI)/(Mathf.PI/12));
                        for(int j=0;j<=steps;j++)
                        {float a=angle+Mathf.PI*.5f+(gap-Mathf.PI)*j/steps;Add(center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*Width);}
                    }
                }
                for(int i=0;i<boundary.Count;i++)batch.Triangle(center,boundary[i],boundary[(i+1)%boundary.Count],3);
            }
            for(int e=0;e<Edges.GetLength(0);e++)
            {
                int a=Edges[e,0],b=Edges[e,1];var direction=(plan.Points[b]-plan.Points[a]).normalized;var side=new Vector2(-direction.y,direction.x);
                var start=plan.Points[a]+direction*(a<4?Cut:0);var end=plan.Points[b]-direction*(b<4?Cut:0);
                float length=Vector2.Distance(start,end);Require(length>.1f,"junction cuts leave a valid arm");int count=Mathf.CeilToInt(length/.65f);
                for(int row=0;row<count;row++)for(int col=0;col<8;col++)
                {
                    float ta=row/(float)count,tb=(row+1f)/count,xa=-1+col*.25f,xb=xa+.25f;
                    var p=Vector2.Lerp(start,end,ta);var q=Vector2.Lerp(start,end,tb);
                    float wa=Mathf.Lerp(plan.Widths[a],plan.Widths[b],ta),wb=Mathf.Lerp(plan.Widths[a],plan.Widths[b],tb);
                    var p0=p+side*xa*wa;var p1=p+side*xb*wa;var q0=q+side*xa*wb;var q1=q+side*xb*wb;
                    batch.Triangle(p0,q0,q1,0);batch.Triangle(p0,q1,p1,0);
                }
            }
            return batch.Mesh();
        }
        [MenuItem("Magic Island/Art/Refine Grassland Desert Trail (Explicit)")]
        public static void Refine()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling,"use Edit Mode");
            var root=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);
            try
            {
                var terrain=root.GetComponent<IslandTerrain>();Require(terrain.World!=null&&terrain.World.IsValid,"original world required");
                var existing=terrain.World.transform.Find(GroupName);
                if(existing!=null)
                {Require(existing.GetComponent<MeshFilter>()?.sharedMesh!=null&&existing.GetComponent<Renderer>()?.sharedMaterial!=null,"repair incomplete authored road explicitly");Debug.Log("Grass/desert trail already refined; retaining later artist edits.");return;}
                Require(!File.Exists(MeshPath),"preserve orphan/manual road asset");
                var material=AssetDatabase.LoadAssetAtPath<Material>(LakeSnowTrailSetup.MaterialPath);
                Require(material!=null&&material.shader.name=="MagicIsland/Exploration Trail"&&!ShaderUtil.ShaderHasError(material.shader),"valid existing shared trail material required");
                var old=AssetDatabase.LoadAssetAtPath<Mesh>(LakeSnowTrailSetup.OldPath);Require(old!=null,"original road required");
                var plan=Prepare(old);var mesh=Build(terrain,plan);
                AssetDatabase.CreateAsset(mesh,MeshPath);old.SetTriangles(plan.Remainder,0,false);EditorUtility.SetDirty(old);
                var go=new GameObject(GroupName,typeof(MeshFilter),typeof(MeshRenderer));go.layer=2;go.transform.SetParent(terrain.World.transform,false);
                go.GetComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;
                PrefabUtility.SaveAsPrefabAsset(root,TerrainSetup.PrefabPath);AssetDatabase.SaveAssets();
                foreach(string path in new[]{MeshPath,LakeSnowTrailSetup.OldPath,TerrainSetup.PrefabPath})Clean(path);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                Debug.Log("Grass/desert trail refined: "+plan.RemovedQuads.Length+" old quads, "+mesh.vertexCount+" vertices; connected forks and original collision retained.");
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        static void Clean(string path)=>File.WriteAllText(path,string.Join("\n",File.ReadAllLines(path).Select(s=>s.TrimEnd()))+"\n");
        sealed class Builder
        {
            readonly IslandTerrain terrain;readonly Plan plan;
            readonly Dictionary<Vector2Int,int> cache=new Dictionary<Vector2Int,int>();
            readonly List<Vector3> vertices=new List<Vector3>();readonly List<Color> colors=new List<Color>();
            readonly List<Vector2> uv=new List<Vector2>(),extra=new List<Vector2>();readonly List<int> triangles=new List<int>();
            public Builder(IslandTerrain terrain,Plan plan){this.terrain=terrain;this.plan=plan;}
            int Vertex(Vector2 point)
            {
                var key=new Vector2Int(Mathf.RoundToInt(point.x*10000),Mathf.RoundToInt(point.y*10000));if(cache.TryGetValue(key,out int found))return found;
                point=new Vector2(key.x/10000f,key.y/10000f);
                Require(StarterIslandLayout.Contains(point.x,point.y,.1)&&StarterIslandLayout.RouteDistance(point.x,point.y)<2.4
                    &&!StarterIslandLayout.CanPlace(Mathf.RoundToInt(point.x),Mathf.RoundToInt(point.y)),"full footprint stays in original road reserve");
                Require(terrain.World.Raycast(new Ray(terrain.transform.TransformPoint(new Vector3(point.x,300,point.y)),Vector3.down),out var hit,350),"actual road floor at "+point);
                float distance=float.MaxValue,endDistance=0,width=Width;
                for(int e=0;e<Edges.GetLength(0);e++)
                {
                    int a=Edges[e,0],b=Edges[e,1];var delta=plan.Points[b]-plan.Points[a];float length=delta.magnitude;
                    float t=Mathf.Clamp01(Vector2.Dot(point-plan.Points[a],delta)/delta.sqrMagnitude);float d=Vector2.Distance(point,plan.Points[a]+delta*t);
                    if(d>=distance)continue;distance=d;width=Mathf.Lerp(plan.Widths[a],plan.Widths[b],t);
                    endDistance=Mathf.Min(plan.EndDistance[a]+t*length,plan.EndDistance[b]+(1-t)*length);
                }
                float blend=Mathf.SmoothStep(0,1,Mathf.Clamp01(endDistance/10));float desert=(float)StarterIslandLayout.DesertWeight(point.x,point.y);
                var tone=Color.Lerp(new Color(.70f,.63f,.49f),new Color(.82f,.69f,.49f),desert);
                int index=vertices.Count;cache.Add(key,index);vertices.Add(terrain.World.transform.InverseTransformPoint(hit.point)+Vector3.up*.07f);
                colors.Add(Color.Lerp(new Color(.84f,.76f,.54f),tone,blend));uv.Add(new Vector2(distance/width,0));extra.Add(new Vector2(blend,-735));return index;
            }
            public void Triangle(Vector2 a,Vector2 b,Vector2 c,int depth)
            {
                if(depth>0)
                {var ab=(a+b)*.5f;var bc=(b+c)*.5f;var ca=(c+a)*.5f;Triangle(a,ab,ca,depth-1);Triangle(ab,b,bc,depth-1);Triangle(ca,bc,c,depth-1);Triangle(ab,bc,ca,depth-1);return;}
                float area=(b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x);if(Mathf.Abs(area)<.000001f)return;
                int ia=Vertex(a),ib=Vertex(b),ic=Vertex(c);if(ia==ib||ib==ic||ia==ic)return;
                triangles.AddRange(area>0?new[]{ia,ic,ib}:new[]{ia,ib,ic});
            }
            public Mesh Mesh()
            {
                var mesh=new Mesh{name="Grass Desert Trail",indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};
                mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetUVs(0,uv);mesh.SetUVs(1,extra);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
                Require(mesh.normals.All(n=>n.y>0),"all faces point above real ground");return mesh;
            }
        }
    }
}
