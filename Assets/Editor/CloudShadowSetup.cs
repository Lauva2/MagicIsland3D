using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    // Offline periodic noise. Runtime reuses the original directional cookie and shared wind clock.
    public static class CloudShadowSetup
    {
        const string Marker="CloudShadowSoft:1:";
        static float Smooth(float t)=>t*t*t*(t*(t*6-15)+10);
        static float Noise(Vector2 uv,int frequency,float seed)
        {
            float x=uv.x*frequency,y=uv.y*frequency;
            return Mathf.Lerp(Mathf.Lerp(Mathf.PerlinNoise(x+seed,y+19),Mathf.PerlinNoise(x-frequency+seed,y+19),Smooth(uv.x)),
                Mathf.Lerp(Mathf.PerlinNoise(x+seed,y-frequency+19),Mathf.PerlinNoise(x-frequency+seed,y-frequency+19),Smooth(uv.x)),Smooth(uv.y));
        }
        public static float Coverage(Vector2 uv)
        {
            var warp=new Vector2(Noise(uv,2,31)-.5f,Noise(uv,3,71)-.5f)*.16f;
            uv+=warp;uv=new Vector2(Mathf.Repeat(uv.x,1),Mathf.Repeat(uv.y,1));
            float field=Noise(uv,3,47)*.72f+Noise(uv,7,17)*.22f+Noise(uv,13,83)*.06f;
            return Mathf.SmoothStep(0,1,Mathf.InverseLerp(.50f,.72f,field));
        }
        [MenuItem("Magic Island/Art/Refine Cloud Shadows (Explicit)")]
        public static void Refine()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling)throw new InvalidOperationException("Refine cloud shadows in Edit Mode.");
            var importer=AssetImporter.GetAtPath(CloudSetup.CookiePath);
            if(importer!=null&&importer.userData.Contains(Marker))
            {Debug.Log("Cloud shadow refinement already baked; preserving the texture and later artist edits.");return;}
            Bake();
        }
        public static void CreateMissingCookie()
        {
            if(!File.Exists(CloudSetup.CookiePath))Bake();
        }
        static void Bake()
        {
            const int size=256;var coverage=new float[size*size];float maximum=0;
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {float value=Coverage(new Vector2((x+.5f)/size,(y+.5f)/size));coverage[y*size+x]=value;maximum=Mathf.Max(maximum,value);}
            if(maximum<.01f)throw new InvalidOperationException("Cloud noise has no valid coverage; preserve existing art.");
            var pixels=coverage.Select(c=>new Color(1,1,1,1-.08f*c/maximum)).ToArray();
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,true,true);
            try{texture.SetPixels(pixels);texture.Apply();File.WriteAllBytes(CloudSetup.CookiePath,texture.EncodeToPNG());}
            finally{Object.DestroyImmediate(texture);}
            AssetDatabase.ImportAsset(CloudSetup.CookiePath,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(CloudSetup.CookiePath);
            importer.textureType=TextureImporterType.Default;importer.sRGBTexture=false;importer.alphaSource=TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency=false;importer.wrapMode=TextureWrapMode.Repeat;importer.filterMode=FilterMode.Trilinear;
            importer.mipmapEnabled=true;importer.isReadable=true;importer.textureCompression=TextureImporterCompression.Uncompressed;
            using(var hash=SHA256.Create())
                importer.userData=(string.IsNullOrEmpty(importer.userData)?"":importer.userData+"\n")+Marker+BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(CloudSetup.CookiePath))).Replace("-","");
            importer.SaveAndReimport();AssetDatabase.SaveAssets();
            if(File.Exists(CloudSetup.PrefabPath))
            {
                var root=PrefabUtility.LoadPrefabContents(CloudSetup.PrefabPath);
                try{PrefabUtility.SaveAsPrefabAsset(root,CloudSetup.PrefabPath);}
                finally{PrefabUtility.UnloadPrefabContents(root);}
                Clean(CloudSetup.PrefabPath);
            }
            Clean(CloudSetup.CookiePath+".meta");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("Cloud shadows refined: periodic irregular masses, maximum 8% direct-sun attenuation; original texture GUID retained.");
        }
        static void Clean(string path)=>File.WriteAllText(path,string.Join("\n",File.ReadAllLines(path).Select(s=>s.TrimEnd()))+"\n");
    }
}
