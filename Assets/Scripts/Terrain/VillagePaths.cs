using MagicIsland.Configuration;
using MagicIsland.Interaction;
using UnityEngine;

namespace MagicIsland.TerrainSystem
{
    public sealed class VillagePaths : MonoBehaviour
    {
        public Transform[] Segments = new Transform[8];
        public Transform[] Junctions = new Transform[4];
        public float Width = 1.15f;
        public bool FollowFarmLayout = true;
        public bool IsValid => Segments!=null&&Segments.Length==8&&System.Array.TrueForAll(Segments,t=>t!=null)
            &&Junctions!=null&&Junctions.Length==4&&System.Array.TrueForAll(Junctions,t=>t!=null)
            &&Width>=.6f&&Width<=2&&GetComponentsInChildren<Collider>(true).Length==0;

        public void Configure(int farmSize,HousingConfig housing)
        {
            if(!FollowFarmLayout)return;
            var home=housing.ExteriorPosition(farmSize);float hall=Mathf.Min(9.3f,farmSize*.6f+2.7f);
            float row=-Mathf.Max(7.2f,farmSize*.6f+1.8f),corridor=11.2f;
            Set(0,new Vector2(home.x,home.z-1.10f),new Vector2(home.x,row));
            Set(1,new Vector2(home.x,row),new Vector2(corridor,row));
            Set(2,new Vector2(corridor,row),new Vector2(corridor,1.4f));
            Set(3,new Vector2(hall-.55f,1.4f),new Vector2(corridor,1.4f));
            Set(4,new Vector2(hall-.55f,2.65f),new Vector2(hall-.55f,1.4f));
            Set(5,new Vector2(hall+1.28f,2.0f),new Vector2(hall+1.28f,1.4f));
            Set(6,new Vector2(corridor,row),new Vector2(corridor,(float)IslandLayout.PierZ));
            Set(7,new Vector2(corridor,(float)IslandLayout.PierZ),new Vector2((float)IslandLayout.PierX-1.2f,(float)IslandLayout.PierZ));
            var points=new[]{new Vector2(home.x,row),new Vector2(corridor,row),new Vector2(corridor,1.4f),new Vector2(corridor,(float)IslandLayout.PierZ)};
            for(int i=0;i<points.Length;i++){Junctions[i].localPosition=Point(points[i])+Vector3.up*.003f;Junctions[i].localScale=new Vector3(Width,1,Width);}
        }
        private static Vector3 Point(Vector2 p)=>new Vector3(p.x,(float)IslandLayout.Height(p.x,p.y)+.012f,p.y);
        private void Set(int index,Vector2 from,Vector2 to)
        {
            var a=Point(from);var b=Point(to);var delta=b-a;
            Segments[index].gameObject.SetActive(delta.sqrMagnitude>.001f);
            if(delta.sqrMagnitude<=.001f)return;
            Segments[index].localPosition=(a+b)*.5f;Segments[index].localRotation=Quaternion.LookRotation(delta.normalized,Vector3.up);
            Segments[index].localScale=new Vector3(Width,1,delta.magnitude);
        }
    }
}
