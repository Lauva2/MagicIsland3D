# 紧凑游戏界面

左上状态卡保留地点、日期、天气、时间、金币、体力与当前种子，原背包汇总与常驻移动说明隐藏；完整库存继续在背包查看。暂停、背包、地图在右上横排，作物 / 目标卡位于其下，任务卡继续向下排列，保持互斥提示和不暂停世界的展开 / 收起。底部居中工具栏保留 1–7 和目标操作，重复的常驻反馈隐藏，操作结果仍使用 Top 层 FeedbackPanel。

布局保存在原 FarmHudPanel、CropInfoPanel、InteractionPromptPanel、TaskPanel、FeedbackPanel 中，保留 GUID、AA 地址、颜色、圆角、阴影、原按钮与 MVC 引用。TaskView.ExpandedHeight 为预制体配置，默认旧布局 260，本次紧凑布局 234；收起仍为 52。没有新增面板、输入方案、碰撞或进度字段。

ExpandedHeight 必须有限且不小于 52，沿用 Validate UI Prefabs 统一检查。高度调整后同时检查详情区、领奖和反馈的位置，不能只缩小卡片背景。

`Magic Island > Apply Compact Gameplay Layout` 是显式美术操作，调整上述五个原预制体的尺寸、锚点、文字和显示状态；重复执行结果一致。普通 Setup 不调用它，也不覆盖后续手工布局。需要恢复该设计时才显式执行，不能使用 Apply Cozy UI Theme 重新生成整个界面来替代。

`GameplayLayoutSetup.PrepareVerification` 检查显式重复应用与普通 Setup 后的资源字节保持一致。`InteractionPromptFlowVerification` 调用 GameplayLayoutVerification，使用真实 Packed AA，在 1280×720、1024×768、1920×1080 检查卡片边界、顶部按钮无遮挡、中文与文字溢出、任务展开 / 收起及操作区域。原流程继续覆盖工具切换、点击、弹窗、床铺、退出、失败 / 取消加载和重新进入；睡觉与读档另用 PauseSleepFlowVerification。

2026-10-17 已在隔离工程实际通过上述准备检查与 InteractionPrompt、Lighting、Seasonal、PauseSleep 四组 Packed AA 流程，检查三种分辨率截图与中文布局，并将五份界面预制体及 WeatherConfig / SeasonConfig 同步回工作区。原 GUID 与所有序列化对象身份保留，存档仍为 v29；未运行独立 Player 构建或整体性能测量。报告为 Logs/GameplayLayoutVerification.txt、AtmospherePolishVerification.txt 及各流程报告，验证副本和报告不入库。

魔法扩展在右上原横排按钮左侧补充「魔法（K）」按钮（108×36，右上偏移 -374/-20），不移动原按钮与卡片。多分辨率校验同时覆盖四个按钮；手札是独立 PopUp，详见 [魔法内容](Magic.md)。
