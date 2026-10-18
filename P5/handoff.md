# P5 最终交付交接

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
