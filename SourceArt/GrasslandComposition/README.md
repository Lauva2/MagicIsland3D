# 草原主路景观模型

Blender MCP 制作的独立 `Grassland Composition Studio`，源文件为 `GrasslandComposition.blend`。原打开场景和对象保留，库导出只包含此工作室。`Tools/Art/BuildGrasslandComposition.py` 可复现三个模板；默认拒绝覆盖已有源文件或同名工作室。仅显式 `main(replace=True)` 可重建本工具拥有的工作室与源文件。

- Rocks：一大一小圆润苔石，200 三角面。
- Grass：19 片立体折叶矮草，190 三角面。
- Flowers：7 枝奶油黄／淡紫野花，665 三角面。

`GrasslandComposition.meshdata.json` v1 保存 Unity 坐标、三角索引、法线和颜色。Blender `(x,y,z)` 转 Unity `(x,z,y)` 并反转绕序；编辑器显式工具校验后烘焙，游戏不读取 JSON 或 Blender 文件。

模板原点位于地面，保持米制尺寸。组合使用现有 Biome Details 共享材质，不新增纹理、碰撞、拾取物或运行脚本。落点读取实际烘焙地面，并对整个保守包围脚印检查通行和可建设格。
