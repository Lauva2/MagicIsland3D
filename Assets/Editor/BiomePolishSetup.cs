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
    // Editor-only, ordered art pass. Existing source meshes own their GUIDs; no runtime generation.
    public static class BiomePolishSetup
    {
        public static readonly string[] Names={"Beach","Grassland","Desert","Forest","Snow"};
        public const float Marker=-740;
        public sealed class Edit
        {
            public Mesh Mesh;public Vector3[] Vertices,Normals;public Color[] Colors;public Vector2[] Stamp;public bool[] Changed;public bool Ground;
        }
        public sealed class Site { public Vector2 Point;public int Kind;public float Phase; }
        public static string Group(int stage)=>Names[stage]+" biome dressing";
        static void Require(bool ok,string why){if(!ok)throw new InvalidOperationException("Biome polish: "+why);}
        static float Smooth(float a,float b,float v)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,v));
        static float Box(float x,float z,float x0,float x1,float z0,float z1,float edge=25)
            =>Smooth(x0,x0+edge,x)*(1-Smooth(x1-edge,x1,x))*Smooth(z0,z0+edge,z)*(1-Smooth(z1-edge,z1,z));
        public static float Mask(int stage,float x,float z)
        {
            float box=stage==1?Box(x,z,-165,145,265,565):stage==2?Box(x,z,-580,-315,640,850):stage==3?Box(x,z,450,650,742,850,18):stage==4?Box(x,z,-310,35,900,1100):0;
            if(box==0)return 0;
            // Earlier artist passes are kept exactly, including entire crown components crossing their boundary.
            if(DesertFrontSetup.Mask(x,z)>0||SnowFrontSetup.Mask(x,z)>0||ForestEdgeSetup.Mask(x,z)>0)return 0;
            float landmark=1;
            foreach(var p in new[]{new Vector2(-420,590),new Vector2(-8,700),new Vector2(480,650),new Vector2(-40,1000)})
                landmark*=Smooth(45,65,Vector2.Distance(new Vector2(x,z),p));
            return box*landmark*Smooth(25,45,(float)StarterIslandLayout.CoastDistance(x,z))
                *Smooth(14,30,(float)StarterIslandLayout.LakeDistance(x,z))*Smooth(12,24,(float)StarterIslandLayout.InletDistance(x,z));
        }
        static Vector2[] Stamp(Mesh mesh,int stage,out bool done)
        {
            var values=new List<Vector2>();mesh.GetUVs(5,values);int bits=0;
            if(values.Count>0)
            {
                bits=(int)values[0].x;
                Require(values.Count==mesh.vertexCount&&bits>=1&&bits<=31&&values.All(v=>v.x==bits&&v.y==Marker),"preserve custom UV5: "+mesh.name);
            }
            done=(bits&(1<<stage))!=0;
            return Enumerable.Repeat(new Vector2(bits|(1<<stage),Marker),mesh.vertexCount).ToArray();
        }
        // Connected components keep individual authored leaf/rock lobes separate from their brown trunks.
        public static int[][] Components(Mesh mesh)
        {
            var parent=Enumerable.Range(0,mesh.vertexCount).ToArray();int Find(int i){while(parent[i]!=i){parent[i]=parent[parent[i]];i=parent[i];}return i;}
            var triangles=mesh.triangles;
            for(int i=0;i<triangles.Length;i+=3){int root=Find(triangles[i]);parent[Find(triangles[i+1])]=root;parent[Find(triangles[i+2])]=root;}
            return Enumerable.Range(0,parent.Length).GroupBy(Find).Select(g=>g.ToArray()).OrderBy(c=>c[0]).ToArray();
        }
        public static Edit[] Plan(IslandTerrain terrain,int stage)
        {
            Require(stage>=0&&stage<Names.Length,"stage");var edits=new List<Edit>();
            if(stage==0)
            {
                foreach(string name in new[]{"Palm Fronds","Shrub Cluster"})
                {
                    var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(CoastArtSetup.MeshFolder+"/"+name+".asset");Require(mesh!=null,"missing coastal model");
                    var stamp=Stamp(mesh,stage,out bool done);if(done)continue;
                    var source=mesh.vertices;var v=(Vector3[])source.Clone();var changed=Enumerable.Repeat(true,v.Length).ToArray();
                    if(name=="Palm Fronds")
                    {
                        Require(v.Length==8*78,"custom palm ribbon topology");
                        for(int blade=0;blade<8;blade++)for(int face=0;face<2;face++)for(int j=0;j<=12;j++)for(int k=0;k<3;k++)
                        {
                            int i=blade*78+face*39+j*3+k;float t=j/12f,phase=(Mathf.Sin(blade*2.31f)+1)*.5f;
                            var p=Quaternion.Euler(0,Mathf.Lerp(-7,7,phase)*t,0)*source[i];
                            p.x*=Mathf.Lerp(1,.90f+phase*.08f,t);p.z*=Mathf.Lerp(1,.90f+phase*.08f,t);
                            p.y+=Mathf.Sin(t*Mathf.PI)*Mathf.Lerp(-.10f,.13f,phase)-t*t*.05f;v[i]=p;
                        }
                    }
                    else
                    {
                        Require(v.Length%7==0,"custom coastal shrub topology");int count=v.Length/7;
                        for(int part=0;part<7;part++)
                        {
                            var bounds=new Bounds(source[part*count],Vector3.zero);for(int i=part*count;i<(part+1)*count;i++)bounds.Encapsulate(source[i]);
                            float phase=(Mathf.Sin(part*2.7f)+1)*.5f;
                            for(int i=part*count;i<(part+1)*count;i++)
                            {var d=source[i]-bounds.center;v[i]=bounds.center+Vector3.Scale(d,new Vector3(.92f+phase*.07f,.87f+phase*.22f,.98f-phase*.06f));}
                        }
                    }
                    edits.Add(Prepare(mesh,v,mesh.colors,stamp,changed,false));
                }
                return edits.ToArray();
            }
            foreach(var filter in terrain.World.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.name.StartsWith("Land ",StringComparison.Ordinal)||f.name.StartsWith("Landscape ",StringComparison.Ordinal)))
            {
                var mesh=filter.sharedMesh;var source=mesh.vertices;if(!source.Any(p=>Mask(stage,p.x,p.z)>0))continue;
                var stamp=Stamp(mesh,stage,out bool done);if(done)continue;
                var v=(Vector3[])source.Clone();var colors=mesh.colors;bool ground=filter.name.StartsWith("Land ",StringComparison.Ordinal);var changed=new bool[v.Length];
                Require(colors.Length==v.Length&&mesh.normals.Length==v.Length,"authored mesh channels");
                if(ground)
                {
                    for(int i=0;i<v.Length;i++)
                    {
                        var p=v[i];float mask=Mask(stage,p.x,p.z);if(mask==0)continue;
                        float noise=Mathf.PerlinNoise(p.x*.023f+41,p.z*.027f+19),originalAlpha=colors[i].a;
                        Color a,b;
                        if(stage==1){a=new Color(.54f,.70f,.39f);b=new Color(.67f,.77f,.48f);}
                        else if(stage==2){a=new Color(.83f,.68f,.46f);b=new Color(.94f,.81f,.57f);}
                        else if(stage==3){a=new Color(.39f,.56f,.39f);b=new Color(.51f,.65f,.43f);}
                        else{a=new Color(.81f,.88f,.88f);b=new Color(.94f,.96f,.94f);}
                        colors[i]=Color.Lerp(colors[i],Color.Lerp(a,b,noise),mask*(stage==4?.40f:.42f));colors[i].a=originalAlpha;changed[i]=true;
                    }
                }
                else foreach(var component in Components(mesh))
                {
                    var bounds=new Bounds(source[component[0]],Vector3.zero);foreach(int i in component)bounds.Encapsulate(source[i]);
                    var center=bounds.center;float mask=Mask(stage,center.x,center.z);if(mask==0)continue;
                    if(component.Any(i=>DesertFrontSetup.Mask(source[i].x,source[i].z)>0||SnowFrontSetup.Mask(source[i].x,source[i].z)>0||ForestEdgeSetup.Mask(source[i].x,source[i].z)>0))continue;
                    var color=colors[component[0]];if(!component.All(i=>colors[i]==color))continue; // preserve hand-painted components
                    bool green=color.g-color.r>.065f;
                    bool stone=stage==2&&color.r-color.g>.10f&&color.g-color.b>.10f;
                    if(!green&&!stone)continue; // including every existing solid brown trunk
                    Require(component.Length==70||component.Length==22,"custom landscape topology");
                    float phase=(float)(Math.Sin(center.x*12.9898+center.z*78.233)*43758.5453);phase-=Mathf.Floor(phase);
                    var scale=new Vector3(Mathf.Lerp(.82f,.98f,phase),Mathf.Lerp(.90f,1.12f,phase),Mathf.Lerp(.97f,.83f,phase));
                    // Round tree crowns retain their horizontal envelope. Snow cones rotate without stretching height.
                    if(stage==4)scale.y=1;
                    var rotate=Quaternion.Euler(0,phase*300*mask,0);
                    foreach(int i in component)
                    {
                        var delta=source[i]-center;v[i]=center+rotate*Vector3.Scale(delta,Vector3.Lerp(Vector3.one,scale,mask));
                        float cap=Mathf.InverseLerp(bounds.min.y,bounds.max.y,source[i].y);Color target;
                        if(stone)
                        {
                            float band=.5f+.5f*Mathf.Sin(cap*12+phase*3);
                            target=Color.Lerp(new Color(.60f,.43f,.29f),new Color(.86f,.69f,.46f),cap*.7f+band*.3f);
                        }
                        else if(stage==4)
                        {
                            float snow=Smooth(-.05f,.28f,mesh.normals[i].y)*Smooth(.05f,.45f,cap);
                            target=Color.Lerp(new Color(.29f,.45f,.40f),new Color(.88f,.94f,.93f),snow);
                        }
                        else if(stage==2)target=Color.Lerp(new Color(.28f,.45f,.32f),new Color(.48f,.62f,.39f),cap*.7f+phase*.3f);
                        else target=Color.Lerp(stage==1?new Color(.36f,.53f,.30f):new Color(.29f,.46f,.34f),stage==1?new Color(.63f,.74f,.39f):new Color(.52f,.64f,.38f),cap*.7f+phase*.3f);
                        target.a=color.a;colors[i]=Color.Lerp(color,target,mask*.85f);changed[i]=true;
                    }
                }
                if(changed.Any(b=>b))edits.Add(Prepare(mesh,v,colors,stamp,changed,ground));
            }
            return edits.ToArray();
        }
        static Edit Prepare(Mesh mesh,Vector3[] vertices,Color[] colors,Vector2[] stamp,bool[] changed,bool ground)
        {
            var normals=mesh.normals;
            if(!ground)
            {
                var temporary=Object.Instantiate(mesh);
                try{temporary.vertices=vertices;temporary.RecalculateNormals();var n=temporary.normals;for(int i=0;i<n.Length;i++)if(changed[i])normals[i]=n[i];}
                finally{Object.DestroyImmediate(temporary);}
            }
            return new Edit{Mesh=mesh,Vertices=vertices,Normals=normals,Colors=colors,Stamp=stamp,Changed=changed,Ground=ground};
        }
        public static bool Clear(Vector2 p,float radius,bool shoulder=true)
        {
            if(!StarterIslandLayout.Contains(p.x,p.y,radius+1))return false;
            for(int ix=0;ix<=12;ix++)for(int iz=0;iz<=12;iz++)
            {
                float x=Mathf.Lerp(p.x-radius,p.x+radius,ix/12f),z=Mathf.Lerp(p.y-radius,p.y+radius,iz/12f);
                if(StarterIslandLayout.RouteDistance(x,z)<2.5||shoulder&&StarterIslandLayout.RouteDistance(x,z)>5.8||StarterIslandLayout.CanPlace(Mathf.RoundToInt(x),Mathf.RoundToInt(z)))return false;
            }
            return true;
        }
        public static Site[] Sites(int stage)
        {
            var sites=new List<Site>();if(stage==0)return sites.ToArray();var random=new System.Random(106001+stage*911);
            if(stage==3)
            {
                // The interior has no road. Dress only existing unbuildable slopes, leaving its construction clearings open.
                var terrain=AssetDatabase.LoadAssetAtPath<GameObject>(TerrainSetup.PrefabPath).GetComponent<IslandTerrain>();
                var trunks=terrain.World.GetComponentsInChildren<MeshCollider>(true).Where(c=>c.name.StartsWith("Solid trunks ",StringComparison.Ordinal))
                    .SelectMany(c=>c.sharedMesh.vertices).Where(p=>p.x>440&&p.x<660&&p.z>730&&p.z<860).Select(p=>new Vector2(p.x,p.z)).ToArray();
                for(int attempt=0;attempt<1200&&sites.Count<12;attempt++)
                {
                    var p=new Vector2(465+(float)random.NextDouble()*160,768+(float)random.NextDouble()*60);float phase=(float)random.NextDouble();
                    if(Mask(stage,p.x,p.y)<.3f||!Clear(p,1.05f,false)||sites.Any(s=>Vector2.Distance(s.Point,p)<14)||trunks.Any(t=>Vector2.Distance(t,p)<2))continue;
                    sites.Add(new Site{Point=p,Kind=random.Next(3),Phase=phase});
                }
                return sites.ToArray();
            }
            int[] segments=stage==1?new[]{3,22,23}:stage==2?new[]{6,7}:stage==3?new[]{14}:new[]{8,9};
            foreach(int segment in segments)
            {
                var a=new Vector2((float)StarterIslandLayout.Route[segment-1,0],(float)StarterIslandLayout.Route[segment-1,1]);
                var b=new Vector2((float)StarterIslandLayout.Route[segment,0],(float)StarterIslandLayout.Route[segment,1]);
                var side=new Vector2(-(b-a).y,(b-a).x).normalized;float length=Vector2.Distance(a,b);
                for(float d=9;d<length-4;d+=12+(float)random.NextDouble()*10)foreach(int sign in new[]{-1,1})
                {
                    var p=Vector2.Lerp(a,b,d/length)+side*sign*4.1f;float phase=(float)random.NextDouble();
                    if(Mask(stage,p.x,p.y)<.2f||!Clear(p,1.05f)||sites.Any(s=>Vector2.Distance(s.Point,p)<10))continue;
                    sites.Add(new Site{Point=p,Kind=random.Next(3),Phase=phase});
                }
            }
            return sites.ToArray();
        }
        [MenuItem("Magic Island/Art/Refine Biomes In Order (Explicit)")]
        public static void RebuildAll(){for(int stage=0;stage<Names.Length;stage++)Rebuild(stage);}
        [MenuItem("Magic Island/Art/Biome Polish/1 Beach (Explicit)")] public static void Beach()=>Rebuild(0);
        [MenuItem("Magic Island/Art/Biome Polish/2 Grassland (Explicit)")] public static void Grassland()=>Rebuild(1);
        [MenuItem("Magic Island/Art/Biome Polish/3 Desert (Explicit)")] public static void Desert()=>Rebuild(2);
        [MenuItem("Magic Island/Art/Biome Polish/4 Forest (Explicit)")] public static void Forest()=>Rebuild(3);
        [MenuItem("Magic Island/Art/Biome Polish/5 Snow (Explicit)")] public static void Snow()=>Rebuild(4);
        public static void Rebuild(int stage)
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling,"use Edit Mode");
            var root=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);
            try
            {
                var terrain=root.GetComponent<IslandTerrain>();Require(terrain.World!=null&&terrain.World.IsValid,"authored world");
                var edits=Plan(terrain,stage);var old=terrain.World.Scenery.Find(Group(stage));var sites=old==null?Sites(stage):Array.Empty<Site>();
                Require(stage==0||stage==3||old!=null||sites.Length>=2,"incomplete safe decoration plan for "+Names[stage]);
                var material=AssetDatabase.LoadAssetAtPath<Material>(IslandBiomeSetup.MaterialFolder+"/Biome Details.mat");Require(material!=null,"shared material");
                var batches=new SortedDictionary<string,Geometry>(StringComparer.Ordinal);
                foreach(var site in sites)
                {
                    Require(terrain.World.Raycast(new Ray(terrain.transform.TransformPoint(new Vector3(site.Point.x,300,site.Point.y)),Vector3.down),out var hit,350),"actual baked ground at "+site.Point);
                    string name=Names[stage]+" biome verge "+Mathf.FloorToInt(site.Point.x/128)+" "+Mathf.FloorToInt(site.Point.y/128);
                    if(!batches.TryGetValue(name,out var g)){g=new Geometry();batches.Add(name,g);}
                    g.Cluster(terrain.World.Scenery.InverseTransformPoint(hit.point),stage,site.Kind,site.Phase);
                }
                foreach(string name in batches.Keys)Require(!File.Exists(StarterIslandWorldSetup.Folder+"/"+name+".asset"),"preserve orphan/manual batch: "+name);
                foreach(var e in edits)
                {
                    if(e.Colors.Length>0)e.Mesh.colors=e.Colors;e.Mesh.SetUVs(5,e.Stamp);
                    if(!e.Ground){e.Mesh.vertices=e.Vertices;e.Mesh.normals=e.Normals;e.Mesh.RecalculateBounds();}EditorUtility.SetDirty(e.Mesh);
                }
                if(batches.Count>0)
                {
                    var group=new GameObject(Group(stage)).transform;group.gameObject.layer=2;group.SetParent(terrain.World.Scenery,false);
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
                Debug.Log("Biome polish "+stage+" "+Names[stage]+": "+edits.Length+" meshes, "+sites.Length+" sites / "+batches.Count+" batches.");
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        static void Clean(string path)=>File.WriteAllText(path,string.Join("\n",File.ReadAllLines(path).Select(s=>s.TrimEnd()))+"\n");
        sealed class Geometry
        {
            readonly List<Vector3> v=new List<Vector3>();readonly List<Color> colors=new List<Color>();readonly List<int> triangles=new List<int>();
            void Ball(Vector3 p,Vector3 scale,Color c)
            {
                const int n=10,rings=6;int first=v.Count;
                for(int j=0;j<=rings;j++)for(int i=0;i<n;i++)
                {float a=j*Mathf.PI/rings,f=i*Mathf.PI*2/n;v.Add(p+Vector3.Scale(new Vector3(Mathf.Sin(a)*Mathf.Cos(f),Mathf.Cos(a),Mathf.Sin(a)*Mathf.Sin(f)),scale));var tint=c*Mathf.Lerp(1.07f,.82f,j/(float)rings);tint.a=1;colors.Add(tint);}
                for(int j=0;j<rings;j++)for(int i=0;i<n;i++){int a=first+j*n+i,b=first+j*n+(i+1)%n;triangles.AddRange(new[]{a,b,a+n,a+n,b,b+n});}
            }
            void Tube(Vector3 a,Vector3 b,float ra,float rb,Color color)
            {
                var axis=(b-a).normalized;var side=Vector3.Cross(axis,Mathf.Abs(axis.y)>.9f?Vector3.right:Vector3.up).normalized;var up=Vector3.Cross(axis,side);int first=v.Count;
                v.Add(a);v.Add(b);colors.Add(color);colors.Add(color);
                for(int i=0;i<10;i++){float angle=i*Mathf.PI*2/10;var d=side*Mathf.Cos(angle)+up*Mathf.Sin(angle);v.Add(a+d*ra);v.Add(b+d*rb);colors.Add(color);colors.Add(color);}
                for(int i=0;i<10;i++){int j=(i+1)%10;triangles.AddRange(new[]{first,first+2+j*2,first+2+i*2,first+1,first+3+i*2,first+3+j*2,first+2+i*2,first+2+j*2,first+3+i*2,first+3+i*2,first+2+j*2,first+3+j*2});}
            }
            public void Cluster(Vector3 p,int stage,int kind,float phase)
            {
                if(stage==1)
                {
                    for(int i=0;i<5;i++)
                    {
                        float angle=(phase+i*.2f)*Mathf.PI*2;var q=p+new Vector3(Mathf.Cos(angle)*.45f,0,Mathf.Sin(angle)*.45f);float h=.36f+(i%3)*.11f;
                        Tube(q,q+Vector3.up*h,.025f,.015f,new Color(.36f,.55f,.29f));
                        Ball(q+Vector3.up*h,new Vector3(.16f,.09f,.16f),kind==0?new Color(.96f,.88f,.58f):kind==1?new Color(.90f,.88f,.79f):new Color(.74f,.70f,.87f));
                        Ball(q+new Vector3(.10f,.14f,0),new Vector3(.17f,.06f,.08f),new Color(.49f,.66f,.34f));
                    }
                }
                else if(stage==2&&kind==0)
                {
                    var c=new Color(.39f,.56f,.35f);Tube(p,p+Vector3.up*1.45f,.22f,.18f,c);Ball(p+Vector3.up*1.45f,Vector3.one*.18f,c);
                    foreach(int sign in new[]{-1,1}){var a=p+Vector3.up*(sign<0?.55f:.85f);var b=a+Vector3.right*sign*.54f;Tube(a,b,.13f,.12f,c);Tube(b,b+Vector3.up*.45f,.12f,.10f,c);Ball(b+Vector3.up*.45f,Vector3.one*.10f,c);}
                }
                else if(stage==2||stage==4&&kind!=0)
                {
                    var c=stage==2?new Color(.76f,.57f,.36f):new Color(.58f,.68f,.68f);
                    Ball(p+Vector3.up*.22f,new Vector3(.78f,.40f,.63f),c);Ball(p+new Vector3(.56f,.12f,.28f),new Vector3(.30f,.23f,.27f),c*.96f);
                    if(stage==4)Ball(p+new Vector3(-.10f,.55f,-.06f),new Vector3(.60f,.12f,.48f),new Color(.91f,.95f,.94f));
                    else if(kind==2)for(int i=0;i<4;i++)Ball(p+new Vector3(-.50f+i*.25f,.15f,.52f),new Vector3(.10f,.22f,.10f),new Color(.61f,.62f,.36f));
                }
                else if(stage==3)
                {
                    for(int i=0;i<4;i++){float a=(phase+i*.25f)*Mathf.PI*2;Ball(p+new Vector3(Mathf.Cos(a)*.36f,.34f+i*.06f,Mathf.Sin(a)*.36f),new Vector3(.44f,.40f,.43f),Color.Lerp(new Color(.30f,.46f,.34f),new Color(.52f,.63f,.39f),i/4f));}
                    if(kind==1)Ball(p+new Vector3(.46f,.12f,.42f),new Vector3(.30f,.22f,.29f),new Color(.55f,.63f,.53f));
                }
                else
                {
                    Tube(p,p+Vector3.up*1.3f,.07f,.03f,new Color(.40f,.37f,.30f));
                    for(int j=0;j<3;j++){float y=.32f+j*.32f,r=.62f-j*.13f;Tube(p+Vector3.up*y,p+Vector3.up*(y+.65f),r,.015f,new Color(.35f,.49f,.43f));Tube(p+Vector3.up*(y+.18f),p+Vector3.up*(y+.68f),r*.78f,.012f,new Color(.88f,.94f,.93f));}
                }
            }
            public Mesh Mesh(){var mesh=new Mesh{indexFormat=v.Count>65535?UnityEngine.Rendering.IndexFormat.UInt32:UnityEngine.Rendering.IndexFormat.UInt16};mesh.SetVertices(v);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;}
        }
    }
}
