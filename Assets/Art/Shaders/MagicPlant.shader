Shader "Magic Island/Moonflower"
{
    Properties { _Color ("Petal colour", Color) = (0.55,0.8,1,1) _Glow ("Moon glow", Range(0,3)) = 1.1 }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        fixed4 _Color;
        half _Glow;
        float _IslandMagicNight;
        struct Input { float3 worldPos; };
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            o.Albedo = _Color.rgb; o.Metallic = 0; o.Smoothness = .22;
            o.Emission = _Color.rgb * _Glow * saturate(_IslandMagicNight); o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
