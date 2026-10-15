using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MagicIsland.Ocean;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    public static partial class OceanBiomeSetup
    {
        public const int Version=8,Segments=256,Rings=128;
        public const string MeshFolder="Assets/Art/Meshes/Ocean/Ecology";
        public const string MaterialPath="Assets/Art/Materials/Ocean/Ecosystem.mat";
        static readonly Color[] CoralColors={new Color(.78f,.55f,.61f),new Color(.65f,.57f,.75f),new Color(.83f,.69f,.48f),new Color(.47f,.68f,.65f),new Color(.68f,.55f,.68f),new Color(.54f,.65f,.75f),new Color(.86f,.65f,.57f),new Color(.69f,.74f,.58f)};
        public static void PrepareValidation(){PrototypeAddressablesSetup.EnsureSetup();AssetDatabase.SaveAssets();}
        public static void Ensure()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(OceanSetup.PrefabPath);
            if(prefab==null||prefab.GetComponent<OceanSurface>().BiomeArtVersion>=Version)return;
            Directory.CreateDirectory(MeshFolder);AssetDatabase.Refresh();
            var root=PrefabUtility.LoadPrefabContents(OceanSetup.PrefabPath);
            try
            {
                var surface=root.GetComponent<OceanSurface>();var geography=new OceanGeography(surface.GeographySeed);
                SaveMesh(OceanSetup.MeshFolder+"/Ocean Surface.asset",SurfaceMesh());
                SaveMesh(OceanSetup.MeshFolder+"/Island Shore.asset",FloorMesh(geography));
                var material=AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
                if(material==null){material=new Material(Shader.Find("MagicIsland/Ocean Ecology")){name="Ocean Ecosystem"};AssetDatabase.CreateAsset(material,MaterialPath);}
                BuildEcology(root,surface,geography,material);
                BuildWildlife(root,surface,geography,material);
                var water=surface.Water.sharedMaterial;
                water.SetColor("_ShallowColor",new Color(.38f,.68f,.64f));water.SetColor("_MidColor",new Color(.19f,.46f,.52f));
                water.SetColor("_DeepColor",new Color(.08f,.25f,.35f));water.SetColor("_FoamColor",new Color(.87f,.92f,.87f));
                water.SetFloat("_DepthRange",4.6f);water.SetFloat("_Sparkle",.10f);water.SetFloat("_FoamStrength",.48f);
                water.SetFloat("_NormalStrength",.06f);water.SetFloat("_CausticsStrength",.045f);EditorUtility.SetDirty(water);
                surface.BiomeArtVersion=Version;PrefabUtility.SaveAsPrefabAsset(root,OceanSetup.PrefabPath);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            AssetDatabase.SaveAssets();
        }
        [MenuItem("Magic Island/Rebuild Ocean Geography And Ecology")]
        public static void Rebuild()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            var root=PrefabUtility.LoadPrefabContents(OceanSetup.PrefabPath);
            try{root.GetComponent<OceanSurface>().BiomeArtVersion=0;PrefabUtility.SaveAsPrefabAsset(root,OceanSetup.PrefabPath);}
            finally{PrefabUtility.UnloadPrefabContents(root);}
            Ensure();
        }
        static Mesh SaveMesh(string path,Mesh mesh)
        {
            mesh.name=Path.GetFileNameWithoutExtension(path);var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(existing==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}
            EditorUtility.CopySerialized(mesh,existing);EditorUtility.SetDirty(existing);Object.DestroyImmediate(mesh);return existing;
        }
        static Mesh SurfaceMesh()
        {
            var axes=new List<float>();for(int x=-120;x< -40;x+=4)axes.Add(x);for(int x=-40;x<=40;x++)axes.Add(x);for(int x=44;x<=120;x+=4)axes.Add(x);
            int n=axes.Count;var vertices=new Vector3[n*n];var triangles=new List<int>((n-1)*(n-1)*6);
            for(int z=0;z<n;z++)for(int x=0;x<n;x++)vertices[z*n+x]=new Vector3(axes[x],0,axes[z]);
            for(int z=0;z<n-1;z++)for(int x=0;x<n-1;x++){int a=z*n+x,b=a+n;triangles.AddRange(new[]{a,b,a+1,a+1,b,b+1});}
            var mesh=new Mesh{vertices=vertices,triangles=triangles.ToArray()};mesh.RecalculateNormals();mesh.RecalculateBounds();var bounds=mesh.bounds;bounds.Expand(Vector3.up*.5f);mesh.bounds=bounds;return mesh;
        }
        static Mesh FloorMesh(OceanGeography geography)
        {
            var vertices=new Vector3[Segments*Rings];var colors=new Color[vertices.Length];var triangles=new int[Segments*(Rings-1)*6];int k=0;
            for(int a=0;a<Segments;a++)
            {
                float angle=a*Mathf.PI*2/Segments,edge=OceanSetup.Radius(angle);var direction=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
                for(int j=0;j<Rings;j++)
                {
                    // Keep the enlarged shallow shelf and its soft edge densely sampled.
                    float radius=j<88?edge-4.5f+j*.5f:Mathf.Lerp(edge+39.5f,180,(j-87)/40f);
                    float x=direction.x*radius,z=direction.y*radius,y=(float)geography.Height(x,z);int index=a*Rings+j;
                    vertices[index]=new Vector3(x,y,z);
                    if(radius<=edge-.45f)colors[index]=CoastArtSetup.LandColor(radius,edge,angle);
                    else
                    {
                        float depth=(float)geography.Depth(x,z);float noise=(float)geography.Noise(x*.18,z*.18);
                        Color sand=new Color(.81f,.78f,.65f),bottom=new Color(.43f,.56f,.58f);
                        Color tint=geography.Biome(x,z)==OceanBiome.SeagrassMeadow?new Color(.54f,.64f,.51f):bottom;
                        colors[index]=Color.Lerp(sand,tint,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.55f,2.8f,depth)))*(.98f+noise*.04f);
                    }
                }
            }
            for(int a=0;a<Segments;a++)for(int j=0;j<Rings-1;j++){int i=a*Rings+j,b=((a+1)%Segments)*Rings+j;triangles[k++]=i;triangles[k++]=b;triangles[k++]=i+1;triangles[k++]=i+1;triangles[k++]=b;triangles[k++]=b+1;}
            var mesh=new Mesh{vertices=vertices,colors=colors,triangles=triangles};mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        static double Range(System.Random random,double a,double b)=>a+(b-a)*random.NextDouble();
        static Color Tint(System.Random random,Color color)=>color*(float)Range(random,.88,1.10);
        static Mesh Source(string name)=>AssetDatabase.LoadAssetAtPath<Mesh>(CoastArtSetup.MeshFolder+"/"+name+".asset");
        static GameObject MeshObject(Transform parent,string name,Mesh mesh,Material material,Vector3 position)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.layer=2;go.transform.SetParent(parent,false);go.transform.localPosition=position;
            go.GetComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;return go;
        }
        static void BuildEcology(GameObject root,OceanSurface surface,OceanGeography geography,Material material)
        {
            var floor=new BakedFloor(surface.Shore.GetComponent<MeshFilter>().sharedMesh);
            var old=root.transform.Find("Shallow reef gardens");if(old!=null)Object.DestroyImmediate(old.gameObject);
            old=root.transform.Find("Reef fish schools");if(old!=null)Object.DestroyImmediate(old.gameObject);
            var gardens=new GameObject("Shallow reef gardens");gardens.layer=2;gardens.transform.SetParent(root.transform,false);
            var schoolsRoot=new GameObject("Reef fish schools");schoolsRoot.layer=2;schoolsRoot.transform.SetParent(root.transform,false);
            var random=new System.Random(surface.GeographySeed+911);var centers=new List<Vector2>();
            var obstacles=new List<OceanSwimObstacle>();
            var rock=Source("Reef Rock");var grass=Source("Seagrass Blades");var coral=Source("Branch Coral");var shell=Source("Clam Shell");
            var fan=SaveMesh(MeshFolder+"/Fan Coral.asset",FanMesh());var sponge=SaveMesh(MeshFolder+"/Tube Sponge.asset",SpongeMesh());var star=SaveMesh(MeshFolder+"/Sea Star.asset",StarMesh());var fish=SaveMesh(MeshFolder+"/Reef Fish.asset",FishMesh());
            var table=SaveMesh(MeshFolder+"/Table Coral.asset",TableCoral());var boulder=SaveMesh(MeshFolder+"/Boulder Coral.asset",BoulderCoral());
            for(int attempt=0;attempt<9000&&centers.Count<84;attempt++)
            {
                bool terrace=centers.Count>=48;
                double angleAt=Range(random,0,Math.PI*2),width=geography.ShelfWidth(angleAt);
                double radiusAt=MagicIsland.Interaction.IslandLayout.CoastRadius(angleAt)+(terrace?Range(random,width+4,width+geography.ReefWidth(angleAt)-1):Range(random,2,width));
                float x=(float)(Math.Cos(angleAt)*radiusAt),z=(float)(Math.Sin(angleAt)*radiusAt);
                float depth=(float)OceanGeography.SeaLevel-floor.Height(x,z);var center=new Vector2(x,z);
                if(depth<.42f||depth>3.2f||(terrace?depth<1.6f:depth>1.4f)||centers.Any(p=>(p-center).sqrMagnitude<10))continue;
                int index=centers.Count;centers.Add(center);Vector3 origin=new Vector3(x,floor.Height(x,z),z);var builder=new Geometry();
                var go=new GameObject("Habitat "+index,typeof(OceanHabitatPatch));go.layer=2;go.transform.SetParent(gardens.transform,false);go.transform.localPosition=origin;
                var patch=go.GetComponent<OceanHabitatPatch>();patch.Biome=terrace?OceanBiome.ReefSlope:geography.Biome(x,z);
                int count=terrace?34:32;
                for(int j=0;j<count;j++)
                {
                    float angle=(float)Range(random,0,Math.PI*2),radius=(float)Math.Sqrt(random.NextDouble())*(terrace?3:2.3f);
                    float px=x+Mathf.Cos(angle)*radius,pz=z+Mathf.Sin(angle)*radius,available=(float)OceanGeography.SeaLevel-floor.Height(px,pz)-.11f;
                    if(available<.12f||available>4.5f)continue;
                    Mesh mesh;Color tint;float scale;bool sway=false;double choice=random.NextDouble();
                    if(choice<(terrace?.68:.46)&&available>.3f)
                    {
                        int shape=random.Next(4);mesh=shape==0?coral:shape==1?fan:shape==2?table:boulder;
                        tint=CoralColors[random.Next(CoralColors.Length)];scale=(float)(terrace?Range(random,1.1,1.85):Range(random,.24,.52));patch.Corals++;
                        if(terrace)patch.LargeCorals++;else patch.SmallCorals++;
                    }
                    else if(choice<.76){mesh=grass;tint=new Color(.40f,.58f,.47f);scale=(float)Range(random,.35,.72);sway=true;patch.Grasses++;}
                    else if(choice<.83&&terrace){mesh=sponge;tint=new Color(.73f,.66f,.48f);scale=(float)Range(random,.7,1.3);patch.Sponges++;}
                    else if(choice<.89){mesh=rock;tint=new Color(.57f,.64f,.59f);scale=(float)Range(random,.25,.65);patch.Rocks++;}
                    else if(choice<.95){mesh=shell;tint=new Color(.84f,.79f,.68f);scale=(float)Range(random,.14,.24);patch.Shells++;}
                    else{mesh=star;tint=CoralColors[random.Next(CoralColors.Length)];scale=(float)Range(random,.6,1);patch.Stars++;}
                    scale=Mathf.Min(scale,available/Mathf.Max(mesh.bounds.size.y,.05f));
                    float py=floor.Height(px,pz)-mesh.bounds.min.y*scale+.005f;
                    if(mesh==coral||mesh==fan||mesh==table||mesh==boulder||mesh==rock||mesh==sponge)
                    {
                        // A rotation-independent circle encloses every vertex of this solid object.
                        float solidRadius=mesh.vertices.Max(v=>new Vector2(v.x,v.z).magnitude)*scale;
                        obstacles.Add(new OceanSwimObstacle{Center=new Vector2(px,pz),Radius=solidRadius,Bottom=py+mesh.bounds.min.y*scale,Top=py+mesh.bounds.max.y*scale,Coral=mesh==coral||mesh==fan||mesh==table||mesh==boulder});
                    }
                    builder.Add(mesh,Matrix4x4.TRS(new Vector3(px,py,pz)-origin,Quaternion.Euler(0,(float)Range(random,0,360),0),Vector3.one*scale),Tint(random,tint),sway);
                }
                if(builder.Count==0){Object.DestroyImmediate(go);centers.RemoveAt(centers.Count-1);continue;}
                MeshObject(go.transform,"Combined reef",SaveMesh(MeshFolder+"/Habitat "+index.ToString("00")+".asset",builder.Mesh()),material,Vector3.zero);
            }
            if(centers.Count!=84)throw new InvalidOperationException("Could not place both shallow gardens and coral terrace habitats");
            surface.SwimObstacles=obstacles.ToArray();
        }
        internal sealed class BakedFloor
        {
            readonly Vector3[] vertices;
            public BakedFloor(Mesh mesh){vertices=mesh.vertices;}
            public float Height(float x,float z)
            {
                double angle=Math.Atan2(z,x);if(angle<0)angle+=Math.PI*2;
                int sector=Math.Min(Segments-1,(int)(angle/(Math.PI*2)*Segments)),next=(sector+1)%Segments;
                for(int j=0;j<Rings-1;j++)
                {
                    var a=vertices[sector*Rings+j];var b=vertices[next*Rings+j];var c=vertices[sector*Rings+j+1];var d=vertices[next*Rings+j+1];
                    if(Triangle(x,z,a,b,c,out float y)||Triangle(x,z,c,b,d,out y))return y;
                }
                throw new InvalidOperationException("Ecology position outside baked ocean floor");
            }
            static bool Triangle(float x,float z,Vector3 a,Vector3 b,Vector3 c,out float y)
            {
                float denominator=(b.z-c.z)*(a.x-c.x)+(c.x-b.x)*(a.z-c.z);
                float u=((b.z-c.z)*(x-c.x)+(c.x-b.x)*(z-c.z))/denominator;
                float v=((c.z-a.z)*(x-c.x)+(a.x-c.x)*(z-c.z))/denominator;
                float w=1-u-v;y=u*a.y+v*b.y+w*c.y;return u>=-.0001f&&v>=-.0001f&&w>=-.0001f;
            }
        }
        sealed class Geometry
        {
            readonly List<Vector3> vertices=new List<Vector3>();readonly List<Color> colors=new List<Color>();readonly List<int> triangles=new List<int>();
            readonly List<Vector4> motion=new List<Vector4>();
            readonly List<Vector4> shapes=new List<Vector4>();
            public int Count=>vertices.Count;
            public void Add(Mesh mesh,Matrix4x4 matrix,Color tint,bool sway,Vector4? fishMotion=null,Vector4? fishShape=null)
            {
                int offset=vertices.Count;var v=mesh.vertices;var c=mesh.colors;
                for(int i=0;i<v.Length;i++){vertices.Add(matrix.MultiplyPoint3x4(v[i]));motion.Add(fishMotion??Vector4.zero);shapes.Add(fishShape??Vector4.zero);Color color=c.Length==v.Length?c[i]*tint:tint;color.a=sway?Mathf.Clamp01((v[i].y-mesh.bounds.min.y)/Mathf.Max(.01f,mesh.bounds.size.y)):c.Length==v.Length?c[i].a:0;colors.Add(color);}
                triangles.AddRange(mesh.triangles.Select(i=>i+offset));
            }
            public void Triangle(Vector3 a,Vector3 b,Vector3 c,Color color)
            {int i=vertices.Count;vertices.AddRange(new[]{a,b,c});motion.AddRange(new[]{Vector4.zero,Vector4.zero,Vector4.zero});shapes.AddRange(new[]{Vector4.zero,Vector4.zero,Vector4.zero});colors.AddRange(new[]{color,color,color});triangles.AddRange(new[]{i,i+1,i+2});}
            public void Tube(Vector3 a,Vector3 b,float radius,Color color)
            {
                var direction=(b-a).normalized;var side=Vector3.Cross(direction,Vector3.forward).normalized;if(side.sqrMagnitude<.1f)side=Vector3.right;var up=Vector3.Cross(direction,side);
                for(int i=0;i<6;i++){float x=i*Mathf.PI/3,y=(i+1)*Mathf.PI/3;var p=(side*Mathf.Cos(x)+up*Mathf.Sin(x))*radius;var q=(side*Mathf.Cos(y)+up*Mathf.Sin(y))*radius;Triangle(a+p,b+p,a+q,color);Triangle(a+q,b+p,b+q,color);Triangle(b+p,b,b+q,color*.7f);}
            }
            public Mesh Mesh(){var mesh=new Mesh();if(vertices.Count>65535)mesh.indexFormat=IndexFormat.UInt32;mesh.vertices=vertices.ToArray();mesh.colors=colors.ToArray();mesh.triangles=triangles.ToArray();mesh.SetUVs(1,motion);mesh.SetUVs(2,shapes);mesh.RecalculateNormals();mesh.RecalculateBounds();var b=mesh.bounds;b.Expand(new Vector3(.12f,0,.12f));mesh.bounds=b;return mesh;}
        }
        static Mesh TableCoral()
        {
            var g=new Geometry();Color c=new Color(1,1,1,0);
            g.Tube(Vector3.zero,Vector3.up*.30f,.05f,c);
            Ellipsoid(g,new Vector3(0,.32f,0),new Vector3(.48f,.055f,.40f),v=>c*(v<0?.82f:1));
            Ellipsoid(g,new Vector3(.16f,.18f,.12f),new Vector3(.27f,.045f,.24f),v=>c);
            return g.Mesh();
        }
        static Mesh BoulderCoral()
        {
            var g=new Geometry();Color c=new Color(1,1,1,0);
            Ellipsoid(g,new Vector3(0,.17f,0),new Vector3(.30f,.23f,.28f),v=>c*(.89f+.11f*v));
            for(int i=0;i<7;i++){float a=i*Mathf.PI*2/7;Ellipsoid(g,new Vector3(Mathf.Cos(a)*.20f,.23f,Mathf.Sin(a)*.19f),new Vector3(.12f,.13f,.12f),v=>c);}
            return g.Mesh();
        }
        static Mesh FanMesh()
        {
            var g=new Geometry();Color c=new Color(1,1,1,0);g.Tube(Vector3.zero,Vector3.up*.22f,.035f,c);
            for(int i=0;i<9;i++){float a=Mathf.Lerp(-1.05f,1.05f,i/8f);var middle=new Vector3(Mathf.Sin(a)*.24f,.28f+Mathf.Cos(a)*.13f,.03f*Mathf.Sin(i));var tip=new Vector3(Mathf.Sin(a)*.47f,.30f+Mathf.Cos(a)*.38f,.08f*Mathf.Sin(i*1.3f));g.Tube(Vector3.up*.17f,middle,.025f,c);g.Tube(middle,tip,.013f,c);}
            return g.Mesh();
        }
        static Mesh SpongeMesh()
        {
            var g=new Geometry();for(int j=0;j<3;j++){float radius=.075f+j*.012f,height=.25f+j*.09f;var p=new Vector3((j-1)*.12f,0,.07f*(j%2));for(int i=0;i<8;i++){float a=i*Mathf.PI/4,b=(i+1)*Mathf.PI/4;var u=new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius);var v=new Vector3(Mathf.Cos(b)*radius,0,Mathf.Sin(b)*radius);var top=Vector3.up*height;Color white=new Color(1,1,1,0),dark=new Color(.35f,.35f,.35f,0);g.Triangle(p+u,p+u+top,p+v,white);g.Triangle(p+v,p+u+top,p+v+top,white);g.Triangle(p+u+top,p+u*.62f+top,p+v+top,white);g.Triangle(p+v+top,p+u*.62f+top,p+v*.62f+top,white);g.Triangle(p+u*.62f+top,p+Vector3.up*(height-.10f),p+v*.62f+top,dark);}}
            return g.Mesh();
        }
        static Mesh StarMesh()
        {
            var g=new Geometry();for(int i=0;i<10;i++){float a=i*Mathf.PI/5,b=(i+1)*Mathf.PI/5;float r=i%2==0?.19f:.075f,s=(i+1)%2==0?.19f:.075f;g.Triangle(Vector3.up*.045f,new Vector3(Mathf.Cos(b)*s,.008f,Mathf.Sin(b)*s),new Vector3(Mathf.Cos(a)*r,.008f,Mathf.Sin(a)*r),new Color(1,1,1,0));}return g.Mesh();
        }
        static Mesh FishMesh()
        {
            var g=new Geometry();var nose=new Vector3(0,0,.19f);var tail=new Vector3(0,0,-.17f);var points=new[]{new Vector3(.065f,0,0),new Vector3(0,.065f,0),new Vector3(-.065f,0,0),new Vector3(0,-.05f,0)};
            for(int i=0;i<4;i++){g.Triangle(nose,points[i],points[(i+1)%4],new Color(1,1,1,0));g.Triangle(tail,points[(i+1)%4],points[i],new Color(.85f,.85f,.85f,0));}
            g.Triangle(tail,new Vector3(-.08f,0,-.28f),new Vector3(.08f,0,-.28f),new Color(.8f,.8f,.8f,.5f));
            for(int side=-1;side<=1;side+=2)g.Triangle(new Vector3(side*.026f,.018f,.12f),new Vector3(side*.03f,.035f,.10f),new Vector3(side*.032f,.008f,.10f),new Color(.04f,.04f,.04f,0));return g.Mesh();
        }
    }
}
