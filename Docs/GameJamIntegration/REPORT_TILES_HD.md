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


## 返工 1（基线 1fed22f；本次独立执行证据）

### 结论与版本边界
**本次最终PNG的Unity技术门槛通过：after录制、Windows构建、隔离Player自测534 PASS / 0 FAIL；不代表美术验收通过。** A1草中频实测13.49%，低于15%下界，重复、过渡硬边与材质辨识不足仍保留。上文初版数据/制作方法属于1fed22f历史，不是本次证据。本节及`tiles_hd/rework1/`才描述返工1。

工作区`E:/gamejam_wt/tiles_hd`，分支`feature/hd-ground-tiles`，基线`1fed22feae2faec7e465c5cacf35b5d7b468e2b0`。总负责人核验后以新提交交付，仅推送feature/hd-ground-tiles，绝不改main；最终commit以交付终端和git记录为准。

### A 地砖：实际实现与不足（逐条）
本次是**真实SDXL AI材质来源 + 程序化绘制混合**，不是初版纯程序化，也不冒称手绘。ComfyUI三次1024×1024生成、28步、固定seed 982073/982074/982075；模型为sd_xl_base_1.0.safetensors。workflow/history/系统信息/原图由美术代理留档。经周期边缘混合、色板重映射及分面投影，再叠叶簇、裂隙、沙纹、光照。3种材质各3个纹理偏移变体不等于地图每种纯材质具备3个独立Tile变体。

|条款|实际实现|结果/限制|
|---|---|---|
|A1 材质中频|3张真实AI材质，9张1024纹理；域扭曲周期场、叶簇、矿物斑驳、流沙纹|草0/石2/土3实测顶面中频相对标准差分别 0.1349/0.1606/0.1579；草未达到严格15%下界，不写全达标。混合格标准差包含材料差异，不能拿来冒充细节幅度。|
|A2 光照|顶面左上到右下10%场，四边2px柔亮边，正面4px AO，左右84%/67%基础光，向下衰减，6px反光|实际体积增强；深侧面仍偏暗。|
|A3 叶簇荧光|每簇5–9弯叶、3色阶、长度110–220纹理像素；#5CF2D6尖光与柔光；前沿草须垂10px|全图固定荧光代理面积 4.1533%（含道具），小于6%；128px仍偏藻毯，叶簇可以继续强化。|
|A4 有机过渡|低频1.35px双轴位移、0.65px滤波/羽化、边缘AO及草须|所有材料评分通过；原遮罩大尺度规律仍在，不宣称5×5没有锯齿或条带。|
|A5 石板|每周期4块错缝大板，暗缝、亮边、风化裂隙、短黄铜镶边、小藤壶|纹理周期边缘裁切会出现部分板片；圆钝/侵蚀质感尚不足。25个石材格中6个有微光符文（离散近1/4），见rune_schedule.json；重复拼纯石格会重复符文。|
|A6 沙和立体贝壳|低对比顺流沙纹；保留原小白连通域位置，计算壳面顶左亮/右下暗与放射肋纹|为固定RGB分类保持蓝灰壳色，未豁免贝壳；小壳128px辨形不足，沉积土仍略像泥砾/暗岩。|
|A7 侧面|约3条起伏地层带、少量嵌石、草根曲线、顶部反光/接触阴影|不是水平平直条，但侧纹重复可见，底部较暗。|
|A8 道具|保留形状和alpha，黄铜渐变/窄高光、玻璃内亮外暗/2–4px柔光、基座AO|16格全部重新着色；玻璃细薄形体仍较平，金属形状偏简化。|


### B 地图：实际实现与不足（逐条）
|条款|本次实现和证据|限制／未达成项|
|---|---|---|
|B1 两处黑洞|调查发现基线右上区域两格、右下区域一格真缺格；只填(-3,-12)、(16,4)、(16,6)，695→698；没有删除旧格，origin/size不变。全景不再见相同内部黑洞。|没有把普通外沿暗侧面当洞；外轮廓锯齿和侧面暗度仍在。|
|B2 主路|cell轴L形(0,0)→(9,0)→(9,9)，主宽3格、石→土遮罩匹配收边，局部露土；世界图像呈两斜段的一次折转，连接出生区域和裂隙门方向。|近景路端仍像几何截断，局部硬切与符文规则重复可见，过渡不等于视觉无缝。|
|B3 战斗区|土为主，四处3×3/3×2/3×4/2×3石残块候选连续区域、5个不对称草岛场；中心矩形安静区约42%格中心（主路例外）。|数量和尺寸是生成场定义，经过已有40格mask匹配后边缘会量化；草岛在全景不够显著；大面积纯土仍重复、噪点均匀。|
|B4 边界|外沿密草、向内连续采样、正弦变化宽度与过渡Tile，使内沿不规则。|原外占用轮廓保护不动；草外沿仍有规则节奏，未达到自然无重复。|
|B5 道具|主路站点0/7/14成对灯共6个，门两侧各1个；3组×3管道，分布上/左下/右下边界。均仅SpriteRenderer装饰，无脚本/碰撞/真实灯光。|素材重复、落地感和光影响弱；不以贴图亮色冒称真实照明。|
|B6 背景|仅Main Camera.backgroundColor改为#07121B（alpha=1）。独立逐记录核对Camera其余字段原样。|实际57f1 GPU截图验证，不是更换渲染系统。|
|B7 备份和重复执行|每次自动JSON备份、immutable 1fed22f场景备份；repeat exit=0、changed=0，当前场景与repaint-first.unity.txt字节相等。RestoreOriginal菜单明确整场景回滚到1fed22f，未实际执行回滚。|首轮repaint因Unity fake-null失败，日志repaint.log保留；显式Unity null检查修复后的repaint-v2和repeat均通过，未隐藏首次失败。|

### 玩法、身份与序列化保护
没有修改运行时玩法源码、碰撞器、玩家/门位置，也未修改美术PNG以追求测试通过。Tilemap无TilemapCollider2D，三格补洞只修视觉。Editor比较197个既有非Tilemap/非专属装饰组件，在应用唯一背景色例外前完全相同；这是展开组件数量，不是YAML记录数。独立`scene-protection.json`：152→173条场景记录，120条受保护基线记录中仅Tilemap内容和Camera背景字段是授权变化，其余玩法记录保持；原695格color/matrix/flags与范围不变。新增/调整的专属装饰单独归类，不冒称场景全字节未改。

PNG仍2048×1792 RGBA、8×7、256px/格。original/v1备份未改；PNG.meta按换行规范化后与1fed22f相同，GUID、sprite name/ID/internalID、rect、pivot和PPU保持；Tile资产和Palette不改。最终图SHA见下。固定1fed22f RGB反分类器独立读最终PNG，不以生成标签作预测，也不豁免贝壳。

### 本次真实命令与结果
命令均在工程根目录执行，最终READY与实际PNG哈希一致后才运行after/build/player：
```bash
python Docs/GameJamIntegration/tiles_hd/rework1/unity/run_editor.py TilesHD.RecordMap.After after
python Docs/GameJamIntegration/tiles_hd/rework1/unity/run_editor.py TapWindowsBuild.Build build -tapbuildpath Builds/Rework1/Windows/TapTap26.exe
python Tools/run_tap_selftest.py --exe Builds/Rework1/Windows/TapTap26.exe --output Docs/GameJamIntegration/tiles_hd/rework1/unity/selftest --timeout 300
python Docs/GameJamIntegration/tiles_hd/rework1/unity/verify_scene.py
python Docs/GameJamIntegration/tiles_hd/rework1/unity/closeout_rework1.py
```
- 本次before/repaint-v2/repaint-repeat/after/build/Player均exit=0；after标记`GPU capture completed; editor raw preferences restored=True`。
- Windows构建实际日志：`[TapWindowsBuild] Succeeded errors=0 warnings=0 bytes=410708450`。独立新路径`Builds/Rework1/Windows/`，不是旧构建。
- 本次Player `RESULT passed=534 failed=0`；final成功流程日志无error CS、Exception:或FAIL标记；首轮repaint失败日志单独保留。
- Editor匹配前缀原值2项；Player匹配前缀原值0项。Editor自恢复、Player退出前自恢复的外部比对及退出后raw恢复全部true。Player有raw类型/未知键测试，但不宣称非空真实玩家存档验证；本次未另跑--baseline/--boss模式，也未故意强杀验证崩溃恢复。
- GPU为NVIDIA GeForce RTX 5080 Laptop GPU，实际Unity为2022.3.57f1。ProjectVersion/packages-lock自动改动在保存tested-*环境副本后恢复基线；Mix与基线已相同，不无故重写。恢复依据baseline manifest+git show一致性，见automatic-files-restored.json。**未在恢复声明2022.3.62f3c1及锁文件后重新构建，不宣称该环境验证通过。**
- 进程查询无Unity.exe/TapTap26.exe/UnityCrashHandler64.exe残留；仅既有mcp-for-unity服务，未停止他人进程。`.vsconfig`和`Tools/__pycache__`原有未追踪项保留，不应提交。构建与私有prefs journal只留本地。

### 40格数值（从本次art CSV读取）
每行依次列Alpha IoU、全格材质正确率、顶面正确率、顶面明度均值、中频相对标准差。混合格标准差包含不同材质明度差，不能用其高值代替15–20%单材质细节要求。完整浮点原值在`art/per_cell_validation.csv`，下表六位小数。

|格号|Alpha IoU|全格材质|顶面材质|顶面明度均值|中频相对标准差|
|---:|---:|---:|---:|---:|---:|
|0|1.000000|0.986012|0.993659|122.065776|0.134911|
|1|1.000000|0.986064|0.993475|122.377461|0.134428|
|2|1.000000|0.981974|0.960908|121.725027|0.160613|
|3|1.000000|1.000000|1.000000|57.838344|0.157864|
|4|1.000000|0.980772|0.966486|120.127910|0.152427|
|5|1.000000|0.980389|0.965705|120.532398|0.127978|
|6|1.000000|0.979499|0.965721|122.209323|0.136734|
|7|1.000000|0.982558|0.971924|120.147554|0.140918|
|8|1.000000|0.987757|0.987676|121.116791|0.130783|
|9|1.000000|0.984994|0.981217|121.494203|0.129899|
|10|1.000000|0.984681|0.981147|119.846454|0.141193|
|11|1.000000|0.985105|0.977715|121.126202|0.128182|
|12|1.000000|0.979553|0.953784|120.774502|0.141028|
|13|1.000000|0.979533|0.954083|121.963883|0.160123|
|14|1.000000|0.978617|0.951269|120.476605|0.150079|
|15|1.000000|0.981344|0.959052|121.778369|0.145721|
|16|1.000000|0.985881|0.958453|79.015714|0.403492|
|17|1.000000|0.984150|0.953353|80.058501|0.378055|
|18|1.000000|0.985191|0.956321|77.946408|0.367125|
|19|1.000000|0.984027|0.953414|78.479322|0.356029|
|20|1.000000|0.987648|0.975195|92.851691|0.371758|
|21|1.000000|0.983523|0.975945|92.220451|0.340545|
|22|1.000000|0.984681|0.980010|93.708599|0.367284|
|23|1.000000|0.986539|0.973225|92.982676|0.358788|
|24|1.000000|0.994812|0.984734|64.967033|0.325059|
|25|1.000000|0.992676|0.980005|64.575478|0.301325|
|26|1.000000|0.992330|0.980125|65.547854|0.327055|
|27|1.000000|0.994466|0.983716|65.549734|0.343457|
|28|1.000000|0.986171|0.983609|113.839510|0.209645|
|29|1.000000|0.985441|0.983550|114.774859|0.222575|
|30|1.000000|0.984805|0.981269|112.725038|0.244709|
|31|1.000000|0.987010|0.984210|113.535397|0.227860|
|32|1.000000|0.981861|0.958204|101.691004|0.312247|
|33|1.000000|0.982899|0.961483|101.016995|0.318043|
|34|1.000000|0.979723|0.965059|102.477870|0.292169|
|35|1.000000|0.977582|0.958814|99.429573|0.290311|
|36|1.000000|0.978987|0.963313|102.563596|0.298682|
|37|1.000000|0.977624|0.959887|102.957077|0.303155|
|38|1.000000|0.983409|0.964030|101.876335|0.304334|
|39|1.000000|0.981231|0.957855|100.417036|0.302788|

40/40 Alpha≥0.98，最小1.000000；全格最小0.977582、顶面最小0.951269均≥0.95。荧光RGB阈值代理占不透明像素4.1533%（含道具），不是物理发光面积；草13.49%不达A1，石16.06%/土15.79%。

### 图片路径与实际开图复核
以下相对`Docs/GameJamIntegration/tiles_hd/rework1/`：
- `art/materials_comparison.png`三种材质对照；`art/texture_0_0.png`至`texture_2_2.png`为9张纹理。
- `art/comparison_128px.png`、`art/comparison_256px.png`新图vs1fed22f；`art/representatives_128.png`、`art/representatives_256.png`代表格与道具。
- `art/seam_3x3_cell_0.png`、`seam_3x3_cell_2.png`、`seam_3x3_cell_3.png`及`art/transition_5x5.png`；`art/visual_review.md`记录美术代理的已开图不足。
- `unity/BiomechanicalCombatScene-before-panorama.png` / `before-closeup.png`与`after-panorama.png` / `after-closeup.png`均为本次1920×1080真实PlayMode相机RenderTexture，before用1fed22f场景及独立内存atlas/Tile/Sprite克隆，不是旧任务截图；不包含overlay Canvas UI，不等于完整HUD framebuffer（相机可见的图标仍可能出现）。
- `unity/selftest/target-after.png`是真实最终Player framebuffer，有HUD与Development Build标记。其他selftest场景截图在同目录，不代替地图前后对比。

本执行代理已实际开图复核before/after全景与近景、128/256代表图及最终Player target-after：路线由屏幕纵向锯齿柱改为网格轴折转，两处报告黑洞不再呈现；深海背景成立。近景土面高频碎屑均匀、周期重复显著，石纹和符文重复、道路硬边和端部收口仍弱；青绿材质128px/256px仍有藻毯或矿物感，侧面底部过暗，小壳与细道具结构不够清楚。角色叠加及头顶条压近头部在before已经存在，不误报为此次新增，也未擅改角色。**这是技术可运行候选，仍需美术负责人复验，不签美术通过。**

### 测试与产物SHA256
- `Assets/Pic/Test/basic_ground_tiles.png`：`34b525b1926db531944c34d66421d1696e28ef800a347c21f66b91d476f46214`
- `Assets/Scenes/BiomechanicalCombatScene.unity`：`eb86c11ad1dfe482b9e22ac78fa2cce072692bff40d852373f30f47b2ba2a2f1`
- `Builds/Rework1/Windows/TapTap26.exe`：`2c392ecb0dac1e2e5211b488220e26b6c30dfde366d10ccfeb938cf572b856dc`
- `Builds/Rework1/Windows/TapTap26_Data/Managed/Assembly-CSharp.dll`：`2dd27e1253fa03717273a9765a2726b7161cb386f43314be3f5cd996cf222c87`
- `Docs/GameJamIntegration/tiles_hd/rework1/unity/selftest/selftest.txt`：`c03e59bf4d510c19d12caf022aaae0b9d90b319ced50550a2f90ee599264ba95`
- `Docs/GameJamIntegration/tiles_hd/rework1/unity/selftest/player.log`：`7a4a27ee7aae8ee0273d70306bfbe47d2f74fefc54d15268a0625cb31cac1905`
- `Docs/GameJamIntegration/tiles_hd/rework1/unity/selftest/target-after.png`：`d2b9885b44381374f21862cec5b0ef283feff5c078d321d2ca4ed8968f8be66b`

完整构建文件、截图、关键脚本/日志哈希：`unity/tested-artifact-hashes.json`；严格收口结果：`unity/closeout-result.json`；进程证据：`unity/process-audit.json`。
