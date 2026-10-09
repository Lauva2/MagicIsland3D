# 游戏流程与系统框架

GameManager 统一调度流程，各系统由对应的 Manager 控制。UIManager 统一持有 UI 的 AA 实例句柄，UI 内部采用 MVC。

## 生命周期

`LoadingConfiguration → MainMenu → LoadingScene → InitializingGame → Playing → ReturningToMenu → MainMenu`

1. GameManager 是跨场景单例，创建各系统 Manager。
2. ConfigurationManager 初始化 AA，然后加载 `GameConfig` 和 `ItemCatalog`。
3. SaveManager 检查本地存档；UIManager 按名字加载 `MainMenuUI`，创建 MainMenuModel / MainMenuController，并根据存档状态启用“加载游戏”。
4. 点击“新游戏”使用初始状态；点击“加载游戏”先从磁盘读取并校验数据。GameManager 锁定流程，SceneManager 通过 AA 以 Additive 模式加载 GameConfig 指定的场景。
5. GameManager 先初始化 InventoryManager；FarmPrototype 创建占位世界对象；随后初始化 FarmManager、PlayerManager，按需恢复存档，再初始化 CameraManager、InteractionManager。
6. UIManager 按名字加载 `FarmHUD`、`InventoryUI`，创建对应 Model / Controller，背包默认收起。
7. 完成以上步骤后隐藏主菜单、启用玩家及交互输入，进入 Playing。
8. 推进日期时自动保存；返回时先保存，成功后禁用输入、卸载游戏场景、释放 HUD 和 Controller 的事件绑定、清除场景引用，再显示保留的主菜单。保存失败则保留游戏并提示重试。

主菜单启动场景保留用于返回，只有游戏场景通过 AA 加载和卸载。直接 Play MainGame 时也会自动创建 GameManager，走相同的配置和系统初始化流程；编辑器直接打开的场景按编辑器调试路径卸载。

## Managers

| Manager | 职责 |
| --- | --- |
| GameManager | 启动、新游戏、继续游戏、自动保存调度、返回菜单、流程状态、初始化顺序、重复请求拦截 |
| SaveManager | 存档版本、校验、JSON 保存与读取、原子替换和备份恢复 |
| SettingsManager | 本次运行的主音量和全屏参数，SettingsUI 通过 MVC 调用 |
| ConfigurationManager | 初始化 AA、加载并校验配置、持有配置句柄 |
| SceneManager | AA 场景加载和卸载、活动场景和句柄管理 |
| UIManager | 按预制体名字实例化 UI、创建和销毁 Controller、显示隐藏、资源释放 |
| InventoryManager | 初始物品、数量查询、添加／消耗、堆叠与容量检查 |
| FarmManager | 农田状态初始化、农务动作、跨日推进 |
| PlayerManager | 玩家初始化、移动参数、输入启停 |
| CameraManager | 镜头初始化、视角和缩放范围、控制启停 |
| InteractionManager | 操作距离、工具选择、交互控制启停 |

将来增加经济、任务、音频等系统时，各自增加对应的 Manager，并接入 GameManager 所需的初始化阶段。当前 GameManager 不提前创建尚未实现的系统。

## UI MVC

- Model：`UI/Models`，保存界面需要的数据，不包含 UI 组件。
- View：MainMenuView、PrototypeHud 继承 UIView，保存 TMP_Text / Button 等序列化引用和 Layer 配置，负责渲染，不加载资源或调用游戏逻辑。UIManager 按 Bottom、Common、PopUp、Top 自动挂载并排序。
- Controller：`UI/Controllers`，绑定按钮事件、更新 Model、驱动 View、调用对应的 Manager；释放时解除自己绑定的事件。
- UIManager：UI 的统一入口和资源拥有者，通过 `Addressables.InstantiateAsync` 加载，通过 `ReleaseInstance` 释放；UI 预制体都在 `Assets/Art/Prefabs/UI`。

事件示例：`NextDay Button → FarmHudController → GameManager.AdvanceDay → FarmManager → FarmState → HUD Model/View`。

## 配置

`Assets/Config/GameConfig.asset` 通过 AA 地址 `GameConfig` 加载，控制场景地址、农田尺寸、初始种子、生长天数、玩家速度、交互距离、镜头俯角和镜头大小。

编辑器配置工具只创建缺失配置，不覆盖已经调整的参数。配置不合法会停止启动并报告错误；启动失败后可调用 GameManager.RetryStartup。游戏场景或 HUD 初始化失败时返回菜单，允许重试。

## 验证

- `Tools/VerifyFarmState.cs`：509 项农务断言，包括配置参数生效。
- `GameFlowVerification.Run`：在独立验证副本中构建 AA，使用 Packed Play Mode 验证两轮进入/返回、单例、重复请求、MVC 按钮事件、日期推进、状态重置及 UI / 场景释放。
- `Magic Island > Validate UI Prefabs`：检查预制体组件和地址与名字一致。
