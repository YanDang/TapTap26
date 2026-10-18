# P2 收尾交接

状态：最终收尾构建成功（build-closeout.log，0 errors / 2 warnings，Editor exit0）；同一二进制 normal 97/0、baseline 93/0，两个监督器均exit0且恢复前后TAP原始状态一致。完整结果/视觉限制/复现见 `P2/validation.md`，两份总报告已同步。F9实际键消息入口已验证：普通启动→菜单进入无Boss战场→background F9 PostMessage→真实Boss/血条/红预警出现；不是人工物理键盘测试。F9普通游玩探针退出后由监督器恢复原TAP数据（before=false、after=true），不冒称自行恢复。

## 本轮修复
BossQualityGates 不再无条件解引用可选 TapCombatScale。baseline 模式在生成 Boss 前独立读取主角 SpriteRenderer 世界 bounds 高度，保留 2.5 倍及 .001 原断言，不跳过、不放宽失败门禁。

## 证据位置
`E:/gamejam_wt/integ_b_evidence/P2/`：旧 final 97/0 和 baseline-final 65/1 空引用保留；本轮 build-closeout.log、closeout-normal/、closeout-baseline/。证据大文件不提交。

## P3 事件契约
- 唯一 HP/生死所有者是 EnemyController；BossBrain 独占战斗时钟，经 ExternalAI Tick 推进。BossAnimator 已禁用自动 Update，由 Brain 驱动。P3 只做表现，不再次扣血/推进时钟。
- BossBrain.MoveStarted(string move)：普通招起手；阈值起手也发出。ThresholdStarted(int index)：0=70%大招，1=50%变身，2=30%双斩，发生在 MoveStarted 之前。
- BossBrain.Impact(string move,int index)：战斗命中结算后通知，普通连段 index 从0开始；大招使用 Ult_Cleave 与 ultimateCut。P3 命中特效使用此事件，不同时监听 Animator.OnImpact 重复生成。阈值子阶段不保证逐个发 MoveStarted。
- Animator.OnPlayMove/OnImpact/OnMoveEnd 为动作表现层事件，非伤害授权；OnFootfall(int side) 可用于脚步表现。中断不发旧动作自然结束，不得以 OnMoveEnd 判战斗胜负。
- BossEncounterController.Defeated(EnemyController) 来自真实 Died，每场一次；CancelEncounter/Destroy 不判胜，不给奖励、不写存档。
- P3 订阅必须在 disable/destroy 解绑，处理取消/死亡清理，自行隔离回调异常；不得在回调重入改战斗状态。表现须遵守 CombatWorldPaused，不抢相机/TimeManager 所有权。

## 未验证项
人工物理键盘、移动端手势、长时间随机实战/全家具组合、全部事件重入交错、故障注入恢复、P3电影化表现未验收。F9实战镜头有头顶出框/与顶栏重叠；定机位全身截图不能代表实战取景通过。肢体侧向重叠/披风遮腿、接地感弱及主角状态条压帽子保留为后续表现待办。
