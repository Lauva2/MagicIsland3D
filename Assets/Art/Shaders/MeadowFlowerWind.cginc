#ifndef ISLAND_MEADOW_FLOWER_WIND
#define ISLAND_MEADOW_FLOWER_WIND
// Shared by the authored flower vertex and its perched butterfly.
float2 MeadowFlowerOffset(float weight,float phase)
{
    float time=_IslandWindTime;
    float sway=.74*sin(time*.71+phase)+.26*sin(time*1.23+phase*1.7);
    float cross=.14*sin(time*.93+phase*2.1);
    float2 direction=_IslandWindDirection.xy;
    return (direction*sway+float2(-direction.y,direction.x)*cross)
        *(weight*weight*.032*clamp(_IslandWindStrength,0,1.6));
}
void MeadowFlowerVertex(inout appdata_full v,float3 root,float phase,float weight,float mask)
{
    v.vertex.xz+=MeadowFlowerOffset(weight,phase);
    MeadowTouch(v,root,mask,0,1,weight);
}
#endif
