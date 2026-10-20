"""Read-only verification/report closeout for the completed rework1 runs; no Unity launch."""
from pathlib import Path
import csv, hashlib, json, re, subprocess
root=Path(__file__).resolve().parents[5]
u=Path(__file__).parent
r=u.parent
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
load=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
expected='34b525b1926db531944c34d66421d1696e28ef800a347c21f66b91d476f46214'
assert sha(root/'Assets/Pic/Test/basic_ground_tiles.png')==load(r/'art/READY.json')['atlas_sha256']==expected
assert subprocess.check_output(['git','branch','--show-current'],cwd=root,text=True).strip()=='feature/hd-ground-tiles'
assert (root/'Assets/Scenes/BiomechanicalCombatScene.unity').read_bytes()==(u/'repaint-first.unity.txt').read_bytes()
assert 'changed=0' in (u/'repaint-result.txt').read_text()
exits={}
for label in ['before','repaint-v2','repaint-repeat','after','build']:
    data=load(u/(label+'-supervisor-result.json'))
    assert type(data['exit_code']) is int and data['exit_code']==0
    assert data['editor_restored_before_supervisor'] is True and data['raw_restored_after_exit'] is True
    exits[label]=data['exit_code']
p=load(u/'selftest/supervisor-result.json')
assert type(p['exit_code']) is int and p['exit_code']==0
assert p['player_restored_before_supervisor'] is True and p['post_exit_raw_restore'] is True
assert 'RESULT passed=534 failed=0' in (u/'selftest/selftest.txt').read_text(encoding='utf-8')
assert 'GPU capture completed; editor raw preferences restored=True' in (u/'record-after-result.txt').read_text()
assert '[TapWindowsBuild] Succeeded errors=0 warnings=0' in (u/'build.log').read_text(encoding='utf-8')
protection=load(u/'scene-protection.json')
assert protection['passed'] is True and not protection['issues']
assert protection['final_scene_sha256']==sha(root/'Assets/Scenes/BiomechanicalCombatScene.unity')
errors=[]
for label in ['before.log','repaint-v2.log','repaint-repeat.log','after.log','build.log','selftest/player.log']:
    for line in (u/label).read_text(encoding='utf-8',errors='replace').splitlines():
        if re.search(r'error CS\d+|\b\w*Exception:|\bFAIL\b|RESULT.*failed=[1-9]',line): errors.append([label,line])
assert not errors,errors
ps=subprocess.check_output(['powershell.exe','-NoProfile','-Command',"Get-CimInstance Win32_Process | Where-Object { $_.Name -match 'Unity|TapTap' } | Select-Object ProcessId,Name,CommandLine | ConvertTo-Json -Depth 3"],text=True,encoding='utf-8')
processes=json.loads(ps) if ps.strip() else []
if isinstance(processes,dict):processes=[processes]
assert not [p for p in processes if p['Name'].lower() in ['unity.exe','taptap26.exe','unitycrashhandler64.exe']]
(u/'process-audit.json').write_text(json.dumps(processes,ensure_ascii=False,indent=2),encoding='utf-8')
restored=load(u/'automatic-files-restored.json')
assert all(v['restored_matches_baseline_ignoring_eol'] is True for v in restored.values())
for name in restored:
    assert (root/name).read_bytes().replace(b'\r\n',b'\n')==(r/'baseline'/Path(name).name).read_bytes().replace(b'\r\n',b'\n')
files=[root/'Assets/Pic/Test/basic_ground_tiles.png',root/'Assets/Scenes/BiomechanicalCombatScene.unity',root/'Assets/Editor/TilesHD/RecordMap.cs',root/'Assets/Editor/TilesHD/RepaintMap.cs',root/'Tools/run_tap_selftest.py',u/'selftest/selftest.txt',u/'selftest/player.log',u/'selftest/target-after.png',u/'after.log',u/'build.log',r/'art/per_cell_validation.csv']
files+=list(u.glob('BiomechanicalCombatScene-*.png'))
files+=list((root/'Builds/Rework1/Windows').rglob('*'))
hashes={x.relative_to(root).as_posix():sha(x) for x in files if x.is_file()}
(u/'tested-artifact-hashes.json').write_text(json.dumps(hashes,indent=2),encoding='utf-8')
result={'technical_passed':True,'art_approved':False,'editor_exits':exits,'player_exit':0,'selftest':'RESULT passed=534 failed=0','atlas_sha256':expected,'scene_sha256':protection['final_scene_sha256'],'repeat_changed':0,'repeat_scene_bytes_identical':True,'detected_error_lines':errors,'editor_original_prefix_count':len(load(u/'after-supervisor-before.json')),'player_original_prefix_count':p['original_TAP_count'],'owned_processes_remaining':[],'actual_unity':'2022.3.57f1','restored_declared_unity':'2022.3.62f3c1','restored_environment_rebuilt':False}
(u/'closeout-result.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
rows=list(csv.DictReader((r/'art/per_cell_validation.csv').open(encoding='utf-8')))
assert len(rows)==40
art=(r/'art/HANDOFF.md').read_text(encoding='utf-8')
a_table=art.split('## A逐项结果与不足\n',1)[1].split('\n## 独立自检',1)[0]
section='''

## 返工 1（基线 1fed22f；本次独立执行证据）

### 结论与版本边界
**本次最终PNG的Unity技术门槛通过：after录制、Windows构建、隔离Player自测534 PASS / 0 FAIL；不代表美术验收通过。** A1草中频实测13.49%，低于15%下界，重复、过渡硬边与材质辨识不足仍保留。上文初版数据/制作方法属于1fed22f历史，不是本次证据。本节及`tiles_hd/rework1/`才描述返工1。

工作区`E:/gamejam_wt/tiles_hd`，分支`feature/hd-ground-tiles`，基线`1fed22feae2faec7e465c5cacf35b5d7b468e2b0`。本执行代理未提交、未push；新提交与push由主负责人另行核验执行，绝不改main。

### A 地砖：实际实现与不足（逐条）
本次是**真实SDXL AI材质来源 + 程序化绘制混合**，不是初版纯程序化，也不冒称手绘。ComfyUI三次1024×1024生成、28步、固定seed 982073/982074/982075；模型为sd_xl_base_1.0.safetensors。workflow/history/系统信息/原图由美术代理留档。经周期边缘混合、色板重映射及分面投影，再叠叶簇、裂隙、沙纹、光照。3种材质各3个纹理偏移变体不等于地图每种纯材质具备3个独立Tile变体。

'''+a_table+'''

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
'''
for row in rows:
    section+='|'+row['cell']+'|'+ '|'.join(f"{float(row[k]):.6f}" for k in ['alpha_iou','material_accuracy','top_accuracy','top_luminance_mean','medium_frequency_relative_std'])+'|\n'
section+='''
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
'''
for name in ['Assets/Pic/Test/basic_ground_tiles.png','Assets/Scenes/BiomechanicalCombatScene.unity','Builds/Rework1/Windows/TapTap26.exe','Builds/Rework1/Windows/TapTap26_Data/Managed/Assembly-CSharp.dll','Docs/GameJamIntegration/tiles_hd/rework1/unity/selftest/selftest.txt','Docs/GameJamIntegration/tiles_hd/rework1/unity/selftest/player.log','Docs/GameJamIntegration/tiles_hd/rework1/unity/selftest/target-after.png']:
    section+=f'- `{name}`：`{hashes[name]}`\n'
section+='\n完整构建文件、截图、关键脚本/日志哈希：`unity/tested-artifact-hashes.json`；严格收口结果：`unity/closeout-result.json`；进程证据：`unity/process-audit.json`。\n'
report=root/'Docs/GameJamIntegration/REPORT_TILES_HD.md'
text=report.read_text(encoding='utf-8')
marker='\n\n## 返工 1（基线 1fed22f；本次独立执行证据）'
if marker in text:text=text.split(marker)[0]
report.write_text(text+section,encoding='utf-8')
print(json.dumps(result,ensure_ascii=False,indent=2))
print('REPORT',report)
print('PLAYER_EXE_SHA256',hashes['Builds/Rework1/Windows/TapTap26.exe'])
print('SELFTEST_SHA256',hashes['Docs/GameJamIntegration/tiles_hd/rework1/unity/selftest/selftest.txt'])
