# gamejam 内容接入说明（Boss「终焉海神」/ 主角大招 / 装备改装）

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

**Boss 正式入口现为第 7 天初始采集门**，详见下方“终焉（第 7 天）”。开发接口 `BossEncounterController.Instance.StartEncounter()` 保留；Boss 死亡有一次性事件（`EnemyController.Died`），奖励暂时只打日志、不写存档。

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

## 9. 终焉（第 7 天）

本节为 FINALE-B 更新，覆盖上方历史数值/默认行为说明：普通怪现在统一受到逐日污染，不再是完全固定数值。最终真实 Windows Player 全量回归：normal 516通过/0失败，baseline 511通过/0失败（baseline不执行5项尺度对齐专用断言）。

- 显示名统一为“终焉海神”，类名与文件名不改。
- 初始门通过原始成品名“初始·异界裂隙之门”识别，不依赖坐标。第 7 天 F/点击免费召唤，不扣精力、不刷守卫；0.35s裂隙、1.45s海神生成、0.8s渐显、1.6s定身、2.35s合拢，镜头与黑边随演出恢复。重复交互不会重复生成。真实击败后恢复普通门试炼/温室规则。F9、-tapboss、BossTest 开发入口保留。
- 普通怪在统一 EnemyController.Start 入口初始化污染；门试炼提前配置后不会再叠加。层数=天数−1，上限6；HP×(1+.25×层数)，对主角伤害×(1+.12×层数)，Boss排除。原精英/数量/移速/冷却规则保留。活怪按生成日快照，复活按当前日重新初始化。
- 紫红乘色、低优先级辉光、脚底软光斑和上浮孢子；攻击/状态提示优先。
- 左上主角血条：深底描边、红填充、白残影、受伤闪框、≤30%脉动、数值。原头顶小血条默认保留，是否去掉仍待确认。
- 菌群Lv1/2/3每跳12/16/20、Lv3处决+20；扎根Lv3每2s+10；浮囊保命+15。原100血与Boss 20/40/60%伤害不改。

验证入口仍为 Tools/run_tap_selftest.py，新增 TapFinaleCorruptionTests / TapFinaleHeroTests / TapFinaleDoorTests，正常与 --baseline 都执行新增测试。

运行命令（先用 TapWindowsBuild.Build 生成 Builds/FinaleB/Player/TapTap26.exe）：
`python Tools/run_tap_selftest.py --exe Builds/FinaleB/Player/TapTap26.exe --output Builds/FinaleB/normal-final --timeout 300 --boss`
以及同命令把输出改为 baseline-final、--boss 改为 --baseline。两个进程依次执行，不能同时运行以免共用存档。两次 Player 自行恢复存档及外部监督恢复核验均 true。

证据位于本机 E:/gamejam_wt/taptap26/Builds/FinaleB/：build-final.log，normal-final/selftest.txt，baseline-final/selftest.txt；normal-final 下五张 finale-*.png 为真实GPU截图，final-contact.png为其缩小拼图。大型构建和证据不入Git。最终构建0错误/1警告；测试使用国际版 Unity 2022.3.57f1，提交前按任务书恢复原 ProjectVersion 与 packages-lock，未声称在原中国版版本重新构建。

数值换算：以默认守卫 HP100、普攻15、抓取10为基础。旧日倍率为HP 1+.20×层、攻击1+.15×层并Round；新倍率为HP 1+.25×层、对主角伤害1+.12×层，不再取整。第1/4/7天非精英新值分别为100/15/10、175/20.4/13.6、250/25.8/17.2（HP/普攻/抓取）。旧HP为100/160/220，旧普攻为Round(15/21.75/28.5)，旧抓取为Round(10/14.5/19)。精英仍额外HP×1.35、攻击×1.25，新值分别135/18.75/12.5、236.25/25.5/17、337.5/32.25/21.5；旧值对应Round(135/18.75/12.5)、Round(216/27.1875/18.125)、Round(297/35.625/23.75)，实际浮点边界遵循Unity。第1天精英列为公式对照，不改变实际出精英规则。

视觉复核：主角满血100/100、低血25/100无遮挡；扣20后80/100另有运行断言。三怪对比、柔边污染光斑、紫红独眼裂隙与海神登场已可见，无错误粉色方块。裂隙独眼部分被原机械门遮挡，海神身体与披风重叠、提示文字略遮头部区域；保留原素材，未宣称演出美术完美。原头顶血条是否移除待确认。登场临时解除相机边界并恢复，裂隙SortingGroup置于地板之上/Boss之下。

验收边界：门功能走真实OnPlayerInteract入口及真实死亡/复活；BossTest和-tapboss启动已覆盖，未把程序化调用说成新一轮实体键盘F/F9或移动端触摸验收。击败标记当前为场景内状态，不新增跨场景/退出重进的Boss永久通关存档。
