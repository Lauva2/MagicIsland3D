using System.Collections.Generic;
using System.IO;
using MagicIsland.Ocean;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    public static class OceanInteractionEffectsSetup
    {
        public const int Version=1;
        public const string PrefabPath="Assets/Art/Prefabs/Environment/OceanInteractionEffects.prefab";
        public const string MeshFolder="Assets/Art/Meshes/Ocean/Effects",MaterialFolder="Assets/Art/Materials/Ocean/Effects";
        public static void Ensure()
        {
            Directory.CreateDirectory(MeshFolder);Directory.CreateDirectory(MaterialFolder);AssetDatabase.Refresh();
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(OceanSetup.PrefabPath);
            if(prefab==null)return;
            var model=prefab.GetComponent<OceanSurface>();
            if(model.EffectsArtVersion>=Version&&model.Effects!=null)return;
            if(!File.Exists(PrefabPath))CreatePrefab();
            var root=PrefabUtility.LoadPrefabContents(OceanSetup.PrefabPath);
            try
            {
                var surface=root.GetComponent<OceanSurface>();
                var old=root.transform.Find("Ocean interaction effects");if(old!=null)Object.DestroyImmediate(old.gameObject);
                var instance=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath),root.transform);
                instance.name="Ocean interaction effects";surface.Effects=instance.GetComponent<OceanInteractionEffects>();surface.EffectsArtVersion=Version;
                PrefabUtility.SaveAsPrefabAsset(root,OceanSetup.PrefabPath);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            AssetDatabase.SaveAssets();
        }
        static void CreatePrefab()
        {
            var ring=MeshAsset("Ripple ribbon",Ring());var grain=MeshAsset("Droplet and sand grain",Octahedron());var puff=MeshAsset("Soft sand puff",Quad());
            var foam=MaterialAsset("Splash foam",new Color(.84f,.96f,.91f),0,0,3010);
            var droplet=MaterialAsset("Splash droplets",new Color(.72f,.89f,.85f),1,0,3010);
            var wake=MaterialAsset("Fish water disturbance",new Color(.64f,.87f,.81f),0,1,3010);
            var reef=MaterialAsset("Underwater sand",new Color(.57f,.66f,.49f),2,0,2990);
            var sand=MaterialAsset("Shore sand grains",new Color(.81f,.63f,.35f),1,0,3010);
            var dust=MaterialAsset("Shore sand puff",new Color(.85f,.72f,.48f),2,0,3010);
            var root=new GameObject("OceanInteractionEffects",typeof(OceanInteractionEffects));root.layer=2;
            try
            {
                var slots=new List<OceanEffectParticle>();
                AddSlots(root,slots,OceanEffectKind.Ripple,10,ring,foam);AddSlots(root,slots,OceanEffectKind.Droplet,24,grain,droplet);
                AddSlots(root,slots,OceanEffectKind.FishWake,12,ring,wake);AddSlots(root,slots,OceanEffectKind.ReefDust,12,puff,reef);
                AddSlots(root,slots,OceanEffectKind.ShoreGrain,16,grain,sand);AddSlots(root,slots,OceanEffectKind.ShorePuff,10,puff,dust);
                root.GetComponent<OceanInteractionEffects>().Particles=slots.ToArray();PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
            }
            finally{Object.DestroyImmediate(root);}
        }
        static void AddSlots(GameObject root,List<OceanEffectParticle> slots,OceanEffectKind kind,int count,Mesh mesh,Material material)
        {
            for(int i=0;i<count;i++)
            {
                var go=new GameObject(kind+" "+i,typeof(MeshFilter),typeof(MeshRenderer),typeof(OceanEffectParticle));go.layer=2;go.transform.SetParent(root.transform,false);
                go.GetComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
                var particle=go.GetComponent<OceanEffectParticle>();particle.Kind=kind;particle.View=renderer;slots.Add(particle);go.SetActive(false);
            }
        }
        static Mesh MeshAsset(string name,Mesh mesh)
        {
            string path=MeshFolder+"/"+name+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(existing!=null){Object.DestroyImmediate(mesh);return existing;}
            mesh.name=name;AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
        static Material MaterialAsset(string name,Color tint,float shape,float broken,int queue)
        {
            string path=MaterialFolder+"/"+name+".mat";var existing=AssetDatabase.LoadAssetAtPath<Material>(path);if(existing!=null)return existing;
            var shader=Shader.Find("MagicIsland/Ocean Interaction");if(shader==null)throw new System.InvalidOperationException("Missing ocean interaction shader");
            var material=new Material(shader){name=name,renderQueue=queue};material.SetColor("_Tint",tint);material.SetFloat("_Shape",shape);material.SetFloat("_Broken",broken);
            AssetDatabase.CreateAsset(material,path);return material;
        }
        static Mesh Ring()
        {
            const int segments=96;var vertices=new Vector3[(segments+1)*2];var uv=new Vector2[vertices.Length];var indices=new int[segments*6];
            for(int i=0;i<=segments;i++)
            {
                float a=i*Mathf.PI*2/segments;var direction=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                vertices[i*2]=direction*.965f;vertices[i*2+1]=direction;uv[i*2]=new Vector2(i/(float)segments,0);uv[i*2+1]=new Vector2(i/(float)segments,1);
                if(i<segments){int k=i*6,j=i*2;indices[k]=j;indices[k+1]=j+2;indices[k+2]=j+1;indices[k+3]=j+1;indices[k+4]=j+2;indices[k+5]=j+3;}
            }
            var mesh=new Mesh{vertices=vertices,uv=uv,triangles=indices};mesh.RecalculateNormals();mesh.RecalculateBounds();var b=mesh.bounds;b.Expand(Vector3.up*.3f);mesh.bounds=b;return mesh;
        }
        static Mesh Quad()
        {
            var mesh=new Mesh{vertices=new[]{new Vector3(-1,0,-1),new Vector3(-1,0,1),new Vector3(1,0,-1),new Vector3(1,0,1)},uv=new[]{Vector2.zero,Vector2.up,Vector2.right,Vector2.one},triangles=new[]{0,1,2,2,1,3}};
            mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        static Mesh Octahedron()
        {
            var sides=new[]{Vector3.right,Vector3.forward,Vector3.left,Vector3.back};var vertices=new List<Vector3>();
            for(int i=0;i<4;i++){vertices.AddRange(new[]{Vector3.up*.8f,sides[i]*.5f,sides[(i+1)%4]*.5f,Vector3.down*.8f,sides[(i+1)%4]*.5f,sides[i]*.5f});}
            var uv=new Vector2[vertices.Count];var indices=new int[vertices.Count];for(int i=0;i<indices.Length;i++){indices[i]=i;uv[i]=Vector2.one*.5f;}
            var mesh=new Mesh{vertices=vertices.ToArray(),triangles=indices,uv=uv};mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
    }
}
