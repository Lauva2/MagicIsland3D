# 交互提示与目标高亮

`InteractionManager` 统一决定鼠标指向的目标、显示内容与点击分发。`InteractionController` 读取鼠标和工具快捷键，每帧只请求一次目标判定；提示、地块轮廓和实际操作使用同一个结果。

## 操作与反馈

- 指向 NPC、树木 / 石头、公告栏、工作台、商店、料理台、储物箱、出货箱、海岸钓点、鸡舍、小屋入口 / 出口、床和已摆放物品时，显示名称、操作、状态与说明。
- 绿色表示可操作，暖金色表示需要工具、材料、体力或背包空间，灰色表示距离太远。点击仍由原系统检查和提交，指向目标不扣除物品、体力或金币。
- 左键执行对应操作；摆放物品使用右键回收。农务沿用按住左键拖过地块、同一次按压不重复操作的规则。工具切换、动作锁和 UI 起始点击继续阻止误操作。
- 空地块显示耕地 / 播种 / 浇水 / 施肥准备提示，并使用已有地块轮廓。已种植地块显示原 `CropInfoPanel`，避免两张信息卡重复出现。
- 鼠标移到可拦截点击的 UI、弹窗打开或正在加载、摆放模式、切换场景和返回菜单时清除目标与高亮。提示卡本身不拦截鼠标。
- 床铺说明进度只在睡觉结算后保存；本功能不新增保存点或存档字段。

## AA、MVC 与布局

`Assets/Art/Prefabs/UI/InteractionPromptPanel.prefab` 使用同名 AA 地址，由 UIManager 加载至 Common 层。InteractionPromptModel 保存展示数据，InteractionPromptController 订阅目标变化并更新 Model，InteractionPromptView 使用中文 TMP_Text 和状态色显示。Controller 释放时解除事件。

提示卡位于右上方，使用与作物信息相同的区域，两者互斥显示；与顶部按钮、下方任务卡和底部操作反馈分开。默认奶油白、草绿、暖沙色。预制体中的 CanvasGroup / Graphic 均不拦截鼠标。

`Assets/Art/Prefabs/Effects/InteractionHighlight.prefab` 由 InteractionManager 通过 AA 加载和释放，默认地址为 `InteractionHighlight`，可在 AA `GameConfig.InteractionHighlightAddress` 中替换。LineRenderer 在目标脚下绘制柔和椭圆，按当前可见 Renderer 的范围更新，轻微呼吸；没有碰撞体，不改目标材质，可与 CameraManager 遮挡淡化同时使用。室内高亮跟随目标的渲染层。

InteractionPromptSetup 只创建缺失的预制体、材质和 AA 注册，重复执行保留已有美术修改。替换模型和 UI 时保留 `.meta`、组件引用和目标标记；高亮预制体需要 InteractionHighlight、有效 LineRenderer、至少 8 个顶点与材质，不得有 Collider。

## 目标判定

射线使用当前镜头的可见层，忽略 UI 层与玩家自身，从最近碰撞体寻找对应系统拥有的目标。未标记的实体障碍会遮住后方农田。农田仍通过网格平面拾取，受镜头层和距离限制。

FarmActions.CanPerform / FarmManager.CanPerform 为只读准备检查，复用实际操作的背包、种子、季节、体力及施肥条件。采集复用 GatheringState.Check。实际点击依然走 ToolManager 与 GameManager 的现有操作流程，成功结算一次后启动动作与音效。

## 验证

InteractionPromptFlowVerification 使用打包 AA 内容验证 UI 层、中文字体、不拦截鼠标、工具 / 距离提示、场景目标、农田点击与遮挡、弹窗屏蔽、采集音效、床与出入口、回收、失败 / 取消加载、返回菜单释放和重新进入。输出 `Logs/InteractionPromptVerification.txt` 及树木、公告栏、床铺预览。

Tools/VerifyFarmState.cs 验证重复准备检查不产生状态变化或通知，以及实际耕地 / 播种后准备状态立即更新。另回归睡觉保存、声音、镜头淡化和玩家动画流程。
