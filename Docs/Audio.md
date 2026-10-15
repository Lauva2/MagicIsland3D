# 基础音效与背景音乐

`AudioManager` 管理音频播放、资源句柄、监听器和音乐过渡，`GameManager` 调度初始化与场景状态。音频配置由 `ConfigurationManager` 通过 AA 地址 `AudioCatalog` 加载，随后 AudioManager 加载配置中 22 个音频地址。当前为 25 份 AA 配置、28 个 UI 预制体，存档仍为版本 29。

## 第一版资源

- 主菜单 `MenuMusic`、农场 `IslandMusic`、小屋 `HomeMusic` 三段原创合成循环配乐，分别约 24、19.2、27.43 秒。
- 界面点击、打开、关闭：`UiClick`、`UiOpen`、`UiClose`。
- 耕地、播种、浇水、收获、砍树、采石、施肥：`Hoe`、`Plant`、`Water`、`Harvest`、`Axe`、`Pickaxe`、`Fertilize`。
- 商店与养殖商店买卖成功：`Trade`。
- 海岸环境：`Waves`、`Wind`、`Rain`、`SeagullCall`、`WingFlap`，以及 `StepGrass`、`StepSand`、`StepWood` 三种脚步；播放规则及资源见 [海岸环境音](CoastalAudio.md)。

WAV 位于 `Assets/Art/Audio/Music`、`Assets/Art/Audio/Effects` 和 `Assets/Art/Audio/Environment`，默认地址为 `Audio_` 加资源名字。`Tools/GeneratePrototypeAudio.py` 合成原有配乐与操作音效，`Tools/GenerateCoastalAudio.py` 合成海岸环境音与脚步，不依赖外部音频。这些是可替换的原型资源；默认仅补缺失 WAV，显式传入 `--replace` 才重新生成已有 WAV，保留原 `.meta` 可保持 GUID 和 AA 引用。

音乐采用 Vorbis 流式读取，短音效采用 PCM 预解压。编辑器用导入器的 userData 标记做一次性配置，后续保留人工调整。`AudioSetup` 创建缺少的 AudioCatalog，注册 AA Audio 分组，并为缺少音效组件的 UI 按钮补齐 `UIButtonAudio`。如果配置的地址已注册到其他音频资源，保留该地址与资源映射；替换正式音频可原位替换 WAV，也可修改 AudioCatalog 地址并自行注册对应 AA 资源。

## 播放规则

- 工具通过 `ActionSucceeded` 事件提供已提交的动作类型。农务 / 采集失败、距离不足、体力不足及动作锁拒绝不会触发成功音效；成熟作物用任何可收获农具操作时播放收获音效。
- 商店通过 `TransactionSucceeded` 事件触发；养殖商店在成功提交后触发。每次真正成功的交易均有音效，失败交易安静。
- UIButtonAudio 只负责界面声音，业务逻辑仍由 MVC Controller 管理。UIManager 注入 AudioManager，组件只绑定一次按钮 onClick，销毁时解绑；不可操作或隐藏的按钮不发声。可用组件的 `PlayClick` 禁用单个按钮声音。
- PopUp 面板实际显示 / 隐藏时播放开关声音。预加载的背包和日报保持隐藏且安静，同一个可见状态重复设置不播放；加载失败不播放打开音效。
- 同类 UI 声音有 45 毫秒间隔，避免一帧内多次关闭或程序触发堆叠。操作成功音效使用已有游戏事务和动作锁，不额外吞掉成功操作。

## 音乐、音量与生命周期

声音使用 2D 音源，环境声按角色距离衰减，鸟声按镜头方向分配声道。AudioManager 持有两个音乐源、八个操作音效源、三个环境循环源、两个鸟声源、一个脚步源及一个持续存在的 AudioListener；场景原有监听器暂时禁用，清理时恢复原启用状态，避免加载间隙无监听器或多个监听器并存。

AudioCatalog 默认配乐音量 0.35、音效音量 0.65、过渡时间 0.8 秒，每条 ClipConfig 还可设置 Gain。两个音乐源使用非缩放时间平滑交叉淡入淡出；相同曲目重复请求不重播。暂停和弹窗保留背景音乐，主菜单、农场和室内按 GameManager 状态选择曲目；加载新游戏时淡出，进入 / 离开小屋和返回菜单时平滑切换。旧曲目淡出结束后停止并清空 AudioSource.clip。

SettingsManager 的现有总音量通过 AudioListener.volume 同时作用于配乐和音效，可立即静音并单独持久化。音频不写游戏存档，游戏进度仍只在每天睡觉结算后保存。

返回主菜单保留已加载音频供菜单和下一局复用，解绑当前 Tools / Shop 的事件并停止场景音效。Clear 或销毁时停止音源、清空 clip 引用、释放全部 AA 句柄并恢复场景监听器；未完成加载的句柄在完成回调中释放，代次检查防止取消后的加载重新建立音源。

## 验证

`AudioFlowVerification.Run` 使用实际打包 AA 内容验证音频地址、波形非静音及循环接缝，工具 / 交易提交、重复和失败操作、UI 点击与预加载、固定音源数量、音乐交叉淡化、音量持久化、暂停、进出小屋、菜单返回、加载失败 / 取消和重新初始化。报告为 `Logs/AudioVerification.txt`。

同时回归 `PauseSleepFlowVerification` 与 `CameraOcclusionFlowVerification`，确认只在睡觉保存、界面预加载和场景切换兼容。
