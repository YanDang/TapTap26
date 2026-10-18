# P2 最终收尾验证

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
