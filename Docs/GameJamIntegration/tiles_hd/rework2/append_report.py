from pathlib import Path
import csv
root=Path(__file__).resolve().parents[4]
out=root/'Docs/GameJamIntegration/REPORT_TILES_HD.md'
base=Path(__file__).parent
rows=list(csv.DictReader((base/'art/per_cell_validation.csv').open()))
assert len(rows)==640
text='''

## 返工 2（基线879645d；新的制作、地图和运行证据）

### 总负责结论
本轮已实际实现并运行：主图重绘、16坐标变体、640静态Tile接入、地图调整、真实前后截图、新Windows构建与534 PASS / 0 FAIL。总负责人重新运行独立scene/integration/closeout三项检查，均exit=0，并复看最终近景和土8×8图。**技术通过，不签全部美术合格**：仍有四格纹理周期、游戏近景菱形明暗节奏、局部道路硬切和细双缝。返工单“截图看不出≤2格重复”的视觉标准尚不能认定完全满足，不能以16个不同像素变体替代视觉验收。

基线879645d，工作区E:/gamejam_wt/tiles_hd，分支feature/hd-ground-tiles；三个执行子代理实际模型均gpt-6-astra。无main操作，无改写历史。新提交仅显式推送origin feature/hd-ground-tiles。最终commit见终端交付及Git历史。

### 六项返工落实情况
|条目|实际实现|验收边界|
|---|---|---|
|1 沉积土白点/重复|普通土移除固定白壳，仅18–27原白点小连通域绘制明暗壳；16个不同纹理变体，方形空间采样；真实坐标8×8检查|白色点阵消失；无1/2格完整像素图案周期，但游戏图仍呈菱形明暗节奏及四格周期，不能宣称视觉完全无重复。|
|2 石板/符文|2048方形世界石板先绘再切成4×4；平整表面、磨边/少量划痕，石基色#6D8186乘0.9；符文按约1/6变体格稀疏计划，位置变化、亮度减半|不再每格闭合菱形缝+同位强亮符文；仍有点状/双缝和重复。|
|3 路缘|L路保留，宽度轻变化，6个碎石区域，连续地形采样匹配石土过渡格；实际9个路缘混合格坐标见integration-protection.json|确有过渡格而非只改口头描述；截图部分路边和端头仍硬，未签全部自然收口。|
|4 海草|替换为7叶簇，HSV饱和×.75/亮度×.8，仅少量叶尖光；实测纯草0顶面亮度降低22.94%、饱和降低26.16%|叶簇在128px仍弱、细格缝仍可见。最终近景角色明显为彩色焦点，灯次之；不以近景无大片草证明全场景绝对层级。|
|5 道具|玻璃中心#7FF5E6→边缘#126A66径向渐变，抑制外框青边，局部弱外光半径10px、混合上限18%；黄铜窄高光|灯/管道最终实图可见；玻璃仍偏发光液体、支架简化。无真实Light，不冒称地面受光。|
|6 地图|删两根额外门灯，保留3对6灯，沿L路站点0/7/14；扩大上半部两处草岛并加不规则过渡，保留三组9管道、深海背景与石残块|原698格占用、玩法对象位置/组件不变；全景实际复核灯管道存在。|

### 制作来源与变体接入
本轮复用返工1真实ComfyUI/SDXL原材质，没有重新提交AI任务；原图、workflow、history保存在rework2/art。真实AI明度素材与确定性程序化叶簇、世界石板、玻璃/壳明暗混合，不是整张AI生成，不冒称纯手绘。

主atlas仍2048×1792、8×7、256px；原meta、guid、所有Sprite身份、PPU/pivot、原始备份保持。新增Assets/Pic/Test/TilesHDVariants/variant_00..15.png及640新Tile，不修改旧Tile与Palette。用positive_mod(x,4)+4*positive_mod(y,4)选择变体，保证石板世界布局连续；不能随机替换石材变体。全698格索引已独立回读验证。

多纹理在Chunk Tilemap下曾产生排序破碎，新增TilesHDVariants.spriteatlas合批后实际GPU地面恢复平整，运行时一张8192×8192未压缩纹理；显存、移动平台性能尚未专项测。另一次中间场景灯/管道实际对象缺失：保存诊断后恢复对象身份，重铺器增加打包后源Tile重载和缺失即失败，禁止静默跳过后删除；录制增加6灯/9管道契约。底层首次失效触发未重现，不把猜测写成已证实原因。

### 保护与真实测试
- 原698格无增加/删除，origin/size、原cell color/matrix/flags保持；120受保护序列化记录不变，唯一授权记录变化Tilemap；173→167记录来自移除两盏门灯。392原资产/源码保护通过。
- before从879645d不可变Git快照实际录制，独立内存atlas/Tile及装饰Sprite，不临时改共享PNG。before/after全景近景均1920×1080 PlayMode相机RenderTexture，不含overlay Canvas，不等同完整HUD。
- repaint-final/repeat/after-final/build及Player均exit=0；repeat changed=0且场景与repaint-accepted.unity.txt字节一致。
- 新Builds/Rework2/Windows构建errors=0、warnings=0；实际Player RESULT passed=534 failed=0；selftest/target-after.png为真实完整HUD framebuffer。未用返工1结果替代。
- Editor原前缀2项、Player0项；自身恢复外部比对和supervisor raw恢复通过。未声称非空真实Player存档、强杀恢复、baseline/boss额外矩阵测试。
- 实际Unity2022.3.57f1；归档tested环境后仅恢复自动ProjectVersion/lock，Mix原本相同。恢复声明2022.3.62f3c1后未重新构建，不能声称该版本已通过。本轮末审核无所属Unity/Player残留。

命令（工程根；详情见unity/handoff.md）：
```
python Docs/GameJamIntegration/tiles_hd/rework2/unity/run_editor.py TilesHD.RepaintMap.Repaint repaint-final
python Docs/GameJamIntegration/tiles_hd/rework2/unity/run_editor.py TilesHD.RepaintMap.Repaint repaint-final-repeat
python Docs/GameJamIntegration/tiles_hd/rework2/unity/run_editor.py TilesHD.RecordMap.After after-final
python Docs/GameJamIntegration/tiles_hd/rework2/unity/run_editor.py TapWindowsBuild.Build build -tapbuildpath Builds/Rework2/Windows/TapTap26.exe
python Tools/run_tap_selftest.py --exe Builds/Rework2/Windows/TapTap26.exe --output Docs/GameJamIntegration/tiles_hd/rework2/unity/selftest --timeout 300
python Docs/GameJamIntegration/tiles_hd/rework2/unity/verify_scene.py
python Docs/GameJamIntegration/tiles_hd/rework2/unity/verify_integration.py
python Docs/GameJamIntegration/tiles_hd/rework2/unity/closeout.py
```

### 每格独立检查
固定既有RGB反分类阈值与评分区域，最终PNG重新读取；无贝壳豁免、不以生成mask作预测。640格全部Alpha≥.98、材质≥.95；全局最低分别1.000000/0.981165/0.959888。下表每种格号取16变体的各项最小值（不同列可能来自不同变体）；全部640行原始值见art/per_cell_validation.csv。

|格号|最小Alpha IoU|最小全格材质|最小顶面材质|
|---:|---:|---:|---:|
'''
for cell in range(40):
 r=[x for x in rows if int(x['cell'])==cell]
 text+=f"|{cell}|"+'|'.join(f"{min(float(x[k]) for x in r):.6f}" for k in ['alpha_iou','material_accuracy','top_accuracy'])+'|\n'
text+='''
### 证据清单（相对tiles_hd/rework2）
- art/materials_comparison.png、comparison_128px.png、comparison_256px.png：材质/879645d前后总图。
- art/representatives_128.png、representatives_256.png：代表格与道具。
- art/variants_8x8_cell_0/2/3.png、variants_3x3_cell_0/2/3.png、transition_5x5.png：实际坐标变体与混合格铺图。
- art/diagnostic_single_8x8_cell_*.png：单张固定重复反例，不是实际变体铺图。
- unity/BiomechanicalCombatScene-before/after-panorama/closeup.png：四张游戏相机对照（文件名实际为before-panorama等）。
- unity/selftest/target-after.png、selftest.txt、player.log、supervisor-result.json：最终真实Player证据。
- art/READY.json、manifest.json、validation.json、supplemental_validation.json；unity/closeout-result.json、tested-artifact-hashes.json、scene-protection.json、integration-protection.json、automatic-files-restored.json、process-audit.json。
- 历史失败图及日志位于diagnostic-unpacked、diagnostic-missing-decor，不能当最终图。额外自测附产截图仅本地保留；私有prefs快照、构建二进制不提交。所有文件哈希清单可包含本地未提交证据，不表示它们都在Git内。

主PNG SHA256：9cfa33cea6c5c266849121adc2baa2aa9612cedc9282e578cf62bbd718664920
场景SHA256：2207f8c305477664adf95387720248c1f46ddc8fdf33dfbb3754663704fed01a

### 总负责人最终视觉意见
最终近景无原白色点阵，角色比地面更醒目，灯具可见且没有整框满青光；但底面周期明暗、石缝细双线、道路部分直切仍明显。8×8土图未见明显白点或硬接缝，但可辨周期纹理。辅助视觉工具关于“动画闪烁周期”的建议不适用本任务：这里检查空间重复，不新增动画验证要求。保留art_approved=false，请美术指导按实际图片复验；这是一份有完整技术证据的返工候选，不是美术全条款完成声明。
'''
old=out.read_text(encoding='utf-8')
marker='\n\n## 返工 2（'
if marker in old: old=old[:old.index(marker)]
out.write_text(old+text,encoding='utf-8')
print(out)
