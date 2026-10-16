float _FishSteered;
float4 _FishPoses[32];
float4 _FishAttitudes[32];
float _FishBodyEnabled;
float4 _FishSpines[160];
float3 FishPitch(float3 p,float pitch)
{
    float s,c;sincos(pitch,s,c);return float3(p.x,p.y*c+p.z*s,p.z*c-p.y*s);
}
// CPU counterparts: OceanFishMotion / OceanFishSpine and OceanSchool.DeformedVertex.
float2 FishRotate(float2 p,float yaw)
{
    float s,c;sincos(yaw,s,c);return float2(p.x*c+p.y*s,-p.x*s+p.y*c);
}
void FishPose(float4 pivot,float4 guide,float time,float scatter,out float3 position,out float yaw)
{
    float phase=pivot.w,rate=guide.w*(.7+.2*sin(phase));
    float pulse=time*rate+phase;
    float angle=guide.z+.32*sin(phase)+.24*sin(pulse);
    float tangent=atan2(-sin(angle),cos(angle)*guide.y);
    float2 offset=pivot.xz+.07*float2(sin(pulse*1.31),cos(pulse*.83+phase));
    offset+=float2(sin(phase*1.7),cos(phase*1.7))*scatter;
    position=float3(cos(angle)*guide.x,pivot.y,sin(angle)*guide.x*guide.y);
    position.xz+=FishRotate(offset,tangent);
    yaw=tangent+.18*sin(pulse*1.17+phase);
}
void AnimateFish(inout appdata_full v,float4 guide,float time,float scatter)
{
    float4 pivot=v.texcoord1,shape=v.texcoord2;
    float3 relative=v.vertex.xyz-pivot.xyz;
    relative.xz=FishRotate(relative.xz,-shape.x);
    v.normal.xz=FishRotate(v.normal.xz,-shape.x);
    v.tangent.xz=FishRotate(v.tangent.xz,-shape.x);
    float3 position;float yaw;float pitch=0;
    int member=(int)round((pivot.w-1)/2.399963);
    if(_FishSteered>.5)
    {
        float4 pose=_FishPoses[member];position=pose.xyz;yaw=pose.w;pitch=_FishAttitudes[member].x;
    }
    else
    {
        FishPose(pivot,guide,time,scatter,position,yaw);
        position.xz-=float2(cos(guide.z),sin(guide.z)*guide.y)*guide.x;
    }
    if(_FishSteered>.5 && _FishBodyEnabled>.5)
    {
        // Fixed-length head-to-tail nodes; the original cross-section is preserved.
        float4 attitude=_FishAttitudes[member];float length=max(.0001,attitude.y-attitude.z);
        float u=clamp((attitude.y-relative.z)/length*4,0,4);int link=min(3,(int)u);
        float2 a=_FishSpines[member*5+link].xy,b=_FishSpines[member*5+link+1].xy;
        float2 direction=normalize(b-a),right=float2(-direction.y,direction.x);
        relative.xz=lerp(a,b,u-link)+right*relative.x;
        float bend=atan2(-direction.x,-direction.y);
        v.normal.xz=FishRotate(v.normal.xz,bend);v.tangent.xz=FishRotate(v.tangent.xz,bend);
        if(shape.y>2.5)relative.y+=sin(attitude.w)*.012*saturate(abs(relative.x)/.45);
    }
    else
    {
        float beat=time*(shape.y>2.5?2.1:7.5+1.4*sin(pivot.w))+pivot.w*2;
        if(shape.y>2.5)relative.y+=sin(beat)*.012*saturate(abs(relative.x)/.45);
        else relative.x+=sin(beat+relative.z*6)*.035*saturate(-relative.z/.30);
    }
    relative=FishPitch(relative,pitch);relative.xz=FishRotate(relative.xz,yaw);v.vertex.xyz=position+relative;
    v.normal=FishPitch(v.normal,pitch);v.normal.xz=FishRotate(v.normal.xz,yaw);
    v.tangent.xyz=FishPitch(v.tangent.xyz,pitch);v.tangent.xz=FishRotate(v.tangent.xz,yaw);
}
