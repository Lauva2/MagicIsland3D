using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MagicIsland.Interaction;
using MagicIsland.TerrainSystem;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    // Explicit editor bake. Original solid trunks, floor geometry and all gameplay coordinates are retained.
    public static class ForestEdgeSetup
    {
        public const string GroupName="Lakeside forest dressing";
        public const float Marker=-736;
        public sealed class Edit
        {
            public Mesh Mesh;public Vector3[] Vertices,Normals;public Color[] Colors;public Vector2[] Stamp;public bool Ground;
            public bool[] Changed;public int Crowns;
        }
        public sealed class Site { public Vector2 Point;public int Kind;public float Phase; }
        static void Require(bool ok,string why){if(!ok)throw new InvalidOperationException("Forest edge: "+why);}
        static float Smooth(float a,float b,float v)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,v));
        public static float Mask(float x,float z)
        {
            float box=Smooth(345,395,x)*(1-Smooth(530,575,x))*Smooth(525,560,z)*(1-Smooth(700,730,z));
            return box*Smooth(38,53,Vector2.Distance(new Vector2(x,z),new Vector2(480,650)))
                *Smooth(5,18,(float)StarterIslandLayout.LakeDistance(x,z));
        }
        static bool Leaf(Color c)=>c.g-c.r>.065f&&c.g-c.b>.05f;
        public static Edit[] PlanEdits(IslandTerrain terrain)
        {
            var edits=new List<Edit>();
            foreach(var filter in terrain.World.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.name.StartsWith("Land ",StringComparison.Ordinal)||f.name.StartsWith("Landscape ",StringComparison.Ordinal)))
            {
                var mesh=filter.sharedMesh;var source=mesh.vertices;
                if(!source.Any(p=>Mask(p.x,p.z)>0))continue;
                var stamp=new List<Vector2>();mesh.GetUVs(4,stamp);
                if(stamp.Count==source.Length&&stamp.All(s=>s.y==Marker))continue;
                Require(stamp.Count==0,"preserve custom UV4 on "+mesh.name);
                var vertices=(Vector3[])source.Clone();var colors=mesh.colors;var normals=mesh.normals;var changed=new bool[source.Length];
                bool ground=filter.name.StartsWith("Land ",StringComparison.Ordinal);int crowns=0;
                Require(colors.Length==source.Length&&normals.Length==source.Length,"missing authored channels: "+mesh.name);
                if(ground)
                {
                    for(int i=0;i<source.Length;i++)
                    {
                        var p=source[i];float mask=Mask(p.x,p.z);if(mask==0)continue;
                        float fern=Mathf.PerlinNoise(p.x*.031f+15,p.z*.027f+47);
                        float verge=1-Smooth(4,17,(float)StarterIslandLayout.RouteDistance(p.x,p.z));
                        var target=Color.Lerp(new Color(.40f,.58f,.40f),new Color(.53f,.66f,.42f),fern*.65f+verge*.35f);
                        float alpha=colors[i].a;colors[i]=Color.Lerp(colors[i],target,mask*.60f);colors[i].a=alpha;changed[i]=true;
                    }
                }
                else
                {
                    // The authored balls each have 7 rings x 10 vertices. Never infer a trunk from proximity alone.
                    for(int first=0;first+70<=source.Length;first++)
                    {
                        if(!Leaf(colors[first]))continue;
                        if(!Enumerable.Range(first,10).All(i=>Vector3.Distance(source[i],source[first])<.001f)
                            ||!Enumerable.Range(first+60,10).All(i=>Vector3.Distance(source[i],source[first+60])<.001f))continue;
                        var center=(source[first]+source[first+60])*.5f;float radius=(source[first+30]-center).magnitude;
                        float mask=Mask(center.x,center.z);
                        if(mask>0)
                        {
                            var color=colors[first];
                            Require(radius>.1f&&Enumerable.Range(first,70).All(i=>colors[i]==color),"custom crown topology/paint: "+mesh.name);
                            crowns++;float phase=Mathf.Repeat(Mathf.Sin(center.x*12.9898f+center.z*78.233f)*43758.5453f,1);
                            float angle=phase*Mathf.PI*2;var rotate=Quaternion.Euler(0,phase*360*mask,0);
                            var scale=new Vector3(Mathf.Lerp(.77f,.97f,phase),Mathf.Lerp(.85f,1.18f,phase),Mathf.Lerp(.96f,.76f,phase));
                            var palette=Color.Lerp(new Color(.29f,.47f,.34f),new Color(.43f,.60f,.36f),phase);
                            float halfHeight=(source[first].y-source[first+60].y)*.5f;
                            for(int i=first;i<first+70;i++)
                            {
                                var delta=source[i]-center;float cap=Mathf.InverseLerp(-halfHeight,halfHeight,delta.y);
                                // Blend rotation angle and scale, rather than lerping opposed positions through the centre.
                                var shaped=rotate*Vector3.Scale(delta,Vector3.Lerp(Vector3.one,scale,mask));
                                // The 0.97 radial ceiling and small lean retain the old canopy's horizontal envelope.
                                shaped+=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*radius*.025f*cap*mask;
                                vertices[i]=center+shaped;
                                var tint=palette*Mathf.Lerp(.80f,1.13f,Smooth(.18f,.95f,cap));tint.a=color.a;
                                colors[i]=Color.Lerp(color,tint,mask*.90f);changed[i]=true;
                            }
                        }
                        first+=69;
                    }
                    if(crowns>0)
                    {
                        var temporary=Object.Instantiate(mesh);
                        try
                        {
                            temporary.vertices=vertices;temporary.RecalculateNormals();var computed=temporary.normals;
                            for(int i=0;i<source.Length;i++)if(changed[i])normals[i]=computed[i];
                        }
                        finally{Object.DestroyImmediate(temporary);}
                    }
                }
                if(!changed.Any(c=>c))continue;
                edits.Add(new Edit{Mesh=mesh,Vertices=vertices,Normals=normals,Colors=colors,Ground=ground,Changed=changed,Crowns=crowns,
                    Stamp=Enumerable.Range(0,source.Length).Select(i=>new Vector2(changed[i]?1:0,Marker)).ToArray()});
            }
            return edits.ToArray();
        }
        public static bool ClearFootprint(Vector2 p,float radius)
        {
            if(Mask(p.x,p.y)<.15f||!StarterIslandLayout.Contains(p.x,p.y,radius+1))return false;
            for(int ix=0;ix<=10;ix++)for(int iz=0;iz<=10;iz++)
            {
                float x=Mathf.Lerp(p.x-radius,p.x+radius,ix/10f),z=Mathf.Lerp(p.y-radius,p.y+radius,iz/10f);
                if(StarterIslandLayout.RouteDistance(x,z)<3||StarterIslandLayout.CanPlace(Mathf.RoundToInt(x),Mathf.RoundToInt(z))
                    ||StarterIslandLayout.LakeDistance(x,z)<1||Vector2.Distance(new Vector2(x,z),new Vector2(480,650))<38)return false;
            }
            return true;
        }
        public static Site[] PlanSites()
        {
            var sites=new List<Site>();var random=new System.Random(103771);
            for(int segment=14;segment<=16;segment++)
            {
                var a=new Vector2((float)StarterIslandLayout.Route[segment-1,0],(float)StarterIslandLayout.Route[segment-1,1]);
                var b=new Vector2((float)StarterIslandLayout.Route[segment,0],(float)StarterIslandLayout.Route[segment,1]);
                var side=new Vector2(-(b-a).y,(b-a).x).normalized;float length=Vector2.Distance(a,b);
                for(float d=7;d<length-3;d+=8+(float)random.NextDouble()*7)foreach(int sign in new[]{-1,1})
                {
                    var p=Vector2.Lerp(a,b,d/length)+side*sign*4.5f;float phase=(float)random.NextDouble();
                    if(!ClearFootprint(p,1.05f)||sites.Any(s=>Vector2.Distance(s.Point,p)<7))continue;
                    sites.Add(new Site{Point=p,Kind=random.Next(3),Phase=phase});
                }
            }
            return sites.ToArray();
        }
        [MenuItem("Magic Island/Art/Refine Lakeside Forest Edge (Explicit)")]
        public static void Rebuild()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling,"use Edit Mode");
            var root=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);
            try
            {
                var terrain=root.GetComponent<IslandTerrain>();Require(terrain.World!=null&&terrain.World.IsValid,"missing authored world");
                var edits=PlanEdits(terrain);var old=terrain.World.Scenery.Find(GroupName);
                var material=AssetDatabase.LoadAssetAtPath<Material>(IslandBiomeSetup.MaterialFolder+"/Biome Details.mat");Require(material!=null,"shared scenery material");
                var sites=old==null?PlanSites():Array.Empty<Site>();Require(old!=null||sites.Length>=8,"incomplete safe roadside plan");
                var batches=new SortedDictionary<string,Geometry>(StringComparer.Ordinal);
                foreach(var site in sites)
                {
                    Require(terrain.World.Raycast(new Ray(terrain.transform.TransformPoint(new Vector3(site.Point.x,300,site.Point.y)),Vector3.down),out var hit,350),"missing actual floor");
                    var point=terrain.World.Scenery.InverseTransformPoint(hit.point);
                    string key="Forest verge "+Mathf.FloorToInt(point.x/128)+" "+Mathf.FloorToInt(point.z/128);
                    if(!batches.TryGetValue(key,out var geometry)){geometry=new Geometry();batches.Add(key,geometry);}
                    geometry.Cluster(point,site.Kind,site.Phase);
                }
                foreach(string key in batches.Keys)Require(!File.Exists(StarterIslandWorldSetup.Folder+"/"+key+".asset"),"preserve orphan/manual batch: "+key);
                foreach(var edit in edits)
                {
                    edit.Mesh.colors=edit.Colors;edit.Mesh.SetUVs(4,edit.Stamp);
                    if(!edit.Ground){edit.Mesh.vertices=edit.Vertices;edit.Mesh.normals=edit.Normals;edit.Mesh.RecalculateBounds();}
                    EditorUtility.SetDirty(edit.Mesh);
                }
                if(old==null)
                {
                    var group=new GameObject(GroupName).transform;group.gameObject.layer=2;group.SetParent(terrain.World.Scenery,false);
                    foreach(var entry in batches)
                    {
                        var mesh=entry.Value.Mesh();mesh.name=entry.Key;AssetDatabase.CreateAsset(mesh,StarterIslandWorldSetup.Folder+"/"+entry.Key+".asset");
                        var go=new GameObject(entry.Key,typeof(MeshFilter),typeof(MeshRenderer));go.layer=2;go.transform.SetParent(group,false);
                        go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=material;
                    }
                    PrefabUtility.SaveAsPrefabAsset(root,TerrainSetup.PrefabPath);Clean(TerrainSetup.PrefabPath);
                }
                AssetDatabase.SaveAssets();
                foreach(string path in edits.Select(e=>AssetDatabase.GetAssetPath(e.Mesh)).Concat(batches.Keys.Select(n=>StarterIslandWorldSetup.Folder+"/"+n+".asset")))Clean(path);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                Debug.Log("Forest edge refined: "+edits.Length+" meshes, "+edits.Sum(e=>e.Crowns)+" crown lobes, "+sites.Length+" clusters / "+batches.Count+" batches; original trunks and floor geometry retained.");
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        static void Clean(string path)=>File.WriteAllText(path,string.Join("\n",File.ReadAllLines(path).Select(s=>s.TrimEnd()))+"\n");
        sealed class Geometry
        {
            readonly List<Vector3> vertices=new List<Vector3>();readonly List<Color> colors=new List<Color>();readonly List<int> triangles=new List<int>();
            void Ball(Vector3 p,Vector3 scale,Color color)
            {
                const int n=10,rings=6;int first=vertices.Count;
                for(int j=0;j<=rings;j++)for(int i=0;i<n;i++)
                {
                    float a=j*Mathf.PI/rings,f=i*Mathf.PI*2/n;
                    vertices.Add(p+Vector3.Scale(new Vector3(Mathf.Sin(a)*Mathf.Cos(f),Mathf.Cos(a),Mathf.Sin(a)*Mathf.Sin(f)),scale));
                    var tint=color*Mathf.Lerp(.82f,1.08f,1-(float)j/rings);tint.a=1;colors.Add(tint);
                }
                for(int j=0;j<rings;j++)for(int i=0;i<n;i++)
                {int a=first+j*n+i,b=first+j*n+(i+1)%n;triangles.AddRange(new[]{a,b,a+n,a+n,b,b+n});}
            }
            public void Cluster(Vector3 p,int kind,float phase)
            {
                if(kind==1)
                {
                    Ball(p+Vector3.up*.24f,new Vector3(.85f,.43f,.62f),new Color(.52f,.59f,.52f));
                    Ball(p+new Vector3(-.12f,.58f,.05f),new Vector3(.58f,.12f,.43f),new Color(.37f,.52f,.34f));
                    Ball(p+new Vector3(.58f,.15f,.30f),new Vector3(.30f,.25f,.27f),new Color(.61f,.65f,.55f));
                }
                else if(kind==0)
                {
                    for(int i=0;i<5;i++)
                    {
                        float angle=(phase+i*.2f)*Mathf.PI*2;
                        Ball(p+new Vector3(Mathf.Cos(angle)*.40f,.42f+(i%2)*.17f,Mathf.Sin(angle)*.40f),new Vector3(.49f,.48f,.43f),
                            Color.Lerp(new Color(.31f,.49f,.32f),new Color(.50f,.64f,.37f),i*.18f));
                    }
                }
                else
                {
                    for(int i=0;i<7;i++)
                    {
                        float angle=(phase+i/7f)*Mathf.PI*2;var dir=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));var side=new Vector3(-dir.z,0,dir.x);
                        for(int j=0;j<4;j++)
                        {
                            float t=(j+1)/5f;var mid=p+dir*(t*.90f)+Vector3.up*(Mathf.Sin(t*Mathf.PI)*.52f+.05f);float w=(1-t)*.22f;
                            int a=vertices.Count;vertices.Add(mid-dir*.13f);vertices.Add(mid+side*w);vertices.Add(mid+dir*.19f);vertices.Add(mid-side*w);
                            var c=Color.Lerp(new Color(.32f,.52f,.36f),new Color(.57f,.67f,.40f),t);for(int k=0;k<4;k++)colors.Add(c);
                            triangles.AddRange(new[]{a,a+1,a+2,a,a+2,a+3});
                        }
                    }
                }
            }
            public Mesh Mesh()
            {
                var mesh=new Mesh{indexFormat=vertices.Count>65535?UnityEngine.Rendering.IndexFormat.UInt32:UnityEngine.Rendering.IndexFormat.UInt16};
                mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
            }
        }
    }
}
