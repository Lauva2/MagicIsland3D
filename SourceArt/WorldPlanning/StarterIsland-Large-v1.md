# 大型初始岛屿地图草案 v1

日期：2026-10-17。通过内置 imagegen 生成的概念规划图，用于讨论整体空间关系，不是实装截图或精确测绘。

- 暂定规划尺度约 1.5 × 1.2 公里，图中面积与路线仅作概念参考，最终以移动速度、探索节奏和性能验证确定。
- 南岸避风海湾：沙滩出生点、附近草地生活区、玩家小屋、农田及码头。
- 中部：大面积草原及连通步道，保留扩展农场和建设的空间。
- 西部：沙漠、沙丘和砂岩地貌，经干草与丘陵向草原过渡。
- 北部：雪山、针叶林与山麓过渡区，以可通行山谷连接其他群系。
- 东部：林地、河流和瀑布，河流连接山麓与南岸。
- 海岸：蓝色外海、暖色浅滩与彩色珊瑚。

本次仅新增设计图及提示词，没有改动 Unity 场景、岸线、碰撞、配置或存档。实施前需要规划新世界坐标和尺度，并协调原农田、设施、NPC 路线、建设和旧档位置；不能直接按图放大陆地网格代替完整迁移。

## 生成方式与完整提示词

工具：内置 image_gen.imagegen；新图生成，无参考图，非透明背景。

```text
Use case: stylized-concept.
Asset type: preliminary large starter-island world map for MagicIsland3D, a cozy Chinese 3D farming/life simulation. A NEW illustrated planning map, not a screenshot or a UI mockup. Landscape 3:2 composition at high resolution.
Primary request: Draw ONE large, coherent, explorable starter island made of grassland, desert, and permanent snowy mountain biomes. Player initially spawns on a beach. Convey dramatically more space than a tiny farm island: a small settlement occupies only about 5 percent of the land, the rest is extensive varied natural terrain.
Style: polished hand-painted game concept map, nearly top-down orthographic with a slight illustrated terrain relief; soft rounded forms, tasteful watercolor/gouache shading, clear readable cartography, cream white, meadow green, golden warm sand, pale icy blue; rich blue ocean with warmer golden shallows and small visible colorful coral patches. Cozy and grounded, inviting exploration; restrained details, no combat emphasis.
Layout: ONE irregular broad landmass fully visible with generous ocean margin. North is at the top. A large central and eastern rolling grassland about 45 percent of land area; broad west and northwest desert about 20 percent, with dunes, sandstone formations and sparse cactus; north and northeast elevated snow mountains and snowy conifers about 20 percent; remaining land is foothills, transitional scrub, forest groves and beaches. Blend biomes through real-looking foothills, dry grass and patchy snow; no arbitrary straight borders, no biomes as separate little islands. Large readable distinct regions, not dense clutter. The northern snow region must feel like a substantial mountain wilderness, not a tiny white patch at the beach. A freshwater stream from foothills through eastern grassy woodland to the southern sea; one modest inland pond. All terrestrial biomes connected by broad walkable valleys and passes. No roads climbing sheer cliffs.
South coast: a sheltered sandy cove with a very clear warm orange pin marking beach player spawn labeled exactly '沙滩出生点'. Nearby inland on grass, a tiny cozy settlement with a player cottage, modest tilled farm plots, shop and a few facilities, labeled '初始生活区'; one small wooden fishing pier on that cove labeled '码头'. The starter area should be sheltered and legible, much smaller than the wilderness. Two subtle walking paths from the beach lead inland: one through central meadow to a western desert pass, the other following the stream toward eastern foothills and northern snow pass. A larger connected meandering main trail loop links settlement, grassland, desert and snow foothills, with a few short exploration spurs; use a thin warm brown lightly dashed route, no thick diagram arrows. Paths must connect and remain geographically plausible.
Cartographic text: readable accurate simplified Chinese, limited elegant labels. Title at top left on a light clean unobtrusive cartouche: '初始岛屿 · 大型地图草案'. Under title in smaller type: '规划尺度约 1.5 × 1.2 公里'. Region labels placed in their regions with restrained cream backing: '中央草原', '西部沙漠', '北境雪山', '东部林地'. Small label near transitional mountain valley '山麓过渡带'. Keep '沙滩出生点' unmistakable. Upper right simple small compass labeled '北'. Bottom left compact 3-item legend in Chinese: orange pin '出生点', cream square '生活设施', thin dashed brown line '主要步道'. Bottom right fine caption: '概念布局，非最终实装'.
Composition: spacious believable big island, natural varied shoreline with coves and headlands, southern beach visible in foreground, all three primary biomes clearly separated yet smoothly connected. Buildings and trees correctly tiny relative to kilometer island. Avoid making the starter village as big as the whole island. No enemies, castles, lava biome, giant mushrooms, sci-fi objects, grid overlay, technical code, interface panels, screenshots, photo textures, logos, watermarks, or fake illegible text.
```

