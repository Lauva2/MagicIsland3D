using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using MagicIsland.Ocean;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    public static partial class OceanReefArtSetup
    {
        // Retain the full authored layout so repeated rebuilds can also restore previously thinned pieces.
        [MenuItem("Magic Island/Art/Rebuild Reef Density (Explicit)")]
        public static void RebuildDensity()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling,"Rebuild reef density in Edit Mode.");
            var layout=JsonUtility.FromJson<Layout>(File.ReadAllText(LayoutPath));
            var importer=AssetImporter.GetAtPath(DataPath);string old=importer.userData;
            Require(!old.StartsWith(Marker,StringComparison.Ordinal)||old.StartsWith(Marker+Hash(File.ReadAllText(DataPath))+":",StringComparison.Ordinal),"Blender reef source changed; rebuild its models before rebuilding density.");
            Require(layout.version==1&&layout.groups.Length==84&&layout.sources.Select(s=>s.name).SequenceEqual(Names),"Invalid reef layout.");
            Require(Digest(layout)==(old.StartsWith(Marker,StringComparison.Ordinal)?old.Split(':').Last():layout.originalDigest),"Authored reef meshes changed; preserve their member layout before rebuilding density.");
            if(layout.density==null)layout.density=new Density();
            var d=layout.density;
            Require(!float.IsNaN(d.sparseRetention)&&!float.IsNaN(d.richRetention)&&d.sparseRetention>=0&&d.sparseRetention<=d.richRetention&&d.richRetention<=1,"Invalid reef density settings.");
            var root=PrefabUtility.LoadPrefabContents(OceanSetup.PrefabPath);var prepared=new List<Mesh>();
            try
            {
                var surface=root.GetComponent<OceanSurface>();var patches=root.GetComponentsInChildren<OceanHabitatPatch>(true);
                var sources=Names.Select(n=>AssetDatabase.LoadAssetAtPath<Mesh>(SourcePath(n))).ToArray();
                Require(sources.All(m=>m!=null),"Missing reef source mesh.");
                var targets=layout.groups.Select(g=>AssetDatabase.LoadAssetAtPath<Mesh>(g.path)).ToArray();
                Require(targets.All(m=>m!=null)&&targets.Distinct().Count()==84,"Missing/duplicated habitat mesh.");
                var ordered=targets.Select(t=>patches.Single(p=>p.GetComponentInChildren<MeshFilter>().sharedMesh==t)).ToArray();
                bool first=layout.originalObstacles==null||layout.originalObstacles.Length==0;
                if(first)layout.originalObstacles=surface.SwimObstacles.ToArray();
                var previousHidden=new List<Vector2>();
                for(int i=0;i<layout.groups.Length;i++)
                {
                    if(first)layout.groups[i].terrace=ordered[i].LargeCorals>0;
                    foreach(var member in layout.groups[i].members.Where(m=>m.source<4||m.source==5||m.source==8))
                    {
                        Vector3 world=ordered[i].transform.TransformPoint(member.position);var center=new Vector2(world.x,world.z);
                        Require(layout.originalObstacles.Any(o=>Vector2.Distance(o.Center,center)<.002f),"Reef instance layout differs from its recorded obstacles; preserve manual edits.");
                        if(member.hidden)previousHidden.Add(center);
                    }
                }
                var expected=layout.originalObstacles.Where(o=>!previousHidden.Any(c=>Vector2.Distance(c,o.Center)<.002f)).ToArray();
                Require(expected.SequenceEqual(surface.SwimObstacles),"Swim obstacles changed; preserve manual edits before rebuilding density.");
                var hiddenSolids=new List<Vector2>();
                for(int i=0;i<layout.groups.Length;i++)
                {
                    var group=layout.groups[i];var patch=ordered[i];var random=new System.Random(unchecked(d.seed+i*7919));
                    float angle=Mathf.Atan2(patch.transform.localPosition.z,patch.transform.localPosition.x),phase=(d.seed%997)*.013f;
                    float field=.5f+.30f*Mathf.Sin(angle*3+phase)+.16f*Mathf.Sin(angle*7+phase*.71f);
                    float blend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.38f,.62f,field));
                    float retention=Mathf.Lerp(d.sparseRetention,d.richRetention,blend);
                    foreach(var member in group.members)member.hidden=random.NextDouble()>retention;
                    if(group.members.All(m=>m.hidden))
                        (group.members.FirstOrDefault(m=>m.source==4||m.source==6||m.source==7)??group.members[0]).hidden=false;
                    foreach(var member in group.members.Where(m=>m.hidden&&(m.source<4||m.source==5||m.source==8)))
                    {
                        Vector3 world=patch.transform.TransformPoint(member.position);
                        var center=new Vector2(world.x,world.z);
                        Require(layout.originalObstacles.Any(o=>Vector2.Distance(o.Center,center)<.002f),"Missing original reef obstacle; preserve manual edits.");
                        hiddenSolids.Add(center);
                    }
                    var combined=Combine(group,sources);combined.name=targets[i].name;prepared.Add(combined);
                }
                // Only remove envelopes. Existing fish homes, passages and terrain retain their original safety.
                var obstacles=layout.originalObstacles.Where(o=>!hiddenSolids.Any(c=>Vector2.Distance(c,o.Center)<.002f)).ToArray();
                for(int i=0;i<targets.Length;i++)
                {
                    EditorUtility.CopySerialized(prepared[i],targets[i]);EditorUtility.SetDirty(targets[i]);
                    var p=ordered[i];var visible=layout.groups[i].members.Where(m=>!m.hidden).ToArray();
                    p.Corals=visible.Count(m=>m.source<4);p.Grasses=visible.Count(m=>m.source==4);p.Sponges=visible.Count(m=>m.source==5);
                    p.Stars=visible.Count(m=>m.source==6);p.Shells=visible.Count(m=>m.source==7);p.Rocks=visible.Count(m=>m.source==8);
                    bool terrace=layout.groups[i].terrace;p.LargeCorals=terrace?p.Corals:0;p.SmallCorals=terrace?0:p.Corals;
                }
                surface.SwimObstacles=obstacles;
                SaveDensityPrefab(root);AssetDatabase.SaveAssets();
                File.WriteAllText(LayoutPath,JsonUtility.ToJson(layout));AssetDatabase.ImportAsset(LayoutPath,ImportAssetOptions.ForceSynchronousImport);
                importer.userData=Marker+Hash(File.ReadAllText(DataPath))+":"+Hash(File.ReadAllText(LayoutPath))+":"+Digest(layout);importer.SaveAndReimport();
                Debug.Log("Reef density rebaked: "+layout.groups.Sum(g=>g.members.Count(m=>!m.hidden))+" visible / "+layout.groups.Sum(g=>g.members.Length)+" authored members; fish, wildlife and geography retained.");
            }
            finally{foreach(var mesh in prepared)Object.DestroyImmediate(mesh);PrefabUtility.UnloadPrefabContents(root);}
        }
        static void SaveDensityPrefab(GameObject root)
        {
            // Unity also serializes newly introduced default fields on unrelated bird/crab
            // components. Keep their exact authored documents, including intentionally absent fields.
            const string pattern=@"(?ms)^--- !u!\d+ &(?<id>-?\d+)[^\n]*\n.*?(?=^--- !u!|\z)";
            string path=OceanSetup.PrefabPath,before=File.ReadAllText(path);
            var documents=Regex.Matches(before,pattern).Cast<Match>().ToDictionary(m=>m.Groups["id"].Value,m=>m.Value);
            string[] owned={AssetDatabase.AssetPathToGUID("Assets/Scripts/Ocean/OceanSurface.cs"),AssetDatabase.AssetPathToGUID("Assets/Scripts/Ocean/OceanHabitatPatch.cs")};
            PrefabUtility.SaveAsPrefabAsset(root,path);string saved=File.ReadAllText(path);
            var matches=Regex.Matches(saved,pattern).Cast<Match>().ToArray();
            Require(matches.Length==documents.Count&&matches.All(m=>documents.ContainsKey(m.Groups["id"].Value)),"Density rebuild must preserve every prefab object and fileID.");
            string merged=Regex.Replace(saved,pattern,m=>owned.Any(g=>m.Value.Contains("guid: "+g))?m.Value:documents[m.Groups["id"].Value]);
            if(merged!=saved){File.WriteAllText(path,merged);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);}
        }
    }
}
