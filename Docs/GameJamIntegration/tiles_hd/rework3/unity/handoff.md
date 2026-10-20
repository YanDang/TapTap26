# 返工3 Unity交接：已完成执行与验证收口（禁止提交）

## 定位
- 工程：`E:/gamejam_wt/tiles_hd`
- 基线：`e07520f16254b12ac7d4625c36209fc334e08650`；分支：`feature/hd-ground-tiles`。
- 本目录：`E:/gamejam_wt/tiles_hd/Docs/GameJamIntegration/tiles_hd/rework3/unity/`（下述相对路径均基于此）。
- 仅本阶段交接，不改总报告、不commit、不stage、不push。历史rework2及既有未跟踪文件均保留。

## 已完成
1. 先poll继承的 `proc_48d46f21bb1a`（PID43480），确认v3校正生成→repaint→after全部exit=0，没有并发Unity。沿用v3线性空间增益，不重画17张已READY源PNG，不增加玩法。
2. 近景、全景及v2/v3同坐标并排裁切均经图像工具实际复核。`diagnostic-v2-v3-crop.png` 显示v3整块菱形亮暗明显降低，土面先呈连续蓝灰底，再呈细格缝；按允许原细格缝的目标基本达标。全景无明显新增漏块/黑洞/断路，装饰完整。**仍有细格缝、轻微纹理重复及少量偏硬路端，不得改写为完全无网格。** 详见 `visual-review.json`。
3. 新鲜repeat：`repeat.log`、`repeat-validation.json`，`changed=0`，场景字节完全一致；SHA256 `541eb0f0d250a05b33acc1e9d528a854a5ce38eb29eb854a5a0058f59582407e`。
4. `TilesHD.Rework3Map.Readback` 独立进程exit=0；`fresh-readback.json` 与保存后快照完全一致。`verify.py` 两次真实PASS（环境恢复前后各一次）。
5. 698占用、origin、size不变；原场景167条序列化记录ID不增不减，唯一允许差异为Tilemap局部路缘与TilemapRenderer材质引用；执行时保护228个展开组件（不是167条YAML记录）。2307受保护文件哈希通过。相对基线27格变动全部在局部路缘，4条纵边任意连续3格都有16–27过渡Tile；原CSV与每边坐标/索引/窗口覆盖在 `road-edge.csv`、`integration-protection.json`。不是全地图或路面重铺。
6. 全新（启动前不存在）`E:/gamejam_wt/tiles_hd/Builds/Rework3/TapTap26.exe` 构建成功，`build.log` 报 Succeeded errors=0 warnings=0 bytes=725650906。命令使用既有 `TapWindowsBuild.Build -tapbuildpath Builds/Rework3/TapTap26.exe`。
7. 既有 `Tools/run_tap_selftest.py` 对该新exe运行正常模式（无baseline参数），`selftest/selftest.txt` 实测 **534 PASS / 0 FAIL**，正常exit=0。`selftest/target-after.png` 为真实player framebuffer，已复核可见范围无明显黑块/粉材质/漏显，HUD正常显示。未把固定Camera.Render图冒充全framebuffer。
8. Editor原2个受保护raw prefs、Player原0个，均已独立读取确认原生类型/原始字节及存在性匹配：`final-prefs-audit.json`。各supervisor均在外部恢复前观察到自身恢复true，退出后raw恢复true；没有靠supervisor掩盖失败。真实player namespace为空；534测试含合成raw类型探针，**不是非空真实存档恢复或崩溃恢复的额外证明**。
9. `tested-environment/ProjectVersion.txt`、`tested-environment/packages-lock.json` 保存实际测试环境2022.3.57f1；`tested-source-sha256.json`保存测试源码/素材哈希。仅从开工前且对照e07520f一致的baseline恢复自动ProjectVersion/lock到2022.3.62f3c1；Mix从未变动，无需还原。`environment-restoration.json`明确：**没有在恢复后的编辑器/包环境重新build**。
10. 严格 `closeout.py` 已实际运行通过：`closeout.json`、`artifact-sha256.json`、`log-audit.json`、`closeout-predicate-tests.json`。检查exit必须整数0、prefs必须布尔true、缺字段/错误字段拒绝、fresh几何/PNG READY/源码一致/534断言/GPU完成/实际build内容均校验。哈希涵盖exe、Managed程序集、全部新build内容、截图日志及生产资产，非仅launcher哈希。

## 核心文件/哈希
- 场景 `Assets/Scenes/BiomechanicalCombatScene.unity`：`541eb0f0d250a05b33acc1e9d528a854a5ce38eb29eb854a5a0058f59582407e`。
- 新build `Builds/Rework3/TapTap26_Data/Managed/Assembly-CSharp.dll`：`2dd27e1253fa03717273a9765a2726b7161cb386f43314be3f5cd996cf222c87`。
- 接续保留实现：`Assets/Editor/TilesHD/RepaintMap.cs`、新增 `Rework3Map.cs`及meta；`Assets/Shaders/TilesHDWorldLight.shader`及meta；`Assets/Art/Tiles/Test/TilesHDWorldLight.mat`、`TilesHDSoilLight.png`及各meta。17张PNG由美术代理完成，本接续没有再写源PNG。
- 本接续新增验证脚本：`finish_environment.py`、`closeout.py`；新增独立readback/repeat/构建/player/视觉/偏好/环境/哈希证据。既有prepare.py不可重新跑来覆盖初始基线。

## 进程释放
- `proc_48d46f21bb1a` / PID43480：exit=0。
- `proc_8bbc00b450d5` / PID29756（repeat+readback+verify）：exit=0。
- `proc_51e7cab708ba` / PID28916（build+player）：exit=0。
- `process-release.json` 是退出后真实进程清单：本工程Unity/player/崩溃处理等执行子进程不在清单中，仅共享 `mcp-for-unity.exe` 服务仍在，未擅自杀服务。Unity执行权已释放，可由主代理继续操作。

## 限制与剩余
- 明确委派的技术验证已完成，无运行中作业；主代理可引用上述证据做总报告。本代理不做总报告、不提交。
- 文档定位更正：真实文件为 `E:/gamejam/Docs/tasks/REWORK_TILES_HD_3.md`，主负责人已读；本次Before修复代理亦已读取原文。此前“未找到”的结论只反映了错误搜索范围，现撤销。提交仍遵循本次明确禁止提交的委派边界。
- 原始Editor日志有授权缓存/Token服务错误及readback退出时Curl callback aborted（共21条服务诊断，保留在log-audit）；实际编译/着色/运行时异常扫描为空，build和player均成功。不能把原始日志描述为所有类别绝对零错误。
- 分支upstream是 `origin/integrate/claude-boss-ult-gear`，不是同名feature远端；不要裸push。
- 保留 `.vsconfig`、`Tools/__pycache__/`、`Docs/GameJamIntegration/tiles_hd/rework2/unity/selftest/p3-audio-readback.txt`，不删除不暂存。

## 独立审查补修：Before基线入口（本轮完成）
- 唯一生产目录内源码改动：`Assets/Editor/TilesHD/RecordMap.cs`，不改玩法、shader、材质、17张PNG和最终地图内容。原入口跟随Output迁到rework3后仍依赖旧单atlas文件且写着879645d，确属辅助取证回归。
- Before保留原场景字节guard，并额外对照固定Git `e07520f16254b12ac7d4625c36209fc334e08650`；从Git独立读取全部16变体及基础atlas，逐个核验meta后按原rect/pivot/PPU构建内存Sprite/Tile克隆。16变体在内存合成一张8192×7168纹理，保持Chunk批次排序；15装饰与所有Tile引用覆盖不全即失败。无生产PNG覆盖、无Tile资源写回。
- 增加 `-tilesHDRecordOutput` 隔离取证目录。本轮所有新GPU图、日志、原/新源码快照位于 `recorder-fix/`；既有最终截图全部未改，历史失败/诊断证据保留。
- `verify_recorder_fix.py` 实际执行：临时装入已校验的baseline场景 → Before → finally恢复最终scene → After；两进程均exit=0，内部raw prefs恢复true且supervisor恢复前观察true。`recorder-fix/verification.json`：保护资产差异=[]、既有图片差异=[]、scene SHA256仍为 `541eb0f0d250a05b33acc1e9d528a854a5ce38eb29eb854a5a0058f59582407e`。
- 独立验证 `baseline-textures-before.txt` 共17行SHA256全部等于Git blobs；Before tilemap纹理 `e07520f_variants 8192x7168; unique=1`，不是单张基础atlas冒充16变体。
- 额外负向真实执行：在最终scene上调用Before，`recorder-fix/guard.log` 明确拒绝“requires exact e07520f baseline scene”，exit=1，prefs不变，没有生成误标Before目录。此预期失败证据完整保留，不混入成功日志扫描。
- 已实际图像复核新Before全景与新After近景：可见布局、道路、角色及装饰完整，未见明显粉材质/黑洞/漏显；不扩大为全视角或完全无细网格承诺。
- 原 `tested-source-sha256.json` **未改写**（SHA256 `572860230a3c19c2740317dc355c3302f0208c90b356c97f4817bc6141d73774`）。原534 PASS/build证据仍绑定原测试源码；唯一后续差异RecordMap通过新源码快照、SHA256、新鲜Before/After编译/PlayMode/GPU证据单独绑定。Player生产内容未变，原Managed程序集仍 `2dd27e1253fa03717273a9765a2726b7161cb386f43314be3f5cd996cf222c87`；本轮没有伪称新build或重跑534。
- 更新 `closeout.py`、`finish_environment.py`，仅允许这一个经新证据哈希限定的Editor差异；文档路径更正。重跑finish_environment、verify、closeout均PASS；更新后的成功日志服务诊断29条，编译/渲染/运行时错误扫描为空。自动ProjectVersion/lock已恢复；没有在恢复后的版本重新build。
- 本轮 `proc_90ed4a222073` exit=0；后续guard正常预期exit=1。最新 `process-release.json` 无本工程Unity/player活进程；Editor原2项、Player原0项raw prefs仍匹配。未stage、commit或push。

