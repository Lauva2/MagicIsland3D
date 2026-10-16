# 海岸场景美术

码头周围的示范海岸使用圆润岸线、草地到暖色沙滩的渐变、椰树与灌木、贝壳、浅海礁石 / 珊瑚 / 水草，以及木板、木桩、弧形绳栏和鱼形指示牌。资源是可编辑的 3D 网格、材质和预制体。

## 资源与管理

`TerrainManager` 通过 AA `IslandTerrain` 加载地形和 CoastalScenery 子层级；`OceanManager` 通过 AA `IslandOcean` 加载海床和 Shallow reef gardens；`FishingManager` 继续通过 AA `CoastalFishingSpot` 加载码头及其细节。GameManager 保持原初始化和退出清理顺序，不在运行时生成美术网格。

可复用预制体位于 `Assets/Art/Prefabs/Environment/Scenery`，AA 地址分别为 `CoastalPalm`、`CoastalShrub`、`BeachShell`、`ReefRock`、`ReefCoral`、`ReefSeagrass`。场景通过嵌套预制体携带这些依赖。网格位于 `Assets/Art/Meshes/Coast`，材质位于 `Assets/Art/Materials/Coast`，可直接修改或替换。

枝状珊瑚、海草和装饰贝壳已作为海底群落美术的共用模板更新为 Blender 网格，保留原 GUID、嵌套引用、位置、材质和碰撞。更新仅通过显式 Rebuild Blender Reef Models，普通配置不覆盖；原位布局、安全包络与编辑约定见 [海底群落源美术](../SourceArt/OceanReef/README.md)。装饰贝壳不加入海滩拾取节点或存档。

## 轮廓与兼容

IslandLayout 采用圆角轮廓与不规则海岸的平滑并集。原方形岛内和此前可行走海岸的坐标继续有效；地形、海床和边界碰撞同时采用该轮廓，避免只修改可见网格。草地与沙滩颜色沿轮廓渐变，保留平坦的农田和原设施位置。

椰树的树干有实体碰撞，视觉与碰撞使用 Ignore Raycast 层，避免拦截鼠标目标；树冠接入 CameraOccluder，遮挡角色时淡化，离开后恢复原 AA 材质。灌木、贝壳与水下装饰不阻挡行走或点击；水下网格位于海面以下，通过透明浅水呈现。

码头入口、垂钓点、NPC 海岸停留点保留通道。恢复存档时，TerrainManager.RespectSavedPlacement 在摆放实体初始化前隐藏与已保存摆放格重叠的新椰树，优先保留玩家建设，不删除或移动存档记录。新增摆放仍由现有实际碰撞检查拒绝树干占用格。

不新增存档字段，进度版本沿用当前版本 29；白天操作不写进度，睡觉结算后才保存。

## 编辑与迁移

CoastArtSetup 接入 Configure Prototype Addressables。首次迁移更新地形 / 海床 / 边界网格时保留 `.meta` 和引用，生成缺失的可复用美术，并在 IslandTerrain、OceanSurface、FishingSpot 记录 CoastArtVersion。当前美术迁移标记为 3，区别于存档版本；后续常规执行保留已有网格、材质、场景布局与码头细节。

修改岛屿轮廓时，仍需同步 IslandLayout 与地形、海床、边界；仅调整树木或道具时可编辑相应嵌套预制体和布局，保留通道与碰撞层。椰树不是采集树，不参与砍树资源结算。

### 椰树分布

椰树通过 `IslandTerrain.PalmScatter` 的独立固定种子在编辑器中生成：随机选择相隔至少 7.5 米的簇中心，每簇最多 2–4 株，混合小簇与零散单株，沿海岸连续抽取前后位置、朝向和大小，保留疏密变化及空地，不使用等角度环绕或循环尺寸。默认种子 20261017、18–23 棵、离岸内缩 1.9–4.8 米、树间至少 1.9 米、缩放 0.80–1.22；实际以预制体 Inspector 参数为准。树干包络含行走余量，避开村落、最大农田范围、码头入口与 NPC 海岸通道；无法容纳指定数量或参数非法时拒绝保存。

调整参数后显式执行 `Magic Island > Rebuild Coastal Palms`。此操作仅重排椰树根节点的落点、朝向和大小，数量变化时增减嵌套椰树；已有实例尽量复用，保留组件覆盖和引用。灌木、贝壳、地形 / 海床网格、道路、四季地表、鸟蟹与鱼群不重建。普通 Configure / Setup 不重排已有椰树或覆盖手调参数；不提高通用 CoastArtVersion 来触发整片海岸迁移。

布局作为 `IslandTerrain` 的嵌套 AA 依赖保存，游戏加载和读档不重新抽取，不占用玩法随机序列。旧玩家摆放仍由 `RespectSavedPlacement` 优先保留。合并更新后的地形预制体后，使用 `Build Prototype Addressables` 重新构建内容，Packed Play Mode / Player 才会使用新布局。

## 验证与预览

TerrainCoastFlowVerification 在打包 AA 下实测美术依赖、网格顶点和包围盒、椰树实体碰撞 / 鼠标过滤 / 镜头淡化与材质恢复、旧摆放物优先、沙滩和码头移动、NPC 路线、钓鱼及睡觉存档约束，同时检查重复配置不会覆盖美术。

Tools/VerifyIslandTerrain.cs 覆盖原岛内位置和此前海岸的 720 个方向；住宅、制作 / 摆放与 NPC 日程继续回归。预览由 Unity 实际镜头渲染，位于 Logs/CoastArtOverview.png、CoastPalmFadePreview.png、CoastPierPreview.png、CoastFishingPreview.png。

`Tools/VerifyCoastalPalms.cs` 检查 256 组种子的确定性、数量 / 尺寸范围、树干间距、落点及通道，默认布局另检查径向错落和疏密差异。纯 C# 编译时加入 `Assets/Scripts/Terrain/CoastalPalmScatter.cs` 与 `Assets/Scripts/Interaction/IslandLayout.cs`。

`CoastalPalmVerification.RebuildAndRun` 仅在 `Logs/` 下的隔离工程执行，验证重建幂等、非椰树布置 / GUID 保留和普通 Setup 保留手调，再运行 `TerrainCoastFlowVerification.Run` 的 Packed AA 集成检查。验证工程使用独立公司 / 产品名隔离持久目录。新增实际镜头预览为 `Logs/CoastalPalmsOverview.png` 和 `Logs/CoastalPalmsDetail.png`；`VerifyAssets` 可对已烘焙资源做只读检查。
