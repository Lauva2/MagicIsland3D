# MagicIsland3D 农务原型

Unity 2022.3.62f2，内置渲染管线，旧版 Input，Addressables 1.21.17。

## 开发约束

以下约定适用于后续新增功能和现有代码修改。

昼夜光照由 WeatherManager 按 AA 的 WeatherConfig 控制：白天使用移动的暖色阳光与三色天空补光，日落后关闭阳光并保留微弱月光；夜间环境、反射与海水同步变暗，室内灯光独立。参数与验证见 [昼夜与天气](Docs/Weather.md)。

室外风力由 WeatherManager 统一管理，WeatherConfig 配置晴雨风力、方向、阵风与过渡。椰叶、灌木、橡树及果树树冠在 GPU 中轻微摆动，云速和风声使用同一倍率；暂停与室内冻结，原碰撞、采集受击、场景布局和存档保持。风动材质随原有 AA 植被预制体加载，并支持遮挡淡化，见 [植被风动](Docs/VegetationWind.md)。

### 统一存档规则

- 游戏进度仅在每天小屋床边确认睡觉、完成结算后保存，存档点为次日清晨。白天交易、升级、存取、玩法操作及进出场景只更新运行状态。
- 暂停菜单不提供保存，各玩法面板不提供手动保存。返回主菜单须确认丢弃当日未睡觉进度，返回不保存；新游戏第一次睡觉前不创建该局存档。
- 睡觉写盘失败只允许在日报重试同一结算快照，不重新跨日或发钱。成功后确认日报不写盘，加载恢复最近一次成功睡觉的清晨。
- SettingsManager 将音量和全屏单独持久化，设置可随时保存，与游戏进度文件分离。详见 [暂停与设置](Docs/PauseAndSettings.md) 和 [存档](Docs/Saving.md)。

### 游戏流程与系统管理

- 果树由 `OrchardManager` 管理，AA 加载 `OrchardCatalog`、`AppleTree` 和 `OrangeTree`。商店购买树苗，背包摆放于空地，睡觉成长与结果，左键采收；右键回收重置成长并清除未采果实。位置与模型复用 PlacementManager，果树状态仅睡觉保存。规则见 [果树](Docs/Orchard.md)。
- 整个游戏流程必须由 `GameManager` 统一控制，包括启动、配置加载、场景切换、进入游戏初始化和返回主菜单。
- NPC 与对话由 `NPCManager`、`DialogueManager` 管理，AA 加载角色、台词配置和 PopUp 层 `DialoguePanel`。完成首次对话才记录见面，睡觉时保存，季节和天气决定日常台词；规则见 [NPC 与对话](Docs/NPCAndDialogue.md)。
- NPC 日程由 `NPCScheduleManager` 管理，AA 加载 `NPCScheduleCatalog`。林舟上午在办事处、下午前往海岸、傍晚返回；挡路和模态交谈暂停行走，睡觉重置上午位置，存档恢复途中进度。规则见 [NPC 日程](Docs/NPCSchedules.md)。
- 制作与摆放由 `CraftingManager`、`PlacementManager` 管理，AA 加载 `CraftingCatalog`、装饰模型、PopUp 层 `CraftingPanel` 和 Common 层 `PlacementPanel`。工作台制作木栅栏、石板路和庭院灯，背包选择后网格预览 / 旋转 / 确认，右键回收；库存与摆放记录一起保存。规则见 [制作与摆放](Docs/CraftingAndPlacement.md)。
- 玩家小屋由 `HousingManager` 管理，`GameManager` 调度进出、睡觉、位置恢复和保存，`SceneManager` 通过 AA 加载室内场景。床移入室内，农场 / NPC / 装饰状态保留；Top 层 `SceneTransitionPanel` 显示切换进度。规则见 [玩家小屋](Docs/Housing.md)。
- 小屋扩建由 `HousingManager` 管理，公告栏进入 PopUp 层 MVC 面板 `HouseUpgradePanel`，默认支付木材 20、石料 10、金币 300，次日完工。室内原位扩至 16×12，保留家具、床、出口和角色位置；参数沿用 AA 的 `HousingConfig`，施工状态与升级等级一起保存。规则见 [小屋扩建](Docs/HouseUpgrades.md)。
- 室内家具由 `FurnitureManager` 管理，复用 `PlacementManager` 的 AA 预览和交互，独立保存室内占位。工作台分页制作木桌、木椅和小台灯，室内背包选择摆放，床边、出生点和出口通道保留。规则见 [室内家具](Docs/Furniture.md)。
- 养殖由 `AnimalManager` 管理。办事处申请建造鸡舍，商店“养殖用品”购买鸡与饲料、出售鸡蛋；每只鸡每天一份饲料，次日产蛋，最多两只。配置及鸡舍 / 鸡模型通过 AA 加载，CoopPanel、AnimalShopPanel 采用 PopUp 层 MVC；养殖与库存 / 金币一同保存。规则见 [鸡舍养殖](Docs/Animals.md)。
- 好感度与送礼由 `RelationshipManager` 管理，AA 加载 `RelationshipCatalog` 和 PopUp 层 `GiftPanel`。礼物、好感度与每日送礼日期一起提交和保存；规则见 [好感度与送礼](Docs/Relationships.md)。
- 好感度事件由 `StoryEventManager` 管理，AA 加载 `StoryEventCatalog`，沿用 `DialoguePanel`。完整结束事件后记录完成和解锁食谱，由 `GameManager` 保存；规则见 [好感度事件](Docs/StoryEvents.md)。
- 钓鱼由 `FishingManager` 管理，AA 加载 `FishingCatalog`、`FishingRod`、`CoastalFishingSpot` 和 PopUp 层 `FishingPanel`。鱼获与体力原子提交，捕获及售鱼成功后更新状态，睡觉时保存；规则见 [钓鱼](Docs/Fishing.md)。
- 每个系统必须由对应的 `Manager` 控制。现有配置、场景、UI、背包、农务、玩家、镜头、交互、存档、设置、经济、商店、时间、日期季节、天气、体力、工具、任务、订单、采集、工具升级、储物、烹饪、食物使用、NPC、对话、好感度、事件、钓鱼、NPC 日程、制作、摆放、果树、家具、玩家小屋、养殖和每日经营结算分别由对应 Manager 管理；新增其他系统时沿用这一规则。
- 烹饪与食物使用由 `CookingManager`、`FoodManager` 管理，AA 加载 `CookingCatalog`、`CookingStation` 和 PopUp 层 `CookingPanel`。制作原子扣料并加入产物，背包食用恢复体力，成功后记录，睡觉时保存；规则见 [烹饪与食物](Docs/CookingAndFood.md)。
- 出货由 `ShippingManager` 管理，AA 加载 `ShippingConfig`、小屋旁的 `ShippingBox` 和 PopUp 层 MVC `ShippingPanel`。农作物、鱼和两种蛋当天可存取，睡觉统一售卖，物品、金币、结算日期和日报明细一起保存；规则见 [出货箱与隔夜售卖](Docs/Shipping.md)。
- 储物由 `StorageManager` 管理，AA 加载 `StorageConfig`、`StorageChest` 和 PopUp 层 `StoragePanel`。存取先检查目标容量，背包与箱子一起提交和保存；规则见 [储物箱](Docs/Storage.md)。
- 工具升级由 `ToolUpgradeManager` 管理，AA 加载 `ToolUpgradeCatalog`、`ToolWorkbench` 和 PopUp 层 `ToolUpgradePanel`。费用与等级一起结算和保存，规则见 [工具升级](Docs/ToolUpgrades.md)。
- 资源采集由 `GatheringManager` 控制，配置 `GatheringCatalog`、树木 `GatheringTree` 和石头 `GatheringRock` 均通过 AA 加载。采集协调体力、背包和资源状态，失败不扣除，按天恢复并随进度保存。
- 每日订单由 `OrderManager` 控制；订单配置和办事处建筑通过 AA 地址 `OrderCatalog`、`OrderHall` 加载，门前公告栏打开 PopUp 层 `OrderPanel`。交付状态、作物和奖励一起保存，读档不能重复领奖。
- 新手任务由 `TaskManager` 控制，任务配置按 AA 地址 `TaskCatalog` 加载；进度只由成功操作更新，领奖和金币一起保存，继续游戏不能重复支付奖励。
- `GameManager` 负责流程调度和系统初始化顺序；具体系统规则放在对应的 Manager 及其内部模块中。
- 启动顺序为：初始化 AA → 加载并校验配置 → 显示主菜单 → 加载游戏场景 → 初始化各系统 → 显示游戏 UI → 启用输入。
- 场景内对象由对应系统初始化，不自行启动整套游戏流程。场景切换需防止重复请求，并在退出时清理系统引用和资源。

### 资源加载

- 项目使用 Addressables（AA）加载运行配置、游戏场景、UI 及后续正式美术资源。
- 配置加载由 `ConfigurationManager` 负责，游戏场景加载和卸载由 `SceneManager` 负责，UI 加载和释放由 `UIManager` 负责，并由 `GameManager` 调度。
- 声音由 `AudioManager` 管理，`ConfigurationManager` 通过 AA 加载 `AudioCatalog`，音效与三段循环配乐也通过 AA 加载。工具和交易成功才播放对应音效，UI 按钮 / 面板声音沿用预制体与 MVC；音乐按主菜单、农场、小屋平滑切换，并接入现有总音量设置，见 [音效与背景音乐](Docs/Audio.md)。
- 海岸环境音复用 `AudioManager` 与 AA：海浪随离岸距离衰减，风雨随天气切换，海鸥间歇鸣叫并在真实惊飞时扑翼；实际贴地移动按草地、沙滩、木码头 / 小屋播放脚步。弹窗停止瞬时世界声音并压低环境循环，室内淡出、菜单清理，音频总量固定且不写进度，详见 [海岸环境音](Docs/CoastalAudio.md)。
- 加载句柄由对应 Manager 持有并负责释放；失败路径也必须清理资源，避免重复加载和泄漏。
- 当前启动场景 `MainMenu` 随 Player 构建，游戏场景通过 AA 加载；运行时生成的原型几何体作为占位内容保留。

### UI 预制体与 MVC

- UI 文本使用 TextMeshProUGUI，脚本通过 `TMP_Text` 引用。`Assets/Art/Fonts/IslandUI.asset` 为中文字体，已配置为 TMP 默认字体和全局回退字体。编辑器配置 UI 时自动补齐代码、配置和预制体文案的字形；运行时外部文案需预先补齐字形。
- UI 从低到高分为 `Bottom`、`Common`、`PopUp`、`Top`。`UIView.Layer` 配置面板层级，`UIManager` 自动挂载并设置排序；同层后创建的面板显示在上方。主菜单为 Bottom、HUD 为 Common、背包为 PopUp。
- UI 面板预制体统一使用 `Panel` 后缀，例如 `MainMenuPanel`、`InventoryPanel`，AA 地址与名字一致；MVC 的 Model、View、Controller 保留各自职责命名。
- 所有 UI 必须创建为预制体，统一存放在 `Assets/Art/Prefabs/UI`，使用对应的 Unity UI 组件。
- 所有 UI 预制体必须通过 `UIManager` 使用 AA 加载，AA 地址必须与预制体名字完全一致，不包含扩展名或路径前缀。例如 `FarmHudPanel.prefab` 的地址为 `FarmHudPanel`。
- UI 框架统一使用 MVC：Model 保存界面数据；View 保存组件引用并负责显示；Controller 绑定交互事件、更新 Model、驱动 View，并调用对应 Manager。
- View 不承担资源加载、场景切换或游戏规则；Controller 销毁时解除自身绑定的事件；UIManager 负责界面的加载、显示、隐藏、关闭和资源释放。
- UI 初期以纯色组件建立布局，配色应适配休闲经营游戏的风格。当前采用奶油白、草绿、暖沙色及圆角、柔和阴影；主菜单使用背景图片。

### 玩法与操作方向

- 时钟由 `TimeManager` 控制；`CalendarManager` 根据农场总天数计算年份、季节和日期，`GameManager` 统一调度跨日。季节配置使用 AA 地址 `SeasonConfig`，默认每季 28 天。背包、商店、睡觉、订单、工具升级、储物、烹饪、对话、送礼和钓鱼弹窗打开及加载时暂停计时，睡觉结算后自动保存。钓鱼会话自身计时继续。
- 昼夜与晴雨由 `WeatherManager` 控制，天气参数通过 AA 地址 `WeatherConfig` 加载。雨天自动浇灌已种植的露天农田，跨日只成长一次；当天天气和随机种子随进度保存，读档不重新抽取。
- 体力由 `StaminaManager` 控制；农务操作成功才消耗，失败不扣除，睡觉回满。体力不足时仍可移动、交易和休息。
- 工具选择、动作锁、手持表现和操作反馈由 `ToolManager` 控制。动作先结算一次再播放，动画期间禁止重复操作和工具切换；提示使用 Top 层的 `FeedbackPanel`，通过 AA / MVC 管理且不拦截鼠标。
- 基础肥料与品质由 `FertilizerManager`、`FarmManager` 管理，AA 加载 `FertilizerConfig`。工作台用木材 ×2 制作肥料 ×5，按 7 走近点击耕地施肥；普通 / 优质两档分别堆叠和计价，料理与订单优先使用普通作物。品质固定，仍只在睡觉时保存，见 [肥料与作物品质](Docs/FertilizerAndCropQuality.md)。
- 作物配置 `CropCatalog` 通过 AA 加载，FarmManager 管理种子选择与各地块独立生长，ShopManager 管理不同作物的买卖。收获物品按地块作物类型发放，不能由当前种子选择决定。
- `CropCatalog` 配置适宜播种季节，`FarmManager` 在实际播种时检查；失败不扣种子和体力。商店全年出售种子，背包和商店显示季节说明，已播种作物跨季继续成长和收获。
- 作物幼苗、生长中、成熟模型存放在 `Assets/Art/Prefabs/Crops`，在 CropCatalog 中配置阶段 AA 地址，由 FarmManager 统一加载和释放。阶段从已有生长进度推导，同阶段不重复创建模型。`CropInfoPanel` 使用 Common 层、AA 同名地址和 MVC 显示指向地块的信息，不拦截鼠标。
- 商店与金币分别由 `ShopManager`、`EconomyManager` 控制；商店摊位使用 AA 地址 `IslandShop`，由 ShopManager 持有和释放实例，交易必须先检查资金、物品和背包容量，失败不改变双方状态；金币随进度保存。
- 游戏以休闲经营成长为主，采用类似星露谷和动森的 3D 斜俯视镜头。
- 玩家使用 WASD 按画面方向移动，使用鼠标点击目标进行操作；工具操作需检查距离，UI 点击需拦截，避免触发场景交互。
- 角色由 `PlayerManager` 通过 AA 地址 `PlayerCharacter` 加载，预制体包含待机、行走和七种操作动画；行走按实际位移计算步频，工具按动作锁进度采样。模型与朝向 / 碰撞分开，进出小屋保留角色、退出释放句柄，见 [角色与动画](Docs/PlayerCharacterAndAnimation.md)。
- 鼠标目标、提示与点击分发由 `InteractionManager` 统一控制，目标使用柔和脚下轮廓；`InteractionPromptPanel` 经 AA / MVC 加载到 Common 层，显示名称、操作及距离 / 工具准备状态。已种植地块沿用作物信息卡，UI 与弹窗遮挡时隐藏，见 [交互提示与目标高亮](Docs/InteractionPrompts.md)。
- 岛屿地形由 `TerrainManager` 通过 AA 地址 `IslandTerrain` 加载，`GameManager` 在玩法系统初始化前准备地面和海岸边界。角色可走到沙滩和真实码头；钓鱼浮漂与海面使用同一波浪时间，鱼线接竿尖。统一轮廓约束行走、摆放和存档落点，兼容旧岛内坐标与 NPC 路线进度，见 [可行走海岸与码头](Docs/WalkableCoast.md)。
- 玩家小屋、扩建外观与订单办事处已接入陶瓦、木框门窗及公告栏细节；AA `IslandPaths` 作为地形依赖由 `TerrainManager` 管理，`GameManager` 在初始化前配置连接小屋、办事处与码头的小路。保留原碰撞、交互点、家具和睡觉存档规则，见 [村落建筑与小路](Docs/VillageArt.md)。
- 岛屿地图由 `MapManager` 管理，按 M 或 HUD 地图按钮打开 AA `MapPanel`。显示当前位置、设施、采集区和钓鱼点，点击查看用途，室内标记小屋；面板按 MVC 挂在 PopUp 层，打开时暂停世界，见 [岛屿地图与地点指引](Docs/Map.md)。
- 可采集橡树与岩石增加枝干、树冠、年轮、矿物纹路和残迹细节；受击只摇晃独立美术层，原碰撞固定，斧头 / 镐子分别产生 AA 木屑 / 碎石，弹窗同步暂停，见 [树木、岩石与采集反馈](Docs/GatheringArt.md)。
- 农田使用 AA `FarmPlot`，区分草地、翻耕田垄、湿土和施肥细节；九个作物阶段补充叶脉、根部、番茄支架和玉米穗粒。AA `FarmActionEffects` 由 ToolManager 管理，提供扬尘、水花和收获闪光，弹窗暂停，见 [农田与作物表现](Docs/FarmArt.md)。
- 商店摊位、工具工作台、料理台和储物箱使用统一的木色、浅绿与黄铜材质，补充棚布、工具架、锅具和锁扣细节。保留交互与碰撞范围，接入 AA 管理和遮挡淡化，见 [商店与生产设施美术](Docs/FacilityArt.md)。
- 海岸美术使用可编辑的椰树、灌木、贝壳、礁石、珊瑚和水草嵌套预制体，由地形 / 海洋 AA 实例携带依赖。岸线圆润化与地面、海床、碰撞同步，椰树支持遮挡淡化，读档时避让已有玩家建设，见 [海岸场景美术](Docs/CoastalArt.md)。
- 海洋由 `OceanManager` 管理，`GameManager` 通过现有 AA `GameConfig.OceanPrefabAddress` 调度加载 `IslandOcean`。240×240 米海面包含固定种子生成的连续环岛浅滩，抬高近岛海床，以自然不规则外缘向外海沟槽与深海过渡，并保留离岸水下沙洲；浅水群落布置海草、多种珊瑚、海绵、海星与贝类。32 群环境鱼包含银色小鱼、小丑鱼、蝴蝶鱼与鳐鱼，海边有盘旋 / 掠水 / 停栖的海鸥和横移的小螃蟹。玩家靠近会惊飞海鸥、使螃蟹沿沙滩逃开；附近垂钓与成功抛竿会使鱼群加速散开，再逐渐恢复。浮漂落水有水滴与贴合波浪的涟漪，受惊鱼群产生细水纹，鳐鱼和螃蟹留下少量沙尘，特效由嵌套 AA 预制体中的固定槽位复用。水色、生物与特效接入昼夜和暂停，在室内随海洋隐藏。地貌烘焙和参数见 [风格化海洋](Docs/Ocean.md)。
- 镜头遮挡由 `CameraManager` 管理，AA 模型通过 `CameraOccluder` 配置参与的 Renderer；树木、建筑和室内墙壁遮挡角色时平滑淡化，离开、切换场景或退出时恢复。参数沿用 AA `GameConfig`，见 [镜头遮挡淡化](Docs/CameraOcclusion.md)。

## 运行

1. 打开工程，等待 Package Manager 安装 Addressables 和脚本编译完成。
2. 编辑器自动创建 GameConfig、ItemCatalog、CropCatalog、WeatherConfig、SeasonConfig、TaskCatalog、OrderCatalog、GatheringCatalog、ToolUpgradeCatalog、StorageConfig、CookingCatalog、NPCCatalog、DialogueCatalog、RelationshipCatalog、StoryEventCatalog、FishingCatalog、NPCScheduleCatalog、CraftingCatalog、HousingConfig、AnimalConfig、InventoryUpgradeConfig、ShippingConfig、FertilizerConfig、AudioCatalog、OrchardCatalog 共 25 份配置，并注册配置、MainGame、PlayerHome 和 UI；也可以执行 `Magic Island > Configure Prototype Addressables`。
3. UI 预制体位于 `Assets/Art/Prefabs/UI`，自动创建缺失文件，也可以执行 `Magic Island > Create Missing UI Prefabs`。UI 使用 Canvas、Image、TextMeshProUGUI、Button，以奶油白、草绿和暖沙色搭配，带圆角与柔和阴影。
4. 打开 `Assets/Scenes/MainMenu.unity`，进入 Play。主菜单包含“新游戏”“加载游戏”“设置”“退出”；没有有效存档时禁用加载按钮。设置弹窗可调整音量和全屏显示，单独保存并在下次启动恢复；退出在编辑器中停止 Play，在构建版本中关闭程序。MainMenuPanel、FarmHudPanel、InventoryPanel、SettingsPanel、ShopPanel、SleepPanel 均通过 AA 实例化，AA 地址严格等于预制体名字。
5. 编辑器使用 Addressables 的 **Use Asset Database (fastest)** Play Mode Script。
6. 打包前执行 `Magic Island > Build Prototype Addressables`，然后构建 Player。Build Settings 中启动场景为 MainMenu；MainGame 只通过 AA 加载，地址为 `Scenes/MainGame`。

可以直接 Play MainGame 调试农务，仍会由 GameManager 加载配置并初始化系统，但这条路径不验证 AA 场景加载。

## 操作

- ESC / HUD“暂停（ESC）”：继续游戏、设置和返回主菜单；返回会确认丢弃当天未睡觉进度。

- WASD：按画面方向移动。滚轮：镜头缩放。
- 1 / 2 / 3 或底部按钮：锄头 / 种子 / 水壶。
- 数字键 4／5 或工具栏斧头／镐子按钮：走近外围树木和石头，逐次点击采集。树木 3 次、每次 4 体力、产出 3 木材、3 天恢复；石头 2 次、每次 5 体力、产出 2 石料、2 天恢复。完整产出空间不足时不扣体力或资源进度。
- 数字键 6 或工具栏鱼竿按钮：走近东南角海岸钓点，点击水面抛竿。等待 2–5 秒，咬钩后 2 秒内点击收竿。成功消耗 5 体力、获得 1 条沙丁鱼或鲈鱼；提前、超时或取消不消耗。鱼可储物或在商店出售，价格 35／65，捕获和售鱼在睡觉时统一保存，日报可在睡觉失败后重试该次睡觉存档。
- M 或右上角地图按钮：查看岛屿、当前位置和地点用途；M / Esc 关闭。
- 数字键 7 / 底部“7 肥料”：走近已耕作农田点击施肥，消耗肥料 ×1、体力 2，每轮一次。工作台“农田用品”制作；默认优质概率从 10% 提高到 40%，芜菁 / 番茄 / 玉米优质售价 38 / 75 / 128。
- 成功操作显示工具动作及泥土／种子／水滴／收获特效，提示体力消耗或获得作物。动作默认 0.55 秒，期间暂停移动；打开弹窗会暂停动作，关闭后继续。
- B 或背包按钮：打开／收起背包；Esc 或关闭按钮也可收起。打开时暂停角色、镜头和工具输入。点击格子查看物品详情。
- 靠近农田东南侧带浅绿 / 奶油白条纹棚布的商店，鼠标点击柜台打开商店；购买种子、出售作物。Esc 或关闭按钮收起商店并恢复输入。
- 靠近农田西南侧的小屋，点击建筑进屋；室内点击床后确认睡觉，点击出口返回农场。HUD“回家休息”在门前进入小屋，室内“休息”需靠近床。睡觉推进作物生长、重置浇水和时间到 06:00，并自动保存室内位置和世界状态；取消或 Esc 继续当天。HUD 显示时间，默认现实 1 秒为游戏 1 分钟，到 24:00 停表并提示睡觉。
- 左键：操作高亮农田；按住左键拖过相邻格子可连续劳动。
- 黄色边框：操作范围内；红色边框：需要走近。
- 成熟作物在任何工具下优先收获。每次按住鼠标只能对每格成功操作一次，松开后可以再操作。
- “下一天”：保留的调试结算入口，与睡觉执行相同规则；当天浇过水的作物生长一阶段，达到对应品种的生长次数后成熟。未浇水不生长，浇水状态每天重置。
- 默认背包 10 格、初始 50 颗芜菁种子和 100 金币，每格最多 99 个。播种消耗种子，收获获得作物；背包满时保留成熟作物。商店种子购买价 10、芜菁出售价格 25，每次交易 1 个；价格可在 ItemCatalog 中调整。
- 默认体力 100；耕地消耗 3、播种 1、浇水 2、收获免费。HUD 显示体力条，低体力使用暖橙色，数值可在 GameConfig 中调整。
- 背包点击种子后选择“用于播种”，关闭背包并切换种子工具；HUD 显示当前种子。商店顶部切换芜菁、番茄、玉米：生长次数 3／5／7，种子价格 10／20／30，售价 25／50／85；参数来自 CropCatalog 和 ItemCatalog。
- HUD 显示年份、季节、日期和晴天／雨天；新游戏首日晴天，后续按新日期所属季节产生天气，默认春／夏／秋／冬降雨概率为 30%／20%／40%／15%。雨水免费浇灌新播种和已有作物，睡觉时结算成长。阳光、环境光和天空随时刻变化，弹窗暂停时钟和雨滴。
- 默认芜菁适宜春、秋，番茄适宜夏，玉米适宜夏、秋；冬季暂不能新种这三种作物。跨季不枯萎，仍可浇水和收获。背包可选择任意种子，实际播种检查当季限制。
- 作物按进度显示幼苗、成长植株或成熟果实。鼠标指向作物时，右侧显示名称、成长进度、浇水状态和操作提示；距离较远时仍可查看。
- 农田前方偏右设料理台：2 个芜菁制作 1 碗芜菁汤（恢复 30 体力），1 个玉米制作 1 份烤玉米（恢复 20 体力）。背包选中食物后点击“食用一个”，满体力不消耗，恢复不超过上限。
- 料理面板可向下滚动查看鱼类食谱：沙丁鱼 ×1 制作烤沙丁鱼，恢复 35 体力；鲈鱼、芜菁、番茄各 ×1 制作鲈鱼蔬菜汤，恢复 50 体力。两道默认开放，成品可作为普通料理送给林舟增加 10 好感度；规则见 [鱼类料理](Docs/FishCooking.md)。
- 鸡舍收集的鸡蛋可制作煎蛋（鸡蛋 ×1，恢复 25 体力）或蔬菜蛋汤（鸡蛋、芜菁各 ×1，恢复 45 体力）。两道默认开放，成品可食用或作为普通料理送礼；料理面板共七张可滚动卡片，规则见 [鸡蛋料理](Docs/EggCooking.md)。
- 建成鸡舍后的次日起，公告栏每日订单池加入鸡蛋 ×2（70 金币）、煎蛋 ×1（50 金币）、蔬菜蛋汤 ×1（90 金币）。建造当天及旧档当天订单保留，读档不重新抽单；规则见 [养殖与料理订单](Docs/AnimalOrders.md)。
- 工作台第三页“养殖用品”可用玉米 ×1 制作饲料 ×10。用品进入背包，可存箱和喂鸡；材料不足或放不下全部十份时不扣玉米，成功后记录，睡觉时保存。沿用 CraftingManager、AA 配置和 MVC，见 [饲料制作](Docs/FeedCrafting.md)。
- 鸡舍显示“小白”和“团团”的独立状态卡：每日摸摸 +5，首次成功喂食 +2，亲密度最高 100，30 为熟悉、70 为亲近。互动显示图形爱心，读档保留每只鸡的当日限制，旧档不补发亲密度；见 [鸡的互动与亲密度](Docs/AnimalFriendship.md)。
- 已喂食且亲密度达到 70 的鸡次日有 25% 概率产优质鸡蛋，每只仍产一枚。普通 / 优质蛋分别收购 25 / 40 金币，可混用制作和交付，优先消耗普通蛋。鸡舍分别记录待收数，原子收取；品质结算固定保存，读档不重抽。见 [优质鸡蛋](Docs/QualityEggs.md)。
- 小屋门口左侧出货箱可放入 / 取回农作物、鱼和两种鸡蛋，显示预计收入；睡觉后统一售卖，日报列出数量、单价和收入。成功存取只更新运行状态，睡觉时保存，结算保存失败可重试且不重复发钱，见 [出货箱](Docs/Shipping.md)。
- 每日经营由 DailyReportManager 统计成功的金币收支和农田收获。睡觉后 DailyReportPanel 展示上一天收支、净变化、收获，以及本次新成熟作物 / 两种蛋产量；确认只关闭日报，加载直接进入保存的清晨。面板通过 AA 预加载、MVC 和 PopUp 层管理，见 [每日经营结算](Docs/DailyReports.md)。
- 新游戏背包默认 10 格，商店“背包扩容”可依次支付 300 / 800 金币升级为 20 / 30 格，每次增加 10 格。InventoryManager 管理即时扩容，保留物品、顺序和系统引用，费用计入日报；旧档保留实际基础容量，规则见 [背包扩容](Docs/InventoryUpgrades.md)。
- 办事处旁的岛务员林舟可走近点击交谈，支持逐句推进和 ESC 退出。完整结束首次对话后记录已见面，后续台词包含当前季节和天气，对话期间暂停场景操作和时间。
- 完成首次交谈后，对话面板可打开“送礼”：林舟每日收一份礼物，芜菁汤 +20，其他料理 +10，作物 +5。好感度最高 100，30 达到熟悉、70 达到友好并改变招呼；种子与材料不可送，满好感度不消耗礼物，读档保留当天限制。
- 林舟好感度 30 时可从对话按钮听“小岛往事”；70 时完成“朋友的食谱”解锁蔬菜浓汤，需先听完往事。两段事件中途退出不完成，奖励不能重复领取。浓汤消耗芜菁、番茄、玉米各 1，恢复 50 体力，料理面板显示锁定条件。
- 农田前方有固定储物箱，默认 18 格。点击左右两侧的格子，存入或取出一个 / 整组物品，按堆叠上限合并，成功存取只更新运行状态，睡觉时保存。箱内物品需先取回背包再使用。
- 办事处前方空地有工具工作台，走近点击升级：斧头消耗木材 6、石料 2、金币 50；镐子消耗木材 3、石料 4、金币 75。每把可强化一次，树木采集缩短至两次、岩石一次，睡觉时保存。
- 农场右后方的岛屿办事处门前右侧设公告栏，走近点击查看每日收购订单。一次交付 1–3 个作物，芜菁／番茄／玉米每个奖励 40／75／130 金币；交付后记录，睡觉时保存，睡觉后刷新，冬季收购库存作物。
- 右侧 TaskPanel 显示“耕地 → 播种 → 浇水 → 睡觉 → 收获 → 出售”新手目标，可收起。完成后点击领取金币，按顺序开放下一步；提前完成的成功操作仍被记录，全部奖励共 85 金币，领奖后记录，睡觉时保存。

## 结构

- `Managers/GameManager`：整个游戏流程入口，统一调度配置、场景、系统初始化、UI 与返回菜单。
- `Managers`：配置、场景、UI、背包、农务、玩家、镜头、交互均由对应的 Manager 控制。
- `Managers/InventoryManager`、`Inventory/InventoryState`：真实物品数量、堆叠、容量检查和变化事件，先于农田初始化。
- `UI/Models`、`UI/Controllers`：MVC 数据和控制器；MainMenuView / PrototypeHud 只负责显示。
- `Configuration/GameConfig`：通过 AA 加载的运行配置，默认农田 5×5、50 颗种子、3 天生长。
- `Configuration/ItemCatalog`：AA 物品配置，定义物品 ID、名称、类型、图标、堆叠上限和描述。
- `Configuration/WeatherConfig`、`Managers/WeatherManager`：AA 天气参数、每日晴雨、昼夜光照、雨滴表现及雨水浇灌。
- `Configuration/SeasonConfig`、`Managers/CalendarManager`：AA 季节长度和降雨概率，从存档总天数推导日期及检查播种季节。
- `Configuration/GatheringCatalog`、`Managers/GatheringManager`、`Gathering/GatheringState`：树木和石头节点、AA 模型、采集次数、材料和体力、按天刷新与存档，工具栏及背包沿用 MVC。
- `Configuration/OrderCatalog`、`Managers/OrderManager`、`Orders/OrderState`：每日订单生成、公告栏建筑 AA 实例、原子交付、奖励和存档，OrderPanel 通过 MVC 显示。
- `Configuration/TaskCatalog`、`Managers/TaskManager`、`Tasks/TaskState`：AA 新手任务、成功行为进度、顺序领奖和存档，TaskPanel 通过 MVC 显示。
- `Configuration/CropCatalog`：AA 作物配置，定义种子／收获物映射、生长次数和成熟占位表现。
- `Prototype/FarmPrototype`：由 GameManager 调用创建占位世界对象，随后由各 Manager 初始化，不自行启动或加载 UI。
- `Managers/PlayerManager`、`Player/PlayerMovement`、`Player/PlayerAnimation`：AA 角色加载 / 释放，CharacterController 移动、转向、真实速度与动画，工具动作由 ToolManager 按进度采样。
- `Camera/CameraController`：42° 正交镜头，跟随和缩放。
- `Farming/FarmState`：纯 C# 农田规则，与表现层独立。
- `Farming/FarmGrid`：网格坐标、鼠标平面选格、作物显示与高亮。
- `Interaction`：工具选择、范围检查、点击节流、UI 点击拦截及 HUD View。
- `Editor/PrototypeAddressablesSetup`：自动注册 AA 场景及内容构建菜单。
- `Editor/PrototypeUiPrefabBuilder`：创建缺失的 UI 预制体并序列化组件引用，保留已经存在的预制体和手动编辑。

`Magic Island > Validate UI Prefabs` 检查 Canvas 组件、UI 引用和 AA 地址。UI 目录下的预制体注册时均以文件名（不含扩展名）为地址。

主菜单背景位于 `Assets/Art/Textures/UI/MainMenuBackground.png`，由主菜单 Image 引用，随 AA 预制体一起打包加载。AspectRatioFitter 按比例铺满画布，避免拉伸。生成说明和提示词见 `Docs/MainMenuArtwork.md`。

`Magic Island > Render Main Menu Preview` 在独立预览场景中渲染实际预制体，保存至 `Logs/MainMenuPreview.png`。`Apply Cozy UI Theme` 可重新生成当前主题的两个 UI 预制体，会覆盖手动布局修改；普通配置和创建缺失预制体的菜单会保留已有修改。

岛屿地面与海岸由 AA 预制体提供，农务工具占位表现在运行时生成，玩家角色预制体及基础动画通过 AA 加载，作物的九个阶段预制体、办事处公告栏建筑、采集树木／石头、岛务员角色、鱼竿／海岸钓点和三种农场装饰已通过 AA 管理，可替换正式美术。当前已实现风格化海洋、镜头遮挡淡化和角色基础动画，暂不包含自动寻路及正式角色美术。工具和反馈说明见 `Docs/ToolsAndFeedback.md`，作物模型及信息面板见 `Docs/CropVisuals.md`。

规则检查见 `Tools/VerifyFarmState.cs`，覆盖整片农田、重复操作、缺水、生长、收获及种子耗尽。

背包说明见 `Docs/Inventory.md`；`Tools/VerifyInventory.cs` 验证跨堆叠、失败操作不丢物品、数量守恒及满背包保护。

背包右上角支持“一键整理”：合并相同 ID 的零散堆叠，按类型排列并将空格移至末尾，普通鸡蛋与优质鸡蛋分别保留。整理清空选中项，结果在睡觉时统一保存；由 GameManager 调度 InventoryManager，复用 AA / MVC InventoryPanel，默认十格和每次升级增加十格的规则不变。检查与完整规则见 [背包整理](Docs/Inventory.md#一键整理)。

背包内部支持左键拖拽整组移动、同种物品合并和不同物品交换，右键将一半数量拆到第一个空格。拖拽显示跟随预览，取消保留原状；成功后清空选择，结果在睡觉时统一保存。原子数量规则、输入保护及 AA / MVC 实现见 [拖拽与拆分](Docs/Inventory.md#拖拽与拆分)。

背包固定三排、每排十格，初始只解锁第一排，两次升级依次解锁第二、第三排。未解锁格子显示锁图标并禁用操作；超过三十格的旧档完整保留库存，使用翻页访问，每页仍为三排十格。布局及兼容规则见 [三排与锁定格子](Docs/Inventory.md#三排与锁定格子)。

储物箱支持跨库存拖拽、Shift 点击快速存取和“存入同类”，容量不足时保留剩余物品；两边在运行状态中一起提交，睡觉时统一保存。StoragePanel 上方背包显示三排十格及锁定状态，旧档通过翻页保留完整库存。详见 [快捷存取](Docs/Storage.md#快捷存取)。

商店说明见 `Docs/Shop.md`，多作物见 `Docs/Crops.md`，时间和睡觉见 `Docs/TimeAndSleep.md`，体力见 `Docs/Stamina.md`，昼夜与天气见 `Docs/Weather.md`，日期和季节见 `Docs/CalendarAndSeasons.md`，任务见 `Docs/Tasks.md`，公告栏订单见 `Docs/Orders.md`，采集见 `Docs/Gathering.md`，烹饪与食物见 `Docs/CookingAndFood.md`，NPC 与对话见 `Docs/NPCAndDialogue.md`，好感度与送礼见 `Docs/Relationships.md`，好感度事件见 `Docs/StoryEvents.md`，玩家小屋见 `Docs/Housing.md`，室内家具见 `Docs/Furniture.md`，扩建见 `Docs/HouseUpgrades.md`，养殖见 `Docs/Animals.md`。ShopPanel、SleepPanel、OrderPanel、ToolUpgradePanel、StoragePanel、CookingPanel、DialoguePanel、GiftPanel、FishingPanel、CraftingPanel、HouseUpgradePanel、CoopPanel、AnimalShopPanel、DailyReportPanel、InventoryUpgradePanel、ShippingPanel、PausePanel、MapPanel 均由 AA 加载到 PopUp 层，TaskPanel、PlacementPanel、InteractionPromptPanel 位于 Common 层，SceneTransitionPanel 位于 Top 层；共 28 个 UI 预制体，地址均为预制体名字。存档版本为 29，兼容迁移版本 1–28。

完整流程和 MVC 约定见 `Docs/GameArchitecture.md`。`SaveManager` 保存日期、农田、背包及角色位置，只在每天睡觉结算后保存；“加载游戏”恢复进度。存档路径、备份与版本规则见 `Docs/Saving.md`。`SettingsManager` 单独保存并恢复音量和全屏选项，设置面板遵循 MVC 并挂在 PopUp 层。

海滩支持拾取扇贝壳、漂流木和海玻璃，初始十个，睡觉跨日补充最多五个，总量最多十六个；未拾取物品保留，满背包不消失，可在商店出售，仍只在睡觉时保存。由 GatheringManager 管理，预制体通过同名 AA 地址加载，详见 [海滩拾取](Docs/BeachForaging.md)。

天空新增由 WeatherManager 管理的 IslandClouds AA 预制体，云朵缓慢漂移，晴天地面偶尔出现柔和云影；暂停冻结，室内隐藏，雨天和夜晚关闭独立云影，沿用当前昼夜亮度。详见 [云层与云影](Docs/Clouds.md)。

海洋采用较窄的不规则浅滩、较深珊瑚礁带和外海三层水深，48 处浅水小珊瑚花园与 36 处大型珊瑚群落使用柔和配色。鱼群保留合批渲染，每条鱼拥有独立跟随、转向和摆尾，鳐鱼缓慢摆鳍；沿用 OceanManager、AA 加载和暂停生命周期。详见 [海洋生态](Docs/Ocean.md)。

鱼群在各自固定的栖息范围内自由巡游、觅食和探索，按种类选择珊瑚或沙底，逐鱼绕开珊瑚和礁石。各类鱼通过 OceanSurface.FishPopulations 设置数量上下限，生成时独立随机抽取；调整范围后使用 Rebuild Ocean Geography And Ecology 重建，详见 [数量配置与避障](Docs/Ocean.md#数量配置觅食与避障)。
