# MagicIsland3D 农务原型

Unity 2022.3.62f2，内置渲染管线，旧版 Input，Addressables 1.21.17。

## 开发约束

以下约定适用于后续新增功能和现有代码修改。

### 游戏流程与系统管理

- 整个游戏流程必须由 `GameManager` 统一控制，包括启动、配置加载、场景切换、进入游戏初始化和返回主菜单。
- 每个系统必须由对应的 `Manager` 控制。现有配置、场景、UI、背包、农务、玩家、镜头、交互、存档、设置分别由 `ConfigurationManager`、`SceneManager`、`UIManager`、`InventoryManager`、`FarmManager`、`PlayerManager`、`CameraManager`、`InteractionManager`、`SaveManager`、`SettingsManager` 管理；新增经济、任务等系统时沿用这一规则。
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
- 所有 UI 必须创建为预制体，统一存放在 `Assets/Art/Prefabs/UI`，使用对应的 Unity UI 组件。
- 所有 UI 预制体必须通过 `UIManager` 使用 AA 加载，AA 地址必须与预制体名字完全一致，不包含扩展名或路径前缀。例如 `FarmHUD.prefab` 的地址为 `FarmHUD`。
- UI 框架统一使用 MVC：Model 保存界面数据；View 保存组件引用并负责显示；Controller 绑定交互事件、更新 Model、驱动 View，并调用对应 Manager。
- View 不承担资源加载、场景切换或游戏规则；Controller 销毁时解除自身绑定的事件；UIManager 负责界面的加载、显示、隐藏、关闭和资源释放。
- UI 初期以纯色组件建立布局，配色应适配休闲经营游戏的风格。当前采用奶油白、草绿、暖沙色及圆角、柔和阴影；主菜单使用背景图片。

### 玩法与操作方向

- 游戏以休闲经营成长为主，采用类似星露谷和动森的 3D 斜俯视镜头。
- 玩家使用 WASD 按画面方向移动，使用鼠标点击目标进行操作；工具操作需检查距离，UI 点击需拦截，避免触发场景交互。

## 运行

1. 打开工程，等待 Package Manager 安装 Addressables 和脚本编译完成。
2. 编辑器自动创建 GameConfig、ItemCatalog 并注册配置、MainGame 和 UI；也可以执行 `Magic Island > Configure Prototype Addressables`。
3. UI 预制体位于 `Assets/Art/Prefabs/UI`，自动创建缺失文件，也可以执行 `Magic Island > Create Missing UI Prefabs`。UI 使用 Canvas、Image、TextMeshProUGUI、Button，以奶油白、草绿和暖沙色搭配，带圆角与柔和阴影。
4. 打开 `Assets/Scenes/MainMenu.unity`，进入 Play。主菜单包含“新游戏”“加载游戏”“设置”“退出”；没有有效存档时禁用加载按钮。设置弹窗可调整本次运行的音量和全屏显示；退出在编辑器中停止 Play，在构建版本中关闭程序。MainMenuUI、FarmHUD、InventoryUI、SettingsUI 均通过 AA 实例化，AA 地址严格等于预制体名字。
5. 编辑器使用 Addressables 的 **Use Asset Database (fastest)** Play Mode Script。
6. 打包前执行 `Magic Island > Build Prototype Addressables`，然后构建 Player。Build Settings 中启动场景为 MainMenu；MainGame 只通过 AA 加载，地址为 `Scenes/MainGame`。

可以直接 Play MainGame 调试农务，仍会由 GameManager 加载配置并初始化系统，但这条路径不验证 AA 场景加载。

## 操作

- WASD：按画面方向移动。滚轮：镜头缩放。
- 1 / 2 / 3 或底部按钮：锄头 / 种子 / 水壶。
- B 或背包按钮：打开／收起背包；Esc 或关闭按钮也可收起。打开时暂停角色、镜头和工具输入。点击格子查看物品详情。
- 左键：操作高亮农田；按住左键拖过相邻格子可连续劳动。
- 黄色边框：操作范围内；红色边框：需要走近。
- 成熟作物在任何工具下优先收获。每次按住鼠标只能对每格成功操作一次，松开后可以再操作。
- “下一天”：推进日期；当天浇过水的作物生长一阶段，三次生长后成熟。未浇水不生长，浇水状态每天重置。
- 默认背包 12 格、初始 50 颗芜菁种子，每格最多 99 个。播种成功消耗种子，收获成功获得实际作物；背包满且没有可用作物堆叠时保留成熟作物。尚未接入出售。

## 结构

- `Managers/GameManager`：整个游戏流程入口，统一调度配置、场景、系统初始化、UI 与返回菜单。
- `Managers`：配置、场景、UI、背包、农务、玩家、镜头、交互均由对应的 Manager 控制。
- `Managers/InventoryManager`、`Inventory/InventoryState`：真实物品数量、堆叠、容量检查和变化事件，先于农田初始化。
- `UI/Models`、`UI/Controllers`：MVC 数据和控制器；MainMenuView / PrototypeHud 只负责显示。
- `Configuration/GameConfig`：通过 AA 加载的运行配置，默认农田 5×5、50 颗种子、3 天生长。
- `Configuration/ItemCatalog`：AA 物品配置，定义物品 ID、名称、类型、图标、堆叠上限和描述。
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

原型几何体在运行时生成，尚无需要额外加载的美术资源。正式模型、材质和作物 Prefab 后续使用 AA 管理。当前不包含自动寻路、体力、经济、正式动画或遮挡淡化。

规则检查见 `Tools/VerifyFarmState.cs`，覆盖整片农田、重复操作、缺水、生长、收获及种子耗尽。

背包说明见 `Docs/Inventory.md`；`Tools/VerifyInventory.cs` 验证跨堆叠、失败操作不丢物品、数量守恒及满背包保护。

完整流程和 MVC 约定见 `Docs/GameArchitecture.md`。`SaveManager` 保存日期、农田、背包及角色位置，推进日期和返回主菜单时自动保存；“加载游戏”恢复进度。存档路径、备份与版本规则见 `Docs/Saving.md`。`SettingsManager` 控制本次运行的音量和全屏选项，设置面板遵循 MVC 并挂在 PopUp 层。
