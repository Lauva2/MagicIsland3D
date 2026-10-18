Shader "MagicIsland/Island Shore"
{
    Properties
    {
        _Color ("整体色", Color)=(1,1,1,1)
        _WetSandStrength ("湿沙深浅", Range(0,1))=.75
        _WetSandSmoothness ("湿沙柔光", Range(.03,.5))=.28
        _WetSandBand ("湿沙高度：湿／干／海底淡出／海底起点", Vector)=(-.16,-.005,-.32,-.235)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert
        #pragma target 3.0
        #include "SeasonalColor.cginc"
        #include "CoastalWetSand.cginc"
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
            IslandWetSand(IN.worldPos,IN.vertexColor.rgb,o.Albedo,o.Smoothness);
            o.Metallic=0; o.Alpha=1;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
