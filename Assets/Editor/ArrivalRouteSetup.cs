using System;
using System.IO;
using System.Linq;
using MagicIsland.Interaction;
using MagicIsland.TerrainSystem;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    public static class ArrivalRouteSetup
    {
        public const string GroundPath=StarterIslandWorldSetup.Folder+"/Village Grass.asset";
        public const string PathMaterial=VillageArtSetup.MaterialFolder+"/Village Sand Path.mat";
        public static readonly string[] PathMeshes={VillageArtSetup.MeshFolder+"/Village Path Strip.asset",VillageArtSetup.MeshFolder+"/Village Path Junction.asset"};
        const string GroupName="Arrival verge";
        static void Require(bool ok,string why){if(!ok)throw new InvalidOperationException("Arrival route: "+why);}
        static void Clean(string path)=>File.WriteAllText(path,string.Join("\n",File.ReadAllLines(path).Select(s=>s.TrimEnd()))+"\n");

        [MenuItem("Magic Island/Art/Refine Beach Arrival Route (Explicit)")]
        public static void Rebuild()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling,"use Edit Mode");
            var ground=AssetDatabase.LoadAssetAtPath<Mesh>(GroundPath);
            var material=AssetDatabase.LoadAssetAtPath<Material>(PathMaterial);
            var paths=PathMeshes.Select(AssetDatabase.LoadAssetAtPath<Mesh>).ToArray();
            var shader=Shader.Find("MagicIsland/Village Path");
            Require(ground!=null&&material!=null&&paths.All(m=>m!=null)&&shader!=null,"missing authored village assets");
            Require(material.shader.name=="Standard"||material.shader==shader,"preserve custom path shader");
            if(material.shader.name=="Standard")
                Require(new[]{"_MainTex","_BumpMap","_DetailAlbedoMap","_DetailNormalMap","_EmissionMap","_MetallicGlossMap","_OcclusionMap","_ParallaxMap"}.All(n=>material.GetTexture(n)==null)
                    &&material.GetFloat("_Metallic")==0&&material.GetColor("_EmissionColor").maxColorComponent==0,"preserve textured/metallic/emissive authored paths");
            Require(ground.uv.Length==ground.vertexCount&&ground.colors.Length==ground.vertexCount,"ground requires authored grass weights/colours");
            var primary=paths.Select((mesh,kind)=>mesh.vertices.Select(v=>new Vector2(v.x/(kind==0?1:1.12f)+.5f,v.z/(kind==0?1:1.12f)+.5f)).ToArray()).ToArray();
            var shapes=paths.Select((mesh,kind)=>Enumerable.Repeat(new Vector2(kind,0),mesh.vertexCount).ToArray()).ToArray();
            for(int kind=0;kind<paths.Length;kind++)
            {
                Require(paths[kind].uv.Length==0||paths[kind].uv.SequenceEqual(primary[kind]),"preserve manually authored path UV0");
                Require(paths[kind].uv3.Length==0||paths[kind].uv3.SequenceEqual(shapes[kind]),"preserve manually authored path UV2");
            }
            var root=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);
            try
            {
                var terrain=root.GetComponent<IslandTerrain>();var cover=terrain.SeasonalDetails;
                Require(cover!=null&&cover.IsValid,"missing existing seasonal cover");
                var group=cover.transform.Find(GroupName);
                if(group!=null)Require(group.GetComponentsInChildren<SeasonalGroundPatch>(true).Length==6
                    &&group.GetComponentsInChildren<SeasonalGroundPatch>(true).All(cover.Patches.Contains),"preserve incomplete or manually changed verge references");
                var template=cover.Patches.FirstOrDefault(p=>p.Biome==IslandBiome.Grassland&&string.IsNullOrEmpty(p.TreeId)&&p.IsValid);
                Require(template!=null,"missing shared four-season grass template");
                Require(group!=null||cover.Patches.Length+6<=160&&cover.MaximumPatches+6<=160,"preserve the 160-patch budget");
                // All data is prepared before touching resources. The two path meshes keep geometry.
                var uv=ground.uv;var colors=ground.colors;var vertices=ground.vertices;
                var difference=new Color(.59f-.92f,.72f-.83f,.42f-.62f,0);bool recoloured=false;
                for(int i=0;i<vertices.Length;i++)
                {
                    var p=vertices[i];float angle=Mathf.Atan2(p.z,p.x),inset=(float)IslandLayout.CoastRadius(angle)-new Vector2(p.x,p.z).magnitude;
                    if(p.z>16||inset<1.3f||inset>4.7f)continue;
                    float offset=.36f*Mathf.Sin(angle*5+.7f)+.22f*Mathf.Sin(angle*11)
                        +(Mathf.PerlinNoise(p.x*.57f+61,p.z*.57f+37)-.5f)*.65f;
                    float grass=Mathf.SmoothStep(0,1,Mathf.InverseLerp(2+offset,4+offset,inset));
                    if(uv[i].x==grass)continue;
                    colors[i]+=difference*(grass-uv[i].x);uv[i].x=grass;recoloured=true;
                }
                if(recoloured){ground.colors=colors;ground.uv=uv;EditorUtility.SetDirty(ground);}
                for(int kind=0;kind<paths.Length;kind++)
                {
                    var mesh=paths[kind];
                    if(!mesh.uv.SequenceEqual(primary[kind])||!mesh.uv3.SequenceEqual(shapes[kind]))
                    {mesh.uv=primary[kind];mesh.uv3=shapes[kind];EditorUtility.SetDirty(mesh);}
                }
                if(material.shader!=shader){material.shader=shader;EditorUtility.SetDirty(material);}
                if(group==null)
                {
                    group=new GameObject(GroupName).transform;group.gameObject.layer=2;group.SetParent(cover.transform,false);
                    var patches=cover.Patches.ToList();
                    // Short asymmetric pockets leave the spawn, route centre and all farm sizes clear.
                    Vector2[] sites={new Vector2(-1.32f,-12.38f),new Vector2(3.17f,-11.61f),new Vector2(-1.64f,-10.14f),
                        new Vector2(3.41f,-9.48f),new Vector2(-1.42f,-8.68f),new Vector2(3.23f,-7.89f)};
                    for(int i=0;i<sites.Length;i++)
                    {
                        var go=Object.Instantiate(template.gameObject,group);go.name="Arrival grass "+i;go.layer=2;
                        go.transform.localPosition=new Vector3(sites[i].x,(float)IslandLayout.Height(sites[i].x,sites[i].y)+.012f,sites[i].y);
                        go.transform.localRotation=Quaternion.Euler(0,27+i*73,0);go.transform.localScale=Vector3.one*(.76f+(i%3)*.07f);
                        var patch=go.GetComponent<SeasonalGroundPatch>();patch.TreeId="";patch.Surface.enabled=false;patches.Add(patch);
                    }
                    cover.Patches=patches.ToArray();cover.MinimumPatches+=6;cover.MaximumPatches+=6;
                    PrefabUtility.SaveAsPrefabAsset(root,TerrainSetup.PrefabPath);Clean(TerrainSetup.PrefabPath);
                }
                AssetDatabase.SaveAssets();
                foreach(string path in PathMeshes.Concat(new[]{GroundPath,PathMaterial}))Clean(path);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            Debug.Log("Arrival route explicitly refined: original geometry/layout retained; shared path edges, grass-sand transition and six seasonal verge patches.");
        }
    }
}
