#ifndef ISLAND_MEADOW_TOUCH
#define ISLAND_MEADOW_TOUCH
float _MeadowTouchCount;
float4 _MeadowTouches[16],_MeadowTouchArea,_MeadowTouchShape,_MeadowTouchWave;
float MeadowSampleMask(float3 root)
{
    if(_MeadowTouchArea.z<=0)return 0;
    return 1-smoothstep(_MeadowTouchArea.z-_MeadowTouchArea.w,_MeadowTouchArea.z,length(root.xz-_MeadowTouchArea.xy));
}
void MeadowTouch(inout appdata_full v,float3 root,float mask,float grass,float flower,float authoredWeight)
{
    if(mask<=0||(!grass&&!flower))return;
    float3 world=mul(unity_ObjectToWorld,v.vertex).xyz;
    float2 push=0;float pressure=0;
    for(int i=0;i<(int)_MeadowTouchCount;i++)
    {
        float2 delta=root.xz-_MeadowTouches[i].xy;
        float distance=length(delta);
        float amount=(1-smoothstep(0,_MeadowTouchShape.x,distance))*_MeadowTouches[i].w;
        amount*=1-smoothstep(.25,1.1,abs(root.y-_MeadowTouches[i].z));
        if(amount>pressure)
        {
            pressure=amount;
            float2 away=delta+(world.xz-root.xz)*(.35*(1-smoothstep(.05,.3,distance)));
            push=away/max(.08,length(away));
        }
    }
    float height=max(0,world.y-root.y-.01);
    float reference=grass ? .26 : .9;
    float weight=min(saturate(height/reference),saturate(authoredWeight*3));
    float amplitude=mask*pressure*_MeadowTouchShape.y*(grass?1:_MeadowTouchShape.z);
    float2 offset=push*amplitude*weight*weight;
    world.xz+=offset;
    world.y-=min(height*.18,amplitude*.18*weight*weight);
    float3 n=UnityObjectToWorldNormal(v.normal);
    float derivative=height<reference?2*weight/reference:0;
    n.y-=dot(n.xz,push*amplitude)*derivative;
    v.normal=normalize(mul(n,(float3x3)unity_ObjectToWorld));
    // Includes both wind and touch; preserves the existing 1.85m complete-plant building cut.
    float2 radial=world.xz-root.xz;float radius=length(radial);
    world.xz=root.xz+radial*min(1,_MeadowTouchShape.w/max(.0001,radius));
    v.vertex=mul(unity_WorldToObject,float4(world,1));
}
#endif
