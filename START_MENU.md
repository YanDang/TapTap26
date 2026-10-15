# 开始界面

## 使用方式

- 发布包以 `StartMenu` 场景启动，随后按需进入 `BiomechanicalCombatScene`；合成与采集的原有入口不变。
- 在 Unity 菜单选择 **Game → Play From Start Menu** 可从开始界面试玩。工具先询问是否保存当前场景，不会自动修改战斗场景。
- 也可打开 `Assets/Scenes/StartMenu.unity` 后按 Play。界面由脚本在运行时生成，编辑模式下场景中只有入口对象。
- 在 StartMenu 对象的 StartMenuController 组件上修改 **Game Title** 可调整标题；Gameplay Scene 保持与构建列表中的战斗场景名称一致。

## 界面与功能

- 采用现有温室插画、深色面板和黄铜色描边，标题使用设计文档中的《生体机械》；带淡入和轻量漂浮光点，无新增美术资源依赖。
- 无有效存档时显示“开始旅程”；有记录时显示“继续旅程”和天数；已完成七日旅程时显示“查看结局”。
- 菜单预览存档只读，不会初始化、创建或覆盖旅程。存储中有旧数据时，开启新局必须二次确认；取消保留原记录。
- 加载使用异步场景切换和进度条，禁止连续点击重复加载。新局重置发生在目标场景准备好之后；场景缺失时显示错误并保留存档。
- 提供触屏操作说明、全局音量增减/静音、退出确认；Esc / 安卓返回键关闭弹窗，在主界面则打开退出确认。
- 使用 1920×1080 参考分辨率、Expand 缩放与安全区锚点，延续项目的横屏配置。
- 不额外添加返回主菜单入口，不改变战斗、合成、采集与结算规则。

## 关键文件

- `Assets/Scripts/UI/StartMenuController.cs`：运行时界面、按钮与加载流程。
- `Assets/Scenes/StartMenu.unity`：发布首场景。
- `Assets/Scripts/Crafting/PlayerSessionData.cs`：只读存档摘要接口。
- `Assets/Scripts/Crafting/Editor/StartMenuEditor.cs`：从开始界面试玩的编辑器菜单。
- `Assets/Scripts/Crafting/Editor/AndroidReleaseBuild.cs`：安卓发布前检查包含四个场景且 StartMenu 排第一。

## 验证范围

- Android 条件 C# 编译、编辑器脚本编译通过；既有 CraftingTestUI 三个未使用字段警告未改动。
- 在独立的临时 Unity 工程中运行真实菜单脚本，检查 16:9、20:9、4:3 渲染，以及操作说明、音量、各存档状态、重新开始取消和加载失败保护。
- 异步继续旅程、确认重新开始、重复点击保护均通过测试目标场景验证；继续保留原天数，重新开始恢复第 1 天、空仓库和 8 点每日精力。
- 隔离工程使用独立 Company/Product，不读取或清除正式游戏存档；场景加载测试使用空的测试目标场景，不代替完整战斗场景联调。
- 渲染预览保存在 Builds/Previews/StartMenu.png，另有超宽屏、平板、操作说明和重开确认预览。
- 仍需在主项目试玩完整的“开始 → 战斗 → 合成/采集 → 保存 → 冷启动继续”流程，并在安卓真机验收安全区与触控。当前机器缺少 Android Build Support，未生成 APK/AAB。
