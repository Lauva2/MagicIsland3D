using System;
using MagicIsland.Interaction;
using MagicIsland.Managers;
using UnityEngine;

namespace MagicIsland.TerrainSystem
{
    // Owned by SeasonalGroundCover. Refresh only after committed placement events; no Update.
    public sealed class MeadowCoverPresentation
    {
        static readonly int CountId=Shader.PropertyToID("_MeadowCutCount"),CutsId=Shader.PropertyToID("_MeadowCuts");
        readonly Renderer[] renderers;
        readonly MaterialPropertyBlock[] originals;
        readonly bool[] enabled;
        readonly Vector4[] cuts=new Vector4[PlacementState.Maximum];
        readonly MaterialPropertyBlock block=new MaterialPropertyBlock();
        bool cleared,shown;
        readonly MeadowTouchSimulation touch;
        readonly MeadowTouchSettings touchSettings;
        readonly bool[] touched;
        readonly Vector4 area;
        static readonly int TouchCountId=Shader.PropertyToID("_MeadowTouchCount"),TouchPointsId=Shader.PropertyToID("_MeadowTouches"),AreaId=Shader.PropertyToID("_MeadowTouchArea"),ShapeId=Shader.PropertyToID("_MeadowTouchShape"),WaveId=Shader.PropertyToID("_MeadowTouchWave");
        public MeadowCoverPresentation(Renderer[] values,MeadowTouchSettings settings=default(MeadowTouchSettings),Transform terrain=null)
        {
            renderers=values??Array.Empty<Renderer>();originals=new MaterialPropertyBlock[renderers.Length];enabled=new bool[renderers.Length];
            for(int i=0;i<renderers.Length;i++)
            {originals[i]=new MaterialPropertyBlock();renderers[i].GetPropertyBlock(originals[i]);enabled[i]=renderers[i].enabled;renderers[i].enabled=false;}
            touchSettings=settings;touched=new bool[renderers.Length];
            if(settings.ArtVersion==1&&settings.IsValid)
            {
                var center=new Vector3(settings.Center.x,0,settings.Center.y);if(terrain!=null)center=terrain.TransformPoint(center);
                area=new Vector4(center.x,center.z,settings.AreaRadius,settings.Feather);touch=new MeadowTouchSimulation(settings,new Vector2(center.x,center.z));
                for(int i=0;i<renderers.Length;i++)touched[i]=Array.IndexOf(settings.Renderers,renderers[i])>=0;
                UploadTouch();
            }
        }
        public void TickTouch(float seconds,Vector3? player,bool paused,bool outside)
        {if(!cleared&&touch!=null&&touch.Tick(seconds,player,paused,outside))UploadTouch();}
        void UploadTouch()
        {
            for(int i=0;i<renderers.Length;i++)if(touched[i]&&renderers[i]!=null)
            {
                renderers[i].GetPropertyBlock(block);block.SetVector(AreaId,area);
                block.SetVector(ShapeId,new Vector4(touchSettings.InfluenceRadius,touchSettings.BendDistance,touchSettings.FlowerFactor,touchSettings.EnvelopeRadius));
                block.SetVector(WaveId,new Vector4(touchSettings.WaveAmplitude,touchSettings.WaveLength,touchSettings.WaveSpeed,0));
                block.SetFloat(TouchCountId,touch.Count);block.SetVectorArray(TouchPointsId,touch.Points);renderers[i].SetPropertyBlock(block);
            }
        }
        // A conservative square includes the 1m object cell, plant footprint and its complete wind envelope.
        public static Vector4 Cut(Vector3 center)=>new Vector4(center.x,center.z,1.85f,1.85f);
        public static bool Covered(Vector2 root,Vector4 cut)=>Mathf.Abs(root.x-cut.x)<=cut.z&&Mathf.Abs(root.y-cut.y)<=cut.w;
        public void Refresh(PlacementManager placements)
        {
            if(cleared)return;
            foreach(var renderer in renderers)
            {
                int count=0;var bounds=renderer.bounds;bounds.Expand(4);
                if(placements!=null)foreach(var actor in placements.Objects.Values)
                {
                    var point=actor.transform.position;
                    if(point.x<bounds.min.x||point.x>bounds.max.x||point.z<bounds.min.z||point.z>bounds.max.z)continue;
                    if(count==cuts.Length)break;cuts[count++]=Cut(point);
                }
                renderer.GetPropertyBlock(block);block.SetFloat(CountId,count);block.SetVectorArray(CutsId,cuts);renderer.SetPropertyBlock(block);
            }
        }
        public void Present(bool outside)
        {
            if(cleared||shown==outside)return;shown=outside;
            if(!outside&&touch!=null){touch.Reset();UploadTouch();}
            for(int i=0;i<renderers.Length;i++)renderers[i].enabled=outside&&enabled[i];
        }
        public void Clear()
        {
            if(cleared)return;cleared=true;
            touch?.Reset();
            for(int i=0;i<renderers.Length;i++)if(renderers[i]!=null){renderers[i].SetPropertyBlock(originals[i]);renderers[i].enabled=enabled[i];}
            Array.Clear(cuts,0,cuts.Length);
        }
    }
}
