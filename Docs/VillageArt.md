# 村落建筑与小路

玩家小屋与订单办事处采用暖色灰泥、栗色木框、陶瓦、象牙白收边和青绿色门窗。小屋补充了山墙、瓦片、门窗框、烟囱、灯具和花箱；扩建外观增加阁楼窗及花箱。办事处完善屋顶、木框、百叶窗、门窗与公告栏，公告栏仍位于建筑前方。

## AA 与 Manager

小屋沿用 AA `PlayerHouse` 和 `PlayerHouseExpansion`，由 HousingManager 加载和控制；办事处沿用 AA `OrderHall`，由 OrderManager 管理。各预制体保留原 `.meta` 和交互组件。

新增 AA `IslandPaths`，位于 `Assets/Art/Prefabs/Environment/IslandPaths.prefab`，作为 `IslandTerrain` 的嵌套依赖加载，资源归 TerrainManager 所有。GameManager 在地形加载后、玩法初始化前调用 TerrainManager.ConfigurePaths；退出时随地形一并释放。没有新增存档字段或配置加载入口。

VillagePaths 是预制体表现组件，通过变换已经保存的道路网格适配农田大小，运行时不创建美术网格。小路连接小屋门前、农田南侧、东侧 NPC 通道、办事处 / 公告栏和码头入口。普通路径宽 1.15 米，路口略抬高以避免重叠面的闪烁。

## 交互、通行与扩建

小屋保持原 1.3 × 2 米占地碰撞、门口距离点和室外返回点；办事处保留原基础、墙体及公告栏碰撞和位置。新美术细节没有 Collider，并使用 Ignore Raycast 层，避免额外拦截门口和公告栏点击。

小路没有 Collider，沿既有地面行走。初始布局为角色、NPC 和公告栏留出通道；路径只提供视觉引导，摆放仍遵循现有农田、NPC 和门口通道保护规则。已有玩家建设不因道路出现而删除或移动。

新增建筑细节接入已有 CameraOccluder，公告栏及订单纸张排除在办事处整体淡化之外，保持可辨认。道路不投射阴影，进屋后由室内镜头层级隐藏。

HousingManager 在睡觉结算完成扩建后加载现有扩建外观，并原位扩展室内。床、出口、家具与玩家区域继续使用原逻辑。白天申请扩建和交付订单只改变运行状态，只有睡觉结算保存进度，存档仍为当前版本 30。

## 编辑与维护

网格保存在 `Assets/Art/Meshes/Village`，材质位于 `Assets/Art/Materials/Village`。VillageArtSetup 首次补齐美术，并以 VillageBuildingVisual.ArtVersion 为迁移标记；后续 Configure Prototype Addressables 保留已有外观、材质和细节。小路以 IslandTerrain.Paths 引用作为一次性绑定标记，不重复创建。

VillagePaths.FollowFarmLayout 默认启用，进入游戏时按当前农田大小重新配置路径段。需要手工布局时可关闭该选项并编辑预制体。替换建筑美术时保留 HomePortal / OrderBoard、原碰撞、交互点及 CameraOccluder 引用；建筑模型仍使用现有尺寸，以保持既有布局和存档兼容。

## 验证

### 出生沙滩至生活区精修（2026-10-18）

显式 **Magic Island > Art > Refine Beach Arrival Route (Explicit)** 只处理生活区美术，不重建全岛：

- `Village Grass` 原网格在草沙交界内按连续噪声与岸向变化更新顶点色和 UV0.x 草色权重，保留原调色差异与干湿沙表现。全部位置、法线、三角面和地形碰撞保持；不改 Biomes 分区、出生点或可建设资格。
- 原 `Village Path Strip/Junction` 只补 UV0 和 UV2 形状标记，保留几何与 GUID。原 `Village Sand Path` 材质切换为共享 `MagicIsland/Village Path`，保留原 `_Color` 和 `_Glossiness`，以轻微颗粒变化、柔化透明边缘融合草地；不增加道路实体、宽度或碰撞。`EdgeSoftness` 默认 0.15，可人工调整。显式操作遇到其他路径着色器、纹理／金属／发光材质或自定义 UV 时拒绝覆盖；普通 Setup 不应用此操作。
- `Island Biome Ground` 的 `ApproachStrength`（默认 0.48）与 `ApproachColor` 表现窄而轻微弯曲的暖沙磨损。`TerrainManager.ConfigurePaths` 在原初始化调用中，向当前地面实例的 MPB 写入 AA `NewGameSpawn` 与已经配置的第 1 号横向道路最近点；随 1–12 农田尺寸与手工道路实际变换适配。仅配置时用原地面与有界胶囊查询检查七个弯曲候选，绕开实际树干／岩石，忽略玩家／NPC 与原地面／玩家专用海岸边界；查询满或全部受阻则关闭这段磨损，绝不移动实体或改变导航／建设规则，退出清空查询引用。其他大陆地面不写入这组参数，默认不显示；不新增 Update、网格生成、全局着色状态或共享材质写入。强度 0 可关闭磨损。
- 现有 `SeasonalGroundCover` 增加六簇错落、低矮的路旁草丛，共享已有四季网格／材质和建设遮盖机制，总池仍不超过 160。原地表记录顺序与引用保留；新增组名 `Arrival verge` 作为补全标记，重复显式操作不再增加数量，也不重置已存在草丛的人工变换。无额外碰撞、拾取、声音或掉落，暂停／室内／退出沿用原系统；玩家旧建设和新建设优先，回收后恢复。

运行时入口、存档结构、26 份配置与 29 个 UI 不增加。普通 Setup 保留原网格颜色、道路材质、草丛布局与参数；修改结果随原 `IslandTerrain/IslandPaths` AA 依赖加载，主工程需执行 **Build Prototype Addressables**。

隔离入口 `ArrivalRouteVerification.BakeAndVerify` 检查全部原层级、变换、激活状态、组件与原几何保留，重复烘焙稳定，普通 Setup 保留手调颜色／路肩，以及自定义 UV 拒绝覆盖。`OceanWaterAppearanceVerification.RunArrivalBaseline/RunArrival` 对照相同镜头和日照下的沙滩、小径、小屋、村落、码头；After 额外检查 12 种农田的真实道路连接和新增草丛的实际建设遮盖／回收。日志与画面位于 `Logs/ArrivalRoute`，均为忽略的本机验收产物。

本轮已在 Unity 2022.3.62f2 隔离工程通过上述烘焙、幂等、手调保留与真正 Packed AA 验证。默认生活区采用弯曲量 -1.6 米的通路绕过出生点前的椰树，12 种农田尺寸均通过实际地面、完整地面脚印与实体胶囊检查；新草丛实际建造后隐藏、回收后恢复。清晨／白天／夜间／雨天各输出相同视角对照，前后各 36 张画面；深海和湖水在全部四种光照下逐像素一致。最终预览为 `Logs/ArrivalRoute/Comparison.png`，左为修改前、右为修改后。合回时核对本轮 8 份已有资源／代码和 6 份新增源码／meta 与打包验证工程一致，另 2,777 份已有美术／脚本／编辑器文件保持原字节；原资源 meta／GUID 保留。

本轮 `VillageArtFlowVerification.Run` 也已通过：全部农田道路适配、实际道路行走、门口／公告栏碰撞和点击、订单／扩建／室内摆放、真实睡觉完成扩建并保存读回、返回／重进和唯一道路实例。保持存档 v30，不增加持久字段或迁移；本轮未重跑完整海洋生态、全版本迁移、独立 Player 或 GPU 性能测量。

VillageArtFlowVerification 在打包 AA 下检查重复配置保留美术、原碰撞 / 门口 / 公告栏位置、小路适配全部 12 种农田大小、实际道路行走和公告栏接近、鼠标点击、订单交付 / 重复提交、申请扩建、室内摆放、真实睡觉完成扩建、家具实例保留、睡觉存档读回，以及菜单清理和唯一道路实例。

回归覆盖交互提示、镜头淡化、海岸垂钓、暂停与睡觉存档；纯规则覆盖小屋扩建、住宅 / 区域和 NPC 日程。Unity 实际渲染预览位于 Logs/VillageOverview.png、VillageCottagePreview.png、VillageHallPreview.png、VillageUpgradedCottagePreview.png。
