# Boss 战斗接入交接文档（给策划 Agent）

更新日期：2026-10-13。核对基线：TapTap26，Git 提交 `8473b83`，Unity `2022.3.62f3c1`。
工程根目录：`E:\Develop_Game\TapTap26`。文中的代码路径均相对工程根目录，便于交接到其他工作目录。

> 本文描述当前项目的真实接口，不是已经实现的 Boss 系统。当前没有 Boss 专用控制器、战斗胜负管理器或通用伤害接口。本次仅新增说明文档，不改运行代码。下文“建议新增”均不是可以直接调用的现有 API。

## 1. 先读结论：推荐如何接入

1. 保留 Boss 根物体上的 `EnemyController`，作为现有玩家、家具、炮台、光环识别的敌人实体。不要只写一个独立 `BossController : MonoBehaviour`，就期待玩家能自动锁定和攻击它。
2. 先做复用普通敌人逻辑的原型，再通过小范围扩展 `EnemyController` 接入 Boss 技能调度、受击策略、处决规则和死亡通知；独立 `BossBrain` 负责阶段与技能，不重复维护第二套真实血量。
3. 现有关键方法不是 `virtual`。不要用子类里的 `new TakeDamage` 或同名方法伪装覆盖；玩家和家具持有 `EnemyController` 引用，仍会调用原实现。
4. 必须先处理“完美格挡／时钟 QTE → 处决 → 直接清零血量”的规则，否则高血量 Boss 仍可能被一次处决秒杀。关闭时钟 QTE 不等于关闭普通格挡处决。
5. 当前战场的 `BiomechanicalCombatAdapter.Awake()` 会销毁找到的所有活动 `EnemyController`。不要直接在正式场景预摆活动 Boss；应在适配器初始化完后，由遭遇触发器运行时生成。
6. 世界暂停只交给 `GameJam.TimeManager`；Boss 技能、子弹和计时额外遵守建造暂停及覆盖场景输入锁，不能只看 `Time.deltaTime`。

## 2. 工作范围与不可破坏的既有功能

- 正式场景：`Assets/Scenes/BiomechanicalCombatScene.unity`、`Assets/Scenes/Mix.unity`、`Assets/Scenes/Collect.unity`；当前 Build Settings 仅启用这三个场景。
- 当前操作分工：B 打开建造抽屉；Tab 进入工坊；空格格挡；F 与附近采集门交互。Boss 不应另占这些键或恢复旧输入逻辑。
- 当前导航：战场 ↔ 工坊；战场 → 通关后的门 → 温室 → 战场。不恢复工坊到温室的按钮。
- 每扇采集门每个游戏日使用一次；门之间资源独立，返回后当天关闭。Boss 入口／胜利奖励不应直接调用 `TryOpen("Collect", ...)` 绕过门记录。
- Boss 是否禁止建造、是否允许进工坊、逃离是否重置，需要策划决定并单独实现遭遇规则，不得擅自破坏现有导航和存档。
- 建议新增代码放 `Assets/Scripts/Boss/`，预制体放 `Assets/Prefabs/Boss/`，测试场景放 `Assets/Scenes/BossTest.unity`；这些目录／场景是建议，当前未为 Boss 创建。
- 改共享类时保持普通怪默认行为；不重生成整个正式战场，不覆盖现有场景、材质、Prefab 的 GUID。提交资源时带上对应 `.meta`。
- `combat_system_integration_spec.md` 是历史合成／构装接入说明，部分路径、快捷键和时间管理描述已过时；以当前源码、本文和 `CURRENT_VERSION_ROADMAP.md` 为准。

## 3. 源码导航

| 源码路径 | 应重点阅读的成员／职责 |
| --- | --- |
| `Assets/Scripts/EnemyController.cs` | 敌人注册、生成、AI、受伤、破防、格挡、处决、抓取、状态效果、死亡复活 |
| `Assets/Scripts/PlayerController.cs` | 玩家锁定与普攻、蓄力重击、格挡处决、受伤、抓取与失败复活 |
| `Assets/Scripts/Core/TimeManager.cs` | 世界时间、慢动作、顿帧、覆盖场景暂停 |
| `Assets/Scripts/IsometricPathfinder.cs` | 地图坐标换算、寻路、家具动态占格 |
| `Assets/Scripts/FurnitureObject.cs` | 家具受伤／反伤通知、推撞伤害、目标合法性 |
| `Assets/Scripts/Auras/` | 水、电、麻痹、减速、安抚、嘲讽与现有水刃弹道 |
| `Assets/Scripts/Crafting/BiomechanicalCombatAdapter.cs` | 开局清理敌人、生成默认门、构装实例化 |
| `Assets/Scripts/Crafting/GameplaySceneTransition.cs` | 工坊／温室叠加加载、返回与镜头恢复 |
| `Assets/Scripts/Crafting/PlayerSessionData.cs` | 输入锁、跨场景背包／成品／采集门数据 |
| `Assets/Scripts/UI/ClockQTE.cs` | 单一活动时钟 QTE |
| `Assets/Scripts/UI/HUD.cs` | 玩家格挡／闪避 HUD；不是 Boss 血条 |
| `Assets/Scripts/Core/Vfx.cs`、`Assets/Scripts/Core/Sfx.cs`、`Assets/Scripts/CameraController.cs` | 特效、音效和镜头反馈 |

核心类型 `EnemyController`、`PlayerController`、`FurnitureObject`、`IsometricPathfinder` 在全局命名空间；`TimeManager`、`Vfx`、`Sfx`、`HUD`、`ClockQTE` 在 `GameJam`；`CraftedProduct` 和 `CraftedProductStats` 在 `BiomechanicalCrafting`。

## 4. 敌人生成、识别与生命周期

### 4.1 现有生成接口

`EnemyController.CreateEnemyInstance(Vector3 position, string enemyName = "Enemy_Squid")` 返回完整的普通敌人实例，自动创建运动学 `Rigidbody2D`、`CircleCollider2D`、`VisualRoot` 和章鱼外观，并将 `autoRespawn` 设为 false、初始状态设为 Chase。

注意：这不是按名称加载任意怪物 Prefab 的工厂，`enemyName` 只是物体名称；传入 Boss 名称不会自动加载 Boss 美术。

`Assets/Prefabs/Enemy_Squid.prefab` 与 `Assets/Resources/Enemy_Squid.prefab` 均存在。正式 Boss 推荐制作独立 Prefab 并使用序列化引用，不直接修改这两个普通怪资产。

### 4.2 只用于验证战斗接线的原型示例

将此组件放到测试场景的遭遇触发物体上。代码仅示范启动时生成一次；正式版应由范围触发／剧情流程调用，禁止在 Update 中重复生成。示例数值是测试值，不是平衡结论。

```csharp
using System.Collections;
using UnityEngine;

public sealed class BossPrototypeSpawner : MonoBehaviour
{
    public Transform spawnPoint;
    private EnemyController boss;

    private IEnumerator Start()
    {
        yield return null;
        var pathfinder = IsometricPathfinder.Instance;
        if (spawnPoint == null || pathfinder == null) yield break;
        var cell = pathfinder.WorldToCell(spawnPoint.position);
        if (!pathfinder.IsWalkable(cell)) yield break;

        boss = EnemyController.CreateEnemyInstance(
            pathfinder.CellToWorld(cell), "Boss_Prototype");
        boss.maxHp = 1500f;
        boss.currentHp = boss.maxHp;
        boss.maxGuardBreak = 300f;
        boss.currentGuardBreak = boss.maxGuardBreak;
        boss.autoRespawn = false;
        boss.moveSpeed = 1.1f;
        boss.attackDamage = 25f;
        boss.normalImpact = 1.0f;
        boss.windowStart = 0.15f;
        boss.windowEnd = 0.8f;
        boss.grabChance = 0f;
        boss.enableClockQTE = false;
        boss.showAttackRange = true;
        boss.UpdateHealthBar();
        boss.UpdateGuardBar();
    }
}
```

**该原型仍使用普通怪 AI，也仍可被普通格挡后的处决秒杀；不能当作最终 Boss 交付。** 首帧等待仅规避当前主场景启动清理；若后续动态添加新的适配器，它仍会清理场上敌人，不能再重复创建。

### 4.3 生命周期与注册约束

- `Awake()` 将当前 HP／破防值重置为最大值，创建血条、选中圈、预警圈，查找寻路和玩家；还会将 detectRange 至少设为 14、chaseGiveUpRange 至少设为 18。
- `Start()` 会把非 Chase 状态改回 Patrol。不要指望在此前直接指定一个复杂技能状态就能保留。
- `OnEnable()` 自动加入 `EnemyController.AllEnemies`；`OnDisable()` 和 `OnDestroy()` 移除。禁用 EnemyController 来“关闭普通 AI”会使它从玩家／家具的敌人列表消失，不是可用的 Boss 接法。
- `IsAlive` 同时检查 currentHp > 0 与内部死亡标志。死亡后直接把 currentHp 加回来，不会完整复活；不要绕过现有生命周期。
- Boss 和召唤物一般设 `autoRespawn = false`；普通字段默认值是 true，复制 Prefab 时必须核对。
- 玩家普攻／重击／锁定、炮台和多数光环都依赖 AllEnemies；水刃和部分碰撞技能用 GetComponent 或 GetComponentInParent 查 EnemyController。受击碰撞体应能找到同一个根实体，不要给每个部位各挂一份 EnemyController。
- 当前鼠标／触摸选敌使用根位置距离（FindEnemyNear，调用半径 0.95），玩家攻击也大量按根位置距离判断。单纯把 Boss 图片放大不会扩大可点选／近战受击区域；大型 Boss 的受击半径、多部位命中需要额外适配。

### 4.4 替换 Boss 美术

EnemyController.Awake 默认会 AttachSquid，且可能关闭 VisualRoot 下其他子物体。定制 Prefab 应预先设置 `autoAttachSquid = false`，配置 `visualRoot`、`monsterRenderer`、`enemyAnimator` 等引用。运行时 AddComponent 后再关闭 autoAttachSquid 已经晚于 Awake。

可复用 `SetVisualFacing(bool)`、`SetMoveAnimation(bool)`、`SetVisualColor(Color)`、`SetVisualAlpha(float)`、`Flash(Color, float = 0.12f)`、`RegisterExtraRenderers(IEnumerable<SpriteRenderer>)`。现有 Animator 使用 Speed 浮点参数与 Attack 触发器，不代表已支持任意 Boss 动画事件。

## 5. 伤害、格挡、破防与控制接口

### 5.1 玩家／家具对 Boss 造成伤害

| 真实接口 | 当前行为／使用约束 |
| --- | --- |
| `enemy.TakeDamage(float damage, float guardBreakDamage, Vector3 attackerPos)` | 完整受击入口，联动闪白、击退、AI 反应、血条、破防、飘字与死亡 |
| `enemy.TakeDamage(float damage, Vector3 attackerPos)` | 默认附带 30 点破防；纯伤害请显式使用三参数版本并传 0 破防 |
| `enemy.IsAlive`、`IsStaggered`、`IsAttacking` | 查询当前状态；IsAttacking 包括普通攻击、抓取前摇和抓住玩家 |
| `enemy.TriggerStagger()`／`RecoverFromStagger()` | 进入／解除内部硬直；解除时恢复满破防值并刷新表现 |
| `enemy.UpdateHealthBar()`／`UpdateGuardBar()` | 初始化或调整属性后刷新现有小血条／破防条 |

必须使用正向、有效的伤害数值。TakeDamage 不负责拒绝负数、NaN 等错误输入；不要把它当作治疗接口。attackerPos 是攻击来源世界坐标，用于击退方向及转火推断，不是命中点／敌人坐标的随意占位。

当前语义：currentGuardBreak 从 maxGuardBreak 向 0 扣减；减到 0 后触发硬直，staggerDuration 控制恢复时长。硬直或 Pending 状态的受击伤害乘 1.5。每次受击还可能把 aiState 改成 Chase、关闭攻击 QTE、解除安抚并产生击退；只增大 Boss 的破防条并不能获得“霸体不打断”。

`IsStaggered` 来自私有布尔值，单独把 aiState 写成 Stagger 不等于进入真实硬直。不要在阶段切换时直接修改 currentHp 或每帧回填血量抵消伤害，这会与处决、血条、死亡判定产生冲突。

### 5.2 Boss 对玩家造成伤害

| 真实接口 | 当前行为／限制 |
| --- | --- |
| `player.TakeDamage(float damage)` | 检查无敌、取消放置、处理格挡和受伤、扣血，HP 归零触发当前复活流程 |
| `player.IsAlive`／`Dead` | 血量状态；调用伤害前自行检查目标仍活着 |
| `player.IsInvulnerable` | 翻滚、临时无敌、被抓／被抛等状态影响；不要绕过 |
| `player.IsGuarding` | 缩壳格挡窗口 |
| `player.DoParry(EnemyController attacker)` | 完美格挡：通知指定敌人 Parried、设置目标、触发慢动作和反馈 |
| `player.Heal(float amount)` | 现有玩家回血入口，不是 Boss 治疗入口 |

**重要限制：玩家 TakeDamage 没有 attacker、damageType、canBlock 参数，也不返回实际命中结果。** 玩家正在格挡时，它会自行寻找附近可格挡／最近敌人并 DoParry；Boss 的远程弹道、场地伤害可能因此错误地让另一只怪进入 Pending。

- 当前普通怪近战在命中处知道攻击者身份，并会先用 player.DoParry(this) 处理格挡。
- 新做 Boss 远程技能、不可格挡攻击、环境伤害时，应先扩展玩家统一伤害入口，明确来源、可格挡／可闪避语义和处理结果，再保留旧重载兼容普通怪。不要直扣 currentHp 绕过这一问题。
- 一次挥击／爆炸用“本次技能已命中的目标集合”去重，多 Collider 只伤害一次；持续伤害使用明确的 tick 间隔，不要 OnTriggerStay 每帧扣血。

### 5.3 格挡、QTE 和处决：Boss 上线前必须改造

实际调用链：

- 普通弹刀：`PlayerController.TriggerShellGuard()` → `FindParryableEnemy()` → `DoParry(enemy)` → `enemy.Parried()` → Pending。
- 时钟 QTE：`ClockQTE.Show(EnemyController owner, float duration)` 创建时钟；玩家 TryHit 成功后直接 ExecuteEnemy(owner)。全场只有一个 `ClockQTE.Current`，新时钟可能关闭旧时钟。
- 处决：`player.ExecuteEnemy(enemy)` → `enemy.BeginExecuted()` → `enemy.KillByExecution(Vector3 dir)` → currentHp = 0 → Die。

ExecuteEnemy 入口只检查敌人非空且活着，不自动限制其必须处于 Pending；KillByExecution 也没有 Boss 抗性。不要只在自己的 BossBrain 中写 canExecute=false——现有调用链根本不会读取这个新字段。

正式 Boss 必须选择并真正接入一种规则：禁止处决但仍可弹刀硬直、仅最终阶段可处决，或处决改成固定／百分比伤害。需要覆盖主动格挡、命中时格挡、QTE、玩家处决入口和最终处决结算，并确保拒绝处决不会留下 Executing 状态、玩家动作锁或慢动作。

调参使用 `normalImpact`（普通攻击实际命中时点）、`windowStart`、`windowEnd`、`hitRange`、`attackCooldown`。不要只改 `attackWindup`：当前攻击状态逻辑用的是 normalImpact／clockDuration。类似地，当前索敌实际用 detectRange／chaseGiveUpRange，不要依赖旧 loseTargetRange 字段说明。

ParryWindowOpen 依赖 aiState == Attack 和私有 stateTimer。外部只给 Boss 显示黄色预警／写入 Attack 枚举，不会自动得到可靠的 Boss 弹刀窗口。

### 5.4 状态效果与家具交互

| EnemyController 接口 | 参数语义与当前行为 |
| --- | --- |
| `Wet(float duration)`、`IsWet` | 按 Time.time 记录淋湿到期时间；水电联动会读取 IsWet |
| `ApplySlow(float slowPercent, float duration)` | 百分数制：40 表示减速 40%，不是 0.4；保留更强减速并延长计时 |
| `ApplyParalysis(float duration)` | 麻痹计时期间原敌人 Update 提前返回 |
| `ApplyKnockback(Vector3 dir, float force, float duration = 0.35f)` | 位移协程；force 在实现中作为位移距离，不是 Rigidbody 力；只检查地面边界，不保证避开所有家具 |
| `Knockback(Vector3 dir, float distance)` | 另一套受击位移入口，TakeDamage 内部会调用；Boss 抗击退需同时覆盖两条路径 |
| `Pacify(float seconds, FurnitureObject by)` | 培养箱安抚，受 CanBePacified 限制，进入 Pacified AI 状态；受伤唤醒 |
| `ApplyPacify(float duration, EnemyController targetOther = null)` | 另一套反戈接口，设置 isPacified／反戈目标，不等同于上面的安抚 |
| `ForceTauntTarget(FurnitureObject target)` | 阀门嘲讽；改变家具目标，部分状态会拒绝 |
| `CanBePacified`、`Pacified`、`isPacified` | 前两个是安抚状态查询，最后一个是另一套反戈字段；不要混为一个状态 |

现有消费者包括：水塔 Wet／减速／感电，电鳗发电机伤害与麻痹，金属陷阱击退／麻痹，培养箱 Pacify，阀门 ForceTauntTarget，以及水刃、气囊爆炸、珊瑚反伤等。

当前没有通用 Boss 抗性／递减控制接口。若设计要求“Boss 不被驯化、减少击退、麻痹转破防”，应在共享 EnemyController 的对应入口添加可配置策略，默认维持普通怪行为；不要让所有家具分别判断 Boss 名称，不要在 BossBrain 每帧清空控制状态来抢执行顺序。外部 Boss AI 还必须能读取并遵守麻痹／硬直等控制结果；当前 paralysisTimer 是私有的，没有公开 IsParalyzed 查询，建议新增明确查询或统一控制更新钩子。

Boss 攻击家具：

`furniture.TakeDamage(float damage, EnemyController attacker = null)` 或 `TakeHit(float damage, EnemyController attacker = null)`。

- 命中前检查 `furniture.Targetable`，传入真实 Boss 实体作为 attacker，让珊瑚反伤等机制正确回击。
- 现有 `onHitTaken` 是 `Action<float, EnemyController>`，`onFurnitureDestroyed` 是 `Action`；它们属于家具，不是敌人死亡事件。
- 采集门不可破坏，接口会保护它；不要直接减 currentDurability 或 Destroy 门来突破保护。
- 构装数值以 `BiomechanicalCrafting.CraftedProductStats.Calculate(CraftedProduct)` 为统一来源，不为 Boss 战复制一套家具计算公式。

### 5.5 抓取接口

`player.TryGrab(EnemyController by)` 返回是否成功；成功后用 `player.GrabTick(float dmg)` 周期扣血，最后 `player.ThrowFrom(EnemyController by)` 释放／抛出。必须先成功抓住再调用 GrabTick，不得把 GrabTick 当通用不可格挡伤害入口。

`EnemyController.Grabber` 是全局唯一抓取者。现有 StartGrab（私有）会提前占用它；TryGrab 自身不会替调用方完整维护 Boss 的抓取状态机。自定义抓取必须协调唯一拥有者、其他敌人的 YieldToGrab、前摇失败、抓取时长及死亡中断。

`enemy.CancelGrab()` 主要清理敌人和全局抓取引用，并不等价于解除玩家的 isGrabbed；Boss 中断／死亡／卸载时必须确保被自己抓住的玩家通过释放流程恢复。不要释放其他怪物的抓取。

## 6. 推荐的正式 Boss 结构（需要新增，不是现成 API）

| 建议职责 | 实现边界 |
| --- | --- |
| BossEncounterController | 开始触发、生成 Boss／小怪、遭遇状态、胜负、仅一次奖励、退出清理 |
| BossBrain | 读同一个 EnemyController 的血量／控制状态，决定阶段、技能与移动 |
| Boss 技能组件 | 前摇、锁定目标、预警形状、一次／多段命中、后摇和取消；不维护独立 HP |
| BossHealthBar | 只读真实 HP／破防值和阶段；离开遭遇或打开覆盖场景时隐藏／恢复 |
| EnemyController 的最小扩展 | AI 驱动切换、受击／打断策略、控制抗性、处决资格与结算、死亡通知 |
| PlayerController 的最小扩展 | 有来源的伤害入口、格挡／不可格挡规则、处决资格检查、可选失败通知 |

### 6.1 如何接管 AI，而不与普通怪抢控制

推荐在 EnemyController.Update 的共同前置检查／控制状态处理之后、普通状态 switch 之前，增加一个显式外部驱动分支。由唯一驱动者执行 Boss 移动和技能；普通怪没有外部驱动时继续原逻辑。

以下内容必须有唯一所有者：位置移动、技能状态、攻击时钟、弹刀窗口、动画、霸体／控制状态。不要仅在独立 BossBrain.Update 中另写移动／攻击，任由 EnemyController 同时追击、随机抓取、回到 Chase。

受击入口还会改变状态、产生击退；接管 Update 本身并不足够，受击策略也需一并接入。当前 public 方法大部分非 virtual；要扩展可用小接口／委托／策略组件，由共享实现主动调用，而不是对现有私有方法做反射调用。

阶段切换用单向标记／阶段索引，避免在“HP < 50%”时每帧重复触发。跨阶段、眩晕、死亡要能取消前摇／激活中的伤害判定；即使协程稍后恢复，也要通过技能版本／取消标记防止残留命中。不要清零 HP 后再立即写满 HP 来模拟第二阶段。

### 6.2 死亡与结算

当前 EnemyController 没有公开死亡事件。Die 是私有方法，负责关闭 QTE、释放抓取、取消目标、播放死亡，然后进入 DeathAndRespawnRoutine；autoRespawn=false 时淡出后 Destroy。

- 原型 UI 可以读取 `boss != null && boss.IsAlive`，正式胜利／奖励建议在真正死亡分支增加一次性通知，让遭遇管理器消费。
- 不得用 OnDestroy 或 boss==null 就判胜：场景卸载、开局清理、测试停止、主动取消同样会销毁对象。
- 死亡通知需有 exactly-once 防重保护；奖励是否持久化、失败后能否重刷由策划确定。
- 玩家当前 HP 归零调用 TriggerDefeat，约 2 个游戏秒后原地满血／满精力复活，再有 1.8 秒临时无敌。它不是现成的 Boss 战失败／重开系统。
- 如果要“死亡判负、重置 Boss”，需明确接入玩家失败流程，避免遭遇已重开但旧复活协程仍在运行。不要未经确认取消全游戏复活机制。
- Boss 整体战斗进度目前没有存档字段；不要把 Boss 数据塞进温室 greenhouse 或门次数里。需要存档时新增独立、带兼容初始化的数据结构。

## 7. 时间、暂停、协程与场景切换

### 7.1 时间管理接口

| 现有 API | 用途 |
| --- | --- |
| `GameJam.TimeManager.I.StartSlowMo(float realSeconds)` | 慢动作请求；重复调用会重设慢动作区间，勿每帧调用 |
| `GameJam.TimeManager.I.DoHitstop(float realSeconds)` | 受击顿帧 |
| `GameJam.TimeManager.SlowMo`／`Hitstop` | 查询状态 |
| `GameJam.TimeManager.I.Refresh()` | 根据既有状态刷新世界时钟 |
| `GameJam.TimeManager.PlayerDelta` | 玩家专用非缩放步长，顿帧时为 0；Boss 世界移动不要使用它 |

Boss／子弹／场地技能用 Time.deltaTime，随世界慢动作与顿帧。不要自行赋值 Time.timeScale 或 Time.fixedDeltaTime，TimeManager 每帧都会管理它们。

建议给所有自定义技能、子弹碰撞、召唤计时使用同一个暂停判定（以下是要自己加入组件的示例方法，不是项目已有方法）：

```csharp
private static bool BossWorldPaused()
{
    return PlayerSessionData.IsCombatInputBlocked
        || BiomechanicalAuraBase.IsCombatPaused
        || GameJam.TimeManager.Hitstop;
}
```

IsCombatInputBlocked 覆盖异步场景切换忙碌、工坊／温室打开和已加载状态；BiomechanicalAuraBase.IsCombatPaused 覆盖 isAIPaused 或建造抽屉打开。建造暂停不保证 Time.timeScale==0，所以不能只依赖 WaitForSeconds／Time.deltaTime 自动冻结。

示意的技能前摇等待片段，需放在自己的 IEnumerator 内；owner、cancelled 由该技能维护：

```csharp
float elapsed = 0f;
while (elapsed < windupSeconds)
{
    if (cancelled || owner == null || !owner.IsAlive) yield break;
    if (!BossWorldPaused()) elapsed += Time.deltaTime;
    yield return null;
}
while (BossWorldPaused())
{
    if (cancelled || owner == null || !owner.IsAlive) yield break;
    yield return null;
}
if (cancelled || owner == null || !owner.IsAlive) yield break;
```

等待之后执行命中判定前仍需检查技能版本、目标有效性、距离与去重。单纯暂停位移不够：Overlap 查询和已存在的触发体仍可能产生逻辑命中。

现有 `WaterBladeProjectile` 是家具对敌人的水刃，不是通用 Boss 弹幕。它可供阅读“找根 EnemyController／命中销毁”的方式，但没有完整的建造暂停与技能取消保护，不能直接改伤害目标后就当作可靠的 Boss 子弹。高速弹道还需自行设计扫掠碰撞以避免穿透。

### 7.2 与工坊／温室共存

现有签名：

```text
GameplaySceneTransition.TryOpen(string sceneName, PlayerController player, Action onFailure = null) -> bool
GameplaySceneTransition.TryReturn(Scene overlay, Action save) -> bool
```

以上是签名速查写法，不是可直接粘贴到类中的 C# 声明。TryOpen 返回 true 仅表示接受请求，不表示场景已经加载完成；onFailure 是异步加载失败回调，立即拒绝时调用方要处理 false。

这套服务为工坊／温室覆盖场景而设计，会锁战斗输入、暂停世界、禁用 CameraController、隐藏现有 HUD。**不能直接用 TryOpen("BossArena", player) 当作新 Boss 战场切换方案**，否则新战斗也会处于覆盖场景暂停语义。

第一版建议在现有战场内触发遭遇。独立 BossArena 若确有需求，应单独设计战斗场景入口、玩家／单例初始化与返回状态，不要叠加两个拥有全局 AllEnemies／玩家单例／适配器的完整战斗场景。

进入工坊时 Boss HP、阶段、剩余前摇、小怪和弹道状态应保留；返回不得重新 StartEncounter 或重置技能。自建 Boss HUD 不会自动被 `HUD.I.SetVisible(false)` 管理，要跟随覆盖场景状态隐藏，不应在自己的 OnDisable 中误判死亡或发奖励。

## 8. 地图、移动、目标与反馈

### 8.1 等距地图移动

- `IsometricPathfinder.Instance` 可用时，调用 `WorldToCell(Vector3 worldPos, float heightOffset = 0.5f)`、`CellToWorld(Vector3Int cell, float heightOffset = 0.5f)`，保持与现有地面偏移一致。
- `IsWalkable(Vector3Int cell)` 检查地面、建造层和 DynamicBlockedCells；`FindPath(Vector3Int startCell, Vector3Int targetCell)` 返回格子列表，转换为世界航点后自行移动。
- 现有根运动主要在 XY 平面，不要按 3D XZ 地面写冲刺／范围检测。
- 地图外、家具占格、角落／窄通道都要测试。大体积 Boss 需要额外 footprint／路径净空规则；现有寻路不自动处理 Boss 体积。
- 不要随意 Clear 全局 DynamicBlockedCells，那会使其他家具失去阻挡；临时障碍需记录并仅清理自己拥有的格子。
- 脱战与重置要单独设计；增大 detectRange 并不等于完成 Boss 封闭战场规则。

### 8.2 锁定与血条

玩家引用优先用 `PlayerController.Instance`，若为空再 `FindObjectOfType<PlayerController>()`；缓存并在失效时重新获取，不要在每个子弹每帧全场搜索。

`player.SetAttackTarget(EnemyController enemy)` 负责选中目标和相关表现；`ClearAttackTarget()` 清除目标。它们不是“让玩家立刻完成一次攻击”的接口；不要自动强迫玩家寻路贴脸 Boss。

Boss 大血条可读 `currentHp / maxHp`、`currentGuardBreak / maxGuardBreak`；最大值为 0 时做保护。现有 UpdateHealthBar 只更新敌人自带小血条，不会创建顶部 Boss HUD。非交互血条／预警 UI 应关闭 raycastTarget，避免遮挡场景点击。

### 8.3 反馈接口

- `DamageTextManager.Instance?.ShowText(Vector3 worldPos, string content, Color color, float scale = 0.08f)`：飘字。
- `GameJam.Sfx.Play(string name, float volume = 1f, bool clean = false)`：按 Resources/Audio 的 clip 名称播放；缺资源会警告，name 不含扩展名。clean 控制绕过慢动作音频效果，不代表暂停技能计时。
- `GameJam.Vfx.Spawn(string clip, Vector3 pos, float roll = 0, float scale = 1, bool unscaled = false, Transform follow = null, Color? tint = null, float speed = 1)`：使用 Resources/SpriteSets/VFX 中存在的动画 clip，如 VFX_ParrySpark、VFX_ExecuteSlash；任意新字符串不会自动生成素材。
- `CameraRig.I?.Shake(float duration, float magnitude)`：注意参数顺序是持续时间在前、强度在后。
- `CameraController.FocusOn(Vector2 worldPosition, float zoom = -1f)`、`SetFollowPlayer(bool follow)`：入场镜头可用，但退出遭遇应恢复先前镜头状态，不覆盖玩家／场景切换器的控制。

已有颜色语义：AttackGlow 黄色表示可格挡、GrabGlow 红色表示抓取危险、PendingGlow 冰蓝色表示处决就绪。不要在没有实现格挡窗口时随意用黄色误导玩家；其他不可格挡技能要有明确不同预警。

## 9. 实施顺序与需要策划确认的事项

### 9.1 推荐实施顺序

1. 先阅读本文和 CURRENT_VERSION_ROADMAP，核对工作分支；保持普通怪、构装和采集门行为不变。
2. 在独立测试场景／运行时触发器中接通 EnemyController 原型，验证玩家近战、重击和家具都能伤害它。
3. 优先做最小共享扩展：外部 AI 驱动、受击／控制策略、处决策略、带来源的玩家伤害入口、一次性死亡通知。新增参数默认保持普通怪原行为。
4. 再做 Boss 阶段／技能：先一个有前摇、有后摇的技能，验证取消与暂停，再增加范围攻击、弹道和召唤。
5. 最后接 Boss HUD、入场／出场、胜负与奖励；明确死亡／逃离／重试规则，再接入正式战场。
6. 完成下表回归并交付改动列表、必要资源、测试结果和未解决边界；不要仅凭“Console 没报错”认定完成。

### 9.2 策划必须明确的规则

- 几个阶段、血量阈值、攻击节奏；同帧大额伤害跨越多个阈值时如何处理。
- 哪些技能可格挡、可闪避、可中断；预警范围何时锁定，是否追踪玩家。
- 是否允许处决；若允许，触发条件和实际结算是什么。
- 减速、麻痹、击退、培养箱安抚、阀门嘲讽对 Boss 是完整生效、削弱还是替代为破防。
- Boss 是否攻击家具、能否摧毁临时掩体；采集门仍保持不可摧毁。
- 召唤上限、召唤物死亡是否参与阶段／胜利判定；Boss 死后是否清理小怪、弹道和危险区。
- 战中是否允许建造／工坊／采集门；返回是否保留全部遭遇状态。
- 玩家失败是原地复活、重置 Boss 还是退出遭遇；脱战是否回满血。
- 奖励内容、重复挑战规则、是否保存已击败状态。未确认前不要自动写入正式存档或增加可无限刷的奖励。

## 10. 验收清单

| 场景／操作 | 必须满足的结果 |
| --- | --- |
| 初始化并进入遭遇 | Boss 不被 Adapter.Awake 清理、不重复生成，AllEnemies 中只有一个对应根实体 |
| 普攻、重击、推撞、炮台、爆炸、珊瑚反伤 | 均进入同一伤害链；无两份血量，多 Collider 不额外多次扣血 |
| Boss 模型边缘点击与近战 | 大型模型的可选中范围、受击半径与预警一致，不只是根位置附近可点 |
| 普通格挡与主动弹刀 | 窗口、攻击来源、阶段处理正确，Boss 不因默认处决链意外被秒杀 |
| 时钟 QTE、普通处决、非允许阶段的直接处决请求 | 全部遵守 Boss 处决策略；不会卡 Executing／玩家动作锁 |
| Boss 远程弹道／场地伤害，同时附近有普通怪 | 玩家格挡不会错误处罚旁边普通怪；不可格挡语义与预警一致 |
| HP 连续跨阶段、同帧多来源伤害 | 阶段和死亡只触发应有次数，不回填已死亡血量，不重复发奖励 |
| 麻痹／减速／击退／安抚／嘲讽 | 符合策划抗性；普通怪仍保持原效果；控制中不偷偷继续施法 |
| 抓取成功、抓空、Boss 抓取中死亡／退出 | 全局 Grabber 与玩家抓取状态均正确释放 |
| 前摇／弹道／持续伤害过程中打开建造抽屉 | Boss 世界行为与命中全部暂停，返回后不补算一串积压伤害 |
| 前摇／慢动作中进入工坊再返回 | HP、阶段、剩余技能时长和镜头保持；Boss HUD 正确隐藏／恢复 |
| 翻滚无敌、顿帧、慢动作 | 伤害遵守无敌，Boss 和弹幕跟随世界时间，不使用玩家加速时间 |
| 地图边缘、家具阻挡、大体积 Boss 的窄通道 | 不冲出地图或穿过掩体，无法到达时有明确处理 |
| Boss 与召唤物死亡 | autoRespawn=false，死亡只结算一次，残留危险区与引用按规则清理 |
| 取消遭遇／卸载场景／退出 Play | 不判胜、不发奖励，不遗留 QTE、HUD、玩家动作锁或全局暂停 |
| 玩家死亡后等待原复活窗口 | 与约定失败规则一致，不发生遭遇重置和旧复活协程相互覆盖 |
| 完整回归普通怪、家具、工坊、采集门 | 原战斗功能正常；每日每门一次、其他门独立资源的规则不受影响 |

项目当前没有为 Boss 配置的自动化测试程序集。可用 Unity MCP 反射／内存断言辅助，但真实鼠标、键盘／触摸输入、动画命中时序及目标平台运行仍需单独验证。

测试前备份 PlayerPrefs 的存在状态与原始字符串：`TAP_SHELL_BACKPACK_DATA`、`TAP_CRAFTED_PRODUCTS_DATA_V2`；测试后退出 Play 并逐字恢复。若后续新增 Boss 存档键，同样备份。不要为测试清空玩家正式存档。

## 11. 可直接发给实现 Agent 的任务说明

> 请基于 BOSS_COMBAT_AGENT_GUIDE.md 和当前源码实现策划确认的 Boss 遭遇。保留 EnemyController 作为敌人识别与受伤实体，不复制第二套 HP，不用禁用组件／new 方法来替代正规扩展。先解决开局敌人清理、普通格挡／QTE 导致处决秒杀、带来源的玩家伤害、建造／工坊暂停及死亡单次结算，再实现阶段与技能。新增 Boss 代码与资源独立放置，修改共享战斗类时让普通怪默认行为保持不变；不要重生成正式场景或改采集门规则。所有文中“建议新增”的接口需先真实实现，不能假定已有。交付最小改动清单、策划规则映射、可运行测试入口及验收表结果；未验证项明确标注，不擅自提交或覆盖用户其他改动。

## 12. 本文核验范围

- 根据当前工程源码逐项核对关键路径，并通过已连接 Unity Editor 的反射确认主要生成、伤害、控制、抓取、处决、寻路和场景接口存在，且关键方法不是 virtual。
- 原型生成组件、暂停判断方法和前摇等待片段已使用当前 Unity 的 .NET Standard 2.1 引用程序集做内存编译检查，通过且无诊断；片段编译时补齐了示意的所属类／协程上下文，没有执行生成或修改场景。
- 本文中的原型与等待片段仅用于展示接入方式，不代表 Boss 玩法已经实现，也不代表这些原型技能已经通过 Play Mode 行为验收。
- 实现 Agent 应在开始修改前再次确认源码版本；若接口变化，以实际代码为准并同步更新文档。
