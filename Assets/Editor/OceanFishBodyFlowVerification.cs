using System;
using System.IO;
using System.Linq;
using MagicIsland.Managers;
using MagicIsland.Ocean;
using UnityEngine;
using Object=UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    public static class OceanFishBodyFlowVerification
    {
        static void Require(bool value,string why){if(!value)throw new Exception("Fish body render: "+why);}
        public static void Verify(GameManager game)
        {
            var surface=game.Ocean.Surface;var school=surface.Schools.First(s=>s.Species==ShallowFishSpecies.Clownfish);
            var camera=game.Players.Player.ViewCamera;var renderer=school.GetComponent<Renderer>();
            var cameraPosition=camera.transform.position;float size=camera.orthographicSize;int mask=camera.cullingMask;Color background=camera.backgroundColor;int layer=school.gameObject.layer;
            var canvases=Object.FindObjectsOfType<Canvas>();var visible=canvases.Select(c=>c.enabled).ToArray();
            game.Cameras.SetOcclusionEnabled(false);
            try
            {
                foreach(var canvas in canvases)canvas.enabled=false;
                school.gameObject.layer=31;camera.cullingMask=1<<31;camera.backgroundColor=Color.black;camera.orthographicSize=1.1f;
                float start=school.MotionTime;school.Render(start+.8f);var rootPosition=school.transform.localPosition;
                var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);
                var positions=block.GetVectorArray("_FishPoses");var attitudes=block.GetVectorArray("_FishAttitudes");
                var focus=school.transform.parent.TransformPoint(school.Steering.RenderState(0).Position);
                camera.transform.position=focus-camera.transform.forward*18;
                var a=Capture(camera,"FishBodyConstrainedA");
                block.SetFloat("_FishBodyEnabled",0);renderer.SetPropertyBlock(block);var rigidA=Capture(camera,"FishBodyRigidControlA");
                school.Render(start+1.6f);renderer.GetPropertyBlock(block);school.transform.localPosition=rootPosition;
                // Hold location, yaw, pitch, fin phase AND ocean clock. Only constrained links differ.
                block.SetVectorArray("_FishPoses",positions);block.SetVectorArray("_FishAttitudes",attitudes);block.SetFloat("_OceanTime",start+.8f);
                block.SetFloat("_FishBodyEnabled",0);renderer.SetPropertyBlock(block);var rigidB=Capture(camera,"FishBodyRigidControlB");
                Require(SameImage(rigidA,rigidB),"Rigid control stays identical with root poses held fixed");
                block.SetFloat("_FishBodyEnabled",1);renderer.SetPropertyBlock(block);var b=Capture(camera,"FishBodyConstrainedB");
                Require(a.Zip(b,(x,y)=>Math.Abs(x.r-y.r)+Math.Abs(x.g-y.g)+Math.Abs(x.b-y.b)>8?1:0).Sum()>25,"Actual fish body silhouette/shading changes independently of whole model motion");
                Require(SameImage(b,Capture(camera,"FishBodyConstrainedPause")),"Paused constrained body render is identical");
                school.gameObject.layer=layer;camera.cullingMask=mask;camera.backgroundColor=background;
                school.ResetSteering();school.Render(surface.AnimationTime);
                for(int frame=0;frame<60;frame++)
                {
                    surface.Tick(1f/30,1,false,game.Weather.Sun);
                    focus=school.transform.parent.TransformPoint(school.Steering.RenderState(0).Position);
                    camera.transform.position=focus+Vector3.up*.05f-camera.transform.forward*18;
                    Capture(camera,"FishBodySwim"+frame.ToString("00"));
                }
                Debug.Log("Fish body render verification PASSED: fixed-root GPU control, independent distance-constrained body deformation, paused image within 8-bit edge rounding and close-up 30 FPS sequence.");
            }
            finally
            {
                school.gameObject.layer=layer;school.ResetSteering();school.Render(surface.AnimationTime);
                camera.transform.position=cameraPosition;camera.orthographicSize=size;camera.cullingMask=mask;camera.backgroundColor=background;
                for(int i=0;i<canvases.Length;i++)canvases[i].enabled=visible[i];game.Cameras.SetOcclusionEnabled(true);
            }
        }
        static bool SameImage(Color32[] a,Color32[] b)
        {
            // Independent D3D11 targets occasionally round an edge by one 8-bit level,
            // as in OceanFishVisibilityVerification. Actual motion exceeds this bound.
            if(a.Length!=b.Length)return false;
            int changed=0;
            for(int i=0;i<a.Length;i++)
            {
                int r=Math.Abs(a[i].r-b[i].r),g=Math.Abs(a[i].g-b[i].g),blue=Math.Abs(a[i].b-b[i].b);
                if(r>1||g>1||blue>1||a[i].a!=b[i].a)return false;
                changed+=(r>0?1:0)+(g>0?1:0)+(blue>0?1:0);
                if(changed>128)return false;
            }
            return true;
        }
        static Color32[] Capture(Camera camera,string name)
        {
            var rt=new RenderTexture(960,540,24);var texture=new Texture2D(960,540,TextureFormat.RGB24,false);var target=camera.targetTexture;var active=RenderTexture.active;
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,960,540),0,0);texture.Apply();Directory.CreateDirectory("Logs");File.WriteAllBytes("Logs/"+name+".png",texture.EncodeToPNG());return texture.GetPixels32();}
            finally{camera.targetTexture=target;RenderTexture.active=active;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(texture);}
        }
    }
}
