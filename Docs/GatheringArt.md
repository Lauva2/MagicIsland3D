# 树木、岩石与采集反馈

可采集橡树使用多层低多边形树冠、树枝、树根和树皮纹理，树桩显示新鲜切面、年轮、裂缝和少量苔藓。岩石改为不规则多面石块，带侧石、矿物纹路及苔藓；采完后显示六块碎石残迹。配色沿用原橡树及岩石材质，与村落、农田和海岸衔接。

## AA 资源与编辑

- `Assets/Art/Prefabs/Gathering/GatheringTree.prefab`，地址 `GatheringTree`。
- `Assets/Art/Prefabs/Gathering/GatheringRock.prefab`，地址 `GatheringRock`。
- 网格：`Assets/Art/Meshes/Gathering`；材质：`Assets/Art/Materials/Gathering`。
- 木屑和碎石模板位于已有 `FarmActionEffects.prefab`，随同 AA 地址 `FarmActionEffects` 加载。
- Blender 斧头 / 镐子位于 `IslandFarmTools.prefab`，经 FarmActionEffects.HeldToolsPrefab 随同一依赖加载；两件分别为 1,884 / 2,384 三角面，与锄头、水壶共用色板材质。保留 ToolSocket、动作进度、命中时机和节点碰撞，手持网格不增加 Collider。编辑方法与生命周期见 [工具动作](ToolsAndFeedback.md)。

GatheringManager 继续拥有两种 AA 模型依赖及五个节点实例，配置中的节点 ID、位置、次数、产出、体力和恢复间隔保持原值。ToolManager 拥有 AA 特效依赖。没有新增 UI、配置资产或存档字段。

GatheringArtSetup 对默认资源执行一次美术迁移，GatheringArtVisual.ArtVersion 标记版本。同版本重新配置不会覆盖预制体、网格或材质中的编辑；碎屑模板只补缺失引用。可以直接编辑资源美术层，或修改配置中的 AA 模型地址。

## 受击、耗尽与恢复

完整和耗尽两套原碰撞体保留，原占位 Renderer 隐藏；新美术层不包含碰撞，放在 Ignore Raycast 层。点击仍命中原树干、触发树冠或岩石。GatheringArtVisual 只摇晃独立美术层，碰撞不会跟随旋转。CameraOccluder 绑定完整资源的新 Renderer，继续支持角色遮挡淡化。

每次成功采集提交状态后，ToolManager 通过 GatheringManager 播放受击反应：完整树木轻微摇晃，完整岩石短暂震动，最后一次点击切换残迹并做轻微缩放反馈。失败操作不播放反应。动作结束恢复原姿态；跨日恢复和读档也从原姿态开始，不重播旧动作。

斧头发出木屑，镐子发出多面碎石；碎屑飞出后落地并缩小消失。沿用 FarmActionEffects 的最多 48 个可复用实例，不改变农田的扬尘、水花或收获闪光。ToolManager 统一推进节点反应和特效，打开背包或其他弹窗时同时冻结，关闭后继续。

树木耗尽后仍等待 3 天，岩石等待 2 天；到期切回完整模型，不赠送材料。未完成的采集次数跨日保留。全部进度只在睡觉结算时保存，返回菜单不写进度。

## 验证

GatheringArtFlowVerification 在隔离项目构建 Packed AA，检查资源保留、位置与碰撞、鼠标分发、距离及错误工具、受击和碎屑暂停、部分／耗尽状态的睡觉存档与恢复、原恢复日期和材料数量、恢复后的再次采集，以及退出资源清理，并生成完整、受击、残迹及恢复预览。

基础规则由 VerifyGathering 和 VerifyToolUpgrades 检查；CameraOcclusionFlowVerification、FarmArtFlowVerification 和 InteractionPromptFlowVerification 覆盖遮挡淡化、共用特效池及既有交互。
