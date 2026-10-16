# 四季地表细节

春季零散小花和嫩草，夏季更茂密的草簇，秋季树下落叶和少量飘叶，冬季稀疏枯草与零星薄霜。薄霜是地面美术，不新增雪天天气或玩法状态。作物、果实、农田成长和睡觉存档规则保持不变。

## 资源与管理

- `Assets/Art/Prefabs/Environment/SeasonalGroundDetails.prefab`，AA 地址 `SeasonalGroundDetails`；作为 IslandTerrain 的嵌套预制体随地形加载，不另开异步加载链。
- `Assets/Art/Meshes/Seasons`：32 份共享四季网格，8 种组合，每种含春 / 夏 / 秋 / 冬，包含顶点颜色与叶片运动标记。
- `Assets/Art/Materials/Weather/Seasonal Ground.mat` 与 `SeasonalDetails.shader`：共用一份材质，保留正常日夜照明。

使用独立 System.Random 按 ScatterSeed 随机烘焙，不消耗玩法随机数。候选位置在草地区域连续取样，保持至少 1.15 米间距，不使用规则格点。候选总数在 MinimumPatches / MaximumPatches（默认 85–110）内随机；默认种子 47831 当前得到 107 簇。朝向随机 0–360°，整体尺寸在 MinimumScale / MaximumScale（默认 0.75–1.15）内随机，每簇选择随机网格组合。花朵、草叶、落叶和薄霜的局部位置、数量及形状参数同样使用独立的种子随机序列。

各斑块只有一个 Renderer，按季节切换已有共享网格；运行时不生成网格、克隆材质或逐片创建叶子。候选位置不等于可见数量，实际显示会避让当前布局。可在预制体中调整随机种子、数量 / 尺寸范围，以及位置、网格、TreeId、FootprintRadius 与材质；扩大美术范围时同步扩大安全包络并验证完整占地。普通加载、读档和进出小屋均复用烘焙结果，不重新抽取。

SeasonalDetailsSetup 只创建缺失资源、补 AA 地址和地形引用；同一资源存在时保留人工模型、配色及布局。首次接入 IslandTerrain 只新增嵌套子物体和引用，不重建海岸、海床、道路、碰撞或已有景观。更新后需要 Build Prototype Addressables；无需重建海洋生态。

明确需要重新随机布置时，使用 **Magic Island > Rebuild Seasonal Ground Details**。此显式操作按预制体中的随机设置重烘焙布置和 32 份共享网格，保留资源 GUID，但会覆盖这些地表资源的手工编辑；常规 Configure 不调用它。修改种子生成另一套分布，种子和参数相同时生成相同结果。

## 可见性与安全边界

GameManager 在设施、动物、出货箱及已保存建设恢复完成后调用 TerrainManager.BindSeasonalDetails，随后才恢复玩家区域并开放操作。地表细节复用 PlacementState.CheckLocation 的农田、NPC 路线、岸线、码头及占地规则，并检查实际道路、鸡舍预留地和场景碰撞；全程不修改摆放记录，也不阻挡新的建设。

PlacementManager 的 Placed / Recovered 只在事务成功后通知。地表细节在这两个事件中重新判定遮盖，同格或相邻果树预留范围内隐藏，回收后恢复；旧建设在初次绑定时执行同一判断。所有斑块位于 Ignore Raycast，无 Collider，因此不挡走路、点击、种植或回收。

WeatherManager.Tick 提供 CalendarManager 的当前季节与室内状态。初始化、床边睡觉跨季、冬季读档和重新进入选择对应网格；进入小屋隐藏整组，室外恢复。飘叶读取同一个 `_IslandWindTime`，暂停和室内冻结；风向来自天气。每个关联斑块最多烘焙两片飘叶，不新增 CPU 粒子或单独 Update。

TreeId 对应稳定采集节点 ID。监听 GatheringState.Changed，在树冠耗尽时关闭该斑块飘叶，地面已有落叶保留；树木重新长出时恢复。未知节点不产生飘叶。切换季节只选择网格，所有选择、可见性和运动时间均不写入存档，版本保持 29。TerrainManager.Clear 在释放地形 AA 前解除摆放与采集订阅；失败及返回菜单走同一清理路径。

## 验证

`SeasonalFlowVerification.Run` 构建真正 Packed AA，复用四季床边睡觉 / 读档 / 退出流程，并执行 SeasonalGroundVerification：

- 检查同名 AA、嵌套引用、32 份小网格、干燥草地位置、完整叶片 / 风动包络及无碰撞；重复 Setup 保留所有地表资源。
- 检查数量范围、连续随机坐标、不同朝向 / 尺寸和至少 1.15 米间距。
- 实际 GPU 飘叶两帧变化、同一暂停时间画面一致、砍树后停止飘叶。
- 最大 12×12 农田及对应实际道路避让，旧建设初次恢复时遮盖且记录不变；实际创建建设、即时隐藏斑块、回收恢复，无白天存档；进入室内隐藏。
- 连续四次床边睡觉跨越四季 / 年份、冬季读档、返回和再次开局。

验证使用独立 Company / Product 和 Logs 下的新存档目录，不读取实际玩家进度。预览为 Logs/SeasonSpring.png 等四季全景与 SeasonGroundLeafA / B / Pause.png。

2026-10-16 实际执行：最终随机分布版本的 SeasonalFlowVerification、PauseSleepFlowVerification 均以 Packed AA 通过；TerrainCoastFlowVerification 在本次地表接入后通过海岸移动、鼠标过滤、旧摆放及码头钓鱼。最终再次核对 Art / Config 与源工程字节一致，保留每季 28 天和存档 v29。生成的全景 / 近景、日志与验证副本仅在忽略的 Logs 下；本次未执行独立 Player 构建或全套纯规则验证。源工程和协作者同步后需要重建 AA 内容。最终近景拼图为 Logs/SeasonalGroundComparison.png。
