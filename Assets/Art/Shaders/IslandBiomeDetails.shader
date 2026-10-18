Shader "MagicIsland/Island Biome Details"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert
        #pragma target 3.0
        struct Input { float4 vertexColor; };
        void vert(inout appdata_full v,out Input o) { UNITY_INITIALIZE_OUTPUT(Input,o);o.vertexColor=v.color; }
        void surf(Input IN,inout SurfaceOutputStandard o)
        {
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
