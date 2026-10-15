# 玩家小屋与室内

当前进度统一在每天睡觉结算后保存；白天操作仅修改运行状态，面板手动保存和返回菜单保存已取消，设置单独持久化。睡觉失败只重试该次固定快照。规则见 [暂停与睡觉存档](PauseAndSettings.md)。

农场原来的露天床替换为玩家小屋。走近点击小屋进门，室内点击出口返回农场。床位于室内，走近点击可打开 SleepPanel，沿用睡觉、跨日结算和睡觉时统一保存。

HUD 显示“岛屿农场”或“我的小屋”。“回家休息”在门口进入小屋；离门太远时提示先到小屋。室内“休息”需靠近床，不再通过 HUD 在任意位置直接推进日期。GameManager.AdvanceDay 保留为统一结算入口，确认睡觉后调用。

## 场景与状态

GameManager 调度进出、输入、场景加载、位置恢复和保存，相关流程在 GameManager.Housing.cs。HousingManager 控制小屋、门口 / 出口、可用位置、区域、角色显示层及镜头。SceneManager 负责所有 AA 场景的加载与卸载。

进入小屋时追加加载 PlayerHome 场景，农场场景和所有系统保持存在。室内在独立坐标区域，默认原点 `(40, 0, 0)`；同一个角色和镜头切换显示层。室内照明、家具、床和出口采用 HousingInterior 层，房间正面墙仅保留 Ignore Raycast 层碰撞，以便斜俯视和点击床。UIManager 将面板层统一为 Unity UI 层，UIView 的 Bottom / Common / PopUp / Top 排序继续独立生效。

进屋后时间、天气结算和林舟日程继续运行。室外雨滴、农田与角色不会显示在室内镜头中；农田状态、物品、好感度、事件和装饰保持。睡觉后角色留在室内，按原流程推进日期、作物、订单、采集、体力、送礼日期和 NPC 早晨位置。

第一次进屋才加载室内；出门保留已加载的房间，再次进屋复用场景。返回主菜单时先卸载室内，再卸载农场并释放小屋模型、区域状态和其他系统。重新进入游戏不重复创建房间或小屋。

## 交互与失败

- 进出必须在 InteractionReach 内，默认 2.25 米；工具忙碌、模态面板或重复请求时拒绝切换。
- 切换使用追加到枚举末尾的 GameFlowState.Transitioning，暂停角色、镜头、工具、交互和游戏时间。Top 层 SceneTransitionPanel 显示正在进屋 / 返回农场。
- 加载失败保留原位置与区域，释放切换面板并恢复输入，可再次点击门口重试。
- 室内可移动、查看背包、食用料理、摆放家具和睡觉；农具按钮禁用。农场装饰继续在农场摆放，室内背包对应按钮禁用；家具由 FurnitureManager 独立管理，详见 [室内家具](Furniture.md)。
- 小屋门前保留通道，新增装饰不可堵住通道。旧存档的已有装饰保留；出门按门前位置、邻近位置和最近可用岛屿格子选择安全落脚点。
- 角色传送时清除旧的下落速度与动作锁，镜头立即跟随新位置，避免长距离平移或下落惯性。

小屋主体占用与原床相同的平面碰撞范围（1.3 × 2 米），避免扩大建筑后覆盖旧装饰。若旧存档中角色位于原床内部，恢复到门前安全位置。已有装饰、库存和进度不移动或重置。

## 资源与配置

| 资源 | 路径 | AA 地址 |
| --- | --- | --- |
| 配置 | Assets/Config/HousingConfig.asset | HousingConfig |
| 小屋外观 | Assets/Art/Prefabs/Buildings/PlayerHouse.prefab | PlayerHouse |
| 室内整体 | Assets/Art/Prefabs/Buildings/PlayerHomeInterior.prefab | PlayerHomeInterior |
| 室内场景 | Assets/Scenes/PlayerHome.unity | Scenes/PlayerHome |
| 切换面板 | Assets/Art/Prefabs/UI/SceneTransitionPanel.prefab | SceneTransitionPanel |

HousingConfig 为第 19 份 AA 配置，包含 ExteriorAddress、InteriorSceneAddress、InteriorOrigin 和 InteriorLayer。室内原点与农场保持至少 35 米轴向间距，坐标绝对值不超过 1000；默认层 8，编辑器选择空闲层并命名 HousingInterior。

室内场景包含室内预制体作为 AA 依赖，不加入 Player Build Settings。正式床、墙、地板与家具在预制体内保存。HomeInterior 引用出生点、床和出口；HomePortal 区分入口 / 出口并引用距离检测点。编辑器只创建缺失资产，保留已编辑场景和预制体；HUD 只补位置标题引用。

室内默认地板 12 × 10 米，允许保存的角色中心为 X ±5.8 / Z ±4.8，Y -0.1–2；加载时再检查实际家具和墙体。靠墙的合法位置按 CharacterController 的 skinWidth 检查，避免把允许的接触误判为家具重叠。替换室内布局时应保留安全出生点、床和出口。

一次扩建后地板向右和后侧扩至 16 × 12 米，原点、床、出口和家具不移动；位置上界改为 X 9.8 / Z 6.8，下界保持原值。HomeInterior 另有旧墙、窗组件和踢脚线引用，供 HousingManager 原位扩容。版本 19 保存升级与施工，见 [小屋扩建](HouseUpgrades.md)。

## 存档

版本 17 新增 PlayerArea，允许 Island / PlayerHome。现有 PlayerX / PlayerY / PlayerZ 在农场记录世界坐标，在室内记录相对 InteriorOrigin 的局部坐标。保存区域与位置时仍保存完整农场、背包、NPC、装饰等状态。

成功进出后睡觉时统一保存；保存失败保留已完成的切换和完整运行状态，显示错误，可在背包保存或返回菜单前重试。房间会话和加载状态不保存。室内读档先初始化农场和系统，再通过 AA 加载房间并恢复角色位置和镜头。

版本 1–16 迁移补 Island，保留原有位置和世界快照，只有与新小屋重叠的角色位置会在进入世界时选择安全落脚点。未知区域、非有限坐标、越界和室内误用世界坐标时拒绝存档；有效备份可恢复。

## 验证

Tools/VerifyHousing.cs 检查区域切换 / 取消 / 重入、边界、非有限坐标、区域与局部位置快照、版本 16 迁移以及保留农田、库存、装饰和 NPC 日程。

HousingFlowVerification.Run 在隔离工程构建 AA 后验证真实门口 / 出口、切换暂停、惰性加载 / 复用、显示层、HUD、室内食用 / 睡觉、世界保留、保存失败与重试、精确位置 / 墙边位置读档、旧床位置迁移、非法区域、AA 失败恢复、清理、最大农田和中文。GameFlowVerification、ConstructionFlowVerification、TaskFlowVerification 回归原有玩法。
