# P6 历史检查点与最终交接
基线 b37ff2b，初始仅未跟踪 .vsconfig。唯一Unity执行者为本P6子代理；不提交、不push。
已读取生产 GameplaySceneTransition、PlayerSessionData、CollectSceneController、GatherDoorAuraBehavior 与原P0-P5测试。
关键契约：Boss期间生产门 EnterGatheringScene 明确拒绝进入Collect；不能移除此规则来伪造Boss温室往返通过。计划测试Boss真实Mix往返保持、Boss拒绝门、取消Boss后两个真实门进入Collect/采集/返回。
初查没有本工程Unity/Player；另工程 integ_ours CrashHandler PID33080 不触碰。
尚未构建/运行；P6全部门禁待完成。旧P5的268/264不是P6成绩。

## P6 进行中检查点（未验收）
- run-1：281 passed / 17 failed，真实Mix/Boss暂停及两扇Collect门往返/采集/每日独立资源通过；首次UI探针在ReturnRoutine结束同帧点击HUD，raycast none；夹具无条件切换导致picker意外开启，污染后续17项。原失败证据保留。存档player恢复前和supervisor恢复后均一致。
- 新候选修正夹具等待真实HUD更新和条件清理，不降低断言；增加普通家具、真实UI raycast+pointer callback；抓钩按钮上移避开格挡，水柱alpha及回响截图等待标签。build-2 / run-2 已完成：normal 307/0、baseline 303/0，均exit0且恢复前/后原始存档一致。已审核run-2水柱：地砖和角色可透见；抓钩不再压格挡/斩。restored-hud存在测试家具耐久飘字遮挡，最后候选等待飘字消退再截图，并补生产存储重新加载验证。最终 build-final / final-normal / final-baseline / final-720p 已完成：308/0、304/0、308/0；超时注入exit124，player恢复前不一致、supervisor原始恢复后完全一致；原始键0，Tools/test_gear_prefs.py隔离类型夹具1 test OK。GPU发现装备Canvas仍压工坊，已做最小隐藏修复并加断言。**这些final-*均是上一候选；当前closeout最终矩阵进行中（proc_cac87c81abca），不以旧通过冒充新源码验收。**
- Boss期间门生产入口明确禁止Collect，保留规则；Boss真实Mix往返与Boss门拒绝分别验证，Collect往返在Boss结束后。

## 最终总负责接管结果
proc_cac87c81abca已退出3：原因是最后超时注入child=124、恢复前false；监督器恢复后true。前三轮正常完成：closeout-normal 312/0、baseline 308/0、720p 312/0，均exit0。build-closeout.log为Succeeded errors=0 warnings=2。
总负责随后独立复跑parent-verification 312/0 exit0，存档恢复前后均true。复核该轮Mix/Collect、水柱、回响8/8真实截图。新增装备Canvas隐藏修复在这轮中已验证。
证据根E:/gamejam_wt/integ_b_evidence/P6/；final-source-binary-sha256.json绑定源码/二进制。最终进程MATCH_COUNT=0，版本/锁文件已还原，无场景diff；.vsconfig保留，生成pycache已清理。
两份报告最终章节已更新。P3双爪/吐出、P4部分源表现、P5水流等视觉保留；实体触屏/其它比例/全键盘/长时混合实战未验收。参见仓内INTEGRATION_FROM_GAMEJAM.md的最终P6章节，不再使用前述进行中状态。
