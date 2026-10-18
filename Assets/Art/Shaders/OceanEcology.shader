Shader "MagicIsland/Ocean Ecology"
{
    Properties
    {
        _Color ("Tint", Color)=(1,1,1,1)
        _OceanTime ("Ocean time", Float)=0
        _FishScatter ("Fish scatter", Float)=0
        _VertexSaturation ("顶点色饱和度", Range(0,3))=1
        _VertexBrightness ("顶点色亮度", Range(0.5,1.5))=1
    }
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
        float _OceanTime,_FishScatter,_FishEnabled,_VertexSaturation,_VertexBrightness;
        float4 _FishGuide;
        float4 _CrabMotion;
        float4 _CrabPose;
        float4 _GullPose;
        struct Input { float4 vertexColor; };
        void vert(inout appdata_full v,out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input,o);o.vertexColor=v.color;
            if(_GullPose.w>.5)
            {
                float weight=smoothstep(.28,.37,v.vertex.y)*smoothstep(.07,.18,v.vertex.z);
                float pitch=_GullPose.x*weight,yaw=_GullPose.y*weight;
                float sp=sin(pitch),cp=cos(pitch),sy=sin(yaw),cy=cos(yaw);
                float3 p=v.vertex.xyz-float3(0,.29,.17);
                p.yz=float2(cp*p.y-sp*p.z,sp*p.y+cp*p.z);
                p.xz=float2(cy*p.x+sy*p.z,-sy*p.x+cy*p.z);
                v.vertex.xyz=p+float3(0,.29,.17);
                v.normal.yz=float2(cp*v.normal.y-sp*v.normal.z,sp*v.normal.y+cp*v.normal.z);
                v.normal.xz=float2(cy*v.normal.x+sy*v.normal.z,-sy*v.normal.x+cy*v.normal.z);
                v.tangent.yz=float2(cp*v.tangent.y-sp*v.tangent.z,sp*v.tangent.y+cp*v.tangent.z);
                v.tangent.xz=float2(cy*v.tangent.x+sy*v.tangent.z,-sy*v.tangent.x+cy*v.tangent.z);
            }
            else if(_CrabMotion.w>.5)
            {
                if(v.texcoord1.w>=100 && v.texcoord1.w<104)
                {
                    float angle=sin(_CrabMotion.x+v.texcoord2.x+_CrabMotion.z)*.28*_CrabMotion.y;
                    float s=sin(angle),c=cos(angle);
                    float3 p=v.vertex.xyz-v.texcoord1.xyz;
                    v.vertex.xz=float2(c*p.x+s*p.z,-s*p.x+c*p.z)+v.texcoord1.xz;
                    v.normal.xz=float2(c*v.normal.x+s*v.normal.z,-s*v.normal.x+c*v.normal.z);
                }
                else if(_CrabPose.w>.5)
                {
                    bool claw=v.texcoord1.w>=110 && v.texcoord1.w<112;
                    float angle=claw?(v.texcoord1.w<111?_CrabPose.x:_CrabPose.y):_CrabPose.z;
                    float3 pivot=claw?v.texcoord1.xyz:float3(0,.09,0);
                    float s=sin(angle),c=cos(angle);float3 p=v.vertex.xyz-pivot;
                    v.vertex.yz=float2(c*p.y-s*p.z,s*p.y+c*p.z)+pivot.yz;
                    v.normal.yz=float2(c*v.normal.y-s*v.normal.z,s*v.normal.y+c*v.normal.z);
                    v.tangent.yz=float2(c*v.tangent.y-s*v.tangent.z,s*v.tangent.y+c*v.tangent.z);
                }
            }
            else if(_FishEnabled>.5 && v.texcoord1.w>.5)AnimateFish(v,_FishGuide,_OceanTime,_FishScatter);
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
            if(_VertexSaturation!=1 || _VertexBrightness!=1)
            {
                float grey=dot(color,float3(.2126,.7152,.0722));
                color=saturate(lerp(grey.xxx,color,_VertexSaturation)*_VertexBrightness);
            }
            #ifndef UNITY_COLORSPACE_GAMMA
            color=GammaToLinearSpace(color);
            #endif
            o.Albedo=color*_Color.rgb;o.Metallic=0;o.Smoothness=.12;o.Alpha=1;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
