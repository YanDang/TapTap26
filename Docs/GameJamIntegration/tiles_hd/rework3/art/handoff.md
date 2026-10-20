# 返工3贴图条目1：美术资产交接

## 结果与精确范围
- 基线：`e07520f16254b12ac7d4625c36209fc334e08650`。
- 实际修改生产文件仅 `Assets/Pic/Test/basic_ground_tiles.png` 与 `Assets/Pic/Test/TilesHDVariants/variant_00.png`–`variant_15.png`，共17张。全部2048×1792、RGBA；主图与variant00文件哈希完全相同。
- 主图 SHA256：`edfd979538fb35685bbfdaa5f2e74fb1aefc9da497352548b058aec3e8e2fc3b`。完整生产文件哈希见 READY.json。
- 仅0–39格可见顶面RGB发生变化；原顶面定义保持 `u,v∈[-.012,1.012]`，并裁到基线alpha>0。原逐格光照 `1.015-.03*(.45*X/256+.55*Y/128)` 改为恒定1.0，方向渐变振幅为 **0%**。浮点阶段重新采样/合成，不是对已量化PNG粗暴反除。
- 所有alpha、所有侧面、完全透明像素RGB、40–55道具全部RGBA逐像素不变；全套meta及备份未改。材质分区、叶片、错缝、贝壳、符文位置/颜色、玻璃与金属全部沿用。
- 实际每张变动像素数 380598–383779，最大单通道差 **2/255**；非授权像素变化数 **0**。本次是小修而非材质重做。

## 顶边高光：必须说明的基线差异
返工1生成器有约2px、1.055倍的菱形边高光，但**返工2实际生成器没有沿用这段代码**。已完整重放返工2并逐像素匹配e07520f的17张生产图，证明不是把历史实现误认成当前实现。
因此当前独立菱形顶边高光有效透明度是0，本次设定边宽上限1px、有效透明度仍为 `0×0.5=0`，**没有凭空补画亮边，也不能声称对一个现存非零高光做了50%削弱**。石板内部3px磨边属于已通过的材质细节，不是格子顶边，严格保持。若验收要求非零1px高光，应由主负责人明确后另做；本次以不新增菱形边框、保留已通过材质优先。

## 实测自检
- 先在本目录reproduction中重放返工2生成器，主图及16变体全RGBA与e07520f逐像素一致：`reproduction_validation.json`。
- 最终PNG重新打开，固定返工2反分类器、阈值和评分区，640格无豁免：alpha IoU最小 **1.000000000**，全格材质一致率最小 **0.981104414**，顶面一致率最小 **0.960246294**。全部通过原 .98/.95/.95 门槛。
- 40种地面每种仍有16个不同像素哈希。该项不是视觉无重复保证。
- 材质源图、stone_4x4、三张texture及rune_schedule文件与返工2SHA完全相同。
- 全部17张alpha逐字节不变；全部17张40–55道具RGBA不变；非授权区域RGBA零变化。meta/备份共22文件实际磁盘SHA检查通过（额外保留原严格QA的换行规范化检查）。
- 证据：`validation.json`、`per_cell_validation.csv`、`scope_validation.json`、`protected_live_hashes.json`、`lighting_contract.json`。

## 总图、对照与目视复核
- `atlas_final.png` 为完整透明地砖总图，与主图像素一致。
- `comparison_128px.png` / `comparison_256px.png`：左e07520f、右返工3，总图对照；另有representatives。
- `comparison_mosaic_cell_0/2/3.png`：草/石/土实际坐标选变体8×8对照；`variants_3x3_*`、`variants_8x8_*`、`transition_5x5.png`保留离线检查。
- 已实际开图检查总图128px及三个材质8×8对照：未见新增菱形亮框，材质及道具外观保持；变化很小，符合最大RGB差2的实测。草仍有部分格缝和叶簇周期、石/土有原纹样重复；不夸大为完全消除所有格子感。
- 上述均为 **离线CPU资产预览，不是游戏截图**；未承诺“游戏近景眯眼完全看不到菱形格子”，该项须结合世界光照和地图截图由另一位负责人验收。

## 并行地图负责人契约
- 尺寸、PPU256、pivot(0.5,0.25)、变体命名和选图规则不变：`positive_mod(cell_x,4)+4*positive_mod(cell_y,4)`。
- 4×4周期只用于原材质坐标与石板接缝，**绝非大尺度世界光照**。本任务不烘焙任何世界光。地图负责人需在真实世界坐标上提供跨格连续、尺度明显大于4格的光照场；此处未验证该功能。
- 路缘修改、全景/近景游戏截图、Unity自测、主报告与提交全部由主代理/地图代理负责。
- 本代理未启动Unity，未改C#、scene、主报告、历史rework2证据、meta及备份；未commit/push。指定未跟踪.vsconfig、Tools/__pycache__、p3-audio-readback.txt未动。

## 复现与导入门禁
工程根顺序运行 `python -B Docs/GameJamIntegration/tiles_hd/rework3/art/build_art.py`、`verify_art.py`，实际开图确认后运行`finalize_art.py`。build先删除本目录READY；重放及所有新脚本/证据只写本目录，最终17张生产PNG按授权更新。
导入前逐个核对READY.production_pngs的SHA256；READY另绑定handoff、QA和manifest。技术图像检查通过不等于完整美术/运行时签收。
