# P3 最终交接


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
