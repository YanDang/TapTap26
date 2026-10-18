# gamejam 内容接入说明

分支：`integrate/claude-boss-ult-gear`。源 `E:/gamejam`、原始对照 `E:/gamejam_wt/taptap26_ref` 只读；不 push。

## P0：测试基础与尺度对齐（已验证）

### 修改边界
- 新增 `Assets/Scripts/Integration/TapCombatScale.cs`：正式战场加载后一次性调整运行时玩家 `baseMoveSpeed`、`rollSpeed`、CameraController `minZoom/maxZoom` 与相机尺寸；不重生成、不保存正式场景，不改共享控制器源码。普通怪速度 1.35 保持原值。
- 新增 `TapSelfTest.cs`：`-tapselftest` 加载正式 `BiomechanicalCombatScene`，实际执行移动、闪避和存档恢复，GPU framebuffer 截图；正常启动不运行测试。
- 新增 `TapPrefsSnapshot.cs`：Windows 原生注册表 TAP_* 全命名空间快照，保留类型、原始字节和不存在状态；退出前卸载场景写入者再恢复。
- 新增 `Assets/Editor/TapWindowsBuild.cs` 与 `Tools/run_tap_selftest.py`：构建菜单/CLI、子进程监督、恢复前独立核对、超时恢复与磁盘恢复日志。

### 尺度依据与结果
源指定 `40_boss_idle.png` 主角被遮挡，改用同源真实 `00_overview.png` 的可见主角轮廓，人工估计 235±5 px / 1080。由源 ortho=5.5 推算视觉高度约2.394世界单位（设计的2m为近似），源移速4.5对应约1.880身高/秒。不是直接使用2.25身高/秒；截图推算有姿态和边缘误差。

最终运行：主角视觉高度0.8776531，速度1.65005589，翻滚速度3.50636864，ortho=2.01673532，缩放范围0.8066939–4.03346968，占屏高0.217592537。原配置速度4、roll8.5、ortho5、缩放2–10。最终二进制的基线模式也实测通过。

真实三图并排：`E:/gamejam_wt/integ_b_evidence/P0/source-before-after.png`。每张完整1920×1080帧等比缩到960×540，仅增加标题，没有合成场景内容。视觉审查：改前明显偏小；改后约22%屏高，与源基本一致；无明显错误材质。状态条叠头顶为既有表现。

### 可复现入口
```text
E:/unity/Downloads/Editor/Unity.exe -batchmode -quit -projectPath E:/gamejam_wt/taptap26 -executeMethod TapWindowsBuild.Build -logFile E:/gamejam_wt/integ_b_evidence/P0/build-final.log
python Tools/run_tap_selftest.py --output E:/gamejam_wt/integ_b_evidence/P0/final
python Tools/run_tap_selftest.py --baseline --output E:/gamejam_wt/integ_b_evidence/P0/baseline-final
```
构建菜单：`TapTap/Build/Windows Development`；默认产物 `Builds/Windows/TapTap26.exe`。运行前确保无同工程Editor和同存档命名空间Player。

### 真实验收与限制
- `P0/build-final.log`：Succeeded，0 errors、1 warning，Unity最终退出码0；日志前部存在许可刷新失败，但未阻止构建。实际使用2022.3.57f1，不声称验证了62f3c1。
- `P0/final/selftest.txt`：20 passed / 0 failed；RTX 5080真实GPU；实际位移0.41934967，预期0.419349849。
- `P0/baseline-final/selftest.txt`：16 passed / 0 failed。原 `baseline/` 保留15/1失败历史（旧测试只按roll时长等待，未覆盖现有反击动作，移动计时口径也已修正），不冒充通过。
- `P0/final/supervisor-result.json`：player exit=0，监督器恢复前已一致、恢复后一致。原TAP键数量为0；非空int/float/Unicode/string/空值/新增键测试由进程内夹具覆盖。超时/崩溃恢复实现存在，但故障注入未验证。
- 手势pinch、完整导航/家具/温室/每日门规则并未做人工全回归；P0不宣称P6通过。
- Unity自动改动的ProjectVersion与packages-lock已还原；正式场景无修改，`.vsconfig`不提交。

## P1：共享类最小扩展（已实现并运行验证）

### 接入接口及默认行为
- `EnemyController.CombatPolicy` 为可选运行时 `EnemyCombatPolicy`，没有策略的普通怪继续原受击、削韧、Pending 1.5倍伤害、安抚和致死处决。新增策略文件及 Unity 生成的 `.meta`，不复制HP，不判断Boss名称。
- `ExternalAI(enemy, dt)` 位于暂停、出场、麻痹、反戈和硬直处理后，取代普通移动/动画/AI；组件保持启用并留在 AllEnemies。`CombatWorldPaused` 同时覆盖输入锁、建造暂停、TimeManager顿帧。外驱动使用 `ExternalParryWindow`，不读取普通怪过期攻击时钟；Start不重置外驱动状态。
- 受击策略可拒绝伤害、改伤害/削韧，选择是否打断AI、是否应用虚弱倍率。控制过滤覆盖速度击退、协程击退、重砸直接位移、减速、麻痹、两种安抚、嘲讽、淋湿和硬直。外驱动不参与普通转火家具/让位抓取逻辑。
- `CanExecute` 在玩家加锁前及最终结算重检；策略接管 Parried 与 ExecutionDamage，允许非致死处决；拒绝/结算后不遗留Executing。玩家处决协程用finally恢复动作锁，OnDisable明确停止该协程，避免禁用组件后旧协程继续扣血。
- `Died` 只在真正Die分支、死亡标记置位后发出，每条生命一次；回调逐个异常隔离；不使用OnDestroy判胜。
- 玩家新重载 `TakeDamage(damage, attacker, canBlock)` 返回 Ignored/Dodged/Blocked/Damaged；新入口只格挡真实来源，不寻找旁怪；不可格挡仍尊重无敌。旧单参数重载保留原附近怪查询语义。普通怪命中接来源，原提前判闪避分支补通知。
- `DodgeStarted/DodgeEnded/PerfectDodge`、`PerfectDodgeCount/LastDodgeWasPerfect`：实际翻滚协程开始发开始事件；正常结束、安全复位、禁用、复活路径防重结束；0.25秒内真实伤害判定被翻滚避开时每次最多一次完美事件。单纯临时无敌不触发完美事件。本阶段不增加完美闪避美术和装备逻辑。
- `DealDamage` 与 `DamageDealt(enemy, actualHpLost)` 接普攻、重砸、翻滚反击和处决；仅通知实际HP减少。家具DOT/反伤和推撞没有明确玩家归属，不冒算为玩家主动伤害充能；后续若需推撞充能，应给Kick传递显式发起者，而非根据距离猜测。

### 实际验证
- `E:/gamejam_wt/integ_b_evidence/P1/build-final.log`：Windows Development构建 Succeeded，0 errors / 1 warning；实际Unity版本仍为2022.3.57f1。
- 同目录 `final/selftest.txt`：**51 passed / 0 failed**，包含P0全部测试和31项P1断言。正式战场Adapter初始化后生成测试章鱼，不生成/保存正式场景。
- 普通默认回归：HP/削韧、弹刀Pending、1.5倍率、减速/麻痹/淋湿、致死处决及死亡回调重入防重。
- 策略回归：外部AI注册与独占、建造/覆盖场景/顿帧暂停、控制缩时与免疫、三条击退、霸体/伤害拒绝、处决拒绝/结算重检/非致死恢复/禁用中断、来源格挡/红招/空来源，真实普攻与重砸伤害事件及重砸抗位移，翻滚事件基数。
- `final/supervisor-result.json`：Player exit=0，监督器恢复前TAP原始状态已一致，恢复后也一致。保留初始 `red`（20/1，缺少策略接口）、`interrupt-red`（44/1，真实禁用处决继续结算缺陷）及修复前首轮 `green`（44/0），不覆盖失败历史。并非所有新增行为都有独立先失败的TDD证据。
- GPU真实截图：`final/target-after.png` 与 `final/p1-combat.png`。图像已检查：主角、章鱼和正式地砖正常，无明显粉色丢材质；章鱼顶部出框及状态条遮挡为本次普通测试模型取景限制，不声称完整Boss视觉验收。
- 复现：先执行P0的 `TapWindowsBuild.Build`，再 `python Tools/run_tap_selftest.py --output E:/gamejam_wt/integ_b_evidence/P1/recheck`。

### 范围与限制
P1只提供共享能力，Boss架势/12%规则/技能、F9/-tapboss、装备和大招属于P2–P5，尚未实现。家具完整归属、真实QTE输入、全部导航/移动端手势、死亡复活后的第二条生命事件以及全部中断时序未做穷举，不能把51项视为P6验收。无正式场景改动；自动版本/锁文件还原，`.vsconfig`不动，不push。

## P2 最终收尾验证

## 实现与修复
- 接入源分层Boss rig/动画、贴图、材质及meta；新增BossEncounterController、BossBrain、BossAttackShape、BossGroundEffects、BossCombatDisplay、BossTest场景/构建入口。正式战场不重生成，EnemyController保持唯一HP/注册/死亡所有者。
- Boss参考高度为主角2.5倍、HP按基础攻击80次换算；三格架势、12%非致死处决、70/50/30有序阈值、致死跨阈值延迟、阶段技能/预警、家具掩护/地面元素、暂停、抓取、复活保留进度；共享控制来源接家具去重，普通怪默认行为保留。战斗中采集门拒绝交互，无奖励/存档扩展。
- baseline故意不安装TapCombatScale；BossQualityGates改为生成Boss前读取主角SpriteRenderer世界bounds高度，不再空引用。保留2.5倍/.001原断言，不删、不跳过、不放宽测试。

## 最终证据
证据根目录 `E:/gamejam_wt/integ_b_evidence/P2/`，本次最终候选为closeout，旧final/baseline-final保留。
- `build-closeout.log`：Succeeded，errors=0 warnings=2，Editor exit=0；实际Unity 2022.3.57f1。许可刷新报错未阻止构建，不声称62f3c1验证。
- `closeout-normal/selftest.txt`：**97 passed / 0 failed**。
- `closeout-baseline/selftest.txt`：**93 passed / 0 failed**；同一最终二进制完整P1/P2回归，差4项为仅启用尺度时适用的P0断言。旧baseline-final 65/1空引用不覆盖。
- 两次`supervisor-result.json`：player exit=0、监督恢复前原TAP状态一致、恢复后一致；原TAP键数0，非空类型/原始字节夹具通过。
- 覆盖真实-tapboss及独立BossTest入口、普通怪回归、八招几何边界/预警、黄红段、光束最近掩体/后方不保护、斩击2m内外、门耐久、元素叠加/暂停/到期、阻挡占格/越界、原步态骨骼变化及交替落脚、真实复活、禁用释放抓取、过期处决、12%规则、连续阈值和唯一死亡。技能/阈值部分使用Player内手动Tick及反射生产结算方法；不等于全键鼠通关。

## 实际F9与视觉审查
- 相同构建不带-tapboss/-tapselftest启动，菜单开启漫游进入无Boss战场；background点击返回background_unavailable，按驱动反馈升级foreground SendInput后成功。随后background F9 PostMessage，真实截图出现Boss、无名夜王血条和红色预警。已验证实际键消息入口，不是直接调用StartEncounter；不声称人工物理键盘验收。见`f9/observation.md`及会话computer_use前后截图（未另落盘PNG）。
- F9普通游玩正常写存档：`f9/result.json` before_recovery_equal=false、raw_restore_equal=true。监督器只终止owned player后恢复原始TAP状态；player_exited=1是有意终止，不冒称玩家自行恢复通过。
- 已审核本次`closeout-normal/p2-full-idle.png`和`p2-full-walk.png`：定机位Boss全身入框、尺度约2.5倍，无明显粉色错误材质；侧身肢体重叠、披风遮腿、接地感弱，主角状态条压帽子/HUD拥挤仍在。单帧不能证明完整步态、滑步或穿地。
- **F9实战跟随镜头中Boss活动动作头顶出框/与顶栏重叠**，明确保留后续镜头/表现待办；定机位通过不代表实战全时段视觉通过。本轮不扩大实现，不宣称P3或P6完成。

## 复现与边界
```text
E:/unity/Downloads/Editor/Unity.exe -batchmode -quit -projectPath E:/gamejam_wt/taptap26 -executeMethod TapWindowsBuild.Build -logFile E:/gamejam_wt/integ_b_evidence/P2/build-recheck.log
python Tools/run_tap_selftest.py --boss --output E:/gamejam_wt/integ_b_evidence/P2/recheck-normal
python Tools/run_tap_selftest.py --boss --baseline --output E:/gamejam_wt/integ_b_evidence/P2/recheck-baseline
```
P3事件契约见`P2/handoff.md`。未验证：移动端手势、长时间随机实战、全家具组合、全部事件重入/中断、故障注入恢复及P3电影化表现。提交前还原ProjectVersion/packages-lock，仅源码/资源/meta/测试报告，不提交.vsconfig、截图/构建物/大日志；源和_ref只读，不push。

## P3 最终收尾：功能双模式通过，视觉部分通过（不宣称完整美术验收）

### 实现与最小修复
- 新增 BossRift、BossUltDirector、BossMoveFx、BossFxAssets 与 URP BossPresentation shader：XY裂隙/天眼、叹息特写、消失与出现、巨爪/交叉斩、转阶段碎片和八招FX。表现读取 BossBrain 的已结算形状快照，不另行判伤；取消事件统一清理。
- 新增 BossAudio、MusicDirector 和源Boss音频/BGM、CREDITS及meta；暂停/恢复与取消警告音乐接入既有暂停语义。CameraController只增加遭遇期间头部余量及暂停保护，不接P4装备。
- 确认并修复新生FX首帧误扣出生前整帧时间的问题：PNG编码/300ms阻塞导致Time.deltaTime约0.333秒，超过短FX寿命。首帧只扣出生后时间，后续正常计时；保留原可见断言，并增加八招首Impact恰好一次及正常到期断言。`stall-red-baseline`保留114/1失败（P2_Claw5）；旧final-baseline 114/1（P2_SpinThrust）亦不覆盖。
- 天眼截图改为有界等待真实裂隙open>=.95，再验证虹膜/瞳孔实际尺寸；不是缩减原断言。巨爪尖端渐细；黑边改为低排序、无输入拦截Canvas，避免IMGUI盖住HUD。

### 最终候选与真实执行
证据根：`E:/gamejam_wt/integ_b_evidence/P3/`。最终候选为 `build-closeout.log`，源码在构建后未再修改；SHA256绑定见外置 `closeout-manifest.json`（源码、Player与Assembly-CSharp.dll）。
- 构建进程 `proc_9e137dda60bc` / PID28588：已确认退出0；日志 **Succeeded errors=0 warnings=2**，Windows Development，实际Unity **2022.3.57f1**。不宣称2022.3.62f3c1或还原锁文件后的重新构建已验证。
- `closeout-normal/selftest.txt`：**131 passed / 0 failed**；进程 `proc_5e08ba084d5f`，退出0。
- 同一二进制 `closeout-baseline/selftest.txt`：**127 passed / 0 failed**；进程 `proc_8f3965c6fca9`，退出0。差4项仍为P0尺度专属断言，不跳过共同回归。
- 两轮1920×1080真实 **NVIDIA GeForce RTX 5080 Laptop GPU**，保留完整P0–P2回归，P3覆盖暂停/恢复、天眼真实张开、权威斩击、自然结束/禁用恢复、30%双斩、八招出生前慢帧及FX到期、URP支持、32项Boss/音乐资源非零PCM读回。
- 两份 `supervisor-result.json` 均：exit_code=0，player_restored_before_supervisor=true，post_exit_raw_restore=true，original_TAP_count=0。原命名空间为空；非空类型/原始字节由进程内夹具覆盖。不是依赖监督器补救才恢复。

### 本次实际GPU截图审核
已审阅最终normal图，不使用旧图冒充本次结果；baseline天眼也已复核。
- `p3-sky-eye.png`：白色眼部、虹膜与深色瞳孔已清楚可辨，天眼不再只有裂隙；baseline同样可辨但瞳孔偏小、对比仍可加强。
- `p3-sigh-rift.png`：上下电影黑边可见，Boss血条、战斗/日期等HUD仍在黑边上方显示；不再由黑边盖掉顶栏。但特写Boss下部被画幅裁切，HUD跨边界布局仍拥挤。这里只验黑边层级，不宣称全身取景通过。
- `p3-giant-claw-emerge.png`、`p3-double-cleave.png`：可见单向/交叉紫色斩痕和淡色爪线，双Impact功能通过；**巨爪实体和吐出动作辨识不足，长直条贯屏、中心角色/伤害重叠，双爪美术可读性未通过完整验收**。不以物体数/测试PASS替代视觉质量。
- `p3-cancel-restored.png`：黑边消失、场景铺满、HUD完整；取消后Vanish/镜头数值恢复断言通过。中心角色重叠，截图不足以证明Boss全身独立可辨。

### 未验证及交接边界
- 未做人工听感/混音验收、最终候选F9重新键鼠实战、全时段跟随镜头取景、移动端/其它宽高比、长时间随机战斗、全家具组合与所有中断交错；超时/崩溃存档恢复故障注入仍未验证。部分测试通过手动Tick/生产入口反射驱动，不是完整人工通关。
- 本轮按收尾范围冻结，不继续视觉迭代或扩到P4；P3功能回归已通过，以上视觉不足明确保留，P6未验收。
- 自动ProjectVersion/packages-lock已还原；无场景差异需要还原。仅提交P3源码、所需资源/meta及报告；`.vsconfig`保留未跟踪，不提交构建、截图、大日志或外置证据。`integ_b_report.md`位于仓库外，仅更新磁盘，不属于本仓库提交。不push。
- 构建与两次自测均已退出，提交后再次核查owned Unity/Player进程；外工作区CrashHandler不动。

复现（串行运行，勿共享TAP命名空间并发）：
```text
E:/unity/Downloads/Editor/Unity.exe -batchmode -quit -projectPath E:/gamejam_wt/taptap26 -executeMethod TapWindowsBuild.Build -logFile E:/gamejam_wt/integ_b_evidence/P3/build-recheck.log
python Tools/run_tap_selftest.py --boss --output E:/gamejam_wt/integ_b_evidence/P3/recheck-normal
python Tools/run_tap_selftest.py --boss --baseline --output E:/gamejam_wt/integ_b_evidence/P3/recheck-baseline
```

## P4 最终收尾：功能矩阵通过，完整表现验收未通过

### 实现、共享类改动与使用
- `Assets/Scripts/HeroUlt/`：TapHeroUlt统一拥有三款动作/能量/取消；TapUltProjectile做XY扫掠命中；TapUltBurn按世界时间燃烧、刷新不叠加；TapUltVisual/UltArcGraphic/TapUltUI提供URP可显示圆环、电弧、三卡选择与可点击图标；TapUltTests接入原完整自测。`Assets/Resources/HeroUlt/`保留源三款图标及meta，新增充能环资源。
- PlayerController：大招锁覆盖移动、格挡、翻滚、蓄力、处决、摆放等入口；伤害/抓取接吸收，冲刺期无敌、炮击霸体不免伤；成功处决额外20、弹反5、死亡清零/禁用取消。EnemyController仅普通AI目标选择加入6源单位回响嘲讽，Boss不受该嘲讽。
- HeroActions复用现有机械壳拳、壳体翻转与持续缩壳，回响不被普通2.5秒看门狗提前清掉。TimeManager增加有所有者的动作慢速，仍由原时间管理器统一赋值；未配置/不施放时保持原分支。
- TapPrefsSnapshot和Python监督器把`GJ_UltChoice`纳入原始字节恢复，与`TAP_*`一起隔离，不写材料/背壳仓库。TapSelfTest追加P4，保留P0–P3共同回归。
- 正式战斗场景加载时自动安装；非战斗点“切换大招”选择，战中禁止切换；满能量点图标或R释放，回响左键或再次释放可提前结束。Boss入口仍为F9（Development）、`-tapboss`与BossTest。新UI未改既有背包/合成入口。
- 源数值按主角普攻伤害单位换算：实际扣血 / 初始CurrentAttackDamage ×4充能；伤害倍率、半径与位移分别使用DamageUnit和Unit。不是把源6伤直接当宿主6HP；面板简写沿用源单位。P5装备未做。

### 红测与最小根因修复
- `block-red-baseline/selftest.txt` **172/1，exit=1**：无来源普通格挡漏5充能。DoParry原有5奖励；普通无目标格挡走另一分支，因此只在该分支增加5。有来源分支调用DoParry后提前return，不经过新奖励。最终新增真实带来源命中测试，验证恰好5而不是10。
- `aim-red-baseline`保留旧方向失败；炮击未锁定时改为角色朝向，仅钳臂自动找最近目标。最终测试证明敌人在背后也不擅自改变炮击方向。
- 先前释放失败源于测试敌人触发宿主自动攻击。测试等待实际CanBeginUltimate自然恢复，并保留攻击期拒绝且不扣能量断言；没有清攻击标志或放宽生产动作锁。旧normal/baseline失败证据保留。

### 最终同一候选验证
证据根 **`E:/gamejam_wt/integ_b_evidence/P4/`**；`final-manifest.json`记录源码、资源、测试、实际构建配置、exe及Assembly-CSharp.dll的SHA256。构建后未改生产/测试代码。

| 门禁 | 真实结果 | 证据 |
|---|---|---|
| Windows Development构建 | exit 0；Succeeded，errors=0，warnings=2 | build-final.log |
| normal完整回归（含-tapboss） | **178 passed / 0 failed，exit 0** | final-normal/selftest.txt、supervisor-result.json |
| 同一二进制baseline完整回归 | **174 passed / 0 failed，exit 0** | final-baseline/selftest.txt、supervisor-result.json |
| 原始存档恢复 | 两轮player_restored_before_supervisor=true、post_exit_raw_restore=true；original_TAP_count=0 | 两轮supervisor-result.json、prefs-before.json |

- baseline只关闭P0尺度适配，不关闭P4；相差4项为P0尺度专属断言，不是删共同测试。测试包括无目标不耗能、两种格挡各5、真实伤害充能、拒伤不充能、动作锁、建造/工坊暂停、炮击霸体/爆炸/燃烧刷新与四次伤害、回响吸收红黄/旧伤害/抓取/8层/自动结束、禁用清理，以及真实Boss的重锤8倍+2架势、不位移、回响10倍与架势上限、炮击燃烧。
- 三款通过生产接口运行；部分Boss探针冻结Boss调度并手动Tick以隔离数值，不冒充自由实战。原始非空类型/字节夹具通过；真实原命名空间为空，崩溃/超时恢复故障注入未测。
- 实际环境Unity **2022.3.57f1**、D3D11、**NVIDIA GeForce RTX 5080 Laptop GPU**、1920×1080。ProjectVersion/packages-lock已恢复仓库声明的2022.3.62f3c1配置；不宣称恢复配置后重新构建或该版本Editor已验证。

### 最终GPU技能/UI审核（真实限制）
已通过图像工具逐张复核final-normal七张P4截图，以及final-baseline的ready和echo-eight，不用旧帧代替：
- `p4-ready.png`：电弧/图标可见；图标中心(380,-180)与背包/合成入口、顶部状态分离；normal/baseline当前16:9静态布局均无新增重叠。`p4-picker.png`三卡名称/图标/说明可读，无截字；缺已装备高亮，弹窗外HUD未压暗。
- `p4-arm-impact.png`：机械构件可见，但角色/敌人相互遮挡，冲击接触点与挥拳方向不够清楚；没有源残影、速度线及抛物线旋转拖尾，不算完整重锤美术通过。
- `p4-cannon-charge.png`：炮口/蓄力辨识偏弱；`p4-cannon-explosion.png`只见黄橙圆环，不是源火球、火苗或烟雾。未实现源开炮镜头推近；非Boss击飞用宿主平面击退而不是源抛物线飞出。
- `p4-echo-eight.png`可见壳体，但**两模式截图仍显示“壳中回响4%”，未明确显示8层标签**；同帧测试断言Stacks=8通过不能代替HUD八层显示证据。截帧/Update时序是否为原因未另验证，保留为视觉未过项。`p4-echo-wave.png`青色冲击环清楚，但粗环遮挡近身细节；中央TARGET/硬直/伤害提示拥挤，敌人顶端裁切。
- 按钮测试实际使用Button.onClick.Invoke，只验证回调及释放锁，不是触屏命中/指针穿透实测；R代码入口存在，但本轮未验证物理键盘输入。移动端、其它宽高比、安全区、展开背包/合成后布局、人工混音、全墙/家具组合、长时实战与所有取消交错未验证。
- 结论：**当前功能回归门禁关闭；视觉只做已知限制下的复核，不宣称三款与源等效或完整P4视觉验收。** 不扩P5/P6，遗留视觉由后续阶段/验收方决定。

### 提交卫生与执行权
- ProjectVersion.txt、packages-lock.json已还原；本轮无.unity场景差异。只提交P4源码、必要资源/meta、仓内报告/handoff；`.vsconfig`保留不提交，外置证据/构建物/截图/大日志不提交。不push。
- `E:/gamejam_wt/integ_b_report.md`在仓库外，仅落盘更新，不属于本仓库commit。
- 串行进程`proc_448a8a579669`已退出0，构建及两轮player皆退出；进程核查无本工作区Unity/Player。只见`integ_ours`另一工作区的CrashHandler，未结束他人进程。

复现（同一候选，必须串行，勿与其它共享存档player并发）：
```text
E:/unity/Downloads/Editor/Unity.exe -batchmode -quit -projectPath E:/gamejam_wt/taptap26 -executeMethod TapWindowsBuild.Build -logFile E:/gamejam_wt/integ_b_evidence/P4/build-recheck.log
python Tools/run_tap_selftest.py --boss --timeout 300 --output E:/gamejam_wt/integ_b_evidence/P4/recheck-normal
python Tools/run_tap_selftest.py --boss --baseline --timeout 300 --output E:/gamejam_wt/integ_b_evidence/P4/recheck-baseline
```
