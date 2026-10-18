using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MagicIsland.Interaction;
using MagicIsland.Ocean;
using MagicIsland.TerrainSystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    public static partial class StarterIslandWorldSetup
    {
        public const string TreeSource="SourceArt/WorldPlanning/Models/StarterAncientTree.meshdata.json";
        [Serializable] sealed class TreeData{public int version;public float[] positions,colors,uv;public int[] triangles;}
        static Mesh TreeFromBlender()
        {
            var d=JsonUtility.FromJson<TreeData>(File.ReadAllText(TreeSource));int n=d.positions.Length/3;
            if(d.version!=1||n<3||d.positions.Length!=n*3||d.colors.Length!=n*4||d.uv.Length!=n*2||d.triangles.Length%3!=0
                ||d.positions.Concat(d.colors).Concat(d.uv).Any(f=>!Finite(f))||d.triangles.Any(i=>i<0||i>=n))throw new InvalidOperationException("Invalid ancient tree source");
            var v=new Vector3[n];var c=new Color[n];var uv=new Vector2[n];
            for(int i=0;i<n;i++){v[i]=new Vector3(d.positions[i*3],d.positions[i*3+1],d.positions[i*3+2]);c[i]=new Color(d.colors[i*4],d.colors[i*4+1],d.colors[i*4+2],d.colors[i*4+3]);uv[i]=new Vector2(d.uv[i*2],d.uv[i*2+1]);}
            var m=new Mesh{indexFormat=n>65535?IndexFormat.UInt32:IndexFormat.UInt16,vertices=v,colors=c,uv=uv,triangles=d.triangles};m.RecalculateNormals();m.RecalculateBounds();var b=m.bounds;b.Expand(1);m.bounds=b;return m;
        }
        [MenuItem("Magic Island/Art/Rebuild Ancient Tree And Water Ecology (Explicit)")]
        public static void RebuildNature()
        {
            AssetDatabase.Refresh();details=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Terrain/Biome Details.mat");
            magic=Material("Magic Landmarks","MagicIsland/World Magic");
            var root=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);
            try
            {
                var world=root.GetComponent<IslandTerrain>().World;
                if(world==null||!world.IsValid)throw new InvalidOperationException("Bake starter island first");
                Landmark(world.Scenery,"Luminous ancient tree",-8,700,TreeFromBlender());
                IsleDetails(world.Scenery);
                BakeWaterEcology(world);
                PrefabUtility.SaveAsPrefabAsset(root,TerrainSetup.PrefabPath);AssetDatabase.SaveAssets();
                Debug.Log("Ancient tree and world water ecology baked. Original island/ocean records preserved.");
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        static void IsleDetails(Transform parent)
        {
            var g=new Geometry();var random=new System.Random(81725);
            for(int i=0;i<72;i++)
            {
                float a=i*2.399963f,r=21.8f+(float)random.NextDouble()*3.5f;
                var p=new Vector3(-8+Mathf.Cos(a)*r,12.1f,700+Mathf.Sin(a)*r);
                if(p.x< -25&&Mathf.Abs(p.z-700)<5)continue;
                g.Ball(p,new Vector3(1.1f+(i%3)*.3f,.8f,1.25f),new Color(.61f,.67f,.57f));
                if(i%2==0)g.Ball(p+Vector3.up*.48f,new Vector3(.85f,.25f,1),new Color(.48f,.64f,.43f));
            }
            for(int i=0;i<70;i++)
            {
                float a=i*2.399963f,r=9+(float)random.NextDouble()*10;var p=new Vector3(-8+Mathf.Cos(a)*r,12.68f,700+Mathf.Sin(a)*r);
                if(p.x< -13&&Mathf.Abs(p.z-700)<4.5f)continue;
                Grass(g,p,.5f,new Color(.43f,.65f,.47f));Flower(g,p,.7f,.26f,i%3==0?new Color(.64f,.63f,.94f):new Color(.77f,.85f,.72f));
            }
            Part(parent,"Ancient island moss and stones",g.Mesh(),details,false);
            var path=new Geometry();var stone=new Color(.66f,.71f,.60f);
            for(float x=-78;x< -30;x+=2.4f)
            {
                float y=CausewayHeight(x)+.025f;
                foreach(float z in new[]{698.3f,701.6f})path.Ball(new Vector3(x,y,z),new Vector3(1.04f,.055f,1.3f),stone);
            }
            Part(parent,"Ancient approach paving",path.Mesh(),details,false);
        }
        static void BakeWaterEcology(StarterIslandWorld world)
        {
            var node=world.transform.Find("World water ecology");
            var go=node!=null?node.gameObject:new GameObject("World water ecology",typeof(WorldWaterEcology));go.transform.SetParent(world.transform,false);go.layer=2;
            // Only this explicit operation replaces its own baked decoration/school nodes.
            for(int i=go.transform.childCount-1;i>=0;i--)Object.DestroyImmediate(go.transform.GetChild(i).gameObject);
            var ecology=go.GetComponent<WorldWaterEcology>();ecology.ArtVersion=1;
            ecology.AncientTree=world.Scenery.Find("Luminous ancient tree").GetComponent<Renderer>();
            ecology.LakeSurface=world.Waters[0];
            var habitats=new List<WorldWaterEcology.Habitat>();var renderers=new List<Renderer>();
            var ocean=AssetDatabase.LoadAssetAtPath<GameObject>(OceanSetup.PrefabPath).GetComponent<OceanSurface>();
            var material=AssetDatabase.LoadAssetAtPath<Material>(OceanBiomeSetup.MaterialPath);
            var reefMaterial=AssetDatabase.LoadAssetAtPath<Material>(OceanReefPaletteSetup.MaterialPath);
            // Freshwater uses minnows, lake stones and plant templates; coral species stay at sea.
            var lakes=new[]{new Vector2(-37,678),new Vector2(-15,660),new Vector2(32,708),new Vector2(285,572)};
            for(int i=0;i<lakes.Length;i++)BakeHabitat(go.transform,"Lake habitat "+i,lakes[i],true,ShallowFishSpecies.SilverMinnow,ocean,material,reefMaterial,habitats,renderers,i);
            for(int i=0;i<6;i++)
            {
                double a=new[]{-2.16,-1.04,-.12,.45,2.68,3.17}[i],r=StarterIslandLayout.CoastRadius(a);
                var center=new Vector2((float)(Math.Cos(a)*StarterIslandLayout.RadiusX*r),(float)(StarterIslandLayout.CenterZ+Math.Sin(a)*StarterIslandLayout.RadiusZ*r));
                center+=new Vector2((float)Math.Cos(a),(float)Math.Sin(a))*22;
                BakeHabitat(go.transform,"Coastal reef "+i,center,false,(ShallowFishSpecies)(i%4),ocean,material,reefMaterial,habitats,renderers,10+i);
            }
            ecology.Habitats=habitats.ToArray();ecology.Ambient=renderers.ToArray();
            ecology.Bind(ocean.FishAnimation);
            foreach(var h in ecology.Habitats)
                for(int i=0;i<h.School.Members;i++){var m=h.School.Swimmers[i];m.Spawn=h.School.Steering.States[i].Position;h.School.Swimmers[i]=m;}
            ecology.Clear();
        }
        static void BakeHabitat(Transform parent,string name,Vector2 center,bool lake,ShallowFishSpecies species,OceanSurface original,Material material,Material reefMaterial,List<WorldWaterEcology.Habitat> habitats,List<Renderer> renderers,int seed)
        {
            float waterLevel=lake?(float)StarterIslandLayout.LakeLevel:original.SeaLevel;var floorBatch=new NatureBatch();const int half=28,step=2;
            float Floor(float x,float z)
            {
                if(lake)return (float)TerrainGridHeight(x,z)+.015f;
                float distance=(float)-StarterIslandLayout.CoastDistance(x,z);
                float height=waterLevel-1.8f-Mathf.Max(0,distance-14)*.055f;
                float edge=Vector2.Distance(new Vector2(x,z),center);float original=(float)TerrainGridHeight(x,z);
                return Mathf.Max(original,Mathf.Lerp(height,original,Mathf.SmoothStep(0,1,Mathf.InverseLerp(15,26,edge))))+.015f;
            }
            for(int x=-half;x<half;x+=step)for(int z=-half;z<half;z+=step)
            {
                if(!lake&&(Vector2.Distance(new Vector2(x+1,z+1),Vector2.zero)>26||StarterIslandLayout.CoastDistance(center.x+x+1,center.y+z+1)>-1))continue;
                Vector3 A(float dx,float dz)=>new Vector3(center.x+dx,Floor(center.x+dx,center.y+dz),center.y+dz);
                floorBatch.Quad(A(x,z),A(x,z+step),A(x+step,z+step),A(x+step,z),lake?new Color(.61f,.66f,.49f):new Color(.86f,.77f,.58f));
            }
            var floor=Part(parent,name+" floor",floorBatch.Mesh(),details,false).GetComponent<MeshFilter>();floor.GetComponent<Renderer>().enabled=!lake;renderers.Add(floor.GetComponent<Renderer>());
            var batch=new NatureBatch();var obstacles=new List<OceanSwimObstacle>();var random=new System.Random(77191+seed*317);
            for(int i=0;i<48;i++)
            {
                float a=(float)random.NextDouble()*Mathf.PI*2,r=8+(float)random.NextDouble()*11;
                float x=center.x+Mathf.Cos(a)*r,z=center.y+Mathf.Sin(a)*r,y=Floor(x,z);
                if(lake&&StarterIslandLayout.LakeDistance(x,z)>-1||lake&&StarterIslandLayout.TreeIsle(x,z,-2)||!lake&&StarterIslandLayout.CoastDistance(x,z)>-3)continue;
                int kind=lake?(i%4==0?8:4):i%9;
                string template=OceanReefArtSetup.Names[kind];var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(OceanReefArtSetup.SourcePath(template));
                float scale=lake?.75f+(float)random.NextDouble()*.7f:.7f+(float)random.NextDouble()*1.1f;
                if(y+mesh.bounds.max.y*scale>waterLevel-.25f)continue;
                var position=new Vector3(x,y,z);float yaw=(float)random.NextDouble()*360;
                Color[] hues={new Color(.93f,.40f,.53f),new Color(.65f,.47f,.84f),new Color(.95f,.63f,.34f),new Color(.39f,.68f,.56f)};
                var tint=lake?new Color(.64f,.77f,.55f):kind==8?new Color(.69f,.69f,.57f):kind==4?new Color(.43f,.66f,.43f):hues[i%hues.Length];
                batch.Add(mesh,position,yaw,scale,tint,kind==4);
                if(kind!=4)
                {
                    float radius=mesh.vertices.Max(v=>new Vector2(v.x,v.z).magnitude)*scale;
                    obstacles.Add(new OceanSwimObstacle{Center=new Vector2(x,z),Radius=radius,Bottom=y+mesh.bounds.min.y*scale,Top=y+mesh.bounds.max.y*scale,Coral=!lake&&kind<4});
                }
            }
            var reef=Part(parent,name+" plants and reef",batch.Mesh(),lake?material:reefMaterial,false).GetComponent<Renderer>();renderers.Add(reef);
            var source=AssetDatabase.LoadAssetAtPath<Mesh>(OceanBiomeSetup.MeshFolder+"/"+OceanWildlifeArtSetup.Names[(int)species]+".asset");
            var fish=new NatureBatch();int count=species==ShallowFishSpecies.SpottedRay?3:7;var swimmers=new OceanFishMember[count];
            for(int i=0;i<count;i++)
            {
                float a=i*Mathf.PI*2/count;var pivot=new Vector4(Mathf.Cos(a)*1.45f,0,Mathf.Sin(a)*1.45f,1+i*2.399963f);
                float scale=species==ShallowFishSpecies.SilverMinnow?1.5f:1.1f;float radius=source.vertices.Max(v=>new Vector2(v.x,v.z).magnitude)*scale+.04f;
                fish.Add(source,(Vector3)pivot,0,scale,Color.white,false,pivot,new Vector4(0,(int)species,0,0));
                swimmers[i]=new OceanFishMember{Pivot=pivot,Radius=radius,Bottom=source.bounds.min.y*scale-.015f,Top=source.bounds.max.y*scale+.015f};
            }
            var fishObject=Part(parent,name+" fish",fish.Mesh(),material,false);var school=fishObject.GetComponent<OceanSchool>()??fishObject.AddComponent<OceanSchool>();
            school.Species=species;school.Members=count;school.HomeRadius=5.4f;school.Radius=2.5f;school.Speed=species==ShallowFishSpecies.SpottedRay?.16f:.24f;school.Phase=seed*.73f;
            school.Center=new Vector3(center.x,0,center.y);school.Obstacles=obstacles.ToArray();school.Habitat=lake?OceanBiome.SeagrassMeadow:OceanBiome.ReefSlope;
            var water=new OceanFishWaterVolume(floor.sharedMesh.vertices,floor.sharedMesh.triangles);float highest=water.HighestFloor(school.Center,school.HomeRadius);
            school.Center=new Vector3(center.x,Mathf.Lerp(highest,waterLevel,.73f),center.y);school.transform.localPosition=school.Center;
            foreach(int i in Enumerable.Range(0,count)){var s=swimmers[i];s.Spawn=school.Center+(Vector3)s.Pivot;swimmers[i]=s;}
            school.Swimmers=swimmers;
            habitats.Add(new WorldWaterEcology.Habitat{School=school,Floor=floor,WaterLevel=waterLevel,Freshwater=lake});
        }
        sealed class NatureBatch
        {
            readonly List<Vector3> v=new List<Vector3>(),n=new List<Vector3>();readonly List<Color> c=new List<Color>();readonly List<int> t=new List<int>();
            readonly List<Vector4> pivots=new List<Vector4>(),shapes=new List<Vector4>();
            public void Add(Mesh mesh,Vector3 position,float yaw,float scale,Color tint,bool sway,Vector4 pivot=default,Vector4 shape=default)
            {
                int offset=v.Count;var rotation=Quaternion.Euler(0,yaw,0);var points=mesh.vertices;var normals=mesh.normals;var colors=mesh.colors;
                for(int i=0;i<points.Length;i++)
                {v.Add(position+rotation*(points[i]*scale));n.Add(rotation*(normals.Length==points.Length?normals[i]:Vector3.up));var color=(colors.Length==points.Length?colors[i]:Color.white)*tint;color.a=sway?Mathf.InverseLerp(mesh.bounds.min.y,mesh.bounds.max.y,points[i].y):0;c.Add(color);pivots.Add(pivot);shapes.Add(shape);}
                t.AddRange(mesh.triangles.Select(i=>i+offset));
            }
            public void Quad(Vector3 a,Vector3 b,Vector3 cc,Vector3 d,Color color)
            {int o=v.Count;v.AddRange(new[]{a,b,cc,d});for(int i=0;i<4;i++){n.Add(Vector3.up);c.Add(color);pivots.Add(Vector4.zero);shapes.Add(Vector4.zero);}t.AddRange(new[]{o,o+1,o+2,o,o+2,o+3});}
            public Mesh Mesh()
            {var m=new Mesh{indexFormat=v.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};m.SetVertices(v);m.SetNormals(n);m.SetColors(c);m.SetTriangles(t,0);m.SetUVs(1,pivots);m.SetUVs(2,shapes);m.RecalculateBounds();return m;}
        }
    }
}
