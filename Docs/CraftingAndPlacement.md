# 制作与摆放

当前进度统一在每天睡觉结算后保存；白天操作仅修改运行状态，面板手动保存和返回菜单保存已取消，设置单独持久化。睡觉失败只重试该次固定快照。规则见 [暂停与睡觉存档](PauseAndSettings.md)。

走近办事处前的工作台，点击打开制作面板。使用木材和石料制作装饰，成品进入背包；工作台的两个面板可切换“工作台制作”和“工具升级”，制作中按类型分页。

| 配方 | 材料 | 产出 |
| --- | --- | --- |
| 木栅栏 | 木材 × 2 | 木栅栏 × 1 |
| 石板路 | 石料 × 2 | 石板路 × 1 |
| 庭院灯 | 木材 × 3、石料 × 2 | 庭院灯 × 1 |

材料从背包扣除，检查扣料后能否容纳成品。制作失败不扣料；成功先一起提交材料和成品，再通知背包变化，不扣体力、金币，也不增加农务任务进度。

## 摆放与回收

商店购买的苹果树苗和橙子树苗也从背包摆放，属于 `PurchasedPlaceables`，不出现在工作台配方中。果树预留周围一格；回收返还树苗并重置成长、清除未采果实，详见 [果树](Orchard.md)。
1. 在背包选择成品，点击“摆放一个”。AA 加载预览后关闭背包，显示 Common 层 PlacementPanel。
2. WASD 移动，鼠标指向地面选择位置；采用 1 米网格，每件占一格。绿色表示可用，红色表示不可用。
3. R 或“旋转”按钮每次旋转 90°；左键有效位置或“确认摆放”扣除一件成品并生成实体。每次摆放一件，再从背包选择可继续摆放。
4. ESC 或“取消”关闭预览，保留成品。加载期间取消也会释放 AA 实例。
5. 走近已摆放物品，右键回收至背包。容量不足、距离过远或非本系统实体时保留原物，重复回收不会增加物品。

范围沿用 GameConfig.InteractionReach，默认 2.25 米。岛内格子中心坐标为 -11–11，总上限 128 件。农田和林舟通道保留；已有装饰、建筑、设施、资源、玩家、NPC 和海岸水面阻止摆放。预览禁用碰撞、灯光与阴影，不影响角色通行。确认后恢复实体碰撞；石板路可走过，庭院灯带暖色点光源。

制作面板暂停世界和输入。摆放模式暂停时钟、天气和 NPC 日程，允许玩家与镜头移动，暂停普通工具及世界交互；打开其他面板、睡觉与农务操作需先取消摆放。

室内背包可以查看成品，但只能回到农场后摆放。新摆放会避开小屋门前通道；已有存档中的装饰保留，出门时选择安全落点。

工作台现在按类型分三页：第一页制作以上三种农场装饰，第二页制作木桌、木椅和小台灯，第三页“养殖用品”用玉米 ×1 制作饲料 ×10。用品直接进入背包，不进入摆放模型和占位系统；详见 [饲料制作](FeedCrafting.md)。室内家具使用独立占位和存档，由 FurnitureManager 管理，详见 [室内家具](Furniture.md)。

## 管理与资源

- CraftingManager / CraftingState：配方校验、材料交换与制作反馈。
- PlacementManager / PlacementState：预览、网格 / 旋转、位置校验、AA 实体、摆放与回收。
- GameManager：配置和世界初始化、工作台和背包入口、输入、互斥、提交、保存及清理。相关流程位于 GameManager.Construction.cs。
- UIManager：AA 加载、层级挂载和释放；CraftingPanel、PlacementPanel 各有 MVC。

| 资源 | 路径 | AA 地址 |
| --- | --- | --- |
| 配置 | Assets/Config/CraftingCatalog.asset | CraftingCatalog |
| 木栅栏 | Assets/Art/Prefabs/Decorations/WoodFence.prefab | WoodFence |
| 石板路 | Assets/Art/Prefabs/Decorations/StonePath.prefab | StonePath |
| 庭院灯 | Assets/Art/Prefabs/Decorations/GardenLamp.prefab | GardenLamp |
| 制作面板 | Assets/Art/Prefabs/UI/CraftingPanel.prefab | CraftingPanel |
| 摆放面板 | Assets/Art/Prefabs/UI/PlacementPanel.prefab | PlacementPanel |

CraftingCatalog 同时定义配方、输出、IsFurniture / IsSupply 分类和摆放模型地址，为第 18 份 AA 配置。装饰与家具的材料须为 Material，输出分别为 Placeable（值 6）和 Furniture（值 7）；用品允许 Crop 或 Material 原料，输出 Material，不需要模型。输出与配方 ID 唯一，原有物品类型值保持稳定。摆放模型保留根 PlacedDecoration、BoxCollider 和渲染器，实体需符合一格的体积；用品不加入此模型和占位系统。

编辑器只创建缺失配置、模型和面板；为已有 InventoryPanel 添加摆放按钮，为 ToolUpgradePanel 添加制作入口，完整的已有引用和布局继续保留。UI 使用 TMP 和现有奶油白 / 草绿 / 暖沙主题，CraftingPanel 为 PopUp，PlacementPanel 为 Common。

## 存档与失败恢复

版本 16 新增 PlacedObjects，保存唯一 Id、ItemId、GridX、GridZ、QuarterTurns，和背包一起写入。确认摆放、回收、制作成功后在睡觉时统一保存。预览不保存，也不预先扣除物品。退出释放预览、实体和模型资源，读档重新通过 AA 创建实体，恢复位置和朝向。

版本 1–15 迁移补空摆放记录，保留农田、库存、NPC 日程、好感度及事件，迁移不赠送装饰。数量、ID、物品、网格、朝向、重复占位、农田和 NPC 通道严格校验。实体加载时再检查真实设施碰撞；场景布局变更导致旧摆放位置冲突时，清理初始化并返回菜单显示错误。

白天制作、摆放和回收不写盘；睡觉写盘失败在日报重试同一次固定快照。返回菜单或结束程序以最近成功睡觉快照为准。提交期间禁止再次确认、取消、制作或返回，避免事件回调造成重复操作。

## 验证

Tools/VerifyConstruction.cs 覆盖材料 / 容量组合、制作原子性、通知与重入、农田尺寸 1–12 的全部网格、路线保护、旋转、占位、回收容量、重复操作、128 件上限、深快照、原子恢复和版本 15 迁移。

ConstructionFlowVerification.Run 在隔离工程构建 AA 并验证三个模型、两个 MVC 面板、工作台切换、背包入口、预览 / 碰撞 / 旋转、确认 / 取消、回收、保存失败与重试、精确读档、迁移、错误数据清理、最大农田与中文排版。
