using MagicIsland.Interaction;
using MagicIsland.TerrainSystem;
using UnityEngine;
using UnityEngine.UI;

namespace MagicIsland.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class IslandMapGraphic : MaskableGraphic
    {
        public Color Sea = new Color(.31f, .63f, .69f), Shallows = new Color(.46f, .76f, .70f), Sand = new Color(.92f, .83f, .59f), Grass = new Color(.57f, .69f, .40f);
        private int farmSize = 5;
        private Vector4[] roads = new Vector4[0];
        private IslandBiomeSettings biomes;
        private bool worldOverview;
        public bool WorldOverview {get=>worldOverview;set{if(worldOverview==value)return;worldOverview=value;SetVerticesDirty();}}
        public void SetMap(int size, Vector4[] segments, IslandBiomeSettings regions = null)
        { if (farmSize == size && ReferenceEquals(roads, segments) && ReferenceEquals(biomes, regions)) return; farmSize = size; roads = segments; biomes = regions; SetVerticesDirty(); }
        private Vector2 Point(Vector2 n) => new Vector2(rectTransform.rect.xMin + n.x * rectTransform.rect.width, rectTransform.rect.yMin + n.y * rectTransform.rect.height);
        private Vector2 World(float x, float z) => Point(new Vector2((float)MapProjection.Normalize(x), (float)MapProjection.Normalize(z)));
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var rect = rectTransform.rect; Quad(vh, new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMin, rect.yMax), new Vector2(rect.xMax, rect.yMax), new Vector2(rect.xMax, rect.yMin), Sea);
            if(WorldOverview){LargeWorld(vh);return;}
            Coast(vh, 1.5f, Shallows); Coast(vh, -.15f, Sand); Coast(vh, -1.7f, Grass);
            if (biomes != null) BiomeLand(vh);
            foreach (var road in roads)
            {
                Vector2 a = Point(new Vector2(road.x, road.y)), b = Point(new Vector2(road.z, road.w));
                Vector2 side = new Vector2(-(b - a).y, (b - a).x).normalized * rect.width / 48 * .56f;
                Quad(vh, a - side, a + side, b + side, b - side, Sand);
            }
            float half = farmSize * Farming.FarmGrid.CellSize * .5f;
            Box(vh, World(-half, -half), World(half, half), new Color(.42f, .54f, .30f));
            for (int i = 1; i < farmSize; i++)
            {
                float offset = -half + i * Farming.FarmGrid.CellSize;
                Box(vh, World(offset - .03f, -half), World(offset + .03f, half), Grass);
                Box(vh, World(-half, offset - .03f), World(half, offset + .03f), Grass);
            }
            Box(vh, World((float)IslandLayout.PierX - 2, (float)IslandLayout.PierZ - 1.2f), World((float)IslandLayout.PierX + 3.4f, (float)IslandLayout.PierZ + 1.2f), new Color(.63f, .46f, .29f));
        }
        private Vector2 LargePoint(float x,float z)=>Point(MagicIsland.Managers.MapManager.WorldProject(new Vector3(x,0,z)));
        private void LargeWorld(VertexHelper vh)
        {
            const int nx=100,nz=80;
            for(int i=0;i<nx;i++)for(int j=0;j<nz;j++)
            {
                float x=Mathf.Lerp((float)StarterIslandLayout.MinX,(float)StarterIslandLayout.MaxX,i/(float)nx),z=Mathf.Lerp((float)StarterIslandLayout.MinZ,(float)StarterIslandLayout.MaxZ,j/(float)nz);
                float dx=(float)(StarterIslandLayout.MaxX-StarterIslandLayout.MinX)/nx,dz=(float)(StarterIslandLayout.MaxZ-StarterIslandLayout.MinZ)/nz;
                float cx=x+dx*.5f,cz=z+dz*.5f;bool land=IslandLayout.ContainsLand(cx,cz);
                Color tint=Sea;
                if(land)tint=cz<100?Grass:StarterIslandLayout.CoastDistance(cx,cz)<20?Sand:new[]{Grass,new Color(.88f,.72f,.43f),new Color(.88f,.93f,.95f),new Color(.31f,.52f,.36f)}[StarterIslandLayout.Region(cx,cz)];
                else if(StarterIslandLayout.LakeDistance(cx,cz)<0)tint=new Color(.21f,.60f,.76f);
                else if(StarterIslandLayout.CoastDistance(cx,cz)>-30)tint=Shallows;
                Quad(vh,LargePoint(x,z),LargePoint(x,z+dz),LargePoint(x+dx,z+dz),LargePoint(x+dx,z),tint);
            }
            for(int i=1;i<StarterIslandLayout.Route.GetLength(0);i++)
            {
                Vector2 a=LargePoint((float)StarterIslandLayout.Route[i-1,0],(float)StarterIslandLayout.Route[i-1,1]),b=LargePoint((float)StarterIslandLayout.Route[i,0],(float)StarterIslandLayout.Route[i,1]);
                Vector2 side=new Vector2(-(b-a).y,(b-a).x).normalized*.8f;Quad(vh,a-side,a+side,b+side,b-side,Sand);
            }
        }
        private void Coast(VertexHelper vh, float offset, Color tint)
        {
            const int count = 96; int first = vh.currentVertCount; vh.AddVert(Point(Vector2.one * .5f), tint, Vector2.zero);
            for (int i = 0; i < count; i++)
            {
                double angle = i * System.Math.PI * 2 / count, radius = IslandLayout.CoastRadius(angle) + offset;
                vh.AddVert(World((float)(System.Math.Cos(angle) * radius), (float)(System.Math.Sin(angle) * radius)), tint, Vector2.zero);
            }
            for (int i = 0; i < count; i++) vh.AddTriangle(first, first + 1 + i, first + 1 + (i + 1) % count);
        }
        private void BiomeLand(VertexHelper vh)
        {
            const int count = 96, rings = 24; int first = vh.currentVertCount;
            vh.AddVert(World(0, 0), Grass, Vector2.zero);
            for (int i = 0; i < count; i++)
            {
                double a = i * System.Math.PI * 2 / count, edge = IslandLayout.CoastRadius(a) - 1.7;
                for (int j = 1; j <= rings; j++)
                {
                    float x = (float)(System.Math.Cos(a) * edge * j / rings), z = (float)(System.Math.Sin(a) * edge * j / rings);
                    vh.AddVert(World(x, z), biomes.Blend(Grass, x, z), Vector2.zero);
                }
            }
            for (int i = 0; i < count; i++)
            {
                int a = first + 1 + i * rings, b = first + 1 + (i + 1) % count * rings;
                vh.AddTriangle(first, a, b);
                for (int j = 0; j < rings - 1; j++) { vh.AddTriangle(a+j, a+j+1, b+j); vh.AddTriangle(a+j+1, b+j+1, b+j); }
            }
        }
        private static void Box(VertexHelper vh, Vector2 a, Vector2 b, Color tint) => Quad(vh, a, new Vector2(a.x, b.y), b, new Vector2(b.x, a.y), tint);
        private static void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color tint)
        {
            int first = vh.currentVertCount; vh.AddVert(a, tint, Vector2.zero); vh.AddVert(b, tint, Vector2.zero); vh.AddVert(c, tint, Vector2.zero); vh.AddVert(d, tint, Vector2.zero);
            vh.AddTriangle(first, first + 1, first + 2); vh.AddTriangle(first, first + 2, first + 3);
        }
    }
}
