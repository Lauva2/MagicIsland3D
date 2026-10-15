using System;
using System.Collections.Generic;
using System.IO;
using MagicIsland.Configuration;
using MagicIsland.Interaction;
using MagicIsland.Inventory;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using MagicIsland.UI.Views;
using TMPro;
using Object=UnityEngine.Object;
using Factory=MagicIsland.EditorTools.PrototypeUiPrefabBuilder;

namespace MagicIsland.EditorTools
{
    public static class BeachForageSetup
    {
        public const string Folder="Assets/Art/Prefabs/Gathering",Meshes="Assets/Art/Meshes/Gathering/Beach";
        public const string MaterialPath="Assets/Art/Materials/Gathering/Beach Forage.mat";
        public static void Ensure(ItemCatalog items,AddressableAssetSettings settings)
        {
            Directory.CreateDirectory(Folder);Directory.CreateDirectory(Meshes);AssetDatabase.Refresh();
            AddItem(items,"beach_shell","扇贝壳",18,new Color(.92f,.62f,.60f),"潮水留在沙滩上的完整扇贝壳，可在商店出售。");
            AddItem(items,"driftwood","漂流木",8,new Color(.66f,.46f,.27f),"经过海水打磨的轻巧木段，可在商店出售。");
            AddItem(items,"sea_glass","海玻璃",25,new Color(.35f,.78f,.68f),"被海浪磨圆的彩色玻璃碎片，可在商店出售。");
            var material=AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if(material==null){material=new Material(Shader.Find("MagicIsland/Ocean Ecology")){name="Beach Forage"};AssetDatabase.CreateAsset(material,MaterialPath);}
            for(int i=0;i<BeachPickup.Items.Length;i++)
            {
                string name=BeachPickup.Address(BeachPickup.Items[i]),path=Folder+"/"+name+".prefab";
                if(!File.Exists(path))Build(name,i,material,path);
                var entry=settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(path))??settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path),settings.FindGroup("Gathering Models"));
                if(entry.address!=name)entry.SetAddress(name);
            }
            var catalog=AssetDatabase.LoadAssetAtPath<GatheringCatalog>("Assets/Config/GatheringCatalog.asset");
            if(catalog!=null&&!File.ReadAllText("Assets/Config/GatheringCatalog.asset").Contains("BeachSites:"))EditorUtility.SetDirty(catalog);
        }
        static void AddItem(ItemCatalog items,string id,string name,int price,Color color,string description)
        {
            if(items.Find(id)!=null)return;
            items.Items.Add(new ItemConfig{Id=id,DisplayName=name,Type=ItemType.Material,MaxStack=99,SellPrice=price,IconColor=color,Icon=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Textures/UI/RoundedPanel.png"),Description=description});EditorUtility.SetDirty(items);
        }
        public static void UpgradeShop()
        {
            string path=Factory.Folder+"/ShopPanel.prefab";
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(prefab.GetComponent<ShopView>().CropButtons.Length>=13)return;
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view=root.GetComponent<ShopView>();int old=view.CropButtons.Length;Array.Resize(ref view.CropButtons,13);
                var panel=view.BalanceText.transform.parent;
                for(int i=old;i<13;i++)view.CropButtons[i]=Factory.Button("Beach Offer "+i,panel,Vector2.zero,new Vector2(122,40),"海滩采集",Factory.Sand);
                for(int i=0;i<13;i++)
                {
                    var rect=view.CropButtons[i].GetComponent<RectTransform>();rect.anchoredPosition=new Vector2(32+i%5*130,-130-i/5*50);rect.sizeDelta=new Vector2(122,40);
                    var text=view.CropButtons[i].GetComponentInChildren<TMP_Text>();text.rectTransform.sizeDelta=rect.sizeDelta;text.fontSize=17;
                }
                panel.GetComponent<RectTransform>().sizeDelta=new Vector2(700,690);
                foreach(var target in new[]{view.BuyText.transform.parent,view.SellText.transform.parent,view.StockText.transform,view.FeedbackText.transform,view.AnimalShopButton.transform,view.InventoryUpgradeButton.transform})
                    target.GetComponent<RectTransform>().anchoredPosition+=new Vector2(0,-50);
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        static void Build(string name,int kind,Material material,string path)
        {
            var geometry=new Geometry();if(kind==0)Shell(geometry);else if(kind==1)Wood(geometry);else Glass(geometry);
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(Meshes+"/"+name+".asset");
            if(mesh==null){mesh=geometry.Mesh();mesh.name=name;AssetDatabase.CreateAsset(mesh,Meshes+"/"+name+".asset");}
            var root=new GameObject(name,typeof(BeachPickup),typeof(MeshFilter),typeof(MeshRenderer),typeof(BoxCollider));
            try
            {
                root.GetComponent<MeshFilter>().sharedMesh=mesh;root.GetComponent<MeshRenderer>().sharedMaterial=material;
                var target=root.GetComponent<BoxCollider>();target.center=new Vector3(0,.16f,0);target.size=new Vector3(.85f,.42f,.65f);target.isTrigger=true;
                root.GetComponent<BeachPickup>().Target=target;PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{Object.DestroyImmediate(root);}
        }
        public static void RebuildMeshes()
        {
            for(int i=0;i<BeachPickup.Items.Length;i++)
            {
                string name=BeachPickup.Address(BeachPickup.Items[i]),path=Meshes+"/"+name+".asset";
                var geometry=new Geometry();if(i==0)Shell(geometry);else if(i==1)Wood(geometry);else Glass(geometry);
                var mesh=geometry.Mesh();mesh.name=name;var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(existing==null)AssetDatabase.CreateAsset(mesh,path);
                else{EditorUtility.CopySerialized(mesh,existing);EditorUtility.SetDirty(existing);Object.DestroyImmediate(mesh);}
            }
            AssetDatabase.SaveAssets();Debug.Log("Beach pickup meshes rebuilt.");
        }
        sealed class Geometry
        {
            readonly List<Vector3> vertices=new List<Vector3>();readonly List<Color> colors=new List<Color>();
            public void Triangle(Vector3 a,Vector3 b,Vector3 c,Color tint){vertices.AddRange(new[]{a,b,c});tint.a=0;colors.AddRange(new[]{tint,tint,tint});}
            public void Tube(Vector3 a,Vector3 b,float radius,Color tint)
            {
                var direction=(b-a).normalized;var side=Vector3.Cross(direction,Vector3.up).normalized;if(side.sqrMagnitude<.1f)side=Vector3.right;var up=Vector3.Cross(direction,side);
                for(int i=0;i<10;i++){float x=i*Mathf.PI/5,y=(i+1)*Mathf.PI/5;var p=(side*Mathf.Cos(x)+up*Mathf.Sin(x))*radius;var q=(side*Mathf.Cos(y)+up*Mathf.Sin(y))*radius;Triangle(a+p,a+q,b+p,tint);Triangle(a+q,b+q,b+p,tint);Triangle(a,a+q,a+p,tint*1.25f);Triangle(b,b+p,b+q,tint*1.25f);}
            }
            public Mesh Mesh(){var indices=new int[vertices.Count];for(int i=0;i<indices.Length;i++)indices[i]=i;var result=new Mesh{vertices=vertices.ToArray(),triangles=indices,colors=colors.ToArray()};result.RecalculateNormals();result.RecalculateBounds();return result;}
        }
        static void Shell(Geometry g)
        {
            var heel=new Vector3(0,.035f,-.25f);Color cream=new Color(.97f,.84f,.74f),rose=new Color(.82f,.49f,.48f);
            for(int i=0;i<18;i++)
            {
                float a=Mathf.Lerp(-1.1f,1.1f,i/18f),b=Mathf.Lerp(-1.1f,1.1f,(i+1)/18f);
                var p=heel+new Vector3(Mathf.Sin(a)*.39f,.025f,Mathf.Cos(a)*.58f);var q=heel+new Vector3(Mathf.Sin(b)*.39f,.025f,Mathf.Cos(b)*.58f);
                var middle=(p+q)*.5f+Vector3.up*(i%2==0?.08f:.045f);g.Triangle(heel,p,middle,i%2==0?cream:rose);g.Triangle(heel,middle,q,cream*.95f);g.Triangle(heel,q,p,rose*.85f);
            }
        }
        static void Wood(Geometry g)
        {
            Color bark=new Color(.55f,.38f,.23f),light=new Color(.76f,.59f,.36f);
            g.Tube(new Vector3(-.38f,.13f,-.04f),new Vector3(.39f,.15f,.05f),.12f,bark);
            g.Tube(new Vector3(-.03f,.15f,.02f),new Vector3(.12f,.19f,.25f),.055f,light);
            for(int i=0;i<5;i++)g.Tube(new Vector3(-.35f,.22f,-.07f+i*.028f),new Vector3(.34f,.24f,-.02f+i*.023f),.008f,light*.82f);
        }
        static void Glass(Geometry g)
        {
            for(int j=0;j<3;j++)
            {
                var center=new Vector3((j-1)*.19f,.025f,j%2==0?.04f:-.14f);float radius=.16f-j*.02f;Color tint=j==0?new Color(.32f,.72f,.65f):j==1?new Color(.64f,.85f,.75f):new Color(.39f,.65f,.80f);
                var top=center+Vector3.up*(.14f-j*.02f);
                for(int i=0;i<6;i++){float a=i*Mathf.PI/3,b=(i+1)*Mathf.PI/3;var p=center+new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius);var q=center+new Vector3(Mathf.Cos(b)*radius,0,Mathf.Sin(b)*radius);g.Triangle(top,q,p,tint*(.85f+i*.035f));}
            }
        }
    }
}
