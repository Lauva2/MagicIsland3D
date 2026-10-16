using System;
using System.IO;
using System.Linq;
using MagicIsland.Managers;
using UnityEngine;
using Object=UnityEngine.Object;

namespace MagicIsland.EditorTools
{
    public static class OceanFishVisibilityVerification
    {
        static void Require(bool yes,string message){if(!yes)throw new Exception("Fish visibility: "+message);}
        public static void Verify(GameManager game)
        {
            var surface=game.Ocean.Surface;var camera=game.Players.Player.ViewCamera;
            var position=camera.transform.position;var rotation=camera.transform.rotation;float size=camera.orthographicSize;
            var canvases=Object.FindObjectsOfType<Canvas>();var enabled=canvases.Select(c=>c.enabled).ToArray();
            try
            {
                foreach(var canvas in canvases)canvas.enabled=false;
                var counts=surface.Schools.Select(s=>s.PresentationCount).ToArray();
                var before=surface.Schools.Select(s=>s.Steering.States[0].Position).ToArray();
                camera.transform.position=new Vector3(10000,10000,10000);
                for(int frame=0;frame<60;frame++)surface.Tick(1f/60,1,false,game.Weather.Sun);
                camera.Render();
                Require(surface.LastFishSubmissions==0&&surface.LastVisibleSchools==0,"Offscreen camera uploads no fish");
                Require(counts.SequenceEqual(surface.Schools.Select(s=>s.PresentationCount)),"Simulation does not submit hidden poses");
                Require(surface.Schools.Where((s,i)=>Vector3.Distance(s.Steering.States[0].Position,before[i])>.001f).Count()>20,"Hidden schools keep simulating at the shared clock");
                var school=surface.Schools[0];camera.transform.position=surface.transform.TransformPoint(school.Center)-camera.transform.forward*18;
                camera.orthographicSize=game.Configuration.Config.CameraSize;camera.Render();
                Require(surface.LastVisibleSchools>0&&surface.LastFishSubmissions>0,"First returning camera render submits current poses");
                var block=new MaterialPropertyBlock();school.GetComponent<Renderer>().GetPropertyBlock(block);
                var poses=block.GetVectorArray("_FishPoses");
                for(int i=0;i<school.Members;i++)Require(Vector3.Distance((Vector3)poses[i]+school.transform.localPosition,school.Steering.RenderState(i).Position)<.00001f,"Returning fish uses the current interpolated simulation");
                var frozen=surface.Schools.Select(s=>s.PresentationCount).ToArray();surface.Tick(0,1,false);camera.Render();
                Require(frozen.SequenceEqual(surface.Schools.Select(s=>s.PresentationCount)),"Paused camera does not repeat pose uploads");
                surface.CullFishPresentation=false;camera.Render();var full=Capture(camera,"OceanVisibilityFull");
                surface.CullFishPresentation=true;camera.Render();var culled=Capture(camera,"OceanVisibilityCulled");
                int changed=0,maxDelta=0;
                for(int i=0;i<full.Length;i++){int delta=Math.Abs(full[i]-culled[i]);if(delta>0)changed++;maxDelta=Math.Max(maxDelta,delta);}
                // Independent D3D11 render targets can round shadow-edge pixels by one 8-bit level.
                // Reject missing/changed geometry, with only a tiny quantization allowance.
                Require(maxDelta<=1&&changed<=128,"Visible image differs beyond 8-bit edge rounding: channels="+changed+", max="+maxDelta);
                Debug.Log("Fish visibility verification PASSED: hidden simulation, zero hidden submissions, first-frame current poses, pause and unchanged visible image (changed channels="+changed+", max="+maxDelta+").");
            }
            finally
            {
                surface.CullFishPresentation=true;camera.transform.SetPositionAndRotation(position,rotation);camera.orthographicSize=size;
                for(int i=0;i<canvases.Length;i++)canvases[i].enabled=enabled[i];
            }
        }
        static byte[] Capture(Camera camera,string name)
        {
            var rt=new RenderTexture(1280,720,24);var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);
            var target=camera.targetTexture;var active=RenderTexture.active;
            try
            {
                camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();
                var png=texture.EncodeToPNG();Directory.CreateDirectory("Logs");File.WriteAllBytes("Logs/"+name+".png",png);return texture.GetRawTextureData<byte>().ToArray();
            }
            finally{camera.targetTexture=target;RenderTexture.active=active;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(texture);}
        }
    }
}
