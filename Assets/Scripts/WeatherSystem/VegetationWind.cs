using UnityEngine;

namespace MagicIsland.WeatherSystem
{
    // Per-renderer authored shape controls; WeatherManager owns the shared wind clock and strength.
    [DisallowMultipleComponent,RequireComponent(typeof(MeshRenderer),typeof(MeshFilter))]
    public sealed class VegetationWind : MonoBehaviour
    {
        public MeshRenderer Target;
        [Min(0)] public float Bend=.10f,Flutter=.025f;
        public bool Radial;
        private MaterialPropertyBlock properties;
        private Bounds originalBounds;
        private bool captured;
        private static readonly int ShapeId=Shader.PropertyToID("_WindShape");
        private static readonly int BendId=Shader.PropertyToID("_WindBend");
        private static readonly int FlutterId=Shader.PropertyToID("_WindFlutter");
        private void OnEnable()
        {
            if(Target==null)Target=GetComponent<MeshRenderer>();var mesh=GetComponent<MeshFilter>().sharedMesh;
            if(Target==null||mesh==null)return;
            originalBounds=Target.localBounds;captured=true;
            var bounds=mesh.bounds;float radius=Mathf.Max(bounds.extents.x,bounds.extents.z);
            if(properties==null)properties=new MaterialPropertyBlock();Target.GetPropertyBlock(properties);
            properties.SetVector(ShapeId,new Vector4(bounds.min.y,Mathf.Max(.01f,bounds.size.y),Mathf.Max(.01f,radius),Radial?1:0));
            properties.SetFloat(BendId,Bend);properties.SetFloat(FlutterId,Flutter);Target.SetPropertyBlock(properties);
            bounds=originalBounds;bounds.Expand(Mathf.Max(Bend,Flutter)*8);Target.localBounds=bounds;
        }
        private void OnDisable()
        {
            if(Target==null||!captured)return;
            Target.GetPropertyBlock(properties);properties.SetFloat(BendId,0);properties.SetFloat(FlutterId,0);Target.SetPropertyBlock(properties);
            Target.localBounds=originalBounds;captured=false;
        }
    }
}
