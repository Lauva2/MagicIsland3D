# 商店与生产设施美术

商店、工作台、料理台和储物箱使用统一的栗木、奶油白、浅绿及黄铜配色，与小屋、办事处和海岸风格衔接。模型由可编辑的网格部件组成，可以在预制体中继续调整。

## 资源与加载

- `Assets/Art/Prefabs/Buildings/IslandShop.prefab`：条纹布棚、柜台、种子袋、蔬果托盘和中文招牌；AA 地址 `IslandShop`。
- `ToolWorkbench.prefab`：木板台面、工具架、锤子、材料卷及下层木石储备。
- `CookingStation.prefab`：浅绿柜体、金属锅具、挂勺、砧板及食材。
- `StorageChest.prefab`：木板箱体、浅绿箱盖、黄铜锁扣和侧把手。
- 新材质位于 `Assets/Art/Materials/Facilities`，棚布网格位于 `Assets/Art/Meshes/Facilities`；沿用村落公共材质。

`GameConfig.ShopPrefabAddress` 指定商店地址。GameManager 在玩法状态初始化后、办事处加载前等待 `ShopManager.LoadCounter`；ShopManager 持有 AA 实例句柄，返回菜单、失败和中途取消时释放资源。商店不再由 FarmPrototype 生成占位方块。其他三类设施继续分别由 ToolUpgradeManager、CookingManager、StorageManager 加载和释放。

## 交互与兼容

保留原柜台位置、工作台及其他设施的交互引用和实体碰撞。商店实体仅为原尺寸柜台，棚顶和商品没有额外碰撞；新增装饰放在 Ignore Raycast 层，不干扰鼠标目标。设施通过 CameraOccluder 接入角色遮挡淡化。

走近柜台点击打开 ShopPanel；走近工作台点击打开 CraftingPanel，可切换工具升级。料理和储物继续使用原有 MVC 面板。美术更新不添加存档字段，白天交易、制作和存取只更新运行状态，睡觉结算才保存。

## 编辑与验证

`FacilityArtSetup` 由 `PrototypeAddressablesSetup.EnsureSetup` 调用，创建缺失资源、迁移旧设施并注册商店地址。`FacilityVisual.ArtVersion` 标记美术版本，同版本再次执行配置不会覆盖预制体和材质中的编辑。

`FacilityArtFlowVerification.Run` 在隔离项目构建实际 AA 内容，验证预制体保留、鼠标分发、买卖及距离限制、制作和工具升级、料理和储物、睡觉存档与读档、资源释放及取消加载，并生成场景预览。纯逻辑规则另由 VerifyShop、VerifyCooking、VerifyStorage、VerifyToolUpgrades 检查。
