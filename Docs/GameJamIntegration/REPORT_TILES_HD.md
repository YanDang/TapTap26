# 高清地砖与地图重铺交付报告

## 结论与交付范围
技术验证通过，已交付可运行的高清重绘及地图重铺候选版本；**不是全部美术条款验收通过**。仍有规则重复、道路锯齿与收口、道具简化等问题，详见末节。不得以534项测试通过代替美术签收。

总负责Hermes；子代理均实际使用gpt-6-astra。总负责人完整阅读 `E:/gamejam/Docs/tasks/TASK_TILES_HD_HERMES.md` 与 `E:/gamejam/Docs/tasks/GUIDE_TILES_HD.md`。仅在 `E:/gamejam_wt/tiles_hd` 的 `feature/hd-ground-tiles` 工作，起点8cb2e0a5e81a0b1b10f6275cb15e708ba1795630。没有修改main或其他worktree，不合并main、不force push。

## 制作方法与资产保护
- 全程确定性程序化遮罩驱动合成，未使用AI生图、不冒充手绘。Python/Pillow/numpy/scipy；每种材质3张1024平方周期纹理，等距投影并分面明暗。16格道具按v1范围重做分层几何/曲面明暗，不是简单放大；仍比参考简化。
- 从original_backup提取草/石/土及小贝壳遮罩。最终2048×1792 RGBA、8×7、256px格，顺序不变。原始备份未改，改前现图另存v1，v1不参与Tile引用。
- 最后一次迭代修复透明区误触发材质AO导致的大格暗框；降低土面贝壳对比，石板半周期跳变改全周期，草改弯曲渐尖叶束。旧/新迭代对照见art/revision_before_after.png。
- png.meta的56个rect精确翻倍；guid、name、spriteID、internalID及每sprite pivot(0.5,0.25)保留。PPU256、Bilinear、无压缩、maxTextureSize2048。Tile资产与Palette未改。GUIDE顶边高光/跨边叶片/有机遮罩扰动并未逐条完整实现，以保轮廓、减弱网格为优先。
- 最终PNG SHA256：`cb7f29e4fdf6ca93a8fe024a2af867c510a8e2821cf67f0f0f11b51276e21ed9`。

## 实际地图与回滚
实际正常入口为StartMenu → BiomechanicalCombatScene；本分支BuildSettings并非任务书概述的旧列表，现启用StartMenu/BiomechanicalCombatScene/Mix/Collect。TileTest/CraftingTest非正常入口，未改。

只重铺 `Assets/Scenes/BiomechanicalCombatScene.unity` 的 `Grid/Tilemap`：695个原占用格全部保留，石板主路连接出生点与裂隙门方向，中间沉积土开阔战斗区，外围海草及过渡，路边4盏灯、边缘6段管道。纯装饰无碰撞/脚本/交互；原有玩法对象、组件、位置不改。原图格型限制下只有草的两个纯色格交替，未做到每种材质地图上2–3变体不相邻；灯间距使用场景尺度而非严格每6–8格。道路末端和边缘管道排列仍需美术调整。

`Assets/Editor/TilesHD/RepaintMap.cs`提供Repaint、Audit、RestoreOriginal菜单，重复执行changed=0且场景SHA相同。每次先导出tilemap-backup-*.json，首次tilemap-before.json不覆盖。完整原场景备份位于unity/BiomechanicalCombatScene.before.unity.txt。恢复菜单回填原格数据并移除专属装饰；不回退PNG。

Editor检查197个既有组件序列化不变；独立比对原118条场景序列化记录，未删除任何原记录，仅Tilemap与SceneRoots变化。695格坐标/范围完全不变。全景凹口来自原有轮廓，未新漏删格。

## 真实验证
最终贴图修改后再次运行（非沿用旧534/0）：
```
python Docs/GameJamIntegration/tiles_hd/unity/run_editor.py TilesHD.RecordMap.After after
python Docs/GameJamIntegration/tiles_hd/unity/run_editor.py TapWindowsBuild.Build build -tapbuildpath Builds/Windows/TapTap26.exe
python Tools/run_tap_selftest.py --exe Builds/Windows/TapTap26.exe --output Docs/GameJamIntegration/tiles_hd/unity/selftest --timeout 300
python Docs/GameJamIntegration/tiles_hd/unity/verify_closeout.py
```
全部exit=0；最终自测 `RESULT passed=534 failed=0`，Player日志无Exception/error CS/自测FAIL。最终读回JSON确认贴图哈希、组件保护和无残留Unity/Player/CrashHandler。构建器产物本地在Builds/Windows，不提交二进制。

实际指定路径Unity.exe自报 **2022.3.57f1**，不是任务书描述的2022.3.62f3c1。最终提交前已恢复ProjectVersion.txt、packages-lock.json和Mix.unity自动重序列化。测试针对实际57f1运行环境，不宣称恢复声明版本之后另一次全新构建通过。首轮有既有BossAnimator未用字段警告；完整构建日志保留。

Editor原有2项存档与Player前缀存档均由raw supervisor隔离恢复；Player原匹配值为0，未冒称非空玩家存档验证。未另跑baseline/boss矩阵或强杀恢复。私有注册表原始备份只留本地，不提交。

## 逐格自检（最终PNG重新读取）
Alpha对原图最近邻×2，材质预测从最终RGB独立阈值反分类；不直接以生成标签充当预测，也没有豁免贝壳。阈值IoU≥0.98、材质≥0.95。顶面另按原几何检查。荧光阈值代理面积约0.24%，非物理发光测量。

|格号|Alpha IoU|全格材质正确率|顶面正确率|
|---:|---:|---:|---:|
|0|1.000000|1.000000|1.000000|
|1|1.000000|1.000000|1.000000|
|2|1.000000|1.000000|1.000000|
|3|1.000000|1.000000|1.000000|
|4|1.000000|1.000000|1.000000|
|5|1.000000|1.000000|1.000000|
|6|1.000000|1.000000|1.000000|
|7|1.000000|1.000000|1.000000|
|8|1.000000|1.000000|1.000000|
|9|1.000000|1.000000|1.000000|
|10|1.000000|1.000000|1.000000|
|11|1.000000|1.000000|1.000000|
|12|1.000000|1.000000|1.000000|
|13|1.000000|1.000000|1.000000|
|14|1.000000|1.000000|1.000000|
|15|1.000000|1.000000|1.000000|
|16|1.000000|1.000000|1.000000|
|17|1.000000|1.000000|1.000000|
|18|1.000000|1.000000|1.000000|
|19|1.000000|1.000000|1.000000|
|20|1.000000|1.000000|1.000000|
|21|1.000000|1.000000|1.000000|
|22|1.000000|1.000000|1.000000|
|23|1.000000|1.000000|1.000000|
|24|1.000000|1.000000|1.000000|
|25|1.000000|1.000000|1.000000|
|26|1.000000|1.000000|1.000000|
|27|1.000000|1.000000|1.000000|
|28|1.000000|1.000000|1.000000|
|29|1.000000|1.000000|1.000000|
|30|1.000000|1.000000|1.000000|
|31|1.000000|1.000000|1.000000|
|32|1.000000|1.000000|1.000000|
|33|1.000000|1.000000|1.000000|
|34|1.000000|1.000000|1.000000|
|35|1.000000|1.000000|1.000000|
|36|1.000000|1.000000|1.000000|
|37|1.000000|1.000000|1.000000|
|38|1.000000|1.000000|1.000000|
|39|1.000000|1.000000|1.000000|

## 截图与证据清单
以下相对 `Docs/GameJamIntegration/tiles_hd/`：
- art/comparison_128px.png：v1/新图同显示尺寸总览。
- art/props_before_after_256px.png：16格道具前后对照。
- art/seam_3x3_cell_0.png、cell_2.png、cell_3.png：草/石/土3×3。
- art/transition_5x5.png：草→石→土组合。
- art/revision_before_after.png：噪声与大格框修正对照。
- unity/BiomechanicalCombatScene-before-panorama.png、before-closeup.png：改前1920×1080全景/近景。
- unity/BiomechanicalCombatScene-after-panorama.png、after-closeup.png：最终贴图重新导入后1920×1080全景/近景。
- 上述4张是实际PlayMode游戏相机GPU RenderTexture，不是完整HUD framebuffer；改前用独立原纹理及内存Tile克隆，无回写贴图竞态。
- unity/selftest/target-after.png：最终真实Windows Player完整HUD framebuffer。
- art/validation.json、per_cell_validation.csv、supplemental_validation.json、final_revision_validation.json：图片数值。
- unity/final-verification.json、repaint-result.txt、after.log、build.log、selftest/selftest.txt、selftest/supervisor-result.json、selftest/player.log：运行证据。
- 自测额外截图（数量及哈希以manifest为准）是既有测试附产物，大部分留本地未提交；不要将所有文件视为已逐张美术签收。最终关键游戏图、对照图已复看。

## 已知不足／未验收项
1. 最终游戏近景白点已受控、角色清晰，道路/土/草分层成立；同材质仍有云纹、草束规律重复，不能宣称完全无重复无缝。石板纹理左右接缝误差实质降低，但这不等于所有组合视觉通过。
2. 道路轮廓锯齿周期、末端碎石收口生硬；边缘6个管段排列偏机械。地图构图可读，但尚未达到全部指导的自然高级美术标准。
3. 沉积土偏蓝灰石面质感，海草侧面分段；道具虽真正重绘，金属厚度、横筒透视和与v1造型细节的贴合仍不足。
4. 不修改玩法以改善镜头或角色头顶条，原有体力条与HUD混合风格不属于本任务。
5. 结论是已实现、已真实技术验证、待美术签收的分支候选稿，不是全部验收绿灯。请Claude逐图复核后决定是否合入；本任务绝不合main。
