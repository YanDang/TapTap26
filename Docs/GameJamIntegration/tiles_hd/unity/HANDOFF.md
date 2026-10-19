# Unity 最终验证交接（已完成执行，技术通过／美术未验收）

## 范围与结论
- 唯一工程：`E:/gamejam_wt/tiles_hd`。初始 HEAD `8cb2e0a5e81a0b1b10f6275cb15e708ba1795630`，分支 `feature/hd-ground-tiles`；本代理未提交、未 push。
- 本轮完整读取了当前委派任务、Unity执行技能、原本文件、`RepaintMap.cs`、`RecordMap.cs`、`run_editor.py`、`TapWindowsBuild.cs`、`Tools/run_tap_selftest.py` 和 art/HANDOFF.md。原始独立任务/美术 GUIDE 未提供路径，本轮搜索未定位；不冒称重新逐条审阅原始美术规范，沿用美术交接披露。
- 没有再次修改贴图、meta、玩法或重铺逻辑。固定PNG SHA256实测仍为 `3d06efd4f71021065fc4d9f05d22f9b528a1bdafd3e895c5000ca62e70538a67`。
- **幂等、197组件保护、改后真实GPU截图、Windows构建、实际Player自测均通过。** 自测最终 `passed=534 failed=0`。这不代表无缝美术标准通过。
- **Unity执行权已释放**：最后只读进程审计中 Unity.exe / TapTap26.exe / UnityCrashHandler64.exe 均为空。未终止别人工程；mcp-for-unity服务保留。

## 已执行命令与进程
全部在工程目录执行，Editor/Player串行：
```bash
# 接手时先poll既有进程，未并发启动Unity
python Docs/GameJamIntegration/tiles_hd/unity/run_editor.py TilesHD.RepaintMap.Repaint repaint-idempotence
python Docs/GameJamIntegration/tiles_hd/unity/run_editor.py TilesHD.RecordMap.After after
# 本轮实际新构建
python Docs/GameJamIntegration/tiles_hd/unity/run_editor.py TapWindowsBuild.Build build -tapbuildpath Builds/Windows/TapTap26.exe
# 本轮实际运行构建产物，不是仅启动
python Tools/run_tap_selftest.py --exe Builds/Windows/TapTap26.exe --output Docs/GameJamIntegration/tiles_hd/unity/selftest --timeout 300
# 独立只读落盘/哈希/进程核验
python Docs/GameJamIntegration/tiles_hd/unity/verify_closeout.py
```
- 接手作业 `proc_3eef642a5b46` / PID12872：poll确认已退出，两个Editor命令均exit=0。
- 构建 `proc_256ccd542563` / PID26936：已退出，exit=0。
- 自测 `proc_002cf17cc0e7` / PID4568：已退出，exit=0。
- `verify_closeout.py` 实际运行exit=0，写出 `final-verification.json`；包含场景/贴图/EXE哈希、图片尺寸与哈希、原始记录差异及最终进程列表。

## 地图审计、重铺与幂等
- 入口：StartMenuController → BiomechanicalCombatScene。原Build Settings启用 StartMenu/BiomechanicalCombatScene/Mix/Collect；生产构建器还会在文件存在时加入BossTest。TileTest/CraftingTest非正常入口，本任务不改。
- `audit.log` / `audit.json` / `tilemap-before.json`：审计作业 `proc_22d008bfaebe` exit=0。Grid/Tilemap原695格，origin(-17,-16,0)，size(38,39,1)，无TilemapCollider2D。玩家场景位置(0,.75,0)、scale(.5,.5,.5)。门先放(0,5,0)，原生产Snap后(0,5.25,0)，未改行为。
- 独立原图/meta在 `unity/baseline_source`；改前现图SHA256 `4190ccdda28f698d0d86457428b9890441c12a5c44edd378d6900fa0a7807640`。完整场景备份 `BiomechanicalCombatScene.before.unity.txt`。
- RepaintMap具有事前JSON备份、连续材质场、原地Tilemap覆盖、无碰撞纯装饰、既有组件序列化不变断言及RestoreOriginal菜单。保留每次 `tilemap-backup-*.json`。
- `repaint-v2.log`：`REPAINT PASS changed=122 invariants=197`。
- `repaint-idempotence.log` / `repaint-result.txt`：`REPAINT PASS changed=0 invariants=197`，footprint=695。
- 二次执行前 `idempotence-before.sha256` 与执行后/构建后场景SHA256一致：`6e20265240179ca59a0c3a27a8cab88842cf6a8d604ae6b1327aa9362d3e784a`。不仅格数一致，场景文件逐字节幂等。
- 独立JSON比较：全部695个坐标集合、origin、size均一致，没有新增或移除占用格。
- 独立场景文本记录比较：原118条序列化记录、现150条，无原记录删除；既有记录仅Tilemap(classID1839735485)和SceneRoots(classID1660057539)改变。其余原场景序列化记录逐字相同。此与Editor中197个展开组件保护是两种不同证据，不能将118误写为组件数。

## 改前／改后截图及视觉限制
- 改前作业 `proc_795e81ae2c50` exit=0；`before-supervisor-result.json` 的editor自恢复、raw恢复均true。
- 改前纹理来自独立baseline_source，LoadImage + 内存Sprite/Tile克隆，不回写资源；退出PlayMode后恢复。
- `BiomechanicalCombatScene-before-panorama.png`、`...-before-closeup.png` 和 `...-after-panorama.png`、`...-after-closeup.png` 均为真实PlayMode、相同固定构图、1920×1080游戏相机RenderTexture GPU渲染；**不是完整屏幕UI framebuffer**。
- 改后 `after.log` 有 `RECORD after PASS`；`runtime-after.txt`：RTX5080 Laptop GPU，Player=(0,.75,0)，Door=(0,5.25,0)，Tilemap=Grid/Tilemap。`record-after-result.txt` 及外部supervisor确认恢复。
- 已用图像工具审查改后全景、近景和改前近景：地图完整入镜、道路/门/角色/装饰可见，无黑屏或典型洋红缺材质。改后地面点纹及菱形拼块重复明显，道路边缘持续锯齿、末端收口生硬，不能称为无明显接缝。
- 全景右上及下缘偏右存在深色凹口视觉；坐标集合独立比较证明未漏删原格，不能仅凭截图将其判成新漏铺，但视觉边缘仍需美术验收。
- 角色头顶横条及拥挤的部件轮廓在改前近景已存在；本次不修改。自测同时验证保留的是体力条、没有玩家头顶HP背景/填充。
- art/HANDOFF.md已明确：草面绿毯感、石板规整、沉积土底板感、道具透视/材质厚度不足、部分GUIDE视觉条款未完成。**这些未通过点仍有效，技术PASS不能覆盖美术FAIL。**

## Windows构建与真实Player测试
- 产物：`Builds/Windows/TapTap26.exe`（Development Windows64）。EXE SHA256 `2c392ecb0dac1e2e5211b488220e26b6c30dfde366d10ccfeb938cf572b856dc`；EXE哈希不是完整数据包哈希。
- `build.log`：`[TapWindowsBuild] Succeeded errors=0 warnings=1 bytes=410685947`。
- 唯一编译警告：`Assets/Scripts/Boss/Anim/BossAnimator.cs(184,47) CS0414`，runtimeTick赋值未使用。未改玩法以消警告。
- `selftest/selftest.txt` 已完整读取，`RESULT passed=534 failed=0`；`player.log` 未检出Exception、error CS或TapSelfTest FAIL。
- 覆盖实际生产场景、玩家/相机/门、移动与闪避回收、Mix与Collect真实场景往返、家具、装备、Boss、演出、大招、终局、生产存档重载等既有534断言。自动化合成pointer事件不等于人类物理键鼠/手机触摸验收；本轮未另外跑 `--baseline` / `--boss` 启动矩阵或长时间人工游玩。
- 自测测量 `screen=1920x1080`，GPU同上，actualDistance=0.4191496、expected=0.419149548；原无障碍移动断言PASS。
- `selftest/target-after.png` 是真实Player framebuffer（含完整HUD），已用图像工具检查：场景、角色、地面、UI正常显示，无黑屏/典型紫材质；仍能看到地面重复、道路锯齿、角色体力条贴近头部及既有UI风格混合。右下Development Build水印属预期。
- 自测另产出演出/装备/场景往返等截图，全部路径/尺寸/哈希写入final-verification.json；本次未逐张作美术签收，不能宣称全部截图已视觉通过。

## 存档隔离与恢复
- 沿用现有raw-registry supervisor，运行前完整备份匹配 `TAP_` / `GJ_UltChoice` / `GJ_GEAR_` 前缀值的存在性、原生类型与raw bytes；先等Player退出，再比较Player自恢复，最后执行外部恢复并复核。
- Player本机初始匹配值数量实测 **0**（`supervisor-prefs-before.json`为{}），不能宣称本轮验证了用户非空真实Player存档。测试内部另有unknown key/native类型/raw bytes/新增键移除断言，均PASS。
- `selftest/supervisor-result.json`：exit_code=0，player_restored_before_supervisor=true，post_exit_raw_restore=true，original_TAP_count=0。
- Editor初始匹配值2项；前/后截图和重铺/build的supervisor结果均editor_restored_before_supervisor=true、raw_restored_after_exit=true。Editor非空已有值确实恢复。
- 本轮未故意制造timeout/crash，不能声称测试了强杀恢复路径。恢复journal保存在证据目录，未泄露值到报告。

## 环境差异、工作树与父代理后续
- 指定exe `E:/unity/Downloads/Editor/Unity.exe` 实际自报 **2022.3.57f1**，不是原描述2022.3.62f3c1。验证只对该实际环境成立。
- 构建日志还有LicensingClient signature validation提示，随后成功授权/构建；不是编译失败。退出allocator统计中的Failed Allocations不是自测失败，不混淆。
- 本轮新增：build日志与supervisor journal、selftest全部日志/结果/截图、`verify_closeout.py`、`final-verification.json`，更新本HANDOFF。没有修复/改写生产代码。
- Git尚有美术PNG/meta、主场景、TilesHD编辑器脚本/备份/Docs等待父代理审核；自动变化 `Assets/Scenes/Mix.unity`、`Packages/packages-lock.json`、`ProjectSettings/ProjectVersion.txt` 按委派留给父代理最终恢复。另有`.vsconfig`、`Tools/__pycache__/`不应误纳入交付。
- 不替父代理提交或push，不擅自删除共享工作树内容。恢复版本/lock后应说明本构建是在实际57f1环境完成，不能冒称恢复配置后的全新构建通过。
- 最终进程核验见 `final-verification.json`：`unity_player_crashhandler_processes=[]`。全部三个进程会话均已退出，**可以交还Unity独占执行权**。

## 总负责最终复核补记
父代理最终修正后PNG为 cb7f29e4fdf6ca93a8fe024a2af867c510a8e2821cf67f0f0f11b51276e21ed9；已重新After截图、Build与Player自测534/0，exit=0，verify_closeout通过。最终图白点减少；旧白点批评属前一稿，重复/锯齿限制仍在。版本/lock/Mix已恢复；以REPORT_TILES_HD.md及最终JSON为准。
