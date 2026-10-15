using System.IO;
using MagicIsland.Configuration;
using MagicIsland.Ocean;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using UnityEngine.Rendering;

namespace MagicIsland.EditorTools
{
    public static class OceanSetup
    {
        public const string PrefabPath = "Assets/Art/Prefabs/Environment/IslandOcean.prefab";
        public const string MeshFolder = "Assets/Art/Meshes/Ocean";
        public const string MaterialFolder = "Assets/Art/Materials/Ocean";
        public const int WaterVersion = 1;
        // Extend the original sea floor without moving any existing beach or reef vertices.
        public static void UpgradeWater()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if(prefab==null||prefab.GetComponent<OceanSurface>().WaterArtVersion>=WaterVersion)return;
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(MeshFolder+"/Island Shore.asset");
            const int segments=192,originalRings=32,extraRings=16;
            if(mesh==null||mesh.vertexCount!=segments*originalRings)return;
            var oldVertices=mesh.vertices;var oldColors=mesh.colors;var oldTriangles=mesh.triangles;
            if(oldColors.Length!=oldVertices.Length)return;
            int count=oldVertices.Length;var vertices=new Vector3[count+segments*extraRings];var colors=new Color[vertices.Length];
            System.Array.Copy(oldVertices,vertices,count);System.Array.Copy(oldColors,colors,count);
            var triangles=new int[oldTriangles.Length+segments*extraRings*6];System.Array.Copy(oldTriangles,triangles,oldTriangles.Length);int k=oldTriangles.Length;
            for(int a=0;a<segments;a++)
            {
                int next=(a+1)%segments;var edge=oldVertices[a*originalRings+originalRings-1];
                var direction=new Vector2(edge.x,edge.z).normalized;float radius=new Vector2(edge.x,edge.z).magnitude;
                for(int j=0;j<extraRings;j++)
                {
                    float r=Mathf.Lerp(radius,60,(j+1)/(float)extraRings),distance=r-radius;int current=count+a*extraRings+j;
                    vertices[current]=new Vector3(direction.x*r,edge.y-distance-distance*distance*.055f,direction.y*r);
                    colors[current]=oldColors[a*originalRings+originalRings-1];
                    int previous=j==0?a*originalRings+originalRings-1:current-1;
                    int nextPrevious=j==0?next*originalRings+originalRings-1:count+next*extraRings+j-1;
                    int nextCurrent=count+next*extraRings+j;
                    triangles[k++]=previous;triangles[k++]=nextPrevious;triangles[k++]=current;
                    triangles[k++]=current;triangles[k++]=nextPrevious;triangles[k++]=nextCurrent;
                }
            }
            mesh.Clear();mesh.vertices=vertices;mesh.colors=colors;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);
            var root=PrefabUtility.LoadPrefabContents(PrefabPath);
            try{root.GetComponent<OceanSurface>().WaterArtVersion=WaterVersion;PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);}
            finally{PrefabUtility.UnloadPrefabContents(root);}
            AssetDatabase.SaveAssets();
        }
        public static float Radius(float angle)
        {
            return (float)MagicIsland.Interaction.IslandLayout.CoastRadius(angle);
        }
        public static void Ensure(AddressableAssetSettings settings, GameConfig config)
        {
            Directory.CreateDirectory("Assets/Art/Prefabs/Environment"); Directory.CreateDirectory(MeshFolder); Directory.CreateDirectory(MaterialFolder); AssetDatabase.Refresh();
            var water = Material("Water", "MagicIsland/Island Ocean", Color.white);
            var coast = Material("Shore", "MagicIsland/Island Shore", Color.white);
            var rock = Material("Reef Stone", "Standard", new Color(.37f,.54f,.46f));
            var coral = Material("Coral", "Standard", new Color(.73f,.42f,.54f));
            var seaweed = Material("Seaweed", "Standard", new Color(.28f,.59f,.39f));
            var plane = MeshAsset("Ocean Surface", SurfaceMesh);
            var shore = MeshAsset("Island Shore", ShoreMesh);
            if (!File.Exists(PrefabPath))
            {
                var root = new GameObject("IslandOcean", typeof(OceanSurface)); root.layer = 2;
                try
                {
                    var model = root.GetComponent<OceanSurface>(); model.SeaLevel = -.22f;
                    model.Shore = MeshObject(root.transform, "Beach and seabed", shore, coast, Vector3.zero);
                    model.Water = MeshObject(root.transform, "Ocean surface", plane, water, new Vector3(0,model.SeaLevel,0));
                    for (int i=0;i<26;i++)
                    {
                        float a=i*Mathf.PI*2/26+.17f, r=Radius(a)+1.4f+(i%3)*.7f;
                        float slope=(r-Radius(a)+1.7f)/9.2f;
                        float bed=-.02f-4.4f*slope*slope;
                        var pos=new Vector3(Mathf.Cos(a)*r,Mathf.Min(bed+.2f,-.62f),Mathf.Sin(a)*r);
                        Part(root.transform,"Submerged reef "+i,PrimitiveType.Sphere,pos,new Vector3(.7f+(i%3)*.25f,.55f,.6f+(i%2)*.25f),rock);
                        if (i%2==0)
                        {
                            Part(root.transform,"Seaweed "+i,PrimitiveType.Capsule,pos+new Vector3(.5f,.08f,.15f),new Vector3(.12f,.20f,.12f),seaweed);
                            Part(root.transform,"Coral "+i,PrimitiveType.Sphere,pos+new Vector3(-.4f,.15f,.15f),new Vector3(.35f,.35f,.28f),coral);
                        }
                    }
                    PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
                }
                finally { Object.DestroyImmediate(root); }
            }
            var group=settings.FindGroup("Environment") ?? settings.CreateGroup("Environment",false,false,true,null,typeof(BundledAssetGroupSchema),typeof(ContentUpdateGroupSchema));
            var guid=AssetDatabase.AssetPathToGUID(PrefabPath); var entry=settings.FindAssetEntry(guid) ?? settings.CreateOrMoveEntry(guid,group);
            if (entry.address!="IslandOcean") entry.SetAddress("IslandOcean");
            if (!File.ReadAllText("Assets/Config/GameConfig.asset").Contains("OceanPrefabAddress:")) EditorUtility.SetDirty(config);
        }
        static Material Material(string name,string shader,Color color)
        {
            string path=MaterialFolder+"/"+name+".mat"; var existing=AssetDatabase.LoadAssetAtPath<Material>(path); if(existing!=null)return existing;
            var found=Shader.Find(shader); if(found==null)throw new System.Exception("Missing ocean shader "+shader);
            var result=new Material(found){name=name}; if(result.HasProperty("_Color"))result.SetColor("_Color",color); if(result.HasProperty("_Glossiness"))result.SetFloat("_Glossiness",.08f);
            AssetDatabase.CreateAsset(result,path);return result;
        }
        static Mesh MeshAsset(string name,System.Func<Mesh> create)
        {
            string path=MeshFolder+"/"+name+".asset"; var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(existing!=null)return existing;
            var mesh=create();mesh.name=name;AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
        static Renderer MeshObject(Transform parent,string name,Mesh mesh,Material material,Vector3 position)
        {
            var root=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));root.layer=2;root.transform.SetParent(parent,false);root.transform.localPosition=position;
            root.GetComponent<MeshFilter>().sharedMesh=mesh;var r=root.GetComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;return r;
        }
        static void Part(Transform parent,string name,PrimitiveType type,Vector3 position,Vector3 scale,Material material)
        {
            var part=GameObject.CreatePrimitive(type);part.name=name;part.layer=2;part.transform.SetParent(parent,false);part.transform.localPosition=position;part.transform.localScale=scale;
            part.GetComponent<Renderer>().sharedMaterial=material;Object.DestroyImmediate(part.GetComponent<Collider>());
        }
        static Mesh SurfaceMesh()
        {
            const int n=80;var vertices=new Vector3[(n+1)*(n+1)];var triangles=new int[n*n*6];int k=0;
            for(int z=0;z<=n;z++)for(int x=0;x<=n;x++)vertices[z*(n+1)+x]=new Vector3(x-40,0,z-40);
            for(int z=0;z<n;z++)for(int x=0;x<n;x++) {int a=z*(n+1)+x,b=a+n+1;triangles[k++]=a;triangles[k++]=b;triangles[k++]=a+1;triangles[k++]=a+1;triangles[k++]=b;triangles[k++]=b+1;}
            var mesh=new Mesh{vertices=vertices,triangles=triangles};mesh.RecalculateNormals();mesh.RecalculateBounds();
            var bounds=mesh.bounds;bounds.Expand(Vector3.up*.5f);mesh.bounds=bounds;return mesh;
        }
        internal static Mesh ShoreMesh()
        {
            const int segments=192,rings=32;var vertices=new Vector3[segments*rings];var colors=new Color[vertices.Length];var triangles=new int[segments*(rings-1)*6];int k=0;
            var sand=new Color(.87f,.79f,.57f);var bottom=new Color(.47f,.70f,.57f);
            for(int a=0;a<segments;a++)
            {
                float angle=a*Mathf.PI*2/segments;var direction=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));float edge=Radius(angle);
                for(int j=0;j<rings;j++)
                {
                    float r,y;Color color;
                    r=Mathf.Lerp(edge-4.5f,edge+7.5f,j/(float)(rings-1));
                    if(r<=edge-.45f){y=(float)MagicIsland.Interaction.IslandLayout.Height(direction.x*r,direction.y*r)-.008f;color=CoastArtSetup.LandColor(r,edge,angle);}
                    else {float t=(r-edge+1.7f)/9.2f;y=-.02f-4.4f*t*t;color=Color.Lerp(sand,bottom,Mathf.Clamp01(t*1.4f));}
                    int i=a*rings+j;vertices[i]=new Vector3(direction.x*r,y,direction.y*r);colors[i]=color;
                }
            }
            for(int a=0;a<segments;a++)for(int j=0;j<rings-1;j++)
            {int i=a*rings+j,b=((a+1)%segments)*rings+j;triangles[k++]=i;triangles[k++]=b;triangles[k++]=i+1;triangles[k++]=i+1;triangles[k++]=b;triangles[k++]=b+1;}
            var mesh=new Mesh{vertices=vertices,colors=colors,triangles=triangles};mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
    }
}
