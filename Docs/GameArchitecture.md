# 游戏流程与系统框架

> 开发前先读根目录 [统一开发约束](../AGENTS.md)。本文保留专项实现与历史演进；旧版本号和旧保存入口描述不代表当前全局规则，现行约束以 AGENTS.md 为准。

当前进度统一在每天睡觉结算后保存；白天操作仅修改运行状态，面板手动保存和返回菜单保存已取消，设置单独持久化。睡觉失败只重试该次固定快照。规则见 [暂停与睡觉存档](PauseAndSettings.md)。

GameManager 统一调度流程，各系统由对应的 Manager 控制。UIManager 统一持有 UI 的 AA 实例句柄，UI 内部采用 MVC。

## 生命周期

`LoadingConfiguration → MainMenu → LoadingScene → InitializingGame → Playing → ReturningToMenu → MainMenu`

1. GameManager 是跨场景单例，创建各系统 Manager。
2. ConfigurationManager 初始化 AA，然后加载并校验 `GameConfig`、`ItemCatalog`、`CropCatalog`、`WeatherConfig`、`SeasonConfig`、`TaskCatalog`、`OrderCatalog`、`GatheringCatalog`、`ToolUpgradeCatalog`、`StorageConfig`、`CookingCatalog`、`NPCCatalog`、`DialogueCatalog`、`RelationshipCatalog`、`StoryEventCatalog`、`FishingCatalog`、`NPCScheduleCatalog`、`CraftingCatalog`、`HousingConfig`、`AnimalConfig`、`InventoryUpgradeConfig`、`ShippingConfig`、`FertilizerConfig`、`AudioCatalog` 和 `OrchardCatalog` 共 25 份配置。
3. SaveManager 检查本地存档；UIManager 按名字加载 `MainMenuPanel`，创建 MainMenuModel / MainMenuController，并根据存档状态启用“加载游戏”。
4. 点击“新游戏”使用初始状态；点击“加载游戏”先从磁盘读取并校验数据。GameManager 锁定流程，SceneManager 通过 AA 以 Additive 模式加载 GameConfig 指定的场景。
5. GameManager 先等待 FarmManager 按 GameConfig 加载 AA FarmPlot 并按 CropCatalog 加载全部阶段模型、PlayerManager 加载 AA 玩家、TerrainManager 加载 AA IslandTerrain 的地面与海岸碰撞，并配置其 IslandPaths 嵌套道路，再初始化 InventoryManager、EconomyManager、StaminaManager；FarmPrototype 创建镜头、灯光和农田网格；随后初始化 TimeManager、FarmManager、PlayerManager，按需恢复存档，再初始化 CalendarManager、CameraManager、WeatherManager、HousingManager、GatheringManager、ToolManager、InteractionManager、ShopManager。床由 HousingManager 绑定室内预制体，进入室内后交给 TimeManager。GatheringManager 恢复采集进度和冷却，ToolManager 管理共用农务／采集动作锁。FarmGrid 根据已恢复的进度显示对应模型；CalendarManager 从总天数推导日期，WeatherManager 恢复当天晴雨并根据已恢复的时刻设置光照，下一天使用新日期所属季节的降雨概率。
6. GameManager 初始化 TaskManager 和 OrderManager，恢复任务及当日订单，先等待 ToolManager 按 GameConfig.ToolEffectsPrefabAddress 加载 AA 农务特效，再等待 ShopManager 按 GameConfig.ShopPrefabAddress 加载 AA 商店摊位 `IslandShop`，再等待 AA 加载 `OrderHall` 并绑定门前公告栏，再等待 GatheringManager 加载模型和创建资源节点。系统与玩家区域恢复完成后，InteractionManager 加载 AA 高亮，MapManager 初始化实际地点与道路；随后 UIManager 按名字加载 `FarmHudPanel`、`InventoryPanel`、`CropInfoPanel`、`InteractionPromptPanel`、`FeedbackPanel`、`TaskPanel`，创建对应 Model / Controller，背包默认收起，作物信息、交互提示和任务在 Common 层，反馈挂在 Top 层。统一目标与点击规则见 [交互提示与目标高亮](InteractionPrompts.md)。
7. 完成以上步骤后隐藏主菜单、启用玩家及交互输入，进入 Playing。点击公告栏由 GameManager 调度 UIManager 加载 PopUp 层 OrderPanel。
8. 床边确认睡觉并完成结算后保存次日清晨快照；返回菜单先确认丢弃当天未睡觉进度，然后禁用输入、卸载场景、释放 HUD 与 Controller 事件、清理系统并显示主菜单，返回不写存档。睡觉写盘失败在日报重试同一快照。

主菜单启动场景保留用于返回，农场和小屋室内场景通过 AA 加载和卸载。玩家区域恢复在启用输入之前完成；进出采用 `Playing → Transitioning → Playing`，返回菜单先卸载室内再卸载农场。直接 Play MainGame 时也会自动创建 GameManager，走相同的配置和系统初始化流程；编辑器直接打开的场景按编辑器调试路径卸载。

## Managers

| Manager | 职责 |
| --- | --- |
| OrchardManager | 果树成长、结果日期、采收容量检查和阶段显示；位置与 AA 实例委托 PlacementManager，睡觉跨日并统一保存 |
| MapManager | 从场景读取地图地点、道路和玩家区域、坐标投影、选择说明与世界引用清理 |
| GameManager | 启动、新游戏、继续游戏、睡觉时统一保存调度、返回菜单、流程状态、初始化顺序、重复请求拦截 |
| SaveManager | 存档版本、校验、JSON 保存与读取、原子替换和备份恢复 |
| HousingManager | AA 小屋 / 扩建部分、室内绑定、区域和安全落点、显示层、门口通道、扩建费用 / 日期和跨日完工 |
| FurnitureManager | 室内家具记录、AA 预览与实例、旋转 / 摆放 / 回收、室内通道保护 |
| AnimalManager | 鸡舍建造、AA 鸡舍 / 鸡实例、购买 / 喂食 / 产蛋 / 收取 / 收购、存档和预留地 |
| NPCManager | 见面状态、AA 角色实例、距离、交谈朝向与角色释放 |
| DialogueManager | 首次 / 日常台词、季节天气选择、逐句会话与会话清理 |
| RelationshipManager | 好感度、关系阶段、指定格子送礼、每日限制、阶段招呼与送礼反馈 |
| StoryEventManager | 好感度事件条件、前置顺序、完成记录和食谱权限 |
| TerrainManager | AA 岛屿地形和海岸边界、码头地面绑定、角色防落海、旧存档安全落点、摆放地面和 AA 村落小路配置 |
| OceanManager | AA 海洋环境、深浅水色和泡沫、暂停 / 昼夜 / 晴雨、与浮漂共用的海浪时间 |
| FishingManager | 钓鱼会话、等待和咬钩时限、鱼获与体力、AA 鱼竿 / 钓点、浮漂和鱼线、清理 |
| SettingsManager | 本次运行的主音量和全屏参数，SettingsPanel 通过 MVC 调用 |
| EconomyManager | 金币数量、收入、扣款和恢复 |
| ShopManager | AA 商店摊位加载 / 释放 / 取消、商品价格、交易校验、柜台距离和买卖 |
| TimeManager | 时钟推进、床引用和距离检查；日期统一由 GameManager 调度 FarmManager 推进 |
| CalendarManager | 从农场总天数推导年份、季节和日期，提供播种季节检查及目标日期降雨概率 |
| TaskManager | 新手目标、成功操作进度、顺序领奖、奖励反馈和事件清理 |
| GatheringManager | 采集配置、AA 模型和节点实例、次数与材料产出、体力协调、受击美术与固定碰撞、恢复日期和清理 |
| OrderManager | 当日订单、办事处建筑 AA 实例和公告栏、交付校验、奖励反馈、次日刷新及清理 |
| WeatherManager | 当天晴雨、天气序列、昼夜光照、雨滴暂停／恢复及清理、雨水浇灌 |
| StaminaManager | 体力初始化、恢复、睡觉回满和清理；农务成功消耗由 FarmActions 协调 |
| ToolManager | 工具选择、动作锁、农务操作入口、手持模型、动作与 AA 特效依赖、暂停和操作反馈事件 |
| ConfigurationManager | 初始化 AA、加载并校验配置、持有配置句柄 |
| SceneManager | AA 场景加载和卸载、活动场景和句柄管理 |
| UIManager | 按预制体名字实例化 UI、创建和销毁 Controller、显示隐藏、资源释放 |
| InventoryManager | 初始物品、数量查询、添加／消耗、堆叠与容量检查 |
| FarmManager | 地块与阶段模型 AA 加载和释放、农田状态初始化、农务动作、跨日推进 |
| PlayerManager | 玩家初始化、移动参数、输入启停 |
| CameraManager | 镜头初始化、视角和缩放范围、控制启停 |
| InteractionManager | 统一鼠标目标与点击分发、距离 / 准备提示、AA 高亮加载和清理、交互启停 |

声音由 AudioManager 控制并接入 GameManager 的初始化和状态切换。后续新增系统同样由对应 Manager 控制，并接入所需的初始化阶段。

## UI MVC

- Model：`UI/Models`，保存界面需要的数据，不包含 UI 组件。
- View：MainMenuView、PrototypeHud 继承 UIView，保存 TMP_Text / Button 等序列化引用和 Layer 配置，负责渲染，不加载资源或调用游戏逻辑。UIManager 按 Bottom、Common、PopUp、Top 自动挂载并排序。
- Controller：`UI/Controllers`，绑定按钮事件、更新 Model、驱动 View、调用对应的 Manager；释放时解除自己绑定的事件。
- UIManager：UI 的统一入口和资源拥有者，通过 `Addressables.InstantiateAsync` 加载，通过 `ReleaseInstance` 释放；UI 预制体都在 `Assets/Art/Prefabs/UI`。

事件示例：`NextDay Button → FarmHudController → GameManager.AdvanceDay → FarmManager → FarmState → HUD Model/View`。

果树在 PlacementManager 恢复占位后初始化 OrchardManager，模型加载后刷新成长阶段；跨日时与农场同步推进结果日期，退出先解除摆放事件再释放实体。跨日时先补当天雨水，再结算农田、推进时钟与天气、应用次日雨水、回满体力、记录睡觉任务、刷新订单和资源节点并保存。天气和时刻通过 FarmHudController 更新 HUD，雨滴及光照由 WeatherManager 管理。订单交付在 GameManager 检查游戏状态、距离和面板后，由 OrderManager 提交，再在睡觉时统一保存。

## 配置

`Assets/Config/GameConfig.asset` 通过 AA 地址 `GameConfig` 加载，控制场景地址、农田尺寸、初始种子及金币、生长天数、时间流速、玩家速度、交互距离、镜头俯角和镜头大小。

编辑器配置工具只创建缺失配置，不覆盖已经调整的参数。配置不合法会停止启动并报告错误；启动失败后可调用 GameManager.RetryStartup。游戏场景或 HUD 初始化失败时返回菜单，允许重试。

## 验证

- `Tools/VerifyFarmState.cs`：509 项农务断言，包括配置参数生效。
- `GameFlowVerification.Run`：在独立验证副本中构建 AA，使用 Packed Play Mode 验证两轮进入/返回、单例、重复请求、MVC 按钮事件、日期推进、状态重置及 UI / 场景释放。
- `Magic Island > Validate UI Prefabs`：检查预制体组件和地址与名字一致。

工具升级由 ToolUpgradeManager 控制。ConfigurationManager 增加第九份 AA 配置 ToolUpgradeCatalog；世界初始化先恢复工具等级，再初始化 GatheringManager，加载办事处后加载 ToolWorkbench。升级面板通过 UIManager 加载，退出清理实例和句柄。详见 [工具升级](ToolUpgrades.md)。

储物由 StorageManager 控制。ConfigurationManager 增加第十份 AA 配置 StorageConfig；世界初始化恢复独立箱内库存，加载 StorageChest 后启用输入。StoragePanel 通过 UIManager 加载到 PopUp 层，存取后同时保存背包和箱子，退出清理实例与句柄。详见 [储物箱](Storage.md)。

烹饪与食物使用由 CookingManager / FoodManager 控制。ConfigurationManager 加载第十一份配置 CookingCatalog；GameManager 在背包和体力就绪后初始化模块，加载 CookingStation 后启用输入。CookingPanel 与背包按钮沿用 MVC，制作及食用后统一保存，退出释放料理台和模块引用。详见 [烹饪与食物](CookingAndFood.md)。

NPC 和对话由 NPCManager / DialogueManager 控制。ConfigurationManager 加载第十二、十三份 AA 配置 NPCCatalog / DialogueCatalog；GameManager 恢复见面状态并在办事处就绪后加载角色，再启用输入。对话会话固定台词快照，完整结束首次对话后保存，退出清理面板、会话和角色实例。详见 [NPC 与对话](NPCAndDialogue.md)。

好感度与送礼由 RelationshipManager 控制。ConfigurationManager 加载第十四份 AA 配置 RelationshipCatalog；GameManager 在 NPC 状态和背包就绪后初始化关系，再初始化 DialogueManager，使台词可按关系阶段选择。跨日同步总天数，成功送礼统一保存背包、好感度和送礼日期。GiftPanel 通过 UIManager 加载到 PopUp 层，进入时关闭对话，退出释放面板和关系引用。详见 [好感度与送礼](Relationships.md)。

好感度事件由 StoryEventManager 控制。ConfigurationManager 加载第十五份 AA 配置 StoryEventCatalog 并交叉校验食谱奖励。GameManager 恢复 NPC 和关系后初始化事件，再初始化 CookingManager 和 DialogueManager，使制作及对话读取当前权限。事件沿用 DialoguePanel，完成记录与食谱权限同步生效，再由 GameManager 保存，退出清理会话、制作和事件引用。详见 [好感度事件](StoryEvents.md)。

钓鱼由 FishingManager 控制。ConfigurationManager 加载第十六份 AA 配置 FishingCatalog；GameManager 在背包、体力、工具就绪后初始化会话，进入世界时加载鱼竿和海岸钓点。FishingPanel 暂停世界并保持自身计时，GameManager 调度范围、捕获和保存，ShopManager 提供单向鱼类收购，退出先取消会话并释放模型，再清理工具和背包。详见 [钓鱼](Fishing.md)。

NPC 日程由 NPCScheduleManager 控制。ConfigurationManager 加载第十七份 AA 配置 NPCScheduleCatalog，GameManager 初始化日程、在角色和设施加载后验证路线，再启用输入。更新时与世界模态暂停一致；NPCManager 的 Focus 额外暂停被交谈角色。睡觉重置上午位置，SaveManager 保存路线距离，退出先清理日程再释放 NPC 实例。详见 [NPC 日程](NPCSchedules.md)。

制作与摆放由 CraftingManager / PlacementManager 控制。ConfigurationManager 加载第十八份 AA 配置 CraftingCatalog。GameManager 初始化纯状态，世界设施就绪后加载模型并恢复已摆放实体，再验证 NPC 路线。工作台 / 背包进入 MVC 制作与摆放流程，提交库存和世界状态后保存；退出先取消预览并释放实体，再清理 NPC 与库存。详见 [制作与摆放](CraftingAndPlacement.md)。

玩家小屋由 HousingManager 控制。ConfigurationManager 加载第十九份 AA 配置 HousingConfig，GameManager 进入世界时加载小屋外观，恢复 PlayerArea，并在需要时由 SceneManager 追加加载室内场景。切换期间使用 Transitioning 状态和 Top 层 MVC 面板暂停输入；室内时间和世界系统继续运行，保存区域与局部位置。返回菜单先卸载室内再卸载农场，清理小屋与系统。详见 [玩家小屋](Housing.md)。

室内家具由 FurnitureManager 控制，复用 PlacementManager 的预览与 AA 资源管理，使用独立室内 PlacementState。家具配方加入现有 CraftingCatalog，通过 IsFurniture 分区，不新增配置加载入口。室外读档时恢复家具记录，第一次进屋后绑定室内并通过 AA 创建家具；UIManager 将 PlacementPanel 的 MVC 绑定到当前区域的管理器。GameManager 统一调度制作、摆放、回收和保存，存档版本 18 新增 Furniture，退出释放两组摆放实例和状态。详见 [室内家具](Furniture.md)。

鱼类与鸡蛋料理复用 CookingManager / FoodManager / RelationshipManager，CookingCatalog 支持 Crop、Fish 和 AnimalProduct 食材。CookingPanel 使用七卡滚动布局，鱼类和鸡蛋料理默认开放，蔬菜浓汤保留事件权限。制作、食用和送礼继续由 GameManager 保存，沿用当前版本 22 的库存、好感度与体力字段。详见 [鱼类料理](FishCooking.md) 和 [鸡蛋料理](EggCooking.md)。

AnimalManager 管理鸡舍、鸡、喂食与产物。GameManager 等待世界设施和模型就绪后加载养殖 AA 资源，恢复鸡舍及鸡；跨日只由统一结算入口产蛋。办公室和商店分别进入 CoopPanel、AnimalShopPanel，操作库存、金币和养殖状态一起提交保存；返回释放实例和句柄。版本 20 记录 Animals，详见 [鸡舍养殖](Animals.md)。

养殖与料理订单沿用 OrderManager 和 AA OrderCatalog / OrderPanel。GameManager 在跨日结算时读取鸡舍建成状态，启用下一天的扩展池；建造当天不更新订单。版本 21 保存生成当天使用的 CoopOffersUnlocked 标记，旧档当天仍按原作物池校验，读档不抽新单或重发奖励。交付期间同步提交背包、金币与完成标记，拦截回调重入、关闭和返回；详见 [养殖与料理订单](AnimalOrders.md)。

饲料制作沿用 CraftingManager 和 AA CraftingCatalog / CraftingPanel。IsSupply 配方允许作物或材料制成 Material 用品，普通用品不加载世界模型、不进入室内外摆放定义。制作面板按装饰、家具、养殖用品分页，背包也按类型隐藏不适用操作；GameManager 继续统一调度、睡觉时统一保存和清理，沿用 v22 库存字段。详见 [饲料制作](FeedCrafting.md)。

鸡的互动沿用 AnimalManager / AnimalState，GameManager 检查照料模式、面板、距离和操作锁后提交摸摸与睡觉时统一保存。CoopPanel 的 MVC 分别展示稳定 ID 对应的名字、Slider、阶段和每日状态。面板使用 HeartGraphic，Chicken 的 AA 预制体持有世界爱心网格 / 材质依赖，AnimalManager 调度瞬时反馈和清理。v22 保存每只鸡的 Affinity / LastPetDay，旧档不补奖励且保留原喂食、产物和订单；见 [鸡的互动与亲密度](AnimalFriendship.md)。

岛屿轮廓、实际可行走沙滩、木码头、浮漂与海浪同步，以及旧落点和 NPC 路线兼容规则见 [可行走海岸与码头](WalkableCoast.md)。地形先于玩法初始化，返回菜单由 TerrainManager 释放 AA 实例及碰撞资源。

玩家小屋与订单办事处的美术、原交互点 / 碰撞兼容及道路初始化见 [村落建筑与小路](VillageArt.md)。道路归 TerrainManager，建筑继续由 HousingManager / OrderManager 控制。

地图由 MapManager 管理，UIManager 按需加载 PopUp 层 MapPanel。M / HUD 打开，M / Esc / 关闭按钮收起；加载与显示期间纳入模态暂停，禁止叠加其他玩法面板。室内以小屋门口标记位置；选择用途不影响世界和存档。见 [岛屿地图与地点指引](Map.md)。
