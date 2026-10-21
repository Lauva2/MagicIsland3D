#ifndef ISLAND_MEADOW_SHRUB_WIND
#define ISLAND_MEADOW_SHRUB_WIND
// Explicit UV0 codec: body -8..-9, tip -10..-11, branch -12..-13;
// the paired phase sentinel protects all original grass, flowers, rocks and other shader users.
bool MeadowShrubWind(inout appdata_full v,float3 root)
{
    float encoded=v.texcoord.x,phase=-v.texcoord.y-128;
    bool body=encoded<=-8&&encoded>=-9,tip=encoded<=-10&&encoded>=-11,stem=encoded<=-12&&encoded>=-13;
    if(v.texcoord.y>-128||v.texcoord.y< -134.284||(!body&&!tip&&!stem))return false;
    float strength=clamp(_IslandWindStrength,0,1.6);
    if(strength<=0)return true;
    float3 world=mul(unity_ObjectToWorld,v.vertex).xyz;
    float height=world.y-root.y,time=_IslandWindTime;
    float weight=tip?saturate((height-.08)/1.4):saturate(-encoded-(stem?12:8));
    float bend=weight*weight,derivative=weight>0&&weight<1?2*weight/1.4:0;
    float rootPhase=dot(root,float3(.71,.29,.43));
    float slow=.7*sin(time*.61+rootPhase)+.3*sin(time*.29+rootPhase*1.7);
    float cross=.010*sin(time*.43-rootPhase*.6);
    float lobe=stem?0:.022*(.75*sin(time*.93+phase)+.25*sin(time*1.37-phase*.6));
    float2 direction=_IslandWindDirection.xy,side=float2(-direction.y,direction.x);
    float2 field=(direction*(.045*slow+lobe*.5)+side*(cross+lobe))*strength;
    float2 offset=field*bend;
    float3 normal=UnityObjectToWorldNormal(v.normal);
    normal.y-=dot(normal.xz,field)*derivative;
    if(tip)
    {
        float leafWeight=saturate(-encoded-10);
        float flutter=.006*strength*sin(time*3.1+phase*2+dot(world.xz-root.xz,float2(9,11)));
        offset+=side*(flutter*leafWeight*leafWeight);
        normal.y-=dot(normal.xz,side)*flutter*(2*leafWeight/.16);
    }
    world.xz+=offset;
    v.vertex=mul(unity_WorldToObject,float4(world,1));
    v.normal=normalize(mul(normal,(float3x3)unity_ObjectToWorld));
    return true;
}
#endif
