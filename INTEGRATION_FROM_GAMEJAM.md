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
