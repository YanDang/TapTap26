# gamejam 内容接入说明（Boss「无名夜王」/ 主角大招 / 装备改装）

分支：`integrate/claude-boss-ult-gear`（基于你的 main「1.0」，**没有动 main**，合不合、怎么合由你决定）。
本说明是总览；逐阶段的技术细节、测试证据和接口契约在同目录 `技术记录_Hermes.md` 和 `P2/`~`P6/handoff.md`。

## 1. 一句话
把我们工程里的 Boss 战、三款主角大招、15 件装备改装台搬进了你的 XY 平面 / URP / Rigidbody2D 工程，**按你 `BOSS_COMBAT_AGENT_GUIDE.md` 的约定接入**：EnemyController 仍然是唯一的实体和血量，共享类只做可选扩展，没有挂策略的普通怪行为不变（有回归自测证明）。正式场景没有重新生成、没有改序列化内容。

## 2. 怎么玩到 / 测到
| 入口 | 说明 |
|---|---|
| **F9**（编辑器 / Development Build） | 在当前战场里立刻刷出 Boss |
| 命令行 `-tapboss` | 启动后直接进 Boss 战 |
| `Assets/Scenes/BossTest.unity` | 独立 Boss 测试场景（打开直接开战） |
| 命令行 `-tapselftest` | 全量自测，结果写 `Builds/Windows/selftest.txt`（最终 **312 项全过**）；`Tools/run_tap_selftest.py` 会自动备份 / 逐字恢复 `TAP_*`、`GJ_*` 存档键，不会弄脏你的存档 |
| 命令行 `-tapbaseline` | 关闭下面第 3 节的尺度对齐，用你原来的移速和镜头跑 |

**Boss 正式出场时机（比如第 7 天的裂隙门）没有做**，留给你定。接口：`BossEncounterController.Instance.StartEncounter()`（返回 BossBrain）；Boss 死亡有一次性事件（`EnemyController.Died`），奖励暂时只打日志、不写存档。

## 3. 移速与镜头（策划要求的对齐）
你那边主角在屏幕上很小、走得很快（主角占屏高 7.7%，每秒走约 4.6 个身高），我们这边是约 18~22%、约 1.9 个身高 / 秒。为了让 Boss（主角 2.5 倍高）和招式范围读得清楚，按我们的尺度做了**运行时**对齐（`Assets/Scripts/Integration/TapCombatScale.cs`，战斗场景加载后一次性设置，不改场景 / prefab）：

| 参数 | 原来 | 现在 |
|---|---|---|
| 相机 orthographicSize | 5 | ≈ 2.02 |
| CameraController 缩放范围 | 2 – 10 | ≈ 0.81 – 4.03（手机捏合照样可用） |
| 主角 baseMoveSpeed | 4.0 | ≈ 1.65 |
| 主角 rollSpeed | 8.5 | ≈ 3.51 |
| 普通怪移速 | 1.35 | 不变 |

觉得不合适：改 `TapCombatScale` 里的常数，或者直接删掉这个组件 / 用 `-tapbaseline`，一切回到原样。

## 4. 按键（避开你已有的键）
你的：空格 = 缩壳格挡、F = 采集门、B = 建造、Tab = 工坊、滑动 = 闪避——**全部保留**。新增：
- **Shift**：触发你的同一个闪避入口（PC）；翻滚结束的自动反击 / 完美闪避（敌人判定前 0.25 s 内起翻）接在你的闪避上。
- **按住空格 > 0.25 s**：举壳（短按仍然是格挡）。
- **Q** 抓钩、**R** 大招（满能量也可以直接点左上大招图标）、**G** 改装台（非战斗时）。
- 移动端：大招图标可点、抓钩按钮、改装台入口按钮。

## 5. 改了你哪些共享类（全部是“可选扩展”，默认不生效）
| 文件 | 改了什么 | 不挂新东西时 |
|---|---|---|
| `EnemyController.cs`（+92 行） | 可选 `CombatPolicy`（受击 / 控制抗性 / 处决资格策略）、`ExternalAI` 外部 AI 驱动、`Died` 一次性死亡事件、诱饵目标、预警剩余时间 | 原逻辑 |
| `PlayerController.cs`（+170 行） | 带来源和“可不可格挡”的 `TakeDamage(damage, attacker, canBlock)` 重载（旧单参数版保留）、`DodgeStarted/DodgeEnded/PerfectDodge`、`DamageDealt` 事件（大招充能用）、大招 / 装备的动作锁和移速攻速倍率、空格长短按分流 | 原逻辑 |
| `CameraController.cs`（+5 行） | Boss 战期间给 Boss 头顶留出取景余量；遭遇暂停时不动 | 原逻辑 |
| `Core/TimeManager.cs` | 带“所有者”的动作慢速（大招用），仍由 TimeManager 统一赋值 timeScale | 原逻辑 |
| `FurnitureObject.cs` | 抓钩拉家具时维护新旧格子占用；固定设施拒绝拉动 | 原逻辑 |
| `Hero2D/HeroActions.cs` | 复用机械壳拳 / 缩壳给大招“壳中回响” | 原逻辑 |
| `Crafting/PlayerSessionData.cs`（1 行） | 改装台打开时算作输入锁 | — |
| `Auras/*`（4 个文件各 1~2 行） | 家具伤害带上来源，让 Boss 的控制抗性能去重 | 原逻辑 |

## 6. 新增文件在哪
- `Assets/Scripts/Boss/`：`BossEncounterController`（遭遇）、`BossBrain`（招式 / 阶段 / 架势）、`BossAttackShape`（判定形状，画面和判定同一套尺寸）、`BossGroundEffects`、`BossCombatDisplay`（顶部血条 + 70/50/30 刻度 + 架势）、`Anim/`（Boss 骨骼动画）、`FX/`（裂隙、开天、招式特效）、`Audio/`（BossAudio、MusicDirector）。
- `Assets/Scripts/HeroUlt/`：三款大招（钳臂重锤 / 贝壳加农 / 壳中回响）+ 选择面板 + 充能环。
- `Assets/Scripts/Gear/`：15 件装备战斗逻辑、改装台 UI、特效；材料直接用你的背壳仓库（15 种核心材料 id 在你的 `BiomechanicalMaterials.json` 里都在，没有做映射）。
- `Assets/Scripts/Integration/`：`EnemyCombatPolicy`、`TapCombatScale`、自测框架。
- 美术 / 音频：`Assets/Art/Characters/Boss/`（PSB 拆层 + 2D Animation 绑骨，.meta GUID 保留）、`Assets/Resources/Audio/`（Boss 音效、BGM，版权说明见 `CREDITS.md`）、`Assets/Resources/HeroUlt/`、`Assets/Resources/Gear/`。
- Shader：自定义效果改成了 URP（Universal2D）兼容。

## 7. Boss 规则速查（按你文档 9.2 节要回答的问题）
- 一阶段斗篷 100%→50%，50% 化形（无敌约 2.5 s），二阶段 50%→0；**70% 和 30% 各放一次开天大招**（30% 那次是 X 形两刀，放完力竭、架势直接满）。同一下跨多个阈值按顺序触发，每个阈值只一次。
- **黄光 = 能格挡，红光 = 只能闪避**；冰蓝光 = 破绽可处决。
- 不能被普通处决秒杀：格挡成功 +1 架势，满 3 格 → 破绽 → 处决扣 12% 最大血，Boss 恢复。
- 击退免疫（改成 +1 架势）、麻痹时长减半、减速最多 30%，安抚 / 嘲讽类免疫，淋湿正常。
- 开天和独眼光束能被掩体家具挡下（家具碎，后面的玩家不受伤）；采集门打不坏、Boss 战期间采集门禁用。
- 玩家死了按你原来的原地复活，Boss 不重置不回血。

## 8. 已知问题 / 没验证的
- **没在真机上测**：手机多指 / 捏合、其他屏幕比例、长时间实战。
- 部分表现还粗糙：开天巨爪的实体感、地缝吐出、二阶段交叉斩的辨识度；部分大招残影 / 火球烟雾；装备“水柱”是简化几何；某些画面 HUD 偏挤。
- 编辑器版本：我们用 2022.3.57f1 打开 / 构建过你的工程（0 错误），提交前已经把 `ProjectVersion.txt` / `packages-lock.json` 还原成你的 62f3c1，**没在 62f3c1 上跑过**，你打开后先编译一下。
- 你的 `AndroidAutoBuilder` 里签名密码是写死在代码里的，建议改成环境变量（我们这边接入你的内容时已经这样改了）。

有问题随时找我们（策划这边 + 各个 AI 代理的完整日志都留着）。
