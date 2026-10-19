using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MagicIsland.Interaction;
using MagicIsland.Ocean;
using MagicIsland.TerrainSystem;
using MagicIsland.UI.Views;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;
using Factory=MagicIsland.EditorTools.PrototypeUiPrefabBuilder;

namespace MagicIsland.EditorTools
{
    // Explicit art operation. Normal Setup never rebuilds or rearranges this authored world.
    public static partial class StarterIslandWorldSetup
    {
        public const string Folder="Assets/Art/Meshes/Terrain/StarterWorld";
        static Material ground,details,magic,water;
        public static void Rebuild()=>BiomeIslandsSetup.Rebuild();
        // Historical source authoring, deliberately absent from menus and normal setup.
        static void RebuildLegacySource()
        {
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            ground=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Terrain/Biome Ground.mat");
            details=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Terrain/Biome Details.mat");
            if(ground==null||details==null)throw new InvalidOperationException("Build existing biome art first.");
            magic=Material("Magic Landmarks","MagicIsland/World Magic");
            bool newWater=!File.Exists("Assets/Art/Materials/Terrain/Lake Water.mat");
            water=Material("Lake Water","MagicIsland/Island Ocean");
            if(!water.HasProperty("_WaveHeight"))throw new InvalidOperationException("Lake water shader missing.");
            // First creation only: subsequent explicit rebakes retain hand-tuned material values.
            if(newWater)
            {
                var ocean=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Ocean/Water.mat");
                water.CopyPropertiesFromMaterial(ocean);water.SetFloat("_WaveHeight",.012f);water.SetFloat("_WaveSpeed",.25f);
                water.SetColor("_DeepColor",new Color(.065f,.30f,.51f));water.SetColor("_MidColor",new Color(.18f,.56f,.68f));
                water.SetColor("_ShallowColor",new Color(.58f,.77f,.68f));EditorUtility.SetDirty(water);
            }
            var root=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);
            try
            {
                var terrain=root.GetComponent<IslandTerrain>();
                var group=terrain.World!=null?terrain.World.gameObject:new GameObject("Large starter island",typeof(StarterIslandWorld));group.transform.SetParent(root.transform,false);
                var world=group.GetComponent<StarterIslandWorld>();world.ArtVersion=2;
                var scenery=world.Scenery;
                if(scenery==null){scenery=new GameObject("Baked landscape").transform;scenery.SetParent(group.transform,false);}world.Scenery=scenery;
                var grounds=new List<MeshCollider>();var waters=new List<Renderer>();
                for(int ix=0;ix<13;ix++)for(int iz=0;iz<9;iz++)
                {
                    float x=-832+ix*128,z=96+iz*128;
                    var mesh=Grid(x,iz==0?32:z,128,iz==0?192:128,8,false);
                    grounds.Add(Part(group.transform,"Land "+ix+" "+iz,mesh,ground,true).GetComponent<MeshCollider>());
                    Landscape(scenery,ix,iz,x,z);
                }
                // Fine collision strips keep the old village transition and narrow crossings continuous.
                grounds.Add(Part(group.transform,"Village boardwalk",Strip(),details,true).GetComponent<MeshCollider>());
                grounds.Add(Part(group.transform,"Tree island stone path",Isle(),details,true).GetComponent<MeshCollider>());
                grounds.Add(Part(group.transform,"Outlet bridge",BridgeMesh(),details,true).GetComponent<MeshCollider>());
                var pathMesh=Paths();LakeSnowTrailSetup.PreserveOnWorldRebuild(group.transform,pathMesh);
                GrassDesertTrailSetup.PreserveOnWorldRebuild(group.transform,pathMesh);
                Part(group.transform,"Exploration paths",pathMesh,details,false);
                waters.Add(Part(group.transform,"Central lake",Lake(),water,false).GetComponent<Renderer>());
                waters.Add(Part(group.transform,"Northern stream",River(new Vector2(130,790),new Vector2(90,950),5),water,false).GetComponent<Renderer>());
                waters.Add(Part(group.transform,"Lake outlet",River(new Vector2(340,520),new Vector2(490,440),9,new Vector2(720,270)),water,false).GetComponent<Renderer>());
                world.Grounds=grounds.ToArray();world.Waters=waters.ToArray();terrain.World=world;
                // Retain all legacy terrain/collider records and move the tiny prototype biomes to the mainland.
                if(terrain.BiomeScenery!=null)terrain.BiomeScenery.gameObject.SetActive(false);
                var village=Object.Instantiate(terrain.Ground.GetComponent<MeshFilter>().sharedMesh);
                var positions=village.vertices;var colors=village.colors;var uv=village.uv;
                for(int i=0;i<positions.Length;i++)
                {
                    var p=positions[i];float inset=(float)IslandLayout.CoastRadius(Math.Atan2(p.z,p.x))-new Vector2(p.x,p.z).magnitude;
                    colors[i]=Color.Lerp(new Color(.92f,.83f,.62f),new Color(.59f,.72f,.42f),Mathf.SmoothStep(0,1,Mathf.InverseLerp(2,4,inset)));
                    if(uv.Length==positions.Length)uv[i]=new Vector2(Mathf.InverseLerp(2,4,inset),0);
                }
                village.colors=colors;village.uv=uv;terrain.Ground.GetComponent<MeshFilter>().sharedMesh=Store("Village Grass",village);
                // Original north coast gets a boardwalk entrance; the actual pier gate stays untouched.
                var boundary=Object.Instantiate(AssetDatabase.LoadAssetAtPath<Mesh>(TerrainSetup.MeshFolder+"/Sea Boundary.asset"));
                var vertices=boundary.vertices;var indices=boundary.triangles;var kept=new List<int>();
                for(int i=0;i<indices.Length;i+=3)
                {
                    var p=(vertices[indices[i]]+vertices[indices[i+1]]+vertices[indices[i+2]])/3;
                    if(p.z>14&&Mathf.Abs(p.x)<5.8f)continue;
                    kept.AddRange(new[]{indices[i],indices[i+1],indices[i+2]});
                }
                boundary.triangles=kept.ToArray();terrain.Boundary.sharedMesh=Store("Village Open Boundary",boundary);
                Landmark(scenery,"Amber ruins",-420,590,Amber());
                Landmark(scenery,"Luminous ancient tree",-8,700,AncientTree());
                Landmark(scenery,"Moonflower grove",480,650,FlowerGrove());
                Landmark(scenery,"Frost circle",-40,1000,Frost());
                RouteDetails(scenery);
                if(!terrain.IsValid)throw new InvalidOperationException("Large starter world has incomplete baked references.");
                PrefabUtility.SaveAsPrefabAsset(root,TerrainSetup.PrefabPath);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            // Only the sea plane grows. Preserve the reef floor, all fish habitats and ecology metadata.
            var oceanRoot=PrefabUtility.LoadPrefabContents(OceanSetup.PrefabPath);
            try
            {
                var surface=oceanRoot.GetComponent<OceanSurface>();var t=surface.Water.transform;
                var size=surface.Water.GetComponent<MeshFilter>().sharedMesh.bounds.size;
                var position=new Vector3(0,t.localPosition.y,560);var scale=new Vector3(3200/size.x,1,2600/size.z);
                if(t.localPosition!=position||t.localScale!=scale)
                {t.localPosition=position;t.localScale=scale;PrefabUtility.SaveAsPrefabAsset(oceanRoot,OceanSetup.PrefabPath);}
            }
            finally{PrefabUtility.UnloadPrefabContents(oceanRoot);}
            EnsureMap();AssetDatabase.SaveAssets();AssetDatabase.Refresh();
            Debug.Log("Large starter island baked: 117 terrain chunks, original village retained, lake/streams/bridge, four magic landmarks.");
        }
        public static void EnsureMap()
        {
            UiPrefabMigration.EnsureChineseFont(UiPrefabMigration.FontAsset);
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(MapPanelSetup.Path);
            if(asset==null||asset.GetComponent<MapView>().ScaleButton!=null)return;
            var root=PrefabUtility.LoadPrefabContents(MapPanelSetup.Path);
            try
            {
                var view=root.GetComponent<MapView>();var card=view.CloseButton.transform.parent;
                view.ScaleButton=Factory.Button("Map Scale",card,new Vector2(774,-20),new Vector2(176,42),"查看全岛",Factory.Sand);
                view.ScaleButton.gameObject.AddComponent<MagicIsland.UI.UIButtonAudio>();
                PrefabUtility.SaveAsPrefabAsset(root,MapPanelSetup.Path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        static Material Material(string name,string shader)
        {
            string path="Assets/Art/Materials/Terrain/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat!=null)return mat;mat=new Material(Shader.Find(shader)){name=name};AssetDatabase.CreateAsset(mat,path);return mat;
        }
        static Mesh Store(string name,Mesh mesh)
        {
            mesh.name=name;string path=Folder+"/"+name+".asset";var current=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(current==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}
            EditorUtility.CopySerialized(mesh,current);EditorUtility.SetDirty(current);Object.DestroyImmediate(mesh);return current;
        }
        static GameObject Part(Transform parent,string name,Mesh mesh,Material material,bool collision)
        {
            var existing=parent.Find(name);var go=existing!=null?existing.gameObject:new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);go.layer=collision?0:2;
            mesh=Store(name,mesh);go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=material;
            if(collision){var collider=go.GetComponent<MeshCollider>();if(collider==null)collider=go.AddComponent<MeshCollider>();collider.sharedMesh=mesh;}return go;
        }
        public static Color GroundColor(double x,double z)
        {
            double coast=StarterIslandLayout.CoastDistance(x,z),lake=StarterIslandLayout.LakeDistance(x,z);
            Color grass=new Color(.60f,.73f,.43f),sand=new Color(.91f,.77f,.51f),snow=new Color(.89f,.94f,.96f),forest=new Color(.43f,.62f,.39f);
            Color color=Color.Lerp(grass,forest,(float)StarterIslandLayout.ForestWeight(x,z));
            color=Color.Lerp(color,sand,(float)StarterIslandLayout.DesertWeight(x,z));
            color=Color.Lerp(color,snow,(float)StarterIslandLayout.SnowWeight(x,z));
            if(coast<22)color=Color.Lerp(sand,color,Mathf.SmoothStep(0,1,Mathf.Clamp01((float)coast/22)));
            if(lake<4)color=Color.Lerp(new Color(.76f,.77f,.56f),color,Mathf.Clamp01((float)(lake+6)/10));
            float variation=(float)(Math.Sin(x*.027)*Math.Sin(z*.018))*.035f;return color+new Color(variation,variation,variation,0);
        }
        static double VertexHeight(double x,double z)
        {
            double coast=StarterIslandLayout.CoastDistance(x,z),lake=StarterIslandLayout.LakeDistance(x,z);
            if(coast<0)return Math.Max(-18,coast*.45);
            if(lake<0&&!StarterIslandLayout.Bridge(x,z))return 12-10*Mathf.SmoothStep(0,1,Mathf.Clamp01((float)-lake/35));
            return StarterIslandLayout.Ground(x,z);
        }
        static double BakedHeight(double x,double z)
        {
            if(StarterIslandLayout.TreeIsle(x,z))return x< -32?CausewayHeight((float)x)-.08:12.6;
            if(StarterIslandLayout.Bridge(x,z)||StarterIslandLayout.Corridor(x,z))return StarterIslandLayout.Ground(x,z);
            return TerrainGridHeight(x,z);
        }
        static double TerrainGridHeight(double x,double z)
        {
            double gx=Math.Floor((x+832)/8)*8-832,gz=Math.Floor((z-96)/8)*8+96,fx=(x-gx)/8,fz=(z-gz)/8;
            double a=VertexHeight(gx,gz),b=VertexHeight(gx+8,gz),c=VertexHeight(gx,gz+8),d=VertexHeight(gx+8,gz+8);
            return fx+fz<=1?a+fx*(b-a)+fz*(c-a):d+(1-fx)*(c-d)+(1-fz)*(b-d);
        }
        static Vector3 P(double x,double z,float offset=0)=>new Vector3((float)x,(float)BakedHeight(x,z)+offset,(float)z);
        static Mesh Grid(float x,float z,int width,int depth,int step,bool isleOnly)
        {
            var g=new Geometry();int nx=width/step,nz=depth/step;
            for(int i=0;i<=nx;i++)for(int j=0;j<=nz;j++)
            {
                double px=x+i*step,pz=z+j*step;double distance=StarterIslandLayout.CoastDistance(px,pz);
                float height=(float)VertexHeight(px,pz);
                float grass=(float)((1-StarterIslandLayout.DesertWeight(px,pz))*(1-StarterIslandLayout.SnowWeight(px,pz))
                    *(1-StarterIslandLayout.ForestWeight(px,pz)))*Mathf.SmoothStep(0,1,Mathf.Clamp01((float)distance/22));
                g.Vertex(new Vector3((float)px,height,(float)pz),GroundColor(px,pz),grass);
            }
            for(int i=0;i<nx;i++)for(int j=0;j<nz;j++)
            {
                double cx=x+(i+.5)*step,cz=z+(j+.5)*step;
                if(isleOnly&&!StarterIslandLayout.TreeIsle(cx,cz))continue;
                int a=i*(nz+1)+j,b=a+nz+1;g.Tri(a,a+1,b);g.Tri(b,a+1,b+1);
            }
            return g.Mesh();
        }
        static Mesh Strip()
        {
            var g=new Geometry();var wood=new Color(.65f,.49f,.31f);
            for(int i=14;i<180;i++)
            {
                float y=BoardwalkHeight(i),ny=BoardwalkHeight(i+1);
                g.Quad(new Vector3(-5.5f,y,i),new Vector3(-5.5f,ny,i+1),new Vector3(5.5f,ny,i+1),new Vector3(5.5f,y,i),i%2==0?wood:wood*.92f);
                if(i%5==0)foreach(int s in new[]{-1,1}){g.Tube(new Vector3(s*5.3f,y-.8f,i),new Vector3(s*5.3f,y+1.05f,i),.12f,.1f,wood);}
                if(i>=25&&i<175)foreach(int s in new[]{-1,1})g.Tube(new Vector3(s*5.3f,y+.95f,i),new Vector3(s*5.3f,ny+.95f,i+1),.07f,.07f,wood*.83f);
            }
            return g.Mesh();
        }
        static float BoardwalkHeight(float z)
        {
            double height=StarterIslandLayout.Ground(0,z);
            foreach(double x in new[]{-5.5,-2.75,0,2.75,5.5})height=Math.Max(height,TerrainGridHeight(x,z));
            return (float)height+.08f;
        }
        static Mesh Isle()
        {
            var g=new Geometry();var stone=new Color(.76f,.78f,.63f);const int n=96,rings=14;
            g.Vertex(new Vector3(-8,12.6f,700),stone);
            for(int j=1;j<=rings;j++)for(int i=0;i<n;i++)
            {
                float r=27f*j/rings,a=i*Mathf.PI*2/n,y=Mathf.Lerp(12.6f,10,Mathf.SmoothStep(0,1,Mathf.InverseLerp(22,27,r)));
                g.Vertex(new Vector3(-8+Mathf.Cos(a)*r,y,700+Mathf.Sin(a)*r),stone);
            }
            for(int i=0;i<n;i++)g.Tri(0,1+(i+1)%n,1+i);
            for(int j=0;j<rings-1;j++)for(int i=0;i<n;i++){int a=1+j*n+i,b=1+j*n+(i+1)%n;g.Tri(a,b,a+n);g.Tri(a+n,b,b+n);}
            for(int x=-80;x<-8;x+=2)g.Quad(new Vector3(x,CausewayHeight(x),696.5f),new Vector3(x,CausewayHeight(x),703.5f),new Vector3(x+2,CausewayHeight(x+2),703.5f),new Vector3(x+2,CausewayHeight(x+2),696.5f),stone);
            return g.Mesh();
        }
        static float CausewayHeight(float x)=>Mathf.Lerp((float)TerrainGridHeight(-80,700)+.025f,12.68f,Mathf.SmoothStep(0,1,Mathf.InverseLerp(-80,-72,x)));
        static Mesh BridgeMesh()
        {
            var g=new Geometry();var c=new Color(.64f,.45f,.29f);
            for(int i=-24;i<24;i++)
            {
                Vector3 a=new Vector3(490+.6f*i,12,440+.8f*i),b=a+new Vector3(.6f,0,.8f),side=new Vector3(.8f,0,-.6f)*3.5f;
                a.y=12+.65f*Mathf.SmoothStep(0,1,Mathf.InverseLerp(24,14,Mathf.Abs(i)));
                b.y=12+.65f*Mathf.SmoothStep(0,1,Mathf.InverseLerp(24,14,Mathf.Abs(i+1)));
                g.Quad(a-side,b-side,b+side,a+side,i%2==0?c:c*.9f);
                if(i%4==0)foreach(int s in new[]{-1,1})g.Tube(a+side*s,a+side*s+Vector3.up*1.2f,.13f,.1f,c);
            }
            return g.Mesh();
        }
        static Mesh Paths()
        {
            var g=new Geometry();var tint=new Color(.84f,.76f,.54f);var r=StarterIslandLayout.Route;var used=new HashSet<string>();
            for(int i=1;i<r.GetLength(0);i++)
            {
                var a=new Vector2((float)r[i-1,0],(float)r[i-1,1]);var b=new Vector2((float)r[i,0],(float)r[i,1]);var side=new Vector2(-(b-a).y,(b-a).x).normalized*2.3f;
                string key=a.x+","+a.y+":"+b.x+","+b.y,reverse=b.x+","+b.y+":"+a.x+","+a.y;
                if(used.Contains(key)||used.Contains(reverse))continue;used.Add(key);
                int count=Mathf.CeilToInt(Vector2.Distance(a,b));
                for(int j=0;j<count;j++)
                {
                    var p=Vector2.Lerp(a,b,j/(float)count);var q=Vector2.Lerp(a,b,(j+1f)/count);var mid=(p+q)*.5f;
                    if(mid.y<180||!StarterIslandLayout.Contains(mid.x,mid.y,1)||StarterIslandLayout.Bridge(mid.x,mid.y,-4))continue;
                    float width=.87f+.1f*Mathf.Sin(mid.y*.047f+mid.x*.031f);var edge=side*width;
                    g.Quad(P(p.x+edge.x,p.y+edge.y,.07f),P(q.x+edge.x,q.y+edge.y,.07f),P(q.x-edge.x,q.y-edge.y,.07f),P(p.x-edge.x,p.y-edge.y,.07f),tint);
                }
            }
            return g.Mesh();
        }
        static Mesh Lake()
        {
            var g=new Geometry();g.Vertex(new Vector3(160,12,630),Color.white);
            const int n=192;
            for(int i=0;i<n;i++)
            {
                double a=i*Math.PI*2/n,r=StarterIslandLayout.LakeRadius(a);
                g.Vertex(new Vector3(160+(float)(Math.Cos(a)*220*r),12,630+(float)(Math.Sin(a)*190*r)),Color.white);
            }
            for(int i=0;i<n;i++)g.Tri(0,1+(i+1)%n,1+i);return g.Mesh();
        }
        static Mesh River(Vector2 a,Vector2 b,float radius,Vector2? c=null)
        {
            var g=new Geometry();var points=c.HasValue?new[]{a,b,c.Value}:new[]{a,b};
            for(int k=1;k<points.Length;k++)
            {
                a=points[k-1];b=points[k];var side=new Vector2(-(b-a).y,(b-a).x).normalized*radius;
                int count=Mathf.CeilToInt(Vector2.Distance(a,b)/3);
                for(int j=0;j<count;j++)
                {
                    var p=Vector2.Lerp(a,b,j/(float)count);var q=Vector2.Lerp(a,b,(j+1f)/count);
                    Func<Vector2,Vector3> wp=v=>new Vector3(v.x,(float)(12*Mathf.SmoothStep(0,1,Mathf.Clamp01((float)StarterIslandLayout.CoastDistance(v.x,v.y)/70))),v.y);
                    g.Quad(wp(p-side),wp(q-side),wp(q+side),wp(p+side),Color.white);
                }
            }
            return g.Mesh();
        }
        static void Landscape(Transform parent,int ix,int iz,float x,float z)
        {
            var random=new System.Random(76391+ix*317+iz*17);var g=new Geometry();var trunks=new Geometry();int count=0;
            for(int i=0;i<65;i++)
            {
                float px=x+(float)random.NextDouble()*128,pz=z+(float)random.NextDouble()*128;
                if(!StarterIslandLayout.OriginalContains(px,pz,24)||StarterIslandLayout.RouteDistance(px,pz)<12||StarterIslandLayout.TreeIsle(px,pz,-10)||StarterIslandLayout.OriginalLakeDistance(px,pz)<16)continue;
                if(new[]{new Vector2(-420,590),new Vector2(-8,700),new Vector2(480,650),new Vector2(-40,1000)}.Any(p=>Vector2.Distance(p,new Vector2(px,pz))<36))continue;
                int region=StarterIslandLayout.OriginalRegion(px,pz);var p0=P(px,pz);float scale=2.5f+(float)random.NextDouble()*3.5f;
                // Thin the original fixed candidates into groves and clearings; never add a new solid trunk.
                float grove=GroveDensity(px,pz);if(ScatterValue(px,pz)>grove)continue;
                if(region==1)
                {
                    if(i%3==0){g.Tube(p0,p0+Vector3.up*scale,.48f,.4f,new Color(.32f,.53f,.35f));g.Ball(p0+Vector3.up*scale,Vector3.one*.41f,new Color(.32f,.53f,.35f));}
                    else g.Ball(p0+Vector3.up*scale*.24f,new Vector3(scale,scale*.5f,scale*.8f),new Color(.72f,.49f,.31f));
                }
                else if(region==3||region==2||i%4==0)
                {
                    g.Tube(p0,p0+Vector3.up*scale*2,.35f,.16f,new Color(.45f,.32f,.22f));
                    trunks.Tube(p0,p0+Vector3.up*scale*1.8f,.35f,.18f,Color.white);
                    Color leaf=Color.Lerp(new Color(.30f,.51f,.30f),new Color(.72f,.83f,.81f),(float)StarterIslandLayout.SnowWeight(px,pz));
                    if(region==2)for(int h=0;h<3;h++)g.Tube(p0+Vector3.up*(scale*(.6f+h*.5f)),p0+Vector3.up*(scale*(1.8f+h*.4f)),scale*(.7f-h*.13f),.02f,leaf);
                    else
                    {
                        g.Ball(p0+Vector3.up*scale*2,new Vector3(scale,scale*.8f,scale),leaf);
                        g.Ball(p0+new Vector3(scale*.6f,scale*1.7f,0),Vector3.one*scale*.7f,leaf*1.06f);
                    }
                }
                else if(i%3==0)g.Ball(p0+Vector3.up*.7f,new Vector3(1.8f,1.2f,1.3f),new Color(.58f,.62f,.54f));
                else
                {
                    for(int h=0;h<4;h++)g.Ball(p0+new Vector3(h*.45f,.2f,(h%2)*.5f),new Vector3(.5f,.35f,.5f),i%2==0?new Color(.89f,.81f,.48f):new Color(.71f,.77f,.52f));
                }
                count++;
            }
            if(count==0)
            {
                foreach(string name in new[]{"Landscape "+ix+" "+iz,"Solid trunks "+ix+" "+iz})
                {var old=parent.Find(name);if(old!=null)Object.DestroyImmediate(old.gameObject);}return;
            }
            Part(parent,"Landscape "+ix+" "+iz,g.Mesh(),details,false);
            if(trunks.Count>0)
            {
                string name="Solid trunks "+ix+" "+iz;var old=parent.Find(name);var go=old!=null?old.gameObject:new GameObject(name,typeof(MeshCollider));go.transform.SetParent(parent,false);
                go.GetComponent<MeshCollider>().sharedMesh=Store(go.name,trunks.Mesh());
            }
            else if(parent.Find("Solid trunks "+ix+" "+iz)!=null)Object.DestroyImmediate(parent.Find("Solid trunks "+ix+" "+iz).gameObject);
        }
        static void Landmark(Transform parent,string name,float x,float z,Mesh mesh)
        {
            var go=Part(parent,name,mesh,name=="Luminous ancient tree"?Material("Ancient Tree","MagicIsland/Ancient Tree"):magic,false);go.transform.localPosition=P(x,z);
            if(name=="Amber ruins"||name=="Luminous ancient tree"||name=="Frost circle")
            {
                var c=go.GetComponent<CapsuleCollider>();if(c==null)c=go.AddComponent<CapsuleCollider>();c.radius=name=="Amber ruins"?11:name=="Frost circle"?2:2.5f;
                c.height=name=="Amber ruins"?22:name=="Frost circle"?7:13;c.center=Vector3.up*c.height*.5f;
            }
        }
        static Mesh Amber()
        {
            const string path="SourceArt/WorldPlanning/Models/StarterAmber.meshdata.json";
            if(!File.Exists(path))throw new InvalidOperationException("Missing Blender amber source: "+path);
            var data=JsonUtility.FromJson<AmberData>(File.ReadAllText(path));
            if(data==null||data.version!=1||data.positions==null||data.positions.Length%3!=0||data.colors==null
                ||data.colors.Length!=data.positions.Length/3*4||data.triangles==null||data.triangles.Length%3!=0)
                throw new InvalidOperationException("Invalid Blender amber mesh data.");
            var positions=new Vector3[data.positions.Length/3];var colors=new Color[positions.Length];
            for(int i=0;i<positions.Length;i++)
            {
                positions[i]=new Vector3(data.positions[i*3],data.positions[i*3+1],data.positions[i*3+2]);
                colors[i]=new Color(data.colors[i*4],data.colors[i*4+1],data.colors[i*4+2],data.colors[i*4+3]);
                if(!Finite(positions[i].x)||!Finite(positions[i].y)||!Finite(positions[i].z)
                    ||!Finite(colors[i].r)||!Finite(colors[i].g)||!Finite(colors[i].b)||!Finite(colors[i].a))
                    throw new InvalidOperationException("Nonfinite Blender amber mesh data.");
            }
            if(positions.Length==0||data.triangles.Any(i=>i<0||i>=positions.Length))throw new InvalidOperationException("Invalid amber indices.");
            var mesh=new Mesh{indexFormat=positions.Length>65535?IndexFormat.UInt32:IndexFormat.UInt16};
            mesh.vertices=positions;mesh.colors=colors;mesh.triangles=data.triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        [Serializable] sealed class AmberData{public int version;public float[] positions;public float[] colors;public int[] triangles;}
        static Mesh AncientTree()
        {
            return TreeFromBlender();
        }
        static Mesh FlowerGrove()
        {
            var g=new Geometry();Color stone=new Color(.59f,.64f,.65f);
            g.Tube(new Vector3(-4,0,0),new Vector3(-4,7,0),1,.8f,stone);g.Tube(new Vector3(4,0,0),new Vector3(4,7,0),1,.8f,stone);
            for(int i=0;i<9;i++){float a=i*Mathf.PI/8;var p=new Vector3(Mathf.Cos(a)*4,7+Mathf.Sin(a)*3,0);g.Ball(p,new Vector3(.9f,1,.9f),stone);}
            var random=new System.Random(764);
            for(int i=0;i<180;i++)
            {
                float a=(float)random.NextDouble()*Mathf.PI*2,r=6+(float)random.NextDouble()*17;var p=new Vector3(Mathf.Cos(a)*r,.5f,Mathf.Sin(a)*r);
                g.Tube(p-Vector3.up*.5f,p,.07f,.05f,new Color(.37f,.61f,.49f));
                g.Ball(p,new Vector3(.5f,.22f,.5f),i%3==0?new Color(.63f,.78f,1):new Color(.72f,.63f,.95f));
            }
            return g.Mesh();
        }
        static Mesh Frost()
        {
            var g=new Geometry();
            for(int i=0;i<8;i++)
            {
                float a=i*Mathf.PI/4;var p=new Vector3(Mathf.Cos(a)*12,0,Mathf.Sin(a)*12);
                g.Ball(p+Vector3.up*2.7f,new Vector3(1.6f,3.2f,1.6f),new Color(.57f,.69f,.73f));
                g.Crystal(p+new Vector3(2,0,0),.8f,3.5f,new Color(.49f,.88f,1));
            }
            g.Crystal(Vector3.zero,2,7,new Color(.61f,.91f,1));return g.Mesh();
        }
        sealed class Geometry
        {
            readonly List<Vector3> v=new List<Vector3>();readonly List<Color> c=new List<Color>();readonly List<Vector2> uv=new List<Vector2>();readonly List<int> t=new List<int>();
            public int Count=>v.Count;
            public void Vertex(Vector3 p,Color color,float grass=0){v.Add(p);c.Add(color);uv.Add(new Vector2(grass,0));}
            public void Tri(int a,int b,int d){t.Add(a);t.Add(b);t.Add(d);}
            public void Quad(Vector3 a,Vector3 b,Vector3 d,Vector3 e,Color color){int i=v.Count;Vertex(a,color);Vertex(b,color);Vertex(d,color);Vertex(e,color);Tri(i,i+1,i+2);Tri(i,i+2,i+3);}
            public void Tube(Vector3 a,Vector3 b,float ra,float rb,Color color)
            {
                var axis=(b-a).normalized;var side=Vector3.Cross(axis,Mathf.Abs(axis.y)>.9f?Vector3.right:Vector3.up).normalized;var up=Vector3.Cross(axis,side);
                const int n=10;int first=v.Count;Vertex(a,color);Vertex(b,color);
                for(int i=0;i<n;i++){float f=i*Mathf.PI*2/n;var d=side*Mathf.Cos(f)+up*Mathf.Sin(f);Vertex(a+d*ra,color);Vertex(b+d*rb,color);}
                for(int i=0;i<n;i++){int j=(i+1)%n;Tri(first,first+2+j*2,first+2+i*2);Tri(first+1,first+3+i*2,first+3+j*2);Tri(first+2+i*2,first+2+j*2,first+3+i*2);Tri(first+3+i*2,first+2+j*2,first+3+j*2);}
            }
            public void Ball(Vector3 p,Vector3 scale,Color color)
            {
                const int n=10,rings=6;int first=v.Count;
                for(int j=0;j<=rings;j++)for(int i=0;i<n;i++){float f=i*Mathf.PI*2/n,a=j*Mathf.PI/rings;Vertex(p+Vector3.Scale(new Vector3(Mathf.Sin(a)*Mathf.Cos(f),Mathf.Cos(a),Mathf.Sin(a)*Mathf.Sin(f)),scale),color);}
                for(int j=0;j<rings;j++)for(int i=0;i<n;i++){int a=first+j*n+i,b=first+j*n+(i+1)%n;Tri(a,b,a+n);Tri(a+n,b,b+n);}
            }
            public void Crystal(Vector3 p,float radius,float height,Color color)
            {
                const int n=6;int first=v.Count;Vertex(p+Vector3.up*height,color*1.12f);
                for(int i=0;i<n;i++){float a=i*Mathf.PI*2/n;Vertex(p+new Vector3(Mathf.Cos(a)*radius,height*.58f,Mathf.Sin(a)*radius),color*(.82f+i*.035f));Vertex(p+new Vector3(Mathf.Cos(a)*radius*.7f,0,Mathf.Sin(a)*radius*.7f),color*.85f);}
                for(int i=0;i<n;i++){int a=first+1+i*2,b=first+1+(i+1)%n*2;Tri(first,b,a);Tri(a,b,a+1);Tri(a+1,b,b+1);}
            }
            public Mesh Mesh(){var mesh=new Mesh{indexFormat=v.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};mesh.SetVertices(v);mesh.SetColors(c);mesh.SetUVs(0,uv);mesh.SetTriangles(t,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;}
        }
    }
}
