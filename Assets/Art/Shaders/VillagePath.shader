Shader "MagicIsland/Village Path"
{
    Properties
    {
        _Color ("原道路色", Color)=(.84,.75,.53,1)
        _Glossiness ("柔光", Range(0,1))=.08
        _EdgeSoftness ("路肩柔化", Range(.02,.25))=.15
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-10" }
        CGPROGRAM
        #pragma surface surf Standard alpha:fade vertex:vert
        #pragma target 3.0
        #include "CoastalWetSand.cginc"
        float4 _Color;
        float _Glossiness,_EdgeSoftness;
        struct Input { float2 pathUV; float disc; float3 worldPos; };
        void vert(inout appdata_full v,out Input o)
        { UNITY_INITIALIZE_OUTPUT(Input,o);o.pathUV=v.texcoord.xy;o.disc=v.texcoord2.x; }
        void surf(Input IN,inout SurfaceOutputStandard o)
        {
            float2 uv=IN.pathUV-.5;
            float edge=lerp(abs(uv.x),length(uv),saturate(IN.disc));
            float grain=BeachNoise(IN.worldPos.xz*2.8+16.1);
            float boundary=.495-(1-grain)*.035;
            o.Alpha=(1-smoothstep(boundary-_EdgeSoftness,boundary,edge))*_Color.a;
            o.Albedo=_Color.rgb*lerp(.97,1.025,BeachNoise(IN.worldPos.xz*5.1-27.4));
            o.Smoothness=_Glossiness;o.Metallic=0;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
