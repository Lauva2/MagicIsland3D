# 交互提示与目标高亮

`InteractionManager` 统一决定鼠标指向的目标、显示内容与点击分发。`InteractionController` 读取鼠标和工具快捷键，每帧刷新指针；点击使用同一射线重新判定，提交后立即刷新。提示、地块轮廓和实际操作共享结果，避免换工具、移动或目标状态变化后沿用旧的准备条件。目标变化通知期间禁止重入点击。

每次刷新仍检查实际射线、距离和准备条件；结果的目标、名称、操作、说明、距离和可用状态均相同时，复用不可变目标快照，不重复通知、更新轮廓或渲染提示卡。地块轮廓直接切换目标与颜色，不先关闭再打开，并复用已取得的边框 Renderer。实体轮廓仍在 LateUpdate 跟随移动目标和呼吸动画。

作物信息继续逐帧读取当前地块，成长天数、浇水、施肥、收获物品与提示变化时才生成文字并更新界面；不缓存可变 FarmCell 或 Model 引用来判断内容变化。底部反馈在停留期与隐藏后不重复渲染，淡出只更新透明度，新反馈立即替换当前内容并重新计时。

## 操作与反馈

- 指向 NPC、树木 / 石头、公告栏、工作台、商店、料理台、储物箱、出货箱、海岸钓点、鸡舍、小屋入口 / 出口、床和已摆放物品时，显示名称、操作、状态与说明。
- 绿色表示可操作，暖金色表示需要工具、材料、体力或背包空间，灰色表示距离太远。点击仍由原系统检查和提交，指向目标不扣除物品、体力或金币。
- 左键执行对应操作；摆放物品使用右键回收。农务沿用按住左键拖过地块、同一次按压不重复操作的规则。工具切换、动作锁和 UI 起始点击继续阻止误操作。
- 空地块显示耕地 / 播种 / 浇水 / 施肥准备提示，并使用已有地块轮廓。已种植地块显示原 `CropInfoPanel`，避免两张信息卡重复出现。
- 鼠标移到可拦截点击的 UI、弹窗打开或正在加载、摆放模式、切换场景和返回菜单时清除目标与高亮。提示卡本身不拦截鼠标。
- 窗口失去焦点时清除指针和按住手势，恢复后需新的鼠标按压；不能复用失焦前或 UI 上的射线发起操作。
- 点击失败的底部反馈同时显示原因与目标名称，同一原因和目标名称一秒内不重复弹出。果树采收及海滩拾取的直接工具入口也会报告距离不足，且不启动动作或扣费。
- 床铺说明进度只在睡觉结算后保存；本功能不新增保存点或存档字段。

## AA、MVC 与布局

`Assets/Art/Prefabs/UI/InteractionPromptPanel.prefab` 使用同名 AA 地址，由 UIManager 加载至 Common 层。InteractionPromptModel 保存展示数据，InteractionPromptController 订阅目标变化并更新 Model，InteractionPromptView 使用中文 TMP_Text 和状态色显示。Controller 释放时解除事件。

提示卡位于右上方，使用与作物信息相同的区域，两者互斥显示；与顶部按钮、下方任务卡和底部操作反馈分开。默认奶油白、草绿、暖沙色。预制体中的 CanvasGroup / Graphic 均不拦截鼠标。

紧凑布局将右上按钮横排，提示卡统一从顶部 72 开始，任务卡从 234 开始，避免地图按钮被覆盖；底部反馈位于工具栏上方。具体资源维护与多分辨率验证见 [游戏界面布局](GameplayLayout.md)。

`Assets/Art/Prefabs/Effects/InteractionHighlight.prefab` 由 InteractionManager 通过 AA 加载和释放，默认地址为 `InteractionHighlight`，可在 AA `GameConfig.InteractionHighlightAddress` 中替换。LineRenderer 在目标脚下绘制柔和椭圆，按当前可见 Renderer 的范围更新，轻微呼吸；没有碰撞体，不改目标材质，可与 CameraManager 遮挡淡化同时使用。室内高亮跟随目标的渲染层。

InteractionPromptSetup 只创建缺失的预制体、材质和 AA 注册，重复执行保留已有美术修改。替换模型和 UI 时保留 `.meta`、组件引用和目标标记；高亮预制体需要 InteractionHighlight、有效 LineRenderer、至少 8 个顶点与材质，不得有 Collider。

## 目标判定

射线使用当前镜头的可见层，忽略 UI 层与玩家自身，从最近碰撞体寻找对应系统拥有的目标。农田通过网格平面拾取，与实体比较实际射线距离：前方未标记的障碍仍遮住地块，地块后方物体不能抢走点击。农田仍受镜头层和交互距离限制。

已种植地块的 CropInfoPanel 从统一目标读取操作名称或失败原因；可操作时保持一行操作提示，鱼竿、施肥、距离不足和动作忙碌时不再另用一套农务描述。

FarmActions.CanPerform / FarmManager.CanPerform 为只读准备检查，复用实际操作的背包、种子、季节、体力及施肥条件。采集复用 GatheringState.Check。实际点击依然走 ToolManager 与 GameManager 的现有操作流程，成功结算一次后启动动作与音效。

## 验证

InteractionPromptFlowVerification 使用打包 AA 内容验证 UI 层、中文字体、不拦截鼠标、工具 / 距离提示、场景目标、农田点击与遮挡、弹窗屏蔽、采集音效、床与出入口、回收、失败 / 取消加载、返回菜单释放和重新进入。输出 `Logs/InteractionPromptVerification.txt` 及树木、公告栏、床铺预览。

该流程使用独立进度 / 设置目录，并调用 InteractionExperienceVerification，补充换工具后的旧提示、点击时距离变化、射线前后顺序、通知重入、失焦 / UI 清理、失败反馈限频、作物信息一致性，以及七类工具 / 海滩拾取的转身、接触、暂停和低帧率检查。补充报告为 `Logs/InteractionExperienceVerification.txt`。

刷新优化另检查连续 120 次相同悬停 / UI 更新不重复发布目标、不重新拼接作物说明、不移动地块轮廓；原位改变成长、浇水、施肥与品质后仍立即显示最新内容，反馈停留、淡出、隐藏及中途替换保持正确。

2026-10-17 刷新优化已在隔离工程中实际通过 InteractionPromptFlowVerification（含上述补充检查）与 PauseSleepFlowVerification，均使用打包 AA；检查了中文作物卡预览及退出 / 重进清理。本次没有配置、预制体、AA 地址或存档版本变更，未进行独立 Player 构建或整体帧率测量。

Tools/VerifyFarmState.cs 验证重复准备检查不产生状态变化或通知，以及实际耕地 / 播种后准备状态立即更新。另回归睡觉保存、声音、镜头淡化和玩家动画流程。
