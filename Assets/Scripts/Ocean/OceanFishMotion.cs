using UnityEngine;

namespace MagicIsland.Ocean
{
    // Shared canonical/pose transforms and the legacy shader fallback. Production body
    // curves come from OceanFishSpine at the paused fixed clock, without per-fish objects.
    public static class OceanFishMotion
    {
        public static Vector2 Rotate(Vector2 p,float yaw)
            => new Vector2(p.x*Mathf.Cos(yaw)+p.y*Mathf.Sin(yaw),-p.x*Mathf.Sin(yaw)+p.y*Mathf.Cos(yaw));

        public static void Pose(Vector4 pivot,Vector4 guide,float time,float scatter,out Vector3 position,out float yaw)
        {
            float phase=pivot.w, rate=guide.w*(.7f+.2f*Mathf.Sin(phase));
            float pulse=time*rate+phase;
            float angle=guide.z+.32f*Mathf.Sin(phase)+.24f*Mathf.Sin(pulse);
            float tangent=Mathf.Atan2(-Mathf.Sin(angle),Mathf.Cos(angle)*guide.y);
            var offset=new Vector2(pivot.x+.07f*Mathf.Sin(pulse*1.31f),pivot.z+.07f*Mathf.Cos(pulse*.83f+phase));
            var away=new Vector2(Mathf.Sin(phase*1.7f),Mathf.Cos(phase*1.7f));
            offset+=away*scatter;
            var rotated=Rotate(offset,tangent);
            position=new Vector3(Mathf.Cos(angle)*guide.x+rotated.x,pivot.y,Mathf.Sin(angle)*guide.x*guide.y+rotated.y);
            yaw=tangent+.18f*Mathf.Sin(pulse*1.17f+phase);
        }

        public static Vector3 Deform(Vector3 vertex,Vector4 pivot,Vector4 shape,Vector4 guide,float time,float scatter)
        {
            Pose(pivot,guide,time,scatter,out var position,out float yaw);
            return position-new Vector3(Mathf.Cos(guide.z)*guide.x,0,Mathf.Sin(guide.z)*guide.x*guide.y)+Body(vertex,pivot,shape,time,yaw);
        }
        public static Vector3 Body(Vector3 vertex,Vector4 pivot,Vector4 shape,float time,float yaw,float pitch=0)
        {
            var relative=Local(vertex,pivot,shape);
            float beat=time*(shape.y>2.5f?2.1f:7.5f+1.4f*Mathf.Sin(pivot.w))+pivot.w*2;
            if(shape.y>2.5f)relative.y+=Mathf.Sin(beat)*.012f*Mathf.Clamp01(Mathf.Abs(relative.x)/.45f);
            else relative.x+=Mathf.Sin(beat+relative.z*6)*.035f*Mathf.Clamp01(-relative.z/.30f);
            float y=relative.y*Mathf.Cos(pitch)+relative.z*Mathf.Sin(pitch);
            relative.z=relative.z*Mathf.Cos(pitch)-relative.y*Mathf.Sin(pitch);relative.y=y;
            var xz=Rotate(new Vector2(relative.x,relative.z),yaw);
            return new Vector3(xz.x,relative.y,xz.y);
        }
        public static Vector3 Local(Vector3 vertex,Vector4 pivot,Vector4 shape)
        {var xz=Rotate(new Vector2(vertex.x-pivot.x,vertex.z-pivot.z),-shape.x);return new Vector3(xz.x,vertex.y-pivot.y,xz.y);}
        public static Vector3 Orient(Vector3 relative,float yaw,float pitch)
        {
            float y=relative.y*Mathf.Cos(pitch)+relative.z*Mathf.Sin(pitch);
            relative.z=relative.z*Mathf.Cos(pitch)-relative.y*Mathf.Sin(pitch);relative.y=y;
            var xz=Rotate(new Vector2(relative.x,relative.z),yaw);return new Vector3(xz.x,relative.y,xz.y);
        }
    }
}
