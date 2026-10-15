using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MagicIsland.CameraSystem;
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
    public static class VillageArtSetup
    {
        public const string MaterialFolder="Assets/Art/Materials/Village";
        public const string MeshFolder="Assets/Art/Meshes/Village";
        public const string PathsPath="Assets/Art/Prefabs/Environment/IslandPaths.prefab";
        public const int Version=2;
        private static Material plaster,wood,trim,clay,clayDark,sage,glass,stone,gold,paper,leaf,flower,path;

        public static void Ensure(AddressableAssetSettings settings)
        {
            Directory.CreateDirectory(MaterialFolder);Directory.CreateDirectory(MeshFolder);AssetDatabase.Refresh();
            plaster=Mat("Warm Plaster",new Color(.94f,.87f,.70f));wood=Mat("Chestnut Timber",new Color(.47f,.31f,.19f));
            trim=Mat("Ivory Trim",new Color(.98f,.93f,.80f));clay=Mat("Terracotta Tiles",new Color(.76f,.42f,.29f));
            clayDark=Mat("Roof Underlay",new Color(.43f,.27f,.21f));sage=Mat("Sage Paint",new Color(.39f,.61f,.46f));
            glass=Mat("Sea Glass",new Color(.48f,.73f,.74f));stone=Mat("Limestone Plinth",new Color(.70f,.72f,.61f));
            gold=Mat("Brass Details",new Color(.87f,.69f,.37f));paper=Mat("Notice Paper",new Color(1,.96f,.81f));
            leaf=Mat("Planter Leaves",new Color(.42f,.65f,.30f));flower=Mat("Planter Flowers",new Color(.94f,.64f,.45f));
            path=Mat("Village Sand Path",new Color(.84f,.75f,.53f));
            UpgradeHouse();UpgradeHall();UpgradeExpansion();EnsurePaths(settings);
        }
        private static Material Mat(string name,Color color)
        {
            string target=MaterialFolder+"/"+name+".mat";var value=AssetDatabase.LoadAssetAtPath<Material>(target);if(value!=null)return value;
            value=new Material(Shader.Find("Standard")){name=name,color=color};value.SetFloat("_Glossiness",name=="Sea Glass"?.28f:.08f);AssetDatabase.CreateAsset(value,target);return value;
        }
        private static Mesh MeshAsset(string name,Func<Mesh> create)
        {
            string target=MeshFolder+"/"+name+".asset";var value=AssetDatabase.LoadAssetAtPath<Mesh>(target);if(value!=null)return value;
            value=create();value.name=name;AssetDatabase.CreateAsset(value,target);return value;
        }
        private static GameObject Part(Transform parent,string name,PrimitiveType primitive,Vector3 position,Vector3 size,Material material)
        {
            var go=GameObject.CreatePrimitive(primitive);go.name=name;go.layer=2;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=size;
            go.GetComponent<Renderer>().sharedMaterial=material;Object.DestroyImmediate(go.GetComponent<Collider>());return go;
        }
        private static GameObject Box(Transform p,string name,Vector3 position,Vector3 size,Material mat)=>Part(p,name,PrimitiveType.Cube,position,size,mat);
        private static GameObject MeshPart(Transform p,string name,Mesh mesh,Material mat,Vector3 position,Vector3 scale)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.layer=2;go.transform.SetParent(p,false);go.transform.localPosition=position;go.transform.localScale=scale;
            go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=mat;return go;
        }
        private static Transform Details(Transform root)
        {
            var previous=root.Find("Village details");if(previous!=null)Object.DestroyImmediate(previous.gameObject);
            var go=new GameObject("Village details");go.layer=2;go.transform.SetParent(root,false);return go.transform;
        }
        private static void Paint(Transform root,string name,Material material)
        {var t=root.Find(name);if(t!=null&&t.GetComponent<Renderer>()!=null)t.GetComponent<Renderer>().sharedMaterial=material;}
        private static bool Needs(GameObject root)=>root.GetComponent<VillageBuildingVisual>()==null||root.GetComponent<VillageBuildingVisual>().ArtVersion<Version;
        private static void Finish(GameObject root,string target)
        {
            var marker=root.GetComponent<VillageBuildingVisual>()??root.AddComponent<VillageBuildingVisual>();marker.ArtVersion=Version;
            var occluder=root.GetComponent<CameraOccluder>()??root.AddComponent<CameraOccluder>();
            occluder.Renderers=root.GetComponentsInChildren<Renderer>(true).Where(r=>r.sharedMaterial!=null&&r.sharedMaterial.shader.name=="Standard"
                &&r.GetComponentInParent<OrderBoard>()==null&&r.name!="Foundation"&&r.name!="Step"&&r.name!="Door Path"&&r.name!="Cottage Step").ToArray();
            PrefabUtility.SaveAsPrefabAsset(root,target);
        }
        private static void Roof(Transform details,string name,Vector3 center,float roll,float width,float depth,bool small)
        {
            var go=MeshPart(details,name,MeshAsset(small?"Cottage Roof Tiles":"Hall Roof Tiles",()=>TilesMesh(small?4:7,small?8:12)),clay,center,new Vector3(width,1,depth));
            go.transform.localRotation=Quaternion.Euler(0,0,roll);
            var rail=Box(details,name+" front fascia",center+Quaternion.Euler(0,0,roll)*new Vector3(0,0,-depth*.5f),new Vector3(width,.11f,.10f),trim);
            rail.transform.localRotation=Quaternion.Euler(0,0,roll);
        }
        private static void Planter(Transform parent,Vector3 point,float width)
        {
            Box(parent,"Window flower box",point,new Vector3(width,.18f,.22f),wood);
            for(int i=0;i<4;i++)
            {
                var p=point+new Vector3((i-1.5f)*width/4,.14f,0);
                Part(parent,"Planter foliage",PrimitiveType.Sphere,p,new Vector3(width*.27f,.19f,.25f),leaf);
                Part(parent,"Blossom",PrimitiveType.Sphere,p+new Vector3(.02f,.10f,-.025f),new Vector3(.07f,.045f,.07f),i%2==0?flower:paper);
            }
        }
        private static void Lantern(Transform parent,Vector3 point,float scale)
        {
            Box(parent,"Lantern bracket",point+new Vector3(0,.20f,.06f)*scale,new Vector3(.05f,.16f,.22f)*scale,wood);
            Box(parent,"Lantern warm glass",point,new Vector3(.15f,.22f,.13f)*scale,gold);
            Box(parent,"Lantern cap",point+Vector3.up*.14f*scale,new Vector3(.21f,.06f,.18f)*scale,wood);
            Box(parent,"Lantern foot",point-Vector3.up*.14f*scale,new Vector3(.19f,.045f,.16f)*scale,wood);
        }
        private static void UpgradeHouse()
        {
            const string target="Assets/Art/Prefabs/Buildings/PlayerHouse.prefab";var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(target);if(!Needs(prefab))return;
            var root=PrefabUtility.LoadPrefabContents(target);
            try
            {
                var d=Details(root.transform);Paint(root.transform,"Cottage Walls",plaster);Paint(root.transform,"Roof Left",clayDark);Paint(root.transform,"Roof Right",clayDark);
                Paint(root.transform,"Door Frame",wood);Paint(root.transform,"Sage Door",sage);Paint(root.transform,"Door Window",glass);Paint(root.transform,"Door Handle",gold);
                Paint(root.transform,"Side Window",glass);Paint(root.transform,"Chimney",stone);
                MeshPart(d,"Cottage gable",MeshAsset("Cottage Gable",()=>GableMesh(.64f,.48f,1)),plaster,new Vector3(0,2.20f,0),Vector3.one);
                Roof(d,"Cottage left tiles",new Vector3(-.38f,2.57f,0),27,.93f,2.30f,true);
                Roof(d,"Cottage right tiles",new Vector3(.38f,2.57f,0),-27,.93f,2.30f,true);
                Box(d,"Cottage ridge cap",new Vector3(0,2.80f,0),new Vector3(.12f,.08f,2.38f),clay);
                Box(d,"Cottage foundation",new Vector3(0,.07f,0),new Vector3(1.32f,.14f,2.02f),stone);
                Box(d,"Cottage Step",new Vector3(0,.025f,-1.34f),new Vector3(.9f,.05f,.55f),stone);
                for(int side=-1;side<=1;side+=2)
                {
                    Box(d,"Corner timber "+side,new Vector3(side*.59f,1.18f,-1.013f),new Vector3(.07f,2.02f,.05f),wood);
                    Box(d,"Door trim "+side,new Vector3(side*.38f,.96f,-1.074f),new Vector3(.055f,1.78f,.035f),trim);
                }
                Box(d,"Door lintel",new Vector3(0,1.88f,-1.074f),new Vector3(.82f,.07f,.035f),trim);
                Box(d,"Door window mullion",new Vector3(0,1.26f,-1.086f),new Vector3(.035f,.42f,.035f),trim);
                Box(d,"Door window rail",new Vector3(0,1.26f,-1.086f),new Vector3(.41f,.035f,.035f),trim);
                Box(d,"Side window sill",new Vector3(-.69f,.99f,.28f),new Vector3(.09f,.075f,.80f),trim);
                for(int z=-1;z<=1;z+=2)Box(d,"Side window trim",new Vector3(-.681f,1.35f,.28f+z*.39f),new Vector3(.07f,.76f,.05f),trim);
                Box(d,"Side window mullion",new Vector3(-.682f,1.35f,.28f),new Vector3(.04f,.64f,.04f),trim);
                Box(d,"Chimney cap",new Vector3(.35f,2.96f,.6f),new Vector3(.34f,.10f,.40f),stone);
                for(int i=0;i<3;i++)Box(d,"Chimney brick line",new Vector3(.35f,2.45f+i*.17f,.43f),new Vector3(.27f,.025f,.025f),trim);
                Lantern(d,new Vector3(.54f,1.65f,-1.13f),.70f);
                // Low details are passable and fit the old collision footprint.
                Planter(d,new Vector3(.53f,.20f,-1.12f),.28f);
                var portal=root.GetComponent<HomePortal>();portal.NameText.transform.localPosition=new Vector3(0,3.12f,0);
                Finish(root,target);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        private static void UpgradeHall()
        {
            const string target="Assets/Art/Prefabs/Buildings/OrderHall.prefab";var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(target);if(!Needs(prefab))return;
            var root=PrefabUtility.LoadPrefabContents(target);
            try
            {
                var d=Details(root.transform);Paint(root.transform,"Walls",plaster);Paint(root.transform,"Gable",plaster);
                Paint(root.transform,"Foundation",stone);Paint(root.transform,"Step",stone);Paint(root.transform,"Door Path",stone);
                Paint(root.transform,"Left Roof",clayDark);Paint(root.transform,"Right Roof",clayDark);
                Roof(d,"Hall left tiles",new Vector3(-.91f,2.84f,0),30,2.25f,3.25f,false);
                Roof(d,"Hall right tiles",new Vector3(.91f,2.84f,0),-30,2.25f,3.25f,false);
                Box(d,"Hall ridge cap",new Vector3(0,3.42f,0),new Vector3(.16f,.09f,3.35f),clay);
                foreach(string name in new[]{"Door Frame","Front Window Frame","Side Window Frame","Hall Sign"})Paint(root.transform,name,wood);
                Paint(root.transform,"Door",sage);Paint(root.transform,"Handle",gold);
                Paint(root.transform,"Front Window",glass);Paint(root.transform,"Side Window",glass);
                Paint(root.transform,"Window Cross",trim);Paint(root.transform,"Window Crossbar",trim);
                for(int side=-1;side<=1;side+=2)
                {
                    Box(d,"Hall corner timber",new Vector3(side*1.62f,1.2f,-1.325f),new Vector3(.09f,1.9f,.07f),wood);
                    Box(d,"Hall shutter",new Vector3(.94f+side*.61f,1.34f,-1.48f),new Vector3(.23f,.93f,.06f),sage);
                    for(int i=0;i<4;i++)Box(d,"Shutter louvre",new Vector3(.94f+side*.61f,1.04f+i*.19f,-1.525f),new Vector3(.21f,.032f,.028f),trim);
                }
                Box(d,"Hall front beam",new Vector3(0,2.07f,-1.33f),new Vector3(3.3f,.10f,.08f),wood);
                Planter(d,new Vector3(.94f,.78f,-1.53f),1.02f);
                Lantern(d,new Vector3(-1.26f,1.56f,-1.49f),1);
                Box(d,"Hall door glazing",new Vector3(-.55f,1.23f,-1.506f),new Vector3(.42f,.46f,.027f),glass);
                Box(d,"Hall door glazing rail",new Vector3(-.55f,1.23f,-1.525f),new Vector3(.44f,.035f,.022f),trim);
                Box(d,"Hall door glazing mullion",new Vector3(-.55f,1.23f,-1.525f),new Vector3(.035f,.46f,.022f),trim);
                var board=root.GetComponentInChildren<OrderBoard>().transform;
                foreach(string name in new[]{"Left Post","Right Post","Board Frame"})Paint(board,name,wood);
                Paint(board,"Board Surface",sage);Paint(board,"Board Canopy",clayDark);
                var oldBoardDetails=board.Find("Board details");if(oldBoardDetails!=null)Object.DestroyImmediate(oldBoardDetails.gameObject);
                var bd=new GameObject("Board details");bd.layer=2;bd.transform.SetParent(board,false);
                MeshPart(bd.transform,"Noticeboard canopy tiles",MeshAsset("Cottage Roof Tiles",()=>TilesMesh(4,8)),clay,new Vector3(0,1.99f,0),new Vector3(1.85f,1,.57f));
                for(int i=0;i<3;i++)
                {
                    Paint(board,"Order Paper "+i,paper);Paint(board,"Pin "+i,gold);float x=(i-1)*.43f;
                    Part(bd.transform,"Round brass pin",PrimitiveType.Sphere,new Vector3(x,1.48f,-.218f),Vector3.one*.055f,gold);
                }
                Box(bd.transform,"Board header trim",new Vector3(0,1.81f,-.11f),new Vector3(1.65f,.065f,.055f),trim);
                Box(bd.transform,"Board foot trim",new Vector3(0,.78f,-.10f),new Vector3(1.65f,.055f,.05f),trim);
                Finish(root,target);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        private static void UpgradeExpansion()
        {
            const string target="Assets/Art/Prefabs/Buildings/PlayerHouseExpansion.prefab";var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(target);if(!Needs(prefab))return;
            var root=PrefabUtility.LoadPrefabContents(target);
            try
            {
                foreach(var r in root.GetComponentsInChildren<Renderer>(true))r.sharedMaterial=r.name.Contains("Shutter")?sage:trim;
                var d=Details(root.transform);
                Box(d,"Dormer facade",new Vector3(0,3.03f,-.58f),new Vector3(.40f,.50f,.31f),plaster);
                Box(d,"Dormer window",new Vector3(0,3.05f,-.748f),new Vector3(.27f,.29f,.025f),glass);
                Box(d,"Dormer mullion",new Vector3(0,3.05f,-.77f),new Vector3(.03f,.29f,.018f),trim);
                Box(d,"Dormer window sill",new Vector3(0,2.88f,-.75f),new Vector3(.40f,.05f,.045f),trim);
                Roof(d,"Dormer left roof",new Vector3(-.13f,3.33f,-.58f),27,.34f,.50f,true);
                Roof(d,"Dormer right roof",new Vector3(.13f,3.33f,-.58f),-27,.34f,.50f,true);
                Box(d,"Dormer ridge",new Vector3(0,3.43f,-.58f),new Vector3(.08f,.06f,.55f),clay);
                Planter(d,new Vector3(.74f,.30f,-1.13f),.44f);
                Finish(root,target);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        private static void EnsurePaths(AddressableAssetSettings settings)
        {
            var housing=AssetDatabase.LoadAssetAtPath<HousingConfig>("Assets/Config/HousingConfig.asset");var config=AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/Config/GameConfig.asset");
            if(!File.Exists(PathsPath))
            {
                var root=new GameObject("IslandPaths",typeof(VillagePaths));root.layer=2;
                try
                {
                    var model=root.GetComponent<VillagePaths>();var strip=MeshAsset("Village Path Strip",PathMesh);var junction=MeshAsset("Village Path Junction",DiscMesh);
                    for(int i=0;i<8;i++){var p=MeshPart(root.transform,"Lane "+i,strip,path,Vector3.zero,Vector3.one);p.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;model.Segments[i]=p.transform;}
                    for(int i=0;i<4;i++){var p=MeshPart(root.transform,"Junction "+i,junction,path,Vector3.zero,Vector3.one);p.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;model.Junctions[i]=p.transform;}
                    model.Configure(config.FarmSize,housing);PrefabUtility.SaveAsPrefabAsset(root,PathsPath);
                }
                finally{Object.DestroyImmediate(root);}
            }
            var id=AssetDatabase.AssetPathToGUID(PathsPath);var entry=settings.FindAssetEntry(id)??settings.CreateOrMoveEntry(id,settings.FindGroup("Environment"));if(entry.address!="IslandPaths")entry.SetAddress("IslandPaths");
            var terrainPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(TerrainSetup.PrefabPath);if(terrainPrefab.GetComponent<IslandTerrain>().Paths!=null)return;
            var terrain=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);
            try
            {
                var paths=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PathsPath));paths.transform.SetParent(terrain.transform,false);
                terrain.GetComponent<IslandTerrain>().Paths=paths.GetComponent<VillagePaths>();PrefabUtility.SaveAsPrefabAsset(terrain,TerrainSetup.PrefabPath);
            }
            finally{PrefabUtility.UnloadPrefabContents(terrain);}
        }
        private static Mesh Build(List<Vector3> vertices,List<int> indices)
        {var m=new Mesh();m.SetVertices(vertices);m.SetTriangles(indices,0);m.RecalculateNormals();m.RecalculateBounds();return m;}
        private static Mesh TilesMesh(int across,int rows)
        {
            var vertices=new List<Vector3>();var triangles=new List<int>();
            for(int x=0;x<across;x++)for(int z=0;z<rows;z++)
            {
                float left=-.5f+x/(float)across+.006f,right=-.5f+(x+1f)/across-.006f;
                float front=-.5f+z/(float)rows+.004f,back=-.5f+(z+1f)/rows-.004f,y=.01f+(z%2)*.004f;
                int start=vertices.Count;
                vertices.AddRange(new[]{new Vector3(left,y,front),new Vector3(left,y,back),new Vector3(right,y,back),new Vector3(right,y,front),new Vector3(left,y+.025f,front),new Vector3(left,y+.025f,back),new Vector3(right,y+.025f,back),new Vector3(right,y+.025f,front)});
                foreach(int i in new[]{4,5,6,4,6,7,0,4,7,0,7,3,3,7,6,3,6,2,2,6,5,2,5,1,1,5,4,1,4,0})triangles.Add(start+i);
            }
            return Build(vertices,triangles);
        }
        private static Mesh GableMesh(float half,float height,float depth)
        {
            var v=new List<Vector3>{new Vector3(-half,0,-depth),new Vector3(0,height,-depth),new Vector3(half,0,-depth),new Vector3(-half,0,depth),new Vector3(0,height,depth),new Vector3(half,0,depth)};
            return Build(v,new List<int>{0,1,2,5,4,3,0,3,4,0,4,1,1,4,5,1,5,2});
        }
        private static Mesh PathMesh()
        {
            var v=new List<Vector3>();var t=new List<int>();const int n=24;
            for(int i=0;i<=n;i++)
            {
                float z=i/(float)n-.5f,w=.5f+.017f*Mathf.Sin(i*2.1f);
                v.Add(new Vector3(-w,0,z));v.Add(new Vector3(w,0,z));
            }
            for(int i=0;i<n;i++){int a=i*2;t.AddRange(new[]{a,a+2,a+1,a+1,a+2,a+3});}return Build(v,t);
        }
        private static Mesh DiscMesh()
        {
            var v=new List<Vector3>{Vector3.zero};var t=new List<int>();const int n=40;
            for(int i=0;i<n;i++){float a=i*Mathf.PI*2/n;v.Add(new Vector3(Mathf.Cos(a)*.56f,0,Mathf.Sin(a)*.56f));}
            for(int i=0;i<n;i++)t.AddRange(new[]{0,1+(i+1)%n,1+i});return Build(v,t);
        }
    }
}
