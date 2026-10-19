using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MagicIsland.Interaction;
using MagicIsland.TerrainSystem;
using UnityEditor;
using UnityEngine;

namespace MagicIsland.EditorTools
{
    // In-place, explicit art pass. Root ownership and all original vertex channels remain intact.
    public static class GrasslandShrubStoneSetup
    {
        public const string Marker="MagicIsland.GrasslandShrubStone.v1";
        public sealed class Edit
        {
            public Mesh Mesh;public Vector3[] Vertices,Normals;public Color[] Colors;public bool[] Changed;
            public string Metadata;public Vector2[] Roots;public int Stones,Lobes;
        }
        static readonly Color[] RockColors={new Color(.48f,.61f,.32f),new Color(.66f,.69f,.56f)};
        public static bool Candidate(string name)=>name.StartsWith("Art Meadow shrubs ",StringComparison.Ordinal)||name.StartsWith("Island meadow cover ",StringComparison.Ordinal)||name.StartsWith("Grove understory ",StringComparison.Ordinal);
        public static bool Done(string metadata)=>metadata.Split(';').Contains(Marker);
        public static void Check(bool ok,string why){if(!ok)throw new InvalidOperationException("Grassland shrubs/stones: "+why);}
        static bool Same(Color a,Color b)=>Mathf.Abs(a.r-b.r)+Mathf.Abs(a.g-b.g)+Mathf.Abs(a.b-b.b)<.00015f;
        static float Noise(Vector2 p,float salt)=>GrasslandGroundLayersSetup.Noise(p,.173f,salt);
        static bool Stone(Color c,bool grove)=>RockColors.Any(original=>Same(c,grove?Color.Lerp(original,new Color(.44f,.55f,.35f),.24f):original));
        static bool Leaf(Color c,float height,bool grove,bool legacy)
            =>Same(c,grove?Color.Lerp(new Color(.33f,.50f,.29f),new Color(.57f,.67f,.38f),Mathf.Clamp01(height/1.4f))
                :legacy?Color.Lerp(new Color(.40f,.57f,.32f),new Color(.61f,.72f,.40f),Mathf.Clamp01(height)):new Color(.47f,.64f,.35f));
        static int[][] Parts(int[] ids,List<int> triangles,Vector3[] vertices)
        {
            var map=ids.Select((id,i)=>(id,i)).ToDictionary(pair=>pair.id,pair=>pair.i);var parent=Enumerable.Range(0,ids.Length).ToArray();
            int Find(int i){while(parent[i]!=i){parent[i]=parent[parent[i]];i=parent[i];}return i;}
            void Join(int a,int b){parent[Find(a)]=Find(b);}
            var welded=new Dictionary<Vector3Int,int>();
            for(int i=0;i<ids.Length;i++)
            {
                var p=vertices[ids[i]];var key=new Vector3Int(Mathf.RoundToInt(p.x*2000),Mathf.RoundToInt(p.y*2000),Mathf.RoundToInt(p.z*2000));
                if(welded.TryGetValue(key,out int previous))Join(i,previous);else welded.Add(key,i);
            }
            for(int i=0;i<triangles.Count;i+=3){Join(map[triangles[i]],map[triangles[i+1]]);Join(map[triangles[i]],map[triangles[i+2]]);}
            return Enumerable.Range(0,ids.Length).GroupBy(Find).Select(g=>g.Select(i=>ids[i]).ToArray()).OrderBy(p=>p.Min()).ToArray();
        }
        public static Edit[] Plan(IslandTerrain terrain)
        {
            var world=new GrasslandRevitalizationSetup.AuthoringWorld(terrain,GrasslandGroundLayersSetup.Rect);var cache=new Dictionary<Vector2,float>();
            float Floor(Vector2 p){if(!cache.TryGetValue(p,out float y)){y=world.Floor(p);cache.Add(p,y);}return y;}
            var collision=new HashSet<Mesh>(terrain.GetComponentsInChildren<MeshCollider>(true).Select(c=>c.sharedMesh));var edits=new List<Edit>();
            foreach(var renderer in terrain.SeasonalDetails.MeadowBatches.Where(r=>Candidate(r.GetComponent<MeshFilter>().sharedMesh.name)))
            {
                var m=renderer.GetComponent<MeshFilter>().sharedMesh;string path=AssetDatabase.GetAssetPath(m),metadata=AssetImporter.GetAtPath(path).userData;
                var known=new[]{GrasslandFloraSetup.Marker,GrasslandIslandMeadowSetup.Marker+";"+GrasslandFloraSetup.Marker,GrasslandGroveSetup.Marker+";"+GrasslandFloraSetup.Marker};
                Check(known.Contains(metadata),"preserve custom metadata / previous source state "+path);
                Check(path.StartsWith(BiomeIslandsSetup.Folder+"/",StringComparison.Ordinal)&&!collision.Contains(m)&&renderer.transform.localToWorldMatrix==terrain.transform.localToWorldMatrix&&renderer.sharedMaterial.shader.name==GrasslandFloraSetup.ShaderName,"preserve collision, material and transform "+path);
                var v=m.vertices;var n=m.normals;var c=m.colors;var wind=m.uv;var roots=m.uv2;var changed=new bool[v.Length];var chosen=new List<Vector2>();int stones=0,lobes=0;
                Check(v.Length==n.Length&&v.Length==c.Length&&v.Length==wind.Length&&v.Length==roots.Length,"complete authored channels");
                var groups=Enumerable.Range(0,v.Length).GroupBy(i=>roots[i]).Where(g=>g.All(i=>wind[i].x>=0)&&g.Any(i=>wind[i].x>.01f)&&g.Any(i=>wind[i].x==0)).ToDictionary(g=>g.Key,g=>g.ToArray());
                var triangles=groups.Keys.ToDictionary(p=>p,p=>new List<int>());var indices=m.triangles;
                for(int i=0;i<indices.Length;i+=3)if(triangles.TryGetValue(roots[indices[i]],out var list)){Check(roots[indices[i]]==roots[indices[i+1]]&&roots[indices[i]]==roots[indices[i+2]],"no cross-root faces");list.AddRange(new[]{indices[i],indices[i+1],indices[i+2]});}
                bool grove=m.name.StartsWith("Grove ",StringComparison.Ordinal),legacy=m.name.StartsWith("Art ",StringComparison.Ordinal);
                foreach(var group in groups)
                {
                    var p=group.Key;var ids=group.Value;
                    if(BiomeIslandLayout.At(p.x,p.y)!=IslandBiome.Grassland||!BiomeIslandLayout.Contains(p.x,p.y,20))continue;
                    var h=ids.ToDictionary(i=>i,i=>v[i].y-Floor(new Vector2(v[i].x,v[i].z))-.018f);
                    var rock=ids.ToDictionary(i=>i,i=>wind[i].x==0&&Stone(c[i],grove));
                    // Preserve hand-painted or unknown clumps rather than replacing their geometry.
                    if(ids.Any(i=>!rock[i]&&!Leaf(c[i],h[i],grove,legacy))||ids.Count(i=>rock[i])<60||ids.Count(i=>!rock[i])<60)continue;
                    var parts=Parts(ids,triangles[p],v);if(parts.Any(part=>part.Length<12||part.Any(i=>rock[i]!=rock[part[0]])))continue;
                    float radius=ids.Max(i=>Vector2.Distance(p,new Vector2(v[i].x,v[i].z)));Check(radius<=1.181f,"existing complete root envelope");
                    chosen.Add(p);int number=0;
                    foreach(var part in parts)
                    {
                        bool stone=rock[part[0]];var box=new Bounds(v[part[0]],Vector3.zero);foreach(int i in part)box.Encapsulate(v[i]);
                        var center=new Vector2(box.center.x,box.center.z);float min=part.Min(i=>h[i]),max=part.Max(i=>h[i]),mid=(min+max)*.5f;
                        Check(box.size.x>.001f&&box.size.z>.001f&&max-min>.001f,"recognizable closed stone / leaf lobe");
                        float sample=Noise(p+Vector2.one*number++,49),width=stone?.95f+.035f*sample:.90f+.075f*sample,height=stone?.82f+.13f*sample:.86f+.30f*sample;
                        float burial=stone?.025f+.025f*Noise(p+center,71):0;
                        var ext=new Vector3(box.extents.x,(max-min)*.5f,box.extents.z);
                        foreach(int i in part)
                        {
                            var delta=new Vector3(v[i].x-center.x,h[i]-mid,v[i].z-center.y);var q=delta;
                            if(!stone)
                            {
                                float length=new Vector3(delta.x/ext.x,delta.y/ext.y,delta.z/ext.z).magnitude;
                                if(length>.01f)q=Vector3.Lerp(delta,delta/length*.96f,.40f);
                            }
                            var next=center+new Vector2(q.x,q.z)*width;var offset=next-p;if(offset.magnitude>radius)next=p+offset.normalized*radius;
                            float nextHeight=stone?min+(h[i]-min)*height-burial:mid+q.y*height;
                            v[i]=new Vector3(next.x,Floor(next)+.018f+nextHeight,next.y);
                            var normal=n[i];
                            if(!stone)
                            {
                                var radial=new Vector3(delta.x/(ext.x*ext.x),delta.y/(ext.y*ext.y),delta.z/(ext.z*ext.z));
                                if(radial.sqrMagnitude>.001f&&Vector3.Dot(normal,radial.normalized)>.3f)normal=Vector3.Slerp(normal,radial.normalized,.55f);
                            }
                            n[i]=new Vector3(normal.x/width,normal.y/height,normal.z/width).normalized;
                            float cap=Mathf.InverseLerp(min,max,h[i]);var xz=new Vector2(v[i].x,v[i].z);
                            if(stone)
                            {
                                var baseColor=Color.Lerp(new Color(.53f,.57f,.49f),new Color(.69f,.70f,.61f),cap*.65f+sample*.2f);
                                float mottling=GrasslandGroundLayersSetup.Noise(xz,7.5f,13);
                                float moss=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.38f,.82f,cap+mottling*.30f))*(.4f+.6f*Mathf.Clamp01(n[i].y));
                                c[i]=Color.Lerp(baseColor,Color.Lerp(new Color(.39f,.51f,.31f),new Color(.55f,.63f,.39f),mottling),moss*.88f);
                            }
                            else c[i]=Color.Lerp(new Color(.36f,.51f,.30f),new Color(.63f,.73f,.44f),Mathf.Clamp01(cap*.72f+sample*.22f));
                            c[i].a=1;changed[i]=true;
                            Check(Vector2.Distance(p,next)<=radius+.0002f&&nextHeight<1.2f,"original footprint and safe lobe height");
                        }
                        if(stone)stones++;else lobes++;
                    }
                }
                edits.Add(new Edit{Mesh=m,Vertices=v,Normals=n,Colors=c,Changed=changed,Metadata=metadata,Roots=chosen.ToArray(),Stones=stones,Lobes=lobes});
            }
            Check(edits.Count>0&&edits.SelectMany(e=>e.Roots).Any()&&edits.Sum(e=>e.Lobes)>0,"recognizable existing shrub/stone groups required");return edits.ToArray();
        }
        static void Clean(string p)=>File.WriteAllText(p,string.Join("\n",File.ReadAllLines(p).Select(l=>l.TrimEnd()))+"\n");
        [MenuItem("Magic Island/Art/Refine Grassland Shrubs And Moss Stones (Explicit)")]
        public static void Rebuild()
        {
            Check(!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling,"Edit Mode required");var root=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);var backup=new Dictionary<string,byte[]>();bool saved=false;
            try
            {
                var t=root.GetComponent<IslandTerrain>();Check(t.IsValid&&t.World.ArtVersion==3,"authored islands");var candidates=t.SeasonalDetails.MeadowBatches.Select(r=>r.GetComponent<MeshFilter>().sharedMesh).Where(m=>Candidate(m.name)).ToArray();
                var done=candidates.Select(m=>Done(AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(m)).userData)).ToArray();if(done.Any(b=>b)){Check(done.All(b=>b),"preserve incomplete/custom migration");Debug.Log("Grassland shrubs, stones and later hand edits retained.");return;}
                Physics.SyncTransforms();var plan=Plan(t);
                foreach(string p in plan.Select(e=>AssetDatabase.GetAssetPath(e.Mesh)).SelectMany(p=>new[]{p,p+".meta"}))backup.Add(p,File.ReadAllBytes(p));
                foreach(var e in plan)if(e.Changed.Any(b=>b)){e.Mesh.vertices=e.Vertices;e.Mesh.normals=e.Normals;e.Mesh.colors=e.Colors;e.Mesh.RecalculateBounds();var bounds=e.Mesh.bounds;bounds.Expand(.20f);e.Mesh.bounds=bounds;EditorUtility.SetDirty(e.Mesh);}AssetDatabase.SaveAssets();
                foreach(var e in plan){string p=AssetDatabase.GetAssetPath(e.Mesh);var importer=AssetImporter.GetAtPath(p);importer.userData=e.Metadata+";"+Marker;importer.SaveAndReimport();Clean(p);Clean(p+".meta");}saved=true;
                Directory.CreateDirectory("Logs/GrasslandShrubStone");File.WriteAllText("Logs/GrasslandShrubStone/Stats.json",JsonUtility.ToJson(new Report{Groups=plan.Sum(e=>e.Roots.Length),Stones=plan.Sum(e=>e.Stones),Lobes=plan.Sum(e=>e.Lobes),Meshes=plan.Length}));
                Debug.Log("Grassland shrubs/stones baked: "+plan.Sum(e=>e.Roots.Length)+" existing groups, "+plan.Sum(e=>e.Stones)+" stones, "+plan.Sum(e=>e.Lobes)+" leaf lobes in "+plan.Length+" existing meshes; no new objects or batches.");
            }
            finally{PrefabUtility.UnloadPrefabContents(root);if(!saved){foreach(var p in backup)File.WriteAllBytes(p.Key,p.Value);foreach(string p in backup.Keys.Where(p=>!p.EndsWith(".meta",StringComparison.Ordinal)))AssetDatabase.ImportAsset(p,ImportAssetOptions.ForceUpdate);}}
        }
        [Serializable]sealed class Report {public int Groups,Stones,Lobes,Meshes;}
    }
}
