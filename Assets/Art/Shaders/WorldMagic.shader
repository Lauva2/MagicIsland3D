Shader "MagicIsland/World Magic"
{
    Properties { _Glow ("Subtle glow",Range(0,0.3))=0.08 }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert
        #pragma target 3.0
        float _Glow;
        struct Input {float4 vertexColor;};
        void vert(inout appdata_full v,out Input o){UNITY_INITIALIZE_OUTPUT(Input,o);o.vertexColor=v.color;}
        void surf(Input IN,inout SurfaceOutputStandard o)
        {
            float3 c=IN.vertexColor.rgb;
            #ifndef UNITY_COLORSPACE_GAMMA
            c=GammaToLinearSpace(c);
            #endif
            o.Albedo=c;o.Smoothness=.18;o.Metallic=0;
            float accent=saturate(max(c.b-c.r,c.r-c.b)-.12)*2;
            o.Emission=c*_Glow*accent;o.Alpha=1;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
