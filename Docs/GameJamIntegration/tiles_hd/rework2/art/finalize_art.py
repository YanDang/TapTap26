from pathlib import Path
import json,hashlib
import numpy as np
from PIL import Image
A=Path(__file__).resolve().parent;P=A.parents[4];S=P/'Assets/Pic/Test';h=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
v=json.loads((A/'validation.json').read_text());assert v['all_alpha_pass'] and v['all_material_pass'] and v['all_top_pass'] and all(v['protected_files'].values());assert all(n==16 for n in v['distinct_variants_per_ground_cell'].values())
files=[S/'basic_ground_tiles.png']+sorted((S/'TilesHDVariants').glob('variant_*.png'));assert len(files)==17
production={str(p.relative_to(P)).replace('\\','/'):{'sha256':h(p),'bytes':p.stat().st_size,'size':list(Image.open(p).size)} for p in files}
assert h(files[0])==h(files[1])
base=np.array(Image.open(A/'baseline/basic_ground_tiles.png'));new=np.array(Image.open(files[0]));y,x=np.mgrid[:256,:256];u=(x-128)/256+y/128;vv=-(x-128)/256+y/128;t=(u>=0)&(u<=1)&(vv>=0)&(vv<=1)
a=base[:256,:256,:3].astype(float);b=new[:256,:256,:3].astype(float)
lums=[float((z[t]@np.array([.2126,.7152,.0722])).mean()) for z in [a,b]];sats=[float(((z.max(2)-z.min(2))/np.maximum(z.max(2),1))[t].mean()) for z in [a,b]]
extra={'pure_grass_cell0_luminance_before_after':lums,'pure_grass_cell0_luminance_reduction':1-lums[1]/lums[0],'pure_grass_cell0_HSV_saturation_before_after':sats,'pure_grass_cell0_HSV_saturation_reduction':1-sats[1]/sats[0], 'props_alpha_byte_equal_to_879645d':bool(np.array_equal(base[1280:,:,3],new[1280:,:,3])),'total_production_png_bytes':sum(p.stat().st_size for p in files),'no_1_or_2_cell_full_tile_repeat':all(n==16 for n in v['distinct_variants_per_ground_cell'].values()),'repeat_test_definition':'40 ground cell images each have16 unique SHA; coordinate 4x4 schedule has no full-image shifts of1 or2 in either axis. This is NOT a perceptual no-repeat guarantee.','baseline_sha256':h(A/'baseline/basic_ground_tiles.png')}
(A/'supplemental_validation.json').write_text(json.dumps(extra,indent=2))
contract=json.loads((A/'variant_contract.json').read_text());contract['status']='final';contract['sha256_manifest']='READY.json: production_pngs';(A/'variant_contract.json').write_text(json.dumps(contract,indent=2))
text=f'''# 返工2美术交接（仅离线资产，不签全部美术通过）

## 稳定产物
- 主图 `Assets/Pic/Test/basic_ground_tiles.png`，SHA256 `{h(files[0])}`。
- 16张 `Assets/Pic/Test/TilesHDVariants/variant_00.png` 至 `variant_15.png`，全部2048×1792、RGBA、8×7、56格256px。主图等于variant00；后16道具格一致，前40格各有16个不同PNG像素内容。
- **必须按 `variant=(positive_mod(cell_x,4))+4*positive_mod(cell_y,4)` 选择。** 石板基于2048方形世界布局按512区块采样，不能随机选，否则石缝连续性会丢失。土也按连续世界坐标取样，草在方形空间旋转/偏移。详见variant_contract.json。PPU256、pivot(0.5,0.25)，原meta绝未修改。
- PNG总量{extra['total_production_png_bytes']}字节。各生产PNG哈希在READY.json；其他离线证据在manifest.json。
- 本代理没有启动Unity、没有改场景/C#/主报告/备份/meta，没有commit/push。

## 真实来源与制作
复用返工1真实ComfyUI/SDXL原材质，原始图片、workflow、successful history与system stats原样复制到本目录。**本次没有重新提交AI生图任务**。SDXL纹理仍提供真实中频/低频表面明度：草改较稀疏7叶簇，石重新在方形大世界纹理绘制跨格错缝，土去壳点并平滑细碎沙纹；非整张AI生成，不冒称纯手绘。

879645d的PNG/meta/原备份通过git show在第一次正式写图前提取至art/baseline，后续重跑不覆盖immutable基线。不是复制并发变化的现图。

## 返工2逐条
1. 普通土（尤其纯土3）完全不叠原白点壳。仅18–27的原白点小连通域画有阴影/肋纹的小壳。16变体且无1/2格完整图案周期；**仍有4格周期**。最终土8x8无明显白点和块状边，偏软/暗岩。
2. 石基色#6D8186乘0.9、低对比真实SDXL表面；在2048世界方形先绘大板、磨边/划痕再裁，每格不画闭合菱形缝。符文按约1/6的(变体,格)稀疏计划、随机安全位置、RGB降为(31,116,112)，没有全图同一位置强亮标。旧云噪削弱，仍有细点状接线及4格重复，不能写完全无缝。
3. 保留原石土过渡遮罩和碎石格，遮罩轻扰动/羽化；提供5x5混合预览。场景主路实际选过渡格由Unity代理负责，不能以离线组合冒充地图验收。
4. 草色采用HSV饱和*.75/value*.8处理新叶簇；实测纯草0顶面相对879645d明度降低{extra['pure_grass_cell0_luminance_reduction']:.2%}，HSV饱和降低{extra['pure_grass_cell0_HSV_saturation_reduction']:.2%}。极少叶尖亮点，仍有128px下叶簇辨识弱/细格缝问题。角色最亮排序需要引擎截图验证。
5. 玻璃中心#7FF5E6→边缘#126A66距离径向场；外缘细青线和孤立点抑制，玻璃局部高斯外光半径10px、混合上限18%，无全框发光；黄铜上左窄高光。最后实际开图看到金属/玻璃分离改善，但玻璃更像发光液体，支架形体简化。
6. 灯3对、上半部草岛扩大属于地图代理；未擅自修改场景。

## 固定独立图像QA
- 16×40=640格，Alpha最小{v['min_alpha_iou']:.6f}，全格材质最小{v['min_material_accuracy']:.6f}，顶面材质最小{v['min_top_accuracy']:.6f}，全通过原.98/.95阈值。
- 从最终PNG重新反分类；固定1fed22f/879645d规则与评分区域，无壳豁免、无改阈值。
- 原meta、original/v1及meta保护通过；meta仅换行规范化比较，未修改原字节。道具alpha与879645d逐字节一致。
- 每格数值per_cell_validation.csv；validation.json；supplemental_validation.json。技术合格不等于美术签收。

## 实际开图证据
comparison_128px / comparison_256px（879645d vs新）、representatives_128 / 256、materials_comparison、variants_3x3_cell_0/2/3、variants_8x8_cell_0/2/3、transition_5x5。`diagnostic_single_8x8_*`是故意重复单张的反例，不是变体验收图。完整观察见visual_review.md。全部是离线CPU图，不是游戏截图。

## 复现
工程根运行 `python Docs/GameJamIntegration/tiles_hd/rework2/art/build_art.py`，然后verify_art.py；实际开图复审后再运行finalize_art.py。build自动删除旧READY以免中间图误导入。最终READY写在所有生产/验证文件稳定之后。
'''
(A/'handoff.md').write_text(text,encoding='utf-8')
manifest={str(p.relative_to(A)).replace('\\','/'):h(p) for p in sorted(A.rglob('*')) if p.is_file() and p.name not in ['manifest.json','READY.json'] and '__pycache__' not in str(p)}
(A/'manifest.json').write_text(json.dumps(manifest,indent=2))
ready={'status':'ready_for_independent_review_and_unity_import','atlas_path':'Assets/Pic/Test/basic_ground_tiles.png','atlas_sha256':h(files[0]),'variant_count':16,'production_pngs':production,'technical_image_checks_passed':True,'art_approved':False,'validation':{'min_alpha_iou':v['min_alpha_iou'],'min_material_accuracy':v['min_material_accuracy'],'min_top_accuracy':v['min_top_accuracy'],'tested_ground_cells':640,'protected_files':v['protected_files']},'known_limitations':['4-cell repetition','grass fine grid seams and weak broad-leaf readability','some stone dotted/double joints','soil soft material identity','glass reads luminous liquid','runtime hierarchy and map changes unverified by art agent'],'handoff':'Docs/GameJamIntegration/tiles_hd/rework2/art/handoff.md','variant_contract':'Docs/GameJamIntegration/tiles_hd/rework2/art/variant_contract.json'}
(A/'READY.tmp').write_text(json.dumps(ready,indent=2));(A/'READY.tmp').replace(A/'READY.json')
print(json.dumps({'status':ready['status'],'atlas_sha256':ready['atlas_sha256'],'variant_count':16,'technical_image_checks_passed':True,'art_approved':False,'extra':extra},indent=2))
