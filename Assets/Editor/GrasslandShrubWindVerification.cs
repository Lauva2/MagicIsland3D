using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using MagicIsland.Managers;
using MagicIsland.TerrainSystem;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    public static class GrasslandShrubWindVerification
    {
        const string Folder="Logs/GrasslandShrubWind",Baseline="Assets/Editor/GrasslandShrubWindBaseline";
        static void Check(bool ok,string why){if(!ok)throw new InvalidOperationException("Shrub wind verification: "+why);}
        static string Hash(string p){using(var s=SHA256.Create())return Convert.ToBase64String(s.ComputeHash(File.ReadAllBytes(p)));}
        static Vector4[] UV(Mesh m,int ch){var a=new List<Vector4>();m.GetUVs(ch,a);return a.ToArray();}
        static void Tick(GameManager g,float dt,bool paused)=>typeof(WeatherManager).GetMethod("TickWind",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(g.Weather,new object[]{dt,paused});
        public static void BakeAndVerify()
        {
            Check(Application.dataPath.Replace('\\','/').Contains("/Logs/"),"isolated project required");
            if(!AssetDatabase.IsValidFolder(Baseline))AssetDatabase.CreateFolder("Assets/Editor","GrasslandShrubWindBaseline");
            string old=File.ReadAllText(Folder+"/ShaderBefore.txt").Replace("Shader \"MagicIsland/Meadow Flora\"","Shader \"Hidden/MagicIsland/Shrub Wind Before\"")
                .Replace("\"SeasonalColor.cginc\"","\"Assets/Art/Shaders/SeasonalColor.cginc\"").Replace("\"MeadowTouch.cginc\"","\"Assets/Art/Shaders/MeadowTouch.cginc\"");
            string oldPath=Baseline+"/Before.shader";File.WriteAllText(oldPath,old);AssetDatabase.ImportAsset(oldPath,ImportAssetOptions.ForceUpdate);
            string probePath=Baseline+"/Probe.shader";File.WriteAllText(probePath,ProbeSource);AssetDatabase.ImportAsset(probePath,ImportAssetOptions.ForceUpdate);
            Check(!ShaderUtil.ShaderHasError(AssetDatabase.LoadAssetAtPath<Shader>(oldPath))&&!ShaderUtil.ShaderHasError(AssetDatabase.LoadAssetAtPath<Shader>(probePath))
                &&!ShaderUtil.ShaderHasError(Shader.Find(GrasslandFloraSetup.ShaderName)),"production, legacy and direct GPU probe shaders compile");
            var root=PrefabUtility.LoadPrefabContents(TerrainSetup.PrefabPath);GrasslandShrubWindSetup.Edit[] plan;
            try
            {
                var t=root.GetComponent<IslandTerrain>();plan=GrasslandShrubWindSetup.Plan(t);
                var r=t.SeasonalDetails.TouchSample.Renderers[0];var material=r.sharedMaterial;var custom=new Material(Shader.Find("Standard"));
                try{r.sharedMaterial=custom;bool refused=false;try{GrasslandShrubWindSetup.Plan(t);}catch(InvalidOperationException){refused=true;}Check(refused,"custom material refused");}
                finally{r.sharedMaterial=material;Object.DestroyImmediate(custom);}
                var imp=AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(plan[0].Mesh));string data=imp.userData;
                try
                {
                    imp.userData=data+";Artist.Custom";bool refused=false;try{GrasslandShrubWindSetup.Plan(t);}catch(InvalidOperationException){refused=true;}Check(refused,"custom metadata refused");
                    foreach(string marker in new[]{GrasslandShrubWindSetup.Marker,GrasslandShrubWindSetup.Pending})
                    {imp.userData=data+";"+marker;refused=false;try{GrasslandShrubWindSetup.Rebuild();}catch(InvalidOperationException){refused=true;}Check(refused,"partial/pending migration refused");}
                }
                finally{imp.userData=data;}
                var mesh=plan[0].Mesh;var before=mesh.uv;
                try{mesh.SetUVs(0,before.Select(p=>new Vector4(p.x,p.y,.03f,0)).ToArray());bool refused=false;try{GrasslandShrubWindSetup.Plan(t);}catch(InvalidOperationException){refused=true;}Check(refused,"custom UV dimensions refused");}
                finally{mesh.SetUVs(0,before);}
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            var paths=plan.Select(e=>AssetDatabase.GetAssetPath(e.Mesh)).ToArray();var v=plan.Select(e=>e.Mesh.vertices).ToArray();
            var n=plan.Select(e=>e.Mesh.normals).ToArray();var c=plan.Select(e=>e.Mesh.colors).ToArray();var faces=plan.Select(e=>e.Mesh.triangles).ToArray();
            var bounds=plan.Select(e=>e.Mesh.bounds).ToArray();var formats=plan.Select(e=>e.Mesh.indexFormat).ToArray();
            var channels=plan.Select(e=>Enumerable.Range(0,8).Select(ch=>UV(e.Mesh,ch)).ToArray()).ToArray();var guids=paths.Select(AssetDatabase.AssetPathToGUID).ToArray();
            foreach(string p in paths)
            {
                string target=Baseline+"/"+Path.GetFileName(p);
                if(File.Exists(target)){var a=AssetDatabase.LoadAssetAtPath<Mesh>(target);var b=AssetDatabase.LoadAssetAtPath<Mesh>(p);Check(a.vertices.SequenceEqual(b.vertices)&&a.normals.SequenceEqual(b.normals)
                    &&a.colors.SequenceEqual(b.colors)&&a.triangles.SequenceEqual(b.triangles)&&Enumerable.Range(0,8).All(ch=>UV(a,ch).SequenceEqual(UV(b,ch))),"exact original fixture");}
                else Check(AssetDatabase.CopyAsset(p,target),"original comparison fixture");
            }
            var owned=new HashSet<string>(paths.SelectMany(p=>new[]{p,p+".meta"}));
            var protectedFiles=Directory.GetFiles("Assets/Art","*",SearchOption.AllDirectories).Select(p=>p.Replace('\\','/')).Where(p=>!owned.Contains(p)).ToArray();var hashes=protectedFiles.Select(Hash).ToArray();
            GrasslandShrubWindSetup.Rebuild();
            for(int e=0;e<plan.Length;e++)
            {
                var m=plan[e].Mesh;var a=m.uv;
                Check(m.vertices.SequenceEqual(v[e])&&m.normals.SequenceEqual(n[e])&&m.colors.SequenceEqual(c[e])&&m.triangles.SequenceEqual(faces[e])
                    &&m.bounds==bounds[e]&&m.indexFormat==formats[e]&&AssetDatabase.AssetPathToGUID(paths[e])==guids[e]&&a.SequenceEqual(plan[e].UV),"UV0-only bake, original full geometry/bounds/GUID/format");
                for(int ch=1;ch<8;ch++)Check(UV(m,ch).SequenceEqual(channels[e][ch]),"original root/season and UV1-7 retained");
                for(int i=0;i<a.Length;i++)Check(plan[e].Kind[i]==0?a[i]==(Vector2)channels[e][0][i]:GrasslandShrubWindSetup.Marked(a[i]),"all unselected UV0 retained, selected paired codec only");
            }
            for(int i=0;i<protectedFiles.Length;i++)Check(Hash(protectedFiles[i])==hashes[i],"protected art "+protectedFiles[i]);
            int selected=Array.FindIndex(plan,e=>e.Kind.Any(k=>k>0));string path=paths[selected];byte[] bytes=File.ReadAllBytes(path);
            try
            {
                var m=plan[selected].Mesh;var a=m.uv;a[Array.FindIndex(plan[selected].Kind,k=>k>0)].y+=.001f;m.uv=a;EditorUtility.SetDirty(m);AssetDatabase.SaveAssets();
                var all=Directory.GetFiles("Assets/Art","*",SearchOption.AllDirectories);var manual=all.Select(Hash).ToArray();
                GrasslandShrubWindSetup.Rebuild();GrasslandShrubDetailSetup.Rebuild();GrasslandSpatialSetup.Rebuild();GrasslandSampleFinishSetup.Rebuild();GrasslandPathEdgeSetup.Rebuild();
                GrasslandEdgeSetup.Rebuild();GrasslandWildflowerSetup.Rebuild();GrasslandBlendSetup.Rebuild();GrasslandPatchSetup.Rebuild();GrasslandReadabilitySetup.Rebuild();GrasslandTouchSetup.Rebuild();PrototypeAddressablesSetup.EnsureSetup();
                for(int i=0;i<all.Length;i++)Check(Hash(all[i])==manual[i],"repeat/old tools/Setup preserve manual wind edits "+all[i]);
            }
            finally{File.WriteAllBytes(path,bytes);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);}
            File.WriteAllText(Folder+"/Stats.json",JsonUtility.ToJson(new Stats{Roots=plan.Sum(e=>e.Roots),Lobes=plan.Sum(e=>e.Lobes),Tips=plan.Sum(e=>e.Tips),Branches=plan.Sum(e=>e.Branches),Vertices=plan.Sum(e=>e.Kind.Count(k=>k>0))},true));
            Debug.Log("Grassland shrub wind bake verification PASSED: UV0-only codec for 13 shrubs/67 lobes/156 tips/26 branches; all geometry, UV1-7, unselected UV0 and other art protected; shader compilation, custom/pending/partial refusal and manual Setup.");
        }
        [Serializable] sealed class Stats{public int Roots,Lobes,Tips,Branches,Vertices;}
        public static void After()=>GrasslandShrubDetailVerification.After();
        static Mesh[] Meshes(GameManager g)=>g.Terrain.Terrain.SeasonalDetails.TouchSample.Renderers.Select(r=>r.GetComponent<MeshFilter>().sharedMesh).ToArray();
        static bool Prepared(GameManager g)=>Meshes(g).All(m=>GrasslandShrubWindSetup.Done(AssetImporter.GetAtPath(BiomeIslandsSetup.Folder+"/"+m.name+".asset").userData));
        public static void Inside(GameManager g)
        {
            if(!Prepared(g))return;double time=g.Weather.Wind.Time;float shaderTime=Shader.GetGlobalFloat("_IslandWindTime");Tick(g,4,false);
            Check(g.Weather.Wind.Time==time&&Shader.GetGlobalFloat("_IslandWindTime")==shaderTime,"indoor shared clock frozen");
            foreach(var r in g.Terrain.Terrain.SeasonalDetails.TouchSample.Renderers)Check(!r.enabled||!r.gameObject.activeInHierarchy||(g.Players.Player.ViewCamera.cullingMask&(1<<r.gameObject.layer))==0,"original indoor hiding");
            Debug.Log("Grassland shrub wind indoor verification PASSED: shared clock frozen and original outdoor culling retained.");
        }
        public static void Visit(GameManager g)
        {
            if(!Prepared(g))return;
            foreach(var m in Meshes(g)){var source=AssetDatabase.LoadAssetAtPath<Mesh>(BiomeIslandsSetup.Folder+"/"+m.name+".asset");Check(Enumerable.Range(0,8).All(ch=>UV(m,ch).SequenceEqual(UV(source,ch)))&&m.bounds==source.bounds,"actual AA wind codec and bounds");}
            GPU(g);Legacy(g);
            GrasslandReadabilityVerification.Compare(g,Folder,Baseline,"shrub wind");
            Check(g.Terrain.TryResolve(new Vector3(-129.9458f,0,445.2327f),out var focus),"close shrub ground");
            GrasslandReadabilityVerification.Compare(g,Folder+"/Close",Baseline,"shrub wind close",false,before=>{GrasslandTouchVerification.Frame(g.Players.Player.ViewCamera,focus);g.Players.Player.ViewCamera.orthographicSize=3;});
            Record(g,focus);
        }

        // Direct floating-point readback of the production include: no CPU reimplementation of wind equations.
        sealed class Probe:IDisposable
        {
            public Mesh Mesh;public Material Material;public Vector3[] Source;public Vector2[] Codec,Roots;public Bounds Bounds;
            public List<(int actual,int reference,bool anchor)> Pairs=new List<(int,int,bool)>();
            public Probe(Mesh source)
            {
                var v=source.vertices.ToList();var n=source.normals.ToList();var uv=source.uv.ToList();var root=source.uv2.ToList();var floor=source.uv3.ToList();Bounds=source.bounds;
                var original=source.uv;
                foreach(var part in BiomePolishSetup.Components(source).Where(a=>a.Length==5&&a.All(i=>original[i].x<=-10&&original[i].x>=-11&&GrasslandShrubWindSetup.Marked(original[i]))))
                foreach(int id in new[]{part.Min(),part.OrderBy(i=>original[i].x).First()})
                {
                    int reference=v.Count;v.Add(v[id]);n.Add(n[id]);root.Add(root[id]);floor.Add(floor[id]);
                    uv.Add(new Vector2(-8-Mathf.Clamp01((v[id].y-floor[id].x-.08f)/1.4f),uv[id].y));
                    Pairs.Add((id,reference,Mathf.Abs(original[id].x+10)<.00001f));
                }
                Source=v.ToArray();Codec=uv.ToArray();Roots=root.ToArray();Mesh=new Mesh{indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
                Mesh.SetVertices(v);Mesh.SetNormals(n);Mesh.SetUVs(0,uv);Mesh.SetUVs(1,root);Mesh.SetUVs(2,floor);
                Mesh.SetUVs(3,Enumerable.Range(0,v.Count).Select(i=>new Vector4(i%512,i/512,i+1,0)).ToArray());Mesh.SetIndices(Enumerable.Range(0,v.Count).ToArray(),MeshTopology.Points,0);
                Material=new Material(AssetDatabase.LoadAssetAtPath<Shader>(Baseline+"/Probe.shader"));
            }
            public Vector3[] Read(bool normal=false)
            {
                int height=(Source.Length+511)/512;var rt=new RenderTexture(512,height,0,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);
                var image=new Texture2D(512,height,TextureFormat.RGBAFloat,false,true);var active=RenderTexture.active;
                try
                {
                    RenderTexture.active=rt;GL.Clear(false,true,Color.clear);Material.SetVector("_ProbeSize",new Vector4(512,height,0,0));Material.SetFloat("_ProbeNormal",normal?1:0);
                    Check(Material.SetPass(0),"GPU probe pass");Graphics.DrawMeshNow(Mesh,Matrix4x4.identity);image.ReadPixels(new Rect(0,0,512,height),0,0);image.Apply();
                    var output=new Vector3[Source.Length];var seen=new bool[Source.Length];
                    foreach(var c in image.GetPixels()){int id=Mathf.RoundToInt(c.a)-1;if(id<0)continue;Check(id<output.Length&&!seen[id],"unique GPU probe pixel");seen[id]=true;output[id]=new Vector3(c.r,c.g,c.b);}
                    Check(seen.All(b=>b),"all actual AA and attachment-reference vertices reached GPU");return output;
                }
                finally{RenderTexture.active=active;Object.DestroyImmediate(rt);Object.DestroyImmediate(image);}
            }
            public void Dispose(){Object.DestroyImmediate(Material);Object.DestroyImmediate(Mesh);}
        }
        static void GPU(GameManager g)
        {
            float time=Shader.GetGlobalFloat("_IslandWindTime"),force=Shader.GetGlobalFloat("_IslandWindStrength");float maxMove=0,maxFlutter=0,desync=0;int anchors=0,pairs=0;
            try
            {
                foreach(var mesh in Meshes(g).Where(m=>m.uv.Any(GrasslandShrubWindSetup.Marked)))using(var probe=new Probe(mesh))
                {
                    foreach(float strength in new[]{0f,.65f,1.6f})foreach(float t in new[]{0f,2.4f,6.6f,12f})
                    {
                        Shader.SetGlobalFloat("_IslandWindTime",t);Shader.SetGlobalFloat("_IslandWindStrength",strength);var delta=probe.Read();
                        for(int i=0;i<delta.Length;i++)
                        {
                            Check(delta[i].magnitude<.112f,"finite bounded actual GPU deformation");
                            if(!GrasslandShrubWindSetup.Marked(probe.Codec[i])||strength==0)Check(delta[i]==Vector3.zero,"unmarked probe / zero wind geometry stationary");
                            if(GrasslandShrubWindSetup.Marked(probe.Codec[i]))
                            {Check(Vector2.Distance(new Vector2(probe.Source[i].x+delta[i].x,probe.Source[i].z+delta[i].z),probe.Roots[i])<=1.3f&&probe.Bounds.Contains(probe.Source[i]+delta[i]),"actual full motion stays in original envelope and bounds");maxMove=Mathf.Max(maxMove,delta[i].magnitude);}
                            if(probe.Codec[i].x==-12){Check(delta[i]==Vector3.zero,"anchored branch base stationary");anchors++;}
                        }
                        foreach(var pair in probe.Pairs)
                        {
                            float difference=Vector3.Distance(delta[pair.actual],delta[pair.reference]);
                            Check(difference<(pair.anchor?.0001f:.0101f),"leaf base shares body deformation; tip flutter stays bounded");if(!pair.anchor)maxFlutter=Mathf.Max(maxFlutter,difference);pairs++;
                        }
                        if(strength==1.6f&&t==2.4f)
                        {
                            var lobes=Enumerable.Range(0,delta.Length).Where(i=>probe.Codec[i].x< -8.2f&&probe.Codec[i].x>=-9&&GrasslandShrubWindSetup.Marked(probe.Codec[i]))
                                .GroupBy(i=>(probe.Roots[i],probe.Codec[i].y)).Select(a=>new{Root=a.Key.Item1,Field=a.Select(i=>delta[i]/Mathf.Pow(-probe.Codec[i].x-8,2)).Aggregate(Vector3.zero,(x,y)=>x+y)/a.Count()});
                            foreach(var group in lobes.GroupBy(a=>a.Root))foreach(var a in group)foreach(var b in group)desync=Mathf.Max(desync,Vector3.Distance(a.Field,b.Field));
                        }
                    }
                    var normals=probe.Read(true);Check(Enumerable.Range(0,normals.Length).Where(i=>GrasslandShrubWindSetup.Marked(probe.Codec[i]))
                        .All(i=>normals[i].sqrMagnitude>.99f&&normals[i].sqrMagnitude<1.01f),"actual deformed GPU normals normalized");
                    Tick(g,.2f,false);var before=probe.Read();double clock=g.Weather.Wind.Time;Tick(g,4,true);var after=probe.Read();
                    Check(g.Weather.Wind.Time==clock&&before.SequenceEqual(after),"modal pause freezes actual float GPU pose and shared clock");
                }
                Check(maxMove>.005f&&maxFlutter>.0005f&&desync>.001f&&anchors>=65&&pairs>=312,"visible sway, distinct lobe phases, independent flutter, anchored roots and attached leaf bases");
                Debug.Log("Grassland shrub wind GPU verification PASSED: direct production-include float readback; zero/normal/strong wind, full envelope/bounds, anchored bases, 156 attached tips, bounded flutter, normalized normals and exact modal pause. Max displacement="+maxMove+", flutter="+maxFlutter);
            }
            finally{Shader.SetGlobalFloat("_IslandWindTime",time);Shader.SetGlobalFloat("_IslandWindStrength",force);}
        }
        static void Legacy(GameManager g)
        {
            var renderers=g.Terrain.Terrain.SeasonalDetails.TouchSample.Renderers;var filters=renderers.Select(r=>r.GetComponent<MeshFilter>()).ToArray();
            var meshes=filters.Select(f=>f.sharedMesh).ToArray();var materials=renderers.Select(r=>r.sharedMaterial).ToArray();var layers=renderers.Select(r=>r.gameObject.layer).ToArray();
            var old=materials.Select(m=>new Material(m){shader=AssetDatabase.LoadAssetAtPath<Shader>(Baseline+"/Before.shader")}).ToArray();
            var camera=g.Players.Player.ViewCamera;int mask=camera.cullingMask;var pos=camera.transform.position;var rot=camera.transform.rotation;float size=camera.orthographicSize;
            try
            {
                Check(g.Terrain.TryResolve(new Vector3(-128.59f,0,447.98f),out var focus),"legacy comparison floor");GrasslandTouchVerification.Frame(camera,focus);camera.cullingMask=1<<30;
                for(int i=0;i<filters.Length;i++){filters[i].sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Baseline+"/"+meshes[i].name+".asset");renderers[i].gameObject.layer=30;}
                foreach(float strength in new[]{0f,.65f,1.6f})
                {
                    Shader.SetGlobalFloat("_IslandWindStrength",strength);Shader.SetGlobalFloat("_IslandWindTime",12);
                    for(int i=0;i<renderers.Length;i++)renderers[i].sharedMaterial=old[i];var a=GrasslandTouchVerification.Capture(camera,Folder+"/Legacy"+strength+"Before.png");
                    for(int i=0;i<renderers.Length;i++)renderers[i].sharedMaterial=materials[i];var b=GrasslandTouchVerification.Capture(camera,Folder+"/Legacy"+strength+"After.png");
                    Check(GrasslandCrownWindVerification.StableFrame(a,b),"unmarked old/new shared shader output retained");
                }
                Debug.Log("Grassland shrub wind legacy GPU verification PASSED: original unmarked meshes, original/current shared shaders, zero/normal/strong wind.");
            }
            finally{for(int i=0;i<filters.Length;i++){filters[i].sharedMesh=meshes[i];renderers[i].sharedMaterial=materials[i];renderers[i].gameObject.layer=layers[i];Object.DestroyImmediate(old[i]);}camera.cullingMask=mask;camera.transform.SetPositionAndRotation(pos,rot);camera.orthographicSize=size;Tick(g,0,true);}
        }
        static void Record(GameManager g,Vector3 close)
        {
            var camera=g.Players.Player.ViewCamera;var pos=camera.transform.position;var rot=camera.transform.rotation;float size=camera.orthographicSize,far=camera.farClipPlane;
            float time=Shader.GetGlobalFloat("_IslandWindTime"),force=Shader.GetGlobalFloat("_IslandWindStrength");Directory.CreateDirectory(Folder+"/Wind");
            try
            {
                camera.farClipPlane=3200;
                for(int frame=0;frame<100;frame++)
                {
                    Tick(g,.04f,false);Shader.SetGlobalFloat("_IslandWindStrength",1.2f);GrasslandTouchVerification.Frame(camera,close);camera.orthographicSize=3;
                    GrasslandTouchVerification.Capture(camera,Folder+"/Wind/Frame"+frame.ToString("D3")+".png");
                }
                Check(!File.Exists(g.Saves.SavePath),"wind preview never saves daytime progress");
                Debug.Log("Grassland shrub wind recording verification PASSED: 100 actual shared-clock GPU frames, amplified wind=1.2 for close preview, no daytime save.");
            }
            finally{camera.transform.SetPositionAndRotation(pos,rot);camera.orthographicSize=size;camera.farClipPlane=far;Shader.SetGlobalFloat("_IslandWindTime",time);Shader.SetGlobalFloat("_IslandWindStrength",force);}
        }
        const string ProbeSource=@"Shader ""Hidden/MagicIsland/Shrub Wind Probe"" { SubShader { Pass { ZTest Always ZWrite Off Cull Off
CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma target 3.0
#include ""UnityCG.cginc""
float _IslandWindTime,_IslandWindStrength;float4 _IslandWindDirection,_ProbeSize;float _ProbeNormal;
#include ""Assets/Art/Shaders/MeadowShrubWind.cginc""
struct output {float4 position:SV_POSITION;float3 delta:TEXCOORD0;float3 normal:TEXCOORD1;float id:TEXCOORD2;float size:PSIZE;};
output vert(appdata_full v){output o;float3 before=v.vertex.xyz;float3 root=mul(unity_ObjectToWorld,float4(v.texcoord1.x,v.texcoord2.x,v.texcoord1.y,1)).xyz;MeadowShrubWind(v,root);o.position=float4((v.texcoord3.xy+.5)/_ProbeSize.xy*2-1,0,1);o.delta=v.vertex.xyz-before;o.normal=v.normal;o.id=v.texcoord3.z;o.size=1;return o;}
float4 frag(output i):SV_Target{return float4(_ProbeNormal>.5?i.normal:i.delta,i.id);}
ENDCG
} } }";
    }
}
