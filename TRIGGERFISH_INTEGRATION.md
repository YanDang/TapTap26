# 扳机鱼敌人接入说明

日期：2026-10-14。目标工程：TapTap26。

## 来源与资产

- 源工程：`E:\Develop_Game\Spine_temp\扳机鱼`。源工程文件未修改，也未复制 Library、ProjectSettings、测试输入脚本或 UOS 插件。
- 实际格式是 Unity 2D PSD Importer／SpriteSkin 骨骼，不是 Spine 导出数据；沿用目标工程现有包，无需安装另一个运行库。
- 原图与绑定：`Assets/Art/Enemies/Triggerfish/Triggerfish.psb`，保留源 PSB 与 meta GUID；PSB 的 SHA256 与源文件一致。
- 动画目录：`Assets/Art/Enemies/Triggerfish/Animations/`，包含 Idle（2 秒循环）、Move（1 秒循环）、Attack（1 秒非循环）及 Triggerfish.controller。
- 战斗预制体：`Assets/Resources/Enemies/Enemy_Triggerfish.prefab`。
- 动画桥接：`Assets/Scripts/Enemies/TriggerfishEnemyAnimation.cs`。

## 如何在游戏中遇到

保持当前战场开局没有预置敌人的设计。首次开启一扇尚未通关的采集门时，生成数量仍为 1～3：第 1、3 只是扳机鱼，第 2 只是原章鱼。原章鱼资源和生成接口均保留，不修改已通关门的状态，也不会重置玩家存档。

已经通关／当天已使用的门不会因为这次更新重新刷怪；测试新敌人时使用未通关的新门，或通过下面的运行时接口生成。直接在正式战场预摆活动敌人仍会被 BiomechanicalCombatAdapter.Awake 清理。

运行时接口（在当前战场初始化之后调用）：

```csharp
EnemyController enemy = EnemyController.CreateTriggerfishInstance(
    spawnPosition, "Enemy_Triggerfish");
```

这会加载已接好动画的 Prefab，注册到 AllEnemies，启用追击，并关闭自动复活。原 `CreateEnemyInstance` 仍生成章鱼；资源意外缺失时新接口警告并回退章鱼，避免门试炼无法完成。

## 战斗与动画规则

- 扳机鱼是新的普通近战敌人，不是 Boss；共用 EnemyController 的寻路、HP、破防、受伤、家具控制、格挡、处决与死亡清理。
- 采用默认 100 HP、100 破防值、15 攻击伤害；攻击在第 0.38 秒判定一次，与源 Attack 的突进关键帧对齐，弹刀窗口 0.04～0.30 秒。
- 桥接现有 Speed 参数与源控制器的 IsMoving 参数；处理攻击被打断后的返回待机，并同步建造／工坊暂停、麻痹、破防、Pending 和 Executing 状态。
- 源工程没有提供受伤、死亡、抓取动画。受伤使用现有闪白／击退，死亡使用现有淡出／销毁，不宣称已经有专门的受伤或死亡动画。
- 禁用该敌人的抓取和时钟 QTE，避免播放缺失动作；普通格挡后的处决仍保留普通敌人规则。
- 新增 `EnemyController.IsParalyzed` 只读查询供动画同步使用，不改麻痹本身的结算。

## 导入时的必要适配

- 从源 SampleScene 的扳机鱼实例恢复 73 项骨骼 Transform 配置，但不导入附着的测试摄像机或 TriggerfishAnimationTest，避免它响应玩家方向键／空格／鼠标。
- 模型独立放在 VisualRoot 下，保留 SpriteSkin 骨骼层级；统一大小并校准模型朝向、脚底与阴影。
- 三段源动画包含录制时的根坐标（约 -7.963、-6.031）。导入副本已扣除这一基准，保留浮动与突进偏移；动画不再驱动战斗实体根位置。
- Attack 的横向偏移已按模型实际朝向校准。攻击入口过渡设为 0 秒并禁止 AnyState 自跳转，以免偏移已约定的命中时点。
- 七个 SpriteRenderer 使用项目现有 SpriteBillboard 材质，以兼容受击闪白和战斗预警；不修改共享材质资产。

## 验证记录

- Unity 脚本编译通过，最终控制台无错误／警告。
- 三个 AnimationClip 的所有绑定路径都能找到对应 Transform；运行时 7 个 SpriteSkin 均有根骨、完整骨骼引用和变形顶点数据。
- 实际 Animator 检查：待机骨骼变化、游动状态切换、左右朝向、攻击播放、受伤打断返回 Idle；建造暂停和麻痹下动画时间不推进。
- 调用真实攻击判定：一次攻击扣 15 HP，重复判定不再扣血；真实 AI 能追击移动。
- 减速 40%、破防至 0、硬直恢复、普通格挡进入 Pending、处决后销毁均通过。
- 实际门试炼生成 2 只扳机鱼 + 1 只章鱼；击杀其中一只不会提前通关，全部击败后敌人注销并正常开放温室。
- 工坊叠加／返回不改变扳机鱼 HP 与位置；全部通关后进入温室生成资源，返回后门仍按每日限次关闭，不能重开。
- Play Mode 截图检查模型显示；测试结束退出 Play，两项 PlayerPrefs 原始存在状态与内容逐字恢复，后台运行设置恢复，正式场景未产生修改。

验证使用 Unity MCP 调用运行接口、反射断言及画面检查；未进行独立 Player 打包、手机触摸实测或长期平衡测试。

