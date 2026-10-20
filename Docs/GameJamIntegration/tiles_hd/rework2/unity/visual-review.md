# 最终Unity与Player图像复核

本接续执行代理通过vision_analyze实际打开以下最终文件，不把层级计数当成可见性证据。

- `diagnostic-missing-decor/BiomechanicalCombatScene-after-closeup.png`：旧packed版确实不见灯。对应保存场景仅空TilesHD_Decor，装饰对象缺失，而非已证实的渲染遮挡。
- `BiomechanicalCombatScene-after-panorama.png`：可辨6个黄铜/青玻璃灯，主路两侧3对；上、左下、右下3组管道可见。缩小视图不足以逐管段肉眼准确计数，9段由独立序列化+运行时读回证明。
- `BiomechanicalCombatScene-after-closeup.png`：5灯入镜（其中顶部1灯被裁切）；地面平整连续，没有先前多纹理Chunk遮挡/顶面破碎。近景不能要求全地图6灯全部进入相机。
- `selftest/target-after.png`：最终Windows Player framebuffer，完整HUD及Development Build标记；视野可见2灯，当前地面完整，无显著缺格/错误材质。HUD没有为拍摄隐藏；这张不能代替全景的6灯/3组验证。

限制：地面仍有4格周期及明显格状节奏，土偏暗岩、石面较平且局部收边硬；灯玻璃为贴图亮色而非真实灯光，没有证明光照地面效果；道具轮廓简化。角色重叠/复杂附件与混合HUD沿用既有玩法，不擅改。`art_approved=false`，不得以534 PASS替代美术验收。

实际GPU见runtime-after.txt，PlayMode相机RenderTexture截图1920×1080（不包含overlay Canvas）与Player framebuffer是不同证据层级。当前运行时地面合并到8192×8192未压缩SpriteAtlas；装饰仍引用原主PNG，未为修复改变其排序层/顺序。未做显存/性能量化或移动平台验证。
