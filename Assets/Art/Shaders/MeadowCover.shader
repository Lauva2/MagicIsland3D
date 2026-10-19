Shader "MagicIsland/Meadow Cover"
{
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert addshadow
        #pragma target 3.0
        #include "SeasonalColor.cginc"
        float _IslandWindTime,_IslandWindStrength,_MeadowCutCount;
        float4 _IslandWindDirection,_MeadowCuts[128];
        struct Input {float4 vertexColor;float visible;float season;};
        void vert(inout appdata_full v,out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input,o);o.vertexColor=v.color;o.visible=1;o.season=v.texcoord2.y;
            float3 root=mul(unity_ObjectToWorld,float4(v.texcoord1.x,v.texcoord2.x,v.texcoord1.y,1)).xyz;
            for(int i=0;i<(int)_MeadowCutCount;i++)
                if(all(abs(root.xz-_MeadowCuts[i].xy)<=_MeadowCuts[i].zw)){o.visible=0;break;}
            float weight=v.texcoord.x*v.texcoord.x;
            float sway=.7*sin(_IslandWindTime*1.1+v.texcoord.y)+.3*sin(_IslandWindTime*.57+v.texcoord.y*1.6);
            v.vertex.xz+=_IslandWindDirection.xy*(weight*.045*clamp(_IslandWindStrength,0,1.6)*sway);
        }
        void surf(Input IN,inout SurfaceOutputStandard o)
        {
            clip(IN.visible-.5);float3 c=IN.vertexColor.rgb;
            #ifndef UNITY_COLORSPACE_GAMMA
            c=GammaToLinearSpace(c);
            #endif
            o.Albedo=IslandSeasonColor(c,_IslandSeasonGrass,IN.season*.65);o.Smoothness=.06;o.Metallic=0;o.Alpha=1;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
