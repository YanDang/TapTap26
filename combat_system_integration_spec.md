# 生体机械构装 · 战斗系统集成与实装技术规范 (Combat System Integration Specification)

> **面向对象**：战斗场景系统实现 AI / 客户端开发者  
> **工程上下文**：TapTap 21天 GameJam 项目 (`TapTap26`)  
> **核心目标**：指导在战斗场景 (`TileTest.unity` 或相关战场关卡) 中无缝集成【温室采集】与【背壳合成工坊】产出的全部构装家具、生体武器与战术杂交废料。

---

## 目录
1. [系统全貌与跨场景数据流闭环](#一-系统全貌与跨场景数据流闭环)
2. [核心数据结构与公共 API (PlayerSessionData)](#二-核心数据结构与公共-api-playersessiondata)
3. [6 大正规生体家具构装战斗落地规范](#三-6-大正规生体家具构装战斗落地规范)
4. [4 大活性生体杂交废料战术用途规范](#四-4-大活性生体杂交废料战术用途规范)
5. [战斗系统现有架构与集成挂载点](#五-战斗系统现有架构与集成挂载点)
6. [即拷即用实装示例：BiomechanicalCombatAdapter](#六-即拷即用实装示例-biomechanicalcombatadapter)

---

## 一、 系统全貌与跨场景数据流闭环

整个游戏的经济与战斗闭环由三个核心模块构成：

```mermaid
flowchart LR
    A["🌿 采集温室 (Collect.unity)\n药剂工艺式点击/滑动\n产出 1x1辅料 与 2x2骨架"] -->|自动顺次入包| B["🎒 背壳材料背包\nPlayerSessionData.shellSlotItems\n(带 2x2 连续拓扑防叠检测)"]
    B -->|回路划线共振合成| C["⚙️ 生体工坊 (Mix.unity)\n四大要素聚合 P/E/G/W\n无废品涌现规则"]
    C -->|成品存入仓库| D["📦 构装成品战术仓库\nPlayerSessionData.craftedProducts\n(家具/废料不占材料背包)"]
    D -->|建造模式部署 / 战斗互动| E["⚔️ 战斗场景 (TileTest.unity)\n等距地块放置 / 踢飞撞击\n手持挥砸 / 元素光环协同"]
```

### 1. 跨场景生命周期与无缝叠加 (LoadSceneMode.Additive)
- **战斗中无缝呼出工坊**：战斗场景内挂载了 [`CombatSceneCraftingEntrance.cs`](file:///D:/Game_Develop/TapTap26/Assets/Scripts/Crafting/CombatSceneCraftingEntrance.cs)。
  - PC 端快捷键：`B` 或 `Tab`
  - 移动端：右上角【建造/工坊】按钮
  - 采用 `LoadSceneMode.Additive` 叠加打开工坊场景，此时战斗场景 `Time.timeScale = 0f`（或逻辑暂停），**怪物的血量、站位、弹道、已部署的墙体 100% 冻结保留**。
- **合成归来与状态同步**：工坊合成完成后关闭卸载，`PlayerSessionData.hasJustReturnedFromCrafting` 自动置为 `true`，战斗场景自动解冻，战斗系统可直接检测并刷新玩家手持武器或建造快捷栏。

---

## 二、 核心数据结构与公共 API (PlayerSessionData)

全部跨场景共享数据均由静态类 [`PlayerSessionData`](file:///D:/Game_Develop/TapTap26/Assets/Scripts/Crafting/PlayerSessionData.cs) 统一托管并自动持久化（基于 `PlayerPrefs`）。

### 1. 成品数据实体：`CraftedProduct`
命名空间：`BiomechanicalCrafting`  
源码位置：[`ShellCraftingRecipe.cs`](file:///D:/Game_Develop/TapTap26/Assets/Scripts/Crafting/ShellCraftingRecipe.cs)

```csharp
namespace BiomechanicalCrafting
{
    [System.Serializable]
    public class CraftedProduct
    {
        public string id;                  // 设施/废料全局唯一ID (如 "furn_water_tower", "scrap_gas_cyst")
        public string productName;         // 显示名称 (如 "精制·脉动造水循环塔")
        public string category;            // 分类: "潮润设施", "电网设施", "增生防线", "气压设施", "智能设施", "水气武器", "活性杂交废料"
        public bool isAberrantScrap;       // 是否为异变杂交废料 (true=生体陷阱/自爆雷, false=正规家具设施)
        public string qualityTier;         // 品质层级: "粗糙" (2-3节点), "精制" (4节点), "多孔过载" (5+节点)
        public int nodeCount;              // 参与合成的生体回路节点数 (2 ~ 8)
        public string primaryElement;      // 主导元素: "Water", "Electric", "Wood", "Gas"
        
        public string flavorDescription;   // 背景世界观风味文本
        public string tacticalEffect;      // 实际战术效果/物理数值说明文本

        // 四大要素聚合数值（已结算节点阻抗衰减与 2x2 大骨架共鸣加权）
        public int totalP;                 // 气 / 压力 (Pressure): 决定击退力、斥力力场、承压强度
        public int totalE;                 // 电 / 电荷 (Electric): 决定放电伤害、供电网络、麻痹时长
        public int totalG;                 // 木 / 增生 (Growth): 决定掩体耐久、荆棘反伤、生命自愈
        public int totalW;                 // 水 / 通量 (Water): 决定潮润覆盖半径、水渍减速、驯化安抚
        
        public bool hasMajorResonance;     // 是否触发了 2x2 大骨架共鸣 (属性获得 +20% 增幅)
        public Color themeColor;           // 设施对应的生体荧光主题色
    }
}
```

### 2. 仓库管理核心接口
战斗场景系统（如背包快捷栏、建造轮盘、建筑管理器）只需调用以下静态方法：

```csharp
// 1. 获取当前仓库中所有已合成、可供部署的构装设施与废料
List<CraftedProduct> availableProducts = PlayerSessionData.GetCraftedProducts();

// 2. 在战场放置/消耗指定构装
bool success = PlayerSessionData.RemoveCraftedProduct(targetProduct);

// 3. 按 ID 消耗一件构装 (例如部署了一台造水塔)
bool success = PlayerSessionData.RemoveCraftedProductById("furn_water_tower");

// 4. 拆除/回收设施返回仓库
PlayerSessionData.AddCraftedProduct(recoveredProduct);
```

---

## 三、 6 大正规生体家具构装战斗落地规范

当玩家通过建造模式将正规家具部署在地面时，它们作为带有碰撞体积的等距实体存在（继承或挂载 [`FurnitureObject`](file:///D:/Game_Develop/TapTap26/Assets/Scripts/FurnitureObject.cs)）。战斗系统应根据 `CraftedProduct` 的属性为它们注入专属战术光环与机制：

### 1. 电鳗电机发电机组 (`furn_eel_generator`)
* **分类**：电网设施 | **主导元素**：Electric | **主题色**：暖黄电弧 (0.98, 0.82, 0.18)
* **动态属性计算**：
  * **触碰麻痹半径**：`radius = Mathf.Clamp(2.0f + totalE / 55.0f, 2.0f, 5.0f)` (米)
  * **电弧反击伤害**：`shockDamage = Mathf.Clamp(Mathf.RoundToInt(18 + totalE * 0.35f + totalP * 0.12f), 18, 75)`
  * **机体耐久值**：`maxDurability = Mathf.Clamp(Mathf.RoundToInt(120 + totalG * 1.5f), 120, 420)`
* **实战逻辑**：
  * **常驻供电**：以自身为圆心半径 4.5m 形成电网供电区，使范围内的防御设施攻速/效能 +25%。
  * **静电反制**：任何靠近半径内的敌怪，每 1.5 秒受到一次电弧跳跃伤害（`shockDamage`），并附加 1.0 秒轻微麻痹（减速 50%）。

---

### 2. 增生珊瑚管排墙 (`furn_coral_wall`)
* **分类**：增生防线 | **主导元素**：Wood | **主题色**：翠绿增生 (0.29, 0.85, 0.44)
* **动态属性计算**：
  * **掩体耐久值**：`wallHp = Mathf.Clamp(Mathf.RoundToInt(150 + totalG * 2.0f), 150, 480)`
  * **荆棘反伤数值**：`thorns = Mathf.Clamp(Mathf.RoundToInt(10 + totalE * 0.18f + totalG * 0.16f), 10, 42)`
  * **每秒自愈恢复**：`regen = Mathf.Clamp(Mathf.RoundToInt(totalG / 25.0f + 1), 1, 8)`
* **实战逻辑**：
  * **A* 寻路硬阻挡**：注册至 `IsometricPathfinder.DynamicBlockedCells`，强行阻断怪物寻路，改变敌人行进路线。
  * **荆棘反噬**：怪物每次近战普通攻击命中珊瑚墙，受击方即刻受到固定 `thorns` 点破防物理伤害。
  * **活体自愈**：脱离攻击 3 秒后，每秒自动恢复 `regen` 点耐久。

---

### 3. 气泡微压悬浮软床 (`furn_pneumatic_bed`)
* **分类**：气压设施 | **主导元素**：Gas | **主题色**：荧光淡紫 (0.75, 0.52, 0.99)
* **动态属性计算**：
  * **斥力圈半径**：`fieldRadius = Mathf.Clamp(2.5f + totalP / 45.0f, 2.5f, 5.5f)` (米)
  * **友方移速增益**：`speedBoost = Mathf.Clamp(Mathf.RoundToInt(12 + totalP / 14.0f), 12, 35)` (%)
  * **气囊承压耐久**：`dura = Mathf.Clamp(Mathf.RoundToInt(90 + totalG * 1.2f), 90, 300)`
* **实战逻辑**：
  * **反重力力场**：玩家或友方随从步入力场范围时，脚底生成气垫微风，移动速度瞬间提升 `+speedBoost%`，翻滚闪避精力消耗降低 25%。
  * **轻微排斥**：敌人进入边缘时受到柔性微压阻力，冲刺技能速度被衰减 30%。

---

### 4. 脉动造水循环塔 (`furn_water_tower`)
* **分类**：潮润设施 | **主导元素**：Water | **主题色**：潮润青蓝 (0.22, 0.74, 0.97)
* **动态属性计算**：
  * **喷淋覆盖半径**：`sprayRadius = Mathf.Clamp(2.5f + totalW / 40.0f, 2.5f, 6.0f)` (米)
  * **水渍减速比例**：`slowPercent = Mathf.Clamp(Mathf.RoundToInt(25 + totalW / 9.0f), 25, 60)` (%)
* **实战逻辑**：
  * **持续造水浸染**：每 2 秒向地面脉动喷洒一次生体活水，使地表覆盖潮湿水渍，踩入的敌人移动速度强制降低 `slowPercent%`。
  * **【元素协同·感电暴击】**：若水渍地表与【电鳗电机】的电网范围重叠，水渍转变为**带电水潭**，经过的敌人受到电击伤害提升 200%，且瘫痪蓄积速度加倍！

---

### 5. 小型生体驯化培养箱 (`furn_bio_incubator`)
* **分类**：智能设施 | **主导元素**：Water | **主题色**：深邃天蓝 (0.20, 0.65, 0.92)
* **动态属性计算**：
  * **脑电安抚半径**：`lureRange = Mathf.Clamp(3.0f + (totalW + totalE) / 50.0f, 3.0f, 7.5f)` (米)
  * **生体驯化概率**：`pacifyChance = Mathf.Clamp(Mathf.RoundToInt(25 + totalG / 7.0f), 25, 65)` (%)
* **实战逻辑**：
  * **心智诱引**：持续向外释放脉冲共鸣波，吸引附近 `lureRange` 内的小型生体怪物向培养箱聚集（聚怪效果）。
  * **阵营逆转/瘫痪**：每 3 秒对范围内的非 Boss 怪物进行一次概率判定（`pacifyChance%`）：
    * 判定成功：怪物头顶浮现爱心/荧光符号，停止攻击玩家，转为反戈攻击其他敌对怪物，持续 8 秒！

---

### 6. 高压自愈喷水炮 (`furn_hydro_cannon`)
* **分类**：水气武器 | **主导元素**：Water | **主题色**：高光苍青 (0.15, 0.82, 0.88)
* **动态属性计算**：
  * **高压水刃射程**：`shotRange = Mathf.Clamp(4.5f + totalP / 38.0f, 4.5f, 8.5f)` (米)
  * **冲击击退推力**：`blastImpact = Mathf.Clamp(Mathf.RoundToInt(18 + (totalP + totalW) * 0.22f), 18, 65)`
* **实战逻辑**：
  * **自动警戒**：朝进入 `shotRange` 的最近敌人发射一道高压水流喷射刃（冷却时间 2.2 秒）。
  * **强力击退破防**：水刃命中造成中等伤害，并将敌人沿射击直线击退数米，瞬间扣除怪物 40 点韧性条。

---

## 四、 4 大活性生体杂交废料战术用途规范

废料（`isAberrantScrap == true`）不是无用垃圾，而是遵循**无废品涌现规则**诞生的生体畸变陷阱。它们成本极低、无需专属蓝图，在战斗中部署后具备极其强悍的“坑怪/战术地雷”效果：

| 废料 ID | 废料名称 | 触发机制 | 实战效果 (Combat Behavior) |
| :--- | :--- | :--- | :--- |
| **`scrap_spasm_metal`** | **抽搐的痉挛金属团** | **踩踏触发** (怪物或踢击碰撞) | **【超级弹力跳板】**：如同超强压缩弹簧，将踩中的怪物直接弹飞 $2.5\sim 6.0\text{ m}$，撞墙眩晕并附加 $1.2\sim 3.0$ 秒麻痹！随后自身解体。 |
| **`scrap_mud_coral`** | **漏电的烂泥珊瑚块** | **范围常驻陷阱** | **【带电烂泥陷阱】**：半径 $2.0\sim 4.5\text{ m}$ 铺开潮湿带电泥潭，踩入者持续减速 $40\%$，每秒遭受 $12\sim 52$ 点电击。耐久耗尽后自爆破裂。 |
| **`scrap_gas_cyst`** | **膨胀的气囊疙瘩** | **受击 / 接近延时自爆** | **【烈性生物雷】**：怪物靠近 $1.2\text{ m}$ 内或受到任何攻击时剧烈抽搐，1 秒后自爆，产生半径 $2.0\sim 4.5\text{ m}$ 的高额破甲冲击（造成 $25\sim 78$ 点范围爆发伤害）。 |
| **`scrap_hybrid_residue`**| **活性共生杂交残渣** | **常驻阻滞地块** | **【恶臭黏油减速块】**：敌人踩踏减速 $50\%$，死亡或打碎后释放一小团生体疗愈水雾，为近旁友军恢复微量生命值。 |

---

## 五、 战斗系统现有架构与集成挂载点

战斗场景中已经具备完整的家具三态与建造管线，新 AI 无需从零重构，只需将它们连接起来：

### 1. 现有三态基类：[`FurnitureObject.cs`](file:///D:/Game_Develop/TapTap26/Assets/Scripts/FurnitureObject.cs)
* **放置态 (Placed)**：占据等距地块，阻断寻路，玩家靠近 $1.4\text{ m}$ 自动点亮金色光圈提示。
* **滑行态 (Sliding)**：
  * 玩家靠近按 `F` 或踢击键，调用 `Kick(direction)`。
  * 家具沿向量高速滑行，具有摩擦阻尼与悬崖边界保护（**绝不飞出地图**）。
  * 撞击怪物造成击撞伤害与高额削韧破防。
* **手持态 (Held)**：
  * 玩家举起家具作为重锤，受到移速惩罚，攻击力大幅飙升至 $130+$。
  * 可使用鼠标右键投掷（`Throw(dir)`）高速抛出砸向远距离怪物。
* **碎裂爆发 (ShatterBurst)**：
  * 耐久度归零或投掷撞墙时触发，造成半径 $1.8\text{ m}$ 范围碎裂爆炸（80伤害 + 100破防）。

### 2. 现有建造控制器：[`BuildController.cs`](file:///D:/Game_Develop/TapTap26/Assets/Scripts/BuildController.cs)
* 已经完美支持**等距立方体顶面 $0.5$ 高程对齐**。
* 目前它的 `PlaceFurniture()` 方法中硬编码为仅生成 `Furniture_LavaTable`。
* **战斗 AI 需要改造的点**：
  在 `BuildController` 中增加当前选中的 `CraftedProduct` 槽位，点击放置时从 `PlayerSessionData.RemoveCraftedProduct()` 扣除，并使用对应的预制体或根据数据动态初始化 `FurnitureObject`！

---

## 六、 即拷即用实装示例：BiomechanicalCombatAdapter

为了让其他 AI 最快上手，这里提供一个可以直接在战斗场景中挂载的适配桥接组件：

```csharp
using UnityEngine;
using BiomechanicalCrafting;

/// <summary>
/// 战斗场景生体构装桥接适配器：负责将 PlayerSessionData 仓库中的构装产物实例化为场景实体
/// </summary>
public class BiomechanicalCombatAdapter : MonoBehaviour
{
    public static BiomechanicalCombatAdapter Instance { get; private set; }

    [Header("Base Furniture Prefab")]
    public GameObject genericFurniturePrefab; // 可复用现有的 Furniture_LavaTable 预制体作为视觉基底

    void Awake()
    {
        Instance = this;
    }

    /// <summary>
    /// 将仓库中的指定产品部署到指定的等距网格地块上
    /// </summary>
    public GameObject SpawnProductAtCell(CraftedProduct product, Vector3Int cellPos, Vector3 worldPos)
    {
        if (product == null) return null;

        // 1. 实例化物体
        GameObject go = Instantiate(genericFurniturePrefab, worldPos, Quaternion.identity);
        go.name = $"{product.id}_{cellPos.x}_{cellPos.y}";

        // 2. 挂载并注入 FurnitureObject 参数
        var furn = go.GetComponent<FurnitureObject>() ?? go.AddComponent<FurnitureObject>();
        furn.instanceId = product.id;
        furn.furnitureName = product.productName;

        // 3. 根据构装四大要素 totalP, totalE, totalG, totalW 动态赋能
        float dura = product.totalG > 0 ? (120f + product.totalG * 1.5f) : 150f;
        furn.maxDurability = dura;
        furn.currentDurability = dura;
        furn.kickDamage = 60f + product.totalP * 0.8f;
        furn.kickGuardBreak = 80f + product.totalP * 0.5f;

        // 4. 着色与精灵外观
        if (furn.furnitureRenderer != null)
        {
            furn.furnitureRenderer.color = product.themeColor;
        }

        // 5. 挂载专属战术光环逻辑组件
        AttachTacticalAura(go, product);

        // 6. 注册至等距网格阻挡
        furn.SnapToNearestGrid();

        // 7. 从仓库扣除 1 件
        PlayerSessionData.RemoveCraftedProduct(product);

        return go;
    }

    private void AttachTacticalAura(GameObject go, CraftedProduct prod)
    {
        // 根据 ID 挂载不同的战术光环行为组件
        switch (prod.id)
        {
            case "furn_water_tower":
                // 示例：挂载潮润喷淋减速圈
                var waterAura = go.AddComponent<WaterTowerAuraBehavior>();
                waterAura.sprayRadius = Mathf.Clamp(2.5f + prod.totalW / 40.0f, 2.5f, 6.0f);
                waterAura.slowPercent = Mathf.Clamp(Mathf.RoundToInt(25 + prod.totalW / 9.0f), 25, 60);
                break;

            case "furn_eel_generator":
                // 示例：挂载电弧麻痹光环
                var elec = go.AddComponent<EelGeneratorAuraBehavior>();
                elec.paralyzeRadius = Mathf.Clamp(2.0f + prod.totalE / 55.0f, 2.0f, 5.0f);
                elec.shockDmg = Mathf.Clamp(Mathf.RoundToInt(18 + prod.totalE * 0.35f), 18, 75);
                break;

            case "scrap_spasm_metal":
                // 示例：挂载弹簧跳板碰撞逻辑
                var spring = go.AddComponent<SpasmMetalTrapBehavior>();
                spring.knockbackForce = 4.5f + prod.totalP / 30.0f;
                break;

            case "scrap_gas_cyst":
                // 示例：挂载自爆气囊地雷逻辑
                var bomb = go.AddComponent<GasCystMineBehavior>();
                bomb.blastDamage = 35f + prod.totalP * 0.4f;
                break;
        }
    }
}
```

---

## 七、 交付检查清单 (Delivery Checklist)

交给战斗系统 AI 时，请确认以下各项均可顺畅运行：
- [x] **数据仓库已就绪**：`PlayerSessionData.GetCraftedProducts()` 可随时拉取工坊合成出来的物品列表。
- [x] **背包消耗闭环**：工坊合成成功时已自动清空背壳格子并放入仓库，玩家返回战斗场景时仓库有数据。
- [x] **无编译阻碍**：代码已通过 Unity Editor 全编译验证（`Result=Succeeded | Errors=0`）。
- [x] **三态物理完备**：战斗场景已具备滑行冲撞、举起挥砸、投掷与碎裂爆发底层物理。
