# 返工2 Unity最终交接

## 结论
**最终技术收口通过：装饰可见性修复；新Rework2 Windows构建成功；真实Player自测534 PASS / 0 FAIL。美术未签收。**
仅 `E:/gamejam_wt/tiles_hd` / `feature/hd-ground-tiles`，基线879645d。本代理未commit/push/stage，未改主报告，未操作main；Unity执行权已释放。

## 灯/管道消失的调查和修复
- `diagnostic-missing-decor/scene.unity.txt` 证明接手时只有空 `TilesHD_Decor`，15个灯/管道对象实际缺失，不是仅凭截图认定排序遮挡。该目录保存当时场景、图片、snapshot和结果。
- 重铺器原 `if (!tile || !sprite) return` 会跳过登记，随后清理未使用子对象。现在在SpriteAtlas打包后重新加载全部56个源Tile，缺Tile/Sprite即抛异常，禁止静默跳过后删除；`RecordMap` 追加运行时6灯/9管道、启用/有效Sprite/排序契约。
- 从前代理已有 `repaint-final-first.unity.txt` 恢复装饰存在的场景快照，保留对象身份；此后经过最终repaint和独立保护验证。没有修改旧Tile/Palette、主PNG/meta、变体PNG、玩法源码、碰撞、角色/门位置或装饰布局/排序/材质。
- `decor-source-references-first.txt` 和 `decor-source-references.txt` 本次打包前后引用均有效，**首次对象失效的具体底层触发未重现**。不能写成已经证明“Pack一定卸载Tile”；已证实对象缺失、静默删除风险、恢复后结果及重复稳定性。
- 最终地面仍用新 `TilesHDVariants.spriteatlas`，运行时1张8192×8192未压缩texture；装饰仍引用原主PNG。没有通过改变全场景排序掩盖问题。

## 本次最终实际命令和结果
在工程根执行：
```bash
python Docs/GameJamIntegration/tiles_hd/rework2/unity/run_editor.py TilesHD.RepaintMap.Repaint repaint-final
# 将此时场景逐字节保存为repaint-accepted.unity.txt
python Docs/GameJamIntegration/tiles_hd/rework2/unity/run_editor.py TilesHD.RepaintMap.Repaint repaint-final-repeat
python Docs/GameJamIntegration/tiles_hd/rework2/unity/run_editor.py TilesHD.RecordMap.After after-final
python Docs/GameJamIntegration/tiles_hd/rework2/unity/run_editor.py TapWindowsBuild.Build build -tapbuildpath Builds/Rework2/Windows/TapTap26.exe
python Tools/run_tap_selftest.py --exe Builds/Rework2/Windows/TapTap26.exe --output Docs/GameJamIntegration/tiles_hd/rework2/unity/selftest --timeout 300
python Docs/GameJamIntegration/tiles_hd/rework2/unity/verify_scene.py
python Docs/GameJamIntegration/tiles_hd/rework2/unity/verify_integration.py
python Docs/GameJamIntegration/tiles_hd/rework2/unity/restore_environment.py
python Docs/GameJamIntegration/tiles_hd/rework2/unity/closeout.py
```
- before由前代理在本次返工2中对879645d真实录制，未以返工1图片替代。本接续没有重跑before。
- repaint-final/repeat/after-final/build/Player均exit=0。repeat `changed=0` 且保存场景与 `repaint-accepted.unity.txt` 字节完全相等；最终after/build后仍相等。
- 新构建日志：`[TapWindowsBuild] Succeeded errors=0 warnings=0 bytes=680228075`。独立目录 `Builds/Rework2/Windows`，非旧构建。
- 新自测：`RESULT passed=534 failed=0`。严格closeout所审final日志没有CS编译错误、Exception、FAIL或失败测试；历史诊断保留，不作为最终门槛日志。
- `verify_scene.py`：120受保护序列化记录，唯一允许变化为Tilemap内容；698格占用、origin/size、color/matrix/flags无变化；173→167总记录对应移除2个DoorLamp的6条记录。
- `verify_integration.py`：640新静态Tile、16坐标变体、698格选择正确；6灯3对，沿L路弧长间隔7、等宽对称；9管道及有效renderer；392原资产/源码保护通过、17张生产PNG与READY哈希一致。
- `closeout.py` 保留严格门槛并增加装饰运行时/序列化检查；`closeout-result.json` 为最终机器判定，`tested-artifact-hashes.json` 覆盖生产图、scene、Editor脚本、新Tile/meta/atlas、构建全部文件（含Assembly-CSharp.dll和sharedassets）、截图/日志/验证脚本等。

## 图像实看与限制
- after全景可辨6灯与3组管道；近景可辨5灯（顶部1灯裁切），地面连续，不见旧多纹理Chunk破碎。
- `selftest/target-after.png` 为新Player完整HUD framebuffer，可见2灯和完整地面；与PlayMode相机RenderTexture（1920×1080、overlay UI不含）分开表述。详细观察见 `visual-review.md`。
- 不签美术通过：仍有4格周期、地面格状节奏、土偏暗岩、石路局部硬边/细点、道具简化；灯只是贴图亮色，不冒称真实照明。atlas较大且未做显存/性能或移动平台测试。全部美术条款见 `../art/handoff.md`，主报告由父代理追加。

## 存档、版本恢复、进程
- Editor原匹配前缀2项、Player原0项；Editor/Player退出前自身恢复外部比对及supervisor退出后raw恢复均true。没有以空Player存档声称非空真实玩家存档覆盖；未另跑baseline/boss或强杀恢复。
- 实测Unity **2022.3.57f1 (9d8bd91adc0a)**。已保存 `tested-ProjectVersion.txt`、`tested-packages-lock.json`、`tested-Mix.unity`；只恢复可证实自动变化的ProjectVersion和lock，Mix本来等于基线未重写。`automatic-files-restored.json` 比对initial manifest+git show，全部restored_matches_initial=true。
- 恢复声明 **2022.3.62f3c1** 后没有重建，不宣称恢复环境也通过。
- 最后 `process-audit.json` 仅既有mcp-for-unity服务；无本工作区Unity.exe/TapTap26.exe/UnityCrashHandler64.exe残留。所有本次supervised job退出0（proc_775f334e1a63、proc_ab88cdc1d377、proc_b0ee114678da）。未停止他人进程。
- `.vsconfig` 和 `Tools/__pycache__` 保留原样，不提交。当前分支跟踪 `origin/integrate/claude-boss-ult-gear`，**父代理不得裸push**。

## 关键SHA256
- 主PNG：`9cfa33cea6c5c266849121adc2baa2aa9612cedc9282e578cf62bbd718664920`
- 场景：`2207f8c305477664adf95387720248c1f46ddc8fdf33dfbb3754663704fed01a`
- 新SpriteAtlas资产：`28129c5175706dcf41bdcfd59e75d8ca1e0519e794a799ee6e23ebb34b956056`
- Player exe：`2c392ecb0dac1e2e5211b488220e26b6c30dfde366d10ccfeb938cf572b856dc`
- Assembly-CSharp.dll：`2dd27e1253fa03717273a9765a2726b7161cb386f43314be3f5cd996cf222c87`
- 自测文本：`5447e02301b61994671441684cdba9297fa74ca0ffac5d7b8ea9a958bff43b01`
- Player最终截图：`b3d1937ea6c48688837555fbeea8f0ca64511d8ec9902f6c73f148cdad98fab3`
