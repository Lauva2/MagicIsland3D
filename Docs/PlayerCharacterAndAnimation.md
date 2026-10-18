# AA 角色预制体与基础动画

玩家使用 `Assets/Art/Prefabs/Characters/PlayerCharacter.prefab`，默认 AA 地址为 `PlayerCharacter`。GameConfig 的 `PlayerPrefabAddress` 可配置已注册的其他角色地址。GameManager 加载游戏场景后调用 PlayerManager 加载角色，再由 FarmPrototype 绑定镜头和场景交互；农场不再运行时拼装角色。当前全项目为 25 份配置、28 个 UI 面板、存档版本 29；主角美术不增加存档字段。

## 角色与管理

- PlayerManager 持有 InstantiateAsync 句柄，校验 PlayerMovement、CharacterController、Visual、PlayerAnimation、Animator 和手持插槽，设置速度并在初始化完成前关闭移动与动画播放。
- 根对象控制位置及碰撞；Visual 只控制朝向；Model 内的 Animator 控制 Bob、头、手臂和腿部。只有根上的 CharacterController 参与碰撞，关节网格只负责外观。
- 工具握点使用 PlayerAnimation.ToolSocket，当前位于右手。ToolManager 持有工具与特效，钓鱼竿沿用原有 AA 加载和钓鱼规则。
- 进入 / 离开小屋复用同一角色及 AA 句柄，恢复位置时清理移动惯性、动作等待和动画姿态，室内层级沿用 HousingManager。
- 返回菜单或销毁时释放角色实例；加载失败释放句柄，取消后用代次检查防止晚到结果绑定，并在加载完成回调中释放实例。

## 动画与移动

动画资源位于 `Assets/Art/Animations/Player`，控制器为 PlayerCharacter.controller，包含 Idle、Walk、Hoe、Plant、Water、Axe、Pickaxe、Harvest、Fertilize 共 9 个状态和动画。Idle / Walk 循环，工具动作不循环；动画不包含结算事件。

WASD 仍按镜头方向移动，斜向输入限制到相同速度。PlayerMovement 从 CharacterController.Move 的实际水平位移计算速度；MoveSpeed 是当前实际速度与移动速度的比值，以 0.1 秒阻尼驱动 Idle / Walk，状态过渡为 0.14 秒。因此抵住墙壁时回到待机，不会根据按键持续踏步。

Walk 使用 StrideRate 调整步频：实际速度除以 PlayerAnimation.ReferenceWalkSpeed（默认 4 米 / 秒），限制为 0.1–3 倍。角色朝向继续由移动脚本平滑控制。所有动画禁用 root motion，避免 Animator 改变碰撞根的位置。

## 工具动作与暂停

ToolManager 成功提交农务 / 采集后设置动作锁、朝向、角色动作和手持模型，再发出成功事件播放音效；失败和重复请求不启动动画。角色动作由 ToolManager 的 ActionProgress 直接采样，手持工具也使用同一进度，改变 GameConfig.ToolActionDuration 仍可保持一致。

动作起手不再瞬间跳转朝向：Visual 在前 20% 动作进度内平滑转向目标，根位置与 CharacterController 不随转身移动。暂停冻结该进度，关闭弹窗继续原来的转身；退出时直接清理待播放表现，避免额外旋转已离开的角色。接触特效由同一动作进度触发，角色 Animator 仍不承担业务结算。

动画始终仅负责表现：扣体力、扣种子、收获和采集都由已有系统提交一次。Animator 不通过 AnimationEvent 发放物品。动作结束释放移动锁，工具恢复当前选择，角色以 0.12 秒过渡到待机。

PlayerManager.SetInputEnabled 同时控制 PlayerMovement 和 PlayerAnimation。弹窗打开时，正常移动回到静止待机；已有工具动作保留姿态，ToolManager 的动作锁与进度一起冻结。关闭后继续原动作。场景切换、读档和恢复位置重置为待机。

## 替换正式角色

原位替换预制体模型、材质和动画时保留 `.meta`。保留根上的 PlayerMovement 与 CharacterController，把 Visual 指向朝向根，把 PlayerAnimation.Animator 指向内部模型，把 ToolSocket 指向手部握点。

Animator 控制器需保留 Base Layer 上上述 9 个状态以及 float 参数 MoveSpeed / StrideRate；可替换各状态的动画、模型骨骼和动作曲线。工具状态使用归一化进度采样，所有模型碰撞和移动仍由根控制。PlayerCharacterSetup 只创建缺少的资源，重复执行不会重建已有预制体、控制器、材质或动画。

## 验证

PlayerAnimationFlowVerification 使用打包 AA 内容和独立存档检查实际移动、斜向速度、步频、撞墙回待机、七种动作与音效、动作锁、暂停、进出小屋、菜单释放、失败 / 取消加载和重新进入。报告为 `Logs/PlayerAnimationVerification.txt`。验证中将时间设为正午，输出行走、七种动作和室内待机的完整界面截图；对应的 `*Detail.png` 临时隐藏环境及 UI，保留实际 AA 角色、手持工具和游戏灯光，结束后恢复全部显示及镜头参数。

同时回归 PauseSleepFlowVerification、AudioFlowVerification、CameraOcclusionFlowVerification。动画状态不写存档，游戏进度仍仅在每天睡觉结算后保存。

## Blender 主角美术样板：Island Farmer

2026-10-16 增加第一版可编辑主角样板：暖沙草帽、棕发、海蓝衬衫、草绿背带裤、奶油色领口 / 袖口和圆头短靴。模型为六个关节网格，共 18,618 三角面、9,686 个导出顶点；使用一张 128×64 色板和一个共享材质。它沿用原 Bob、Head、左右 Arm / Leg 节点及右手 ToolSocket，不增加运行时骨骼、动画事件或碰撞。

资源与生成方式见 [源美术说明](../SourceArt/Characters/README.md)。`.blend` 包含独立预览场景、按现有 Unity `.anim` 曲线采样的九段姿态，以及仅在 Hoe 段显示的初版握持检查锄头。该预览道具保留原占位尺寸及旋转进度，不进入角色网格数据或 FBX；正式锄头、水壶、斧头和镐子见 [工具源美术](../SourceArt/Tools/README.md)。Blender 预览不替代引擎内的 Animator、工具提交、暂停和 AA 验收。

2026-10-17 已在 Unity **2022.3.62f2** 完成接入并同步正式 PlayerCharacter 预制体。模型经 PlayerCharacter 原 AA 地址加载，不增加配置、UI 面板或存档字段。

在正确版本 Unity 中完成编译后，显式执行 `Magic Island > Art > Apply Blender Player Art`：

1. 校验六块网格、法线 / UV / 三角形和现有角色节点。
2. 创建六个 Unity Mesh 资产及共享 Standard 色板材质；将它们挂到原关节的 `IslandFarmer Art` 子对象。
3. 原位保留 prefab GUID、AA 地址、CharacterController、Visual、Model / Animator、九段动画及 ToolSocket。旧原型对象保留，禁用其 Renderer，方便对照。
4. 以源数据 SHA-256 记录导入标记。再次 Apply 同一版本直接返回，保留人工修改；源数据改变时要求显式 `Rebuild Blender Player Art (Explicit)`，重建保留已有网格 GUID 和合法材质参数。

普通 `Configure Prototype Addressables` 和 PlayerCharacterSetup 不调用此导入，不覆盖人工美术。

接入检查入口为 `PlayerCharacterArtVerification.Run`，验证六块网格、共享材质、碰撞与握点，并实际修改预制体局部位置及材质后检查重复 Apply / Setup 保留手调结果，最后恢复原资产。

本轮在 `Logs/PlayerArtVerificationProject` 独立工程、独立存档和 Packed Play Mode 下通过接入检查及 `PlayerAnimationFlowVerification.Run`、`PauseSleepFlowVerification.Run`、`AudioFlowVerification.Run`、`CameraOcclusionFlowVerification.Run`。已检查中文界面、游戏镜头与动作近景；几何检查 `Tools/VerifyPlayerArt.py` 也通过。验证副本、构建缓存、报告与截图不提交；未生成独立游戏发行包。

团队同步资产后按常规执行 `Magic Island > Build Prototype Addressables`。预制体已应用当前模型，无需重复重建；后续编辑源模型并导出时，使用上述显式 Rebuild，再执行同一组验证。
