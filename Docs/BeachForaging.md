# 海滩拾取

岛屿可行走的干沙滩上会出现扇贝壳、漂流木和海玻璃。走近后鼠标左键拾取一个，播放收取动作并显示入包反馈，不要求切换工具，也不消耗体力。背包无法容纳时不移除采集物；同一物品不能重复领取。装饰用的小贝壳仍属于场景景观。

初始生成 10 个采集物，沿 24 个带种子扰动的海岸位置分布，避开码头通道。未拾取的物品保留原位置，每次睡觉跨日补充最多 5 个，地面总量不超过 16 个。回菜单、进出房屋和加载游戏不会触发额外补充。

三种物品可以存入储物箱，并在岛屿商店出售：扇贝壳 18 金币、漂流木 8 金币、海玻璃 25 金币。商店增加对应收购选项，复用 AA 加载的 ShopPanel、ShopManager 与 MVC，不新增保存按钮。

## 配置与表现

GatheringManager 统一控制原有树石采集和海滩拾取，BeachForageState 保存独立规则，GameManager 调度加载、跨日与清理。GatheringCatalog 的 BeachSites、BeachInitialCount、BeachDailyCount、BeachMaximumActive 控制数量，ItemCatalog 控制名称、价格与堆叠。

拾取预制体位于 `Assets/Art/Prefabs/Gathering`，AA 地址分别为同名的 `BeachShellPickup`、`BeachDriftwood`、`BeachSeaGlass`。模型与材质作为依赖随 AA 加载；触发碰撞体用于鼠标命中，不阻挡移动。BeachForageSetup 只补全缺失资源，已有预制体、网格、材质与物品配置可在编辑器调整。

## 存档

拾取和出售只更新当前运行状态，进度只在每天睡觉结算后保存。版本 29 的 BeachForage 记录日期、种子、物品 ID、生成日期和固定位置编号；读档完整恢复地面剩余物品，空海滩也不会重新生成。版本 28 及更早存档通过既有迁移链为保存当天初始化海滩，不改变原有树石、背包、金币和果树状态。

## 验证

`Tools/VerifyBeachForage.cs` 验证数量上限、原子拾取、满背包保护、重入保护、确定性刷新、位置稳定、非法快照拒绝与迁移。

`BeachCloudFlowVerification.Run` 使用真正的 AA 打包内容，检查每个采集物的鼠标命中与可达性、工具和体力保持、商店 MVC 出售与中文布局、睡觉补充、存档重载及退出清理；同时验证云层和云影渲染。预览保存于 Logs 下的 BeachForagePreview、BeachPickupPreview、BeachShopPreview 和 CloudShadowPreview。
