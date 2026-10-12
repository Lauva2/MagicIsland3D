# 物品与背包

物品配置通过 ConfigurationManager 从 AA 地址 `ItemCatalog` 加载。InventoryManager 在 GameManager 初始化世界之前创建背包，初始种子数量来自 GameConfig。

## 配置

- `Assets/Config/ItemCatalog.asset`：稳定物品 ID、名称、类型、图标、图标颜色、堆叠上限和描述。当前包含芜菁、番茄、玉米的种子及成熟作物，以及木材、石料两种材料，每格最多 99 个。图标暂用不同颜色的圆角色块。
- `Assets/Config/GameConfig.asset`：InventorySlots 默认 12，可配置 6～36；SeedItemId / HarvestItemId 指定默认种子和收获物，其他品种由 CropCatalog 定义。
- 稳定 ID 用于规则和数量查询，显示名称可修改；UI 预制体的 AA 地址仍等于预制体名字。

## 数据与规则

- InventoryState 是纯 C# 数据，提供只读格子快照、数量查询和全部成功或全部失败的添加／消耗。
- 添加时先填已有同类堆叠，再占空格；消耗可跨堆叠，并在数量归零时释放格子。
- 数量不足、容量不足、未知 ID、非正数量都会拒绝操作，不改变已有物品，也不发出变化事件。
- FarmManager 通过 FarmActions 协调农田与背包：播种成功消耗一颗种子，收获成功加入一个作物；背包满时保留成熟作物。即使没有空格，只要已有作物堆叠还有空间，也可以收获。
- GatheringManager 协调资源次数、体力与材料产出。每次操作前要求空间容纳完整产出，失败不改变资源或体力，最后一下加入整份材料。HUD 显示木材和石料数量，材料不能用于播种。
- FarmState 只保存农田状态，不再保存独立的种子／收获计数；HUD 数量从真实背包读取。

## UI MVC

- `InventoryPanel.prefab` 位于 `Assets/Art/Prefabs/UI`，由 UIManager 按 AA 地址 `InventoryPanel` 实例化。
- InventoryModel 保存格子和详情数据；InventoryView / InventorySlotView 显示组件；InventoryController 绑定选择／关闭事件，并订阅背包变化，关闭游戏时解除绑定。
- B 键或 HUD 的背包按钮打开，B / Esc / 关闭按钮收起。打开期间停用角色移动、工具交互和镜头缩放；关闭后恢复。
- 点击格子显示物品名称、描述、数量和堆叠上限。容量超过当前视口时可以滚动。
- 种子详情还显示适宜播种季节及当前是否适宜。可选择非当季种子，实际播种时检查季节；错误季节不消耗种子或体力。详情预制体已扩展高度，新增文案使用已烘焙的 TMP 中文字体。
- 选择 CropCatalog 支持且有库存的种子后，可点击“用于播种”，关闭背包并切换到对应种子的播种工具；HUD 显示当前种子。工具动作进行中时拒绝切换，保留背包并提示。
- 返回主菜单前保存进度，随后释放 InventoryPanel 并清理运行时背包。“加载游戏”恢复原有格子和数量，“新游戏”使用初始物品。商店可购买种子、出售作物；当前未接入拖动整理和丢弃。

## 检查

- `Tools/VerifyFarmState.cs`：509 项农务与实际物品流转断言。
- `Tools/VerifyInventory.cs`：1515 项背包／农务断言，包括跨堆叠、失败操作、数量守恒和满背包保留作物。
- `GameFlowVerification.Run`：实际 AA 构建与 MVC 进入／返回流程，包含背包按钮、详情、输入启停、播种／收获、容量保护及新游戏重置。

储物箱使用独立 InventoryState。InventoryState.TryTransferSlotTo 按选定格子原子转移，源与目标一起变动再通知；StorageManager 协调存取和保存。箱内数量不计入种植、买卖、订单和升级库存。详见 [储物箱](Storage.md)。

新增 Food 分类。背包选择食物显示“食用一个”，详情显示恢复量和当前体力，成功后自动保存。体力已满禁用食用，食物不能播种；箱内食物需先取出。详见 [烹饪与食物](CookingAndFood.md)。

新增 Fish 分类，沙丁鱼和鲈鱼沿用 99 堆叠、背包与储物字段，可在商店出售或作为料理食材；原鱼不能直接食用、播种或送礼。两种鱼类成品属于 Food，可食用、送礼及储物。详见 [钓鱼](Fishing.md)和[鱼类料理](FishCooking.md)。

新增 Placeable 分类（枚举值 6），木栅栏、石板路、庭院灯沿用 99 堆叠和储物规则。背包选择成品显示“摆放一个”，关闭背包进入网格预览；确认才扣除一件，取消保留物品。右键回收先检查背包容量。成品不能播种、食用或送礼，详见 [制作与摆放](CraftingAndPlacement.md)。

室内背包仍使用同一库存，可食用料理、摆放家具及保存进度；农场装饰摆放按钮禁用，带回农场后可以继续摆放。家具在室外禁用摆放，带入小屋后启用。区域与位置保存规则见 [玩家小屋](Housing.md)，家具见 [室内家具](Furniture.md)。
