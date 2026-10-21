Shader "MagicIsland/Meadow Insects"
{
    Properties { _EcologyMode("Butterfly 0 / Firefly 1",Float)=0 }
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        Cull Off
        CGPROGRAM
        #pragma surface surf Standard vertex:vert noforwardadd
        #pragma target 3.0
        float _EcologyMode,_OceanTime,_Daylight,_MeadowRain,_MeadowMotionReady;
        float4 _MeadowFlowers[32],_MeadowResponses[32],_MeadowVisitingHomes[32],_MeadowFlowerRoots[32];
        float _IslandWindTime,_IslandWindStrength;
        float4 _IslandWindDirection;
        #include "MeadowTouch.cginc"
        #include "MeadowFlowerWind.cginc"
        struct Input {float4 vertexColor;float activity;float pulse;};
        float2 Turn(float2 p,float a){float s=sin(a),c=cos(a);return float2(c*p.x+s*p.y,-s*p.x+c*p.y);}
        void vert(inout appdata_full v,out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input,o);
            float3 center=float3(v.texcoord.x,v.texcoord1.x,v.texcoord.y);
            float phase=v.texcoord1.y,t=_OceanTime;
            float individual=frac(sin(phase*17.13+1.7)*437.17);
            float cycle=t*(.29+individual*.21)+phase;
            float day=smoothstep(.12,.65,_Daylight);
            float activity=lerp(day*(1-_MeadowRain),(1-day)*(1-.85*_MeadowRain),_EcologyMode);
            float3 drift=float3(.44*sin(cycle),(.09+.09*individual)*sin(t*(.71+individual*.63)+phase),.38*sin(cycle*.77+phase*1.9));
            float flight=1;float3 relocation=0;
            if(_EcologyMode<.5&&_MeadowMotionReady>.5)
            {
                int site=(int)floor((phase+.0001)/2.399963);
                int slot=clamp(site*2+(int)round((phase-site*2.399963)/1.83),0,31);
                float visit=frac((t+phase*2)/(11+individual*4));
                flight=smoothstep(.24,.38,visit)*(1-smoothstep(.72,.90,visit));
                float4 flower=_MeadowFlowers[slot],response=_MeadowResponses[slot];
                float4 home=_MeadowVisitingHomes[slot];float3 visitCenter=center;
                if(abs(home.w)>.5)
                {
                    visitCenter=mul(unity_WorldToObject,float4(home.xyz,1)).xyz;relocation=visitCenter-center;
                    activity*=step(0,home.w);
                }
                float3 landing=mul(unity_WorldToObject,float4(flower.xyz,1)).xyz-visitCenter;
                // The perched insect follows the same rooted pollen wind, including modal freeze.
                float windPhase=center.x*.71+center.z*.43;
                float sway=.7*sin(_IslandWindTime*1.1+windPhase)+.3*sin(_IslandWindTime*.57+windPhase*1.6);
                if(abs(home.w)>.5)
                {
                    float4 root=_MeadowFlowerRoots[slot];
                    appdata_full petal=(appdata_full)0;petal.vertex=float4(visitCenter+landing,1);petal.normal=float3(0,1,0);
                    // Remove the insect's clearance while evaluating the actual petal deformation.
                    petal.vertex.y-=.025;
                    MeadowFlowerVertex(petal,root.xyz,flower.w,root.w,MeadowSampleMask(root.xyz));
                    landing=petal.vertex.xyz+float3(0,.025,0)-visitCenter;
                }
                else landing.xz+=_IslandWindDirection.xy*flower.w*flower.w*.06*clamp(_IslandWindStrength,0,1.6)*sway;
                drift=lerp(landing,drift,flight);
                drift=lerp(drift,mul((float3x3)unity_WorldToObject,response.xyz),response.w);
                flight=lerp(flight,1,response.w);
            }
            float3 q=v.vertex.xyz-center;
            float flap=lerp(.95+.07*sin(t*1.6+phase),.45+.85*sin(t*(12+individual*3)+phase*2),flight)*sign(q.x)*v.texcoord2.x;
            float s=sin(flap),c=cos(flap);
            q.xy=float2(c*q.x-s*q.y,s*q.x+c*q.y);
            v.normal.xy=float2(c*v.normal.x-s*v.normal.y,s*v.normal.x+c*v.normal.y);
            float yaw=v.texcoord2.y+.35*sin(cycle*.7)*flight;
            q.xz=Turn(q.xz,yaw);v.normal.xz=Turn(v.normal.xz,yaw);
            drift*=lerp(.2,1,activity);drift.y-=.65*(1-activity);
            v.vertex.xyz=center+relocation+drift+q*smoothstep(0,.16,activity);
            o.vertexColor=v.color;o.activity=activity;
            // Different frequencies, duty cycles and a slower envelope avoid synchronized flashes.
            float pulse=.5+.5*sin(t*(1.05+individual*1.2)+phase*3);
            o.pulse=.18+.82*pow(pulse,2+individual*3)*(.72+.28*sin(t*.37+phase));
        }
        void surf(Input IN,inout SurfaceOutputStandard o)
        {
            clip(IN.activity-.015);
            float3 c=IN.vertexColor.rgb;
            #ifndef UNITY_COLORSPACE_GAMMA
            c=GammaToLinearSpace(c);
            #endif
            o.Albedo=c;o.Smoothness=.12;o.Metallic=0;o.Alpha=1;
            o.Emission=c*_EcologyMode*IN.pulse*IN.activity*2*smoothstep(.55,.8,IN.vertexColor.r);
        }
        ENDCG
    }
    Fallback "Diffuse"
}
