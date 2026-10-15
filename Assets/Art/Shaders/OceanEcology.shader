Shader "MagicIsland/Ocean Ecology"
{
    Properties { _Color ("Tint", Color)=(1,1,1,1) _OceanTime ("Ocean time", Float)=0 _FishScatter ("Fish scatter", Float)=0 }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Cull Off
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert addshadow
        #pragma target 3.0
        #include "UnityCG.cginc"
        #include "OceanFishMotion.cginc"
        float4 _Color;
        float _OceanTime,_FishScatter,_FishEnabled;
        float4 _FishGuide;
        struct Input { float4 vertexColor; };
        void vert(inout appdata_full v,out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input,o);o.vertexColor=v.color;
            if(_FishEnabled>.5 && v.texcoord1.w>.5)AnimateFish(v,_FishGuide,_OceanTime,_FishScatter);
            else
            {
                float3 w=mul(unity_ObjectToWorld,v.vertex).xyz;
                v.vertex.x+=sin(_OceanTime*.85+w.x*.9+w.z*.6)*v.color.a*.045;
                v.vertex.z+=cos(_OceanTime*.67+w.z*.8)*v.color.a*.025;
            }
        }
        void surf(Input IN,inout SurfaceOutputStandard o)
        {
            float3 color=IN.vertexColor.rgb;
            #ifndef UNITY_COLORSPACE_GAMMA
            color=GammaToLinearSpace(color);
            #endif
            o.Albedo=color*_Color.rgb;o.Metallic=0;o.Smoothness=.12;o.Alpha=1;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
