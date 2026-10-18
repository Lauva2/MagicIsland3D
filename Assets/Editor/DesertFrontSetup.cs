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
    // Local, explicit art pass. Gameplay geography and every solid trunk stay unchanged.
    public static class DesertFrontSetup
    {
        public const string GroupName="Desert approach dressing";
        const float Marker=-731;
        public sealed class Edit { public Mesh Mesh;public Color[] Colors;public Vector2[] Grass,Stamp; }
        public sealed class Site { public Vector2 Point;public int Kind;public float Scale,Yaw; }
        static void Require(bool ok,string why){if(!ok)throw new InvalidOperationException("Desert approach: "+why);}
        public static float Mask(float x,float z)
        {
            float box=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-450,-395,x))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(-190,-150,x)))
                *Mathf.SmoothStep(0,1,Mathf.InverseLerp(300,350,z))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(740,790,z)));
            float shore=Mathf.SmoothStep(0,1,Mathf.InverseLerp(26,55,(float)StarterIslandLayout.CoastDistance(x,z)));
            float lake=Mathf.SmoothStep(0,1,Mathf.InverseLerp(8,26,(float)StarterIslandLayout.LakeDistance(x,z)));
            return box*shore*lake*(1-(float)StarterIslandLayout.SnowWeight(x,z));
        }
        public static Edit[] PlanEdits(IslandTerrain terrain)
        {
            var edits=new List<Edit>();
            foreach(var filter in terrain.World.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.name.StartsWith("Land ",StringComparison.Ordinal)||f.name.StartsWith("Landscape ",StringComparison.Ordinal)))
            {
                var mesh=filter.sharedMesh;var vertices=mesh.vertices;
                if(!vertices.Any(p=>Mask(p.x,p.z)>0&&(float)StarterIslandLayout.DesertWeight(p.x,p.z)>.001f))continue;
                var stamp=mesh.uv3;
                if(stamp.Length==vertices.Length&&stamp.All(s=>s.y==Marker))continue; // Preserve later artist edits.
                Require(stamp.Length==0,"preserve custom UV2 on "+mesh.name);
                bool ground=filter.name.StartsWith("Land ",StringComparison.Ordinal);
                var colors=mesh.colors;var grass=mesh.uv;
                Require(colors.Length==vertices.Length&&(!ground||grass.Length==vertices.Length),"matching authored colours/weights: "+mesh.name);
                stamp=new Vector2[vertices.Length];
                for(int i=0;i<vertices.Length;i++)
                {
                    var p=vertices[i];float mask=Mask(p.x,p.z),desert=(float)StarterIslandLayout.DesertWeight(p.x,p.z);
                    stamp[i]=new Vector2(ground?grass[i].x:-1,Marker);
                    if(mask==0||desert<=.001f)continue;
                    if(ground)
                    {
                        var green=new Color(.60f,.73f,.43f);var paleSand=new Color(.91f,.77f,.51f);
                        var dryGrass=new Color(.74f,.71f,.44f);var ochre=new Color(.86f,.67f,.40f);
                        var desired=Color.Lerp(green,dryGrass,Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,.6f,desert)));
                        desired=Color.Lerp(desired,ochre,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.38f,1,desert)));
                        var old=Color.Lerp(green,paleSand,desert);var delta=(desired-old)*mask;delta.a=0;
                        // Low-contrast wind-shaped sand patches, never white repeated stripes.
                        float mottling=(Mathf.PerlinNoise(p.x*.035f+71,p.z*.055f+19)-.5f)*.028f*mask*desert;
                        colors[i]+=delta+new Color(mottling,mottling*.8f,mottling*.5f,0);
                        grass[i].x*=1-.30f*mask*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.15f,.85f,desert));
                    }
                    else
                    {
                        float green=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.02f,.16f,colors[i].g-colors[i].r));
                        float transition=4*desert*(1-desert);
                        colors[i]+=new Color(.15f,.025f,.005f,0)*(green*transition*mask);
                        float sandstone=colors[i].r>.6f&&colors[i].g<.6f&&colors[i].b<.43f?1:0;
                        colors[i]+=new Color(.065f,.065f,.035f,0)*(sandstone*mask*desert);
                    }
                }
                edits.Add(new Edit{Mesh=mesh,Colors=colors,Grass=ground?grass:null,Stamp=stamp});
            }
            return edits.ToArray();
        }
        public static Site[] PlanSites()
        {
            var sites=new List<Site>();var random=new System.Random(102618);
            for(int segment=4;segment<=5;segment++)
            {
                var a=new Vector2((float)StarterIslandLayout.Route[segment-1,0],(float)StarterIslandLayout.Route[segment-1,1]);
                var b=new Vector2((float)StarterIslandLayout.Route[segment,0],(float)StarterIslandLayout.Route[segment,1]);
                var direction=(b-a).normalized;var side=new Vector2(-direction.y,direction.x);
                for(float distance=5;distance<Vector2.Distance(a,b)-3;distance+=12+(float)random.NextDouble()*9)
                {
                    int sign=random.Next(2)==0?-1:1;var p=a+direction*distance+side*sign*(3.9f+(float)random.NextDouble()*.65f);
                    float desert=(float)StarterIslandLayout.DesertWeight(p.x,p.y);
                    int kind=desert>.64f&&random.NextDouble()<.38?2:random.NextDouble()<.42?1:0;
                    float scale=kind==0?1:kind==1?1.15f+(float)random.NextDouble()*.5f:1.35f+(float)random.NextDouble()*.7f;
                    if(Mask(p.x,p.y)<.15f||!ClearFootprint(p,.95f)||sites.Any(s=>Vector2.Distance(s.Point,p)<5))continue;
                    sites.Add(new Site{Point=p,Kind=kind,Scale=scale,Yaw=(float)random.NextDouble()*360});
                }
            }
            return sites.ToArray();
        }
        public static bool ClearFootprint(Vector2 p,float radius)
        {
            if(!StarterIslandLayout.Contains(p.x,p.y,radius)||StarterIslandLayout.RouteDistance(p.x,p.y)<3.1)return false;
            foreach(float x in new[]{p.x-radius,p.x,p.x+radius})foreach(float z in new[]{p.y-radius,p.y,p.y+radius})
                if(StarterIslandLayout.RouteDistance(x,z)<2.5||StarterIslandLayout.RouteDistance(x,z)>5.7
                    ||StarterIslandLayout.CanPlace(Mathf.RoundToInt(x),Mathf.RoundToInt(z)))return false;
            return true;
        }
        [MenuItem("Magic Island/Art/Refine Grassland Desert Approach (Explicit)")]
        public static void Rebuild()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling,"use Edit Mode");
            var root=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);
            try
            {
                var terrain=root.GetComponent<IslandTerrain>();Require(terrain.World!=null&&terrain.World.IsValid,"missing authored mainland");
                var edits=PlanEdits(terrain);var oldGroup=terrain.World.Scenery.Find(GroupName);
                var cactus=AssetDatabase.LoadAssetAtPath<Mesh>(IslandBiomeSetup.Folder+"/Cactus.asset");
                var rock=AssetDatabase.LoadAssetAtPath<Mesh>(IslandBiomeSetup.Folder+"/Sandstone.asset");
                var material=AssetDatabase.LoadAssetAtPath<Material>(IslandBiomeSetup.MaterialFolder+"/Biome Details.mat");
                Require(cactus!=null&&rock!=null&&material!=null,"missing shared authored desert templates");
                var sites=oldGroup==null?PlanSites():Array.Empty<Site>();
                Require(oldGroup!=null||sites.Length>=8,"cannot fit a complete collider-free roadside plan");
                var batches=new SortedDictionary<string,Batch>(StringComparer.Ordinal);
                foreach(var site in sites)
                {
                    var world=terrain.transform.TransformPoint(new Vector3(site.Point.x,300,site.Point.y));
                    Require(terrain.World.Raycast(new Ray(world,Vector3.down),out var hit,350),"missing actual baked floor at "+site.Point);
                    string name="Desert verge "+Mathf.FloorToInt(site.Point.x/128)+" "+Mathf.FloorToInt(site.Point.y/128);
                    if(!batches.TryGetValue(name,out var batch)){batch=new Batch();batches.Add(name,batch);}
                    var point=terrain.World.Scenery.InverseTransformPoint(hit.point+Vector3.up*.02f);
                    float d=(float)StarterIslandLayout.DesertWeight(site.Point.x,site.Point.y);
                    if(site.Kind==0)batch.Grass(point,site.Yaw,Color.Lerp(new Color(.60f,.69f,.40f),new Color(.78f,.67f,.40f),d));
                    else batch.Append(site.Kind==1?rock:cactus,Matrix4x4.TRS(point,Quaternion.Euler(0,site.Yaw,0),Vector3.one*site.Scale),site.Kind==1?new Color(1.06f,1.06f,1,1):Color.white);
                }
                foreach(string name in batches.Keys)Require(!File.Exists(StarterIslandWorldSetup.Folder+"/"+name+".asset"),"preserve orphaned/manual batch: "+name);
                // No writes until all artist-channel guards, floor checks and plans pass.
                foreach(var edit in edits)
                {edit.Mesh.colors=edit.Colors;if(edit.Grass!=null)edit.Mesh.uv=edit.Grass;edit.Mesh.uv3=edit.Stamp;EditorUtility.SetDirty(edit.Mesh);}
                if(oldGroup==null)
                {
                    var group=new GameObject(GroupName).transform;group.gameObject.layer=2;group.SetParent(terrain.World.Scenery,false);
                    foreach(var entry in batches)
                    {
                        string path=StarterIslandWorldSetup.Folder+"/"+entry.Key+".asset";
                        var mesh=entry.Value.Mesh();mesh.name=entry.Key;AssetDatabase.CreateAsset(mesh,path);
                        var go=new GameObject(entry.Key,typeof(MeshFilter),typeof(MeshRenderer));go.layer=2;go.transform.SetParent(group,false);
                        go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=material;
                    }
                    PrefabUtility.SaveAsPrefabAsset(root,TerrainSetup.PrefabPath);Clean(TerrainSetup.PrefabPath);
                }
                AssetDatabase.SaveAssets();
                foreach(string path in edits.Select(e=>AssetDatabase.GetAssetPath(e.Mesh)).Concat(batches.Keys.Select(n=>StarterIslandWorldSetup.Folder+"/"+n+".asset")))Clean(path);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                Debug.Log("Desert approach refined: "+edits.Length+" original meshes recoloured, "+sites.Length+" roadside clusters in "+batches.Count+" shared-material batches; no geometry or collider changes.");
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        static void Clean(string path)=>File.WriteAllText(path,string.Join("\n",File.ReadAllLines(path).Select(s=>s.TrimEnd()))+"\n");
        sealed class Batch
        {
            readonly List<Vector3> vertices=new List<Vector3>(),normals=new List<Vector3>();
            readonly List<Color> colors=new List<Color>();readonly List<int> triangles=new List<int>();
            public void Append(Mesh mesh,Matrix4x4 matrix,Color tint)
            {
                int offset=vertices.Count;var source=mesh.vertices;var sourceNormals=mesh.normals;var sourceColors=mesh.colors;var normalMatrix=matrix.inverse.transpose;
                for(int i=0;i<source.Length;i++){vertices.Add(matrix.MultiplyPoint3x4(source[i]));normals.Add(normalMatrix.MultiplyVector(sourceNormals[i]).normalized);colors.Add(sourceColors[i]*tint);}
                triangles.AddRange(mesh.triangles.Select(i=>i+offset));
            }
            void Tri(Vector3 a,Vector3 b,Vector3 c,Color color)
            {int start=vertices.Count;var normal=Vector3.Cross(b-a,c-a).normalized;vertices.AddRange(new[]{a,b,c});normals.AddRange(new[]{normal,normal,normal});colors.AddRange(new[]{color,color,color});triangles.AddRange(new[]{start,start+1,start+2});}
            public void Grass(Vector3 p,float yaw,Color color)
            {
                for(int i=0;i<7;i++)
                {
                    float angle=(yaw+i*137.5f)*Mathf.Deg2Rad;var offset=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                    var root=p+offset*(i%3)*.12f;var width=new Vector3(-offset.z,0,offset.x)*.055f;var tip=root+Vector3.up*(.35f+(i%3)*.12f)+offset*.14f;
                    Tri(root-width,tip,root+width,color);Tri(root+width,tip,root-width,color);
                }
            }
            public Mesh Mesh()
            {var mesh=new Mesh{indexFormat=vertices.Count>65535?UnityEngine.Rendering.IndexFormat.UInt32:UnityEngine.Rendering.IndexFormat.UInt16};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();return mesh;}
        }
    }
}
