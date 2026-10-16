# Blender 农具：锄头、水壶、斧头与镐子

通过 Blender MCP 在 Blender 5.2.2 LTS 制作。四件工具共 9,396 三角面、一个共享色板材质；锄头 1,760，水壶 3,368，斧头 1,884，镐子 2,384。配色沿用 Island Farmer 的暖木、奶油白和柔和海蓝。

## 文件与握持约定

- `FarmTools.blend`：独立可编辑场景、四块工具网格、打包色板、预览灯光和镜头。
- `FarmTools.fbx`：四件工具及各自握点父节点，不包含地面、灯光或镜头。
- `Assets/Art/Meshes/Tools/FarmTools.meshdata.json`：Unity 局部米制网格数据。
- `Assets/Art/Textures/Tools/FarmToolsPalette.png`：128×64 色板，Point / Clamp、sRGB、无压缩、无 mipmap。
- `Assets/Art/Prefabs/Tools/IslandFarmTools.prefab`：四件工具的显示根和序列化引用，作为 FarmActionEffects 的 AA 依赖。

场景名为 `MagicIsland - Farm Tools`。正式网格名为 IslandHoe、IslandWateringCan、IslandAxe、IslandPickaxe；父节点的 unity_path 分别为 Hoe、Water、Axe、Pickaxe。网格原点就是 ToolSocket 握点，Unity +Y 向上、+Z 向工具前方。源场景父节点的位置仅用于摆开展示，Unity 使用网格局部数据。保持网格自身变换为单位变换，修改后应用修改器和网格变换；不要移动网格原点或改变单位。

美术新细节合并入对应工具网格，Palette UV 保持在色块中心。水壶提手经过握点，壶身悬于手下并略向前，避开静止时的腿部；倾倒姿态沿用已有角色动画和工具旋转，不增加业务动画事件。

## 编辑与显式导出

通过 Blender MCP 或 Python 控制台执行：

```python
from pathlib import Path
repo = Path('你的工程目录')
namespace = {'__name__': 'farm_tool_art'}
script = repo / 'Tools/Art/BuildFarmTools.py'
exec(compile(script.read_text(encoding='utf-8'), str(script), 'exec'), namespace)
namespace['export_current'](str(repo), str(repo / 'Logs/FarmTools'))
```

export_current 保留手工编辑，仅重新导出上述工具网格、色板、独立源文件和 FBX。extend_gathering_tools(repository, preview_directory) 仅向已有两件农具场景增加缺失的斧头和镐子，保留已有网格、色板及其他场景；新工具已存在时拒绝覆盖。build(repository, preview_directory) 是明确的全量重建入口，会重建本工具场景；它保留主角场景及用户其他场景。基础几何帮助函数来自同仓库的 BuildIslandFarmer.py。源文件封装在单独后台 Blender 中完成，不保存用户的其他场景。

在 Unity 2022.3.62f2 中执行 `Magic Island > Art > Rebuild Blender Farm Tools (Explicit)` 更新已有工具，随后重建 Addressables 并执行 [工具文档](../../Docs/ToolsAndFeedback.md) 中的验收。普通 Setup 只补缺失的特效引用，不重建网格，不覆盖人工握持偏移或合法材质参数。

## 验证

`Tools/VerifyFarmTools.py` 检查网格数据、法线、绕序、退化面、UV、尺寸和源文件。

源文件及 FBX 回读在单独进程执行：

```text
blender --background --factory-startup SourceArt/Tools/FarmTools.blend --python-exit-code 1 --python Tools/Art/VerifyFarmToolsSource.py
```

Unity 接入检查为 FarmToolArtVerification.Run，游戏回归为 PlayerAnimationFlowVerification.Run、FarmArtFlowVerification.Run、GatheringArtFlowVerification.Run、PauseSleepFlowVerification.Run、AudioFlowVerification.Run。验证使用独立工程和存档，报告和截图位于忽略提交的 Logs 下；完整界面截图保持游戏镜头，Detail 近景暂时隐藏环境和 UI 并保留游戏灯光，Side 用于核对握持和动作侧面。

不增加配置、AA 地址、UI 或存档字段；团队同步后重建本机 AA 内容。

2026-10-17 首次锄头 / 水壶验收：几何检查、Blender 5.2.2 LTS 源文件 / FBX 回读、Unity 2022.3.62f2 接入与重复配置保留手调均通过。独立工程 `Logs/PlayerArtVerificationProject` 中的 Packed AA 角色动作、农务、暂停睡觉和音效四项流程回归通过；已检查两件工具五个动作进度的近景与侧视图，调整水壶以避开静止时腿部。另在独立 Unity 重启后验证导入标记仍保留。验证文件不提交，未生成独立游戏发行包。

2026-10-17 斧头 / 镐子扩充验收：四件工具几何、独立 Blender 源文件 / FBX 回读、Unity 接入和重复 Apply / Setup 保留手调均通过。独立工程的 Packed AA 角色动作、采集、暂停睡觉和音效四项回归通过，覆盖砍树 / 采石产出、耗尽 / 恢复、动作暂停、失败 / 取消加载及退出清理；已检查新工具五个动作进度的近景与侧视图，沿用既有单手动画。合回时核对原锄头 / 水壶网格、旧预制体组件、材质、GUID 和特效依赖均保留，仅增加新工具资源与引用；未生成独立发行包。
