# 返工2美术交接（仅离线资产，不签全部美术通过）

## 稳定产物
- 主图 `Assets/Pic/Test/basic_ground_tiles.png`，SHA256 `9cfa33cea6c5c266849121adc2baa2aa9612cedc9282e578cf62bbd718664920`。
- 16张 `Assets/Pic/Test/TilesHDVariants/variant_00.png` 至 `variant_15.png`，全部2048×1792、RGBA、8×7、56格256px。主图等于variant00；后16道具格一致，前40格各有16个不同PNG像素内容。
- **必须按 `variant=(positive_mod(cell_x,4))+4*positive_mod(cell_y,4)` 选择。** 石板基于2048方形世界布局按512区块采样，不能随机选，否则石缝连续性会丢失。土也按连续世界坐标取样，草在方形空间旋转/偏移。详见variant_contract.json。PPU256、pivot(0.5,0.25)，原meta绝未修改。
- PNG总量14194461字节。各生产PNG哈希在READY.json；其他离线证据在manifest.json。
- 本代理没有启动Unity、没有改场景/C#/主报告/备份/meta，没有commit/push。

## 真实来源与制作
复用返工1真实ComfyUI/SDXL原材质，原始图片、workflow、successful history与system stats原样复制到本目录。**本次没有重新提交AI生图任务**。SDXL纹理仍提供真实中频/低频表面明度：草改较稀疏7叶簇，石重新在方形大世界纹理绘制跨格错缝，土去壳点并平滑细碎沙纹；非整张AI生成，不冒称纯手绘。

879645d的PNG/meta/原备份通过git show在第一次正式写图前提取至art/baseline，后续重跑不覆盖immutable基线。不是复制并发变化的现图。

## 返工2逐条
1. 普通土（尤其纯土3）完全不叠原白点壳。仅18–27的原白点小连通域画有阴影/肋纹的小壳。16变体且无1/2格完整图案周期；**仍有4格周期**。最终土8x8无明显白点和块状边，偏软/暗岩。
2. 石基色#6D8186乘0.9、低对比真实SDXL表面；在2048世界方形先绘大板、磨边/划痕再裁，每格不画闭合菱形缝。符文按约1/6的(变体,格)稀疏计划、随机安全位置、RGB降为(31,116,112)，没有全图同一位置强亮标。旧云噪削弱，仍有细点状接线及4格重复，不能写完全无缝。
3. 保留原石土过渡遮罩和碎石格，遮罩轻扰动/羽化；提供5x5混合预览。场景主路实际选过渡格由Unity代理负责，不能以离线组合冒充地图验收。
4. 草色采用HSV饱和*.75/value*.8处理新叶簇；实测纯草0顶面相对879645d明度降低22.94%，HSV饱和降低26.16%。极少叶尖亮点，仍有128px下叶簇辨识弱/细格缝问题。角色最亮排序需要引擎截图验证。
5. 玻璃中心#7FF5E6→边缘#126A66距离径向场；外缘细青线和孤立点抑制，玻璃局部高斯外光半径10px、混合上限18%，无全框发光；黄铜上左窄高光。最后实际开图看到金属/玻璃分离改善，但玻璃更像发光液体，支架形体简化。
6. 灯3对、上半部草岛扩大属于地图代理；未擅自修改场景。

## 固定独立图像QA
- 16×40=640格，Alpha最小1.000000，全格材质最小0.981165，顶面材质最小0.959888，全通过原.98/.95阈值。
- 从最终PNG重新反分类；固定1fed22f/879645d规则与评分区域，无壳豁免、无改阈值。
- 原meta、original/v1及meta保护通过；meta仅换行规范化比较，未修改原字节。道具alpha与879645d逐字节一致。
- 每格数值per_cell_validation.csv；validation.json；supplemental_validation.json。技术合格不等于美术签收。

## 实际开图证据
comparison_128px / comparison_256px（879645d vs新）、representatives_128 / 256、materials_comparison、variants_3x3_cell_0/2/3、variants_8x8_cell_0/2/3、transition_5x5。`diagnostic_single_8x8_*`是故意重复单张的反例，不是变体验收图。完整观察见visual_review.md。全部是离线CPU图，不是游戏截图。

## 复现
工程根运行 `python Docs/GameJamIntegration/tiles_hd/rework2/art/build_art.py`，然后verify_art.py；实际开图复审后再运行finalize_art.py。build自动删除旧READY以免中间图误导入。最终READY写在所有生产/验证文件稳定之后。
