using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MagicIsland.CameraSystem;
using MagicIsland.Configuration;
using MagicIsland.Interaction;
using MagicIsland.TerrainSystem;
using TMPro;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using Object=UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    public static class FacilityArtSetup
    {
        public const string ShopPath="Assets/Art/Prefabs/Buildings/IslandShop.prefab";
        public const string MaterialFolder="Assets/Art/Materials/Facilities",MeshFolder="Assets/Art/Meshes/Facilities";
        public const int Version=3;
        static Material wood,trim,sage,stone,gold,steel,cloth,cream,leaf,red,seed,paper;
        public static void Ensure(AddressableAssetSettings settings,GameConfig config)
        {
            Directory.CreateDirectory(MaterialFolder);Directory.CreateDirectory(MeshFolder);AssetDatabase.Refresh();
            wood=Village("Chestnut Timber");trim=Village("Ivory Trim");sage=Village("Sage Paint");stone=Village("Limestone Plinth");gold=Village("Brass Details");paper=Village("Notice Paper");
            steel=Mat("Kitchen Steel",new Color(.67f,.73f,.69f));cloth=Mat("Awning Sage",new Color(.38f,.60f,.48f));cream=Mat("Awning Cream",new Color(.97f,.90f,.70f));
            leaf=Mat("Produce Leaves",new Color(.34f,.62f,.29f));red=Mat("Market Produce",new Color(.91f,.48f,.31f));seed=Mat("Seed Sacks",new Color(.79f,.66f,.43f));
            CreateShop();Upgrade("ToolWorkbench",Bench);Upgrade("CookingStation",Kitchen);Upgrade("StorageChest",Chest);
            var id=AssetDatabase.AssetPathToGUID(ShopPath);var entry=settings.FindAssetEntry(id)??settings.CreateOrMoveEntry(id,settings.FindGroup("Buildings"));if(entry.address!="IslandShop")entry.SetAddress("IslandShop");
            if(!File.ReadAllText("Assets/Config/GameConfig.asset").Contains("ShopPrefabAddress:"))EditorUtility.SetDirty(config);
        }
        static Material Village(string name)=>AssetDatabase.LoadAssetAtPath<Material>(VillageArtSetup.MaterialFolder+"/"+name+".mat");
        static Material Mat(string name,Color color)
        {
            string path=MaterialFolder+"/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);if(mat!=null)return mat;
            mat=new Material(Shader.Find("Standard")){name=name,color=color};mat.SetFloat("_Glossiness",name=="Kitchen Steel"?.23f:.06f);if(name=="Kitchen Steel")mat.SetFloat("_Metallic",.25f);AssetDatabase.CreateAsset(mat,path);return mat;
        }
        static GameObject Part(Transform parent,string name,PrimitiveType type,Vector3 point,Vector3 size,Material mat)
        {
            var p=GameObject.CreatePrimitive(type);p.name=name;p.layer=2;p.transform.SetParent(parent,false);p.transform.localPosition=point;p.transform.localScale=size;p.GetComponent<Renderer>().sharedMaterial=mat;Object.DestroyImmediate(p.GetComponent<Collider>());return p;
        }
        static GameObject Box(Transform p,string name,Vector3 point,Vector3 size,Material m)=>Part(p,name,PrimitiveType.Cube,point,size,m);
        static void Paint(Transform root,string name,Material mat)
        {var p=root.Find(name);if(p!=null&&p.GetComponent<Renderer>()!=null)p.GetComponent<Renderer>().sharedMaterial=mat;}
        static void Stamp(GameObject root)
        {
            var marker=root.GetComponent<FacilityVisual>()??root.AddComponent<FacilityVisual>();marker.ArtVersion=Version;
            var fade=root.GetComponent<CameraOccluder>()??root.AddComponent<CameraOccluder>();fade.Renderers=root.GetComponentsInChildren<Renderer>(true).Where(r=>r.sharedMaterials.Any(m=>m!=null&&m.shader.name=="Standard")&&!r.name.Contains("Name Plate")&&!r.name.Contains("Sign board")).ToArray();
        }
        static void Label(Transform parent,string name,string content,Vector3 point,Vector2 size,float fontSize)
        {
            var root=new GameObject(name,typeof(TextMeshPro));root.layer=2;root.transform.SetParent(parent,false);root.transform.localPosition=point;
            var text=root.GetComponent<TextMeshPro>();text.font=UiPrefabMigration.FontAsset;text.text=content;text.fontSize=fontSize;text.rectTransform.sizeDelta=size;text.alignment=TextAlignmentOptions.Center;text.color=new Color(.25f,.31f,.23f);
        }
        static void CreateShop()
        {
            if(File.Exists(ShopPath))
            {
                if(AssetDatabase.LoadAssetAtPath<GameObject>(ShopPath).GetComponent<FacilityVisual>()?.ArtVersion>=Version)return;
                var existing=PrefabUtility.LoadPrefabContents(ShopPath);
                try{if(existing.GetComponent<FacilityVisual>()?.ArtVersion<3)MoveMarketDisplay(existing.transform);Stamp(existing);PrefabUtility.SaveAsPrefabAsset(existing,ShopPath);}finally{PrefabUtility.UnloadPrefabContents(existing);}return;
            }
            var root=new GameObject("IslandShop",typeof(ShopCounter),typeof(BoxCollider));
            try
            {
                var c=root.GetComponent<BoxCollider>();c.size=new Vector3(1.4f,1.2f,1);
                Box(root.transform,"Counter timber",Vector3.zero,new Vector3(1.4f,1.2f,1),wood);
                Box(root.transform,"Countertop",new Vector3(0,.625f,0),new Vector3(1.47f,.07f,1.05f),trim);
                for(int i=0;i<7;i++)Box(root.transform,"Front timber slat",new Vector3(-.60f+i*.20f,-.02f,-.505f),new Vector3(.18f,1.02f,.035f),i%2==0?wood:seed);
                for(int side=-1;side<=1;side+=2)
                {
                    Box(root.transform,"Canopy post",new Vector3(side*.63f,.61f,.37f),new Vector3(.085f,1.32f,.09f),wood);
                    Box(root.transform,"Counter brass corner",new Vector3(side*.65f,0,-.535f),new Vector3(.065f,1.1f,.035f),gold);
                }
                var roof=new GameObject("Striped canvas",typeof(MeshFilter),typeof(MeshRenderer));roof.layer=2;roof.transform.SetParent(root.transform,false);roof.transform.localPosition=new Vector3(0,1.29f,0);
                const string meshPath=MeshFolder+"/Shop Awning.asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);if(mesh==null){mesh=Awning();mesh.name="Shop Awning";AssetDatabase.CreateAsset(mesh,meshPath);}
                roof.GetComponent<MeshFilter>().sharedMesh=mesh;roof.GetComponent<Renderer>().sharedMaterials=new[]{cloth,cream};
                for(int i=0;i<10;i++)Box(root.transform,"Canvas valance",new Vector3(-.81f+i*.18f,1.23f,-.72f),new Vector3(.175f,.14f,.025f),i%2==0?cloth:cream);
                Box(root.transform,"Rear shelf",new Vector3(0,.94f,.39f),new Vector3(1.25f,.065f,.18f),wood);
                for(int i=0;i<3;i++)
                {
                    var p=new Vector3(-.43f+i*.25f,.73f,.25f);Part(root.transform,"Seed sack",PrimitiveType.Capsule,p,new Vector3(.20f,.19f,.16f),seed);
                    Box(root.transform,"Seed sack tag",p+new Vector3(0,.04f,-.085f),new Vector3(.08f,.08f,.015f),paper);
                }
                Box(root.transform,"Produce tray",new Vector3(.30f,.70f,-.12f),new Vector3(.54f,.10f,.42f),wood);
                for(int i=0;i<6;i++)
                {
                    var p=new Vector3(.15f+(i%3)*.14f,.80f,-.22f+(i/3)*.17f);Part(root.transform,"Fresh produce",PrimitiveType.Sphere,p,Vector3.one*.14f,i%2==0?red:cream);
                    Part(root.transform,"Produce leaf",PrimitiveType.Sphere,p+Vector3.up*.075f,new Vector3(.08f,.025f,.04f),leaf);
                }
                Box(root.transform,"Sign board",new Vector3(0,.09f,-.55f),new Vector3(.90f,.27f,.05f),paper);
                Label(root.transform,"Shop name","岛屿商店",new Vector3(0,.09f,-.58f),new Vector2(.82f,.23f),1.2f);
                MoveMarketDisplay(root.transform);Stamp(root);PrefabUtility.SaveAsPrefabAsset(root,ShopPath);
            }
            finally{Object.DestroyImmediate(root);}
        }
        static void MoveMarketDisplay(Transform root)
        {
            foreach(Transform part in root)
            {
                float offset=part.name=="Seed sack"||part.name=="Seed sack tag"?-.70f:
                    part.name=="Produce tray"||part.name=="Fresh produce"||part.name=="Produce leaf"?-.40f:0;
                if(offset!=0)part.localPosition+=Vector3.forward*offset;
            }
        }
        static void Upgrade(string name,Action<Transform,Transform> decorate)
        {
            string path="Assets/Art/Prefabs/Buildings/"+name+".prefab";var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(prefab.GetComponent<FacilityVisual>()?.ArtVersion>=Version)return;
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var old=root.transform.Find("Facility details");if(old!=null)Object.DestroyImmediate(old.gameObject);
                var group=new GameObject("Facility details");group.layer=2;group.transform.SetParent(root.transform,false);
                decorate(root.transform,group.transform);Stamp(root);PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        static void Bench(Transform root,Transform d)
        {
            Paint(root,"Top",wood);Paint(root,"Shelf",wood);Paint(root,"Tool Rack",sage);Paint(root,"Plans",paper);
            foreach(var r in root.GetComponentsInChildren<Renderer>())if(r.name=="Leg")r.sharedMaterial=wood;
            for(int i=0;i<8;i++)Box(d,"Workbench plank line",new Vector3(-.7f+i*.2f,.909f,0),new Vector3(.018f,.006f,.78f),seed);
            Box(d,"Tool rack frame",new Vector3(0,1.48f,.355f),new Vector3(1.56f,.07f,.08f),wood);
            for(int side=-1;side<=1;side+=2)Box(d,"Rack side frame",new Vector3(side*.75f,1.20f,.35f),new Vector3(.055f,.64f,.065f),wood);
            for(int i=0;i<5;i++)Part(d,"Tool peg",PrimitiveType.Cylinder,new Vector3(-.60f+i*.30f,1.38f,.245f),new Vector3(.035f,.065f,.035f),gold).transform.localRotation=Quaternion.Euler(90,0,0);
            Part(d,"Material spool",PrimitiveType.Cylinder,new Vector3(-.44f,.97f,-.12f),new Vector3(.17f,.06f,.17f),seed);
            Box(d,"Hammer handle",new Vector3(-.10f,.94f,-.13f),new Vector3(.05f,.045f,.32f),wood);
            Box(d,"Hammer head",new Vector3(-.10f,.97f,.025f),new Vector3(.23f,.09f,.09f),steel);
            for(int i=0;i<3;i++)Box(d,"Stored timber",new Vector3(-.40f,.32f+i*.07f,.02f),new Vector3(.52f,.055f,.18f),seed);
            Part(d,"Stone supply",PrimitiveType.Sphere,new Vector3(.42f,.37f,.05f),new Vector3(.28f,.20f,.22f),stone);
            Box(d,"Workbench Name Plate",new Vector3(0,.69f,-.435f),new Vector3(1.30f,.23f,.035f),paper);
            var text=root.Find("Workbench Name").GetComponent<TMP_Text>();text.color=new Color(.25f,.31f,.23f);text.transform.localPosition=new Vector3(0,.69f,-.46f);
        }
        static void Kitchen(Transform root,Transform d)
        {
            Paint(root,"Cabinet",sage);Paint(root,"Counter",trim);Paint(root,"Oven Frame",steel);Paint(root,"Oven Door",wood);Paint(root,"Handle",gold);Paint(root,"Chopping Board",wood);Paint(root,"Soup Pot",steel);Paint(root,"Pot Lid",steel);
            Box(d,"Kitchen toe rail",new Vector3(0,.10f,-.44f),new Vector3(1.55f,.08f,.055f),wood);
            Box(d,"Cupboard divider",new Vector3(.08f,.43f,-.44f),new Vector3(.035f,.62f,.055f),trim);
            for(int i=0;i<2;i++)Box(d,"Brass cabinet handle",new Vector3(.22f+i*.37f,.60f,-.46f),new Vector3(.16f,.035f,.055f),gold);
            Box(d,"Splashback",new Vector3(0,1.12f,.38f),new Vector3(1.50f,.42f,.08f),sage);
            Box(d,"Utensil rail",new Vector3(0,1.30f,.31f),new Vector3(1.32f,.035f,.045f),wood);
            for(int i=0;i<3;i++)
            {
                Part(d,"Hanging utensil",PrimitiveType.Cylinder,new Vector3(-.5f+i*.27f,1.18f,.27f),new Vector3(.018f,.09f,.018f),wood);
                Part(d,"Utensil head",PrimitiveType.Sphere,new Vector3(-.5f+i*.27f,1.08f,.27f),new Vector3(.07f,.10f,.025f),steel);
            }
            Part(d,"Pot handle knob",PrimitiveType.Sphere,new Vector3(-.4f,1.225f,0),Vector3.one*.075f,wood);
            for(int side=-1;side<=1;side+=2)Box(d,"Pot side handle",new Vector3(-.4f+side*.25f,1.06f,0),new Vector3(.11f,.045f,.07f),wood);
            Box(d,"Kitchen knife blade",new Vector3(.48f,.966f,-.06f),new Vector3(.065f,.012f,.25f),steel);
            Box(d,"Kitchen knife handle",new Vector3(.48f,.977f,-.245f),new Vector3(.05f,.03f,.14f),wood);
            Part(d,"Prepared vegetable",PrimitiveType.Sphere,new Vector3(.31f,1.03f,-.10f),Vector3.one*.13f,red);
            Box(d,"Kitchen Name Plate",new Vector3(.44f,.35f,-.463f),new Vector3(.68f,.20f,.025f),paper);
            var text=root.Find("Station Label");if(text!=null){text.localPosition=new Vector3(.44f,.35f,-.484f);text.GetComponent<TMP_Text>().color=new Color(.25f,.31f,.23f);}
        }
        static void Chest(Transform root,Transform d)
        {
            Paint(root,"Body",wood);Paint(root,"Lid",sage);Paint(root,"Latch",gold);Paint(root,"Name Plate",paper);
            foreach(var r in root.GetComponentsInChildren<Renderer>())if(r.name.Contains("Band"))r.sharedMaterial=gold;
            for(int i=0;i<6;i++)Box(d,"Chest plank seam",new Vector3(-.54f+i*.215f,.35f,-.434f),new Vector3(.018f,.56f,.015f),seed);
            for(int side=-1;side<=1;side+=2)
            {
                Box(d,"Lid end trim",new Vector3(side*.67f,.78f,0),new Vector3(.07f,.04f,.90f),wood);
                Box(d,"Chest side handle",new Vector3(side*.667f,.47f,0),new Vector3(.035f,.06f,.23f),gold);
                for(int i=0;i<3;i++)Part(d,"Band rivet",PrimitiveType.Sphere,new Vector3(side*.44f,.16f+i*.19f,-.45f),Vector3.one*.035f,gold);
            }
            Box(d,"Latch keyhole",new Vector3(0,.58f,-.488f),new Vector3(.035f,.060f,.014f),wood);
        }
        static Mesh Awning()
        {
            const int columns=10,rows=6;var v=new List<Vector3>();var a=new List<int>();var b=new List<int>();
            for(int z=0;z<=rows;z++)for(int x=0;x<=columns;x++)
            {float px=-.9f+1.8f*x/columns,pz=-.7f+1.4f*z/rows;v.Add(new Vector3(px,.16f*(1-pz*pz/.49f),pz));}
            for(int z=0;z<rows;z++)for(int x=0;x<columns;x++)
            {int i=z*(columns+1)+x;var t=x%2==0?a:b;t.AddRange(new[]{i,i+columns+1,i+1,i+1,i+columns+1,i+columns+2});}
            var mesh=new Mesh();mesh.SetVertices(v);mesh.subMeshCount=2;mesh.SetTriangles(a,0);mesh.SetTriangles(b,1);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
    }
}
