using System;
using System.IO;
using System.Linq;
using MagicIsland.CalendarSystem;
using MagicIsland.Interaction;
using MagicIsland.Managers;
using MagicIsland.TerrainSystem;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using Object=UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    public static class SeasonalGroundVerification
    {
        static void Require(bool value,string why){if(!value)throw new Exception("Seasonal ground: "+why);}
        public static void VerifyAssets()
        {
            var root=AssetDatabase.LoadAssetAtPath<GameObject>(SeasonalDetailsSetup.PrefabPath);var cover=root.GetComponent<SeasonalGroundCover>();
            Require(cover.IsValid&&cover.Patches.Length>=30,"Baked, bounded collider-free cover");
            Require(cover.Patches.Length>=cover.MinimumPatches&&cover.Patches.Length<=cover.MaximumPatches,"Seeded count stays inside authored range");
            Require(cover.Patches.Select(p=>p.transform.localScale.x).Distinct().Count()>8&&cover.Patches.Select(p=>p.transform.localEulerAngles.y).Distinct().Count()>8,"Random scales and rotations");
            Require(cover.Patches.All(p=>Mathf.Abs(p.transform.localPosition.x-Mathf.Round(p.transform.localPosition.x))>.0001f),"Continuous random positions instead of grid slots");
            for(int a=0;a<cover.Patches.Length;a++)for(int b=a+1;b<cover.Patches.Length;b++)Require(Vector3.Distance(cover.Patches[a].transform.localPosition,cover.Patches[b].transform.localPosition)>1.149f,"Irregular scatter maintains minimum spacing");
            Require(AddressableAssetSettingsDefaultObject.Settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(SeasonalDetailsSetup.PrefabPath)).address==SeasonalGroundCover.Address,"Same-name AA address");
            Require(AssetDatabase.LoadAssetAtPath<GameObject>(TerrainSetup.PrefabPath).GetComponent<IslandTerrain>().SeasonalDetails!=null,"Terrain carries nested AA dependency");
            Require(!ShaderUtil.ShaderHasError(Shader.Find("MagicIsland/Seasonal Details")),"Detail shader compiles");
            foreach(var p in cover.Patches)
            {
                var local=p.transform.localPosition;double r=Math.Sqrt(local.x*local.x+local.z*local.z);
                Require(r+4.5<IslandLayout.CoastRadius(Math.Atan2(local.z,local.x)),"All baked details stay off sand/water");
                for(int s=0;s<4;s++)
                {
                    Require(p.Seasons[s].vertexCount<2500,"Small shared baked meshes");
                    foreach(var v in p.Seasons[s].vertices)Require(Mathf.Abs(v.x)+.17f<p.FootprintRadius&&Mathf.Abs(v.z)+.17f<p.FootprintRadius||v.y<1.9f&&Mathf.Abs(v.x)+.04f<p.FootprintRadius&&Mathf.Abs(v.z)+.04f<p.FootprintRadius,"Full wind/leaf envelope fits reserved patch");
                }
            }
        }
        public static string Placeable(GameManager g)=>g.Configuration.Crafting.PlacementEntries.First(r=>r.IsPlaceable&&!r.IsFurniture&&r.Clearance==0).OutputItemId;
        public static void VerifyPlacement(GameManager g)
        {
            var cover=g.Terrain.Terrain.SeasonalDetails;SeasonalGroundPatch target=null;
            foreach(var patch in cover.Patches.Where(p=>p.Surface.enabled))
            {
                var local=g.Terrain.Terrain.transform.InverseTransformPoint(patch.transform.position);
                g.Players.RestorePosition(patch.transform.position+new Vector3(0,.1f,-1.7f));Physics.SyncTransforms();
                g.Placements.SetCandidate(Mathf.RoundToInt(local.x),Mathf.RoundToInt(local.z));
                if(g.Placements.CheckCandidate()==PlacementResult.Success){target=patch;break;}
            }
            Require(target!=null,"Actual available construction site");
            Require(g.Placements.Confirm()&&!target.Surface.enabled,"Committed construction immediately hides covered ground");
            var actor=g.Placements.Objects.Values.Single();Require(g.Placements.Recover(actor)&&target.Surface.enabled,"Recover restores existing ground without spawning or removing player records");
            Require(!File.Exists(g.Saves.SavePath),"Scenery / placement changes never save daytime progress");
        }
        public static void VerifyRuntime(GameManager g)
        {
            var cover=g.Terrain.Terrain.SeasonalDetails;Require(cover!=null&&cover.VisibleCount>15,"Packed AA binds meaningful visible cover");
            VerifyMaximumFarm(g);
            foreach(Season season in Enum.GetValues(typeof(Season)))
            {
                cover.Present(season,true);
                Require(cover.Patches.All(p=>p.Filter.sharedMesh==p.Seasons[(int)season]),"Select baked seasonal mesh "+season);
            }
            cover.Present(Season.Autumn,true);
            var patch=cover.Patches.First(p=>p.Surface.enabled&&!string.IsNullOrEmpty(p.TreeId));
            var camera=g.Players.Player.ViewCamera;var before=camera.transform.position;int mask=camera.cullingMask;float size=camera.orthographicSize;Color background=camera.backgroundColor;
            int layer=patch.gameObject.layer;float windTime=Shader.GetGlobalFloat("_IslandWindTime");
            var canvases=Object.FindObjectsOfType<Canvas>();var enabled=canvases.Select(c=>c.enabled).ToArray();
            try
            {
                foreach(var c in canvases)c.enabled=false;
                patch.gameObject.layer=31;camera.cullingMask=1<<31;camera.backgroundColor=Color.black;camera.orthographicSize=1.35f;
                camera.transform.position=patch.transform.position+Vector3.up*.95f-camera.transform.forward*20;
                Shader.SetGlobalFloat("_IslandWindTime",2);var a=Capture(camera,"LeafA");Shader.SetGlobalFloat("_IslandWindTime",4);var b=Capture(camera,"LeafB");
                Require(a.Zip(b,(x,y)=>Mathf.Abs(x.r-y.r)+Mathf.Abs(x.g-y.g)+Mathf.Abs(x.b-y.b)>.03?1:0).Sum()>30,"Actual GPU leaf movement");
                var paused=Capture(camera,"LeafPause");Require(b.SequenceEqual(paused),"Same paused clock gives identical leaf render");
                var block=new MaterialPropertyBlock();patch.Surface.GetPropertyBlock(block);Require(block.GetFloat("_LeafEnabled")==1,"Leaves require existing canopy");
                var nodes=g.Gathering.State;int index=Enumerable.Range(0,nodes.Count).First(i=>nodes.Definitions[i].Id==patch.TreeId);
                for(int hit=0;hit<20&&!nodes.IsDepleted(index);hit++)nodes.Hit(index,MagicIsland.Farming.FarmTool.Axe,g.Inventory.State,g.Stamina.State);
                Require(nodes.IsDepleted(index),"Tree depletion fixture");patch.Surface.GetPropertyBlock(block);Require(block.GetFloat("_LeafEnabled")==0,"Chopped canopy immediately stops falling leaves");
            }
            finally
            {
                Shader.SetGlobalFloat("_IslandWindTime",windTime);patch.gameObject.layer=layer;camera.transform.position=before;camera.cullingMask=mask;camera.orthographicSize=size;camera.backgroundColor=background;
                for(int i=0;i<canvases.Length;i++)canvases[i].enabled=enabled[i];cover.Present(g.Calendar.Date.Season,true);
            }
        }
        static void VerifyMaximumFarm(GameManager g)
        {
            var terrain=g.Terrain.Terrain;var cover=terrain.SeasonalDetails;
            var fixture=new GameObject("Maximum farm ground fixture").AddComponent<PlacementManager>();
            var config=Object.Instantiate(g.Configuration.Config);config.FarmSize=12;
            var paths=Object.Instantiate(terrain.Paths,terrain.Paths.transform.parent);paths.Configure(12,g.Configuration.Housing);paths.gameObject.SetActive(false);
            try
            {
                fixture.Initialize(g.Configuration.Crafting,g.Inventory,config,g.Players,terrain.transform.parent,null);
                cover.Bind(fixture,g.Gathering.State,paths,terrain,g.Animals);cover.Present(Season.Spring,true);
                foreach(var p in cover.Patches)
                {
                    var point=terrain.transform.InverseTransformPoint(p.transform.position);
                    if(Mathf.Abs(point.x)<7.7f&&Mathf.Abs(point.z)<7.7f)Require(!p.Surface.enabled,"Maximum farm excludes ground cover");
                    if(!p.Surface.enabled)continue;
                    foreach(var segment in paths.Segments)
                    {
                        if(!segment.gameObject.activeSelf)continue;var local=segment.InverseTransformPoint(p.transform.position);
                        Require(Mathf.Abs(local.x)>.5f+.48f/segment.lossyScale.x||Mathf.Abs(local.z)>.5f+.48f/segment.lossyScale.z,"Maximum-layout paths stay clear");
                    }
                }
                var target=cover.Patches.First(p=>p.Surface.enabled);var pos=terrain.transform.InverseTransformPoint(target.transform.position);
                fixture.State.Restore(new[]{new SavedPlacedObject{Id=1,ItemId=Placeable(g),GridX=Mathf.RoundToInt(pos.x),GridZ=Mathf.RoundToInt(pos.z)}});
                cover.Bind(fixture,g.Gathering.State,paths,terrain,g.Animals);cover.Present(Season.Spring,true);
                Require(!target.Surface.enabled&&fixture.State.Count==1,"Restored player records hide scenery without deletion");
            }
            finally
            {
                cover.Bind(g.Placements,g.Gathering.State,terrain.Paths,terrain,g.Animals);cover.Present(g.Calendar.Date.Season,true);
                Object.DestroyImmediate(fixture.gameObject);Object.DestroyImmediate(config);Object.DestroyImmediate(paths.gameObject);
            }
        }
        static Color32[] Capture(Camera camera,string suffix)
        {
            var rt=new RenderTexture(960,540,24);var texture=new Texture2D(960,540,TextureFormat.RGB24,false);var old=camera.targetTexture;var active=RenderTexture.active;
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,960,540),0,0);texture.Apply();File.WriteAllBytes("Logs/SeasonGround"+suffix+".png",texture.EncodeToPNG());return texture.GetPixels32();}
            finally{camera.targetTexture=old;RenderTexture.active=active;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(texture);}
        }
    }
}
