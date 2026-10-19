# 草原树冠层次

入口：`Magic Island > Art > Refine Grassland Tree Crowns (Explicit)`，实现为 `GrasslandCrownSetup`。这是独立的一次性美术细化，普通 Setup 不执行。

以已有 GrasslandGrove 标记和原树冠形状识别草原岛的绿色树冠，按原树干锚点统一树型。高挑、宽冠、紧凑三种比例与叶团方向由锚点确定；每个旧球形叶团替换为三簇圆润叶团，使用连续平滑法线和柔和灰绿／嫩叶色。各簇均在原树冠椭球范围内，避免向道路或相邻实体扩张。不更换其他群系或灵辉古树。

只在原 `Art Landscape` 共享网格中移除选中的树冠面并追加细化叶团；旧顶点及其全部频道原样保留，原树干、石块与未选中面索引不变。追加顶点继承原树冠 UV 与透明度，不分配新 UV 频道。保留网格 GUID、保守 bounds、原 Renderer、共享材质、完整预制体、原实体树干碰撞和建设规则；无新对象、组件、逐帧逻辑、材质副本或存档字段。

原镜头淡化绑定保持不变：可交互树木仍由 CameraManager／CameraOccluder 管理。合并景观树冠沿原 Landscape Renderer 渲染，本次不把整区景观绑定为一个淡化目标，也不新增风动或独立时钟。

后续树冠风动为独立显式步骤，见 [草原树冠风动](GrasslandCrownWind.md)；仅使用本轮新增叶团原空白 UV0，原树冠细化工具不覆盖其数据。

导入器追加 `MagicIsland.GrasslandCrown.v1`，保留原 Grove 标记；完整标记后重复入口保留后续手调，部分完成则拒绝继续。未知元数据、材质、变换、静态网格格式、缺失树干或与碰撞共用的网格在写入前拒绝。全部计划完成后才写资源，异常恢复原网格与 meta。更改设计需明确安排新迁移，不能清除标记覆盖手调。

验证在 `Logs/PrototypeValidationProject` 隔离副本执行，不使用正式进度文件：

- `MagicIsland.EditorTools.GrasslandCrownVerification.BakeAndVerify`：保留原网格对照，核对新叶团范围／单位法线、旧顶点／UV0–7／未选中面／GUID／bounds；对其余 Art（包括地形、碰撞、材质和完整预制体）逐文件比较，验证未知元数据拒绝、后续手调及重复／普通 Setup 保护。
- `MagicIsland.EditorTools.GrasslandCrownVerification.After`：构建 Packed Addressables 并从 MainMenu 进入，在正常游玩镜头的 42°／size 7 下拍摄三种树型及昼／暮／雨同机位前后对照；读取实际 AA 树冠数据并检查共享资源拥有关系，随后执行原草原真实通行、生态、建设遮盖／回收、床边睡觉、五岛读档与退出清理流程。

截图和测试日志写入忽略的 `Logs/GrasslandCrowns`。运行项目前重新执行 **Build Prototype Addressables** 以更新实际加载的资源。

2026-10-19 验收结果：21 棵原树、42 个旧叶团细化为 126 簇，写回原 8 份网格及其 meta，预制体 2,878 条记录逐字节不变；无新增 Renderer 或批次。新增静态叶团合计 32,760 顶点／55,440 三角面。烘焙保护检查与 Packed 全流程均以退出码 0 完成，包含 6,936 次实际角色步进、原生态／建设遮盖、床边睡觉、五岛读档及释放。三种树型的昼／暮／雨前后对照已检查。存档字段和版本不变，正式进度未参与测试。
