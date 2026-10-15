# 岛屿地形、可行走沙滩与码头

TerrainManager 由 GameManager 创建，先通过现有 AA GameConfig.TerrainPrefabAddress 加载 `IslandTerrain`，再初始化农场与场景系统。地形预制体包含完整草地、缓坡沙滩 MeshCollider 和海岸边界；码头继续使用 AA 地址 `CoastalFishingSpot`，替换了原方块水池。旧的运行时方形地面和四面木墙已移除。

## 行走与边界

岛内原有农田、建筑和采集点坐标仍受平坦地面支撑。WASD 可以穿过原 12 米边界，走到外围沙滩。沙滩到海水的边缘由静态碰撞网格限制，东南侧为码头留出入口；木板平台提供实际地面碰撞，两侧与尽头限制落海。

PlayerMovement 在 CharacterController.Move 后还通过 TerrainManager 检查角色完整脚印，限制岸边和码头入口附近的越界移动。TerrainManager 用实际地面 / 码头碰撞射线解析存档落点；旧岛内位置恢复到地面上，被占用的位置沿用 HousingManager 的安全落点查找。

`IslandLayout` 是当前原型的统一轮廓规则，供 AA 网格生成、角色边界、摆放、NPC 路线和存档校验使用。地形和海床由同一个海岸轮廓推导；实际海床、海水仍由 OceanManager 管理。正式改变岛屿轮廓时，需要同时调整 IslandLayout、地形 / 边界网格及码头入口，不能只移动可见网格。

## 码头垂钓

默认钓点根坐标改为 (14.8, 0, -8.2)。FishingSpot.ReachPoint 标记实际垂钓位置，FishingManager 的距离检查使用该点；点击木板或护栏仍由 InteractionManager 分发到现有 FishingPanel。FishingPanel 使用 AA、PopUp 层和 MVC，移到左下方，给角色、海面与浮漂留出视野。

OceanSurface.HeightAt 采用与海洋着色器相同的两组波浪、材质波高 / 速度和动画时间。等待时浮漂中心高于水面 0.055 米；咬钩时降至水面下 0.045 米。FishingManager.LateUpdate 在海面更新后校准浮漂和鱼线，两端分别接竿尖与浮漂。

垂钓会话计时继续运行，因此海浪在钓鱼面板内也继续流动。普通暂停仍冻结波浪。成功收竿沿用现有库存与体力提交；失败、取消和重复收竿不新增结算。关闭垂钓后隐藏浮漂和鱼线。

## NPC、摆放和存档

默认 NPC 海岸停留偏移改为 (-0.65, 0, 2)，避开码头护栏。路线仍绕开农田，角色高度根据沙滩地面更新。新的默认路线比旧路线长 0.3 米，旧存档的绝对距离进度继续有效，不强行重置为出生点。

装饰可摆到新增陆地区域；整格必须留在陆地内，岸边保留通行余量，码头入口与 NPC 路线预留。摆放高度从地形推导，不新增存档字段。版本仍为 27，兼容原有岛内坐标；只有每日床边确认睡觉、完成结算才保存进度。

TerrainSetup 创建缺失地形和网格，使用 FishingSpot.Deck、FishingView.CoastLayout 和 NPCScheduleCatalog.CoastTerrainReady 标记一次性迁移，后续执行保留预制体、界面布局和日程配置的美术修改。替换码头时保留根标记、ReachPoint、单位缩放的 Deck BoxCollider、Bobber 与 Label，并检查入口 / 护栏 / 边界衔接。

## 验证

TerrainCoastFlowVerification 使用打包 AA 内容实测 CharacterController 行走、越过旧边界、沙滩落脚、码头入口及侧边 / 尽头防落海、旧岛内落点与 NPC 进度、摆放预留、鼠标垂钓、波浪 / 浮漂 / 鱼线、咬钩 / 捕获 / 取消、白天不保存、进出小屋及资源清理。

纯规则检查覆盖原有 25×25 坐标范围的地面支撑、海水拒绝、完整摆放格、旧路线进度恢复，以及住宅、制作 / 摆放和 NPC 日程。另回归交互提示、暂停睡觉、镜头淡化、角色动画和海洋流程。预览位于 Logs/WalkableBeachPreview.png、CoastPierPreview.png、CoastFishingPreview.png。

海岸示范区域已加入圆润轮廓、沙滩颜色渐变、植被、浅海与码头细节；地面和边界同步更新，既有海岸落点仍有效，见 [海岸场景美术](CoastalArt.md)。
