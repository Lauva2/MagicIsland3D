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

HousingManager 在睡觉结算完成扩建后加载现有扩建外观，并原位扩展室内。床、出口、家具与玩家区域继续使用原逻辑。白天申请扩建和交付订单只改变运行状态，只有睡觉结算保存进度，存档仍为版本 27。

## 编辑与维护

网格保存在 `Assets/Art/Meshes/Village`，材质位于 `Assets/Art/Materials/Village`。VillageArtSetup 首次补齐美术，并以 VillageBuildingVisual.ArtVersion 为迁移标记；后续 Configure Prototype Addressables 保留已有外观、材质和细节。小路以 IslandTerrain.Paths 引用作为一次性绑定标记，不重复创建。

VillagePaths.FollowFarmLayout 默认启用，进入游戏时按当前农田大小重新配置路径段。需要手工布局时可关闭该选项并编辑预制体。替换建筑美术时保留 HomePortal / OrderBoard、原碰撞、交互点及 CameraOccluder 引用；建筑模型仍使用现有尺寸，以保持既有布局和存档兼容。

## 验证

VillageArtFlowVerification 在打包 AA 下检查重复配置保留美术、原碰撞 / 门口 / 公告栏位置、小路适配全部 12 种农田大小、实际道路行走和公告栏接近、鼠标点击、订单交付 / 重复提交、申请扩建、室内摆放、真实睡觉完成扩建、家具实例保留、睡觉存档读回，以及菜单清理和唯一道路实例。

回归覆盖交互提示、镜头淡化、海岸垂钓、暂停与睡觉存档；纯规则覆盖小屋扩建、住宅 / 区域和 NPC 日程。Unity 实际渲染预览位于 Logs/VillageOverview.png、VillageCottagePreview.png、VillageHallPreview.png、VillageUpgradedCottagePreview.png。
