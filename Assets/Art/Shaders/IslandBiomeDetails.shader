Shader "MagicIsland/Island Biome Details"
{
    Properties
    {
        _CrownSway ("Grassland crown sway (m)", Range(0,.16))=.10
        _CrownFlutter ("Grassland leaf drift (m)", Range(0,.035))=.018
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert addshadow
        #pragma target 3.0
        #include "GrasslandCrownWind.cginc"
        #include "LandscapeCrownFade.cginc"
        struct Input { float4 vertexColor; float4 screenPos; };
        void vert(inout appdata_full v,out Input o) { UNITY_INITIALIZE_OUTPUT(Input,o);o.vertexColor=float4(v.color.rgb,IslandCrownOpacity(v.texcoord));IslandCrownWind(v); }
        void surf(Input IN,inout SurfaceOutputStandard o)
        {
            IslandCrownClip(IN.vertexColor.a,IN.screenPos);
            float3 c=IN.vertexColor.rgb;
            #ifndef UNITY_COLORSPACE_GAMMA
            c=GammaToLinearSpace(c);
            #endif
            o.Albedo=c;o.Smoothness=.08;o.Metallic=0;o.Alpha=1;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
