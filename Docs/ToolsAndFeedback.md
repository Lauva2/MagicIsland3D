# 工具动作与反馈

当前进度统一在每天睡觉结算后保存；白天操作仅修改运行状态，面板手动保存和返回菜单保存已取消，设置单独持久化。睡觉失败只重试该次固定快照。规则见 [暂停与睡觉存档](PauseAndSettings.md)。

`ToolManager` 管理当前工具、动作锁、手持模型、动作表现和农务／采集结果事件，由 `GameManager` 初始化、启停输入和清理。`FarmManager` / `FarmActions` 保留农田、背包与体力规则；鼠标农务通过 ToolManager 调用，HUD 工具按钮与数字键使用相同选择入口。

锄头、水壶、斧头、镐子已使用 Blender 制作的圆润网格；种子袋、收获篮和肥料袋沿用原有表现，鱼竿沿用钓鱼资源。数字键 1–7 与工具栏共用选择入口，斧头和镐子分别用于树木和石头。操作时角色朝向目标并轻微俯身，工具挥动、播撒或倾倒；耕地显示扬尘，播种显示种粒，浇水显示水滴与落地水花，收获显示金色闪光。特效使用 AA FarmActionEffects，由 ToolManager 持有依赖并统一推进及清理；角色动画和音效已接入。资源与验证见 [农田与作物表现](FarmArt.md)。

## Blender 农具

`Assets/Art/Prefabs/Tools/IslandFarmTools.prefab` 包含 FarmToolModels、Hoe、Water、Axe、Pickaxe 四个独立显示根，共用一张色板及一个 Standard 材质。锄头 1,760、水壶 3,368、斧头 1,884、镐子 2,384 三角面，共 9,396；无 Collider、Animator 或独立 Update。暖木柄、圆角金属刃、海蓝壶身、奶油色弧形提手和花洒嘴沿用现有角色配色。斧头为楔形刃口，镐子为弧形双尖，握柄带木纹和皮革缠绕；扩充工具时保留原锄头、水壶的网格数据和握持位置。

FarmActionEffects.HeldToolsPrefab 保存该预制体引用，它随原 `FarmActionEffects` AA 地址作为依赖加载，不增加配置或独立异步加载。ToolManager 在开放输入前验证工具引用、网格和无碰撞约束；缺失或无效时按原失败路径释放句柄并阻止初始化完成。ToolPresentation.BindEffects 在原 ToolSocket / Tool Grip 下实例化一套工具，Show 只切换显示，Animate 继续采样同一动作进度；实例共享网格和材质。清理时先销毁手持实例及特效，再由 ToolManager 释放 AA 依赖；取消加载不绑定迟到结果。

源文件和编辑方法见 [工具源美术](../SourceArt/Tools/README.md)。`Magic Island > Art > Apply Blender Farm Tools` 显式创建缺失网格、材质、预制体和特效引用；重复 Apply 保留手调。源数据改变需执行 `Rebuild Blender Farm Tools (Explicit)`，更新网格时保留 GUID、合法材质参数及工具根的握持偏移。普通资源配置仅在默认工具资源存在且特效引用缺失时补齐引用，不覆盖合法替换。

`FarmToolArtVerification.Run` 检查资源接入、缺失引用拒绝与重复配置保留手调。`PlayerAnimationFlowVerification.Run` 检查 Packed AA 网格 / 材质复用、四件工具切换、动作 / 暂停、退出 / 再进入和加载失败 / 取消清理，并输出各件工具 0%、25%、50%、75%、100% 的动作截图。农务、采集与睡觉回归分别使用 FarmArtFlowVerification、GatheringArtFlowVerification、PauseSleepFlowVerification，音效回归使用 AudioFlowVerification；均使用独立存档。几何与 Blender / FBX 回读入口为 `Tools/VerifyFarmTools.py`、`Tools/Art/VerifyFarmToolsSource.py`。

上述 Blender 农具接入仅改变美术资源与加载依赖，物品 ID、按键、农务规则、动作时长和存档版本 29 保持兼容。团队同步后按常规重建 Addressables。

2026-10-17 首次锄头 / 水壶接入通过 Unity 2022.3.62f2 接入检查及角色动作、农务、暂停睡觉、音效四项 Packed AA 回归；独立 Unity 重启后的导入标记也已验证。源文件 / FBX 回读和几何检查通过，游戏内近景、侧视图用于核对挥动 / 倾倒过程及水壶与腿部的间距。斧头 / 镐子扩充的验收记录见工具源美术文档。

`GameConfig.ToolActionDuration` 默认 0.55 秒。动作开始时农务立即结算一次，再播放表现；失败不播放动作或生成特效。动作锁在结算前建立，重复调用及事件内的重复请求不会再次扣体力、种子或增加作物；动作期间不能切换工具、打开睡觉确认或使用调试跨日入口。

动作期间暂停移动。打开背包或其他弹窗时，动作和特效冻结，关闭后继续；释放输入前已提交的农务不回滚。场景离开时清除锁、模型、特效和运行时材质，动作表现不写入存档。

角色在动作前 20% 进度内平滑转向目标，碰撞根保持原位。浇水在 35% 进度开始水滴，其余农具、收获与拾取在 50% 进度播放落点特效；树木 / 石头的受击反应同时开始，使用动作剩余时长恢复。动作时长仍读取 GameConfig.ToolActionDuration，角色与手持工具继续采样同一进度。工具成功音效与成功反馈沿用提交时的即时确认。

待播放特效只属于本次已经成功提交的动作。暂停时连同转身冻结，退出时清除；低帧率跨过接触点和动作结束时，先发出一次表现，再按该帧剩余时间推进效果，不重扣物品或体力。动作结束前采样最后一帧，再恢复当前工具和待机，避免跳过收势。

鼠标按住拖过不同农田仍可连续劳动，每个动作完成后再处理下一格。同一次按住期间，每格尝试一次；失败时松开后可重试。UI 点击被拦截，打开弹窗时清除旧鼠标手势，避免关闭后立刻对农田进行操作。工具管理器同时校验操作距离和输入状态。

`FeedbackPanel.prefab` 位于 `Assets/Art/Prefabs/UI`，AA 地址 `FeedbackPanel`，继承 UIView 并挂到 Top 层。FeedbackModel 保存提示状态，FeedbackController 监听 ToolManager 结果事件，FeedbackView 负责 TMP 显示和色彩。成功显示完成文案、体力消耗或对应作物名称及“+1”；失败显示距离、体力、农田条件、种子和容量等原因。

提示默认显示 2.2 秒，末尾淡出；同一原因和目标名称一秒内不重复弹出。打开背包或 PopUp 面板时清除残留提示，避免遮挡详情。CanvasGroup 和所有 Graphic 均不阻挡鼠标，避免提示盖住农田后导致操作中断。面板随游戏初始化加载，返回菜单时释放并解绑事件。

验证：`Tools/VerifyToolGate.cs` 检查忙碌拦截、暂停、恢复、超时和非法参数。`InteractionPromptFlowVerification.Run` 调用 InteractionExperienceVerification，在独立工程使用 Packed AA 检查提示 / 点击一致、失败原因、七类动作与拾取的转身 / 接触、实际弹窗暂停、低帧率和重复请求。角色、农务和采集分别回归 PlayerAnimationFlowVerification、FarmArtFlowVerification、GatheringArtFlowVerification，睡觉与读档沿用 PauseSleepFlowVerification，音效沿用 AudioFlowVerification；历史 GameFlowVerification 的旧保存断言不作为现行睡觉规则的验收入口。

采集通过 GameManager.TryGather → ToolManager.TryGather → GatheringManager → GatheringState，与农务共用动作锁和距离检查。逐次点击才能操作，错工具、材料空间不足、体力不足或资源冷却都不消耗；完整采集显示材料数量及体力反馈，规则和存档见 [资源采集](Gathering.md)。

2026-10-17 操作体验优化已在 Unity 2022.3.62f2 独立工程、独立进度 / 设置目录及 Packed Play Mode 下完成 InteractionPrompt（含 InteractionExperience）、PlayerAnimation、FarmArt、GatheringArt、Orchard、PauseSleep、Audio 七组流程检查，工具锁纯规则 14 项断言通过。最后复查覆盖持续失焦、普通悬停通知不能发起事务、点击通知不能重入和作物提示单行布局；已检查中文反馈、起手 / 接触截图及工具近景。采集验证改为在实际接触采样后立即检查摇晃与暂停，避免依赖下一帧的剩余动作时间。存档仍为 v29，无新增配置、AA 地址或资源覆盖，不创建独立 Player 发行包。报告位于 Logs/InteractionExperienceVerification.txt 及对应流程 Verification.txt，验证工程与本机报告不入库。
