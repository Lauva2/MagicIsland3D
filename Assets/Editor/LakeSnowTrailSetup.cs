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
    // Replace only the visual ribbon on the northeast approach. All physical ground is retained.
    public static class LakeSnowTrailSetup
    {
        public const string GroupName="Lake snow trail";
        public const string OldPath=StarterIslandWorldSetup.Folder+"/Exploration paths.asset";
        public const string MeshPath=StarterIslandWorldSetup.Folder+"/Lake Snow Trail.asset";
        public const string MaterialPath="Assets/Art/Materials/Terrain/Exploration Trail.mat";
        public sealed class Plan { public int[] Remainder,RemovedQuads;public Vector2[] Points;public float StartWidth,EndWidth; }
        static void Require(bool ok,string why){if(!ok)throw new InvalidOperationException("Lake snow trail: "+why);}
        static Vector2 XZ(Vector3 p)=>new Vector2(p.x,p.z);
        static Vector2 Point(int i)=>new Vector2((float)StarterIslandLayout.Route[i,0],(float)StarterIslandLayout.Route[i,1]);
        public static Plan Prepare(Mesh mesh)
        {
            var v=mesh.vertices;var indices=mesh.triangles;
            Require(v.Length%4==0&&indices.Length==v.Length/4*6,"preserve unsupported/manual path topology");
            var removed=new List<int>();var remainder=new List<int>();var counts=new int[3];int first=-1,last=-1;
            for(int q=0;q<v.Length/4;q++)
            {
                int a=q*4;Require(indices.Skip(q*6).Take(6).SequenceEqual(new[]{a,a+1,a+2,a,a+2,a+3}),"preserve custom triangulation");
                var center=(XZ(v[a])+XZ(v[a+1])+XZ(v[a+2])+XZ(v[a+3]))*.25f;
                bool selected=false;
                for(int segment=10;segment<=14;segment++)
                {
                    var p=Point(segment-1);var b=Point(segment);
                    if(StarterIslandLayout.SegmentDistance(center.x,center.y,p.x,p.y,b.x,b.y)>.01)continue;
                    if(segment>=11&&segment<=13){selected=true;counts[segment-11]++;}
                    else if(segment==10&&Vector2.Distance(center,b)<8){selected=true;if(first<0)first=q;}
                    else if(segment==14&&Vector2.Distance(center,p)<8){selected=true;last=q;}
                }
                if(selected)removed.Add(q);else remainder.AddRange(indices.Skip(q*6).Take(6));
            }
            for(int i=0;i<3;i++)Require(counts[i]==Mathf.CeilToInt(Vector2.Distance(Point(i+10),Point(i+11))),"complete original route span "+(i+11));
            Require(first>=0&&last>=0,"intact original end connections");
            int start=first*4,end=last*4;
            return new Plan{Remainder=remainder.ToArray(),RemovedQuads=removed.ToArray(),
                Points=new[]{(XZ(v[start])+XZ(v[start+3]))*.5f,Point(10),Point(11),Point(12),Point(13),(XZ(v[end+1])+XZ(v[end+2]))*.5f},
                StartWidth=Vector2.Distance(XZ(v[start]),XZ(v[start+3]))*.5f,EndWidth=Vector2.Distance(XZ(v[end+1]),XZ(v[end+2]))*.5f};
        }
        public static float Snow(Vector2 p)
        {float mask=SnowFrontSetup.Mask(p.x,p.y);return Mathf.Lerp((float)StarterIslandLayout.SnowWeight(p.x,p.y),SnowFrontSetup.Snow(p.x,p.y),mask);}
        // Called only by the explicit whole-island builder, on its newly generated temporary mesh.
        // Retain the authored replacement and prevent regenerated legacy faces from showing underneath it.
        public static void PreserveOnWorldRebuild(Transform world,Mesh freshPaths)
        {
            var local=world.Find(GroupName);if(local==null)return;
            Require(local.GetComponent<MeshFilter>()?.sharedMesh!=null,"preserve incomplete local trail for manual repair");
            freshPaths.SetTriangles(Prepare(freshPaths).Remainder,0,false);
        }
        public static Mesh Build(IslandTerrain terrain,Plan plan)
        {
            var points=plan.Points;var sides=new Vector2[points.Length];var cumulative=new float[points.Length];
            for(int i=1;i<points.Length;i++)cumulative[i]=cumulative[i-1]+Vector2.Distance(points[i-1],points[i]);
            for(int i=0;i<points.Length;i++)
            {
                Vector2 before=(points[Mathf.Min(i+1,points.Length-1)]-points[Mathf.Max(i-1,0)]).normalized;
                Vector2 after=before;
                if(i>0)before=(points[i]-points[i-1]).normalized;
                if(i<points.Length-1)after=(points[i+1]-points[i]).normalized;
                var leftBefore=new Vector2(-before.y,before.x);var leftAfter=new Vector2(-after.y,after.x);
                var miter=(leftBefore+leftAfter).normalized;
                float width=i==0?plan.StartWidth:i==points.Length-1?plan.EndWidth:1.95f;
                sides[i]=miter*Mathf.Min(2.28f,width/Mathf.Max(.65f,Vector2.Dot(miter,leftAfter)));
            }
            var vertices=new List<Vector3>();var colors=new List<Color>();var uv=new List<Vector2>();var extra=new List<Vector2>();var triangles=new List<int>();
            float[] across={-1,-.65f,0,.65f,1};int rows=0;
            for(int segment=1;segment<points.Length;segment++)
            {
                float length=Vector2.Distance(points[segment-1],points[segment]);int steps=Mathf.CeilToInt(length/.65f);
                for(int j=segment==1?0:1;j<=steps;j++)
                {
                    float t=j/(float)steps;var center=Vector2.Lerp(points[segment-1],points[segment],t);var side=Vector2.Lerp(sides[segment-1],sides[segment],t);
                    float distance=cumulative[segment-1]+length*t;
                    float blend=Mathf.SmoothStep(0,1,Mathf.Clamp01(distance/10))*Mathf.SmoothStep(0,1,Mathf.Clamp01((cumulative.Last()-distance)/10));
                    float irregular=1+((Mathf.PerlinNoise(center.x*.12f+17,center.y*.12f+43)-.5f)*.08f)*blend;
                    foreach(float x in across)
                    {
                        var p=center+side*(x*irregular);
                        Require(StarterIslandLayout.Contains(p.x,p.y,.1)&&StarterIslandLayout.RouteDistance(p.x,p.y)<2.4
                            &&!StarterIslandLayout.CanPlace(Mathf.RoundToInt(p.x),Mathf.RoundToInt(p.y)),"ribbon remains in the original reserved route");
                        Require(terrain.World.Raycast(new Ray(terrain.transform.TransformPoint(new Vector3(p.x,300,p.y)),Vector3.down),out var hit,350),"actual route floor at "+p);
                        vertices.Add(terrain.World.transform.InverseTransformPoint(hit.point)+Vector3.up*.07f);
                        float snow=Snow(p);var tone=Color.Lerp(new Color(.70f,.63f,.49f),new Color(.73f,.79f,.79f),Mathf.SmoothStep(0,1,snow));
                        colors.Add(Color.Lerp(new Color(.84f,.76f,.54f),tone,blend));uv.Add(new Vector2(x,snow));extra.Add(new Vector2(blend,-734));
                    }
                    if(rows>0)for(int col=0;col<4;col++)
                    {int a=(rows-1)*5+col,b=rows*5+col;triangles.AddRange(new[]{a,b+1,b,a,a+1,b+1});}
                    rows++;
                }
            }
            var mesh=new Mesh{name="Lake Snow Trail",indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};
            mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetUVs(0,uv);mesh.SetUVs(1,extra);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            Require(mesh.normals.All(n=>n.y>0),"all ribbon faces point upward");return mesh;
        }
        [MenuItem("Magic Island/Art/Refine Lake Snow Trail (Explicit)")]
        public static void Refine()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling,"use Edit Mode");
            var root=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);
            try
            {
                var terrain=root.GetComponent<IslandTerrain>();Require(terrain.World!=null&&terrain.World.IsValid,"original world required");
                var existing=terrain.World.transform.Find(GroupName);
                if(existing!=null)
                {
                    Require(existing.GetComponent<MeshFilter>()?.sharedMesh!=null&&existing.GetComponent<Renderer>()?.sharedMaterial!=null,"repair incomplete authored trail explicitly");
                    Debug.Log("Lake snow trail already refined; retaining all later artist edits.");return;
                }
                Require(!File.Exists(MeshPath),"preserve orphan/manual trail asset");
                var old=AssetDatabase.LoadAssetAtPath<Mesh>(OldPath);Require(old!=null,"original path mesh required");
                var shader=Shader.Find("MagicIsland/Exploration Trail");Require(shader!=null&&!ShaderUtil.ShaderHasError(shader),"trail shader must compile");
                var material=AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
                Require(material==null||material.shader==shader,"preserve custom trail material");
                var plan=Prepare(old);var mesh=Build(terrain,plan); // Complete all topology and real-floor checks before writing.
                if(material==null){material=new Material(shader){name="Exploration Trail"};AssetDatabase.CreateAsset(material,MaterialPath);}
                AssetDatabase.CreateAsset(mesh,MeshPath);
                old.SetTriangles(plan.Remainder,0,false);EditorUtility.SetDirty(old);
                var go=new GameObject(GroupName,typeof(MeshFilter),typeof(MeshRenderer));go.layer=2;go.transform.SetParent(terrain.World.transform,false);
                go.GetComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;
                PrefabUtility.SaveAsPrefabAsset(root,TerrainSetup.PrefabPath);AssetDatabase.SaveAssets();
                foreach(string path in new[]{OldPath,MeshPath,MaterialPath,TerrainSetup.PrefabPath})Clean(path);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                Debug.Log("Lake snow trail refined: "+plan.RemovedQuads.Length+" old visual quads replaced by a connected "+mesh.vertexCount+"-vertex ribbon; all ground/collision and other route faces preserved.");
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        static void Clean(string path)=>File.WriteAllText(path,string.Join("\n",File.ReadAllLines(path).Select(s=>s.TrimEnd()))+"\n");
    }
}
