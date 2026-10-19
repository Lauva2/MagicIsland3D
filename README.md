# MagicIsland3D 农务原型

Unity 2022.3.62f2，内置渲染管线，旧版 Input，Addressables 1.21.17。

## 开发约束

所有成员和 AI 编程助手在开发前请先阅读根目录的 [统一开发约束（AGENTS.md）](AGENTS.md)。该文件汇总架构、AA / UI、睡觉存档、玩法、资源维护、验证与多人协作要求，是团队共用规则的统一入口。

各系统的详细设计与历史迁移保留在 `Docs/`，完整索引见 AGENTS.md。新增或修改规则时同步更新统一约束与相关专项文档；旧文档中的白天保存、面板手动保存、返回菜单保存等历史描述，以 AGENTS.md 的现行睡觉存档规则为准。

## 运行

1. 打开工程，等待 Package Manager 安装 Addressables 和脚本编译完成。
2. 编辑器自动创建 GameConfig、ItemCatalog、CropCatalog、WeatherConfig、SeasonConfig、TaskCatalog、OrderCatalog、GatheringCatalog、ToolUpgradeCatalog、StorageConfig、CookingCatalog、NPCCatalog、DialogueCatalog、RelationshipCatalog、StoryEventCatalog、FishingCatalog、NPCScheduleCatalog、CraftingCatalog、HousingConfig、AnimalConfig、InventoryUpgradeConfig、ShippingConfig、FertilizerConfig、AudioCatalog、OrchardCatalog 共 25 份配置，并注册配置、MainGame、PlayerHome 和 UI；也可以执行 `Magic Island > Configure Prototype Addressables`。
3. UI 预制体位于 `Assets/Art/Prefabs/UI`，自动创建缺失文件，也可以执行 `Magic Island > Create Missing UI Prefabs`。UI 使用 Canvas、Image、TextMeshProUGUI、Button，以奶油白、草绿和暖沙色搭配，带圆角与柔和阴影。
4. 打开 `Assets/Scenes/MainMenu.unity`，进入 Play。主菜单包含“新游戏”“加载游戏”“设置”“退出”；没有有效存档时禁用加载按钮。设置弹窗可调整音量和全屏显示，单独保存并在下次启动恢复；退出在编辑器中停止 Play，在构建版本中关闭程序。MainMenuPanel、FarmHudPanel、InventoryPanel、SettingsPanel、ShopPanel、SleepPanel 均通过 AA 实例化，AA 地址严格等于预制体名字。
5. 编辑器使用 Addressables 的 **Use Asset Database (fastest)** Play Mode Script。
6. 打包前执行 `Magic Island > Build Prototype Addressables`，然后构建 Player。Build Settings 中启动场景为 MainMenu；MainGame 只通过 AA 加载，地址为 `Scenes/MainGame`。

可以直接 Play MainGame 调试农务，仍会由 GameManager 加载配置并初始化系统，但这条路径不验证 AA 场景加载。

当前世界使用五个独立群系岛屿。进入游戏后可从 `Magic Island > 测试工具窗口` 点击群系按钮传送；先重建 AA 内容。布局、使用条件与旧大陆存档限制见 [独立群系岛屿](Docs/BiomeIslands.md)。

## 操作

- ESC / HUD“暂停（ESC）”：继续游戏、设置和返回主菜单；返回会确认丢弃当天未睡觉进度。

- WASD：按画面方向移动。滚轮：镜头缩放。
- 1 / 2 / 3 或底部按钮：锄头 / 种子 / 水壶。
- 数字键 4／5 或工具栏斧头／镐子按钮：走近外围树木和石头，逐次点击采集。树木 3 次、每次 4 体力、产出 3 木材、3 天恢复；石头 2 次、每次 5 体力、产出 2 石料、2 天恢复。完整产出空间不足时不扣体力或资源进度。
- 数字键 6 或工具栏鱼竿按钮：走近东南角海岸钓点，点击水面抛竿。等待 2–5 秒，咬钩后 2 秒内点击收竿。成功消耗 5 体力、获得 1 条沙丁鱼或鲈鱼；提前、超时或取消不消耗。鱼可储物或在商店出售，价格 35／65，捕获和售鱼在睡觉时统一保存，日报可在睡觉失败后重试该次睡觉存档。
- M 或右上角地图按钮：查看岛屿、当前位置和地点用途；M / Esc 关闭。
- 数字键 7 / 底部“7 肥料”：走近已耕作农田点击施肥，消耗肥料 ×1、体力 2，每轮一次。工作台“农田用品”制作；默认优质概率从 10% 提高到 40%，芜菁 / 番茄 / 玉米优质售价 38 / 75 / 128。
- 成功操作显示工具动作及泥土／种子／水滴／收获特效，提示体力消耗或获得作物。动作默认 0.55 秒，期间暂停移动；打开弹窗会暂停动作，关闭后继续。
- B 或背包按钮：打开／收起背包；Esc 或关闭按钮也可收起。打开时暂停角色、镜头和工具输入。点击格子查看物品详情。
- 靠近农田东南侧带浅绿 / 奶油白条纹棚布的商店，鼠标点击柜台打开商店；购买种子、出售作物。Esc 或关闭按钮收起商店并恢复输入。
- 靠近农田西南侧的小屋，点击建筑进屋；室内点击床后确认睡觉，点击出口返回农场。HUD“回家休息”在门前进入小屋，室内“休息”需靠近床。睡觉推进作物生长、重置浇水和时间到 06:00，并自动保存室内位置和世界状态；取消或 Esc 继续当天。HUD 显示时间，默认现实 1 秒为游戏 1 分钟，到 24:00 停表并提示睡觉。
- 左键：操作高亮农田；按住左键拖过相邻格子可连续劳动。
- 黄色边框：操作范围内；红色边框：需要走近。
- 成熟作物在任何工具下优先收获。每次按住鼠标只能对每格成功操作一次，松开后可以再操作。
- “下一天”：保留的调试结算入口，与睡觉执行相同规则；当天浇过水的作物生长一阶段，达到对应品种的生长次数后成熟。未浇水不生长，浇水状态每天重置。
- 默认背包 10 格、初始 50 颗芜菁种子和 100 金币，每格最多 99 个。播种消耗种子，收获获得作物；背包满时保留成熟作物。商店种子购买价 10、芜菁出售价格 25，每次交易 1 个；价格可在 ItemCatalog 中调整。
- 默认体力 100；耕地消耗 3、播种 1、浇水 2、收获免费。HUD 显示体力条，低体力使用暖橙色，数值可在 GameConfig 中调整。
- 背包点击种子后选择“用于播种”，关闭背包并切换种子工具；HUD 显示当前种子。商店顶部切换芜菁、番茄、玉米：生长次数 3／5／7，种子价格 10／20／30，售价 25／50／85；参数来自 CropCatalog 和 ItemCatalog。
- HUD 显示年份、季节、日期和晴天／雨天；新游戏首日晴天，后续按新日期所属季节产生天气，默认春／夏／秋／冬降雨概率为 30%／20%／40%／15%。雨水免费浇灌新播种和已有作物，睡觉时结算成长。阳光、环境光和天空随时刻变化，弹窗暂停时钟和雨滴。
- 默认芜菁适宜春、秋，番茄适宜夏，玉米适宜夏、秋；冬季暂不能新种这三种作物。跨季不枯萎，仍可浇水和收获。背包可选择任意种子，实际播种检查当季限制。
- 作物按进度显示幼苗、成长植株或成熟果实。鼠标指向作物时，右侧显示名称、成长进度、浇水状态和操作提示；距离较远时仍可查看。
- 农田前方偏右设料理台：2 个芜菁制作 1 碗芜菁汤（恢复 30 体力），1 个玉米制作 1 份烤玉米（恢复 20 体力）。背包选中食物后点击“食用一个”，满体力不消耗，恢复不超过上限。
- 料理面板可向下滚动查看鱼类食谱：沙丁鱼 ×1 制作烤沙丁鱼，恢复 35 体力；鲈鱼、芜菁、番茄各 ×1 制作鲈鱼蔬菜汤，恢复 50 体力。两道默认开放，成品可作为普通料理送给林舟增加 10 好感度；规则见 [鱼类料理](Docs/FishCooking.md)。
- 鸡舍收集的鸡蛋可制作煎蛋（鸡蛋 ×1，恢复 25 体力）或蔬菜蛋汤（鸡蛋、芜菁各 ×1，恢复 45 体力）。两道默认开放，成品可食用或作为普通料理送礼；料理面板共七张可滚动卡片，规则见 [鸡蛋料理](Docs/EggCooking.md)。
- 建成鸡舍后的次日起，公告栏每日订单池加入鸡蛋 ×2（70 金币）、煎蛋 ×1（50 金币）、蔬菜蛋汤 ×1（90 金币）。建造当天及旧档当天订单保留，读档不重新抽单；规则见 [养殖与料理订单](Docs/AnimalOrders.md)。
- 工作台第三页“养殖用品”可用玉米 ×1 制作饲料 ×10。用品进入背包，可存箱和喂鸡；材料不足或放不下全部十份时不扣玉米，成功后记录，睡觉时保存。沿用 CraftingManager、AA 配置和 MVC，见 [饲料制作](Docs/FeedCrafting.md)。
- 鸡舍显示“小白”和“团团”的独立状态卡：每日摸摸 +5，首次成功喂食 +2，亲密度最高 100，30 为熟悉、70 为亲近。互动显示图形爱心，读档保留每只鸡的当日限制，旧档不补发亲密度；见 [鸡的互动与亲密度](Docs/AnimalFriendship.md)。
- 已喂食且亲密度达到 70 的鸡次日有 25% 概率产优质鸡蛋，每只仍产一枚。普通 / 优质蛋分别收购 25 / 40 金币，可混用制作和交付，优先消耗普通蛋。鸡舍分别记录待收数，原子收取；品质结算固定保存，读档不重抽。见 [优质鸡蛋](Docs/QualityEggs.md)。
- 小屋门口左侧出货箱可放入 / 取回农作物、鱼和两种鸡蛋，显示预计收入；睡觉后统一售卖，日报列出数量、单价和收入。成功存取只更新运行状态，睡觉时保存，结算保存失败可重试且不重复发钱，见 [出货箱](Docs/Shipping.md)。
- 每日经营由 DailyReportManager 统计成功的金币收支和农田收获。睡觉后 DailyReportPanel 展示上一天收支、净变化、收获，以及本次新成熟作物 / 两种蛋产量；确认只关闭日报，加载直接进入保存的清晨。面板通过 AA 预加载、MVC 和 PopUp 层管理，见 [每日经营结算](Docs/DailyReports.md)。
- 新游戏背包默认 10 格，商店“背包扩容”可依次支付 300 / 800 金币升级为 20 / 30 格，每次增加 10 格。InventoryManager 管理即时扩容，保留物品、顺序和系统引用，费用计入日报；旧档保留实际基础容量，规则见 [背包扩容](Docs/InventoryUpgrades.md)。
- 办事处旁的岛务员林舟可走近点击交谈，支持逐句推进和 ESC 退出。完整结束首次对话后记录已见面，后续台词包含当前季节和天气，对话期间暂停场景操作和时间。
- 完成首次交谈后，对话面板可打开“送礼”：林舟每日收一份礼物，芜菁汤 +20，其他料理 +10，作物 +5。好感度最高 100，30 达到熟悉、70 达到友好并改变招呼；种子与材料不可送，满好感度不消耗礼物，读档保留当天限制。
- 林舟好感度 30 时可从对话按钮听“小岛往事”；70 时完成“朋友的食谱”解锁蔬菜浓汤，需先听完往事。两段事件中途退出不完成，奖励不能重复领取。浓汤消耗芜菁、番茄、玉米各 1，恢复 50 体力，料理面板显示锁定条件。
- 农田前方有固定储物箱，默认 18 格。点击左右两侧的格子，存入或取出一个 / 整组物品，按堆叠上限合并，成功存取只更新运行状态，睡觉时保存。箱内物品需先取回背包再使用。
- 办事处前方空地有工具工作台，走近点击升级：斧头消耗木材 6、石料 2、金币 50；镐子消耗木材 3、石料 4、金币 75。每把可强化一次，树木采集缩短至两次、岩石一次，睡觉时保存。
- 农场右后方的岛屿办事处门前右侧设公告栏，走近点击查看每日收购订单。一次交付 1–3 个作物，芜菁／番茄／玉米每个奖励 40／75／130 金币；交付后记录，睡觉时保存，睡觉后刷新，冬季收购库存作物。
- 右侧 TaskPanel 显示“耕地 → 播种 → 浇水 → 睡觉 → 收获 → 出售”新手目标，可收起。完成后点击领取金币，按顺序开放下一步；提前完成的成功操作仍被记录，全部奖励共 85 金币，领奖后记录，睡觉时保存。

## 结构

- `Managers/GameManager`：整个游戏流程入口，统一调度配置、场景、系统初始化、UI 与返回菜单。
- `Managers`：配置、场景、UI、背包、农务、玩家、镜头、交互均由对应的 Manager 控制。
- `Managers/InventoryManager`、`Inventory/InventoryState`：真实物品数量、堆叠、容量检查和变化事件，先于农田初始化。
- `UI/Models`、`UI/Controllers`：MVC 数据和控制器；MainMenuView / PrototypeHud 只负责显示。
- `Configuration/GameConfig`：通过 AA 加载的运行配置，默认农田 5×5、50 颗种子、3 天生长。
- `Configuration/ItemCatalog`：AA 物品配置，定义物品 ID、名称、类型、图标、堆叠上限和描述。
- `Configuration/WeatherConfig`、`Managers/WeatherManager`：AA 天气参数、每日晴雨、昼夜光照、雨滴表现及雨水浇灌。
- `Configuration/SeasonConfig`、`Managers/CalendarManager`：AA 季节长度、降雨概率及四季草地 / 树冠 / 常绿配色，从存档总天数推导日期及检查播种季节；WeatherManager 在初始化与睡觉跨季时同步环境颜色。配色及 Packed AA 验证见 [日期与季节](Docs/CalendarAndSeasons.md#四季环境配色)。
- `Terrain/SeasonalGroundCover`：随地形 AA 加载烘焙花草、落叶及薄霜，建设覆盖时隐藏，回收后恢复，飘叶随统一风动时钟暂停；见 [四季地表细节](Docs/SeasonalGroundDetails.md)。
- `Ocean/OceanFishAnimation`：五节点距离约束鱼身、惯性跟随及昼夜 / 晴雨活动节律，参数位于 IslandOcean 的 FishAnimation，复用既有安全包络；见 [海洋表现](Docs/Ocean.md#鱼身距离约束与环境节律)。
- `Ocean/OceanFishPassage`：珊瑚前逐鱼分流、错峰通过和渐进归群，复用完整鱼身导航与统一海洋时钟；见 [鱼群分流](Docs/Ocean.md#障碍分流与逐渐归群)。
- `Configuration/GatheringCatalog`、`Managers/GatheringManager`、`Gathering/GatheringState`：树木和石头节点、AA 模型、采集次数、材料和体力、按天刷新与存档，工具栏及背包沿用 MVC。
- `Configuration/OrderCatalog`、`Managers/OrderManager`、`Orders/OrderState`：每日订单生成、公告栏建筑 AA 实例、原子交付、奖励和存档，OrderPanel 通过 MVC 显示。
- `Configuration/TaskCatalog`、`Managers/TaskManager`、`Tasks/TaskState`：AA 新手任务、成功行为进度、顺序领奖和存档，TaskPanel 通过 MVC 显示。
- `Configuration/CropCatalog`：AA 作物配置，定义种子／收获物映射、生长次数和成熟占位表现。
- `Prototype/FarmPrototype`：由 GameManager 调用创建占位世界对象，随后由各 Manager 初始化，不自行启动或加载 UI。
- `Managers/PlayerManager`、`Player/PlayerMovement`、`Player/PlayerAnimation`：AA 角色加载 / 释放，CharacterController 移动、转向、真实速度与动画，工具动作由 ToolManager 按进度采样。
- `Camera/CameraController`：42° 正交镜头，跟随和缩放。
- `Farming/FarmState`：纯 C# 农田规则，与表现层独立。
- `Farming/FarmGrid`：网格坐标、鼠标平面选格、作物显示与高亮。
- `Interaction`：工具选择、范围检查、点击节流、UI 点击拦截及 HUD View。
- `Editor/PrototypeAddressablesSetup`：自动注册 AA 场景及内容构建菜单。
- `Editor/PrototypeUiPrefabBuilder`：创建缺失的 UI 预制体并序列化组件引用，保留已经存在的预制体和手动编辑。

`Magic Island > Validate UI Prefabs` 检查 Canvas 组件、UI 引用和 AA 地址。UI 目录下的预制体注册时均以文件名（不含扩展名）为地址。

主菜单背景位于 `Assets/Art/Textures/UI/MainMenuBackground.png`，由主菜单 Image 引用，随 AA 预制体一起打包加载。AspectRatioFitter 按比例铺满画布，避免拉伸。生成说明和提示词见 `Docs/MainMenuArtwork.md`。

`Magic Island > Render Main Menu Preview` 在独立预览场景中渲染实际预制体，保存至 `Logs/MainMenuPreview.png`。`Apply Cozy UI Theme` 可重新生成当前主题的两个 UI 预制体，会覆盖手动布局修改；普通配置和创建缺失预制体的菜单会保留已有修改。

岛屿地面与海岸由 AA 预制体提供，农务工具占位表现在运行时生成，玩家角色预制体及基础动画通过 AA 加载，作物的九个阶段预制体、办事处公告栏建筑、采集树木／石头、岛务员角色、鱼竿／海岸钓点和三种农场装饰已通过 AA 管理，可替换正式美术。当前已实现风格化海洋、镜头遮挡淡化和角色基础动画，暂不包含自动寻路及正式角色美术。工具和反馈说明见 `Docs/ToolsAndFeedback.md`，作物模型及信息面板见 `Docs/CropVisuals.md`。

规则检查见 `Tools/VerifyFarmState.cs`，覆盖整片农田、重复操作、缺水、生长、收获及种子耗尽。

背包说明见 `Docs/Inventory.md`；`Tools/VerifyInventory.cs` 验证跨堆叠、失败操作不丢物品、数量守恒及满背包保护。

背包右上角支持“一键整理”：合并相同 ID 的零散堆叠，按类型排列并将空格移至末尾，普通鸡蛋与优质鸡蛋分别保留。整理清空选中项，结果在睡觉时统一保存；由 GameManager 调度 InventoryManager，复用 AA / MVC InventoryPanel，默认十格和每次升级增加十格的规则不变。检查与完整规则见 [背包整理](Docs/Inventory.md#一键整理)。

背包内部支持左键拖拽整组移动、同种物品合并和不同物品交换，右键将一半数量拆到第一个空格。拖拽显示跟随预览，取消保留原状；成功后清空选择，结果在睡觉时统一保存。原子数量规则、输入保护及 AA / MVC 实现见 [拖拽与拆分](Docs/Inventory.md#拖拽与拆分)。

背包固定三排、每排十格，初始只解锁第一排，两次升级依次解锁第二、第三排。未解锁格子显示锁图标并禁用操作；超过三十格的旧档完整保留库存，使用翻页访问，每页仍为三排十格。布局及兼容规则见 [三排与锁定格子](Docs/Inventory.md#三排与锁定格子)。

储物箱支持跨库存拖拽、Shift 点击快速存取和“存入同类”，容量不足时保留剩余物品；两边在运行状态中一起提交，睡觉时统一保存。StoragePanel 上方背包显示三排十格及锁定状态，旧档通过翻页保留完整库存。详见 [快捷存取](Docs/Storage.md#快捷存取)。

商店说明见 `Docs/Shop.md`，多作物见 `Docs/Crops.md`，时间和睡觉见 `Docs/TimeAndSleep.md`，体力见 `Docs/Stamina.md`，昼夜与天气见 `Docs/Weather.md`，日期和季节见 `Docs/CalendarAndSeasons.md`，任务见 `Docs/Tasks.md`，公告栏订单见 `Docs/Orders.md`，采集见 `Docs/Gathering.md`，烹饪与食物见 `Docs/CookingAndFood.md`，NPC 与对话见 `Docs/NPCAndDialogue.md`，好感度与送礼见 `Docs/Relationships.md`，好感度事件见 `Docs/StoryEvents.md`，玩家小屋见 `Docs/Housing.md`，室内家具见 `Docs/Furniture.md`，扩建见 `Docs/HouseUpgrades.md`，养殖见 `Docs/Animals.md`。ShopPanel、SleepPanel、OrderPanel、ToolUpgradePanel、StoragePanel、CookingPanel、DialoguePanel、GiftPanel、FishingPanel、CraftingPanel、HouseUpgradePanel、CoopPanel、AnimalShopPanel、DailyReportPanel、InventoryUpgradePanel、ShippingPanel、PausePanel、MapPanel 均由 AA 加载到 PopUp 层，TaskPanel、PlacementPanel、InteractionPromptPanel 位于 Common 层，SceneTransitionPanel 位于 Top 层；共 28 个 UI 预制体，地址均为预制体名字。存档版本为 29，兼容迁移版本 1–28。

完整流程和 MVC 约定见 `Docs/GameArchitecture.md`。`SaveManager` 保存日期、农田、背包及角色位置，只在每天睡觉结算后保存；“加载游戏”恢复进度。存档路径、备份与版本规则见 `Docs/Saving.md`。`SettingsManager` 单独保存并恢复音量和全屏选项，设置面板遵循 MVC 并挂在 PopUp 层。

海滩支持拾取扇贝壳、漂流木和海玻璃，初始十个，睡觉跨日补充最多五个，总量最多十六个；未拾取物品保留，满背包不消失，可在商店出售，仍只在睡觉时保存。由 GatheringManager 管理，预制体通过同名 AA 地址加载，详见 [海滩拾取](Docs/BeachForaging.md)。

天空新增由 WeatherManager 管理的 IslandClouds AA 预制体，云朵缓慢漂移，晴天地面偶尔出现柔和云影；暂停冻结，室内隐藏，雨天和夜晚关闭独立云影，沿用当前昼夜亮度。详见 [云层与云影](Docs/Clouds.md)。

小屋窗户与门灯、码头入口及尽头两盏矮灯随傍晚渐亮，雨天提前亮灯；室内关闭户外照明，远处海洋保持夜间暗色。灯具和共享窗光材质随原小屋 / 码头 AA 依赖加载，更新资源后重新构建 Prototype Addressables。参数与验证入口见 [局部夜灯](Docs/Weather.md#小屋与码头局部夜灯)。

海洋采用较窄的不规则浅滩、较深珊瑚礁带和外海三层水深，48 处浅水小珊瑚花园与 36 处大型珊瑚群落使用柔和配色。鱼群保留合批渲染，每条鱼拥有独立跟随、转向和摆尾，鳐鱼缓慢摆鳍；沿用 OceanManager、AA 加载和暂停生命周期。详见 [海洋生态](Docs/Ocean.md)。

海鸥默认 6–10 只、螃蟹 5–9 只，按独立种子随机烘焙数量与分散位置；鸟类飞行 / 停栖与蟹类停留 / 横移各自错峰，螃蟹腿部跟随步伐。玩家靠岸会惊扰附近浅水鱼，持续接近保持警觉，离开后恢复。调整 IslandOcean.CoastalPopulation 后显式执行 Rebuild Coastal Wildlife，再构建 AA；普通配置、进出和读档不重抽，详见 [鸟蟹随机分布](Docs/Ocean.md#鸟蟹数量与活动配置)。

鸟蟹沿当前场景的实际树石、角色和摆放物检查安全路线。螃蟹受阻绕行或等待，海鸥落点被占用时换点或继续盘旋，离开警戒后错峰恢复；路线与落点不写存档。详见 [海岸避障与安全落点](Docs/Ocean.md#海岸避障与安全落点)。

停栖海鸥会错峰观察、低头啄食和转头理羽，配合单侧翅膀轻抬；玩家接近时中断并衔接现有逃离。间隔与时长可在 CoastalBird.Habits 调整，继续使用原共享模型、材质和海洋暂停时钟，详见 [海鸥停栖动作](Docs/Ocean.md#海鸥停栖动作)。

螃蟹停稳后会交替探沙觅食或抬钳观察，再按原周期短距横移；实际行走和受惊平滑中断动作。CoastalCrab.Habits 可调间隔 / 时长，双钳沿原共享网格的肩部枢轴独立转动，脚部保持贴地，详见 [螃蟹停留动作](Docs/Ocean.md#螃蟹停留动作)。

海洋生态随昼夜和天气调整活动：黄昏珊瑚鱼逐渐靠近原水域内的安全礁区，夜间减少探索；岸边海鸥少飞、雨天少觅食，螃蟹夜间更活跃。节奏平滑过渡并随暂停冻结，受惊仍优先。参数位于 IslandOcean 的 FishAnimation / CoastalRhythm，详见 [昼夜与天气活动](Docs/Ocean.md#昼夜与天气活动)。

鱼群在各自固定的栖息范围内自由巡游、觅食和探索，按种类选择珊瑚或沙底，逐鱼绕开珊瑚和礁石。各类鱼通过 OceanSurface.FishPopulations 设置群数与成员数量上下限，独立 FishSeed 决定数量及位置；调整后显式 Rebuild Fish Schools，再构建 AA，详见 [数量配置与避障](Docs/Ocean.md#数量配置觅食与避障)。

新版鱼群可用 `./Tools/VerifyOceanFish.ps1 -Seconds 600` 复现 Windows 托管长时检查；Unity 菜单 `Verify Fish Presentation And CPU` 检查插值、着色器参数及 CPU 开销，`OceanFlowVerification.Run` 验收真正打包的 AA 内容。固定步检查使用真实步起点，避免把跨越多个模拟步的弯曲路径误当作直线。验收结果与隔离说明见 [实机验收](Docs/Ocean.md#实机验收与复现2026-10-16)，Logs 保留本地。

正常游戏镜头只提交可见鱼群的插值 / GPU 姿态，屏幕外继续完整模拟，重新进入画面立即刷新。独立 Windows 验证构建及正常镜头晴雨 / 昼夜采样入口见 [可见性与 Player 验收](Docs/Ocean.md#正常镜头可见性与独立-player-验收)。

沙漠入口至内部主路的第二轮景观精修见 [沙漠景观组合](Docs/DesertComposition.md)，通过显式 Refine Desert Route Composition 烘焙。资源更新后执行 Build Prototype Addressables；普通 Setup 保留已有布局与手调。

草原整轮优化与内部植被见 [草原完整一轮优化](Docs/GrasslandRevitalization.md)。低草和花丛按斑块烘焙，建设覆盖处自动隐藏，显式入口 Revitalize Grassland In Order，普通 Setup 保留人工编辑。

草原花色群落、花草形态与分层风动见 [草原花草细化](Docs/GrasslandFlora.md)。显式 Refine Grassland Flowers And Grass 原位处理既有批次，沿用建设遮盖和统一风场；资源更新后重新 Build Prototype Addressables。

草原低草打底、花坡渐疏及树下／路肩地色见 [草原地表层次](Docs/GrasslandGroundLayers.md)，仅显式 Refine Grassland Ground Layers 烘焙，复用原风场与建设遮盖。

已有灌木叶团、半埋苔石与原位小景精修见 [草原灌木与苔石](Docs/GrasslandShrubStone.md)，显式 Refine Grassland Shrubs And Moss Stones 更新原网格，保留批次数和整组建设遮盖。

草原树型与圆润叶团精修见 [草原树冠层次](Docs/GrasslandCrowns.md)，显式 Refine Grassland Tree Crowns 更新原树冠，保留树干碰撞、共享资源和原镜头淡化绑定。

草原树冠的错峰轻风摆动见 [草原树冠风动](Docs/GrasslandCrownWind.md)，显式 Prepare Grassland Crown Wind 只给已细化叶团补风动数据，沿用统一天气风场与暂停清理。

草原合并景观树支持逐树冠遮挡淡化，沿用 CameraManager 与 AA 相机参数，同网格其他树保持正常；资源准备、细颗粒覆盖与验证见 [草原树冠遮挡](Docs/GrasslandCrownOcclusion.md)。

草原传送落点附近增加角色拨草、平滑回弹、叶形变化及统一风场草浪样区；参数随原地形 AA 加载，显式维护与验证见 [草原拨草样区](Docs/GrasslandTouch.md)。
