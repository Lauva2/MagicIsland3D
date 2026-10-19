# 草原树冠风动

后续草原拨草的集成回归统一了 GPU 静止帧判定：最多容许 4 个像素、各通道 1/255 的舍入差；风动检查要求超过 64 个像素发生变化，避免单像素舍入被当作运动。实际风时钟与零风力条件仍须保持。

显式入口：`Magic Island > Art > Prepare Grassland Crown Wind (Explicit)`，实现为 `GrasslandCrownWindSetup`。要求先完成原树冠细化，普通 Setup 不触发。

仅在原 8 份 Landscape 网格中识别上轮新增的 126 簇叶团。将这些顶点原先空白的四维 UV0 写为高度权重、树木相位、叶团相位和树高倒数；不增加 UV 频道，原旧顶点、树干、岩石、其他群系与 UV1–7 均保留。树木相位按原树干锚点统一，各叶团局部节奏错开；不消耗随机序列，不改顶点位置、颜色、拓扑、实体碰撞、建设规则或存档。

原共享 `Island Biome Details` 着色器只对这个显式标记执行形变，未标记顶点保持静止。`GrasslandCrownWind.cginc` 读取 WeatherManager 的 `_IslandWindTime`、`_IslandWindStrength` 和 `_IslandWindDirection`，使用低频树冠摆动与幅度较小的叶团侧向偏移。高度权重平方限制低处位移，法线按同一剪切的逆转置变换；阴影使用同一顶点函数。没有新的 Update、Manager、逐树对象、材质副本或运行时网格修改。

共享材质可调 `Grassland crown sway (m)`（默认 0.10、范围 0–0.16）和 `Grassland leaf drift (m)`（默认 0.018、范围 0–0.035），只影响已标记叶团。实际风力仍来自 AA WeatherConfig，晴雨变化继续沿原 WindSimulation 平滑推进。着色器限制风力不超过 1.6，最大水平位移 0.312 米；原网格 bounds 每侧扩展 0.325 米避免动态裁剪，地形与碰撞不扩张。普通 Setup 不重写人工材质参数。

暂停与室内不推进既有风时钟，室内沿原 HousingManager 镜头遮罩隐藏室外树木；退出由 WeatherManager 恢复原风全局参数。既有 CameraOccluder 绑定不变，不把合并景观区域新增为淡化目标。

导入器追加 `MagicIsland.GrasslandCrownWind.v1`。完整标记后重复执行保留后续手调；部分完成、未知元数据、原叶团 UV0 已编辑、未知材质、变换或碰撞网格复用均在写入前拒绝。所有网格计划通过后才写入，失败恢复原网格和 meta。原树冠细化入口识别已完成标记后同样保留新数据。

隔离验证入口：

- `GrasslandCrownWindVerification.BakeAndVerify`：验证仅已知叶团 UV0／bounds 改动，全部几何、其他频道、GUID、整份预制体、碰撞、材质及其他美术资源保留；检查最大风动包络、错峰相位、未知元数据／UV 拒绝、重复和手调保护。
- `GrasslandCrownWindVerification.After`：构建实际 Packed AA，在默认游玩镜头拍摄动态序列；隔离原树冠 Renderer，验证 GPU 位移、零风静止与暂停画面一致；实测进屋时风时钟冻结、室外镜头遮罩和返回菜单后原风参数恢复，并沿原五岛流程检查通行、生态、建设遮盖、睡觉读档和清理。

测试和预览保存在忽略的 `Logs/GrasslandCrownWind`。主项目运行前执行 **Build Prototype Addressables** 更新网格与着色器依赖。

2026-10-19 验收：8 份原网格、126 簇叶团、32,760 个标记顶点；完整预制体 2,878 条记录逐字节不变，原材质及其引用未修改。烘焙保护检查与 Packed AA 流程均以退出码 0 完成；隔离 GPU 位移／零风／暂停、室内冻结和镜头遮罩、退出恢复检查通过。已检查默认镜头晴雨画面，原 6,936 步实际通行、生态、建设遮盖、床边睡觉、五岛读档和退出清理均通过。未使用正式进度，无存档迁移。
