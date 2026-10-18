# 草原小兔源模型

`MeadowRabbit.blend` 包含独立的 `Meadow Rabbit Studio` 场景。通过 Blender MCP 制作圆润奶油色兔子，含眼睛高光、粉色耳内侧、短爪与绒球尾巴。源场景不替换用户原活动场景。

`Tools/Art/BuildMeadowRabbit.py` 是可复现的 Blender Python 脚本，默认拒绝覆盖已有 `.blend` 和 JSON。需要迭代时先另存源文件，明确备份并协调资源编辑；普通 Unity Setup 不生成或覆盖源模型。

`MeadowRabbit.meshdata.json` 为展开三角面的顶点、平滑法线、sRGB 顶点色、关节 ID、枢轴及索引。Blender +Y 朝前、+Z 朝上转换为 Unity +Z 朝前、+Y 朝上，并反转三角绕序。每只 1,784 三角面；模型约宽 0.45 米、长 0.72 米、高 0.81 米。

关节 ID：0 身体／尾巴，1 头部／眼鼻，2／3 左右耳，4／5 前爪，6／7 后腿。头耳共用 Unity `(0, 0.40, 0.17)` 颈部枢轴，耳与四肢另有自己的枢轴。改形须保持部件标记，并通过全部动作包络检查。

Unity 显式 **Add Grassland Rabbits (Explicit)** 将四只合并为原 IslandTerrain AA 依赖中的一个网格。运行时不加载源文件，不新增逐只对象。完整维护与验证见 [草原小兔](../../Docs/MeadowRabbits.md)。
