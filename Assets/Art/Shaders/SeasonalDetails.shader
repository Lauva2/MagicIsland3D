Shader "MagicIsland/Seasonal Details"
{
    Properties { _LeafEnabled ("Tree canopy present", Float)=0 }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Cull Off
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert addshadow
        #pragma target 3.0
        float _IslandWindTime,_IslandWindStrength,_LeafEnabled;
        float4 _IslandWindDirection;
        struct Input { float4 vertexColor; float falling; };
        void vert(inout appdata_full v,out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input,o);o.vertexColor=v.color;o.falling=v.texcoord1.x;
            float3 anchor=mul(unity_ObjectToWorld,float4(0,0,0,1)).xyz;
            float phase=dot(anchor.xz,float2(.71,.43));
            float2 wind=mul((float3x3)unity_WorldToObject,float3(_IslandWindDirection.x,0,_IslandWindDirection.y)).xz;
            wind/=max(.01,length(wind));
            if(v.texcoord1.x>.5)
            {
                float cycle=frac(_IslandWindTime*.11+v.texcoord1.y+phase);
                float angle=_IslandWindTime*1.7+v.texcoord1.y*23+phase;
                v.vertex.y-=cycle*2.0;
                v.vertex.xz+=float2(sin(angle),cos(angle*.7))*.10+wind*(cycle-.5)*.12;
            }
            else
                v.vertex.xz+=wind*sin(_IslandWindTime*1.3+phase+v.vertex.x*4)*min(.04,_IslandWindStrength*.018)*saturate(v.vertex.y/.24);
        }
        void surf(Input IN,inout SurfaceOutputStandard o)
        {
            clip(1-IN.falling*(1-_LeafEnabled)-.5);
            float3 color=IN.vertexColor.rgb;
            #ifndef UNITY_COLORSPACE_GAMMA
            color=GammaToLinearSpace(color);
            #endif
            o.Albedo=color;o.Alpha=1;o.Smoothness=.03;o.Metallic=0;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
