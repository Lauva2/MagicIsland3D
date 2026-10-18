# 沙滩组合源美术

通过 Blender MCP 制作，Blender 5.2.2 LTS。`BeachComposition.blend` 仅包含独立的 Beach Composition Studio 场景和三类居中模板：Rocks、Driftwood、Grass。单位米、Z 向上；模板供编辑器组合，源文件不参与 AA 运行加载。

`BeachComposition.meshdata.json` 为 version=1 的 templates 数组，每项包含 name、positions、normals、colors、triangles。位置／法线已转为 Unity 的 X／Y／Z，三角形反向以匹配坐标变换；颜色为 RGBA，使用共享 Biome Details 材质，不依赖外部纹理。

脚本 `Tools/Art/BuildBeachComposition.py` 可通过 Blender MCP 的 execute_blender_code 执行：设置 `__file__` 为该脚本绝对路径并运行。默认保留已有导出；显式调用 `main(replace=True)` 才允许替换带有 MagicIslandBeachSource=1 的独立源场景。保留用户其他场景与对象，使用 libraries.write 保存独立源文件，不覆盖当前打开的文件。预览后恢复 `bpy.app.driver_namespace['BeachCompositionPreviousScene']` 指向的场景。

正式美术迁移使用 **Magic Island > Art > Refine Beach Arrival Composition (Explicit)**。已有烘焙子树与后续手调保留；需要替换已烘焙模型时先设计明确的版本迁移，不删除源资源 meta 或自动清除保护。落点、拾取和建设约束与验证见 `Docs/CoastalArt.md`。
