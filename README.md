# MagicIsland3D 农务原型

Unity 2022.3.62f2，内置渲染管线，旧版 Input，Addressables 1.21.17。

## 开发约束

以下约定适用于后续新增功能和现有代码修改。

### 游戏流程与系统管理

- 整个游戏流程必须由 `GameManager` 统一控制，包括启动、配置加载、场景切换、进入游戏初始化和返回主菜单。
- 每个系统必须由对应的 `Manager` 控制。现有配置、场景、UI、背包、农务、玩家、镜头、交互、存档、设置、经济、商店、时间、日期季节、天气、体力、工具、任务、订单、采集、工具升级和储物分别由对应 Manager 管理；新增其他系统时沿用这一规则。
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

- 时钟由 `TimeManager` 控制；`CalendarManager` 根据农场总天数计算年份、季节和日期，`GameManager` 统一调度跨日。季节配置使用 AA 地址 `SeasonConfig`，默认每季 28 天。背包、商店、睡觉、订单、工具升级和储物弹窗打开及加载时暂停计时，睡觉结算后自动保存。
- 昼夜与晴雨由 `WeatherManager` 控制，天气参数通过 AA 地址 `WeatherConfig` 加载。雨天自动浇灌已种植的露天农田，跨日只成长一次；当天天气和随机种子随进度保存，读档不重新抽取。
- 体力由 `StaminaManager` 控制；农务操作成功才消耗，失败不扣除，睡觉回满。体力不足时仍可移动、交易和休息。
- 工具选择、动作锁、手持表现和操作反馈由 `ToolManager` 控制。动作先结算一次再播放，动画期间禁止重复操作和工具切换；提示使用 Top 层的 `FeedbackPanel`，通过 AA / MVC 管理且不拦截鼠标。
- 作物配置 `CropCatalog` 通过 AA 加载，FarmManager 管理种子选择与各地块独立生长，ShopManager 管理不同作物的买卖。收获物品按地块作物类型发放，不能由当前种子选择决定。
- `CropCatalog` 配置适宜播种季节，`FarmManager` 在实际播种时检查；失败不扣种子和体力。商店全年出售种子，背包和商店显示季节说明，已播种作物跨季继续成长和收获。
- 作物幼苗、生长中、成熟模型存放在 `Assets/Art/Prefabs/Crops`，在 CropCatalog 中配置阶段 AA 地址，由 FarmManager 统一加载和释放。阶段从已有生长进度推导，同阶段不重复创建模型。`CropInfoPanel` 使用 Common 层、AA 同名地址和 MVC 显示指向地块的信息，不拦截鼠标。
- 商店与金币分别由 `ShopManager`、`EconomyManager` 控制，交易必须先检查资金、物品和背包容量，失败不改变双方状态；金币随进度保存。
- 游戏以休闲经营成长为主，采用类似星露谷和动森的 3D 斜俯视镜头。
- 玩家使用 WASD 按画面方向移动，使用鼠标点击目标进行操作；工具操作需检查距离，UI 点击需拦截，避免触发场景交互。

## 运行

1. 打开工程，等待 Package Manager 安装 Addressables 和脚本编译完成。
2. 编辑器自动创建 GameConfig、ItemCatalog、CropCatalog、WeatherConfig、SeasonConfig、TaskCatalog、OrderCatalog、GatheringCatalog、ToolUpgradeCatalog、StorageConfig 并注册配置、MainGame 和 UI；也可以执行 `Magic Island > Configure Prototype Addressables`。
3. UI 预制体位于 `Assets/Art/Prefabs/UI`，自动创建缺失文件，也可以执行 `Magic Island > Create Missing UI Prefabs`。UI 使用 Canvas、Image、TextMeshProUGUI、Button，以奶油白、草绿和暖沙色搭配，带圆角与柔和阴影。
4. 打开 `Assets/Scenes/MainMenu.unity`，进入 Play。主菜单包含“新游戏”“加载游戏”“设置”“退出”；没有有效存档时禁用加载按钮。设置弹窗可调整本次运行的音量和全屏显示；退出在编辑器中停止 Play，在构建版本中关闭程序。MainMenuPanel、FarmHudPanel、InventoryPanel、SettingsPanel、ShopPanel、SleepPanel 均通过 AA 实例化，AA 地址严格等于预制体名字。
5. 编辑器使用 Addressables 的 **Use Asset Database (fastest)** Play Mode Script。
6. 打包前执行 `Magic Island > Build Prototype Addressables`，然后构建 Player。Build Settings 中启动场景为 MainMenu；MainGame 只通过 AA 加载，地址为 `Scenes/MainGame`。

可以直接 Play MainGame 调试农务，仍会由 GameManager 加载配置并初始化系统，但这条路径不验证 AA 场景加载。

## 操作

- WASD：按画面方向移动。滚轮：镜头缩放。
- 1 / 2 / 3 或底部按钮：锄头 / 种子 / 水壶。
- 数字键 4／5 或工具栏斧头／镐子按钮：走近外围树木和石头，逐次点击采集。树木 3 次、每次 4 体力、产出 3 木材、3 天恢复；石头 2 次、每次 5 体力、产出 2 石料、2 天恢复。完整产出空间不足时不扣体力或资源进度。
- 成功操作显示工具动作及泥土／种子／水滴／收获特效，提示体力消耗或获得作物。动作默认 0.55 秒，期间暂停移动；打开弹窗会暂停动作，关闭后继续。
- B 或背包按钮：打开／收起背包；Esc 或关闭按钮也可收起。打开时暂停角色、镜头和工具输入。点击格子查看物品详情。
- 靠近农田东南侧带黄色棚顶的商店，鼠标点击柜台或棚顶打开商店；购买种子、出售作物。Esc 或关闭按钮收起商店并恢复输入。
- 靠近农田西南侧蓝色被子的床，鼠标点击后确认睡觉；取消或 Esc 继续当天。睡觉推进作物生长、重置浇水和时间到 06:00，并自动保存。HUD 显示时间，默认现实 1 秒为游戏 1 分钟，到 24:00 停表并提示睡觉。
- 左键：操作高亮农田；按住左键拖过相邻格子可连续劳动。
- 黄色边框：操作范围内；红色边框：需要走近。
- 成熟作物在任何工具下优先收获。每次按住鼠标只能对每格成功操作一次，松开后可以再操作。
- “下一天”：保留的调试结算入口，与睡觉执行相同规则；当天浇过水的作物生长一阶段，达到对应品种的生长次数后成熟。未浇水不生长，浇水状态每天重置。
- 默认背包 12 格、初始 50 颗芜菁种子和 100 金币，每格最多 99 个。播种消耗种子，收获获得作物；背包满时保留成熟作物。商店种子购买价 10、芜菁出售价格 25，每次交易 1 个；价格可在 ItemCatalog 中调整。
- 默认体力 100；耕地消耗 3、播种 1、浇水 2、收获免费。HUD 显示体力条，低体力使用暖橙色，数值可在 GameConfig 中调整。
- 背包点击种子后选择“用于播种”，关闭背包并切换种子工具；HUD 显示当前种子。商店顶部切换芜菁、番茄、玉米：生长次数 3／5／7，种子价格 10／20／30，售价 25／50／85；参数来自 CropCatalog 和 ItemCatalog。
- HUD 显示年份、季节、日期和晴天／雨天；新游戏首日晴天，后续按新日期所属季节产生天气，默认春／夏／秋／冬降雨概率为 30%／20%／40%／15%。雨水免费浇灌新播种和已有作物，睡觉时结算成长。阳光、环境光和天空随时刻变化，弹窗暂停时钟和雨滴。
- 默认芜菁适宜春、秋，番茄适宜夏，玉米适宜夏、秋；冬季暂不能新种这三种作物。跨季不枯萎，仍可浇水和收获。背包可选择任意种子，实际播种检查当季限制。
- 作物按进度显示幼苗、成长植株或成熟果实。鼠标指向作物时，右侧显示名称、成长进度、浇水状态和操作提示；距离较远时仍可查看。
- 农田前方有固定储物箱，默认 18 格。点击左右两侧的格子，存入或取出一个 / 整组物品，按堆叠上限合并，成功存取自动保存。箱内物品需先取回背包再使用。
- 办事处前方空地有工具工作台，走近点击升级：斧头消耗木材 6、石料 2、金币 50；镐子消耗木材 3、石料 4、金币 75。每把可强化一次，树木采集缩短至两次、岩石一次，自动保存。
- 农场右后方的岛屿办事处门前右侧设公告栏，走近点击查看每日收购订单。一次交付 1–3 个作物，芜菁／番茄／玉米每个奖励 40／75／130 金币；交付后保存，睡觉后刷新，冬季收购库存作物。
- 右侧 TaskPanel 显示“耕地 → 播种 → 浇水 → 睡觉 → 收获 → 出售”新手目标，可收起。完成后点击领取金币，按顺序开放下一步；提前完成的成功操作仍被记录，全部奖励共 85 金币，领奖后自动保存。

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
- `Player/PlayerMovement`：CharacterController 移动、转向和操作时的短暂停顿。
- `Camera/CameraController`：42° 正交镜头，跟随和缩放。
- `Farming/FarmState`：纯 C# 农田规则，与表现层独立。
- `Farming/FarmGrid`：网格坐标、鼠标平面选格、作物显示与高亮。
- `Interaction`：工具选择、范围检查、点击节流、UI 点击拦截及 HUD View。
- `Editor/PrototypeAddressablesSetup`：自动注册 AA 场景及内容构建菜单。
- `Editor/PrototypeUiPrefabBuilder`：创建缺失的 UI 预制体并序列化组件引用，保留已经存在的预制体和手动编辑。

`Magic Island > Validate UI Prefabs` 检查 Canvas 组件、UI 引用和 AA 地址。UI 目录下的预制体注册时均以文件名（不含扩展名）为地址。

主菜单背景位于 `Assets/Art/Textures/UI/MainMenuBackground.png`，由主菜单 Image 引用，随 AA 预制体一起打包加载。AspectRatioFitter 按比例铺满画布，避免拉伸。生成说明和提示词见 `Docs/MainMenuArtwork.md`。

`Magic Island > Render Main Menu Preview` 在独立预览场景中渲染实际预制体，保存至 `Logs/MainMenuPreview.png`。`Apply Cozy UI Theme` 可重新生成当前主题的两个 UI 预制体，会覆盖手动布局修改；普通配置和创建缺失预制体的菜单会保留已有修改。

地面、角色和工具占位表现在运行时生成，作物的九个阶段预制体、办事处公告栏建筑及采集树木／石头已通过 AA 管理，可替换正式美术。当前不包含自动寻路、正式动画或遮挡淡化。工具和反馈说明见 `Docs/ToolsAndFeedback.md`，作物模型及信息面板见 `Docs/CropVisuals.md`。

规则检查见 `Tools/VerifyFarmState.cs`，覆盖整片农田、重复操作、缺水、生长、收获及种子耗尽。

背包说明见 `Docs/Inventory.md`；`Tools/VerifyInventory.cs` 验证跨堆叠、失败操作不丢物品、数量守恒及满背包保护。

商店说明见 `Docs/Shop.md`，多作物见 `Docs/Crops.md`，时间和睡觉见 `Docs/TimeAndSleep.md`，体力见 `Docs/Stamina.md`，昼夜与天气见 `Docs/Weather.md`，日期和季节见 `Docs/CalendarAndSeasons.md`，任务见 `Docs/Tasks.md`，公告栏订单见 `Docs/Orders.md`，采集见 `Docs/Gathering.md`。ShopPanel、SleepPanel、OrderPanel、ToolUpgradePanel、StoragePanel 均由 AA 加载到 PopUp 层，TaskPanel 位于 Common 层，地址均为预制体名字；存档版本为 11，兼容迁移版本 1–10。

完整流程和 MVC 约定见 `Docs/GameArchitecture.md`。`SaveManager` 保存日期、农田、背包及角色位置，推进日期和返回主菜单时自动保存；“加载游戏”恢复进度。存档路径、备份与版本规则见 `Docs/Saving.md`。`SettingsManager` 控制本次运行的音量和全屏选项，设置面板遵循 MVC 并挂在 PopUp 层。
