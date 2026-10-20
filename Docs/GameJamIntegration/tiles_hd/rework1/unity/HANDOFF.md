# 返工1 Unity 执行交接（最终完成；未提交）

## 最终结果
- 工程 `E:/gamejam_wt/tiles_hd`，分支 `feature/hd-ground-tiles`，基线 `1fed22feae2faec7e465c5cacf35b5d7b468e2b0`。
- **技术门槛通过，美术未验收通过。** READY实物PNG哈希确认 `34b525b1926db531944c34d66421d1696e28ef800a347c21f66b91d476f46214`；执行代理没有修改美术PNG。
- repeat真实结果exit=0、changed=0、698格；`repaint-first.unity.txt`与最终场景字节相等。场景SHA256 `eb86c11ad1dfe482b9e22ac78fa2cce072692bff40d852373f30f47b2ba2a2f1`。
- 最终after录制exit=0，`GPU capture completed; editor raw preferences restored=True`。1920×1080全景/近景，真实PlayMode相机RenderTexture，不等于完整HUD framebuffer。
- 最终Windows build exit=0，`[TapWindowsBuild] Succeeded errors=0 warnings=0 bytes=410708450`，新产物 `Builds/Rework1/Windows/TapTap26.exe`。
- 最终隔离Player selftest exit=0，**RESULT passed=534 failed=0**。Player退出前自恢复的独立比对true，退出后raw恢复true；原匹配前缀0项，不冒称非空真实玩家存档验证。Editor原匹配前缀2项且自恢复/supervisor恢复均true。
- `verify_scene.py` exit=0；`closeout_rework1.py` exit=0，严格收口结果见 `closeout-result.json`。

## 实际命令
在工程根目录执行，after/build/player顺序串行，未与其他Unity并行：
```bash
python Docs/GameJamIntegration/tiles_hd/rework1/unity/run_editor.py TilesHD.RecordMap.After after
python Docs/GameJamIntegration/tiles_hd/rework1/unity/run_editor.py TapWindowsBuild.Build build -tapbuildpath Builds/Rework1/Windows/TapTap26.exe
python Tools/run_tap_selftest.py --exe Builds/Rework1/Windows/TapTap26.exe --output Docs/GameJamIntegration/tiles_hd/rework1/unity/selftest --timeout 300
python Docs/GameJamIntegration/tiles_hd/rework1/unity/verify_scene.py
python Docs/GameJamIntegration/tiles_hd/rework1/unity/closeout_rework1.py
```
真实完成session：after `proc_123d6969d0d5`、build `proc_0421691697a3`、player `proc_42fa7b83d31e`，均已收取exit=0。没有活动执行任务。

## 历史和保护边界
- baseline/由git 1fed22f导出：场景、scene.meta、atlas/meta、original mask、ProjectVersion/packages-lock/Mix；manifest保存哈希。基线场景 `6e20265240179ca59a0c3a27a8cab88842cf6a8d604ae6b1327aa9362d3e784a`，基线atlas `cb7f29e4fdf6ca93a8fe024a2af867c510a8e2821cf67f0f0f11b51276e21ed9`。
- before本次真实GPU录制exit=0；1fed22f场景及独立内存基线Texture/Tile/Sprite克隆（含旧装饰），从未临时覆盖共享PNG。
- B1基线真缺格为(-3,-12)、(16,4)、(16,6)，三格对应报告两处区域；695→698仅补这三格，无删除，origin/size/旧格color/matrix/flags保持。Tilemap无TilemapCollider2D，无改碰撞。
- cell轴L形路(0,0)→(9,0)→(9,9)，3格主宽与mask过渡；4石残块/5草岛连续生成场，中央安静区约42%格中心（主路例外）；7格间距3对灯+门2灯，3组×3管道；背景仅改#07121B。
- 首轮repaint exit=1（新SpriteRenderer使用??触发Unity fake-null）；日志`repaint.log`保留。显式Unity null修复后`repaint-v2` exit=0、changed=248；repeat changed=0。
- 197既有非Tilemap/非专属装饰组件在应用背景色例外前完全相同。独立YAML结果152→173记录，120条受保护基线记录只允许Tilemap和Camera背景变更；Camera其余字段原样。前后组件JSONL、首轮副本、备份均保留。
- RestoreOriginal提供明确的1fed22f整场景回滚，未调用。最终收尾没有再修改场景/C#/玩法。

## 图像复核与不足
实际打开before/after全景、before/after近景、art代表图128/256及selftest/target-after.png：网格轴折路/深海背景成立，内部原黑洞不再明显；地面与符文重复、道路硬边/端部、草像藻毯或矿物、暗侧面、小贝壳与道具细节弱仍存在。角色叠加与头顶条拥挤在before已存在，不作为本次新增缺陷，也没有擅改角色。
**草中频变化13.49%低于A1要求15%，不宣称美术通过。** A/B逐条落实及不足、40格完整指标已追加主报告，不能用534 PASS替代美术签收。

## 环境恢复及进程释放
- 实测Unity `2022.3.57f1 (9d8bd91adc0a)`，GPU RTX5080 Laptop；不是声明的2022.3.62f3c1。
- 基线manifest与git show双重核对后，ProjectVersion/packages-lock恢复基线；保存`tested-ProjectVersion.txt`、`tested-packages-lock.json`。Mix已与基线字节相同，未无故重写。证据`automatic-files-restored.json`。未在恢复后的声明环境重新构建。
- 最终`process-audit.json`仅既有mcp-for-unity服务，无Unity/Player/CrashHandler残留；未结束他人进程。Unity执行权已释放。
- 原有`.vsconfig`、`Tools/__pycache__`未追踪项保留，不提交。私人raw prefs journals和构建目录仅本地保留。

## 主报告与哈希
- 已追加 `E:/gamejam_wt/tiles_hd/Docs/GameJamIntegration/REPORT_TILES_HD.md` 的“返工 1”完整章节，保留初版历史。
- EXE SHA256 `2c392ecb0dac1e2e5211b488220e26b6c30dfde366d10ccfeb938cf572b856dc`。
- selftest.txt SHA256 `c03e59bf4d510c19d12caf022aaae0b9d90b319ced50550a2f90ee599264ba95`。
- 完整构建/脚本/图像/日志SHA清单 `tested-artifact-hashes.json`；日志 `after.log`、`build.log`、`selftest/player.log`；外部监管 `after-supervisor-result.json`、`build-supervisor-result.json`、`selftest/supervisor-result.json`。
- **未git add/commit/push。** 主负责人尚须复核提交文件清单、忽略规则、敏感journal排除与所有资源/meta配对，再新提交并push feature分支；不要改写1fed22f、不碰main。
