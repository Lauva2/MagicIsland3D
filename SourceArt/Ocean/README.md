# Blender 海洋动物

Blender MCP / Blender 5.2.2 LTS 制作四种鱼、海鸥和螃蟹，共八块共享网格、10,138 三角面。颜色使用 sRGB 顶点色，Unity 沿用原 Ocean Ecosystem 材质和动画着色器，无新增纹理、材质实例或 AA 地址。

| 网格 | 三角面 | 约定 |
| --- | ---: | --- |
| SilverMinnow | 590 | 银蓝背部、浅色鱼鳍 |
| Clownfish | 590 | 橙色体表、两道奶油白条纹及深色过渡 |
| Butterflyfish | 590 | 扁平黄身、深色眼带 |
| SpottedRay | 1,242 | 双面胸鳍、背部斑点、细尾 |
| Seagull Body | 2,166 | 圆润头颈、喙线、眼睛高光、分趾和尾羽 |
| Seagull Left / Right Wing | 各 824 | 原翼根下的分层羽片 |
| Shore Crab | 3,312 | 壳面纹理、八条关节腿、双钳和眼柄 |

`OceanWildlife.blend` 为独立可编辑场景 `MagicIsland - Ocean Wildlife`，包含顶点色、螃蟹关节属性和展示灯光；`OceanWildlife.fbx` 用于交换外观及顶点色。正式游戏数据为 `Assets/Art/Meshes/Ocean/Wildlife.meshdata.json`；FBX 不替代 JSON / .blend 中的自定义关节数据。`ReferenceEnvelopes.json` 保留替换前四种鱼的水平半径、上下界及鳐鱼尾端，供显式生成时保持原安全范围。

所有网格使用 Unity 局部米制、+Y 向上、+Z 向前；Blender 对应 (x, -z, y)。网格本身保持单位变换，父节点仅用于展示。海鸥翅膀原点保持原翼根局部坐标；游戏内父节点位置仍为左右 ±0.07、Y=0.27。不要把展示父节点的位置烘焙进网格。

螃蟹 POINT 属性 `hinge` 为 Unity 坐标枢轴，`joint_tag` 为部件标记，`leg_phase` 为步相。导出到 Unity UV 通道 1 / 2，八条腿沿用 100–103，双钳沿用 110 / 111，身体为 0。新细节合入所属部件并保留这些属性；不增加骨骼、Animator 或逐腿对象。动物顶点色 alpha 为 0，避免进入海草风动分支。

## 编辑与导出

在 Blender MCP 或 Python 控制台执行：

```python
from pathlib import Path
repo = Path('你的工程目录')
ns = {'__name__': 'ocean_art'}
p = repo / 'Tools/Art/BuildOceanWildlife.py'
exec(compile(p.read_text(encoding='utf-8'), str(p), 'exec'), ns)
ns['export_current'](str(repo), str(repo / 'Logs/OceanModels'))
```

`export_current` 导出当前编辑，不重建造型。`build` 是显式全量重建本样板场景的入口，只处理带自身标记的 Ocean Wildlife 场景，保留角色、工具及其他场景；源文件封装在独立后台 Blender 完成。基本几何与摄影棚帮助函数来自 BuildIslandFarmer.py。

Unity 执行 **Magic Island > Art > Rebuild Blender Ocean Wildlife (Explicit)**。导入器保留原八个网格及现有 School 网格 GUID，逐成员沿用原枢轴、旋转、缩放、色调、数量、出生记录和网格 bounds。所有新几何和完整鱼身初始化检查通过后才写资源；不保存 Ocean 预制体，不重新随机海床、珊瑚、栖息地或鸟蟹位置。来源与已编辑的合并网格不匹配时拒绝覆盖，应先保留并导出该人工布局。

重复 **Apply Blender Ocean Wildlife** 及普通 Configure 只验证导入标记 / 保留现有编辑，不覆盖网格、材质或活动参数。源数据改变后必须显式 Rebuild；随后按常规 **Build Prototype Addressables**。不新增配置、UI、交互、掉落或存档字段，仍兼容 v29。

## 验证

- `Tools/VerifyOceanWildlifeArt.py`：实际几何、单位法线、绕序、非退化面、顶点色、原鱼身包络及蟹关节。
- 独立 Blender：`blender --background --factory-startup SourceArt/Ocean/OceanWildlife.blend --python-exit-code 1 --python Tools/Art/VerifyOceanWildlifeSource.py`，检查源文件、属性、FBX 顶点色与空间边界回读。
- `OceanWildlifeArtVerification.Run`：原 AA 引用、实际网格、完整鸟蟹动作包络、脚部固定和重复 Apply / Configure 保留手调。
- `OceanFlowVerification.Run`：Packed AA 鱼群、鱼身、GPU 变形、鸟蟹动作、惊扰、导航、暂停、室内、加载失败 / 取消、退出及重进。
- `CoastalAudioFlowVerification.Run` / `PauseSleepFlowVerification.Run`：环境音与睡觉存档回归，使用独立工程 / 存档。

截图与报告仅保留在忽略提交的 Logs 中；Blender 棚拍不替代引擎内检查。未生成独立发行包或测量 GPU 性能。

2026-10-17 验收：几何检查、独立 Blender 源文件 / FBX 回读、Unity 2022.3.62f2 接入与重复配置保留手调均通过。隔离工程的 Packed OceanFlowVerification、CoastalAudioFlowVerification、PauseSleepFlowVerification 全部退出 0 并输出 PASSED；覆盖四种鱼完整鱼身与 GPU 弯曲、鸟蟹动作 / 导航 / 玩家惊扰、暂停、室内、加载失败 / 取消、退出重进及固定快照睡觉回归。已检查鱼群游动、鳐鱼、海鸥啄食 / 理羽和螃蟹觅食 / 抬钳画面，蟹类近景部分受原椰叶遮挡。合回仅更新八块源网格、30 个现有合并鱼群网格及导入标记；原预制体、全部 GUID、84 个珊瑚群落、海床、水面、材质及原记录保持。
