using System;
using System.Linq;
using MagicIsland.Interaction;
using MagicIsland.TerrainSystem;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    public static partial class CoastArtSetup
    {
        [MenuItem("Magic Island/Rebuild Coastal Details")]
        public static void RebuildCoastalDetails()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            var root=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);
            try
            {
                var terrain=root.GetComponent<IslandTerrain>();
                if(terrain.CoastalScenery==null)throw new InvalidOperationException("Missing coastal scenery.");
                if(terrain.DetailScatter==null)terrain.DetailScatter=new CoastalDetailScatter();
                ApplyCoastalDetails(terrain);
                PrefabUtility.SaveAsPrefabAsset(root,TerrainSetup.PrefabPath);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        static void ApplyCoastalDetails(IslandTerrain terrain)
        {
            var palms=terrain.CoastalScenery.GetComponentsInChildren<CoastalSceneryPatch>(true)
                .Select(p=>new PalmSite(p.transform.localPosition.x,p.transform.localPosition.z,p.transform.localEulerAngles.y,p.transform.localScale.x)).ToArray();
            var plan=terrain.DetailScatter.CreatePlan(palms);
            string[] names={"CoastalShrub","BeachShell","ReefRock"};
            var prefabs=names.Select(n=>AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder+"/"+n+".prefab")).ToArray();
            if(prefabs.Any(p=>p==null))throw new InvalidOperationException("Missing coastal detail prefab; existing scenery preserved.");
            for(int kind=0;kind<names.Length;kind++)
            {
                var existing=terrain.CoastalScenery.Cast<Transform>().Where(t=>
                    PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject)==prefabs[kind]).ToArray();
                var sites=plan.Where(s=>s.Kind==kind).ToArray();
                for(int i=0;i<sites.Length;i++)
                {
                    var site=sites[i];var t=i<existing.Length?existing[i]:((GameObject)PrefabUtility.InstantiatePrefab(prefabs[kind],terrain.CoastalScenery)).transform;
                    t.localPosition=new Vector3((float)site.X,(float)IslandLayout.Height(site.X,site.Z)+(kind==1?.012f:kind==2?-.07f:0),(float)site.Z);
                    t.localRotation=Quaternion.Euler(0,(float)site.Yaw,0);
                    t.localScale=Vector3.one*(float)site.Scale;
                    if(kind==2)t.localScale=Vector3.Scale(t.localScale,new Vector3(1,.72f,.84f));
                }
                for(int i=sites.Length;i<existing.Length;i++)Object.DestroyImmediate(existing[i].gameObject);
            }
        }
        [MenuItem("Magic Island/Rebuild Coastal Ground Colours")]
        public static void RebuildCoastalGroundColours()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            foreach(string path in new[]{TerrainSetup.MeshFolder+"/Island Land.asset",OceanSetup.MeshFolder+"/Island Shore.asset"})
            {
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(mesh==null)throw new InvalidOperationException("Missing coastal ground mesh.");
                var vertices=mesh.vertices;var colors=mesh.colors;
                if(colors.Length!=vertices.Length)throw new InvalidOperationException("Preserve ground with unmatched vertex colours.");
                for(int i=0;i<vertices.Length;i++)
                {
                    var v=vertices[i];float angle=Mathf.Atan2(v.z,v.x),radius=new Vector2(v.x,v.z).magnitude,edge=OceanSetup.Radius(angle);
                    // Keep the entire original walkable shape, heights, triangles and underwater colours.
                    if(radius<=edge-.45f)colors[i]=LandColor(radius,edge,angle);
                }
                mesh.colors=colors;EditorUtility.SetDirty(mesh);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
