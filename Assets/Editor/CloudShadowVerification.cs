using System;
using System.IO;
using System.Linq;
using MagicIsland.Managers;
using MagicIsland.WeatherSystem;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    public static class CloudShadowVerification
    {
        static void Require(bool ok,string why){if(!ok)throw new InvalidOperationException("Cloud shadow refinement: "+why);}
        static string Hierarchy(GameObject root)=>string.Join("\n",root.GetComponentsInChildren<Transform>(true).Select(t=>
            t.name+"/"+t.GetSiblingIndex()+"/"+t.localPosition.ToString("R")+t.localRotation.ToString("R")+t.localScale.ToString("R")+t.gameObject.activeSelf+"/"+string.Join(",",t.GetComponents<Component>().Select(c=>c.GetType().Name))));
        public static void BakeAndVerify()
        {
            Require(Application.dataPath.Replace('\\','/').Contains("/Logs/"),"use an isolated project");
            string cookie=CloudSetup.CookiePath,prefab=CloudSetup.PrefabPath;
            var original=AssetDatabase.LoadAssetAtPath<GameObject>(prefab);string hierarchy=Hierarchy(original);
            var field=original.GetComponent<CloudField>();var wind=field.Wind;float size=field.FieldSize,altitude=field.Altitude;
            var guid=AssetDatabase.AssetPathToGUID(cookie);var mesh=File.ReadAllBytes(CloudSetup.MeshPath);var material=File.ReadAllBytes(CloudSetup.MaterialPath);
            var scenery=File.ReadAllBytes(TerrainSetup.PrefabPath);var ocean=File.ReadAllBytes(OceanSetup.PrefabPath);
            CloudShadowSetup.Refine();CloudFlowVerification.VerifyAssets();
            original=AssetDatabase.LoadAssetAtPath<GameObject>(prefab);field=original.GetComponent<CloudField>();
            Require(hierarchy==Hierarchy(original)&&wind==field.Wind&&size==field.FieldSize&&altitude==field.Altitude,"all cloud geometry, placement and wind preserved");
            Require(guid==AssetDatabase.AssetPathToGUID(cookie),"original cookie GUID");
            Require(mesh.SequenceEqual(File.ReadAllBytes(CloudSetup.MeshPath))&&material.SequenceEqual(File.ReadAllBytes(CloudSetup.MaterialPath)),"original cloud mesh and material");
            Require(scenery.SequenceEqual(File.ReadAllBytes(TerrainSetup.PrefabPath))&&ocean.SequenceEqual(File.ReadAllBytes(OceanSetup.PrefabPath)),"all island and ocean art retained");
            for(int i=0;i<=256;i++)
            {
                float t=i/256f;
                Require(Mathf.Abs(CloudShadowSetup.Coverage(new Vector2(0,t))-CloudShadowSetup.Coverage(new Vector2(1,t)))<.00005f
                    &&Mathf.Abs(CloudShadowSetup.Coverage(new Vector2(t,0))-CloudShadowSetup.Coverage(new Vector2(t,1)))<.00005f,"periodic field has no seam");
            }
            string[] paths={cookie,cookie+".meta",prefab};var bytes=paths.Select(File.ReadAllBytes).ToArray();
            CloudShadowSetup.Refine();PrototypeAddressablesSetup.EnsureSetup();
            for(int i=0;i<paths.Length;i++)Require(bytes[i].SequenceEqual(File.ReadAllBytes(paths[i])),"repeat and ordinary Setup preserve art");
            try
            {
                var fixture=new Texture2D(2,2);byte[] manual;
                try{fixture.SetPixels(new[]{Color.white,Color.gray,Color.white,Color.white});fixture.Apply();manual=fixture.EncodeToPNG();}
                finally{Object.DestroyImmediate(fixture);}
                File.WriteAllBytes(cookie,manual);AssetDatabase.ImportAsset(cookie,ImportAssetOptions.ForceSynchronousImport);
                CloudShadowSetup.Refine();PrototypeAddressablesSetup.EnsureSetup();
                Require(manual.SequenceEqual(File.ReadAllBytes(cookie)),"later manual texture painting is retained");
                var importer=AssetImporter.GetAtPath(cookie);importer.userData="ArtistCustomCookie";importer.SaveAndReimport();
                CloudShadowSetup.CreateMissingCookie();Require(manual.SequenceEqual(File.ReadAllBytes(cookie)),"fill-missing path preserves custom unmarked textures");
            }
            finally
            {
                for(int i=0;i<paths.Length;i++)File.WriteAllBytes(paths[i],bytes[i]);
                AssetDatabase.ImportAsset(cookie,ImportAssetOptions.ForceSynchronousImport);AssetDatabase.ImportAsset(prefab,ImportAssetOptions.ForceSynchronousImport);
            }
            Debug.Log("Cloud shadow bake verification PASSED: original cloud geometry/GUID/placement, periodic edges, repeat/Setup/manual art preservation.");
        }
        public static void VerifyRuntime(GameManager g)
        {
            CloudFlowVerification.VerifyAssets();CloudFlowVerification.VerifyRuntime(g);
            double minute=g.Time.Clock.Minute;g.Weather.Tick(750,true);
            var clouds=g.Weather.Clouds;var sun=g.Weather.Sun;
            var oldCookie=sun.cookie;float oldSize=sun.cookieSize;var oldPosition=sun.transform.position;
            var fixture=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(CloudSetup.PrefabPath));
            try
            {
                var field=fixture.GetComponent<CloudField>();field.Bind(sun);float span=field.FieldSize*field.ShadowScale;
                float speed=field.Wind.magnitude;
                field.Tick((span-.02f)/speed,1,false,true,1,Vector2.right);
                var a=sun.transform.position;
                field.Tick(.04f/speed,1,false,true,1,Vector2.right);var b=sun.transform.position;
                Require(Mathf.Abs(Vector3.Dot(b-a,sun.transform.right)+span-.04f)<.01f,"shadow wrap matches its texture period");
                Require(Mathf.Abs(Vector3.Dot(b-a,sun.transform.up))<.01f,"wrap never changes the other projection axis");
                float time=field.AnimationTime;field.Tick(0,1,false,true);Require(time==field.AnimationTime&&b==sun.transform.position,"paused broad shadow position is exact");
                field.Detach();Require(sun.cookie==oldCookie&&sun.cookieSize==oldSize&&sun.transform.position==oldPosition,"detach restores prior cookie and sun transform");
            }
            finally{Object.DestroyImmediate(fixture);g.Weather.Tick(minute,true);}
            Require(g.Weather.Clouds==clouds&&g.Weather.OwnsClouds,"temporary wrap check does not replace owned AA clouds");
            Debug.Log("Cloud shadow runtime verification PASSED: large-period wrap, sun restoration, wind, pause, indoors and rain/night.");
        }
    }
}
