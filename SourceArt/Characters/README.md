# Island Farmer 主角美术样板

第一版样板通过 Blender MCP 制作，源工具为 Blender 5.2.2 LTS。目标是保持当前主角接口，完善圆润轮廓、草帽、头发、服装和短靴。2026-10-17 已接入正式 PlayerCharacter，并在 Unity 2022.3.62f2 完成 Packed AA 流程验收。

## 文件

| 文件 | 用途 |
| --- | --- |
| `IslandFarmer.blend` | 独立、可编辑场景；六块关节网格、色板、灯光 / 镜头、九段动作预览和预览用锄头 |
| `IslandFarmer.fbx` | 中性姿态的六块网格和关节 / 握点，供美术交换；不含预览灯光、地面、工具或动作 |
| `Assets/Art/Meshes/Player/IslandFarmer.meshdata.json` | Unity 接入用的局部米制顶点、法线、色板 UV 和三角形，避开 FBX 导入轴向与缩放设置对原节点的影响 |
| `Assets/Art/Textures/Player/IslandFarmerPalette.png` | 128×64 色板，Unity 使用 Point / Clamp、sRGB、无压缩、无 mipmap |
| `Assets/Editor/PlayerCharacterArtSetup.cs` | 显式导入与重建菜单；普通 Setup 不触发 |

角色六块网格共 18,618 三角面、9,686 个导出顶点，合用一个材质。原预制体中的关节和动画曲线不变；导入后只有六个新 Renderer 启用，旧原型 Renderer 禁用并保留引用。

## Blender 编辑与导出

打开 `.blend`，选择 `MagicIsland - Island Farmer` 场景。`IslandFarmer - Export` 集合包含正式角色；`IslandFarmer - Preview Only` 包含地面、灯光、镜头及握持检查工具。

九段预览：Idle 1–73，Walk 85–109，Hoe 121–151，Plant 163–193，Water 205–235，Axe 247–277，Pickaxe 289–319，Harvest 331–361，Fertilize 373–403。时间轴有对应标记，30 fps。原 Unity 欧拉 / 位置曲线在 Blender 内逐帧采样，只用于外观检查，不作为新游戏动画导入。

本角色源文件中的锄头检查道具仍使用初版占位尺寸；正式锄头、水壶、斧头、镐子源文件及游戏内握持验收见 [工具源美术](../Tools/README.md)。

美术编辑时保持 `IslandFarmerBody`、`IslandFarmerHead`、`IslandFarmerLeftArm`、`IslandFarmerRightArm`、`IslandFarmerLeftLeg`、`IslandFarmerRightLeg` 六个 Mesh 及其父节点。新细节合并到对应网格；不改变父节点、原点、单位或 ToolSocket。导出前应用 Mesh 对象自身的变换和修改器，保留关节变换；未应用时导出会拒绝，避免 Blender 所见与 Unity 数据不一致。保持 `Palette` UV 位于色块中心，避免跨色块三角形。

通过 Blender MCP / Python 显式导出当前编辑，不重建几何：

```python
from pathlib import Path
repo = Path("你的工程目录")
namespace = {"__name__": "island_farmer_art"}
script = repo / "Tools/Art/BuildIslandFarmer.py"
exec(compile(script.read_text(encoding="utf-8"), str(script), "exec"), namespace)
namespace["export_current"](str(repo), str(repo / "Logs/PlayerArt"))
```

`build(repository, preview_directory)` 是明确的全量重建入口，会重新生成本样板场景及上述源文件；有手调修改时使用 `export_current`。工具只处理带自身标记的样板场景，保留其他场景。源文件封装使用短暂的 Blender 后台进程，独立 `.blend` 只包含本样板场景及其依赖，不写入用户的其他场景。

## Unity 接入与验收

1. 安装并使用工程固定的 Unity 2022.3.62f2，等待依赖及脚本编译。
2. 当前资源已包含应用后的 PlayerCharacter、六个 Mesh `.asset` 和共享材质。首次导入尚未应用的样板时执行 `Magic Island > Art > Apply Blender Player Art`。
3. 同一份源数据重复 Apply 保留人工修改；有意更新已应用的几何时执行 `Rebuild Blender Player Art (Explicit)`。保留生成资产及其 `.meta`，不要删除后重新生成 GUID。
4. 执行 `PlayerCharacterArtVerification.Run` 检查接入和重复配置保留手调。构建 Addressables，使用 Packed Play Mode 和独立验证存档执行角色动画、暂停睡觉、音效、镜头遮挡流程验证。确认中文、游戏镜头、七种动作与握持、暂停、进出小屋、返回菜单及再次进入。

`Tools/VerifyPlayerArt.py` 检查有限数据、单位法线、有效索引、退化面、绕序、色板 UV、源文件、动画路径和 GUID。本次几何检查、Unity 实际导入、AA 构建、接入保留手调验证及四项游戏流程回归均通过。美术没有存档兼容变化。

本轮 Unity 验证在忽略提交的 `Logs/PlayerArtVerificationProject` 副本执行，该目录保存五份 `*Verification.txt` 报告及截图。`PlayerAnimationFlowVerification` 使用正午光照；完整界面截图之外，`*Detail.png` 临时隐藏环境及 UI，检查实际角色与手持工具，并恢复原显示状态。未生成独立游戏发行包。团队同步后需按常规重建本机 Addressables 内容。

源文件和 FBX 回读检查在单独的 Blender 后台进程执行，不在当前工作场景执行：

```text
blender --background --factory-startup SourceArt/Characters/IslandFarmer.blend --python-exit-code 1 --python Tools/Art/VerifyIslandFarmerSource.py
```

结果写入忽略提交的 `Logs/PlayerArt/SourceVerification.json`。本次 Blender 5.2.2 LTS 检查通过：六块网格、同一材质、九段预览，FBX 回读尺寸一致，握点误差约 1.04×10⁻⁷ 米。Unity 接入与流程验收均使用工程固定的 2022.3.62f2。
