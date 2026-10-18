Shader "MagicIsland/Meadow Foliage"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert addshadow
        #pragma target 3.0
        float _IslandWindTime,_IslandWindStrength;
        float4 _IslandWindDirection;
        struct Input {float4 vertexColor;};
        void vert(inout appdata_full v,out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input,o);o.vertexColor=v.color;
            float phase=v.texcoord.y,weight=v.texcoord.x*v.texcoord.x;
            float sway=.7*sin(_IslandWindTime*1.1+phase)+.3*sin(_IslandWindTime*.57+phase*1.6);
            v.vertex.xz+=_IslandWindDirection.xy*(weight*.06*clamp(_IslandWindStrength,0,1.6)*sway);
        }
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
