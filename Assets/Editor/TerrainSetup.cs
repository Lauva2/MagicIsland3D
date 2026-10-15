using System.Collections.Generic;
using System.IO;
using MagicIsland.Configuration;
using MagicIsland.Interaction;
using MagicIsland.TerrainSystem;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace MagicIsland.EditorTools
{
    public static class TerrainSetup
    {
        public const string PrefabPath="Assets/Art/Prefabs/Environment/IslandTerrain.prefab";
        public const string MeshFolder="Assets/Art/Meshes/Terrain";
        public static void Ensure(AddressableAssetSettings settings,GameConfig config)
        {
            Directory.CreateDirectory(MeshFolder);AssetDatabase.Refresh();
            var ground=MeshAsset("Island Land",GroundMesh);var edge=MeshAsset("Sea Boundary",BoundaryMesh);
            if(!File.Exists(PrefabPath))
            {
                var root=new GameObject("IslandTerrain",typeof(IslandTerrain));
                try
                {
                    var land=new GameObject("Walkable island",typeof(MeshFilter),typeof(MeshRenderer),typeof(MeshCollider));land.transform.SetParent(root.transform,false);
                    land.GetComponent<MeshFilter>().sharedMesh=ground;land.GetComponent<MeshRenderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(OceanSetup.MaterialFolder+"/Shore.mat");
                    var model=root.GetComponent<IslandTerrain>();model.Ground=land.GetComponent<MeshCollider>();model.Ground.sharedMesh=ground;
                    var boundary=new GameObject("Sea boundary",typeof(MeshCollider));boundary.layer=2;boundary.transform.SetParent(root.transform,false);model.Boundary=boundary.GetComponent<MeshCollider>();model.Boundary.sharedMesh=edge;
                    PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
                }
                finally{Object.DestroyImmediate(root);}
            }
            var group=settings.FindGroup("Environment");var guid=AssetDatabase.AssetPathToGUID(PrefabPath);var entry=settings.FindAssetEntry(guid)??settings.CreateOrMoveEntry(guid,group);if(entry.address!="IslandTerrain")entry.SetAddress("IslandTerrain");
            if(!File.ReadAllText("Assets/Config/GameConfig.asset").Contains("TerrainPrefabAddress:"))EditorUtility.SetDirty(config);
            UpgradePier();
            const string fishingPanel="Assets/Art/Prefabs/UI/FishingPanel.prefab";
            if(!AssetDatabase.LoadAssetAtPath<GameObject>(fishingPanel).GetComponent<MagicIsland.UI.Views.FishingView>().CoastLayout)
            {
                var ui=PrefabUtility.LoadPrefabContents(fishingPanel);
                try
                {
                    var view=ui.GetComponent<MagicIsland.UI.Views.FishingView>();var card=view.StatusText.transform.parent.GetComponent<RectTransform>();
                    card.anchorMin=card.anchorMax=card.pivot=new Vector2(0,1);card.anchoredPosition=new Vector2(20,-350);view.CoastLayout=true;
                    PrefabUtility.SaveAsPrefabAsset(ui,fishingPanel);
                }
                finally{PrefabUtility.UnloadPrefabContents(ui);}
            }
            var schedules=AssetDatabase.LoadAssetAtPath<NPCScheduleCatalog>("Assets/Config/NPCScheduleCatalog.asset");
            if(!schedules.CoastTerrainReady)
            {
                foreach(var s in schedules.Schedules)if(s.CoastOffset==new Vector3(-1.25f,0,1.4f))s.CoastOffset=new Vector3(-.65f,0,2f);
                schedules.CoastTerrainReady=true;EditorUtility.SetDirty(schedules);
            }
        }
        static Mesh MeshAsset(string name,System.Func<Mesh> build)
        {string path=MeshFolder+"/"+name+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(mesh!=null)return mesh;mesh=build();mesh.name=name;AssetDatabase.CreateAsset(mesh,path);return mesh;}
        internal static Mesh GroundMesh()
        {
            const int n=192,rings=24;var v=new Vector3[1+n*rings];var colors=new Color[v.Length];var triangles=new List<int>();
            colors[0]=CoastArtSetup.LandColor(0,17,0);
            for(int a=0;a<n;a++)
            {
                float angle=a*Mathf.PI*2/n,dx=Mathf.Cos(angle),dz=Mathf.Sin(angle),edge=(float)IslandLayout.CoastRadius(angle);
                for(int j=0;j<rings;j++)
                {
                    float r=Mathf.Lerp(edge-5,edge-.45f,j/(float)(rings-1));
                    int i=1+a*rings+j;v[i]=new Vector3(dx*r,(float)IslandLayout.Height(dx*r,dz*r),dz*r);colors[i]=CoastArtSetup.LandColor(r,edge,angle);
                }
            }
            for(int a=0;a<n;a++)
            {
                int first=1+a*rings,next=1+((a+1)%n)*rings;triangles.AddRange(new[]{0,next,first});
                for(int j=0;j<rings-1;j++){int i=first+j,b=next+j;triangles.AddRange(new[]{i,b,i+1,i+1,b,b+1});}
            }
            var mesh=new Mesh{vertices=v,colors=colors,triangles=triangles.ToArray()};mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        internal static Mesh BoundaryMesh()
        {
            const int n=320;var v=new List<Vector3>();var t=new List<int>();
            for(int a=0;a<n;a++)
            {
                float f=a*Mathf.PI*2/n,g=(a+1)*Mathf.PI*2/n,r=(float)IslandLayout.CoastRadius(f)-.45f,s=(float)IslandLayout.CoastRadius(g)-.45f;
                var p=new Vector3(Mathf.Cos(f)*r,-.5f,Mathf.Sin(f)*r);var q=new Vector3(Mathf.Cos(g)*s,-.5f,Mathf.Sin(g)*s);
                if(IslandLayout.Gate((p.x+q.x)*.5,(p.z+q.z)*.5))continue;
                int i=v.Count;v.AddRange(new[]{p,q,p+Vector3.up*2.5f,q+Vector3.up*2.5f});t.AddRange(new[]{i,i+1,i+2,i+2,i+1,i+3});
            }
            var mesh=new Mesh{vertices=v.ToArray(),triangles=t.ToArray()};mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        static void UpgradePier()
        {
            const string path="Assets/Art/Prefabs/Fishing/CoastalFishingSpot.prefab";
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(prefab.GetComponent<FishingSpot>().Deck!=null)return;
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                for(int i=root.transform.childCount-1;i>=0;i--)Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
                var marker=root.GetComponent<FishingSpot>();var wood=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Buildings/Hall Wood.mat");var pale=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Fishing/Sand.mat");var red=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Fishing/Bobber Coral.mat");
                for(int i=0;i<18;i++)Part(root.transform,"Dock plank "+i,PrimitiveType.Cube,new Vector3(-1.85f+i*.3f,.055f,0),new Vector3(.285f,.09f,2.4f),wood);
                for(int side=-1;side<=1;side+=2)
                {
                    Part(root.transform,"Side beam "+side,PrimitiveType.Cube,new Vector3(.7f,-.04f,side*1.02f),new Vector3(5.4f,.12f,.13f),wood);
                    Part(root.transform,"Rope rail "+side,PrimitiveType.Cylinder,new Vector3(.7f,.65f,side*1.12f),new Vector3(.045f,2.7f,.045f),pale).transform.localRotation=Quaternion.Euler(0,0,90);
                    for(int p=0;p<5;p++)Part(root.transform,"Dock post "+side+" "+p,PrimitiveType.Cylinder,new Vector3(-1.7f+p*1.25f,.12f,side*1.12f),new Vector3(.14f,.65f,.14f),wood);
                    Barrier(root.transform,"Side collision "+side,new Vector3(.7f,.5f,side*1.17f),new Vector3(5.7f,1.4f,.13f));
                }
                Barrier(root.transform,"End collision",new Vector3(3.42f,.5f,0),new Vector3(.12f,1.4f,2.4f));
                marker.Deck=Barrier(root.transform,"Walkable deck",new Vector3(.7f,.02f,0),new Vector3(5.4f,.16f,2.4f));
                marker.ReachPoint=new GameObject("Fishing position").transform;marker.ReachPoint.SetParent(root.transform,false);marker.ReachPoint.localPosition=new Vector3(2.3f,.1f,0);
                marker.Bobber=Part(root.transform,"Bobber",PrimitiveType.Sphere,new Vector3(4.3f,-.17f,0),Vector3.one*.17f,red).transform;
                var label=new GameObject("Pier label",typeof(TMPro.TextMeshPro));label.transform.SetParent(root.transform,false);label.transform.localPosition=new Vector3(2.1f,.8f,0);marker.Label=label.transform;
                var text=label.GetComponent<TMPro.TextMeshPro>();text.font=UiPrefabMigration.FontAsset;text.text="海岸码头";text.fontSize=1.5f;text.rectTransform.sizeDelta=new Vector2(2,.3f);text.alignment=TMPro.TextAlignmentOptions.Center;text.color=new Color(1,.97f,.84f);
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            var catalog=AssetDatabase.LoadAssetAtPath<FishingCatalog>("Assets/Config/FishingCatalog.asset");
            if(catalog.SpotPosition==new Vector3(10.4f,0,-8.2f)){catalog.SpotPosition=new Vector3((float)IslandLayout.PierX,0,(float)IslandLayout.PierZ);EditorUtility.SetDirty(catalog);}
        }
        static GameObject Part(Transform parent,string name,PrimitiveType type,Vector3 position,Vector3 scale,Material material)
        {var go=GameObject.CreatePrimitive(type);go.name=name;go.layer=2;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=material;Object.DestroyImmediate(go.GetComponent<Collider>());return go;}
        static BoxCollider Barrier(Transform parent,string name,Vector3 center,Vector3 size)
        {var go=new GameObject(name,typeof(BoxCollider));go.transform.SetParent(parent,false);var collider=go.GetComponent<BoxCollider>();collider.center=center;collider.size=size;return collider;}
    }
}
