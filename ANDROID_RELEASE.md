# 安卓打包与上传检查

## 本次适配

- 保持现有 1920×1080 横版设计，仅允许左右横屏旋转。
- Android 不在刘海区域外扩渲染；屏幕空间 Canvas 使用 Expand，保证超宽屏和平板能够容纳设计区域。
- Android 目标帧率 60；双指缩放不再触发玩家单指动作，修复建造触控 fingerId 被误当作数组下标、从 UI 开始拖动后相机误移动的问题。
- Android 使用 IL2CPP，包含 ARMv7 和 ARM64；保留现有最低 API 22、自动目标 API、版本号 0.1 / Version Code 1，未擅自指定发布身份。

## 打包步骤

1. 为项目对应的 Unity **2022.3.62f3c1** 安装 Android Build Support，包含 Android SDK & NDK Tools、OpenJDK。本次检查该编辑器仅有 Windows 构建模块，无法生成 APK/AAB。
2. 打开项目，等待编译完成；Build Settings 选择 Android → Switch Platform。
3. Player Settings → Other Settings：填写正式 Package Name、Version、Bundle Version Code。已有应用必须沿用原包名及签名，并递增 Version Code。
4. Publishing Settings：配置并备份正式 keystore、alias，填写密码。密钥及密码不要提交到仓库；本项目已忽略 *.keystore / *.jks。
5. 检查应用名称、图标；按实际上传渠道要求核对 Target API、包格式、权限和隐私材料。自动 Target API 不代表已经满足平台审核要求。
6. Build Settings 场景顺序为 BiomechanicalCombatScene、Mix、Collect，三个均启用。
7. 使用菜单 **Build → Android → Build Release APK**；渠道要求 AAB 时选择 **Build Release AAB**。入口会检查场景、IL2CPP/ARM64、包名及签名，并强制使用非 Development 构建选项。

## 上传前真机验收

- 16:9、20:9 和平板比例，刘海及手势导航设备：界面无关键内容裁切，左右旋转正常。
- 战斗：点击移动、攻击/防御、滑动翻滚、双指缩放；缩放松手不会移动或翻滚。
- 建造：抽屉拖动不带动相机，选择/放置/回收正常，多指切换无误放置。
- 合成入口：战斗 HUD 左侧点击“背包 / 合成”，不依赖键盘；桌面保留 Tab 快捷键（B 是建造抽屉）。进入合成/采集后入口随 HUD 隐藏，返回后恢复；死亡、结算和场景切换期间不可进入，连续点击不重复加载。
- 合成及采集：打开、点击、拖拽、滚动、返回键和返回按钮，往返场景无残留遮挡。
- 保存：切后台再返回、退出后冷启动，检查背包、家具和旅程进度。
- 使用最终签名包测试全新安装及旧版覆盖安装；核对版本信息及性能。

## 本次验证范围

- 使用本地 Unity 程序集和 C# 编译器，以 UNITY_ANDROID / ENABLE_IL2CPP 条件编译运行时代码，并排除 Editor 程序集引用：通过；仅有 CraftingTestUI 的三个既有未使用字段警告。
- 新增 AndroidReleaseBuild 编辑器脚本独立编译：通过。git diff --check：通过。
- 上述为 C# 静态编译，不等于 Unity Android Player 构建、IL2CPP 原生编译、Gradle 打包或真机验证。

未生成发布包、未完成真机验收前，不应将本次代码适配视为“已可直接上传”。发布身份、签名和渠道要求需由项目发布者确认。
