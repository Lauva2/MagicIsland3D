# 游戏流程与系统框架

GameManager 统一调度流程，各系统由对应的 Manager 控制。UIManager 统一持有 UI 的 AA 实例句柄，UI 内部采用 MVC。

## 生命周期

`LoadingConfiguration → MainMenu → LoadingScene → InitializingGame → Playing → ReturningToMenu → MainMenu`

1. GameManager 是跨场景单例，创建各系统 Manager。
2. ConfigurationManager 初始化 AA，然后加载并校验 `GameConfig`、`ItemCatalog`、`CropCatalog`、`WeatherConfig`、`SeasonConfig`、`TaskCatalog`、`OrderCatalog`、`GatheringCatalog`、`ToolUpgradeCatalog` 和 `StorageConfig`。
3. SaveManager 检查本地存档；UIManager 按名字加载 `MainMenuPanel`，创建 MainMenuModel / MainMenuController，并根据存档状态启用“加载游戏”。
4. 点击“新游戏”使用初始状态；点击“加载游戏”先从磁盘读取并校验数据。GameManager 锁定流程，SceneManager 通过 AA 以 Additive 模式加载 GameConfig 指定的场景。
5. GameManager 先等待 FarmManager 按 CropCatalog 加载全部阶段模型，再初始化 InventoryManager、EconomyManager、StaminaManager；FarmPrototype 创建占位世界对象、柜台及床；随后初始化 TimeManager、FarmManager、PlayerManager，按需恢复存档，再初始化 CalendarManager、CameraManager、WeatherManager、GatheringManager、ToolManager、InteractionManager、ShopManager。GatheringManager 恢复采集进度和冷却，ToolManager 管理共用农务／采集动作锁。FarmGrid 根据已恢复的进度显示对应模型；CalendarManager 从总天数推导日期，WeatherManager 恢复当天晴雨并根据已恢复的时刻设置光照，下一天使用新日期所属季节的降雨概率。
6. GameManager 初始化 TaskManager 和 OrderManager，恢复任务及当日订单，等待 AA 加载 `OrderHall` 并绑定门前公告栏，再等待 GatheringManager 加载模型和创建资源节点。随后 UIManager 按名字加载 `FarmHudPanel`、`InventoryPanel`、`CropInfoPanel`、`FeedbackPanel`、`TaskPanel`，创建对应 Model / Controller，背包默认收起，作物信息和任务在 Common 层，反馈挂在 Top 层。
7. 完成以上步骤后隐藏主菜单、启用玩家及交互输入，进入 Playing。点击公告栏由 GameManager 调度 UIManager 加载 PopUp 层 OrderPanel。
8. 推进日期时自动保存；返回时先保存，成功后禁用输入、卸载游戏场景、释放 HUD 和 Controller 的事件绑定、清除场景引用，再显示保留的主菜单。保存失败则保留游戏并提示重试。

主菜单启动场景保留用于返回，只有游戏场景通过 AA 加载和卸载。直接 Play MainGame 时也会自动创建 GameManager，走相同的配置和系统初始化流程；编辑器直接打开的场景按编辑器调试路径卸载。

## Managers

| Manager | 职责 |
| --- | --- |
| GameManager | 启动、新游戏、继续游戏、自动保存调度、返回菜单、流程状态、初始化顺序、重复请求拦截 |
| SaveManager | 存档版本、校验、JSON 保存与读取、原子替换和备份恢复 |
| SettingsManager | 本次运行的主音量和全屏参数，SettingsPanel 通过 MVC 调用 |
| EconomyManager | 金币数量、收入、扣款和恢复 |
| ShopManager | 商品价格、交易校验、柜台距离和买卖 |
| TimeManager | 时钟推进、床引用和距离检查；日期统一由 GameManager 调度 FarmManager 推进 |
| CalendarManager | 从农场总天数推导年份、季节和日期，提供播种季节检查及目标日期降雨概率 |
| TaskManager | 新手目标、成功操作进度、顺序领奖、奖励反馈和事件清理 |
| GatheringManager | 采集配置、AA 模型和节点实例、次数与材料产出、体力协调、恢复日期和清理 |
| OrderManager | 当日订单、办事处建筑 AA 实例和公告栏、交付校验、奖励反馈、次日刷新及清理 |
| WeatherManager | 当天晴雨、天气序列、昼夜光照、雨滴暂停／恢复及清理、雨水浇灌 |
| StaminaManager | 体力初始化、恢复、睡觉回满和清理；农务成功消耗由 FarmActions 协调 |
| ToolManager | 工具选择、动作锁、农务操作入口、手持模型、动作与特效和操作反馈事件 |
| ConfigurationManager | 初始化 AA、加载并校验配置、持有配置句柄 |
| SceneManager | AA 场景加载和卸载、活动场景和句柄管理 |
| UIManager | 按预制体名字实例化 UI、创建和销毁 Controller、显示隐藏、资源释放 |
| InventoryManager | 初始物品、数量查询、添加／消耗、堆叠与容量检查 |
| FarmManager | 阶段模型 AA 加载和释放、农田状态初始化、农务动作、跨日推进 |
| PlayerManager | 玩家初始化、移动参数、输入启停 |
| CameraManager | 镜头初始化、视角和缩放范围、控制启停 |
| InteractionManager | 操作距离、工具选择、交互控制启停 |

将来增加音频等系统时，由对应的 Manager 控制，并接入 GameManager 所需的初始化阶段。当前 GameManager 不提前创建尚未实现的系统。

## UI MVC

- Model：`UI/Models`，保存界面需要的数据，不包含 UI 组件。
- View：MainMenuView、PrototypeHud 继承 UIView，保存 TMP_Text / Button 等序列化引用和 Layer 配置，负责渲染，不加载资源或调用游戏逻辑。UIManager 按 Bottom、Common、PopUp、Top 自动挂载并排序。
- Controller：`UI/Controllers`，绑定按钮事件、更新 Model、驱动 View、调用对应的 Manager；释放时解除自己绑定的事件。
- UIManager：UI 的统一入口和资源拥有者，通过 `Addressables.InstantiateAsync` 加载，通过 `ReleaseInstance` 释放；UI 预制体都在 `Assets/Art/Prefabs/UI`。

事件示例：`NextDay Button → FarmHudController → GameManager.AdvanceDay → FarmManager → FarmState → HUD Model/View`。

跨日时先补当天雨水，再结算农田、推进时钟与天气、应用次日雨水、回满体力、记录睡觉任务、刷新订单和资源节点并保存。天气和时刻通过 FarmHudController 更新 HUD，雨滴及光照由 WeatherManager 管理。订单交付在 GameManager 检查游戏状态、距离和面板后，由 OrderManager 提交，再立即保存。

## 配置

`Assets/Config/GameConfig.asset` 通过 AA 地址 `GameConfig` 加载，控制场景地址、农田尺寸、初始种子及金币、生长天数、时间流速、玩家速度、交互距离、镜头俯角和镜头大小。

编辑器配置工具只创建缺失配置，不覆盖已经调整的参数。配置不合法会停止启动并报告错误；启动失败后可调用 GameManager.RetryStartup。游戏场景或 HUD 初始化失败时返回菜单，允许重试。

## 验证

- `Tools/VerifyFarmState.cs`：509 项农务断言，包括配置参数生效。
- `GameFlowVerification.Run`：在独立验证副本中构建 AA，使用 Packed Play Mode 验证两轮进入/返回、单例、重复请求、MVC 按钮事件、日期推进、状态重置及 UI / 场景释放。
- `Magic Island > Validate UI Prefabs`：检查预制体组件和地址与名字一致。

工具升级由 ToolUpgradeManager 控制。ConfigurationManager 增加第九份 AA 配置 ToolUpgradeCatalog；世界初始化先恢复工具等级，再初始化 GatheringManager，加载办事处后加载 ToolWorkbench。升级面板通过 UIManager 加载，退出清理实例和句柄。详见 [工具升级](ToolUpgrades.md)。

储物由 StorageManager 控制。ConfigurationManager 增加第十份 AA 配置 StorageConfig；世界初始化恢复独立箱内库存，加载 StorageChest 后启用输入。StoragePanel 通过 UIManager 加载到 PopUp 层，存取后同时保存背包和箱子，退出清理实例与句柄。详见 [储物箱](Storage.md)。
