# 云层与云影

天空使用 `IslandClouds` AA 预制体，地址与预制体同名，由 WeatherManager 在岛屿加载后实例化。24 团云朵在 42 米以上缓慢移动，使用离线生成的云团网格和可编辑材质，不会挡住近景游戏镜头或鼠标射线。

晴天时，太阳的 Directional Light Cookie 在地面投下稀疏、柔和的移动云影。云影最深处减少约 22% 的太阳直射光，大部分区域保持完整阳光；环境光、太阳强度、太阳方向以及月光参数沿用原有昼夜系统。阴雨天取消独立的晴天云影，夜间太阳关闭时也不会增加亮度。云朵会随白天、雨天、夜间调整颜色。

暂停停止云朵和云影移动。进入室内隐藏云朵并恢复原有太阳 Cookie，回到岛上继续漂移。退出游戏和取消加载会释放 AA 实例、恢复 Cookie、Cookie Size 以及太阳位置，重复进入不会叠加云层。

## 资源与调整

- `Assets/Art/Prefabs/Environment/IslandClouds.prefab`：云团数量、位置、形状比例，CloudField 的 Wind 长度为基准云速，FieldSize 为循环范围。正常游戏的漂移方向与速度倍率由 WeatherManager 的统一风力提供，见 [植被风动](VegetationWind.md)。
- `Assets/Art/Meshes/Weather/Cloud Cluster.asset`：云团网格。
- `Assets/Art/Materials/Weather/Sky Cloud.mat`：云朵材质。
- `Assets/Art/Textures/Weather/Cloud Shadows.png`：云影 Cookie，Alpha 越低遮光越多。纹理使用线性颜色、Repeat，边缘连续。
- `Assets/Art/Shaders/SkyCloud.shader`：柔和边缘与昼夜云色。

CloudSetup.Ensure 只补全缺失资源，保留已有预制体、网格、贴图和材质的调整。资源在编辑器生成后随 AA 打包，运行时不会重建模型、贴图或材质。

云层按每帧风速累计位移，晴雨切换与阵风改变速度时保持位置连续，不再以“当前风速 × 总时间”重新计算位置。

## 生命周期与验证

GameManager 在 Ocean.Load 后调用 `yield return Weather.LoadClouds(world)`，检查 `Weather.LastError`。每帧 Weather.Tick 统一更新昼夜外观、暂停状态和室内状态；Weather.Clear 负责清理。

`CloudFlowVerification.VerifyAssets()` 检查地址、云团高度、无碰撞及 Cookie 的稀疏柔和遮光。`VerifyRuntime(game)` 检查移动、暂停、室内、雨夜和光照参数保持。`CapturePreviews(game)` 使用真实游戏镜头对比有无 Cookie，确认地面云影实际进入渲染，并保存地面与天空截图。
