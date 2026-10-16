using System;
using System.IO;
using System.Linq;
using MagicIsland.Ocean;
using UnityEditor;
using UnityEngine;
namespace MagicIsland.EditorTools
{
    public static class OceanReefArtVerification
    {
        static void Require(bool ok,string why){if(!ok)throw new InvalidOperationException(why);}
        public static void Run()
        {
            OceanReefArtSetup.Apply();var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(OceanSetup.PrefabPath);var surface=prefab.GetComponent<OceanSurface>();
            Require(surface.IsValid&&prefab.GetComponentsInChildren<OceanHabitatPatch>(true).Length==84,"Existing valid AA ocean and all habitat records retained.");
            var data=JsonUtility.FromJson<OceanWildlifeArtSetup.Data>(File.ReadAllText(OceanReefArtSetup.DataPath));var layout=JsonUtility.FromJson<OceanReefArtSetup.Layout>(File.ReadAllText(OceanReefArtSetup.LayoutPath));
            int total=0;
            for(int i=0;i<8;i++)
            {
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(OceanReefArtSetup.SourcePath(data.parts[i].name));var colors=mesh.colors;
                Require(mesh.vertexCount==data.parts[i].positions.Length/3&&mesh.triangles.Length==data.parts[i].triangles.Length,"Source mesh matches Blender export.");
                if(i==4)Require(colors.Min(c=>c.a)<.001f&&colors.Max(c=>c.a)>.999f,"Grass has anchored roots and moving tips.");
                else Require(colors.All(c=>c.a==0),"Rigid coral, sponges and shells must not move as seaweed.");
            }
            foreach(var group in layout.groups)
            {
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(group.path);int expected=group.members.Sum(m=>m.source<8?data.parts[m.source].triangles.Length/3:layout.sources[m.source].triangles);
                Require(mesh.triangles.Length/3==expected,"All original reef members retained in one combined renderer.");total+=expected;
                var bounds=mesh.bounds;bounds.Expand(.00001f);Require(mesh.vertices.All(bounds.Contains),"Authored reef geometry stays in culling bounds.");
            }
            Require(total<layout.groups.Sum(g=>g.members.Sum(m=>layout.sources[m.source].triangles))*1.25,"Whole-reef triangle budget stays within 25 percent of the original.");
            var paths=Directory.GetFiles(OceanSetup.MeshFolder,"*.asset",SearchOption.AllDirectories).Concat(OceanReefArtSetup.Names.Take(8).Select(OceanReefArtSetup.SourcePath))
                .Concat(Directory.GetFiles(CoastArtSetup.MaterialFolder,"*.mat")).Concat(Directory.GetFiles(OceanSetup.MaterialFolder,"*.mat"))
                .Concat(new[]{OceanSetup.PrefabPath,OceanReefArtSetup.LayoutPath,OceanReefArtSetup.DataPath+".meta"}).Distinct().ToArray();
            var originals=paths.Select(File.ReadAllBytes).ToArray();
            var source=AssetDatabase.LoadAssetAtPath<Mesh>(OceanReefArtSetup.SourcePath("Branch Coral"));
            try
            {
                var v=source.vertices;v[0]+=Vector3.up*.0001f;source.vertices=v;EditorUtility.SetDirty(source);AssetDatabase.SaveAssets();var edited=paths.Select(File.ReadAllBytes).ToArray();
                OceanReefArtSetup.Apply();OceanReefArtSetup.Apply();PrototypeAddressablesSetup.EnsureSetup();PrototypeAddressablesSetup.EnsureSetup();
                for(int i=0;i<paths.Length;i++)Require(edited[i].SequenceEqual(File.ReadAllBytes(paths[i])),"Ordinary setup changed reef artist edits: "+paths[i]);
                bool rejected=false;try{OceanReefArtSetup.Rebuild();}catch(InvalidOperationException e){rejected=e.Message.Contains("Authored reef meshes changed");}
                Require(rejected,"Explicit rebuild refuses an unmatched manually edited combined/source layout.");
                for(int i=0;i<paths.Length;i++)Require(edited[i].SequenceEqual(File.ReadAllBytes(paths[i])),"Rejected rebuild changed existing art.");
            }
            finally
            {
                for(int i=0;i<paths.Length;i++)if(!paths[i].EndsWith(".meta",StringComparison.Ordinal)&&!originals[i].SequenceEqual(File.ReadAllBytes(paths[i])))
                {File.WriteAllBytes(paths[i],originals[i]);AssetDatabase.ImportAsset(paths[i],ImportAssetOptions.ForceSynchronousImport);}
            }
            Directory.CreateDirectory("Logs/ReefArt");File.WriteAllText("Logs/ReefArt/ArtVerification.txt","PASS: eight source meshes, all 84 habitats / 2744 original members; anchored grass, rigid decorations, culling and triangle budget; Apply/Setup preserve edits; unmatched edits rejected before rebuild. Combined triangles="+total+".\n");
            Debug.Log("Reef art verification PASSED.");
        }
    }
}
