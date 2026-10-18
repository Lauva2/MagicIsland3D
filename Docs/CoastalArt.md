# 海岸场景美术

码头周围的示范海岸使用圆润岸线、草地到暖色沙滩的渐变、椰树与灌木、贝壳、浅海礁石 / 珊瑚 / 水草，以及木板、木桩、弧形绳栏和鱼形指示牌。资源是可编辑的 3D 网格、材质和预制体。

## 资源与管理

### 出生沙滩至码头景观组合（2026-10-18）

第二轮局部精修使用 Blender MCP 制作圆润三石组、磨圆的分叉漂流木和低矮草簇；源文件与顶点色／法线导出位于 `SourceArt/BeachComposition`，可复现脚本为 `Tools/Art/BuildBeachComposition.py`。只写独立源场景和文件，保留 Blender 中原有场景。源模型和 JSON 不加入运行时加载。

显式执行 **Magic Island > Art > Refine Beach Arrival Composition (Explicit)**，在原 `IslandTerrain` 根下增加 `Beach arrival composition`。四处岸边小簇位于约 (-1.28,-17.85)、(7.15,-16.54)、(11.89,-15.48)、(15.99,-10.49)，保留宽阔沙面；岩组与漂流木交替，草簇偏置而不等距铺满岸线。复用 Biome Details 共享材质，合并为 `Assets/Art/Meshes/Coast/Beach Arrival Composition.asset` 一份网格／一个 Renderer，随原地形 AA 加载。没有新增材质、灯光、碰撞、拾取、逐件脚本、Update 或存档字段。

整簇水平半径不超过 0.48 米，完整包围范围密集检查原不可建设格，保留出生点、码头入口与旧通行。通过原 Ground 与原 Ocean Shore 可见沙面的实际高度贴地；外侧湿沙超出可行走碰撞范围时，仅在编辑器创建临时查询碰撞并立即清理，不写入资源。不改地面几何、色板、原椰树／灌木／贝壳布局或任何玩法碰撞。读取 AA GatheringCatalog 的 BeachSites 及三类拾取物实际碰撞包络，覆盖所有种子的站点抖动区间并留余量；新增装饰漂流木没有拾取权限。以后调整站点数、拾取模型包络或岸线时，须同步重验这些落点。

普通 Configure／Setup 不调用本工具。已有子树重复执行时保留后续人工改形、调色、变换与资源 GUID；无对应子树的孤立同名网格在写入前拒绝覆盖。不会重建整个岛屿、椰树布局或海洋生态。新增源美术默认拒绝覆盖已有导出，只有显式 `main(replace=True)` 才重新制作已标记的专用源场景。

`BeachCompositionVerification.BakeAndVerify` 在隔离工程检查模型法线、非退化三角面、完整建设／拾取包络、原资源和碰撞、重复／普通 Setup 与后续手调保护。`OceanWaterAppearanceVerification.RunCompositionBaseline/RunComposition` 生成相同镜头的清晨／白天／夜间／雨天对照，并核对真实 Packed AA 模型、当局拾取物间距和 CharacterController 出生点至码头实走。验证与截图保存在忽略的 `Logs/BeachComposition`；正式工程需执行 **Magic Island > Build Prototype Addressables** 使用新打包资源。进度仍为 v30，只在床边确认睡觉后保存。

本次资源检查、32 张前／32 张最终 Packed 画面对照及出生点至码头实走通过（`BakeSurface.log`／`AfterSurface.log`）。清晨、白天、夜间、雨天的深海与湖面八张对照逐像素一致。`CoastFlow.log` 通过原海岸实际移动、码头／钓鱼、椰树碰撞与淡化、旧建设优先、室内切换、返回重进及 AA 失败／取消／释放回归。

合入的四处小簇共 1,568 个三角形、4,704 个顶点，只有一个新增渲染器。`Review.json` 核对保留原预制体 2,223 条记录，只追加四条记录；原有美术／脚本／编辑器文件中 2,861 份保持本轮快照，1,612 个 GUID 无重复，正式资源与隔离验证资源一致。未重跑全大陆路线、全版本存档迁移、完整 OceanFlow、独立 Player 构建或 GPU 性能测量。正式 AA 构建仍需在主工程执行。

### 原有海岸资源

2026-10-18 的群系顺序精修仅调整原椰叶／灌木共享模型，保留本文件描述的全部布置、碰撞、生态和实例引用；显式入口、UV5 手调标记及验收见 [群系美术逐区优化](BiomeArtSequence.md)。

`TerrainManager` 通过 AA `IslandTerrain` 加载地形和 CoastalScenery 子层级；`OceanManager` 通过 AA `IslandOcean` 加载海床和 Shallow reef gardens；`FishingManager` 继续通过 AA `CoastalFishingSpot` 加载码头及其细节。GameManager 保持原初始化和退出清理顺序，不在运行时生成美术网格。

可复用预制体位于 `Assets/Art/Prefabs/Environment/Scenery`，AA 地址分别为 `CoastalPalm`、`CoastalShrub`、`BeachShell`、`ReefRock`、`ReefCoral`、`ReefSeagrass`。场景通过嵌套预制体携带这些依赖。网格位于 `Assets/Art/Meshes/Coast`，材质位于 `Assets/Art/Materials/Coast`，可直接修改或替换。

枝状珊瑚、海草和装饰贝壳已作为海底群落美术的共用模板更新为 Blender 网格，保留原 GUID、嵌套引用、位置、材质和碰撞。更新仅通过显式 Rebuild Blender Reef Models，普通配置不覆盖；原位布局、安全包络与编辑约定见 [海底群落源美术](../SourceArt/OceanReef/README.md)。装饰贝壳不加入海滩拾取节点或存档。

## 轮廓与兼容

IslandLayout 采用圆角轮廓与不规则海岸的平滑并集。原方形岛内和此前可行走海岸的坐标继续有效；地形、海床和边界碰撞同时采用该轮廓，避免只修改可见网格。草地与沙滩颜色沿轮廓渐变，保留平坦的农田和原设施位置。

椰树的树干有实体碰撞，视觉与碰撞使用 Ignore Raycast 层，避免拦截鼠标目标；树冠接入 CameraOccluder，遮挡角色时淡化，离开后恢复原 AA 材质。灌木、贝壳与水下装饰不阻挡行走或点击；水下网格位于海面以下，通过透明浅水呈现。

码头入口、垂钓点、NPC 海岸停留点保留通道。恢复存档时，TerrainManager.RespectSavedPlacement 在摆放实体初始化前隐藏与已保存摆放格重叠的新椰树，优先保留玩家建设，不删除或移动存档记录。新增摆放仍由现有实际碰撞检查拒绝树干占用格。

不新增存档字段，进度版本沿用当前版本 30；白天操作不写进度，睡觉结算后才保存。

## 编辑与迁移

CoastArtSetup 接入 Configure Prototype Addressables。首次迁移更新地形 / 海床 / 边界网格时保留 `.meta` 和引用，生成缺失的可复用美术，并在 IslandTerrain、OceanSurface、FishingSpot 记录 CoastArtVersion。当前美术迁移标记为 3，区别于存档版本；后续常规执行保留已有网格、材质、场景布局与码头细节。

修改岛屿轮廓时，仍需同步 IslandLayout 与地形、海床、边界；仅调整树木或道具时可编辑相应嵌套预制体和布局，保留通道与碰撞层。椰树不是采集树，不参与砍树资源结算。

### 椰树分布

椰树通过 `IslandTerrain.PalmScatter` 的独立固定种子在编辑器中生成：随机选择相隔至少 7.5 米的簇中心，每簇最多 2–3 株，混合松散小簇与零散单株，沿海岸连续抽取前后位置、朝向和大小。默认种子 20261017、16–20 棵、离岸内缩 1.9–4.8 米、树干间距至少 1.9 米、缩放 0.80–1.22；实际以预制体 Inspector 参数为准。间距还须达到 `CrownSpacing × 两树缩放之和`，默认 CrownSpacing=1.55，避免大树冠连成一片。树干包络含行走余量，避开村落、最大农田范围、码头入口与 NPC 海岸通道；无法容纳指定数量或参数非法时拒绝保存。

调整参数后显式执行 `Magic Island > Rebuild Coastal Palms`。此操作仅重排椰树根节点的落点、朝向和大小，数量变化时增减嵌套椰树；已有实例尽量复用，保留组件覆盖和引用。灌木、贝壳、地形 / 海床网格、道路、四季地表、鸟蟹与鱼群不重建。普通 Configure / Setup 不重排已有椰树或覆盖手调参数；不提高通用 CoastArtVersion 来触发整片海岸迁移。

布局作为 `IslandTerrain` 的嵌套 AA 依赖保存，游戏加载和读档不重新抽取，不占用玩法随机序列。旧玩家摆放仍由 `RespectSavedPlacement` 优先保留。合并更新后的地形预制体后，使用 `Build Prototype Addressables` 重新构建内容，Packed Play Mode / Player 才会使用新布局。

## 验证与预览

### 干湿沙与岸边留白（2026-10-18）

`Island Shore` 与 `Island Biome Ground` 共用 `CoastalWetSand.cginc`，按实际地面世界高度和原沙色形成干湿渐变。低频噪声轻微打散边界，湿沙适度变深、保留暖色，向浅水海床羽化消失；草地、雪地和高处大陆沙地不染湿。效果只修改片元反照率和粗糙度，不改原顶点色、UV、几何、海水、岸线、碰撞或四季草色。

共享地表材质的可调项：`WetSandStrength` 默认 0.75、`WetSandSmoothness` 默认 0.28，`WetSandBand` 默认 `(-0.16, -0.005, -0.32, -0.235)`，四项分别为全湿高度、全干高度、海底淡出高度和海底起点（世界米）。效果在现有海平面（-0.22 米）下约 10 厘米内淡出，避免压暗浅水沙底。这些是与现有海岸坡度匹配的静态美术高度带；若主动改变海平面／地形高度，应同时检查高度带。强度 0 可关闭效果。柔光使用现有 Standard 日照／阴影，不加自发光、镜面白边、动画时钟、对象或材质副本；普通 Setup 不写回或重置材质参数。

显式 **Magic Island > Art > Thin Beach Shells And Pebbles (Explicit)** 只将原 `CoastalScenery` 中部分装饰贝壳／小石子根节点设为不激活，按固定连续空间噪声保留小簇与空白。只筛选原海岸内缩 0.7–3.1 米的无碰撞装饰，保留全部原实例、位置、旋转、缩放、组件、GUID 和引用；不移动椰树、灌木，不改变可拾取贝壳、海洋生态或玩家记录。重复操作不继续抽样／减量；普通 Setup 保留原激活状态和人工布局。需要恢复单件时在预制体中重新激活；再次显式筛疏会重新应用该空间规则。`DetailScatter` 数量仍表示保留的实例总数，包含隐藏装饰。

隔离验证入口 `CoastalBeachSetup.BakeAndVerify` 检查重复筛疏／普通 Setup 的字节级保留、原地形／海床／边界网格和海洋预制体不变。`OceanWaterAppearanceVerification.RunBeachBaseline/RunBeach` 用相同镜头、太阳和海洋时间，在真实 Packed AA 中输出清晨／白天／夜晚／雨天的东南西海滩、村落、浅水／礁群／深海／湖水画面；After 中额外通过临时 MPB 关闭湿沙进行画面对照，不改共享材质。验收图位于 `Logs/BeachTransition`。主工程 AA 内容须在合回后执行 **Build Prototype Addressables**。

本轮实际验收：保留全部 94 个原海岸实例，其中 13 件装饰贝壳和 4 组小石子增加不激活覆盖，77 个实例继续激活；对预制体文本核对仅新增这 17 处激活覆盖，全部变换、GUID、fileID、组件、生态与玩法引用保留。重复筛疏／普通 Setup 验证通过，原网格和共享材质文件不变；美术／脚本／编辑器基线中另外 2,777 份已有文件保持原字节。32 张修改前和 36 张最终 Packed 画面验证通过，深海与湖水在四种光照下逐像素一致，村落对照最多差一个色阶；同一帧关闭湿沙的差异集中在岸边，运行与 MPB 对照均保留共享地表材质。实际已检查东南西海滩、清晨、夜间和雨天，预览为 `Logs/BeachTransition/Comparison.png`。

`CoastalBeachSetup.BakeAndRun` 的 Packed `TerrainCoastFlowVerification` 本轮通过，覆盖真实沙滩／码头移动、旧落点和 NPC 路线、椰树碰撞／镜头淡化、旧建设优先、钓鱼、水面匹配、白天不保存、进出小屋、返回／重进与加载失败／取消清理。仍使用存档 v30，无字段或迁移变化；本轮没有另跑实际睡觉写盘／全迁移套件、完整 OceanFlow、独立 Player 构建或 GPU 性能测量。

### 海岸疏密优化（2026-10-17）

`IslandTerrain.DetailScatter` 独立种子 20261018，默认灌木 35、装饰贝壳 28、小石子 11。连续抽取落点、朝向和尺寸，以小簇、零散单件和空隙打破等距环带；灌木避开实际椰树树干，全部小物避开村落、码头和 NPC 通道。显式 **Rebuild Coastal Details** 才重排，尽量复用同类型嵌套实例和组件覆盖，不新增碰撞、拾取物或存档状态。普通 Setup 保留人工参数与布局。

显式 **Rebuild Coastal Ground Colours** 仅更新原地形和海床干地部分的顶点色，以连续噪声和不同尺度的岸向变化打散草沙交界。保留全部顶点位置、三角面、法线、地面高度、岸线、边界和 GUID；不扩大或缩小玩法建设区，不重新烘焙四季地表细节。普通 Setup 不自动重着色。

海底疏密使用 **Art > Rebuild Reef Density (Explicit)**；完整成员记录、稀疏可见标记及鱼群障碍兼容见 [源美术说明](../SourceArt/OceanReef/README.md)。不执行整片地貌 / 生态重建。

旧全景下缘的横向断层来自 Size=25 的正交预览仍使用正常游戏 18 米镜头偏移，近裁剪面切到海面。`LightingFlowVerification.Capture` 根据视野高度和俯角后移预览镜头，恢复时还原；不改变实际游戏镜头或海面网格。`CoastalLandscapeVerification` 同时检查正常 Size=5/7/9 的近裁剪面并输出东南西北海岸和全景。

隔离验证入口 `CoastalLandscapeVerification.RebuildAndRun` 按小物、椰树、海底疏密、草沙颜色顺序烘焙，复查重建幂等、原地形 / 海床几何、鱼鸟蟹布局、普通 Setup / 手调保留，再运行真实 Packed AA 海岸流程。只读入口 `VerifyAssets`；纯规则入口 `Tools/VerifyCoastalPalms.cs` 同时覆盖小物分布。

本轮验收：默认烘焙 20 棵椰树、35 株灌木、28 件装饰贝壳和 11 组小石子；84 个海底群落保留全部 2,744 件原成员记录，显示 1,325 件，每群 1–34 件。纯规则通过 63,263 项分布断言及 1,556 项地形 / 旧路线断言；Unity 2022.3.62f2 隔离工程的重复重建、手调 / 障碍 / 源文件保护、美术验证、鱼群实际身体安全和 Packed TerrainCoastFlowVerification 全部通过。全景和四侧正常视野已检查，前景海面无裁切断层；合回后再次核对完整成员记录、Ocean fileID、GUID 及源 / 布局 / 网格签名。

鱼群 48 秒回归通过 1,559,213 项实际群游 / 海床 / 障碍断言，另通过 Boids、随机数量、身体约束、分流归群检查。最终预览为 `Logs/CoastalLandscapeOverview.png`、`CoastalLandscapeDetail.png` 和 `CoastalLandscapeWest/South/East/North.png`；不增加配置地址、UI 或 v29 存档字段。主工程 Unity 已打开，本轮 AA 构建与运行验证在隔离工程完成；主工程使用打包加载前需执行 **Build Prototype Addressables**。未构建独立发行包或测量 GPU 性能。

TerrainCoastFlowVerification 在打包 AA 下实测美术依赖、网格顶点和包围盒、椰树实体碰撞 / 鼠标过滤 / 镜头淡化与材质恢复、旧摆放物优先、沙滩和码头移动、NPC 路线、钓鱼及睡觉存档约束，同时检查重复配置不会覆盖美术。

Tools/VerifyIslandTerrain.cs 覆盖原岛内位置和此前海岸的 720 个方向；住宅、制作 / 摆放与 NPC 日程继续回归。预览由 Unity 实际镜头渲染，位于 Logs/CoastArtOverview.png、CoastPalmFadePreview.png、CoastPierPreview.png、CoastFishingPreview.png。

`Tools/VerifyCoastalPalms.cs` 检查 256 组种子的确定性、数量 / 尺寸范围、树干 / 树冠间距、落点及通道，并检查 64 组小物分布，默认布局另检查径向错落和疏密差异。纯 C# 编译时加入 `Assets/Scripts/Terrain/CoastalPalmScatter.cs`、`CoastalDetailScatter.cs` 与 `Assets/Scripts/Interaction/IslandLayout.cs`。

`CoastalPalmVerification.RebuildAndRun` 仅在 `Logs/` 下的隔离工程执行，验证重建幂等、非椰树布置 / GUID 保留和普通 Setup 保留手调，再运行 `TerrainCoastFlowVerification.Run` 的 Packed AA 集成检查。验证工程使用独立公司 / 产品名隔离持久目录。新增实际镜头预览为 `Logs/CoastalPalmsOverview.png` 和 `Logs/CoastalPalmsDetail.png`；`VerifyAssets` 可对已烘焙资源做只读检查。
