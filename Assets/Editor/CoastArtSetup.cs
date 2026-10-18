using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MagicIsland.CameraSystem;
using MagicIsland.Interaction;
using MagicIsland.Ocean;
using MagicIsland.TerrainSystem;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    public static partial class CoastArtSetup
    {
        public const string MeshFolder = "Assets/Art/Meshes/Coast";
        public const string MaterialFolder = "Assets/Art/Materials/Coast";
        public const string PrefabFolder = "Assets/Art/Prefabs/Environment/Scenery";
        public const int Version = 3;
        private static Material bark, leaf, leafLight, shrub, rock, shell, coral, coralGold, weed, wood, rope;

        // The one-time migration keeps mesh identities. Later setup runs preserve artist changes.
        public static void Ensure(AddressableAssetSettings settings)
        {
            Directory.CreateDirectory(MeshFolder);Directory.CreateDirectory(MaterialFolder);Directory.CreateDirectory(PrefabFolder);AssetDatabase.Refresh();
            bark = Material("Palm Bark", new Color(.56f,.37f,.21f));
            leaf = Material("Palm Leaves", new Color(.29f,.58f,.20f));
            leafLight = Material("Young Leaves", new Color(.55f,.72f,.25f));
            shrub = Material("Coastal Shrub", new Color(.38f,.62f,.28f));
            rock = Material("Coastal Limestone", new Color(.70f,.73f,.58f));
            shell = Material("Shell Ivory", new Color(.99f,.87f,.68f));
            coral = Material("Coral Rose", new Color(.87f,.46f,.56f));
            coralGold = Material("Coral Gold", new Color(.91f,.69f,.27f));
            weed = Material("Seagrass", new Color(.26f,.57f,.41f));
            wood = Material("Pier Honey Wood", new Color(.64f,.44f,.27f));
            rope = Material("Pier Rope", new Color(.88f,.78f,.55f));
            CreatePrefabs(settings);
            UpgradeTerrain();UpgradeOcean();UpgradePier();
        }

        public static Color LandColor(float radius,float edge,float angle)
        {
            float inset=edge-radius;
            float x=Mathf.Cos(angle)*radius,z=Mathf.Sin(angle)*radius;
            float irregular=.55f*Mathf.Sin(angle*5+.4f)+.32f*Mathf.Sin(angle*11+1.8f)
                +.85f*(Mathf.PerlinNoise(x*.48f+37,z*.48f+19)-.5f);
            float blend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(4.0f+irregular,2.5f+irregular,inset));
            var grass=new Color(.56f,.72f,.34f);var sand=new Color(.90f,.80f,.55f);
            var color=Color.Lerp(grass,sand,blend);
            color*=1+.018f*Mathf.Sin(angle*8+radius*1.1f);
            return color;
        }

        private static Material Material(string name,Color color)
        {
            string path=MaterialFolder+"/"+name+".mat";
            var value=AssetDatabase.LoadAssetAtPath<Material>(path);if(value!=null)return value;
            value=new Material(Shader.Find("Standard")){name=name,color=color};value.SetFloat("_Glossiness",.06f);
            AssetDatabase.CreateAsset(value,path);return value;
        }
        private static Mesh MeshAsset(string name,Func<Mesh> create)
        {
            string path=MeshFolder+"/"+name+".asset";var value=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(value!=null)
            {
                if(Finite(value.bounds.center)&&Finite(value.bounds.extents)&&value.bounds.size.sqrMagnitude>.001f&&value.vertices.All(Finite))return value;
                ReplaceMesh(path,create());return AssetDatabase.LoadAssetAtPath<Mesh>(path);
            }
            value=create();value.name=name;AssetDatabase.CreateAsset(value,path);return value;
        }
        private static void ReplaceMesh(string path,Mesh value)
        {
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);value.name=Path.GetFileNameWithoutExtension(path);
            if(existing==null)AssetDatabase.CreateAsset(value,path);
            else {EditorUtility.CopySerialized(value,existing);EditorUtility.SetDirty(existing);Object.DestroyImmediate(value);}
        }
        private static GameObject MeshPart(Transform parent,string name,Mesh mesh,Material material,Vector3 position)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.layer=2;go.transform.SetParent(parent,false);go.transform.localPosition=position;
            go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=material;return go;
        }
        private static GameObject Part(Transform parent,string name,PrimitiveType primitive,Vector3 position,Vector3 scale,Material material)
        {
            var go=GameObject.CreatePrimitive(primitive);go.name=name;go.layer=2;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=scale;
            go.GetComponent<Renderer>().sharedMaterial=material;Object.DestroyImmediate(go.GetComponent<Collider>());return go;
        }
        private static GameObject Prefab(string name,Action<GameObject> build,AddressableAssetSettings settings)
        {
            string path=PrefabFolder+"/"+name+".prefab";var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(prefab==null)
            {
                var root=new GameObject(name);root.layer=2;
                try{build(root);PrefabUtility.SaveAsPrefabAsset(root,path);}finally{Object.DestroyImmediate(root);}
                prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            var id=AssetDatabase.AssetPathToGUID(path);var entry=settings.FindAssetEntry(id)??settings.CreateOrMoveEntry(id,settings.FindGroup("Environment"));
            if(entry.address!=name)entry.SetAddress(name);return prefab;
        }
        private static void CreatePrefabs(AddressableAssetSettings settings)
        {
            var trunk=MeshAsset("Palm Trunk",PalmTrunk);var fronds=MeshAsset("Palm Fronds",()=>RibbonMesh(8,1.85f,.36f,false));
            var cluster=MeshAsset("Shrub Cluster",()=>ClusterMesh(7));var stone=MeshAsset("Reef Rock",RockMesh);
            var clam=MeshAsset("Clam Shell",ShellMesh);var seagrass=MeshAsset("Seagrass Blades",()=>RibbonMesh(9,.76f,.08f,true));
            var branches=MeshAsset("Branch Coral",CoralMesh);
            Prefab("CoastalPalm",go=>
            {
                MeshPart(go.transform,"Curved trunk",trunk,bark,Vector3.zero);
                MeshPart(go.transform,"Palm crown",fronds,leaf,new Vector3(.28f,3.1f,.06f));
                var young=MeshPart(go.transform,"Young fronds",fronds,leafLight,new Vector3(.28f,3.18f,.06f));young.transform.localScale=new Vector3(.57f,.8f,.57f);young.transform.localRotation=Quaternion.Euler(0,24,0);
                for(int i=0;i<3;i++)Part(go.transform,"Coconut "+i,PrimitiveType.Sphere,new Vector3(.25f+Mathf.Cos(i*2.1f)*.17f,2.97f,.06f+Mathf.Sin(i*2.1f)*.17f),Vector3.one*.21f,bark);
                var c=go.AddComponent<CapsuleCollider>();c.radius=.23f;c.height=2.1f;c.center=new Vector3(.06f,1.05f,0);
                go.AddComponent<CoastalSceneryPatch>().Obstacle=c;
                go.AddComponent<CameraOccluder>().Renderers=go.GetComponentsInChildren<Renderer>();
            },settings);
            Prefab("CoastalShrub",go=>MeshPart(go.transform,"Leaf clusters",cluster,shrub,Vector3.zero),settings);
            Prefab("BeachShell",go=>MeshPart(go.transform,"Ridged shell",clam,shell,Vector3.zero),settings);
            Prefab("ReefRock",go=>MeshPart(go.transform,"Faceted stone",stone,rock,Vector3.zero),settings);
            Prefab("ReefCoral",go=>MeshPart(go.transform,"Branching coral",branches,coral,Vector3.zero),settings);
            Prefab("ReefSeagrass",go=>MeshPart(go.transform,"Ribbon leaves",seagrass,weed,Vector3.zero),settings);
        }
        private static GameObject Instance(string name,Transform parent,Vector3 position,float yaw,Vector3 scale)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder+"/"+name+".prefab");
            var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab);go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localRotation=Quaternion.Euler(0,yaw,0);go.transform.localScale=scale;return go;
        }
        [MenuItem("Magic Island/Rebuild Coastal Palms")]
        public static void RebuildCoastalPalms()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            var root=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);
            try
            {
                var terrain=root.GetComponent<IslandTerrain>();
                if(terrain.CoastalScenery==null)throw new InvalidOperationException("Coastal scenery is missing; run the initial Setup first.");
                if(terrain.PalmScatter==null)terrain.PalmScatter=new CoastalPalmScatter();
                var plan=terrain.PalmScatter.CreatePlan();
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder+"/CoastalPalm.prefab");
                if(prefab==null||prefab.GetComponent<CoastalSceneryPatch>()==null)
                    throw new InvalidOperationException("CoastalPalm prefab is missing or invalid; existing scenery preserved.");
                var existing=terrain.CoastalScenery.GetComponentsInChildren<CoastalSceneryPatch>(true);
                for(int i=0;i<plan.Length;i++)
                {
                    // Reuse nested instances so their component overrides and fileIDs survive.
                    var palm=i<existing.Length?existing[i].gameObject:(GameObject)PrefabUtility.InstantiatePrefab(prefab,terrain.CoastalScenery);
                    ApplyPalmSite(palm.transform,plan[i]);
                }
                for(int i=plan.Length;i<existing.Length;i++)Object.DestroyImmediate(existing[i].gameObject);
                PrefabUtility.SaveAsPrefabAsset(root,TerrainSetup.PrefabPath);
                Debug.Log("Coastal palms rebaked: seed="+terrain.PalmScatter.Seed+", count="+plan.Length);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        private static void ApplyPalmSite(Transform palm,PalmSite site)
        {
            palm.localPosition=new Vector3((float)site.X,(float)IslandLayout.Height(site.X,site.Z),(float)site.Z);
            palm.localRotation=Quaternion.Euler(0,(float)site.Yaw,0);
            palm.localScale=Vector3.one*(float)site.Scale;
        }
        private static void UpgradeTerrain()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(TerrainSetup.PrefabPath);
            if(prefab.GetComponent<IslandTerrain>().CoastArtVersion>=Version)return;
            ReplaceMesh(TerrainSetup.MeshFolder+"/Island Land.asset",TerrainSetup.GroundMesh());
            ReplaceMesh(TerrainSetup.MeshFolder+"/Sea Boundary.asset",TerrainSetup.BoundaryMesh());
            var root=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);
            try
            {
                var marker=root.GetComponent<IslandTerrain>();if(marker.CoastalScenery!=null)Object.DestroyImmediate(marker.CoastalScenery.gameObject);
                var scenery=new GameObject("Coastal scenery");scenery.transform.SetParent(root.transform,false);marker.CoastalScenery=scenery.transform;
                if(marker.PalmScatter==null)marker.PalmScatter=new CoastalPalmScatter();
                foreach(var site in marker.PalmScatter.CreatePlan())
                {
                    var palm=Instance("CoastalPalm",scenery.transform,Vector3.zero,0,Vector3.one);
                    ApplyPalmSite(palm.transform,site);
                }
                if(marker.DetailScatter==null)marker.DetailScatter=new CoastalDetailScatter();
                ApplyCoastalDetails(marker);
                marker.CoastArtVersion=Version;PrefabUtility.SaveAsPrefabAsset(root,TerrainSetup.PrefabPath);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        private static void UpgradeOcean()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(OceanSetup.PrefabPath);
            if(prefab.GetComponent<OceanSurface>().CoastArtVersion>=Version)return;
            ReplaceMesh(OceanSetup.MeshFolder+"/Island Shore.asset",OceanSetup.ShoreMesh());
            var root=PrefabUtility.LoadPrefabContents(OceanSetup.PrefabPath);
            try
            {
                var marker=root.GetComponent<OceanSurface>();
                // Replace the original evenly spaced primitive reef placeholders.
                foreach(var child in root.transform.Cast<Transform>().ToArray())if(child!=marker.Water.transform&&child!=marker.Shore.transform)Object.DestroyImmediate(child.gameObject);
                var reefs=new GameObject("Shallow reef gardens");reefs.layer=2;reefs.transform.SetParent(root.transform,false);
                for(int i=0;i<48;i++)
                {
                    float a=i*Mathf.PI*2/48+.12f+.029f*Mathf.Sin(i*2.3f);float r=(float)IslandLayout.CoastRadius(a)+1.2f+(i%4)*.47f;
                    float t=(r-(float)IslandLayout.CoastRadius(a)+1.7f)/9.2f;
                    var p=new Vector3(Mathf.Cos(a)*r,Mathf.Min(-.02f-4.4f*t*t,marker.SeaLevel-.47f),Mathf.Sin(a)*r);
                    Instance("ReefRock",reefs.transform,p,i*127,new Vector3(.44f+(i%3)*.15f,.28f,.38f+(i%2)*.14f));
                    var coralRoot=Instance("ReefCoral",reefs.transform,p+new Vector3(.4f,0,.23f),i*51,Vector3.one*(.58f+(i%3)*.12f));
                    if(i%2==0)foreach(var renderer in coralRoot.GetComponentsInChildren<Renderer>())renderer.sharedMaterial=coralGold;
                    Submerge(coralRoot,marker.Water.transform.position.y);
                    for(int j=0;j<2;j++)
                    {
                        var plant=Instance("ReefSeagrass",reefs.transform,p+new Vector3(-.37f-j*.22f,-.01f,-.25f+j*.35f),i*41+j*23,new Vector3(.7f,.65f,.7f));
                        Submerge(plant,marker.Water.transform.position.y);
                    }
                }
                foreach(var renderer in reefs.GetComponentsInChildren<Renderer>())renderer.shadowCastingMode=ShadowCastingMode.Off;
                marker.CoastArtVersion=Version;PrefabUtility.SaveAsPrefabAsset(root,OceanSetup.PrefabPath);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        private static void UpgradePier()
        {
            const string path="Assets/Art/Prefabs/Fishing/CoastalFishingSpot.prefab";
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(prefab.GetComponent<FishingSpot>().CoastArtVersion>=Version)return;
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var oldDetail=root.transform.Find("Pier details");if(oldDetail!=null)Object.DestroyImmediate(oldDetail.gameObject);
                var detail=new GameObject("Pier details");detail.transform.SetParent(root.transform,false);
                for(int i=0;i<18;i++)
                {
                    var plank=root.transform.Find("Dock plank "+i);if(plank==null)continue;
                    plank.GetComponent<Renderer>().sharedMaterial=i%4==0?bark:wood;
                    for(int side=-1;side<=1;side+=2)Part(detail.transform,"Iron peg "+i+" "+side,PrimitiveType.Sphere,new Vector3(-1.85f+i*.3f,.105f,side*.98f),new Vector3(.035f,.015f,.035f),rock);
                }
                for(int side=-1;side<=1;side+=2)
                {
                    for(int i=0;i<5;i++)
                    {
                        var p=new Vector3(-1.7f+i*1.25f,-.55f,side*1.02f);
                        Part(detail.transform,"Timber piling "+side+" "+i,PrimitiveType.Cylinder,p,new Vector3(.18f,.65f,.18f),bark);
                    }
                    for(int span=0;span<4;span++)for(int segment=0;segment<6;segment++)
                    {
                        float x=-1.7f+span*1.25f;float t=segment/6f,u=(segment+1)/6f;
                        Beam(detail.transform,"Rope curve",new Vector3(x+1.25f*t,.65f-.15f*Mathf.Sin(t*Mathf.PI),side*1.12f),new Vector3(x+1.25f*u,.65f-.15f*Mathf.Sin(u*Mathf.PI),side*1.12f),.025f,rope);
                    }
                    var old=root.transform.Find("Rope rail "+side);if(old!=null)Object.DestroyImmediate(old.gameObject);
                }
                // Props stay outside the usable deck; all detail geometry ignores pointer raycasts.
                Part(detail.transform,"Fishing sign post",PrimitiveType.Cube,new Vector3(-1.55f,.68f,1.35f),new Vector3(.10f,1.3f,.10f),bark);
                Part(detail.transform,"Fishing sign board",PrimitiveType.Cube,new Vector3(-1.55f,1.25f,1.35f),new Vector3(.72f,.42f,.07f),wood);
                var fish=Part(detail.transform,"Fish sign",PrimitiveType.Sphere,new Vector3(-1.55f,1.26f,1.30f),new Vector3(.29f,.11f,.055f),shell);
                fish.transform.localRotation=Quaternion.Euler(0,0,10);
                Part(detail.transform,"Fish tail",PrimitiveType.Cube,new Vector3(-1.75f,1.25f,1.30f),new Vector3(.11f,.14f,.04f),shell).transform.localRotation=Quaternion.Euler(0,0,45);
                Part(detail.transform,"Fish eye",PrimitiveType.Sphere,new Vector3(-1.47f,1.28f,1.265f),Vector3.one*.026f,bark);
                root.GetComponent<FishingSpot>().CoastArtVersion=Version;PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        private static void Beam(Transform parent,string name,Vector3 start,Vector3 end,float radius,Material material)
        {
            var part=Part(parent,name,PrimitiveType.Cylinder,(start+end)*.5f,new Vector3(radius*2,(end-start).magnitude*.5f,radius*2),material);
            part.transform.localRotation=Quaternion.FromToRotation(Vector3.up,end-start);
        }
        private static void Submerge(GameObject go,float seaLevel)
        {
            float top=go.GetComponentsInChildren<Renderer>().Max(r=>r.bounds.max.y);
            if(top>seaLevel-.10f)go.transform.position-=Vector3.up*(top-seaLevel+.10f);
        }

        private static bool Finite(Vector3 v)=>!float.IsNaN(v.x)&&!float.IsNaN(v.y)&&!float.IsNaN(v.z)&&!float.IsInfinity(v.x)&&!float.IsInfinity(v.y)&&!float.IsInfinity(v.z);
        private static Mesh Build(List<Vector3> v,List<int> triangles)
        {
            if(!v.All(Finite))throw new InvalidOperationException("Coast mesh contains invalid vertices");
            var mesh=new Mesh();mesh.SetVertices(v);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        private static Mesh PalmTrunk()
        {
            var v=new List<Vector3>();var triangles=new List<int>();const int rings=16,sides=10;
            for(int j=0;j<=rings;j++)
            {
                float t=j/(float)rings;var center=new Vector3(.28f*t*t,t*3.1f,.06f*t);
                float r=Mathf.Lerp(.17f,.075f,t)*(j%2==0?1.12f:1);
                for(int s=0;s<sides;s++){float a=s*Mathf.PI*2/sides;v.Add(center+new Vector3(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r));}
            }
            for(int j=0;j<rings;j++)for(int s=0;s<sides;s++)
            {int a=j*sides+s,b=j*sides+(s+1)%sides;triangles.AddRange(new[]{a,a+sides,b,b,a+sides,b+sides});}
            return Build(v,triangles);
        }
        private static Mesh RibbonMesh(int blades,float length,float width,bool upright)
        {
            var v=new List<Vector3>();var triangles=new List<int>();const int segments=12;
            for(int i=0;i<blades;i++)
            {
                float a=i*Mathf.PI*2/blades;var direction=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));var side=new Vector3(-direction.z,0,direction.x);int begin=v.Count;
                float size=1+.12f*Mathf.Sin(i*2.7f);
                for(int j=0;j<=segments;j++)
                {
                    float t=j/(float)segments;float w=width*Mathf.Pow(Mathf.Max(0,Mathf.Sin(t*Mathf.PI)),.7f)*(j%2==0?1:.78f);
                    var mid=upright?direction*(.03f+t*t*.28f)+Vector3.up*(t*length*size):direction*(.08f+t*length*size)+Vector3.up*(.48f*Mathf.Sin(t*Mathf.PI)-.38f*t*t);
                    v.Add(mid-side*w-Vector3.up*w*.16f);v.Add(mid+Vector3.up*w*.07f);v.Add(mid+side*w-Vector3.up*w*.16f);
                }
                for(int j=0;j<segments;j++)for(int k=0;k<2;k++)
                {int b=begin+j*3+k;triangles.AddRange(new[]{b,b+3,b+1,b+1,b+3,b+4});}
                // Explicit reversed duplicate vertices provide real two-sided ribbon lighting.
                int frontEnd=v.Count,back=frontEnd;for(int n=begin;n<frontEnd;n++)v.Add(v[n]);
                int frontCount=segments*12,triangleBegin=triangles.Count-frontCount;
                for(int n=triangleBegin;n<triangleBegin+frontCount;n+=3)triangles.AddRange(new[]{back+triangles[n]-begin,back+triangles[n+2]-begin,back+triangles[n+1]-begin});
            }
            return Build(v,triangles);
        }
        private static Mesh ClusterMesh(int count)
        {
            var pieces=new List<CombineInstance>();var baseMesh=RockMesh();
            for(int i=0;i<count;i++)
            {
                float a=i*2.4f;var p=new Vector3(Mathf.Cos(a)*.29f,.22f+(i%3)*.07f,Mathf.Sin(a)*.29f);
                pieces.Add(new CombineInstance{mesh=baseMesh,transform=Matrix4x4.TRS(p,Quaternion.Euler(0,i*33,0),new Vector3(.33f,.28f,.33f))});
            }
            var result=new Mesh();result.CombineMeshes(pieces.ToArray());Object.DestroyImmediate(baseMesh);return result;
        }
        private static Mesh RockMesh()
        {
            var v=new List<Vector3>();var t=new List<int>();const int rings=7,sides=11;
            for(int j=0;j<=rings;j++)for(int s=0;s<sides;s++)
            {
                float a=s*Mathf.PI*2/sides,p=j*Mathf.PI/rings;float wobble=1+.1f*Mathf.Sin(s*3.2f+j*1.8f);
                v.Add(new Vector3(Mathf.Sin(p)*Mathf.Cos(a)*wobble,Mathf.Cos(p)*.8f,Mathf.Sin(p)*Mathf.Sin(a)*wobble));
            }
            for(int j=0;j<rings;j++)for(int s=0;s<sides;s++){int a=j*sides+s,b=j*sides+(s+1)%sides;t.AddRange(new[]{a,b,a+sides,a+sides,b,b+sides});}
            // Duplicate faces for a soft low-poly silhouette and faceted lighting.
            var flat=new List<Vector3>();var indices=new List<int>();foreach(int index in t){indices.Add(flat.Count);flat.Add(v[index]);}return Build(flat,indices);
        }
        private static Mesh ShellMesh()
        {
            var v=new List<Vector3>{new Vector3(0,.025f,-.25f)};var t=new List<int>();
            for(int i=0;i<=20;i++){float a=i*Mathf.PI/20;float radius=i%2==0?1:.94f;v.Add(new Vector3(Mathf.Cos(a)*radius,.12f*Mathf.Sin(a),Mathf.Sin(a)*radius*.9f));}
            for(int i=1;i<=20;i++)t.AddRange(new[]{0,i+1,i});return Build(v,t);
        }
        private static Mesh CoralMesh()
        {
            var v=new List<Vector3>();var t=new List<int>();
            void Branch(Vector3 start,Vector3 end,float radius)
            {
                int index=v.Count;var rotation=Quaternion.FromToRotation(Vector3.up,end-start);const int sides=6;
                for(int row=0;row<2;row++)for(int s=0;s<sides;s++){float a=s*Mathf.PI*2/sides;v.Add((row==0?start:end)+rotation*new Vector3(Mathf.Cos(a)*radius*(row==0?1:.65f),0,Mathf.Sin(a)*radius*(row==0?1:.65f)));}
                for(int s=0;s<sides;s++){int a=index+s,b=index+(s+1)%sides;t.AddRange(new[]{a,a+sides,b,b,a+sides,b+sides});}
                for(int s=1;s<sides-1;s++)t.AddRange(new[]{index+sides,index+sides+s+1,index+sides+s});
            }
            Branch(Vector3.zero,new Vector3(0,.63f,0),.065f);
            for(int i=0;i<5;i++)
            {
                float a=i*2.3f;var from=new Vector3(0,.18f+i*.065f,0);var mid=from+new Vector3(Mathf.Cos(a)*.24f,.12f,Mathf.Sin(a)*.24f);var tip=mid+new Vector3(.02f,.22f,0);
                Branch(from,mid,.043f);Branch(mid,tip,.036f);Branch(mid,tip+new Vector3(Mathf.Cos(a+1)*.15f,-.04f,Mathf.Sin(a+1)*.15f),.026f);
            }
            return Build(v,t);
        }
    }
}
