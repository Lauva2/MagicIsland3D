# 沙漠入口至内部主路景观组合

2026-10-19。接续草原收尾，沿原西侧沙漠道路进行第二轮局部美术精修。首轮群系与原入口分别见 [BiomeArtSequence.md](BiomeArtSequence.md)、[StarterIslandWorld.md](StarterIslandWorld.md)。

显式入口为 **Magic Island > Art > Refine Desert Route Composition (Explicit)**，实现 `DesertCompositionSetup`。普通 Configure／Setup 不执行，不要求重建整岛、道路或原沙漠布局。

## 模型、配色与安全范围

沿 `(-180,400) → (-280,500) → (-390,565) → (-420,760) → (-360,816)` 的既有道路，交错设置疏密不同的小簇。复用原 `Sandstone`、`Cactus` 网格及 `SourceArt/GrasslandComposition` 的 Blender 矮草模板；仅生成新的分区组合网格，不覆盖模板。组合包含高低砂岩与碎石、大小仙人掌、石旁干草、疏草小簇，变化朝向、比例与伴生位置，保留开阔沙地。

新砂岩在原模板上收窄不同高度的轮廓，使用低反差的暖陶土／浅砂岩层理；仙人掌使用灰绿与浅橄榄色，花端保留淡粉色；草丛从入口灰绿向内部干草色过渡。局部原 Land 顶点色向柔和暖沙混合，原 Landscape 仅为符合原圆润砂岩拓扑／色域的组件柔化层理。原树干、树冠、地面／景观几何、法线、三角面、bounds、全部 UV0–7 和草色权重保持。古迹周边、海岸和北侧雪地逐渐退出本轮范围。

新组合使用 1.05 米保守方形脚印，密集检查原道路、全部相交建设格及岛内范围；逐顶点读取原地面碰撞贴地，避开原入口／首轮群系小簇及实际固定景观。只占原不可建设路肩，道路中心保留超过 3 米净空。新网格按 128 米分区合并在 `IslandTerrain / … / Baked landscape / Desert route composition`，共用原 Biome Details 材质、Ignore Raycast 层，不增加碰撞、拾取、灯光、运行脚本、Update 或材质副本。

## 编辑保护与交接

本轮不再分配 Mesh UV 频道。原网格通过导入器 `userData=MagicIsland.DesertComposition.v1` 记录一次性完成状态；非空自定义元数据在写入前拒绝覆盖。完整计划、模板、地面、保守脚印和同名资源检查通过后才写入。已有完整子树直接保留，包括后续人工改形／调色／布置；孤立同名网格或不完整子树拒绝覆盖。保留原 GUID 和预制体记录，不清除旧工具标记。显式重建整岛会重做原大陆网格，本工具不自动重做已覆盖的精修。

没有新增配置、UI、物品、玩法或持久化字段，存档保持 v31。资源合入后执行 **Magic Island > Build Prototype Addressables** 更新正式内容包。

## 验证入口

`DesertCompositionVerification.BakeAndVerify` 只在隔离工程执行，检查原几何／法线／三角面／bounds／全部八组 UV、未选中颜色、完整碰撞集合、原美术哈希、新组合完整脚印与非退化三角面，覆盖自定义元数据拒绝、重复入口、普通 Setup 和后续手工编辑保留。

`OceanWaterAppearanceVerification.RunDesertCompositionBaseline/RunDesertComposition` 构建真实 Packed AA，记录清晨／白天／夜间／雨天的入口、弯道、内部、北端、总览及四处原游玩镜头，并保留草原、林缘、海洋和湖泊对照。运行验证核对实际打包模型、颜色和 CharacterController 从入口到内部的通行。`StarterIslandWorldFlowVerification.Run` 覆盖大陆、地图、室内、床边睡觉、继续、返回重进及 AA 清理。验证副本、日志及截图放在忽略的 `Logs/DesertComposition`。

## 本轮验收

隔离工程 `BakeVerified.log` 通过：23 处组合、7 个共享材质批次、25,908 个三角面；17 份原 Land／Landscape 网格共 3,789 个顶点改色。原几何、法线、三角面、bounds、UV0–7、未选中颜色、完整碰撞集合和无关美术字节保持。自定义导入器元数据拒绝、重复入口、普通 Setup 与后续手工改形／调色保留均通过。验证脚本曾修正预制体碰撞查询和 `.meta` 还原导入方式；最终只以该次通过日志及配套资源为准。

`Before.log` 和 `Packed.log` 均以退出码 0 完成，生成 60 张修改前／60 张最终 Packed 对照；已检查四处正常游玩镜头以及白天、清晨、夜间、雨天代表视角。实际打包模型的位置、顶点数量、三角面数量和 Color32 配色与烘焙源一致，CharacterController 从入口到沙漠内部及北端的实走通过。深海、湖泊和草原四种光照逐像素一致；珊瑚白天两处颜色通道及林缘白天／雨天分别一／三处通道差 1/255。浅滩跨次运行存在局部像素差异，未作为完全一致的断言；原海洋资源和运行逻辑保持。

`WorldFlow.log` 退出码 0，覆盖大陆道路、湖河／桥梁、地图中文与暂停、室内隐藏、床边睡觉、生活区／雪地继续、返回／重新进入及 AA 清理。正式合入前核对原 2,333 条预制体记录，仅增加一个父节点子项和 30 条新记录；49 份目标资源／meta 与最终验证副本字节一致，1,669 个 GUID 无重复，原有 AA Profile 未提交修改保留。其他配置、UI、物品与存档仍为原基线，新增内容没有运行组件或逐帧逻辑。

没有运行独立 Player 构建、GPU 性能测量、完整原海洋 OceanFlow 或全部版本迁移。正式工程的 AA 内容包仍需执行 **Build Prototype Addressables** 更新。验证副本与截图保留在忽略的 Logs，不提交。
