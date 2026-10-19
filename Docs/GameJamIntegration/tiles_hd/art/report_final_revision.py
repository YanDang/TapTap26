"""Bind the final art iteration report to the actual saved atlas."""
from pathlib import Path
import json,hashlib
import numpy as np
from PIL import Image
from build_tiles_hd import source_mask
A=Path(__file__).resolve().parent; P=A.parents[3]; S=P/'Assets/Pic/Test'
new=np.array(Image.open(S/'basic_ground_tiles.png'));old=np.array(Image.open(A/'revision_before/basic_ground_tiles.png'))
orig=np.array(Image.open(S/'basic_ground_tiles_original_backup.png'))
v=json.loads((A/'validation.json').read_text()); audit=json.loads((A/'supplemental_validation.json').read_text());before=json.loads((A/'revision_before/supplemental_validation.json').read_text())
y,x=np.mgrid[:256,:256];u=(x-128)/256+y/128;w=-(x-128)/256+y/128;top=(u>=0)&(u<=1)&(w>=0)&(w<=1)
strict=[];shellbefore=[];shellafter=[]
for i in range(40):
 r,c=divmod(i,8);a=orig[r*128:(r+1)*128,c*128:(c+1)*128];m,shell=source_mask(a);m=m.repeat(2,0).repeat(2,1);shell=shell.repeat(2,0).repeat(2,1);valid=(a[...,3]>8).repeat(2,0).repeat(2,1)&top
 b=new[r*256:(r+1)*256,c*256:(c+1)*256];o=old[r*256:(r+1)*256,c*256:(c+1)*256];rr,g,bl=b[...,:3].astype(float).transpose(2,0,1)
 pred=np.full(m.shape,2,np.uint8);pred[(bl-g)<=g*.12]=1;pred[(g>rr*1.55)&(g>bl*1.08)]=0
 strict.append(float((pred[valid]==m[valid]).mean()))
 shellbefore.extend(o[valid&shell,:3].mean(axis=1).tolist());shellafter.extend(b[valid&shell,:3].mean(axis=1).tolist())
checks={'sha256':v['sha256'],'alpha_byte_identical_to_previous':bool(np.array_equal(new[...,3],old[...,3])),'props_byte_identical_to_previous':bool(np.array_equal(new[1280:],old[1280:])),'meta_sha256':hashlib.sha256((S/'basic_ground_tiles.png.meta').read_bytes()).hexdigest(),'strict_top_original_geometry_min':min(strict),'strict_whole_min':min(q['material_accuracy'] for q in v['rows']),'shell_rgb_mean_before':float(np.mean(shellbefore)),'shell_rgb_mean_after':float(np.mean(shellafter))}
assert checks['alpha_byte_identical_to_previous'] and checks['props_byte_identical_to_previous']
assert checks['strict_top_original_geometry_min']>=.95 and checks['strict_whole_min']>=.95
assert checks['meta_sha256']=='a96d3599814fe3e67494cdf9492d42c4b2bb23f36ae6ccbf91ba81f8f94db8ed'
(A/'final_revision_validation.json').write_text(json.dumps(checks,indent=2))
lines=['# 高清贴图最终局部修正交接','', '## 状态：本轮CPU看图有实质改善；不是Unity美术验收通过','', '完整阅读TASK与GUIDE。本轮仅修改art内制作/证据和basic_ground_tiles.png；构建按基线重写meta但字节SHA与本轮前一致。未修改场景/C#/Unity，未提交。原图与v1备份不改。', '', '## 实质修复', '- 白色贝壳从RGB(165,184,184)改为低对比蓝灰：原位置保留，四分之一以55%强度混入(61,79,94)，其余以12%强度埋入底色。不是用贝壳豁免掩盖反分类失败。', '- 找到顶面格框来源：接触AO把透明区域误当其他材质，沿每格轮廓画暗框。现在只遮蔽真实不透明异材质接触；顶色向原alpha阶梯下延伸约1.5像素，alpha完全不改。没有另加顶边高光。', '- 石板半余弦取模导致边缘跳变，改成全周期平滑起伏；板缝移至纹理内部，降低细高光。', '- 草束从等长三叉折线换成不等长、弯曲、渐尖的根生叶扇，减少重复高频笔触。道具不改。', '', '## 运行与严格验证', '实际运行build_tiles_hd.py、audit_tiles_hd.py、report_final_revision.py。全部通过。', f"- 40格alpha IoU全部1；本轮前后alpha逐字节一致。全格严格反分类最低{checks['strict_whole_min']:.6%}，原几何顶面严格反分类最低{min(strict):.6%}。分类器阈值不变、不豁免贝壳。", '- meta字节SHA不变，56 sprite身份/rect/pivot保持；16道具像素逐字节不变；原图与HEAD、v1与baseline核验通过。', f"- 原贝壳位置RGB均值：{checks['shell_rgb_mean_before']:.3f} → {checks['shell_rgb_mean_after']:.3f}（非感知亮度指标）。", '', '|石板变体|旧wrap_x MAE|新wrap_x MAE|新内部x MAE|', '|---|---:|---:|---:|']
for b,n in zip(before['texture_edges'],audit['texture_edges']):
 if 'limestone' in n['file']:lines.append(f"|{n['file']}|{b['wrap_x_MAE']:.6f}|{n['wrap_x_MAE']:.6f}|{n['interior_x_MAE']:.6f}|")
lines+=['', '## 最终图片已实际检查', '已通过图像工具检查最终revision_before_after.png和transition_5x5.png；对比图左旧右新、由上至下为土/石/草。看图确认土面白点显著消退，土面安静、三材质格框明显减弱，石板普通铺缝保留。不是只依靠数值判断。', '', '**仍未通过/限制：**土仍偏蓝灰石质而非强泥土感；同格复用仍有云纹/草束节奏；石板铺装规则。5×5边界缺口与碎石重复、前端转折生硬，草侧面重复分段仍明显。没有为打破重复而动地图或alpha，也没有冒称达到全部艺术标准。', '', '**现有unity/BiomechanicalCombatScene-after-closeup.png是本轮前旧图，不能当作最终PNG验收证据。父代理须重新Unity导入、截图和自测。旧534/0不代表本轮已跑。**', '', '## 文件与复现', '- 最终图：Assets/Pic/Test/basic_ground_tiles.png', '- 本目录build_tiles_hd.py → audit_tiles_hd.py → report_final_revision.py，依次运行。', '- revision_before_after.png：本轮前后对照；revision_before/：只读本轮前快照。', '- comparison_128px.png、seam_3x3_cell_{0,2,3}.png、transition_5x5.png、9张texture_*已重建。', '- validation.json、per_cell_validation.csv、supplemental_validation.json、final_revision_validation.json为当前证据。', '- seam_3x3_cell_1.png与cell_16.png为历史样例，不作为当前证据。', '', '## 最终PNG SHA256', '`'+v['sha256']+'`', '', '## 40格严格结果', '|格|alpha IoU|全格反分类|原几何顶面反分类|','|---:|---:|---:|---:|']
for row,score in zip(v['rows'],strict):lines.append(f"|{row['cell']}|{row['alpha_iou']:.6f}|{row['material_accuracy']:.6f}|{score:.6f}|")
handoff=A/'HANDOFF.md'
previous=handoff.read_text(encoding='utf-8') if handoff.exists() else ''
marker='## 总负责最终复核补记'
supplement='\n'+previous[previous.index(marker):] if marker in previous else ''
handoff.write_text('\n'.join(lines)+'\n'+supplement,encoding='utf-8')
print(json.dumps(checks,indent=2))
