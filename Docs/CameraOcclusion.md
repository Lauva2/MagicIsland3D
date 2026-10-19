# 镜头遮挡淡化

角色被树木、建筑或室内墙壁遮挡时，相应模型平滑变为半透明；角色离开后恢复原材质。默认约 0.2 秒、不透明度 22%，不改变碰撞、采集、进门或公告栏操作。

## 配置与管理

- `CameraManager` 统一检测和调度，在镜头跟随后执行。`GameManager` 仅在 Playing 状态启用淡化，加载、进出小屋、返回主菜单时立即恢复。
- AA 的 `GameConfig` 提供 `CameraOccludedOpacity`、`CameraFadeDuration`、`CameraOcclusionMargin`，无需增加配置地址或修改存档。
- 模型上添加 `CameraOccluder`，在 `Renderers` 中明确指定参与淡化的 Renderer。`AllowFade` 可禁用该组效果；禁用组件或对象时立即恢复。不要让两个组件管理同一个 Renderer。
- 当前已配置橡树完整植株、办事处、小屋及扩建外观、鸡舍、室内原有及扩建墙壁、窗框和阅读书架。地板、床、作物、人物、公告栏内容、标牌和待收鸡蛋不参与。
- `CameraOcclusionSetup` 在 AA 配置流程中为缺少组件的原型资源补齐配置，已有组件作为迁移标记，重复执行保留 Renderer 列表和人工设置。

## 检测与材质

从镜头朝角色身体下部、胸部、头部各发出一条视线，检查已配置且可见的 Renderer 包围盒。正交镜头使用经过角色屏幕位置的平行射线，跟随尚未到位时仍可正确检测。只处理镜头和角色之间的模型，并遵循相机层级剔除；屋顶和树冠不需要碰撞体。包围盒检测会在不规则模型边缘略微提前淡化，可调整 margin。

首次遮挡时复制 Standard 材质，切换为 Fade、关闭深度写入和投射阴影，透明度乘以原颜色 alpha。同组同源材质共用一份副本，每帧只更新颜色。恢复时重新赋回原 sharedMaterials 和原阴影模式并销毁副本；禁用对象、销毁组件、暂停管理器、场景切换和 Clear 都有恢复路径。暂停游戏仍使用非缩放时间完成淡化。

当前支持内置 Standard shader 和 Vegetation Wind。风动植被通过 CameraOccluder.VegetationFadeMaterial 引用随 AA 打包的透明材质模板，运行副本使用其着色器并保留原颜色、纹理和风动形变。其他 shader 仍需补充透明适配后加入列表；用 MaterialPropertyBlock 覆盖 `_Color` 的模型也需单独适配。不会修改原材质资源或重建模型。

## 验证

`CameraOcclusionFlowVerification.Run` 在独立 Unity 工程使用打包 AA 内容验证：多重遮挡、无碰撞体检测、平滑变化、原材质不变、重复材质槽、层级剔除、角色后方模型、镜头跟随偏差、对象禁用、暂停、进出小屋、鸡舍建造、扩建墙壁替换、返回菜单、重新进入及材质释放。

运行报告为 `Logs/CameraOcclusionVerification.txt`。预览包括 `CameraTreeOpaquePreview.png`、`CameraTreeFadePreview.png`、`CameraHallFadePreview.png`、`CameraInteriorFadePreview.png` 和 `CameraExpandedInteriorFadePreview.png`。

游戏进度仍仅在每天睡觉结算后保存，淡化状态不进入存档。

## 合并草原树冠

草原景观树沿用原合并网格，由 `LandscapeCrownOccluder` 保存每棵树的局部包围盒与原风动相位，仍由 CameraManager 的同一轮检测驱动。只淡化挡住角色的叶团，树干、石头和同网格其他树保持原样。完整风动包络包含在检测盒中，边缘可能稍提前淡化。

这条路径使用 MPB 与稳定像素覆盖实现细颗粒淡化，读取原 AA 相机参数；保留原阴影与风动，不创建透明材质副本或逐树对象。近看可见细小点阵，不使用会影响整个合并景观排序的透明队列。首次淡化捕获原实例 MPB，恢复、禁用、切换和退出还原；同一 Renderer 只能由一个遮挡组件拥有，不能同时配置 CameraOccluder。

资源准备、手调保护及验证见 [草原树冠遮挡](GrasslandCrownOcclusion.md)。
