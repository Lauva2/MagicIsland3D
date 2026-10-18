# 大型初始岛屿地图草案 v3：大湖与魔法景观

此文件保留概念图制作时的记录。随后完成的 Unity 首轮地形、通路与地标实装见 `Docs/StarterIslandWorld.md`，琥珀 Blender 源模型见 `Models/README.md`。

日期：2026-10-17。使用内置 image_gen.imagegen 依次编辑 v1 生成两张概念图，保留此前原图。

- [v2 大湖版本](StarterIsland-Large-v2-Lake.png)：草原与东部林地之间加入大型内陆湖，接通北部山麓来水与东南侧入海河流，调整环湖步道。
- [v3 大湖与魔法版本](StarterIsland-Large-v3-Magic.png)：在大湖版基础上增加湖畔灵辉古树、西部大型琥珀晶石遗迹、东部月光花林，以及雪地霜晶石阵；局部花草作为景观引导。
- 规划尺度仍暂定约 1.5 × 1.2 公里，南岸保留沙滩出生点、初始生活区、农场和码头。
- 魔法景观点仅代表美术与空间方向，未定义传送、技能、解谜或奖励规则。
- 本次只绘制概念图，没有修改 Unity 场景、地形、岸线、碰撞、AA 配置或存档。最终地形与路线需要在实现时重新校验，概念比例不代替测量。

## v2 编辑提示词

参考图：StarterIsland-Large-v1.png。

```text
Use case: precise-object-edit / stylized-concept.
Edit the supplied existing MagicIsland3D large starter island concept map. Keep its painterly cartographic style, canvas/aspect ratio, approximate 1.5 × 1.2 km island scale, overall land outline, western desert, northern snow mountains, eastern woodland, southern starter village/farm, beach spawn, fishing pier, blue sea, coral shallows, title, compass, existing accurate Chinese labels and legend.
Requested targeted change: add ONE LARGE INLAND FRESHWATER LAKE, replacing the existing tiny central pond and some surrounding meadow. This is a lake, not an amber gemstone. The lake should become a major unmistakable landmark occupying roughly 12–15 percent of total visible island land area, broad and naturally irregular, approximately 400–500 m across in conceptual scale. Place it in the central-eastern basin between the central grassland and the eastern woodland, south of the snow-mountain foothills and well north of the starter village. Preserve plenty of open central-western grassland; keep the starting farm and beach intact.
Draw a substantial calm blue/turquoise lake with a visually distinct deep blue center, warm sunlit grassy banks, shallow edges, a few reeds, one or two modest rocky headlands. It must remain completely inland, not become a sea inlet, not cover the village, not flood the desert. Preserve the single coherent island. Show a clear natural river entering from the northern mountain foothills into the lake and one single outlet flowing east/southeast via the existing forest waterfalls/river to the sea. Hydrologically legible connectivity, the lake shore is closed except the narrow inlet/outlet. Move the existing nearby grassland label slightly west if needed.
Add a tasteful cream-backed Chinese label on the lake reading exactly '中央大湖'. Reroute existing dashed brown walking trails to pass along parts of the lake shore with a connected scenic loop; paths stay on land, use at most one small wooden bridge over the narrow outflow. Avoid roads across open lake water, extra settlements, new game mechanics, giants, amber crystals, thick arrows, technical annotations or additional panels. The lake must visibly be large at full-map scale, not a tiny decorative pond.
Keep other existing labels verbatim and legible: '初始岛屿 · 大型地图草案', '规划尺度约 1.5 × 1.2 公里', '北境雪山', '西部沙漠', '中央草原', '山麓过渡带', '东部林地', '初始生活区', '沙滩出生点', '码头', '北', '出生点', '生活设施', '主要步道', '概念布局，非最终实装'.
```

## v3 编辑提示词

参考图：StarterIsland-Large-v2-Lake.png。

```text
Use case: precise-object-edit / stylized-concept.
Edit THIS supplied large island concept map which already contains a large inland lake. Preserve the entire island's coastline, scale, major biome proportions, northern snowy mountains, western desert, central grassland, eastern woodland, the LARGE central inland lake, its northern river inlet and southeastern outlet/waterfalls, the connected trail loop, southern starter village/farm, beach spawn, pier, warm shallow sea and blue outer ocean. Keep all existing Chinese labels, title, scale, compass and legend readable. Same painterly illustrated top-down game-planning aesthetic, same aspect ratio.
New request: integrate restrained but clearly visible MAGIC ELEMENTS into this cozy farming-world map. This is gentle nature magic and ancient discovery, not a combat or dark fantasy map. Add FOUR carefully placed magical landmarks that feel physically rooted in their region:
1) Central large lake: near the northwestern shore, one small rocky islet reachable by a few stepping stones at the shore, with one luminous ancient willow-like tree, silver mint foliage, soft blue-violet lantern-like flowers and very subtle glowing water around its roots. Keep the majority of the lake completely open, its main '中央大湖' label unobscured and its shore/stream connections unchanged. A small cream-backed label next to the tree reads exactly '灵辉古树'. The tree/islet must remain a small feature, not turn the lake into land.
2) Western desert: one prominent LARGE HONEY-AMBER CRYSTAL MONOLITH rooted in a low sandstone archaeological plinth, with a few warm golden crystalline shards and faded carved runes, softly glowing in daylight. This supplies a large amber landmark as well as magic. Visible at full-map scale but geographically much smaller than a biome. Place in a free desert clearing away from the main '西部沙漠' label. Label exactly '琥珀遗迹'. Preserve desert dunes and existing connecting paths.
3) Eastern woodland: a modest grove of moonlit lavender-blue flowers and a few pale glowing mushrooms surrounding a small old stone arch, partially sheltered by trees, beside an existing side trail. Soft silver/lilac accents distinguish it from surrounding deep green woodland, even in the daytime map. Label exactly '月光花林'. Keep the river, waterfalls and main forest label visible.
4) Northern snow foothill pass: a small ancient rune-stone circle with a few icy cyan crystals, embedded in snow on a walkable shelf. Gentle cold glow, tiny star-like flecks, grounded geology. Label exactly '霜晶石阵'. Keep the snowy peaks dominant, and do not block the mountain pass.
Add a few tiny scattered magic-flower patches near the lake and the path leading from the starter grassland toward the lake, as a visual breadcrumb trail, with very low density. Connect each landmark with short believable walking-path branches from the existing main loop, no thick arrows, no roads across open water.
Preserve the original cozy daylight lighting. Magical elements should be legible subtle accents, not neon lasers, oversized fantasy castles, floating continents, new armies, enemies or big HUD icons. Do not flood the island with crystals. Keep the settlement modest and the natural landscapes spacious. Existing generated Chinese labels must stay verbatim. Four additional Chinese landmark labels are the only new text; maintain consistent cream cartographic label design without overlaps. This remains a clearly preliminary map drawing, not an in-engine screenshot.
```

