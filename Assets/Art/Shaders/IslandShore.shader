Shader "MagicIsland/Island Shore"
{
    Properties { _Color ("整体色", Color)=(1,1,1,1) }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert
        #pragma target 3.0
        #include "SeasonalColor.cginc"
        float4 _Color;
        struct Input { float4 vertexColor; float3 worldPos; };
        void vert(inout appdata_full v, out Input o) { UNITY_INITIALIZE_OUTPUT(Input,o); o.vertexColor=v.color; }
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float3 color=IN.vertexColor.rgb;
            #ifndef UNITY_COLORSPACE_GAMMA
            color=GammaToLinearSpace(color);
            #endif
            float grass=smoothstep(.01,.12,IN.vertexColor.g-IN.vertexColor.r);
            float dry=smoothstep(-.20,-.08,IN.worldPos.y);
            o.Albedo=IslandSeasonColor(color*_Color.rgb,_IslandSeasonGrass,grass*dry);
            o.Smoothness=.03; o.Metallic=0; o.Alpha=1;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
