# 作物阶段模型与农田信息

FarmManager 负责作物模型的 AA 加载、句柄持有和释放。GameManager 在进入游戏的 InitializingGame 阶段等待所有阶段模型加载成功，再初始化农田和恢复存档。FarmGrid 根据地块 CropId 和生长进度显示模型，不自行加载资源。加载失败时卸载游戏场景、释放已加载模型并返回主菜单，可再次进入重试。

## 阶段与资源

- 幼苗：Growth 为 0，即刚播种或尚未完成一次浇水跨日。
- 生长中：Growth 大于 0 且小于 RequiredGrowthDays。
- 成熟：Growth 等于 RequiredGrowthDays，可以收获。生长天数为 1 的作物跨日后直接进入成熟阶段。

阶段由已有进度推导，存档当前为版本 9，不新增表现字段。未浇水不会切换阶段，浇水或同一阶段内成长不会重建模型；仅在种植、阶段切换、收获或改变地块作物时替换实例。旧实例先隐藏再销毁，避免同帧显示重叠。收获后清空模型，再次播种从幼苗开始。

九个低多边形预制体位于 `Assets/Art/Prefabs/Crops`，AA 地址与文件名一致，在 Crop Models 分组注册：

| 作物 | 幼苗 | 生长中 | 成熟 |
| --- | --- | --- | --- |
| 芜菁 | TurnipSeedling | TurnipGrowing | TurnipMature |
| 番茄 | TomatoSeedling | TomatoGrowing | TomatoMature |
| 玉米 | CornSeedling | CornGrowing | CornMature |

芜菁有浅色根部与叶簇，番茄从绿果成长为红果植株，玉米具有细长茎叶、黄色果穗与顶部穗须。模型使用烘焙的平面法线网格和 Standard 材质；网格位于 `Assets/Art/Meshes/Crops`，材质位于 `Assets/Art/Materials/Crops`，作为预制体依赖一起打包。

CropCatalog 的 SeedlingPrefabAddress、GrowingPrefabAddress、MaturePrefabAddress 定义三个阶段的 AA 地址，启动时要求地址非空，进入农场时校验加载结果包含 Renderer 且不含 Collider。模型根节点位置为地块中心地面，单位尺度为米；使用纯视觉模型，鼠标仍通过农田平面选择地块。

FarmManager 使用 `Addressables.LoadAssetAsync<GameObject>` 预加载模型并保留句柄，同一地址只加载一次；地块实例通过已加载预制体创建，所有实例存续期间保留资源引用。离开场景后清除实例并释放句柄，加载中清理也会在操作完成后释放。

编辑器的 CropVisualPrefabBuilder 只创建缺失的默认预制体、网格和材质，只补空的默认阶段地址，保留已有编辑。Shape 保留用于旧配置兼容，MatureColor 用于首次生成果实材质，运行时外观以阶段预制体为准。替换正式美术时注册新预制体并调整三个地址即可；无需改变种植、成长、交易或存档逻辑。

## 指向农田的信息

InteractionController 解析农田射线和距离，提供 HoverIndex、HoverInRange。CropInfoController 读取当前地块、物品名称及农务提示，更新 CropInfoModel，由 CropInfoView 渲染。`CropInfoPanel.prefab` 位于 `Assets/Art/Prefabs/UI`，UIManager 通过 AA 地址 CropInfoPanel 加载到 Common 层。

右侧信息卡显示作物名称、幼苗／生长中／已成熟、成长进度、今天是否浇水及操作提示。超出操作距离时仍可读取作物信息，并提示需要靠近。空地、鼠标指向 UI／商店／床、打开弹窗时隐藏；所有 Graphic 和 CanvasGroup 均不阻挡鼠标。面板随游戏退出释放。

## 验证

- `Tools/VerifyCropStages.cs`：51 项断言，覆盖 1／3／5／7 次成长、缺水、各阶段边界、读档推导、成熟保持与再次播种。
- `CropVisualFlowVerification.Run`：在独立 Unity 工程验证 Packed AA 九个模型、失败清理与重试、阶段替换、同阶段实例复用、浇水、失败操作、收获、重新播种、混种存档恢复和句柄释放；同时检查信息面板 MVC、中文、射线、距离、UI 拦截和弹窗隐藏，生成阶段预览。
- `GameFlowVerification.Run`：回归原有菜单、背包、工具、商店、跨日及存档流程。
