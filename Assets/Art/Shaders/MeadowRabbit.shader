Shader "MagicIsland/Meadow Rabbit"
{
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert addshadow
        #pragma target 3.0
        float4 _RabbitPose[8],_RabbitActions[8],_RabbitEars[8];
        float _RabbitVisible[8],_RabbitBound;
        struct Input {float4 vertexColor;float visible;};
        float3 AroundX(float3 q,float a){float s=sin(a),c=cos(a);return float3(q.x,c*q.y-s*q.z,s*q.y+c*q.z);}
        float3 AroundZ(float3 q,float a){float s=sin(a),c=cos(a);return float3(c*q.x-s*q.y,s*q.x+c*q.y,q.z);}
        void vert(inout appdata_full v,out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input,o);
            int i=clamp((int)round(v.texcoord.y),0,7);float part=v.texcoord.x;
            float4 pose=_RabbitPose[i],action=_RabbitActions[i];float3 q=v.vertex.xyz-v.texcoord2.xyz,pivot=v.texcoord1.xyz;
            float visible=_RabbitVisible[i];
            if(_RabbitBound<.5){pose=float4(mul(unity_ObjectToWorld,float4(v.texcoord2.xyz,1)).xyz,0);action=0;visible=1;}
            if(part>1.5&&part<3.5)
            {
                float flick=_RabbitBound>.5?(part<2.5?_RabbitEars[i].x:_RabbitEars[i].y):0;
                float ear=(part<2.5?-1:1)*(.10+.13*action.w+.06*action.z+flick);
                q=pivot+AroundZ(q-pivot,ear);v.normal=AroundZ(v.normal,ear);
            }
            if(part>.5&&part<3.5)
            {
                // Head and ears share the neck motion; individual ear pivots remain rigid.
                float3 neck=float3(0,.40,.17);float bend=action.y*.42;
                q=neck+AroundZ(AroundX(q-neck,bend),action.z);v.normal=AroundZ(AroundX(v.normal,bend),action.z);
            }
            if(part>3.5)
            {
                float fold=action.x*(part>5.5?-.48:.45);
                q=pivot+AroundX(q-pivot,fold);v.normal=AroundX(v.normal,fold);
            }
            q.y+=action.x*.18;
            float s=sin(pose.w),c=cos(pose.w);
            q.xz=float2(c*q.x+s*q.z,-s*q.x+c*q.z);
            v.normal.xz=float2(c*v.normal.x+s*v.normal.z,-s*v.normal.x+c*v.normal.z);
            v.vertex.xyz=mul(unity_WorldToObject,float4(pose.xyz,1)).xyz+q*visible;
            o.vertexColor=v.color;o.visible=visible;
        }
        void surf(Input IN,inout SurfaceOutputStandard o)
        {
            clip(IN.visible-.015);float3 c=IN.vertexColor.rgb;
            #ifndef UNITY_COLORSPACE_GAMMA
            c=GammaToLinearSpace(c);
            #endif
            o.Albedo=c;o.Smoothness=.13;o.Metallic=0;o.Alpha=1;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
