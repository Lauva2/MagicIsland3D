# 云层与云影

天空使用 `IslandClouds` AA 预制体，地址与预制体同名，由 WeatherManager 在岛屿加载后实例化。24 团云朵在 42 米以上缓慢移动，使用离线生成的云团网格和可编辑材质，不会挡住近景游戏镜头或鼠标射线。

晴天时，太阳的 Directional Light Cookie 在地面投下稀疏、柔和的移动云影。云影使用周期噪声与域偏移形成大块、不规则的连续轮廓，最深处减少约 8% 的太阳直射光，大部分区域保持完整阳光；环境光、太阳强度、太阳方向以及月光参数沿用原有昼夜系统。阴雨天取消独立的晴天云影，夜间太阳关闭时也不会增加亮度。云朵会随白天、雨天、夜间调整颜色。

暂停停止云朵和云影移动。进入室内隐藏云朵并恢复原有太阳 Cookie，回到岛上继续漂移。退出游戏和取消加载会释放 AA 实例、恢复 Cookie、Cookie Size 以及太阳位置，重复进入不会叠加云层。

## 资源与调整

- `Assets/Art/Prefabs/Environment/IslandClouds.prefab`：云团数量、位置、形状比例，CloudField 的 Wind 长度为基准云速，FieldSize 为循环范围。正常游戏的漂移方向与速度倍率由 WeatherManager 的统一风力提供，见 [植被风动](VegetationWind.md)。
- `CloudField.ShadowScale`：只控制云影循环跨度相对天空云场的比例，默认 3（128 米云场对应 384 米云影跨度，Inspector 范围 1–6）。天空云朵保持原位置、数量与 128 米循环。云影位移和 Cookie Size 使用同一扩大后的周期，跨界接缝连续，不能仍按旧云场周期重置太阳投影位置。它是 AA 美术参数，不写进度存档。
- `Assets/Art/Meshes/Weather/Cloud Cluster.asset`：云团网格。
- `Assets/Art/Materials/Weather/Sky Cloud.mat`：云朵材质。
- `Assets/Art/Textures/Weather/Cloud Shadows.png`：云影 Cookie，Alpha 越低遮光越多。纹理使用线性颜色、Repeat，边缘连续。
- `Assets/Art/Shaders/SkyCloud.shader`：柔和边缘与昼夜云色。

CloudSetup.Ensure 只补全缺失资源，保留已有预制体、网格、贴图和材质的调整。资源在编辑器生成后随 AA 打包，运行时不会重建模型、贴图或材质。

已有项目仅通过 **Magic Island > Art > Refine Cloud Shadows (Explicit)** 重应用柔和云影，不重建天空网格或重排云朵。新建缺失 Cookie 时使用相同算法；普通 Setup 不覆盖已有纹理。原 PNG、`.meta` GUID 与 AA 引用保留，导入设置仍为线性、Repeat、Trilinear、mipmap 与无压缩。烘焙在原纹理 importer 的 userData 追加 `CloudShadowSoft:1:<SHA256>` 标记并保留原 userData；已标记时重复操作保留纹理以及后续手绘。需要更新烘焙规则时应明确迁移标记，不能由普通 Setup 静默覆盖。默认最大遮光从旧版约 22% 降至 8%，不新增材质副本、灯光、对象、逐帧纹理生成或独立时钟。

云层按每帧风速累计位移，晴雨切换与阵风改变速度时保持位置连续，不再以“当前风速 × 总时间”重新计算位置。

## 生命周期与验证

GameManager 在 Ocean.Load 后调用 `yield return Weather.LoadClouds(world)`，检查 `Weather.LastError`。每帧 Weather.Tick 统一更新昼夜外观、暂停状态和室内状态；Weather.Clear 负责清理。

`CloudFlowVerification.VerifyAssets()` 检查地址、云团高度、无碰撞及 Cookie 的稀疏柔和遮光。`VerifyRuntime(game)` 检查移动、暂停、室内、雨夜和光照参数保持。`CapturePreviews(game)` 使用真实游戏镜头对比有无 Cookie，确认地面云影实际进入渲染，并保存地面与天空截图。

本次入口 `CloudShadowVerification.BakeAndVerify` 在隔离工程检查原天空层级／位置／网格／材质、地形与海洋、PNG GUID、周期噪声接缝、重复执行／普通 Setup／后续手绘保留。`OceanWaterAppearanceVerification.RunCloudBaseline/RunCloud` 使用真正 Packed AA 渲染草原、沙漠、雪地、海滩及海湖对照，覆盖清晨／白天／夜间／雨天；关闭 Cookie 的白天对照用于确认只改变云影，最终运行同时检查云影扩大周期跨界、暂停、室内／雨夜及 Detach 恢复太阳参数。全岛睡觉／继续／返回重进与清理由 `StarterIslandWorldFlowVerification.Run` 覆盖。日志与预览放在忽略的 `Logs/CloudShadow`。正式工程体验前仍需执行 **Magic Island > Build Prototype Addressables**。

2026-10-18 本轮实际验证通过：资源烘焙、周期接缝、重复／普通 Setup／后续手绘保留，以及 Packed AA 下风向漂移、384 米周期跨界、暂停、室内／雨夜与恢复太阳参数。35 张修改前／35 张最终画面对照已经检查；关闭 Cookie 的草原／沙漠／雪地白天对照及雨夜静态地面对照，最多 1 个颜色通道有 1/255 的 GPU 舍入差异。PNG 量化后最大遮光约 7.84%，约 80.34% 纹理区域保留超过 99% 直射光透过率。雪地对比见 `Logs/CloudShadow/ComparisonSnow.png`（左前右后）。

原 `BeachCloudFlowVerification.Run` 回归通过，包括真实云影／无云影渲染对照、暂停／进屋、海滩拾取、睡觉保存与继续、返回重进和 AA 清理；`StarterIslandWorldFlowVerification.Run` 全流程通过，包含大陆天气跟随、原路线／湖河／桥梁实际通行、水域生态、地图／暂停、床边睡觉、原生活区／雪地继续及返回清理。最终生成工具再验证 LF／空白保留：PNG 与已打包验收的像素文件逐字节一致，预制体相对原文件仅增加 `ShadowScale: 3`，原 importer 仅追加 userData 标记。合回 7 份已有文件及 4 份新增源码／meta；其余 2,806 份已有美术／脚本／编辑器文件保持本轮快照字节，1,586 个资源 GUID 无重复。未增加配置、UI 或进度字段，存档仍为 v30；未重跑完整原海洋 OceanFlow、全版本迁移、独立 Player 或 GPU 性能测量。
