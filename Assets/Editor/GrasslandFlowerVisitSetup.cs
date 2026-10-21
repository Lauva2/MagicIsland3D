using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MagicIsland.Ocean;
using MagicIsland.TerrainSystem;
using UnityEditor;
using UnityEngine;

namespace MagicIsland.EditorTools
{
    public static class GrasslandFlowerVisitSetup
    {
        static void Check(bool ok,string why){if(!ok)throw new InvalidOperationException("Flower visit: "+why);}
        public static MeadowFlowerVisit[] Plan(IslandTerrain t)
        {
            var e=t.World.GetComponentInChildren<WorldWaterEcology>(true);var s=t.SeasonalDetails.TouchSample;
            Check(t.IsValid&&s.ArtVersion==1&&s.Renderers.Length==6&&e.FlowerVisitVersion==0&&e.FlowerVisits.Length==0,"prepared sample, no custom/partial links");
            var butterflies=e.GrasslandAmbient.Single(r=>r.name=="Island meadow Butterfly");
            Check(butterflies.sharedMaterial.shader.name=="MagicIsland/Meadow Insects"&&butterflies.transform.localToWorldMatrix==t.transform.localToWorldMatrix,"original merged insect batch");
            var h=butterflies.GetComponent<MeshFilter>().sharedMesh.uv2;
            var slots=h.Select(p=>{int site=Mathf.FloorToInt((p.y+.0001f)/2.399963f);return site*2+Mathf.RoundToInt((p.y-site*2.399963f)/1.83f);})
                .Distinct().Where(i=>i%2==1).OrderBy(i=>i).Take(3).ToArray();Check(slots.Length==3,"three existing independent insects, no new population");
            var candidates=new List<(Renderer r,int v,Vector2 root)>();
            foreach(var r in s.Renderers)
            {
                var m=r.GetComponent<MeshFilter>().sharedMesh;var v=m.vertices;var uv=m.uv;var roots=m.uv2;var c=m.colors;var n=m.normals;var season=m.uv3;
                Check(r.transform.localToWorldMatrix==butterflies.transform.localToWorldMatrix&&r.sharedMaterial.shader.name==GrasslandFloraSetup.ShaderName
                    &&GrasslandShrubWindSetup.Done(AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(m)).userData),"preserve custom flower source/transform or unprepared art");
                var used=m.triangles.Distinct().ToHashSet();
                foreach(var group in used.GroupBy(i=>roots[i]))
                {
                    if(group.Any(i=>uv[i].x> -2||uv[i].x< -3)||Vector2.Distance(group.Key,s.Center)>s.AreaRadius-3
                        ||GrasslandIslandMeadowSetup.RouteDistance(group.Key)<3.8f)continue;
                    var pollen=group.Where(i=>c[i].r>.72f&&c[i].g>.45f&&c[i].g<.82f&&c[i].b<.55f&&season[i].y==0&&n[i].y>.3f
                        &&v[i].y-season[i].x>.16f&&v[i].y-season[i].x<.9f).OrderByDescending(i=>v[i].y).ToArray();
                    if(pollen.Length>0)candidates.Add((r,pollen[0],group.Key));
                }
            }
            var centers=new[]{s.Center+new Vector2(-10.5f,1),s.Center+new Vector2(12,-2),s.Center+new Vector2(-7,14.5f)};
            var result=new List<MeadowFlowerVisit>();var selected=new List<Vector2>();
            for(int i=0;i<centers.Length;i++)
            {
                var nearby=candidates.Where(p=>Vector2.Distance(p.root,centers[i])<6&&!selected.Any(q=>Vector2.Distance(q,p.root)<8)).OrderBy(p=>(p.root-centers[i]).sqrMagnitude).ToArray();
                Check(nearby.Length>0,"visible small flower in each existing band "+i);
                var best=nearby[0];selected.Add(best.root);result.Add(new MeadowFlowerVisit{Source=best.r,Vertex=best.v,Slot=slots[i]});
                Debug.Log("Flower visit link: slot="+slots[i]+", root="+best.root+", vertex="+best.v+", source="+best.r.name);
            }
            return result.ToArray();
        }
        [MenuItem("Magic Island/Art/Link Grassland Flower Visit Sample (Explicit)")]
        public static void Rebuild()
        {
            Check(!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling,"Edit Mode required");
            var backups=new Dictionary<string,byte[]>();var root=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);bool saved=false;
            try
            {
                var t=root.GetComponent<IslandTerrain>();var e=t.World.GetComponentInChildren<WorldWaterEcology>(true);
                if(e.FlowerVisitVersion==1){Check(e.IsValid,"preserve invalid/custom completed links");saved=true;Debug.Log("Flower visit links and manual edits retained.");return;}
                var plan=Plan(t);var mesh=e.GrasslandAmbient.Single(r=>r.name=="Island meadow Butterfly").GetComponent<MeshFilter>().sharedMesh;
                string meshPath=AssetDatabase.GetAssetPath(mesh);Check(meshPath==GrasslandIslandMeadowSetup.Folder+"/Island meadow Butterfly.asset"
                    &&AssetImporter.GetAtPath(meshPath).userData==GrasslandIslandMeadowSetup.Marker,"original butterfly mesh metadata");
                foreach(string p in new[]{TerrainSetup.PrefabPath,TerrainSetup.PrefabPath+".meta",meshPath,meshPath+".meta"})backups.Add(p,File.ReadAllBytes(p));
                e.FlowerVisitVersion=-1;PrefabUtility.SaveAsPrefabAsset(root,TerrainSetup.PrefabPath);
                var bounds=mesh.bounds;
                foreach(var link in plan){var point=link.Source.GetComponent<MeshFilter>().sharedMesh.vertices[link.Vertex];bounds.Encapsulate(point+Vector3.one*1.5f);bounds.Encapsulate(point-Vector3.one*1.5f);}
                if(bounds!=mesh.bounds){mesh.bounds=bounds;EditorUtility.SetDirty(mesh);AssetDatabase.SaveAssets();}
                e.FlowerVisits=plan;e.FlowerVisitVersion=1;Check(e.IsValid,"valid final links");
                Check(PrefabUtility.SaveAsPrefabAsset(root,TerrainSetup.PrefabPath)!=null,"saved link data");
                foreach(string p in new[]{TerrainSetup.PrefabPath,meshPath})File.WriteAllText(p,string.Join("\n",File.ReadAllLines(p).Select(l=>l.TrimEnd()))+"\n");
                saved=true;Debug.Log("Grassland flower visit prepared: 3 existing butterflies, 3 actual flower vertices; population and all flower geometry retained.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
                if(!saved){foreach(var pair in backups)File.WriteAllBytes(pair.Key,pair.Value);foreach(var p in backups.Keys.Where(p=>!p.EndsWith(".meta",StringComparison.Ordinal)))AssetDatabase.ImportAsset(p,ImportAssetOptions.ForceUpdate);}
            }
        }
    }
}
