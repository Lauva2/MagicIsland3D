# 琥珀遗迹源模型

`StarterAmber.blend` 在 Blender 5.2.2 LTS 中通过 MCP 制作；独立场景 `Starter Amber Studio` 包含分面巨型琥珀、六组晶簇、双层石台与几何符文。主晶体约 18.5 米高，整体高约 20 米、直径 21.6 米。

复现脚本：仓库根目录下 `Tools/Art/BuildStarterAmber.py`，在 Blender 中执行。脚本仅重建专用场景，其他场景保留；保存源文件并导出 `StarterAmber.meshdata.json`。导出按三角面拆顶点，保留硬边与材质色，转换为 Unity 的 X／Y 向上／Z 坐标，并反转绕序；786 个三角面，运行时合并为一个网格、一个共享材质。

修改 `.blend` 后需相应导出 JSON；直接再次运行生成脚本会重建专用场景，因此手工编辑应先另存副本。Unity 显式菜单 `Magic Island > Art > Rebuild Large Starter Island (Explicit)` 导入 JSON，原位更新 `Amber ruins.asset`，保留网格 GUID。普通 Setup 不导入、不覆盖人工修改。运行时不加载 Blender 或 JSON，源模型不新增碰撞或交互，沿用地形预制体中的地标碰撞。

## 灵辉古树

`StarterAncientTree.blend` 在同一 Blender 5.2.2 LTS 中通过 MCP 制作。复现脚本为 `Tools/Art/BuildStarterAncientTree.py`，仅重建专用 `Starter Ancient Tree Studio` 场景，其他场景保留；保存副本不覆盖原琥珀源文件。树干扭转、九条外露根、七组弯枝、分层银绿树冠、垂叶、蓝紫灯笼花和 36 枚微光均为源几何。整体约 27 × 29 米、高 23 米，共 7,500 个三角面；源场景 667 个部件在 Unity 合为一个网格和共享材质，不产生逐叶／逐粒子对象。

导出 `StarterAncientTree.meshdata.json` 使用 Unity 米制、Y 向上、三角绕序反转和拆分顶点色。颜色 alpha 表示叶片摆动权重；UV0.x 为固定相位，UV0.y=2 标记微光几何，其他为 0。`AncientTree.shader` 只由 OceanSurface 传入的 `_OceanTime` / `_Daylight` 控制垂叶轻摆、微光漂移和夜间花朵发光，不增加灯光、独立 Update 或运行时网格生成。

显式执行 `Magic Island > Art > Rebuild Ancient Tree And Water Ecology (Explicit)` 更新古树原网格 GUID、小岛无碰撞装饰及独立湖海生态子树；普通 Setup 不运行此操作。大型岛屿显式重建同样导入当前古树源文件，原生态子树保留。原地标 CapsuleCollider 和古树石径地面不改；编辑源模型后应同步导出 JSON，再重建并构建 AA。直接重跑 Blender 脚本会重建专用场景，手工编辑需先保留副本。
