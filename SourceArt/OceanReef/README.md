# Blender 海底群落模型

Blender MCP / Blender 5.2.2 LTS 制作八种模型：枝状、扇状、桌状、团块珊瑚，海草、管状海绵、海星和扇贝。源网格共 4,612 三角面，使用共享灰度顶点色，游戏内沿用已有群落的柔和配色与材质。

水下可辨识度现由 Unity 的共享 `Reef Palette.mat` 增强（饱和度 2.2、亮度 1.10）。Blender 源数据、成员色调和合并网格无需修改；群落仍受正常昼夜光照，动物及共用海岸模型沿用原材质。调色 / 显式应用与资源构建流程见 [海洋说明](../../Docs/Ocean.md)。

| 网格 | 三角面 | 主要细节 |
| --- | ---: | --- |
| Branch Coral | 800 | 分枝与圆润枝梢 |
| Fan Coral | 744 | 放射枝梢、枝间细连接 |
| Table Coral | 644 | 两层圆盘、波浪边和表面小珊瑚虫 |
| Boulder Coral | 528 | 圆润褶皱轮廓 |
| Seagrass Blades | 560 | 七片弧形折叶、双面厚度、根部固定 |
| Tube Sponge | 540 | 三根高低管体、唇口和深色内壁 |
| Sea Star | 420 | 圆润五臂、背部小颗粒和底面 |
| Clam Shell | 376 | 弧形壳面、放射纹路、底面和边缘厚度 |

`OceanReef.blend` 为独立可编辑场景 `MagicIsland - Reef Models`；`OceanReef.fbx` 为外观 / 顶点色交换文件。正式 Unity 局部米制数据为 `Assets/Art/Meshes/Ocean/Reef.meshdata.json`。展示父节点只用于排版，个别小物的展示缩放不进入游戏数据；模型本身保持单位变换及原局部原点。

## 原位接入与兼容

`Assets/Art/Meshes/Ocean/ReefLayout.json` 记录从现有合并网格匹配得到的 84 个群落、2,744 件装饰的原模型类型、局部位置、旋转、尺寸与色调，并保存九种原始模板（含不替换的礁石）的安全范围。首次 **Capture Existing Reef Layout (Explicit)** 仅在该文件缺失时执行，匹配原顶点 / 索引 / 颜色后才记录；无法匹配的人工网格明确拒绝，不改资源、不重新随机。

**Magic Island > Art > Rebuild Blender Reef Models (Explicit)** 更新八个模板和 84 个合并群落网格，保留原 `.meta` / GUID、Ocean 预制体、群落组件 / 数量、成员位置 / 旋转 / 尺寸 / 色调、鱼群障碍及动物资源。岩石、海床、海面、岸线、材质和活动参数保持。枝状珊瑚、海草及贝壳的模板原本位于 `Assets/Art/Meshes/Coast`，海岸嵌套资源同步使用这些新外观，沿用原碰撞与位置。

每个新模板限制在原水平半径、最低点及最高点以内，避免改变鱼群安全通道或把装饰伸出海床 / 水面。海草颜色 alpha 保存根部 0、顶部 1 的摆动权重；其他装饰 alpha 为 0。沿用 OceanSurface 的统一可暂停时间，不增加碰撞、单件对象、逐帧网格生成、额外 Update 或材质副本。

普通 **Apply Blender Reef Models** / Configure 保留人工编辑。源数据改变需显式 Rebuild；原模板或合并群落有与记录不一致的人工编辑时拒绝覆盖，先保留并导出对应成员布局，再更新布局记录及资源签名。不要删除布局记录以绕过检查。需要重新随机生态时仍使用已有明确的地貌 / 生态 Rebuild，并重新交接对应布局记录。

所有替换数据准备完成后才写资源。保留每群一个合并 Renderer；全海底三角面从 1,455,520 变为 1,562,370，增加约 7.34%。导出合并重复顶点，逐三角面的位置、法线、顶点色与摆动权重保持一致；不以面数变化推断实际 FPS 或 GPU 性能。

## 编辑与导出

### 独立调整海底疏密

**Magic Island > Art > Rebuild Reef Density (Explicit)** 仅改变原 84 个群落内已有成员的显示疏密。ReefLayout 保留完整 2,744 个成员的位置、朝向、缩放和色调，通过 `hidden` 标记稀疏区；`density` 保存独立种子及稀疏 / 丰富区的保留率，默认 20261018、0.08 / 1。连续岸向变化形成丰富礁区和空沙地，每群至少保留一件装饰，仍是每群一个合并网格，没有运行时单件对象。重复重建可恢复此前隐藏的成员。

布局同时记录首次重建前的完整鱼群障碍及群落浅滩 / 大珊瑚类型。只移除隐藏固体对应的原障碍，保留其他障碍的精确数据；不新增障碍、不移动鱼群、鸟蟹、地形或成员。现有障碍或模板 / 合并网格不匹配记录时，在写资源前拒绝覆盖。保留群落网格 GUID、原模板和源 Blender 文件，更新可见数量、合并网格及艺术签名。

**Rebuild Blender Reef Models** 沿用当前成员记录及 `hidden` 标记更新外观；普通 Apply / Setup 保留当前布局和手调。修改疏密使用专用命令，不能删除原布局或签名绕过人工编辑检查。海底原位模型验证按可见成员检查合并网格，同时仍校验完整原始成员记录与安全包络。

通过 Blender MCP 或 Python 控制台执行：

```python
from pathlib import Path
repo = Path('你的工程目录')
ns = {'__name__': 'reef_art'}
p = repo / 'Tools/Art/BuildOceanReef.py'
exec(compile(p.read_text(encoding='utf-8'), str(p), 'exec'), ns)
ns['export_current'](str(repo), str(repo / 'Logs/ReefArt'))
```

`export_current` 保留当前编辑，仅重新导出源文件、FBX、灰度顶点色与米制网格。`build` 是显式全量重建本样板场景的入口，只处理带自身标记的 Reef Models 场景；角色、工具、海洋动物和其他场景保留。帮助函数来自 BuildOceanWildlife.py / BuildIslandFarmer.py，独立源文件在后台 Blender 封装。

坐标沿用 Unity +Y 向上、+Z 向前，Blender 对应 (x, -z, y)。新增细节合入对应网格，保留 `OceanColor` 顶点色及海草权重；原点与单位不变。Blender 共用展示材质将对象展示色乘上顶点色，仅用于棚拍；Unity 使用已保存的群落色调和原材质。

## 验证入口

- `Tools/VerifyOceanReefArt.py`：几何、法线、绕序、非退化面、顶点色、海草权重、原安全包络、布局数量及整体面数预算。
- 独立 Blender：`blender --background --factory-startup SourceArt/OceanReef/OceanReef.blend --python-exit-code 1 --python Tools/Art/VerifyOceanReefSource.py`，源文件 / FBX 空间与海草权重回读。
- `OceanReefArtVerification.Run`：八种模板、84 个原群落、草根固定、合并网格、裁剪范围、重复 Apply / Configure 保留手调，以及不匹配的人工编辑在重建前被拒绝。
- `OceanFlowVerification.Run` / `TerrainCoastFlowVerification.Run` / `PauseSleepFlowVerification.Run`：使用隔离工程、独立存档和真正 Packed AA，检查海洋生态、共用海岸模型、暂停、室内、失败 / 取消、退出重进与睡觉存档。

资源同步后执行 **Magic Island > Build Prototype Addressables**。不增加配置、AA 地址、UI、交互或存档字段，仍兼容 v29。日志 / 截图仅保留在忽略提交的 Logs；未生成独立发行包或测量 GPU 性能。

2026-10-17 模型验收：几何、Blender 源文件 / FBX 空间及权重回读通过，逐三角面核对去重前后位置、法线、颜色和摆动数据相同。Unity 2022.3.62f2 的 OceanReefArtVerification、Packed OceanFlowVerification、TerrainCoastFlowVerification、PauseSleepFlowVerification 均退出 0 并输出 PASSED。已检查实际浅滩 / 礁区画面；合回仅更新八个模板、84 个合并群落及新增源美术 / 布局记录，原预制体、材质、全部 GUID、动物、鱼群和障碍保持。其后单独增加的鱼转向参数与验证见海洋文档，不属于本模型布局迁移。
