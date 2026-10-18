using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MagicIsland.Interaction;
using MagicIsland.Managers;
using MagicIsland.TerrainSystem;
using UnityEditor;
using UnityEngine;

namespace MagicIsland.EditorTools
{
    public static class ArrivalRouteVerification
    {
        static void Require(bool ok,string why){if(!ok)throw new InvalidOperationException("Arrival route verification: "+why);}
        static string[] Edited=>ArrivalRouteSetup.PathMeshes.Concat(new[]{ArrivalRouteSetup.GroundPath,ArrivalRouteSetup.PathMaterial,TerrainSetup.PrefabPath}).ToArray();
        static Dictionary<string,string> Hierarchy(GameObject root)
        {
            string Key(Transform t)=>t.parent==null?t.name:Key(t.parent)+"/"+t.name+":"+t.GetSiblingIndex();
            return root.GetComponentsInChildren<Transform>(true).ToDictionary(Key,t=>
                t.localPosition.ToString("R")+t.localRotation.ToString("R")+t.localScale.ToString("R")+t.gameObject.activeSelf+"|"+t.gameObject.layer+"|"+
                string.Join(",",t.GetComponents<Component>().Select(c=>c.GetType().FullName)));
        }
        public static void BakeAndVerify()
        {
            Require(Application.dataPath.Replace('\\','/').Contains("/Logs/"),"use an isolated project");
            var original=Hierarchy(AssetDatabase.LoadAssetAtPath<GameObject>(TerrainSetup.PrefabPath));
            var meshes=ArrivalRouteSetup.PathMeshes.Concat(new[]{ArrivalRouteSetup.GroundPath}).Select(AssetDatabase.LoadAssetAtPath<Mesh>).ToArray();
            var vertices=meshes.Select(m=>m.vertices).ToArray();var triangles=meshes.Select(m=>m.triangles).ToArray();var normals=meshes.Select(m=>m.normals).ToArray();
            string[] protectedPaths={OceanSetup.PrefabPath,OceanSeabedSlopeSetup.FloorPath,OceanSeabedSlopeSetup.FloorPath+".meta",VillageArtSetup.PathsPath,
                "Assets/Config/GameConfig.asset","Assets/Config/HousingConfig.asset","Assets/Art/Materials/Ocean/Water.mat"};
            var protectedBytes=protectedPaths.Select(File.ReadAllBytes).ToArray();
            var pathMaterial=AssetDatabase.LoadAssetAtPath<Material>(ArrivalRouteSetup.PathMaterial);var color=pathMaterial.color;float gloss=pathMaterial.GetFloat("_Glossiness");
            ArrivalRouteSetup.Rebuild();
            var current=Hierarchy(AssetDatabase.LoadAssetAtPath<GameObject>(TerrainSetup.PrefabPath));
            foreach(var pair in original)Require(current.TryGetValue(pair.Key,out var value)&&pair.Value==value,"original instance transform/components/activation retained: "+pair.Key);
            Require(current.Count==original.Count+7,"six small patches and one grouping transform only");
            for(int i=0;i<meshes.Length;i++)Require(vertices[i].SequenceEqual(meshes[i].vertices)&&triangles[i].SequenceEqual(meshes[i].triangles)&&normals[i].SequenceEqual(meshes[i].normals),"exact original ground/path geometry");
            Require(pathMaterial.color==color&&pathMaterial.GetFloat("_Glossiness")==gloss,"original path tint and gloss retained");
            var paths=Edited;var bytes=paths.Select(File.ReadAllBytes).ToArray();
            ArrivalRouteSetup.Rebuild();for(int i=0;i<paths.Length;i++)Require(bytes[i].SequenceEqual(File.ReadAllBytes(paths[i])),"repeat explicit bake is byte stable: "+paths[i]);
            PrototypeAddressablesSetup.EnsureSetup();for(int i=0;i<paths.Length;i++)Require(bytes[i].SequenceEqual(File.ReadAllBytes(paths[i])),"Setup preserves authored route: "+paths[i]);
            for(int i=0;i<protectedPaths.Length;i++)Require(protectedBytes[i].SequenceEqual(File.ReadAllBytes(protectedPaths[i])),"protected coast/layout/config retained: "+protectedPaths[i]);
            VerifyManualEdits(paths,bytes);
            Require(!ShaderUtil.ShaderHasError(Shader.Find("MagicIsland/Village Path"))&&!ShaderUtil.ShaderHasError(Shader.Find("MagicIsland/Island Biome Ground")),"route shaders compile");
            Directory.CreateDirectory("Logs/ArrivalRoute");File.WriteAllText("Logs/ArrivalRoute/Bake.txt","PASS: original hierarchy/transforms/activation/components and ground/path geometry retained; six shared seasonal patches; original tint/gloss; repeat/Setup/manual edits and UV guard verified.\n");
            Debug.Log("Arrival route bake verification PASSED.");
        }
        static void VerifyManualEdits(string[] paths,byte[][] bytes)
        {
            try
            {
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(ArrivalRouteSetup.GroundPath);var colors=mesh.colors;colors[20]=Color.magenta;mesh.colors=colors;EditorUtility.SetDirty(mesh);
                var material=AssetDatabase.LoadAssetAtPath<Material>(ArrivalRouteSetup.PathMaterial);material.SetFloat("_EdgeSoftness",.19f);EditorUtility.SetDirty(material);AssetDatabase.SaveAssets();
                var tuned=paths.Select(File.ReadAllBytes).ToArray();PrototypeAddressablesSetup.EnsureSetup();
                for(int i=0;i<paths.Length;i++)Require(tuned[i].SequenceEqual(File.ReadAllBytes(paths[i])),"ordinary Setup preserves manual colour and road softness");
                var strip=AssetDatabase.LoadAssetAtPath<Mesh>(ArrivalRouteSetup.PathMeshes[0]);var uv=strip.uv;uv[0].x+=.1f;strip.uv=uv;EditorUtility.SetDirty(strip);AssetDatabase.SaveAssets();
                var edited=paths.Select(File.ReadAllBytes).ToArray();bool rejected=false;
                try{ArrivalRouteSetup.Rebuild();}catch(InvalidOperationException e){rejected=e.Message.Contains("authored path UV0");}
                Require(rejected,"explicit bake refuses custom UVs before changing other assets");
                for(int i=0;i<paths.Length;i++)Require(edited[i].SequenceEqual(File.ReadAllBytes(paths[i])),"rejected bake preserves all files");
            }
            finally
            {
                for(int i=0;i<paths.Length;i++){File.WriteAllBytes(paths[i],bytes[i]);AssetDatabase.ImportAsset(paths[i],ImportAssetOptions.ForceSynchronousImport);}
            }
        }
        public static void VerifyRuntime(GameManager g)
        {
            var terrain=g.Terrain.Terrain;var paths=terrain.Paths;var renderer=terrain.Ground.GetComponent<Renderer>();
            var patches=terrain.SeasonalDetails.Patches.Where(p=>p.name.StartsWith("Arrival grass ",StringComparison.Ordinal)).ToArray();
            Require(patches.Length==6&&patches.All(p=>p.IsValid&&p.GetComponentsInChildren<Collider>(true).Length==0),"six collider-free AA seasonal patches");
            Require(terrain.SeasonalDetails.Patches.Length<=160&&terrain.SeasonalDetails.ScatterValid,"existing bounded cover pool");
            Require(patches.Count(p=>p.Surface.enabled)>=2,"arrival grass is actually visible after gameplay clearances");
            string material=EditorJsonUtility.ToJson(renderer.sharedMaterial);
            for(int farm=1;farm<=12;farm++)
            {
                g.Terrain.ConfigurePaths(farm,g.Configuration.Housing);var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);var guide=block.GetVector("_VillageApproach");
                var start=terrain.transform.TransformPoint(terrain.Biomes.NewGameSpawn);
                Require(Vector2.Distance(new Vector2(guide.x,guide.y),new Vector2(start.x,start.z))<.001f,"guide starts at AA authored spawn");
                var end=new Vector3(guide.z,0,guide.w);var local=paths.Segments[1].InverseTransformPoint(end);
                Require(new Vector2(guide.z-guide.x,guide.w-guide.y).sqrMagnitude>1,"a clear arrival route is available");
                Require(Mathf.Abs(local.x)<.001f&&Mathf.Abs(local.z)<=.5001f,"guide joins actual configured lane for farm "+farm);
                float bend=block.GetFloat("_VillageApproachCurve");var direction=end-start;direction.y=0;var side=new Vector3(-direction.z,0,direction.x).normalized;
                for(int i=0;i<=24;i++)
                {
                    float t=i/24f;var p=Vector3.Lerp(start,end,t)+side*(Mathf.Sin(t*Mathf.PI)*bend);var q=terrain.transform.InverseTransformPoint(p);
                    Require(IslandLayout.ContainsLand(q.x,q.z,.75),"complete visual trail stays on dry original land");
                    float half=farm*.6f+.5f;
                    Require(!(Mathf.Abs(q.x)<half+.75f&&Mathf.Abs(q.z)<half+.75f),"trail avoids full farm footprint");
                    Require(g.Terrain.TryResolve(p,out var ground),"actual guide floor");
                    foreach(var hit in Physics.OverlapCapsule(ground+Vector3.up*.35f,ground+Vector3.up*1.15f,.3f,~0,QueryTriggerInteraction.Ignore))
                        Require(terrain.IsGround(hit)||hit==terrain.Boundary||hit is CharacterController||hit.GetComponentInParent<NPCActor>()!=null,"guide avoids actual solid collider "+hit.name);
                }
            }
            g.Terrain.ConfigurePaths(g.Configuration.Config.FarmSize,g.Configuration.Housing);
            var active=new MaterialPropertyBlock();renderer.GetPropertyBlock(active);Debug.Log("Arrival bend chosen: "+active.GetFloat("_VillageApproachCurve"));
            Require(material==EditorJsonUtility.ToJson(renderer.sharedMaterial),"per-instance guide never writes shared ground material");
            Debug.Log("Arrival route runtime verification PASSED: actual spawn/lane and all 12 farm sizes, AA grass and shared material preservation.");
        }
        public static void BeginPlacement(GameManager g)
        {
            string id=SeasonalGroundVerification.Placeable(g);Require(g.Inventory.TryAdd(id,1),"fixture inventory capacity");
            g.Placements.StartCoroutine(g.Placements.Begin(id));
        }
        public static void VerifyPlacement(GameManager g)
        {
            var patches=g.Terrain.Terrain.SeasonalDetails.Patches.Where(p=>p.name.StartsWith("Arrival grass ",StringComparison.Ordinal)&&p.Surface.enabled).ToArray();
            SeasonalGroundPatch chosen=null;
            foreach(var p in patches)
            {
                var local=g.Terrain.Terrain.transform.InverseTransformPoint(p.transform.position);
                g.Players.RestorePosition(p.transform.position+Vector3.back*1.7f);Physics.SyncTransforms();
                g.Placements.SetCandidate(Mathf.RoundToInt(local.x),Mathf.RoundToInt(local.z));
                if(g.Placements.CheckCandidate()==PlacementResult.Success){chosen=p;break;}
            }
            Require(chosen!=null,"one actual arrival verge can accept existing player construction");
            Require(g.Placements.Confirm()&&!chosen.Surface.enabled,"construction hides arrival grass immediately");
            var actor=g.Placements.Objects.Values.Single();Require(g.Placements.Recover(actor)&&chosen.Surface.enabled,"recovery restores grass using existing instance");
            g.Placements.Cancel();Debug.Log("Arrival route construction priority verification PASSED.");
        }
    }
}
