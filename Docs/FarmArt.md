# 农田与作物表现

农田使用 AA 地块预制体 `FarmPlot`，包含圆润边缘的草地、翻耕土壤、五道田垄和施肥颗粒。未开垦时显示草地与少量草尖；翻土后显示浅棕干土，浇水后切换深棕湿土。施肥保留当天干湿状态并增加颗粒细节，收获后恢复普通土壤。每天睡觉推进生长后，湿土恢复为干土。

## 管理与资源

`GameConfig.FarmPlotPrefabAddress` 指定地块地址。FarmManager 在初始化农田前预加载地块及作物阶段模型，持有 AA 句柄；FarmGrid 只创建实例并按 FarmCell 切换外观。退出、加载失败和取消时由 FarmManager 清理实例及依赖。

- 地块：`Assets/Art/Prefabs/Crops/FarmPlot.prefab`，AA 地址 `FarmPlot`。
- 农务特效：`Assets/Art/Prefabs/Effects/FarmActionEffects.prefab`，AA 地址 `FarmActionEffects`。
- 锄头 / 水壶 / 斧头 / 镐子：`Assets/Art/Prefabs/Tools/IslandFarmTools.prefab`，由 FarmActionEffects.HeldToolsPrefab 引用并随同一 AA 句柄加载；编辑与生命周期见 [工具动作](ToolsAndFeedback.md)。
- 地块网格与材质：`Assets/Art/Meshes/Farm`、`Assets/Art/Materials/Farm`。
- 作物沿用九个 AA 阶段地址，网格与材质仍位于 Crops 文件夹。

FarmPlotVisual 保存地块材质及可视部件引用，按已有 Tilled、Watered、Fertilized 数据渲染。保持每格 1.25 米及原鼠标平面拾取规则；地块和作物没有碰撞，所有纯视觉部件在 Ignore Raycast 层。

未开垦地块的 Plot Grass 与 Grass Tips 材质使用 Seasonal Grass 着色器，读取 SeasonConfig 的草地配色，跟随岛屿草地显示四季。保留原材质 GUID、纹理和颜色，已耕干土 / 湿土、肥料及作物保持原表现；FarmArtSetup 只在创建缺失草地材质时选择该着色器，重复配置不覆盖已有材质编辑。四季流程见 [日期与季节](CalendarAndSeasons.md#四季环境配色)。

## 作物与反馈

芜菁增加紫色肩部、根尖和叶脉；番茄增加支架、绑带、成长阶段小花及成熟果实亮色细节；玉米增加穗粒。叶片使用弯曲叶形网格，幼苗、生长中和成熟仍由原成长进度推导。同阶段刷新复用模型，收获隐藏并清除模型，再播种显示幼苗。

`GameConfig.ToolEffectsPrefabAddress` 指定农务特效。GameManager 等待 ToolManager 加载成功后才启用玩法；ToolPresentation 创建已加载的特效预制体实例，ToolManager 持有依赖并推进表现。锄头扬起土尘，播种撒出种粒，水滴落地变为扁平水花，收获显示金色闪光。采集和施肥沿用相同入口；斧头和镐子分别使用木屑、碎石模板，见 [树木、岩石与采集反馈](GatheringArt.md)。

粒子采用可编辑网格与材质，最多复用 48 个实例。打开背包或其他弹窗时暂停，关闭后继续；失败操作不产生特效。清理表现后释放 AA 依赖。表现不写入存档，种植、体力、收获及睡觉保存规则沿用原逻辑。

## 编辑与验证

FarmArtSetup 只创建缺失地块、特效、网格及材质。CropVisualPrefabBuilder 为默认作物执行一次美术迁移，CropArtVisual 标记版本；同版本再次配置保留修改后的预制体、材质与地址。可继续编辑这些预制体，或将配置地址换为正式美术资源。

FarmArtFlowVerification 在隔离项目构建实际 AA 内容，检查地块与九个作物阶段、鼠标拾取、翻土／播种／浇水／施肥／收获／再播种、特效复用与暂停、三次睡觉成长与湿土恢复、读档重建，以及白天不保存和资源清理，并生成预览。基础规则使用 VerifyFarmState、VerifyCropStages、VerifyToolGate 验证。
