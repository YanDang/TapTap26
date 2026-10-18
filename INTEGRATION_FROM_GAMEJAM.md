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


## P5 最终收尾：15件装备、真实支付与目标XY战斗接入

### 验证状态与证据边界
最终候选串行进程 proc_5a742e17c89f 已退出0：
- build-final-2.log：Succeeded，errors=0 warnings=2；Windows Development产物 Builds/Windows/TapTap26.exe。
- final-normal/selftest.txt：**268 passed / 0 failed**；final-baseline/selftest.txt：**264 passed / 0 failed**。均为最终二进制、RTX 5080 Laptop GPU，包含P0–P5完整自测及新增完美举壳红伤减半/Boss恰好+1架势且不位移断言。
- 两个supervisor-result.json均player exit=0、player_restored_before_supervisor=true、post_exit_raw_restore=true；初始受保护键数量0。
- crafting-final.log：**PASS 205，raw namespace restored=True**。Tools/test_gear_prefs.py再次运行1 test OK（隔离注册表原始值保护）。
- 初次最终构建 build-final.log 编译失败：继承补丁误用AttackType与AddPosture双参数；改用宿主TakeDamage(10,e,false)、AddPosture(this)，不改测试期待，再完成整矩阵。旧close-*失败/通过日志保留，不覆盖历史。
- 候选源码SHA256清单：P5/final-source-sha256.json；全部最终日志/截图位于 E:/gamejam_wt/integ_b_evidence/P5/。
- 旧 proc_3314d996f19f 已退出0；不是用其旧结果代替此次最终候选。

### 每件逻辑 / 测试映射
共用实现 Assets/Scripts/Gear/TapGearCombat.cs；下列运行探针位于 TapGearTests.Run/Extended（断言名称写入 selftest.txt）。全部15件的Lv1/2/3真实仓库支付、均值与升级另由 TapGearCraftingTests.Run 的205断言覆盖，不能与战斗等级穷举混称。

|装备ID / 名称|接入逻辑|真实测试映射及未穷举项|
|---|---|---|
|shell_mobile 立体机动蟹壳|长按举壳、正面黄伤吸收、红伤减半、耐久/破壳恢复；完美Boss+1架势；Lv3反光束|front yellow / perfect shell red HP half and exactly one Boss posture no push / actual Boss beam reflected；耐久耗尽全等级未穷举|
|shell_static 静电反应壳|格挡/受伤攒3层，下次格挡链雷；湿身增伤，Lv3麻痹|static holds three stacks / fourth block discharges real damage；全目标数/湿身倍率未逐级穷举|
|shell_float 应急浮囊壳|致命伤吸收、回血与1.5秒泡泡；高等级击退/两次机会|lethal bubble saves real player；跨多场遭遇次数及全部等级未穷举|
|arm_spring 弹簧连锁拳|复用宿主闪避反击，不重复启动反击；Lv2/3增加1/2目标，Lv3聚拢|spring actual extra HP recipients Lv1–3；聚拢复杂墙角未穷举|
|arm_eel 电鳗钳|Lv1每4击、Lv2每3击；湿目标直接电击，Lv3跳电|eel cadence no early damage / actual damage and level3 jump Lv1–3；Boss实际HP/麻痹半时长|
|arm_hydro 高压水钳|替换重砸为XY水柱，淋湿/击退；高等级增距、Lv3穿透水洼|hydro actual damage wet and shared displacement；Boss HP wet no displacement plus posture；水洼全时段未穷举|
|arm_grapple 螺纹抓钩|Q/按钮：普通怪拉近，Boss拉玩家，家具通过格网迁移，冷却、Lv3终点伤害|grapple pulls enemy XY / paused / cancelled；furniture AND grid ownership / immovable refusal；boss moves player not boss|
|leg_decoy 翻滚爆破装置|真实完美闪避生成诱饵；Lv2嘲讽，Lv3双错峰爆炸，Boss免嘲讽|decoy actual avoided damage / taunt level gate / actual AI pursuit / timed HP explosion Lv1–3；second detonation|
|leg_jet 喷气滑步|原翻滚提速、结束减速气流；Lv2三充能恢复、Lv3冲撞|jet starts original dodge / spends one of three / recharge frozen / displacement / timed recovery；气流/冲撞全分支未穷举|
|leg_root 珊瑚生根|站定扎根减伤、移动拔根刺伤；Lv2更快，Lv3定时回血|stationary roots / movement unroots；全等级减伤和刺伤数值未穷举|
|core_fungi 荧光共生菌群|6秒未受伤后定时回血；Lv3处决回血|fungi no premature heal / timed HP Lv1–3；处决回血独立分支未穷举|
|core_nerve 镀银神经索|真实攻击间隔缩短，Lv3第五击额外伤害|nerve attack timer Lv1–3；第五击独立伤害未穷举|
|core_siphon 循环虹吸肉管|基础/脱战移速加成，Lv3喷气恢复缩短|siphon real movement speed / multiplier Lv1–3；跨装备恢复组合未穷举|
|core_logic 破损逻辑指令片|完美闪避窗口加长；预警提前0.2秒、不改Boss结算时钟，Lv3慢动作延长|logic warning visible 0.2s earlier without advancing impact clock；全等级窗口边界未穷举|
|core_antenna 泄压感应触角|大招充能倍率，Lv3大招结束补闪避|antenna actual energy Lv1–3；结束补闪避组合未穷举|

### 共享改动与资源
- PlayerController：装备移速/攻速、受伤过滤返回值、普攻/重砸/原反击/处决钩子、Space输入分流、喷气充能及动作互斥。保留既有Shift翻滚/滑动/自动反击，不另建第二套。
- EnemyController：诱饵目标及真实AI追踪、预警剩余时间；BossBrain暴露实际预警时钟，Boss控制仍经策略抗性处理。FurnitureObject.GrappleTo维护旧/新格占用，拒绝固定设施。TapHeroUlt接充能倍率。
- PlayerSessionData将改装台纳入输入锁；TapGearUI提供G非战斗面板、合成/升级/装备服务检查和Q按钮；Canvas排序2000解决旧HUD遮挡。TapGearFx使用目标Universal2D shader，程序化XY效果而非源3D效果逐帧复制。
- 导入15原图标/meta、GearContracts/GearCrafting及meta。15核心材料ID在目标BiomechanicalMaterials.json全部存在、元素匹配，无需映射，见P5/material-audit.md。
- 支付按真实材料实例去重，扣除所有占格引用；先完成材料/装备存储再广播，异常监听器隔离；注入失败恢复键存在性、字符串及槽位引用。不是两个PlayerPrefs键的OS崩溃原子事务。
- TapPrefsSnapshot与run_tap_selftest.py扩展GJ_GEAR_*保护，未知键原始类型/字节与TAP_*一并保存。Tools/test_gear_prefs.py以隔离注册表命名空间测未知二进制/DWORD/Unicode/原本不存在键，不改真实存档。
- 本轮关键修复：装备Start早于Ultimate.Start导致尺度缓存0；家具夹具必须保留注册；回血只累计6秒后的有效时间；2D渲染shader；完美举壳Boss架势与红伤。历史失败日志保留，没有降低断言换PASS。

### GPU复核与未验证项
已通过图像工具复核 final-normal 的 p5-bench / p5-water / p5-shell / p5-logic-warning，以及 final-baseline 的 p5-bench / p5-water，均为本次最终GPU帧：
- 改装台3列×5行15卡、15图标、中文及按钮完整，未被旧HUD压在上面；但半透明面板和边缘仍透出旧战斗/设置HUD。未合成按钮未明确灰化，不能宣称最终交互视觉完美。
- normal与baseline水柱均为不透明青色粗柱，遮挡角色下半身和地面，端点圆环可见；缺少源水流纹理、透明度与飞溅。明确为视觉未过项，不用伤害断言冒充源美术等效。
- 举壳静态壳面可辨、圈线与战斗文字拥挤，普通敌人顶部出框；预警黄色轮廓可见，但角色/状态条/效果重叠影响辨识。
- GPU截图证明真实渲染输出，不是GPU性能测试，不证明全动画、所有视角或全部装备FX美术验收。本轮不扩大需求重做美术。
物理键盘G/Q/Space/Shift、真实移动端触摸命中、其它宽高比/安全区、长时战斗/所有取消交错、复杂家具导航及全装备组合未验证。运行探针直接驱动生产API，不冒充物理输入测试。P0–P4旧视觉限制仍适用，P6未验收。

### 复现与交付卫生
必须同工程单进程，Unity构建完成退出后再启动player：
```text
E:/unity/Downloads/Editor/Unity.exe -batchmode -quit -projectPath E:/gamejam_wt/taptap26 -executeMethod TapWindowsBuild.Build -logFile E:/gamejam_wt/integ_b_evidence/P5/build-recheck.log
python Tools/run_tap_selftest.py --boss --timeout 300 --output E:/gamejam_wt/integ_b_evidence/P5/final-normal
python Tools/run_tap_selftest.py --boss --baseline --timeout 300 --output E:/gamejam_wt/integ_b_evidence/P5/final-baseline
E:/unity/Downloads/Editor/Unity.exe -batchmode -quit -projectPath E:/gamejam_wt/taptap26 -executeMethod TapGearCraftingTests.Run -logFile E:/gamejam_wt/integ_b_evidence/P5/crafting-final.log
```
实际测试环境Unity2022.3.57f1；已清理自动ProjectVersion/packages-lock改动，不宣称恢复到2022.3.62f3c1后的全新环境也已构建。git核对无.unity场景差异，未删除用户场景；自产Tools/__pycache__/run_tap_selftest.cpython-313.pyc已清理；.vsconfig原样保留；源E:/gamejam只读。仓外integ_b_report.md落盘但不属于仓内commit。中文P5提交，不push。process-exit-audit.txt：Unity.exe/TapTap26.exe查询成功且无匹配进程；本工作区执行权已释放。功能回归完成，视觉及上述未验证边界保留，不宣称P6或源效果完全等效。

## P6 历史检查点（已被下方最终结果取代）
- run-1：281 passed / 17 failed，真实Mix/Boss暂停及两扇Collect门往返/采集/每日独立资源通过；首次UI探针在ReturnRoutine结束同帧点击HUD，raycast none；夹具无条件切换导致picker意外开启，污染后续17项。原失败证据保留。存档player恢复前和supervisor恢复后均一致。
- 新候选修正夹具等待真实HUD更新和条件清理，不降低断言；增加普通家具、真实UI raycast+pointer callback；抓钩按钮上移避开格挡，水柱alpha及回响截图等待标签。build-2/run-2串行执行中，不能用run-1为新源码背书。
- Boss期间门生产入口明确禁止Collect，保留规则；Boss真实Mix往返与Boss门拒绝分别验证，Collect往返在Boss结束后。

## P6 最终总负责审核：功能回归通过，整体表现有保留

### 最终候选与独立复跑
证据根目录：`E:/gamejam_wt/integ_b_evidence/P6/`。以下均在最后的装备Canvas隐藏修复后产生，不引用旧final-*作为最终结果。
- `build-closeout.log`：Succeeded，errors=0、warnings=2，Windows Development。日志仍有许可服务报错，不称整份日志无Error；实际Editor为2022.3.57f1。
- `closeout-normal/selftest.txt`：312 passed / 0 failed；`closeout-baseline/selftest.txt`：308 passed / 0 failed。
- `closeout-720p/selftest.txt`：1280×720，312 passed / 0 failed。两种分辨率都是16:9，不证明其它比例。
- 总负责独立运行最终二进制：`parent-verification/selftest.txt`，312 passed / 0 failed，进程退出0；不是仅采信子代理口头结果。
- 上述四轮`supervisor-result.json`均exit_code=0、player_restored_before_supervisor=true、post_exit_raw_restore=true，初始受保护键数量0；保留既有非空类型/字节夹具。
- `closeout-timeout-recovery/supervisor-result.json`：4秒超时，child exit_code=124、恢复前false、监督器恢复后true；外层命令实际退出3（脚本将恢复前不一致映射为3）。这是故障恢复通过，不是完整自测通过，不是Player自己恢复成功。没有验证操作系统断电/监督器一起崩溃。
- 总负责重跑`python -B Tools/test_gear_prefs.py`：1 test OK。
- 源码/测试/Player/Assembly-CSharp.dll SHA256：`final-source-binary-sha256.json`；候选构建后没有再改源码。

### 新增覆盖与必要修复
- `Assets/Scripts/Integration/TapP6Tests.cs`及meta：通过生产入口实际加载Mix，验证Boss血量、阶段、前摇和impact index暂停保持；按钮raycast/pointer事件返回后继续，镜头、主HUD及输入锁恢复。
- Boss期间Collect门拒绝保持原规则；Boss结束后通过两扇真实门进入Collect，采集、返回、同日同门拒绝、两门资源独立、生产存储重新加载及次日可用通过。试炼完成与次日状态为测试夹具，并非人工完成试炼/完整七天通关。
- 普通家具真实受伤、拾取、放下、踢动落定/占格及永久家具抗损回归通过。
- 新按钮经过EventSystem.RaycastAll验证最上层命中，再派发PointerEventData；实际打开选择/改装、抓钩拉敌及释放回响。属于合成指针事件验证，不是实体手机触摸或物理键盘全测。
- `TapGearUI`隐藏非战斗覆盖场景上的装备Canvas并恢复；抓钩按钮上移避开原动作盘。`TapGearFx`水柱改为半透明外层与细内层；`TapUltUI.Caption`提供实际HUD文本读取，八层截图等待真实“回响 8 / 8”，未改数值。
- `Tools/run_tap_selftest.py`新增宽高参数。未降低旧断言，run-1的281/17失败保留。

### 总负责实际GPU图像复核
以下均为独立复跑`parent-verification/`的真实帧：
- `p6-mix.png`：工坊、材料格与配方完整显示，装备/战斗按钮不再残留；原天数/精力进度面板仍显示，无明显错误粉材质。
- `p6-collect-a.png`：温室、返回、采集对象与反馈可见，无战斗装备HUD覆盖；第二扇门证据为`p6-collect-b.png`。
- `p4-echo-eight.png`：左侧明确显示“回响 8 / 8”，关闭P4旧八层显示证据缺口。
- `p5-water.png`：外层半透明、地砖透见；仍为几何柱/亮带，身体细节受蓝色覆盖，末端与抓钩区域视觉重叠，不判定为源水流美术等效。
- `p6-restored-hud.png`为返回战场恢复帧；720p对应全套帧位于`closeout-720p/`。

### 交付边界与待验收
P0–P6功能集成和上述自动回归已交付；完整任务视觉/实机验收仍有缺口，不能标为无条件全通过：P3巨爪实体、吐出动作与双斩辨识不足；P4源残影/火球烟雾/镜头表现不完整；P5水流仍简化、部分HUD拥挤。实体Android/iOS、多触点/pinch、其它宽高比、最终候选全物理键盘链路、长时间随机战斗、全部装备等级组合和人工听感未验证。
正式Boss剧情入口、奖励与存档扩展仍留给程序同学，现有开发入口F9、-tapboss、BossTest保留。P0实测尺度与设计近似值差异已在P0说明，留验收方确认，不隐瞒换算口径。

### 提交卫生与复现
总负责确认ProjectVersion.txt/packages-lock.json已还原，无.unity场景差异；仅清理本次自产pycache，保留原.vsconfig未跟踪。实际构建使用57f1，不宣称还原后的62f3c1全新环境构建通过。最终进程查询exit0、MATCH_COUNT=0，见`process-exit-audit.txt`；未结束其它工程进程。不push。
复现：先按P5命令串行Build，再运行`python Tools/run_tap_selftest.py --boss --timeout 300 --output <新证据目录>`；baseline追加`--baseline`，720p追加`--width 1280 --height 720`。不要并行启动共享PlayerPrefs的游戏实例。
