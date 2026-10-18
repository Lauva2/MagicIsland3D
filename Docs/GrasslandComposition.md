# 草原主路景观组合精修

2026-10-18。群系第二轮按沙滩→草原→沙漠→林地→雪地继续；本轮处理生活区北侧栈道出口，经中央草原岔路到西侧湖泊入口。首轮群系美术见 [BiomeArtSequence.md](BiomeArtSequence.md)。

草原腹地、南侧支路、湖岸入口和原主路花草疏密的后续收尾见 [GrasslandFinish.md](GrasslandFinish.md)。本文件记录的是此前主路组合这一轮的范围与验证。

显式入口为 **Magic Island > Art > Refine Grassland Route Composition (Explicit)**，实现 `GrasslandCompositionSetup`。普通 Setup 不调用，也不重建整岛、路径、植被布局或海洋生态。

## 模型与布置

Blender MCP 制作圆润苔石、折叶矮草和奶油黄／淡紫野花三个模板，源文件与顶点色导出保存在 `SourceArt/GrasslandComposition`，可复现脚本为 `Tools/Art/BuildGrasslandComposition.py`。游戏只加载烘焙后的原生 Mesh，不读取 JSON 或 Blender 文件。

沿 `(0,185)→(0,240)→(-150,370)→(-100,520)→(-100,635)` 分段布置，左右错开、间距变化，部分点位增加短距离伴生小簇。苔石隔簇出现，花草使用不同高度和朝向，保留草原的开阔留白。原路标、道路和已有路肩花草保留。

落点只取原草原群系的既有不可建设路肩；1.05 米半径保守方形脚印密集采样，并逐一检查所有相交建设格，保持道路中心 3 米以上净空。每个新顶点复查道路距离和建设资格，读取原烘焙地面碰撞贴地，不改变地形高度。所有组合按 128 米分区合并在 `IslandTerrain / ... / Baked landscape / Grassland route composition`，复用原 Biome Details 共享材质，不增加碰撞、拾取物、光源、逐件脚本或 Update。

## 原景观与手调保护

仅选取距这条路线 29 米内、Z215–600 的原绿色树冠球面，调整宽高和上层暖叶色；树干、地面、原对象坐标与碰撞保持。水平轮廓只收窄，不扩大到原脚印以外；形变不旋转相反顶点，不造成树冠压扁。完整组件触及原沙漠／雪地入口或靠湖林缘范围时跳过，保留这些局部精修。

原网格 UV6（Unity `GetUVs(6)`，第七组 UV）记录 `x=0/1` 未选中／已选中与 `y=-742` 标记，UV0–5、索引、GUID 和未选中顶点／法线／颜色保留。其他自定义 UV6 在写入前拒绝覆盖。此频道只供编辑器使用，Packed 构建可正常剥离。

已有完整景观子树时整个操作跳过，保留后续手工改形、调色与布置；没有子树却存在孤立同名网格时拒绝覆盖。整岛显式重建会重新生成原大陆网格，之后需要重新规划本轮的树冠调整；不要删除标记来自动覆盖人工结果。

## 验证与交接

`GrasslandCompositionVerification.BakeAndVerify` 只在隔离工程执行，检查原索引、UV、alpha、未选中树干和景观、完整碰撞集合、其他美术资源字节，以及新模型的法线、面、完整脚印和共享材质。覆盖自定义频道拒绝、重复执行、普通 Setup 和后续手工改形／调色保护。

`OceanWaterAppearanceVerification.RunGrasslandCompositionBaseline/RunGrasslandComposition` 使用真实 Packed AA、同机位及固定动画时间，记录清晨／白天／夜间／雨天的草原路线与原海洋／湖泊对照，实际用 CharacterController 实走栈道出口至湖泊入口。另运行 `StarterIslandWorldFlowVerification.Run` 覆盖大陆生命周期。输出在忽略的 `Logs/GrasslandComposition`。

没有新增配置、玩法、持久化字段或存档迁移，保留当前 v31。正式工程通过 **Magic Island > Build Prototype Addressables** 重建内容后使用本轮资源。

本轮资源验证通过：30 处景观组合、7 个共享材质分区网格，共 27,650 三角面；4 份原 Landscape 网格的 910 个叶片顶点调整形状／配色。原顶点索引、UV0–5、树干、完整碰撞集合及其他美术资源字节保持，外部 UV6 拒绝和重复／Setup／手调保护均通过。正式工程的资源构建步骤与本机忽略的验证副本分开。

最终 `Bake.log`、`After.log` 和 `WorldFlow.log` 通过。36 张修改前／36 张最终 Packed 画面完成同机位对照，检查清晨、白天、夜间和雨天代表视角；修正新野花花瓣绕序，并将花茎布置到苔石旁边。实际草原路线通行、全岛道路／湖河／桥梁、地图中文／暂停、室内隐藏、床边睡觉、生活区及雪地继续、返回／重进和 AA 释放通过。深海四种光照与湖泊三种光照逐像素一致；湖泊白天仅一个颜色通道差 1/255，保持原水域资源字节一致。

合入核对保留原预制体 2,227 条记录，只追加景观子树和分区网格组件共 30 条记录；其余 3,264 份原文件保持本轮快照，1,621 个资源 GUID 无重复。正式工程资源与最终验证副本字节一致，原海洋、地形碰撞、材质和其他群系美术保留。没有重跑完整 OceanFlow、全部存档迁移、独立 Player 构建或 GPU 性能测量。
