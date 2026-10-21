using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MagicIsland.TerrainSystem;
using UnityEditor;
using UnityEngine;

namespace MagicIsland.EditorTools
{
    // Explicit offline polish of the existing sample. No runtime objects or channels are added.
    public static class GrasslandSampleFinishSetup
    {
        public const string Marker="MagicIsland.GrasslandSampleFinish.v1", Pending="MagicIsland.GrasslandSampleFinish.Pending.v1";
        static readonly HashSet<string> Known=new HashSet<string>
        {
            GrasslandRevitalizationSetup.Marker,GrasslandIslandMeadowSetup.Marker,GrasslandGroveSetup.Marker,
            GrasslandFloraSetup.Marker,GrasslandGroundLayersSetup.Marker,GrasslandShrubStoneSetup.Marker,
            GrasslandTouchSetup.Marker,GrasslandReadabilitySetup.Marker,GrasslandPatchSetup.Marker,
            GrasslandBlendSetup.Marker,GrasslandWildflowerSetup.Marker,GrasslandEdgeSetup.Marker,GrasslandPathEdgeSetup.Marker
        };
        public sealed class Edit
        {
            public Mesh Mesh; public Color[] Colors; public int[] Indices;
            public bool[] Changed,Removed; public string Metadata; public int Thinned;
        }
        public static bool Done(string data)=>data.Split(';').Contains(Marker);
        static void Check(bool ok,string why){if(!ok)throw new InvalidOperationException("Sample finish: "+why);}
        static bool Grass(float w)=>w>=-5&&w<=-4;
        static bool Flower(float w)=>w>=-3&&w<=-2;
        public static Edit[] Plan(IslandTerrain t)
        {
            var s=t.SeasonalDetails.TouchSample;
            Check(t.IsValid&&s.ArtVersion==1&&s.Renderers.Length==6,"original complete sample");
            var collision=new HashSet<Mesh>(t.GetComponentsInChildren<MeshCollider>(true).Select(c=>c.sharedMesh));
            var flowers=new List<Vector2>();var grass=new List<Vector2>();
            foreach(var r in s.Renderers)
            {
                var m=r.GetComponent<MeshFilter>().sharedMesh;string path=AssetDatabase.GetAssetPath(m);
                string data=AssetImporter.GetAtPath(path).userData;
                Check(path.StartsWith(BiomeIslandsSetup.Folder+"/",StringComparison.Ordinal)&&!collision.Contains(m)
                    &&r.transform.localToWorldMatrix==t.transform.localToWorldMatrix&&m.subMeshCount==1
                    &&m.tangents.Length==0&&m.blendShapeCount==0&&m.bindposes.Length==0&&m.boneWeights.Length==0,"original static visual mesh");
                Check(r.sharedMaterials.Length==1&&r.sharedMaterial.shader.name==GrasslandFloraSetup.ShaderName
                    &&GrasslandEdgeSetup.Done(data)&&data.Split(';').All(Known.Contains),"preserve custom material / metadata");
                Check(Enumerable.Range(0,3).All(ch=>m.GetVertexAttributeDimension((UnityEngine.Rendering.VertexAttribute)
                    ((int)UnityEngine.Rendering.VertexAttribute.TexCoord0+ch))==2),"preserve custom UV dimensions");
                Check(Enumerable.Range(3,5).All(ch=>{var uv=new List<Vector4>();m.GetUVs(ch,uv);return uv.Count==0;}),"preserve custom UV channels");
                var v=m.vertices;var c=m.colors;var a=m.uv;var roots=m.uv2;var season=m.uv3;
                Check(new[]{m.normals.Length,c.Length,a.Length,roots.Length,season.Length}.All(n=>n==v.Length),"complete original attributes");
                foreach(var g in m.triangles.Distinct().GroupBy(i=>roots[i]))
                {
                    if(g.All(i=>Grass(a[i].x)))grass.Add(g.Key);
                    if(g.Count(i=>Flower(a[i].x))>288)flowers.Add(g.Key);
                }
            }
            var edits=new List<Edit>();
            foreach(var r in s.Renderers)
            {
                var m=r.GetComponent<MeshFilter>().sharedMesh;var v=m.vertices;var original=m.colors;var colors=(Color[])original.Clone();
                var uv=m.uv;var roots=m.uv2;var season=m.uv3;var faces=m.triangles;
                var e=new Edit{Mesh=m,Colors=colors,Changed=new bool[v.Length],Removed=new bool[v.Length],Metadata=AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(m)).userData};
                foreach(var group in faces.Distinct().GroupBy(i=>roots[i]))
                {
                    var p=group.Key;float d=Vector2.Distance(p,s.Center);
                    float fade=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(s.AreaRadius-s.Feather,s.AreaRadius-1.3f,d));
                    if(fade<=0)continue;
                    // Clear only crowded pure-grass roots around the remaining large decorative bouquets.
                    // Mixed shrub/stone/flower roots remain intact; all cuts retain original whole-root ownership.
                    if(fade>.99f&&group.All(i=>Grass(uv[i].x))&&flowers.Any(f=>Vector2.Distance(f,p)<1.65f)
                        &&grass.Count(q=>Vector2.Distance(p,q)<1.8f)>5
                        &&GrasslandGroundLayersSetup.Noise(p,1.17f,109)<.47f)
                    {foreach(int i in group)e.Removed[i]=true;e.Thinned++;continue;}
                    foreach(int i in group)
                    {
                        if(Vector2.Distance(new Vector2(v[i].x,v[i].z),s.Center)>=s.AreaRadius)continue;
                        var c=original[i];Color target=c;float amount=0;
                        if(Grass(uv[i].x))
                        {
                            // Keep the earlier ground-matched root paint, soften the exposed blade tips.
                            float tip=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.025f,.22f,v[i].y-season[i].x));
                            target=new Color(.55f,.64f,.42f,c.a);amount=.26f*tip;
                        }
                        else if(Flower(uv[i].x)&&season[i].y<.01f)
                        {
                            bool lilac=c.b>c.g+.025f;
                            target=lilac?new Color(.78f,.73f,.83f,c.a)
                                :c.g<c.r-.065f?new Color(.91f,.83f,.57f,c.a):new Color(.94f,.915f,.81f,c.a);
                            amount=.62f;
                        }
                        else if(season[i].y>.99f&&uv[i].x>=0)
                        {
                            float range=Mathf.Max(c.r,c.g,c.b)-Mathf.Min(c.r,c.g,c.b);
                            target=range<.12f?new Color(.58f,.61f,.53f,c.a):new Color(.47f,.57f,.38f,c.a);
                            amount=range<.12f?.20f:.16f;
                        }
                        else if(Flower(uv[i].x)) {target=new Color(.48f,.59f,.37f,c.a);amount=.16f;}
                        colors[i]=Color.Lerp(c,target,amount*fade);colors[i].a=c.a;e.Changed[i]=colors[i]!=c;
                    }
                }
                var kept=new List<int>(faces.Length);
                for(int i=0;i<faces.Length;i+=3)
                {
                    bool cut=e.Removed[faces[i]];
                    Check(cut==e.Removed[faces[i+1]]&&cut==e.Removed[faces[i+2]],"whole grass roots only");
                    if(!cut)kept.AddRange(new[]{faces[i],faces[i+1],faces[i+2]});
                }
                e.Indices=kept.ToArray();edits.Add(e);
            }
            Check(edits.Sum(e=>e.Changed.Count(b=>b))>1000,"visible bounded palette pass");return edits.ToArray();
        }
        static void Clean(string p)=>File.WriteAllText(p,string.Join("\n",File.ReadAllLines(p).Select(l=>l.TrimEnd()))+"\n");
        [MenuItem("Magic Island/Art/Finish Grassland Sample Palette (Explicit)")]
        public static void Rebuild()
        {
            Check(!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling,"Edit Mode required");
            var root=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);var backups=new Dictionary<string,byte[]>();bool saved=false;
            try
            {
                var t=root.GetComponent<IslandTerrain>();var paths=t.SeasonalDetails.TouchSample.Renderers.Select(r=>AssetDatabase.GetAssetPath(r.GetComponent<MeshFilter>().sharedMesh)).ToArray();
                Check(paths.Length==6,"complete sample");var data=paths.Select(p=>AssetImporter.GetAtPath(p).userData).ToArray();
                Check(!data.Any(d=>d.Split(';').Contains(Pending)),"preserve interrupted write");
                if(data.Any(Done)){Check(data.All(Done),"preserve partial migration");saved=true;Debug.Log("Grassland sample finish and later manual edits retained.");return;}
                var plan=Plan(t);
                foreach(string p in paths.SelectMany(p=>new[]{p,p+".meta"}))backups.Add(p,File.ReadAllBytes(p));
                var first=AssetImporter.GetAtPath(paths[0]);first.userData=data[0]+";"+Pending;first.SaveAndReimport();
                foreach(var e in plan){e.Mesh.colors=e.Colors;e.Mesh.SetTriangles(e.Indices,0,false);EditorUtility.SetDirty(e.Mesh);}
                AssetDatabase.SaveAssets();
                foreach(var e in plan.Skip(1).Concat(plan.Take(1)))
                {string p=AssetDatabase.GetAssetPath(e.Mesh);var importer=AssetImporter.GetAtPath(p);importer.userData=e.Metadata+";"+Marker;importer.SaveAndReimport();Clean(p);Clean(p+".meta");}
                saved=true;Debug.Log("Grassland sample finish baked: "+plan.Sum(e=>e.Changed.Count(b=>b))+" softened vertices, "+plan.Sum(e=>e.Thinned)+" crowded grass roots thinned.");
            }
            finally
            {PrefabUtility.UnloadPrefabContents(root);if(!saved){foreach(var p in backups)File.WriteAllBytes(p.Key,p.Value);foreach(string p in backups.Keys.Where(p=>!p.EndsWith(".meta",StringComparison.Ordinal)))AssetDatabase.ImportAsset(p,ImportAssetOptions.ForceUpdate);}}
        }
    }
}
