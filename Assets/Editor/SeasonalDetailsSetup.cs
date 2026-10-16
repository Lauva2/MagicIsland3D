using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MagicIsland.Configuration;
using MagicIsland.Interaction;
using MagicIsland.TerrainSystem;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    public static class SeasonalDetailsSetup
    {
        public const string PrefabPath="Assets/Art/Prefabs/Environment/SeasonalGroundDetails.prefab";
        public const string Folder="Assets/Art/Meshes/Seasons";
        public const string MaterialPath="Assets/Art/Materials/Weather/Seasonal Ground.mat";
        public static void Prepare(){PrototypeAddressablesSetup.EnsureSetup();AssetDatabase.SaveAssets();}
        [MenuItem("Magic Island/Rebuild Seasonal Ground Details")]
        public static void Rebuild()
        {
            Prepare();Create(AssetDatabase.LoadAssetAtPath<Material>(MaterialPath),true);AssetDatabase.SaveAssets();
        }
        public static void Ensure(AddressableAssetSettings settings)
        {
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            var material=AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if(material==null){material=new Material(Shader.Find("MagicIsland/Seasonal Details")){name="Seasonal Ground"};AssetDatabase.CreateAsset(material,MaterialPath);}
            if(!File.Exists(PrefabPath))Create(material);
            var group=settings.FindGroup("Environment");var guid=AssetDatabase.AssetPathToGUID(PrefabPath);
            var entry=settings.FindAssetEntry(guid)??settings.CreateOrMoveEntry(guid,group);if(entry.address!=SeasonalGroundCover.Address)entry.SetAddress(SeasonalGroundCover.Address);
            var terrain=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);
            try
            {
                var marker=terrain.GetComponent<IslandTerrain>();
                if(marker.SeasonalDetails==null)
                {
                    var existing=terrain.GetComponentInChildren<SeasonalGroundCover>(true);
                    marker.SeasonalDetails=existing!=null?existing:((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath),terrain.transform)).GetComponent<SeasonalGroundCover>();
                    PrefabUtility.SaveAsPrefabAsset(terrain,TerrainSetup.PrefabPath);
                }
            }
            finally{PrefabUtility.UnloadPrefabContents(terrain);}
        }
        static void Create(Material material,bool rebuild=false)
        {
            var root=new GameObject(SeasonalGroundCover.Address,typeof(SeasonalGroundCover));root.layer=2;
            try
            {
                var cover=root.GetComponent<SeasonalGroundCover>();
                var authored=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)?.GetComponent<SeasonalGroundCover>();
                if(authored!=null)
                {cover.ScatterSeed=authored.ScatterSeed;cover.MinimumPatches=authored.MinimumPatches;cover.MaximumPatches=authored.MaximumPatches;cover.MinimumScale=authored.MinimumScale;cover.MaximumScale=authored.MaximumScale;}
                if(!cover.ScatterValid)throw new ArgumentException("Invalid seasonal scatter settings");
                var patches=new List<SeasonalGroundPatch>();var points=new List<Vector3>();var random=new System.Random(cover.ScatterSeed);
                int count=random.Next(cover.MinimumPatches,cover.MaximumPatches+1);
                var meshes=new Mesh[8,4];for(int v=0;v<8;v++)for(int s=0;s<4;s++)meshes[v,s]=MeshAsset(v,s,cover.ScatterSeed,rebuild);
                var gathering=AssetDatabase.LoadAssetAtPath<GatheringCatalog>("Assets/Config/GatheringCatalog.asset");
                for(int attempt=0;attempt<12000&&patches.Count<count;attempt++)
                {
                    var point=Point(random,15);float px=point.x,pz=point.z;
                    float r=Mathf.Sqrt(px*px+pz*pz),edge=(float)IslandLayout.CoastRadius(Mathf.Atan2(pz,px));
                    if(r<5.8f||r>edge-4.8f||points.Any(p=>(p-point).sqrMagnitude<1.15f*1.15f))continue;
                    points.Add(point);
                    var node=gathering.Nodes.Where(n=>n.TypeId=="tree"&&Vector2.Distance(new Vector2(n.Position.x,n.Position.z),new Vector2(px,pz))<2.7f).FirstOrDefault();
                    int variant=random.Next(4)+(node!=null?4:0);
                    var go=new GameObject("Ground patch "+patches.Count,typeof(MeshFilter),typeof(MeshRenderer),typeof(SeasonalGroundPatch));go.layer=2;go.transform.SetParent(root.transform,false);
                    go.transform.localPosition=new Vector3(px,(float)IslandLayout.Height(px,pz)+.016f,pz);
                    go.transform.localRotation=Quaternion.Euler(0,(float)random.NextDouble()*360,0);
                    go.transform.localScale=Vector3.one*Mathf.Lerp(cover.MinimumScale,cover.MaximumScale,(float)random.NextDouble());
                    var p=go.GetComponent<SeasonalGroundPatch>();p.Filter=go.GetComponent<MeshFilter>();p.Surface=go.GetComponent<MeshRenderer>();p.TreeId=node?.Id;
                    p.Surface.sharedMaterial=material;p.Surface.shadowCastingMode=ShadowCastingMode.Off;p.Surface.enabled=false;
                    for(int s=0;s<4;s++)p.Seasons[s]=meshes[variant,s];
                    p.Filter.sharedMesh=p.Seasons[0];patches.Add(p);
                }
                if(patches.Count!=count)throw new InvalidOperationException("Seasonal scatter cannot fit the requested patch count");
                cover.Patches=patches.ToArray();PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
            }
            finally{Object.DestroyImmediate(root);}
        }
        static Mesh MeshAsset(int variant,int season,int seed,bool rebuild)
        {
            string path=Folder+"/Ground "+variant+" "+season+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(mesh!=null&&!rebuild)return mesh;
            var g=new Geometry();var random=new System.Random(unchecked(seed+variant*17+season*991));
            Color green=season==0?new Color(.50f,.68f,.32f):season==1?new Color(.37f,.59f,.27f):season==2?new Color(.61f,.53f,.29f):new Color(.56f,.57f,.46f);
            int tufts=season==1?random.Next(11,18):season==0?random.Next(6,11):season==2?random.Next(3,7):random.Next(1,5);
            for(int i=0;i<tufts;i++)
            {
                var p=Point(random,.28f);float h=(season==1?.18f:.11f)+(float)random.NextDouble()*.09f;
                for(int b=0;b<3;b++)
                {
                    float a=(float)random.NextDouble()*6.28f;var side=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*.025f;
                    g.Triangle(p-side,p+Vector3.up*h+side*.7f,p+side,green);
                }
            }
            if(season==0)for(int f=0,flowers=random.Next(3,8);f<flowers;f++)
            {
                var p=Point(random,.29f);float h=.13f+(float)random.NextDouble()*.05f;
                g.Triangle(p-Vector3.right*.008f,p+Vector3.up*h,p+Vector3.right*.008f,green);
                var center=p+Vector3.up*h;Color c=f%3==0?new Color(.88f,.68f,.73f):f%3==1?new Color(.94f,.85f,.62f):new Color(.91f,.91f,.81f);
                for(int petal=0;petal<5;petal++)
                {float a=petal*Mathf.PI*.4f;var offset=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*.040f;g.Diamond(center+offset,.033f,.018f,a,c);}
                g.Diamond(center+Vector3.up*.003f,.018f,.018f,0,new Color(.83f,.65f,.28f));
            }
            if(season==2)
            {
                for(int i=0,leaves=variant>=4?random.Next(14,25):random.Next(2,6);i<leaves;i++)
                {
                    var p=Point(random,.32f)+Vector3.up*.004f;var c=i%2==0?new Color(.76f,.44f,.23f):new Color(.83f,.64f,.32f);
                    g.Diamond(p,.045f,.023f,(float)random.NextDouble()*6.28f,c);
                }
                if(variant>=4)for(int i=0,falling=random.Next(1,3);i<falling;i++)
                {var p=Point(random,.14f)+Vector3.up*(2.02f+i*.03f);g.Diamond(p,.045f,.025f,i*.8f,new Color(.78f,.52f,.27f),new Vector2(1,i*.41f));}
            }
            if(season==3)for(int f=0,frost=random.Next(8,17);f<frost;f++)
            {
                var p=Point(random,.32f)+Vector3.up*.004f;float a=(float)random.NextDouble()*6.28f;
                g.Diamond(p,.055f,.035f,a,new Color(.78f,.82f,.76f));
            }
            var created=g.Mesh("Ground "+variant+" "+season);
            if(mesh==null){AssetDatabase.CreateAsset(created,path);return created;}
            EditorUtility.CopySerialized(created,mesh);Object.DestroyImmediate(created);EditorUtility.SetDirty(mesh);return mesh;
        }
        static Vector3 Point(System.Random random,float radius)
        {float a=(float)random.NextDouble()*Mathf.PI*2,r=Mathf.Sqrt((float)random.NextDouble())*radius;return new Vector3(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r);}
        sealed class Geometry
        {
            readonly List<Vector3> vertices=new List<Vector3>();readonly List<Color> colors=new List<Color>();readonly List<Vector2> motion=new List<Vector2>();readonly List<int> triangles=new List<int>();
            public void Triangle(Vector3 a,Vector3 b,Vector3 c,Color color,Vector2 uv=default)
            {int n=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);for(int i=0;i<3;i++){colors.Add(color);motion.Add(uv);triangles.Add(n+i);}}
            public void Diamond(Vector3 p,float length,float width,float angle,Color c,Vector2 uv=default)
            {var a=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*length;var b=new Vector3(-Mathf.Sin(angle),0,Mathf.Cos(angle))*width;Triangle(p-a,p+b+Vector3.up*.005f,p+a,c,uv);Triangle(p-a,p+a,p-b,c,uv);}
            public Mesh Mesh(string name)
            {var m=new Mesh{name=name};m.SetVertices(vertices);m.SetColors(colors);m.SetUVs(1,motion);m.SetTriangles(triangles,0);m.RecalculateNormals();m.RecalculateBounds();var bounds=m.bounds;bounds.Expand(new Vector3(.34f,.10f,.34f));m.bounds=bounds;return m;}
        }
    }
}
