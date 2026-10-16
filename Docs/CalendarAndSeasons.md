# 日期、季节与播种

当前进度统一在每天睡觉结算后保存；白天操作仅修改运行状态，面板手动保存和返回菜单保存已取消，设置单独持久化。睡觉失败只重试该次固定快照。规则见 [暂停与睡觉存档](PauseAndSettings.md)。

## 系统职责

`GameManager` 创建 `CalendarManager`，在农场初始化和存档恢复后初始化日期系统，再初始化天气、工具、交互和商店。返回菜单和初始化失败时清理引用。

`CalendarManager` 通过 `Farm.State.Day` 和 `SeasonConfig.DaysPerSeason` 推导年份、季节及季节内日期；不保存另一份可变日期。玩家在床边确认睡觉后，GameManager 统一结算成长、推进日期、重置时钟、按新日期产生天气及环境配色、补满体力，最后保存次日清晨快照。调试“下一天”和旧 AdvanceDay 入口不能绕过床边确认。

`ConfigurationManager` 使用 AA 地址 `SeasonConfig` 加载并校验季节配置，持有句柄并在销毁时释放。天数允许 1–365，四季降雨概率要求有限数且在 0–1 内。非法配置阻止进入主菜单；重试重新加载全部配置。

## 默认配置

春、夏、秋、冬依次循环，每季 28 天，一年 112 天。

| 总天数 | 日期 |
| --- | --- |
| 1 / 28 | 第 1 年春季 1 / 28 日 |
| 29 / 56 | 第 1 年夏季 1 / 28 日 |
| 57 / 84 | 第 1 年秋季 1 / 28 日 |
| 85 / 112 | 第 1 年冬季 1 / 28 日 |
| 113 | 第 2 年春季 1 日 |

`Assets/Config/SeasonConfig.asset` 默认降雨概率：春 30%、夏 20%、秋 40%、冬 15%。冬季目前仍为晴天或雨天，尚未加入雪天。新游戏首日晴天；以后按**目标日期**的季节概率与存档天气种子生成天气。

## 四季环境配色

SeasonConfig 的 Spring / Summer / Autumn / Winter 各有 Grass、Foliage、Evergreen 和 Strength。前三者为草地、普通树冠及常绿叶片的目标颜色，Strength 为混合强度（0 保留原始美术，1 完全应用目标色调）。所有颜色通道及强度必须有限且在 0–1 内，缺失色板或非法值阻止配置加载；默认值与实际 AA 资源一致，Inspector 合法编辑在重复配置时保留。

春季嫩绿，夏季浓绿，秋季草地偏暖、树冠橙黄，冬季使用低饱和灰绿。椰叶通过 VegetationWind.Evergreen 使用独立常绿色，不会在秋季整棵变橙；灌木、可采集树冠和各阶段果树树冠使用普通叶色。树干、果实、作物、道路、建筑、沙滩、海洋与水下生态保持原有表现。花草、秋季落叶与冬季薄霜见 [四季地表细节](SeasonalGroundDetails.md)，不新增积雪天气或玩法限制。

WeatherManager 持有 SeasonalPresentation，全局配色在 CalendarManager 已根据恢复后的日期初始化时应用，并在 NextDay 结算新日期时刷新，首次可操作前已正确显示。新实例或隐藏后重新启用的树冠直接使用当前配色，无需逐帧查找 Renderer 或克隆材质。暂停、晴雨与昼夜不推进季节；室内使用原室内材质。Clear / 初始化失败恢复进入前的全局值，重新进入不残留上一局颜色。

Island Shore 沿用烘焙顶点色，以绿色及干燥地表掩码区分草地，保留沙滩与海床；未开垦农田的 Plot Grass / Grass Tips 使用 Seasonal Grass，随外围草地一起变色，翻耕、浇水和施肥后的土壤保留原有颜色。Vegetation Wind、Fade 和 Seasonal Grass 共用 SeasonalColor.cginc，在原纹理明暗上调整色调，保持风动和透明度一致。常绿分类使用原 MaterialPropertyBlock，与风动参数共存。地形、海床及生态无需重烘焙。修改色板或着色器后重新执行 Build Prototype Addressables。

## 作物与操作

`CropCatalog.Crops[].PlantingSeasons` 使用 `SeasonMask` 多选季节，允许一季、多季或 `All`；空掩码及未知位无效。

| 作物 | 适宜播种季节 |
| --- | --- |
| 芜菁 | 春、秋 |
| 番茄 | 夏 |
| 玉米 | 夏、秋 |

`FarmManager` 将季节检查传给 `FarmActions`，操作发生时读取当前日期和选中作物。非适宜季节无法对空的已耕地块播种，返回中文提示，不改变地块、背包、体力，不触发物品变化事件。未耕地、已有作物和种子耗尽仍有相应提示。成熟作物在任何工具下优先收获，不受当前种子的季节限制。

已播种作物跨季继续成长、浇水和收获，不枯萎；生长仍由每日浇水决定。冬季默认无法新种这三种作物，但保留已种植作物。温室、枯萎、节日以后扩展。

商店全年可购买所有种子，背包可选择非当季种子作为当前工具。背包种子详情和商店当前商品均显示适宜季节以及“当前可播种／当前季节不适宜”。真正的限制由农务操作检查。HUD 的日期通过 `FarmHudModel`、`FarmHudController` 和 `PrototypeHud` 显示，背包和商店继续使用现有 MVC；所有预制体仍位于 `Assets/Art/Prefabs/UI`，使用同名 AA 地址。

## 存档与配置维护

存档当前为版本 29，保留版本 1–28 的迁移链；早期日期模块在版本 9 加入的日期仍从已有 `Day` 推导。四季配色不增加存档字段。加载时恢复当天晴雨，不重新抽取，不重复成长，并由日期恢复环境颜色。相同天气种子、天数和季节配置会产生相同后续天气。

改变每季天数会重新解释已有存档日期；改变季节降雨概率会影响未来天气，已保存的当天天气保持原值。当前未保存季节配置快照，发行后若需要永久保留旧历法，应增加配置版本迁移。

编辑器自动创建缺失的 SeasonConfig 并注册 AA；只为没有 `PlantingSeasons` 序列化字段的旧 CropCatalog 补默认季节，已配置的掩码保持不变。新作物默认 `All`，可在 Inspector 指定季节。既有默认 UI 的日期宽度和详情高度扩展一次，后续配置不重复扩展。

## 验证

- `Tools/VerifyCalendar.cs`：4552 项断言，覆盖每季 1–365 天的季节／年份边界、掩码、错误输入、大天数、播种失败原子性、跨季成长与收获、版本 6 日期恢复和天气序列。
- `SeasonalFlowVerification.Run`：构建 Packed AA，校验色板、Setup 保留人工编辑、四季实际渲染、常绿与风动 / 淡化兼容、碰撞和共享材质不变、暂停 / 夜间、真实床边睡觉跨越四季及年份、冬季加载、返回恢复与新游戏重新进入。测试运行时将独立夹具的每季天数设为 1，不写回项目配置；图片为 Logs/SeasonSpring.png、SeasonSummer.png、SeasonAutumn.png、SeasonWinter.png。
- `CalendarFlowVerification.Run`：在独立 Unity 工程构建并使用打包 AA，验证配置、HUD、背包／商店中文与布局、交易、四季边界、目标季节天气、跨季作物、保存／加载和系统清理。
- `GameFlowVerification.Run`：旧完整流程的测试夹具使用四季可种和无雨，保留对多作物、交易、工具、体力、时间与存档的独立验证。实际默认季节规则由日期专项流程验证。

验证存档与图片位于独立工程的 `Logs`，不影响日常游戏存档。

2026-10-16 本次实际在独立 Company / Product 的验证工程执行并通过 SeasonalFlowVerification、VegetationWindFlowVerification、LightingFlowVerification 和 FarmArtFlowVerification，均使用构建后的 Packed AA。覆盖四季 / 年份切换、冬季读档、风动 / 透明淡化、昼夜 / 雨天 / 室内、农务完整操作与睡觉成长、加载失败 / 取消及返回清理。运行后核对全部已跟踪 Art / Config 与源工程字节一致，每季天数仍为 28，存档版本仍为 29。实际玩家存档未参与；本次未运行独立 Player 构建或全套 Tools 规则验证。主工程及协作者同步后需重建 AA 内容，无需重建海床或生态。
