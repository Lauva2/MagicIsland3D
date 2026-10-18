using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MagicIsland.Interaction;
using MagicIsland.TerrainSystem;
using UnityEditor;
using UnityEngine;

namespace MagicIsland.EditorTools
{
    // Explicit, baked art only. Preserve the original terrain and solid-tree geometry.
    public static class SnowFrontSetup
    {
        public const string GroupName="Snow approach dressing";
        const float Marker=-732;
        public sealed class Edit { public Mesh Mesh;public Color[] Colors;public Vector2[] Grass,Stamp; }
        public sealed class Site { public Vector2 Point;public int Kind;public float Scale,Yaw; }
        static void Require(bool ok,string why){if(!ok)throw new InvalidOperationException("Snow approach: "+why);}
        static float Smooth(float a,float b,float v)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,v));
        public static float Mask(float x,float z)
        {
            float box=Smooth(40,105,x)*(1-Smooth(390,445,x))*Smooth(740,790,z)*(1-Smooth(1010,1060,z));
            return box*Smooth(26,55,(float)StarterIslandLayout.CoastDistance(x,z))
                *Smooth(10,30,(float)StarterIslandLayout.LakeDistance(x,z))
                *Smooth(10,26,(float)StarterIslandLayout.InletDistance(x,z));
        }
        public static float Snow(float x,float z)
        {
            float contour=z+28*Mathf.Sin(x*.009f)+12*Mathf.Sin(x*.023f);
            float patches=(Mathf.PerlinNoise(x*.038f+23,z*.029f+61)-.5f)*36;
            return Smooth(785,945,contour+patches);
        }
        public static Edit[] PlanEdits(IslandTerrain terrain)
        {
            var edits=new List<Edit>();
            foreach(var filter in terrain.World.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.name.StartsWith("Land ",StringComparison.Ordinal)||f.name.StartsWith("Landscape ",StringComparison.Ordinal)))
            {
                var mesh=filter.sharedMesh;var vertices=mesh.vertices;
                if(!vertices.Any(p=>Mask(p.x,p.z)>0&&Snow(p.x,p.z)>.001f))continue;
                // UV3 is independent of the west-desert pass's UV2 marker.
                var stamp=mesh.uv4;
                if(stamp.Length==vertices.Length&&stamp.All(s=>s.y==Marker))continue;
                Require(stamp.Length==0,"preserve custom UV3 on "+mesh.name);
                bool ground=filter.name.StartsWith("Land ",StringComparison.Ordinal);
                var colors=mesh.colors;var grass=mesh.uv;var normals=mesh.normals;
                Require(colors.Length==vertices.Length&&normals.Length==vertices.Length&&(!ground||grass.Length==vertices.Length),"authored channels: "+mesh.name);
                stamp=new Vector2[vertices.Length];
                for(int i=0;i<vertices.Length;i++)
                {
                    var p=vertices[i];float mask=Mask(p.x,p.z),snow=Snow(p.x,p.z);
                    stamp[i]=new Vector2(ground?grass[i].x:-1,Marker);
                    if(mask==0||snow<=.001f)continue;
                    float oldSnow=(float)StarterIslandLayout.SnowWeight(p.x,p.z);
                    if(ground)
                    {
                        float desert=(float)StarterIslandLayout.DesertWeight(p.x,p.z),forest=(float)StarterIslandLayout.ForestWeight(p.x,p.z);
                        var green=Color.Lerp(new Color(.60f,.73f,.43f),new Color(.43f,.62f,.39f),forest);
                        green=Color.Lerp(green,new Color(.91f,.77f,.51f),desert);
                        var old=Color.Lerp(green,new Color(.89f,.94f,.96f),oldSnow);
                        var greyGrass=Color.Lerp(green,new Color(.58f,.68f,.59f),Smooth(.015f,.42f,snow));
                        var desired=Color.Lerp(greyGrass,new Color(.90f,.94f,.94f),Smooth(.18f,.97f,snow));
                        var delta=(desired-old)*mask;delta.a=0;colors[i]+=delta;
                        grass[i].x=Mathf.Lerp(grass[i].x,(1-desert)*(1-forest)*(1-snow)*(1-Smooth(.04f,.65f,snow)*.45f),mask);
                    }
                    else if(colors[i].g-colors[i].r>.065f&&colors[i].g-colors[i].b>.005f)
                    {
                        // Snow collects on upward-facing leaves; dark evergreen undersides keep the silhouette.
                        float cap=snow*Mathf.Lerp(.30f,1,Smooth(-.05f,.65f,normals[i].y));
                        var old=Color.Lerp(new Color(.30f,.51f,.30f),new Color(.72f,.83f,.81f),oldSnow);
                        var desired=Color.Lerp(new Color(.31f,.47f,.39f),new Color(.87f,.93f,.93f),cap);
                        var delta=(desired-old)*mask;delta.a=0;colors[i]+=delta;
                    }
                }
                edits.Add(new Edit{Mesh=mesh,Colors=colors,Grass=ground?grass:null,Stamp=stamp});
            }
            return edits.ToArray();
        }
        public static Site[] PlanSites()
        {
            var sites=new List<Site>();var random=new System.Random(102619);
            foreach(int segment in new[]{11,12,13})
            {
                var a=new Vector2((float)StarterIslandLayout.Route[segment-1,0],(float)StarterIslandLayout.Route[segment-1,1]);
                var b=new Vector2((float)StarterIslandLayout.Route[segment,0],(float)StarterIslandLayout.Route[segment,1]);
                var side=new Vector2(-(b-a).y,(b-a).x).normalized;
                for(float d=6;d<Vector2.Distance(a,b)-3;d+=11+(float)random.NextDouble()*8)
                {
                    var p=Vector2.Lerp(a,b,d/Vector2.Distance(a,b))+side*(random.Next(2)==0?-1:1)*(4.05f+(float)random.NextDouble()*.25f);
                    int kind=random.Next(3);float scale=kind==2?1.25f+(float)random.NextDouble()*.5f:kind==1?1.4f+(float)random.NextDouble()*.6f:.7f;
                    if(Mask(p.x,p.y)<.2f||!ClearFootprint(p,1.05f)||sites.Any(s=>Vector2.Distance(s.Point,p)<6))continue;
                    sites.Add(new Site{Point=p,Kind=kind,Scale=scale,Yaw=(float)random.NextDouble()*360});
                }
            }
            return sites.ToArray();
        }
        public static bool ClearFootprint(Vector2 p,float radius)
        {
            if(!StarterIslandLayout.Contains(p.x,p.y,radius))return false;
            foreach(float x in new[]{p.x-radius,p.x,p.x+radius})foreach(float z in new[]{p.y-radius,p.y,p.y+radius})
                if(StarterIslandLayout.RouteDistance(x,z)<2.5||StarterIslandLayout.RouteDistance(x,z)>5.7||StarterIslandLayout.CanPlace(Mathf.RoundToInt(x),Mathf.RoundToInt(z)))return false;
            return true;
        }
        [MenuItem("Magic Island/Art/Refine Grassland Snow Approach (Explicit)")]
        public static void Rebuild()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling,"use Edit Mode");
            var root=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);
            try
            {
                var terrain=root.GetComponent<IslandTerrain>();Require(terrain.World!=null&&terrain.World.IsValid,"missing authored mainland");
                var edits=PlanEdits(terrain);var oldGroup=terrain.World.Scenery.Find(GroupName);
                var pine=AssetDatabase.LoadAssetAtPath<Mesh>(IslandBiomeSetup.Folder+"/Snow Pine.asset");
                var rock=AssetDatabase.LoadAssetAtPath<Mesh>(IslandBiomeSetup.Folder+"/Snow Rock.asset");
                var material=AssetDatabase.LoadAssetAtPath<Material>(IslandBiomeSetup.MaterialFolder+"/Biome Details.mat");
                Require(pine!=null&&rock!=null&&material!=null,"missing shared authored snow templates");
                var sites=oldGroup==null?PlanSites():Array.Empty<Site>();
                Require(oldGroup!=null||sites.Length>=8,"cannot fit a complete roadside plan");
                var batches=new SortedDictionary<string,Batch>(StringComparer.Ordinal);
                foreach(var site in sites)
                {
                    var world=terrain.transform.TransformPoint(new Vector3(site.Point.x,300,site.Point.y));
                    Require(terrain.World.Raycast(new Ray(world,Vector3.down),out var hit,350),"missing actual baked floor at "+site.Point);
                    string name="Snow verge "+Mathf.FloorToInt(site.Point.x/128)+" "+Mathf.FloorToInt(site.Point.y/128);
                    if(!batches.TryGetValue(name,out var batch)){batch=new Batch();batches.Add(name,batch);}
                    var point=terrain.World.Scenery.InverseTransformPoint(hit.point);
                    // Tiny evergreen scrub uses the same pine template; no new solid trunks.
                    batch.Append(site.Kind==1?rock:pine,Matrix4x4.TRS(point,Quaternion.Euler(0,site.Yaw,0),Vector3.one*site.Scale),Snow(site.Point.x,site.Point.y));
                }
                foreach(string name in batches.Keys)Require(!File.Exists(StarterIslandWorldSetup.Folder+"/"+name+".asset"),"preserve orphaned/manual batch: "+name);
                foreach(var edit in edits)
                {edit.Mesh.colors=edit.Colors;if(edit.Grass!=null)edit.Mesh.uv=edit.Grass;edit.Mesh.uv4=edit.Stamp;EditorUtility.SetDirty(edit.Mesh);}
                if(oldGroup==null)
                {
                    var group=new GameObject(GroupName).transform;group.gameObject.layer=2;group.SetParent(terrain.World.Scenery,false);
                    foreach(var entry in batches)
                    {
                        string path=StarterIslandWorldSetup.Folder+"/"+entry.Key+".asset";var mesh=entry.Value.Mesh();mesh.name=entry.Key;AssetDatabase.CreateAsset(mesh,path);
                        var go=new GameObject(entry.Key,typeof(MeshFilter),typeof(MeshRenderer));go.layer=2;go.transform.SetParent(group,false);
                        go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=material;
                    }
                    PrefabUtility.SaveAsPrefabAsset(root,TerrainSetup.PrefabPath);Clean(TerrainSetup.PrefabPath);
                }
                AssetDatabase.SaveAssets();
                foreach(string path in edits.Select(e=>AssetDatabase.GetAssetPath(e.Mesh)).Concat(batches.Keys.Select(n=>StarterIslandWorldSetup.Folder+"/"+n+".asset")))Clean(path);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                Debug.Log("Snow approach refined: "+edits.Length+" original meshes recoloured, "+sites.Length+" roadside clusters in "+batches.Count+" shared-material batches; no geometry or collider changes.");
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        static void Clean(string path)=>File.WriteAllText(path,string.Join("\n",File.ReadAllLines(path).Select(s=>s.TrimEnd()))+"\n");
        sealed class Batch
        {
            readonly List<Vector3> vertices=new List<Vector3>(),normals=new List<Vector3>();
            readonly List<Color> colors=new List<Color>();readonly List<int> triangles=new List<int>();
            public void Append(Mesh mesh,Matrix4x4 matrix,float snow)
            {
                int offset=vertices.Count;var source=mesh.vertices;var sourceNormals=mesh.normals;var sourceColors=mesh.colors;var normalMatrix=matrix.inverse.transpose;
                for(int i=0;i<source.Length;i++)
                {
                    var color=sourceColors[i];
                    if(color.r>.85f&&color.b>.85f)color=Color.Lerp(new Color(.44f,.57f,.50f),color,Smooth(.08f,.85f,snow));
                    vertices.Add(matrix.MultiplyPoint3x4(source[i]));normals.Add(normalMatrix.MultiplyVector(sourceNormals[i]).normalized);colors.Add(color);
                }
                triangles.AddRange(mesh.triangles.Select(i=>i+offset));
            }
            public Mesh Mesh()
            {var mesh=new Mesh{indexFormat=vertices.Count>65535?UnityEngine.Rendering.IndexFormat.UInt32:UnityEngine.Rendering.IndexFormat.UInt16};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();return mesh;}
        }
    }
}
