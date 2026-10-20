"""Read-only atlas closeout; READY is always written last."""
from pathlib import Path
import json,hashlib
import numpy as np
from PIL import Image
A=Path(__file__).resolve().parent;P=A.parents[4];S=P/'Assets/Pic/Test';B=A.parent/'baseline'
h=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
v=json.loads((A/'validation.json').read_text());sha=h(S/'basic_ground_tiles.png')
assert v['atlas_sha256']==sha and v['all_alpha_pass'] and v['all_material_pass'] and v['all_top_pass']
assert all(v['protected_files'].values())
assert h(A/'atlas_final.png')==sha
new=np.array(Image.open(S/'basic_ground_tiles.png'));base=np.array(Image.open(B/'basic_ground_tiles.png'))
extra={'props_alpha_byte_equal_to_1fed22f':bool(np.array_equal(new[1280:,:,3],base[1280:,:,3])),'baseline_png_sha256':h(B/'basic_ground_tiles.png'),'baseline_meta_git_bytes_sha256':h(B/'basic_ground_tiles.png.meta'),'working_meta_sha256':h(S/'basic_ground_tiles.png.meta'),'meta_comparison_note':'Git baseline LF, worktree CRLF; normalized bytes exactly identical, no metadata edits by art agent.','texture_metrics':[]}
for p in sorted(A.glob('texture_*_*.png')):
 a=np.array(Image.open(p)).astype(float)
 extra['texture_metrics'].append({'name':p.name,'wrap_x_mae':float(abs(a[:,0]-a[:,-1]).mean()),'wrap_y_mae':float(abs(a[0]-a[-1]).mean()),'interior_x_mae':float(abs(np.diff(a,axis=1)).mean()),'interior_y_mae':float(abs(np.diff(a,axis=0)).mean())})
(A/'supplemental_validation.json').write_text(json.dumps(extra,indent=2))
schedule=json.loads((A/'rune_schedule.json').read_text())
text=f'''# 地砖返工1 · 美术交接

## 状态
**最终atlas已落盘，可交给地图/Unity代理导入验证；技术图像检查通过，但不是全部美术验收通过。**

- 工程：`E:/gamejam_wt/tiles_hd`，基线 `1fed22feae2faec7e465c5cacf35b5d7b468e2b0`。
- 交付：`Assets/Pic/Test/basic_ground_tiles.png`，2048×1792 RGBA，8×7，256px/格。
- SHA256：`{sha}`。
- 原始备份、v1备份、所有meta未修改；meta规范化换行后与1fed22f全文相同，sprite身份、PPU、rect、pivot不变。baseline保存的是git show原字节，不以CRLF差异误报身份变化。
- 首次写图前已用git show提取PNG与meta到`../baseline/basic_ground_tiles.png[.meta]`；PNG SHA256 `{extra['baseline_png_sha256']}`。
- 我没有启动Unity、没有修改场景/C#、没有git提交、没有修改主报告。共享工作区的场景/Editor等变化归另一代理；不要回滚它们。

## 真实生成方式
不是纯程序化，也不是整图AI重绘。先读取ComfyUI技能；8188最初拒绝连接，随后发现`E:/GameTools/ComfyUI`已有SDXL和venv，在127.0.0.1:8188启动现有服务（未安装软件）。真实RTX5080上分别完成3个1024×1024 SDXL任务，模型`sd_xl_base_1.0.safetensors`，28步，固定seed 982073/982074/982075。workflow、系统信息、成功history及原图均留存。完成后已停止本代理启动的ComfyUI，释放GPU。

AI图作为中频明度来源，去除结构冲突后周期边缘混合、色板明度重映射；每材质3偏移变体。上叠程序化叶簇、错缝石板/磨边/裂隙/黄铜、沙纹等；按original遮罩投影、光照、接触AO、壳体明暗；道具保留1fed22f造型但重新计算金属距离场反光与玻璃内光。这是**AI + 程序化绘制混合**，不冒称人工手绘。

## A逐项结果与不足
|条款|实际实现|结果/限制|
|---|---|---|
|A1 材质中频|3张真实AI材质，9张1024纹理；域扭曲周期场、叶簇、矿物斑驳、流沙纹|草0/石2/土3实测顶面中频相对标准差分别 {v['rows'][0]['medium_frequency_relative_std']:.4f}/{v['rows'][2]['medium_frequency_relative_std']:.4f}/{v['rows'][3]['medium_frequency_relative_std']:.4f}；草未达到严格15%下界，不写全达标。混合格标准差包含材料差异，不能拿来冒充细节幅度。|
|A2 光照|顶面左上到右下10%场，四边2px柔亮边，正面4px AO，左右84%/67%基础光，向下衰减，6px反光|实际体积增强；深侧面仍偏暗。|
|A3 叶簇荧光|每簇5–9弯叶、3色阶、长度110–220纹理像素；#5CF2D6尖光与柔光；前沿草须垂10px|全图固定荧光代理面积 {v['glow_fraction_opaque']*100:.4f}%（含道具），小于6%；128px仍偏藻毯，叶簇可以继续强化。|
|A4 有机过渡|低频1.35px双轴位移、0.65px滤波/羽化、边缘AO及草须|所有材料评分通过；原遮罩大尺度规律仍在，不宣称5×5没有锯齿或条带。|
|A5 石板|每周期4块错缝大板，暗缝、亮边、风化裂隙、短黄铜镶边、小藤壶|纹理周期边缘裁切会出现部分板片；圆钝/侵蚀质感尚不足。{len(schedule['stone_bearing_cells'])}个石材格中{len(schedule['rune_cells'])}个有微光符文（离散近1/4），见rune_schedule.json；重复拼纯石格会重复符文。|
|A6 沙和立体贝壳|低对比顺流沙纹；保留原小白连通域位置，计算壳面顶左亮/右下暗与放射肋纹|为固定RGB分类保持蓝灰壳色，未豁免贝壳；小壳128px辨形不足，沉积土仍略像泥砾/暗岩。|
|A7 侧面|约3条起伏地层带、少量嵌石、草根曲线、顶部反光/接触阴影|不是水平平直条，但侧纹重复可见，底部较暗。|
|A8 道具|保留形状和alpha，黄铜渐变/窄高光、玻璃内亮外暗/2–4px柔光、基座AO|16格全部重新着色；玻璃细薄形体仍较平，金属形状偏简化。|

## 独立自检（从最终PNG重新读取）
固定1fed22f分类器，未改阈值、未改评分区域、未拿生成标签当预测、未豁免贝壳：默认土；石 `(B-G)<=G*.12`；草 `G>R*1.55 && G>B*1.08`。

- 40/40 Alpha IoU≥0.98，最小 **{v['min_alpha_iou']:.6f}**。
- 40/40全格材质≥0.95，最小 **{v['min_material_accuracy']:.6f}**。
- 40/40顶面材质≥0.95，最小 **{v['min_top_accuracy']:.6f}**。
- 道具alpha与1fed22f逐字节相同：{extra['props_alpha_byte_equal_to_1fed22f']}。
- 原备份/v1/meta保护全部通过；逐格真实值见 `per_cell_validation.csv` 和 `validation.json`；纹理边缘指标见 `supplemental_validation.json`。周期边缘指标不等于视觉无缝。

## C图片（本目录相对路径）
- `materials_comparison.png`：三材质单独对照；`texture_0_0.png`至`texture_2_2.png`：9个1024纹理。
- `comparison_128px.png`、`comparison_256px.png`：新 vs 1fed22f总图。
- `representatives_128.png`、`representatives_256.png`：原尺寸代表格/道具。
- `seam_3x3_cell_0.png`、`seam_3x3_cell_2.png`、`seam_3x3_cell_3.png`：三材质相邻铺排。
- `transition_5x5.png`、`preview_grid.json`：按既有源遮罩邻接选择的混合预览。
- `atlas_final.png`：与正式PNG同哈希留档。
- `visual_review.md`：实际开图观察及未解决不足。
- `ai_workflow_*.json`、`ai_history_*.json`、`ai_system_stats.json`、`ai_output/material_*.png`：真实AI来源。

**以上全部是离线素材QA，不是Unity游戏截图。游戏前后全景/近景、实际地图、自测由地图/Unity代理负责。**

## 复现命令（工程根目录，bash）
```bash
# 若要重新生成AI来源：先启动已有ComfyUI（非Unity），或直接使用已交付的AI PNG
E:/GameTools/ComfyUI/.venv/Scripts/python.exe E:/GameTools/ComfyUI/main.py --listen 127.0.0.1 --port 8188 --output-directory E:/gamejam_wt/tiles_hd/Docs/GameJamIntegration/tiles_hd/rework1/art/ai_output
python Docs/GameJamIntegration/tiles_hd/rework1/art/generate_ai.py
# 可复用固定AI结果，仅执行以下流程即可重建当前atlas
python Docs/GameJamIntegration/tiles_hd/rework1/art/build_rework.py
python Docs/GameJamIntegration/tiles_hd/rework1/art/verify_rework.py
python Docs/GameJamIntegration/tiles_hd/rework1/art/finalize_art.py
```

`READY.json`是资源交接完成标记，不代表美术签收。导入/截图时请再次核对SHA，避免使用制作中间版本。
'''
(A/'HANDOFF.md').write_text(text,encoding='utf-8')
manifest={str(p.relative_to(A)):h(p) for p in sorted(A.rglob('*')) if p.is_file() and p.name not in ['READY.json','manifest.json'] and '__pycache__' not in str(p)}
(A/'manifest.json').write_text(json.dumps({'atlas_sha256':sha,'files':manifest},indent=2))
# Last artifact write: the consumer may now start importing this SHA.
ready={'status':'ready_for_unity_import','atlas_sha256':sha,'atlas_path':'Assets/Pic/Test/basic_ground_tiles.png','technical_image_checks_passed':True,'art_approved':False,'known_visual_limitations':'repeat patterns; grass medium-frequency std below15%; small shells; dark side faces; requires art acceptance','handoff':'Docs/GameJamIntegration/tiles_hd/rework1/art/HANDOFF.md'}
(A/'READY.json').write_text(json.dumps(ready,indent=2))
print(json.dumps(ready,indent=2))
