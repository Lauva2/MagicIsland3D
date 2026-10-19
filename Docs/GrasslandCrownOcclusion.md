# 草原树冠遮挡

草原岛已细化的 21 棵景观树继续使用原 8 个合并 Renderer，由 CameraManager 在镜头跟随之后统一检测角色下身、胸部和头部视线。挡住玩家的树冠按 GameConfig 的 CameraOccludedOpacity / CameraFadeDuration / CameraOcclusionMargin 平滑淡化，离开后恢复。同网格其他树、树干与石头不受影响。

## 资源和所有权

显式菜单 `Magic Island > Art > Prepare Grassland Crown Occlusion (Explicit)` 在原 IslandTerrain AA 预制体的 8 个景观对象上补充 LandscapeCrownOccluder，不创建新对象、Renderer、材质、配置或 AA 地址。每棵树由既有 UV0 风动相位识别，离线聚合叶片 bounds 并包含最大风动余量，不改任一网格频道、顶点、碰撞或树木位置。

工具先验证全部源数据和独占 Renderer，再保存原预制体；保存失败还原原文件。未知风动元数据、材质、重复 Renderer 所有者、不完整组件迁移均拒绝覆盖。完整版本 1 的再次执行不写文件，保留合法手调 AllowFade 和包含完整叶冠的更大 Bounds；普通 Setup 不运行该工具。

运行中组件只持有树冠展示记录与一个固定上限 32 项的 MPB 数组，无独立 Update 或时钟。CameraManager 的原调用顺序、视线与流程开关同时调度这条路径。首次淡化捕获原实例 MPB，恢复、组件禁用、对象卸载及 Manager.Clear 立即还原；所属 AA 实例销毁时移除注册，不保留世界引用。不要让其他系统同时写这些景观 Renderer 的 MPB 或给它们添加 CameraOccluder。

## 着色表现

Island Biome Details 根据原叶团相位读取各树覆盖率，以稳定屏幕像素点阵覆盖实现平滑淡化；近看可见细颗粒，避免整个合并网格进入透明排序。原阴影仍保留，并继续与原 WeatherManager 风动形变一致。没有改变原地面、其他群系、模型、共享材质参数或 AA 资源所有权；淡化状态不写存档。

## 验证入口

在独立工程执行 GrasslandCrownOcclusionVerification.BakeAndVerify：检查 21 树 / 8 Renderer、部分迁移拒绝、重复执行及普通 Setup 保留手调，并逐文件核对其他美术资源不变。

再执行 GrasslandCrownOcclusionVerification.After，构建真正 Packed AA 并运行原五岛流程：在真实地面无碰撞的落点，以默认镜头检查遮挡、过渡、同网格其他树保持不透明、GPU 画面变化、风动、共享材质与碰撞不变；检查跟随偏差、剔除、禁用、主动关闭、Clear、原 MPB 恢复与画面恢复（允许重新注册 Renderer 后至多 4 个像素的单通道 1/255 GPU 舍入差异），并回归原 CameraOccluder fixtures。完整流程继续覆盖 6936 步通路、进屋、睡觉生成第 2 天档案、五岛读档、返回及再次进入和 AA 释放。所有存档写入隔离测试目录。

验证输出在 Logs/GrasslandCrownOcclusion，资源发布须核对原预制体记录只有 8 个新增组件引用和对应组件记录，其他原记录不变。团队同步资源后重新 Build Prototype Addressables。
