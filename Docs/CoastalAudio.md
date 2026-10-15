# 海岸环境音与脚步

AudioManager 统一管理海浪、风雨、海鸥和脚步，GameManager 调度场景状态和环境更新。沿用 AA AudioCatalog 和现有总音量设置，不新增 UI，也不写游戏进度；存档继续只在每天睡觉时保存。

## 播放规则

- 海浪根据角色到海岸的距离平滑衰减，默认 16 米外安静；岛上户外保留轻风声。雨天加入雨声并增强风声、海浪声，放晴后淡出雨声。
- 海鸥默认每 9–20 秒尝试鸣叫一次，只选择听觉范围内实际显示的海鸥；距离越远越轻，并根据镜头方向分配左右声道。夜间和雨天取消鸣叫。真实惊飞触发扑翼声，同一受惊过程只触发一次，全局至少间隔 2.5 秒。
- 角色实际贴地移动时累计距离，每约 1.65 米播放一次脚步，附带轻微音高变化。草地、干沙滩、码头木板分别播放不同声音，小屋内使用木板声。静止、撞墙没有位移、工具动作锁、传送和恢复位置不产生脚步。
- 暂停和玩法弹窗停止脚步 / 鸟叫 / 扑翼，并冻结鸟叫间隔；户外循环压低到 45%，音乐保留。进入小屋或切换场景时户外循环淡出，回到岛上恢复。返回菜单清除全部岛屿声音，保留缓存供下一局复用。

## 资源与配置

新 WAV 位于 `Assets/Art/Audio/Environment`，地址为 `Audio_Waves`、`Audio_Wind`、`Audio_Rain`、`Audio_SeagullCall`、`Audio_WingFlap`、`Audio_StepGrass`、`Audio_StepSand`、`Audio_StepWood`。海浪 24 秒、风雨各 16 秒，使用带交叠过渡的循环；三段循环流式加载，短音效使用预解压 PCM。

这些是原创合成的原型音频，可原位替换为正式录音并保留 `.meta` / AA 地址。`Tools/GenerateCoastalAudio.py` 默认只生成缺失 WAV，显式传入 `--replace` 才覆盖已有文件；编辑器普通配置不会重新合成音频或覆盖手动调整的音量和地址。

AudioCatalog 的 WavesVolume、WindVolume、RainVolume、BirdVolume、FootstepVolume 控制各类音量，还可调整海岸 / 海鸥听觉距离、淡入淡出速度、脚步步幅及鸟叫间隔。原来的 MusicVolume、EffectsVolume 和各音频 Gain 继续生效，总音量静音覆盖全部声音。

风声由 WeatherManager 的统一风力倍率驱动，与树叶摆动和云速共享晴雨变化及阵风；倍率变化不重播循环，详见 [植被风动](VegetationWind.md)。

固定共 16 个 AudioSource：音乐 2、原有界面 / 操作音效 8、环境循环 3、鸟类声音 2、脚步 1。环境声音不会抢占工具或 UI 音源。仍只有一个持久 AudioListener，远近与声道由角色和镜头位置计算；不为每只海鸥创建音源。

## 生命周期与验证

PlayerMovement 只上报实际贴地位移，AudioManager 绑定 / 解绑事件并决定脚步声音。CoastalBird 记录瞬时惊飞次数，由 AudioManager 观察，与海洋原有受惊行为共用状态，不改变生态规则。Clear / 取消加载释放所有 AA 句柄并清理音源和事件，重复进入不会重复绑定。

`CoastalAudioFlowVerification.Run` 使用打包 AA 内容验证循环导入、数量有界的音源、海浪距离、天气切换、实际草地 / 沙滩 / 码头 / 室内移动、静止 / 动作锁 / 传送 / 暂停保护、海鸥惊飞、昼夜鸟叫、总音量、室内与菜单释放、重新进入及白天不保存。

`AudioFlowVerification.Run` 回归既有音乐、工具、交易、MVC 按钮、音量设置、加载失败和取消等行为。
