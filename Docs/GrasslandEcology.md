# 草原花丛与环境生态

后续的地面小动物与安全活动区见 [草原小兔](MeadowRabbits.md)；沿用本模块的拥有者、时钟与 AA 生命周期。

2026-10-18。接续 [草原收尾](GrasslandFinish.md)，在原主路、南侧支路及西湖岸的已有花丛附近加入蝴蝶／萤火虫，并为上一轮的矮草、野花补充根部固定的轻微风动。

显式入口为 **Magic Island > Art > Add Grassland Ecology (Explicit)**。普通 Setup 不执行，完整生态子树存在时保留全部后续人工改形、调色、材质和布置；孤立同名资源及其他自定义 UV0／材质在写入前拒绝覆盖。

## 美术与活动范围

Blender MCP 制作奶油黄、淡紫边的圆翼蝴蝶及暖黄色萤火虫，模型、顶点颜色、翼部标记及 Unity 坐标／绕序导出保存在 `SourceArt/GrasslandEcology`。可复现脚本为 `Tools/Art/BuildGrasslandEcology.py`，默认拒绝覆盖已有导出并保留用户打开的 Blender 场景。运行时不读取源 JSON 或 Blender 文件。

栖息点沿用 `GrasslandCompositionSetup` 的每第三处花丛及 `GrasslandFinishSetup` 中部分带花组合；每点 2 只蝴蝶、4 只萤火虫，按 256 米分区及物种合并网格，保存在原 IslandTerrain AA 依赖的 `World water ecology / Grassland insect habitats` 中。不是逐只 GameObject、脚本或粒子发射器，没有拾取、掉落、新碰撞或光源，也不改变可建设格、路线、树干或地形。

实际烘焙地面碰撞决定高度；翅膀最大折叠、个体朝向变化与横向飞行包络完整限定在原 1.05 米路肩脚印内。蝴蝶交替访花停留与短途飞行，玩家靠近时轻柔散开，离开后渐渐回归。萤火虫的低空漂移与明灭具有不同频率、幅度和相位。原主路中心及已有建设空地保持。

上一轮 `Grassland route composition`、`Grassland meadow finish` 的 12 个既有共享网格保留顶点、法线、颜色、索引、原 GUID 和 UV1–7。仅在原为空的 UV0 烘焙风动权重／相位，并切换到新的 Meadow Foliage 共享材质；未知 UV0 或手工替换材质拒绝覆盖。花茎和叶片按离地高度摆动，苔石及苔帽固定，完整风动包络仍在原预留脚印内。仅为渲染增加保守 bounds，不改碰撞。

## 时钟、天气与拥有者

沿用 `WorldWaterEcology`，其可选 `GrasslandAmbient` 引用分区 Renderer；没有新增 Manager、Update、逐帧对象／材质创建或独立随机源。OceanManager 在输入前通过原 Bind 链绑定，并在任一 AA 依赖释放前 Clear。WorldWaterEcology 的旧鱼群、古树、湖面和海洋引用保留。

蝴蝶依日照逐渐显隐，萤火虫在暮色／夜间出现；雨天活动量随原环境秒数渐变。普通暂停与模态冻结原 OceanSurface 时钟和雨天过渡，钓鱼沿用既有环境时钟继续的规则。进屋时整片昆虫子树隐藏且停止推进，返回室外恢复。草叶风动使用 WeatherManager 已有风向、阵风与冻结时钟。

每个 Renderer 的原 MaterialPropertyBlock 在 Bind 时保存，生态只添加自己需要的时钟、日照及雨量；Clear 恢复绑定前的完整属性块。共享材质和 Transform 不在运行中改写；退出与重新进入不叠加绑定。不增加 AA 配置、UI 或进度字段，存档保持 v31，唯一保存点仍为床边睡觉。

## 验证入口

`GrasslandEcologyVerification.BakeAndVerify` 在隔离工程检查旧网格几何／索引／颜色／UV1–7、全部碰撞、其他美术字节、完整活动／风动脚印、岩石固定、外部 UV0 拒绝，以及重复、普通 Setup 和人工改形／调色／材质保护。

`OceanWaterAppearanceVerification.RunGrasslandEcologyBaseline/RunGrasslandEcology` 使用实际 Packed AA、同机位与固定环境状态，拍摄清晨／白天／夜间／雨天远近景和原海洋／湖泊对照。固定截图显式设置生态雨量，另独立检验真实的渐变与暂停。运行验证实际 GPU 扇翅／漂移、夜间发光、雨天隐藏、逐像素暂停、根部固定风动、室内隐藏、原属性块恢复与不消耗玩法随机数，并实走原草原西侧／东侧路线。`StarterIslandWorldFlowVerification.Run` 回归地图、睡觉／继续、室内外与 AA 释放。

正式工程需要执行 **Magic Island > Build Prototype Addressables** 重建内容包。日志、截图和隔离项目位于忽略的 `Logs/GrasslandEcology`，不作为正式资源提交。

## 首轮烘焙验收

`Bake.log` 通过：15 处原花丛栖息点，30 只蝴蝶、60 只萤火虫，合并为 12 个原生昆虫网格，共 9,840 三角面；12 个既有花草网格补齐根部风动。原顶点／法线／颜色／索引、UV1–7、全部碰撞及其他美术字节保持；完整动画包络、苔石固定、自定义 UV0 拒绝、重复执行、普通 Setup 与人工网格／材质调整保护通过。

`BeforeRender.log`、`After.log` 通过，完成 32 张修改前和 32 张最终 Packed 画面。独立实际 GPU 截图验证翅膀／飞行、夜间漂移明灭、花草风动、雨天隐藏与逐像素暂停；同时检查雨天渐变／暂停、室内隐藏、原属性块恢复、Transform／共享材质固定及玩法随机数不变。草原西侧主路和东行支路真实 CharacterController 通行通过。深海四种光照、湖泊清晨／夜间／雨天逐像素一致；湖泊白天仅一个颜色通道差 1/255。

`WorldFlow.log` 通过，覆盖大陆道路／湖河／桥梁、原古树与水域生态、地图中文与暂停、室内隐藏、床边睡觉保存、生活区／雪地继续、返回重进和 AA 释放。合入时保留 2,279 条原预制体记录，仅按预期变更原 12 个花草 Renderer 的材质引用、生态拥有者的可选列表与父节点子引用，新增 50 条景观子树／组件记录；其余 3,291 份原文件保持本轮快照，1,647 个 GUID 无重复。正式工程资源与最终验证副本字节一致。

没有重跑完整 OceanFlow、全部存档迁移、独立 Player 构建或 GPU 性能测量；没有新增进度字段或迁移。正式工程 AA 内容仍需单独构建。

## 访花与受惊行为细化

沿用原 15 个栖息点、30 只蝴蝶、60 只萤火虫和 12 个昆虫批次，不重烘焙模型，不修改任何网格、材质或预制体。`MeadowInsectMotion` 是 WorldWaterEcology 绑定时创建、清理时丢弃的普通 C# 状态，不是组件；没有独立 Update、逐只对象、材质实例、玩法随机数或进度字段。

绑定时读取原蝴蝶 UV1 中的 `site * 2.399963 + member * 1.83` 相位，通过固定 32 槽数组寻址，当前使用 30 槽。超出容量或自定义相位的整个批次保留有界环境飞行，不重写美术或阻止游戏启动。以后增加栖息点时须同步容量、shader 常量及 Packed 验证，不能静默复用槽位。落点来自原花草网格的暖金花粉顶点，限定距栖息点中心 0.60 米内；无匹配花粉时保留中心附近的悬停。访花时随相同花草根部风场移动。

每只蝴蝶的访花周期约 11–15 秒，停留时轻合翅，短途飞行时提高扇翅速度，个体相位错开。玩家在约 2.6 米内、相近高度时产生局部惊扰，约 0.7 米内达到最大；平滑抬升与向外散开，同花丛两只各偏转 24 度，远处栖息点不响应。玩家离开后缓慢衰减惊扰，几秒内回归访花。受惊仅改变表现，不阻挡玩家或影响交互。

原全局环境秒数推进访花、萤火虫漂移与闪光；受惊与恢复只按传入的正环境秒数推进。零秒暂停即使玩家位置发生变化也不更新受惊，室内在任何推进前返回。萤火虫按个体种子改变水平速度、上下漂移频率、闪光频率和亮灭占比，保留柔和暖光。花朵、飞行与散开位置连续混合，完整体型和最大风动仍在原 1.05 米脚印及既有裁剪范围内。

原 Bind/Clear 继续保存与恢复每个 Renderer 的完整属性块；清理同时丢弃全部落点和惊扰数组，重进从无惊扰状态开始。普通 Setup 与显式 Add Grassland Ecology 均保留已有美术，无需再次执行美术重建，只需重新构建正式 Addressables 内容。

验证入口：`MeadowBehaviourVerification.VerifyAssets` 检查 shader 编译及普通 Setup／显式生态工具对全部既有美术文件的字节保护；`OceanWaterAppearanceVerification.RunMeadowBehaviour` 使用真正 Packed AA，先回归原草原通行、昼夜／雨天、风动与清理，再检查全部 30 只蝴蝶真实花粉落点、局部柔和受惊、邻居分离、逐渐回归、混合帧率下的包络、暂停时移动玩家、室内停止和重绑清理，并拍摄停留／飞行／散开／回归与夜间微光画面。

本轮 `Logs/MeadowBehaviour/Assets.log`、最终 `Packed.log` 和 `WorldFlow.log` 均通过。Packed 验证含上述行为检查、原西侧及东侧 CharacterController 通行、昼夜／雨天和花草风动；补充 48 帧真实渲染的访花—散开—回归连续演示。WorldFlow 回归大陆／湖河／桥梁、古树和原水域生态、地图、室内外、床边睡觉保存、继续、返回及重进后的 AA 清理。

原 3,348 份文件保留本轮基线字节，只修改拥有者、昆虫 shader、验证调度器和两份说明；新增一个普通状态类和一个编辑器验证类及各自 .meta。全部美术资源与最终 Packed 副本一致，1,649 个 GUID 无重复。未执行完整 OceanFlow、全部迁移、独立 Player 构建或 GPU 性能测量；正式工程 AA 内容仍需通过 Build Prototype Addressables 构建。
