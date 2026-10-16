# 植被风动与统一风力

WeatherManager 提供统一的室外风力、方向和阵风时钟。椰叶、灌木、可采集橡树树冠，以及苹果 / 橙树各成长阶段的树冠随风轻微摆动；云层漂移速度和风声音量使用同一风力倍率。晴天较轻，雨天逐渐增强，不在天气切换瞬间跳变。

## 表现与碰撞

VegetationWind 组件只在启用时设置各 Renderer 的形变参数和保守包围盒，逐帧风动在 GPU 顶点着色器内完成。椰叶按离冠心距离增加摆幅，灌木和树冠按离底部的高度增加摆幅。不同位置和叶簇有不同相位，避免整片植被整齐同步。

原始网格、根部、树干、椰果、果实、场景布局和碰撞体保持不变；风动不修改 Transform，不干扰采集受击摇晃、鼠标命中或玩家建设。可采集树被砍倒后，完整树冠随原有层级隐藏，树桩保持原样。

风动材质支持 PBR 日夜照明和动态阴影。CameraOccluder 在遮挡时使用专门的 Vegetation Wind Fade 透明通道，保留同一顶点形变；恢复后换回原 AA 材质并释放副本。不会每帧创建材质。

## AA 资源与参数

沿用同名 AA 预制体：CoastalPalm、CoastalShrub、GatheringTree、AppleTree、OrangeTree。风动材质位于 `Assets/Art/Materials/Vegetation`，随这些预制体作为依赖加载，不新增配置地址或 UI。

- WeatherConfig.WindDirection：世界 X / Z 平面方向，默认 (1, 0.35)，使用时归一化。
- SunnyWindStrength / RainWindStrength：默认 0.65 / 1.25，支持晴天无风；雨天强度不低于晴天。
- WindGustAmount：默认 0.18，由两段慢速波动生成轻微阵风。
- WindChangeSeconds：默认 4 秒，控制风力向晴 / 雨目标靠近的速度。
- VegetationWind.Bend / Flutter / Radial：单个叶簇的弯曲、细颤和根部约束方式。可在预制体内调整，常规配置保留已有参数。
- VegetationWind.Evergreen：是否使用 SeasonConfig 的常绿配色，椰树默认开启；与风动参数共用已有 MaterialPropertyBlock。普通与 Fade 着色器共用四季颜色算法，Alpha 仍由镜头淡化控制，见 [日期与季节](CalendarAndSeasons.md#四季环境配色)。
- CloudField.Wind 的长度仍定义基准云速，实际方向和倍率来自天气；风声音量仍受 AudioCatalog.WindVolume、Gain 和总音量设置控制。

着色器为 VegetationWind.shader 和 VegetationWindFade.shader，共用 VegetationWind.cginc，避免透明状态与正常状态使用不同的摆动算法。VegetationWindSetup 首次为已知叶簇补齐组件与风动材质，保留原有颜色、纹理、碰撞和引用；后续执行保留已有材质和预制体调整。

## 暂停、场景与保存

暂停和玩法弹窗冻结风动时钟及阵风；进入小屋同样冻结室外风动，室外云层隐藏、风声淡出，返回岛上继续。云层位置按速度累计位移，风速改变不会把已经漂移的云突然移到别处。

WeatherManager.Clear 恢复此前的全局风参数，退出和重新进入不叠加风场。风动完全属于表现，不新增存档字段，存档仍为版本 29，进度仍只在每天睡觉结算后保存。

## 验证

VegetationWindFlowVerification 使用真正的 Packed AA 内容，检查预制体 / 材质 / 配置幂等、GPU 两帧形变、全部碰撞体与 Transform 固定、单独隔离叶片的真实透明渲染、材质恢复、晴雨渐变、云速连续和风声倍率、暂停 / 室内冻结，以及退出清理和再次进入。

TerrainCoastFlowVerification 回归海岸碰撞、遮挡淡化、鼠标过滤、旧摆放优先、实际移动和码头钓鱼；LightingFlowVerification 回归昼夜、雨天及室内光照。预览为 Logs/WindSunnyPreviewA.png 和 WindSunnyPreviewB.png。
