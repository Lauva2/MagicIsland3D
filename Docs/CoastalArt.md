# 海岸场景美术

码头周围的示范海岸使用圆润岸线、草地到暖色沙滩的渐变、椰树与灌木、贝壳、浅海礁石 / 珊瑚 / 水草，以及木板、木桩、弧形绳栏和鱼形指示牌。资源是可编辑的 3D 网格、材质和预制体。

## 资源与管理

`TerrainManager` 通过 AA `IslandTerrain` 加载地形和 CoastalScenery 子层级；`OceanManager` 通过 AA `IslandOcean` 加载海床和 Shallow reef gardens；`FishingManager` 继续通过 AA `CoastalFishingSpot` 加载码头及其细节。GameManager 保持原初始化和退出清理顺序，不在运行时生成美术网格。

可复用预制体位于 `Assets/Art/Prefabs/Environment/Scenery`，AA 地址分别为 `CoastalPalm`、`CoastalShrub`、`BeachShell`、`ReefRock`、`ReefCoral`、`ReefSeagrass`。场景通过嵌套预制体携带这些依赖。网格位于 `Assets/Art/Meshes/Coast`，材质位于 `Assets/Art/Materials/Coast`，可直接修改或替换。

## 轮廓与兼容

IslandLayout 采用圆角轮廓与不规则海岸的平滑并集。原方形岛内和此前可行走海岸的坐标继续有效；地形、海床和边界碰撞同时采用该轮廓，避免只修改可见网格。草地与沙滩颜色沿轮廓渐变，保留平坦的农田和原设施位置。

椰树的树干有实体碰撞，视觉与碰撞使用 Ignore Raycast 层，避免拦截鼠标目标；树冠接入 CameraOccluder，遮挡角色时淡化，离开后恢复原 AA 材质。灌木、贝壳与水下装饰不阻挡行走或点击；水下网格位于海面以下，通过透明浅水呈现。

码头入口、垂钓点、NPC 海岸停留点保留通道。恢复存档时，TerrainManager.RespectSavedPlacement 在摆放实体初始化前隐藏与已保存摆放格重叠的新椰树，优先保留玩家建设，不删除或移动存档记录。新增摆放仍由现有实际碰撞检查拒绝树干占用格。

不新增存档字段，版本仍为 27；白天操作不写进度，睡觉结算后才保存。

## 编辑与迁移

CoastArtSetup 接入 Configure Prototype Addressables。首次迁移更新地形 / 海床 / 边界网格时保留 `.meta` 和引用，生成缺失的可复用美术，并在 IslandTerrain、OceanSurface、FishingSpot 记录 CoastArtVersion。当前美术迁移标记为 3，区别于存档版本；后续常规执行保留已有网格、材质、场景布局与码头细节。

修改岛屿轮廓时，仍需同步 IslandLayout 与地形、海床、边界；仅调整树木或道具时可编辑相应嵌套预制体和布局，保留通道与碰撞层。椰树不是采集树，不参与砍树资源结算。

## 验证与预览

TerrainCoastFlowVerification 在打包 AA 下实测美术依赖、网格顶点和包围盒、椰树实体碰撞 / 鼠标过滤 / 镜头淡化与材质恢复、旧摆放物优先、沙滩和码头移动、NPC 路线、钓鱼及睡觉存档约束，同时检查重复配置不会覆盖美术。

Tools/VerifyIslandTerrain.cs 覆盖原岛内位置和此前海岸的 720 个方向；住宅、制作 / 摆放与 NPC 日程继续回归。预览由 Unity 实际镜头渲染，位于 Logs/CoastArtOverview.png、CoastPalmFadePreview.png、CoastPierPreview.png、CoastFishingPreview.png。
