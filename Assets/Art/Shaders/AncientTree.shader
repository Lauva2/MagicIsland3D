Shader "MagicIsland/Ancient Tree"
{
    Properties { _Glow("Lantern glow",Range(0,2))=.65 _OceanTime("Ocean time",Float)=0 _Daylight("Daylight",Range(0,1))=1 }
    SubShader
    {
        Tags {"RenderType"="Opaque"}
        Cull Off
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert addshadow
        #pragma target 3.0
        float _Glow,_OceanTime,_Daylight;
        struct Input {float4 vertexColor;float2 motion;};
        void vert(inout appdata_full v,out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input,o);o.vertexColor=v.color;o.motion=v.texcoord.xy;
            if(v.texcoord.y>1)
            {v.vertex.x+=sin(_OceanTime*.31+v.texcoord.x)*.35;v.vertex.z+=cos(_OceanTime*.27+v.texcoord.x)*.35;v.vertex.y+=sin(_OceanTime*.46+v.texcoord.x)*.45;}
            else
            {float sway=v.color.a*saturate(v.vertex.y/7);v.vertex.x+=sin(_OceanTime*.6+v.vertex.z*.2)*sway*.16;v.vertex.z+=cos(_OceanTime*.48+v.vertex.x*.2)*sway*.1;}
        }
        void surf(Input IN,inout SurfaceOutputStandard o)
        {
            float3 c=IN.vertexColor.rgb;
            float accent=saturate((c.b-c.r)*3)+saturate((c.g-c.r-.2)*3);
            if(IN.motion.y>1)accent=1;
            #ifndef UNITY_COLORSPACE_GAMMA
            c=GammaToLinearSpace(c);
            #endif
            o.Albedo=c;o.Metallic=0;o.Smoothness=.12;
            float pulse=IN.motion.y>1?.7+.3*sin(_OceanTime*.7+IN.motion.x):1;
            o.Emission=c*accent*_Glow*lerp(1,.16,_Daylight)*pulse;o.Alpha=1;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
