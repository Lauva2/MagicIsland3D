# 物品与背包

物品配置通过 ConfigurationManager 从 AA 地址 `ItemCatalog` 加载。InventoryManager 在 GameManager 初始化世界之前创建背包，初始种子数量来自 GameConfig。

## 配置

- `Assets/Config/ItemCatalog.asset`：稳定物品 ID、名称、类型、图标、图标颜色、堆叠上限和描述。当前包含 `turnip_seed`（芜菁种子）和 `turnip`（芜菁），每格最多 99 个。图标暂用不同颜色的圆角色块。
- `Assets/Config/GameConfig.asset`：InventorySlots 默认 12，可配置 6～36；SeedItemId / HarvestItemId 指定当前种植使用的种子和收获物。
- 稳定 ID 用于规则和数量查询，显示名称可修改；UI 预制体的 AA 地址仍等于预制体名字。

## 数据与规则

- InventoryState 是纯 C# 数据，提供只读格子快照、数量查询和全部成功或全部失败的添加／消耗。
- 添加时先填已有同类堆叠，再占空格；消耗可跨堆叠，并在数量归零时释放格子。
- 数量不足、容量不足、未知 ID、非正数量都会拒绝操作，不改变已有物品，也不发出变化事件。
- FarmManager 通过 FarmActions 协调农田与背包：播种成功消耗一颗种子，收获成功加入一个作物；背包满时保留成熟作物。即使没有空格，只要已有作物堆叠还有空间，也可以收获。
- FarmState 只保存农田状态，不再保存独立的种子／收获计数；HUD 数量从真实背包读取。

## UI MVC

- `InventoryUI.prefab` 位于 `Assets/Art/Prefabs/UI`，由 UIManager 按 AA 地址 `InventoryUI` 实例化。
- InventoryModel 保存格子和详情数据；InventoryView / InventorySlotView 显示组件；InventoryController 绑定选择／关闭事件，并订阅背包变化，关闭游戏时解除绑定。
- B 键或 HUD 的背包按钮打开，B / Esc / 关闭按钮收起。打开期间停用角色移动、工具交互和镜头缩放；关闭后恢复。
- 点击格子显示物品名称、描述、数量和堆叠上限。容量超过当前视口时可以滚动。
- 返回主菜单前保存进度，随后释放 InventoryUI 并清理运行时背包。“加载游戏”恢复原有格子和数量，“新游戏”使用初始物品。当前未接入拖动整理、丢弃、商店或出售。

## 检查

- `Tools/VerifyFarmState.cs`：509 项农务与实际物品流转断言。
- `Tools/VerifyInventory.cs`：1515 项背包／农务断言，包括跨堆叠、失败操作、数量守恒和满背包保留作物。
- `GameFlowVerification.Run`：实际 AA 构建与 MVC 进入／返回流程，包含背包按钮、详情、输入启停、播种／收获、容量保护及新游戏重置。
