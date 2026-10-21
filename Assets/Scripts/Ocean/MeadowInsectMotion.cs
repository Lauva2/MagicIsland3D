using System;
using System.Collections.Generic;
using UnityEngine;

namespace MagicIsland.Ocean
{
    // Cosmetic state for the existing merged batches. Allocated once by WorldWaterEcology.Bind.
    // UV1 phase is the original bake's site * 2.399963 + member * 1.83; no mesh rewrite is needed.
    sealed class MeadowInsectMotion
    {
        const int Capacity=32;
        const float SitePhase=2.399963f;
        static readonly int FlowersId=Shader.PropertyToID("_MeadowFlowers"),ResponsesId=Shader.PropertyToID("_MeadowResponses"),ReadyId=Shader.PropertyToID("_MeadowMotionReady");
        readonly Vector3[] homes=new Vector3[Capacity];
        readonly float[] phases=new float[Capacity],scores=new float[Capacity];
        readonly bool[] active=new bool[Capacity],ready;
        readonly Vector4[] flowers=new Vector4[Capacity],responses=new Vector4[Capacity],visitingHomes=new Vector4[Capacity],roots=new Vector4[Capacity];
        readonly MeadowFlowerVisit[] visits;
        readonly MaterialPropertyBlock sourceBlock=new MaterialPropertyBlock();
        readonly List<Vector4> cuts=new List<Vector4>(128),touches=new List<Vector4>(16);
        Vector4 touchArea,touchShape,touchWave;int touchCount;
        static readonly int HomesId=Shader.PropertyToID("_MeadowVisitingHomes"),RootsId=Shader.PropertyToID("_MeadowFlowerRoots"),CutsId=Shader.PropertyToID("_MeadowCuts");
        static readonly int TouchesId=Shader.PropertyToID("_MeadowTouches"),TouchCountId=Shader.PropertyToID("_MeadowTouchCount");
        public static bool ValidVisits(int version,MeadowFlowerVisit[] links,Renderer[] ambient)
        {
            if(links==null||links.Length>Capacity||version<0||version>1||(version==0)!=(links.Length==0))return false;
            var used=new bool[Capacity];
            foreach(var link in links)
            {
                if(link==null||link.Source==null||link.Source.sharedMaterial==null||link.Source.sharedMaterial.shader.name!="MagicIsland/Meadow Flora"
                    ||link.Slot<0||link.Slot>=Capacity||used[link.Slot])return false;
                used[link.Slot]=true;var m=link.Source.GetComponent<MeshFilter>()?.sharedMesh;
                if(m==null||link.Vertex<0||link.Vertex>=m.vertexCount||Array.IndexOf(m.triangles,link.Vertex)<0)return false;
                var uv=m.uv;var root=m.uv2;var season=m.uv3;var point=m.vertices;
                if(uv.Length!=m.vertexCount||root.Length!=m.vertexCount||season.Length!=m.vertexCount||uv[link.Vertex].x> -2||uv[link.Vertex].x< -3)return false;
                int v=link.Vertex;if(!Finite(uv[v].x)||!Finite(uv[v].y)||!Finite(root[v].x)||!Finite(root[v].y)||!Finite(season[v].x)
                    ||!Finite(point[v].x)||!Finite(point[v].y)||!Finite(point[v].z))return false;
                bool found=false;
                foreach(var r in ambient)if(r.sharedMaterial.shader.name=="MagicIsland/Meadow Insects"&&r.sharedMaterial.GetFloat("_EcologyMode")<.5f)
                    foreach(var p in r.GetComponent<MeshFilter>().sharedMesh.uv2)
                    {int site=Mathf.FloorToInt((p.y+.0001f)/SitePhase),slot=site*2+Mathf.RoundToInt((p.y-site*SitePhase)/1.83f);if(slot==link.Slot&&r.transform.localToWorldMatrix==link.Source.transform.localToWorldMatrix)found=true;}
                if(!found)return false;
            }
            return true;
        }
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);

        public MeadowInsectMotion(Renderer[] renderers,MeadowFlowerVisit[] links)
        {
            visits=links;
            ready=new bool[renderers.Length];
            for(int batch=0;batch<renderers.Length;batch++)
            {
                var r=renderers[batch];var material=r.sharedMaterial;
                if(material.shader.name!="MagicIsland/Meadow Insects"||material.GetFloat("_EcologyMode")>.5f)continue;
                var mesh=r.GetComponent<MeshFilter>().sharedMesh;var centers=mesh.uv;var heights=mesh.uv2;
                if(centers.Length!=mesh.vertexCount||heights.Length!=mesh.vertexCount)continue;
                bool valid=true;
                for(int v=0;v<heights.Length;v++)
                {
                    if(!Finite(heights[v].x)||!Finite(heights[v].y)||!Finite(centers[v].x)||!Finite(centers[v].y)){valid=false;break;}
                    float phase=heights[v].y;int site=Mathf.FloorToInt((phase+.0001f)/SitePhase);
                    int member=Mathf.RoundToInt((phase-site*SitePhase)/1.83f),slot=site*2+member;
                    if(site<0||member<0||member>1||slot<0||slot>=Capacity||Mathf.Abs(phase-site*SitePhase-member*1.83f)>.001f){valid=false;break;}
                }
                if(!valid)continue; // A custom phase channel keeps the shader's bounded ambient fallback.
                ready[batch]=true;
                for(int v=0;v<heights.Length;v++)
                {
                    float phase=heights[v].y;int site=Mathf.FloorToInt((phase+.0001f)/SitePhase);
                    int slot=site*2+Mathf.RoundToInt((phase-site*SitePhase)/1.83f);
                    homes[slot]=r.transform.TransformPoint(new Vector3(centers[v].x,heights[v].x,centers[v].y));
                    phases[slot]=phase;active[slot]=true;
                }
            }
            for(int i=0;i<Capacity;i++)
            {
                scores[i]=float.PositiveInfinity;
                flowers[i]=homes[i]+Vector3.down*.25f;
            }
            // Read the existing authored pollen only once, rather than guessing a landing on the stone.
            foreach(var r in renderers)
            {
                if(r.sharedMaterial.shader.name!="MagicIsland/Meadow Foliage")continue;
                var mesh=r.GetComponent<MeshFilter>().sharedMesh;var points=mesh.vertices;var colors=mesh.colors;var wind=mesh.uv;
                for(int v=0;v<colors.Length;v++)
                {
                    var c=colors[v];if(Mathf.Abs(c.r-.90f)>.002f||Mathf.Abs(c.g-.59f)>.002f||Mathf.Abs(c.b-.23f)>.002f)continue;
                    var p=r.transform.TransformPoint(points[v]);
                    for(int i=0;i<Capacity;i++)
                    {
                        if(!active[i])continue;var delta=p-homes[i];delta.y=0;if(delta.sqrMagnitude>.60f*.60f)continue;
                        var preferred=homes[i]+new Vector3(Mathf.Sin(phases[i]),0,Mathf.Cos(phases[i]))*.35f;
                        float score=(new Vector2(p.x-preferred.x,p.z-preferred.z)).sqrMagnitude+(homes[i].y-p.y)*.005f;
                        if(score>=scores[i])continue;scores[i]=score;var landing=p+Vector3.up*.025f;
                        flowers[i]=new Vector4(landing.x,landing.y,landing.z,wind.Length==points.Length?wind[v].x:0);
                    }
                }
            }
            foreach(var link in visits)
            {
                var m=link.Source.GetComponent<MeshFilter>().sharedMesh;int v=link.Vertex,i=link.Slot;
                var p=link.Source.transform.TransformPoint(m.vertices[v]);var uv=m.uv[v];var root=m.uv2[v];var floor=m.uv3[v];
                var worldRoot=link.Source.transform.TransformPoint(new Vector3(root.x,floor.x,root.y));
                flowers[i]=new Vector4(p.x,p.y+.025f,p.z,uv.y);
                roots[i]=new Vector4(worldRoot.x,worldRoot.y,worldRoot.z,-uv.x-2);
                homes[i]=p+Vector3.up*.32f;visitingHomes[i]=new Vector4(homes[i].x,homes[i].y,homes[i].z,1);
            }
            RefreshFlowers();
        }
        void RefreshFlowers()
        {
            foreach(var link in visits)
            {
                link.Source.GetPropertyBlock(sourceBlock);cuts.Clear();sourceBlock.GetVectorArray(CutsId,cuts);
                int count=Mathf.Min((int)sourceBlock.GetFloat("_MeadowCutCount"),cuts.Count);var root=roots[link.Slot];bool covered=false;
                for(int n=0;n<count;n++)if(Mathf.Abs(root.x-cuts[n].x)<=cuts[n].z&&Mathf.Abs(root.z-cuts[n].y)<=cuts[n].w){covered=true;break;}
                visitingHomes[link.Slot].w=covered?-1:1;
            }
            if(visits.Length==0)return;
            visits[0].Source.GetPropertyBlock(sourceBlock);touches.Clear();sourceBlock.GetVectorArray(TouchesId,touches);
            touchCount=Mathf.Min((int)sourceBlock.GetFloat("_MeadowTouchCount"),touches.Count);
            touchArea=sourceBlock.GetVector("_MeadowTouchArea");touchShape=sourceBlock.GetVector("_MeadowTouchShape");touchWave=sourceBlock.GetVector("_MeadowTouchWave");
        }

        public void Tick(float seconds,Vector3? player)
        {
            RefreshFlowers();
            if(seconds<=0)return;
            for(int i=0;i<Capacity;i++)
            {
                if(!active[i])continue;
                var previous=responses[i];float target=0;var away=new Vector3(previous.x,0,previous.z);
                if(player.HasValue&&Mathf.Abs(player.Value.y-homes[i].y)<2.2f)
                {
                    var delta=homes[i]-player.Value;delta.y=0;float distance=delta.magnitude;
                    target=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.7f,2.6f,distance));
                    if(target>0)
                    {
                        away=distance>.01f?delta/distance:new Vector3(Mathf.Sin(phases[i]),0,Mathf.Cos(phases[i]));
                        // Neighbours separate slightly instead of moving as one rigid pair.
                        away=Quaternion.Euler(0,(i%2==0?-24:24),0)*away*.55f;
                    }
                }
                float response=1-Mathf.Exp(-seconds*(target>previous.w?3.4f: .65f));
                var offset=Vector3.Lerp(new Vector3(previous.x,previous.y,previous.z),new Vector3(away.x,.28f,away.z),1-Mathf.Exp(-seconds*2.5f));
                responses[i]=new Vector4(offset.x,offset.y,offset.z,Mathf.Lerp(previous.w,target,response));
            }
        }

        // The cover owner can update touch/cuts after OceanManager.Update. Read again before rendering.
        // This refresh never advances either the visit clock or the startle state.
        public void RefreshPresentation(Renderer[] renderers,MaterialPropertyBlock block)
        {
            if(visits.Length==0)return;RefreshFlowers();
            for(int i=0;i<renderers.Length;i++)if(ready[i])
            {renderers[i].GetPropertyBlock(block);Apply(i,block);renderers[i].SetPropertyBlock(block);}
        }
        public void Apply(int batch,MaterialPropertyBlock block)
        {
            block.SetFloat(ReadyId,ready[batch]?1:0);
            if(!ready[batch])return;
            block.SetVectorArray(FlowersId,flowers);block.SetVectorArray(ResponsesId,responses);
            block.SetVectorArray(HomesId,visitingHomes);block.SetVectorArray(RootsId,roots);
            block.SetVector("_MeadowTouchArea",touchArea);block.SetVector("_MeadowTouchShape",touchShape);block.SetVector("_MeadowTouchWave",touchWave);
            block.SetFloat(TouchCountId,touchCount);if(touches.Count>0)block.SetVectorArray(TouchesId,touches);
        }
    }
}
