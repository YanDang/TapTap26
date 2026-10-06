using System;
using System.Collections.Generic;
using UnityEngine;

namespace BiomechanicalCrafting
{
    /// <summary>
    /// 合成产物实体（正规生体家具 / 活性生体杂交废料）
    /// 包含根据 P/E/G/W 实时推演的动态物理/实战属性
    /// </summary>
    [Serializable]
    public class CraftedProduct
    {
        public string id = "prod_default";
        public string productName = "未命名生体构件";
        public string category = "家具设施"; // 电网设施, 增生防线, 活性杂交废料
        public bool isAberrantScrap = false;  // 是否为异变生体废料
        public string qualityTier = "粗糙";   // 3节点=粗糙，4节点=精制，5+节点=多孔过载
        public int nodeCount = 3;
        public string primaryElement = "None";

        public string flavorDescription = "";
        public string tacticalEffect = "";    // 实际物理/战斗特性说明

        [Header("四大要素聚合数值")]
        public int totalP = 0; // 聚合压力值
        public int totalE = 0; // 聚合电荷律
        public int totalG = 0; // 聚合生物增生力
        public int totalW = 0; // 聚合流体通量
        public bool hasMajorResonance = false; // 是否触发大骨架共振加成

        public Color themeColor = Color.white;
    }

    /// <summary>
    /// 一笔画生体回路配方与涌现推演器
    /// 贯彻“连接即逻辑，无绝对废品”原则与“四元素 P/E/G/W 动力学”：
    /// 1. 规整匹配 -> 派生图鉴标准设施（数值随 P/E/G/W 动态增幅）；
    /// 2. 混乱匹配 -> 派生千奇百怪的活性生体废料（弹簧/自爆/导电/嘲讽）；
    /// 3. 大骨架核心起手 -> 触发 20% 生体共鸣倍率！
    /// </summary>
    public static class ShellCraftingRecipe
    {
        /// <summary>
        /// 实时计算回路中的要素聚合值，内置回路阻抗递减法则与大骨架共鸣倍率
        /// </summary>
        public static (int p, int e, int g, int w, bool hasResonance) CalculateAccumulatedStats(List<BiomechanicalMaterial> lineMaterials)
        {
            if (lineMaterials == null || lineMaterials.Count == 0) return (0, 0, 0, 0, false);

            float rawP = 0, rawE = 0, rawG = 0, rawW = 0;
            bool hasResonance = lineMaterials[0] != null && lineMaterials[0].isMajor;
            float resonanceMul = hasResonance ? 1.2f : 1.0f;

            for (int i = 0; i < lineMaterials.Count; i++)
            {
                var m = lineMaterials[i];
                if (m == null) continue;

                // 回路阻抗递减法则 (Circuit Impedance / Diminishing Returns):
                // 节点 0, 1: 100% 转化
                // 节点 2: 85% 转化
                // 节点 3: 70% 转化
                // 节点 4+: 50% 转化
                float nodeEfficiency = 1.0f;
                if (i == 2) nodeEfficiency = 0.85f;
                else if (i == 3) nodeEfficiency = 0.70f;
                else if (i >= 4) nodeEfficiency = 0.50f;

                rawP += m.p * nodeEfficiency;
                rawE += m.e * nodeEfficiency;
                rawG += m.g * nodeEfficiency;
                rawW += m.w * nodeEfficiency;
            }

            // 骨架共振加成
            rawP *= resonanceMul;
            rawE *= resonanceMul;
            rawG *= resonanceMul;
            rawW *= resonanceMul;

            return (
                Mathf.RoundToInt(rawP),
                Mathf.RoundToInt(rawE),
                Mathf.RoundToInt(rawG),
                Mathf.RoundToInt(rawW),
                hasResonance
            );
        }

        /// <summary>
        /// 根据划线选中的材料链条进行生体合成推演
        /// </summary>
        public static CraftedProduct Evaluate(List<BiomechanicalMaterial> lineMaterials)
        {
            if (lineMaterials == null || lineMaterials.Count < 2)
            {
                return null;
            }

            int count = lineMaterials.Count;
            string qualityTier = count <= 3 ? "粗糙" : (count == 4 ? "精制" : "多孔过载");
            float qualityMultiplier = count <= 3 ? 1.0f : (count == 4 ? 1.15f : 1.28f);

            // 1. 统计 P, E, G, W 总量及材料 ID 集合 (应用边际衰减)
            var stats = CalculateAccumulatedStats(lineMaterials);
            int sumP = stats.p;
            int sumE = stats.e;
            int sumG = stats.g;
            int sumW = stats.w;
            bool hasMajorResonance = stats.hasResonance;

            HashSet<string> idSet = new HashSet<string>();
            foreach (var m in lineMaterials)
            {
                if (m != null) idSet.Add(m.id);
            }

            // 判定主导元素
            string dominantElem = "None";
            int maxVal = Mathf.Max(sumP, Mathf.Max(sumE, Mathf.Max(sumG, sumW)));
            if (maxVal == sumE) dominantElem = "Electric";
            else if (maxVal == sumG) dominantElem = "Wood";
            else if (maxVal == sumP) dominantElem = "Gas";
            else dominantElem = "Water";

            Color themeCol = dominantElem == "Electric" ? new Color(0.98f, 0.82f, 0.18f) :
                            (dominantElem == "Wood" ? new Color(0.29f, 0.85f, 0.44f) :
                            (dominantElem == "Gas" ? new Color(0.75f, 0.52f, 0.99f) : new Color(0.22f, 0.74f, 0.97f)));

            string resonanceTag = hasMajorResonance ? " [大骨架共鸣+20%]" : "";

            // =========================================================================
            // A. 正规生体图鉴蓝图匹配
            // =========================================================================

            // 1. 电鳗电机发电机组 (电系核心：砗磲绝缘壳 + 电鳗肌束)
            if (idSet.Contains("mat_elec_clam_shell") && idSet.Contains("mat_elec_muscle_bundle"))
            {
                float paralyzeRadius = Mathf.Clamp(2.0f + sumE / 55.0f * qualityMultiplier, 2.0f, 5.0f);
                int shockDmg = Mathf.Clamp(Mathf.RoundToInt((18 + sumE * 0.35f + sumP * 0.12f) * qualityMultiplier), 18, 75);
                int dura = Mathf.Clamp(Mathf.RoundToInt((120 + sumG * 1.5f) * qualityMultiplier), 120, 420);

                return new CraftedProduct
                {
                    id = "furn_eel_generator",
                    productName = $"{qualityTier}·电鳗电机发电机组",
                    category = "电网设施",
                    isAberrantScrap = false,
                    qualityTier = qualityTier,
                    nodeCount = count,
                    primaryElement = "Electric",
                    totalP = sumP, totalE = sumE, totalG = sumG, totalW = sumW,
                    hasMajorResonance = hasMajorResonance,
                    flavorDescription = "砗磲贝壳紧紧闭合包裹着放电肌，向外吐出滋滋电火花。" + resonanceTag,
                    tacticalEffect = $"【脉冲供电网】为周围设施供电；触碰麻痹范围 {paralyzeRadius:F1}m；电弧伤害 {shockDmg}；外壳耐久 {dura} 点。",
                    themeColor = themeCol
                };
            }

            // 2. 增生珊瑚管排墙 (木系防线核心：珊瑚骨板/珊瑚管 + 齿轮/菌群)
            if ((idSet.Contains("mat_wood_coral_plate") || idSet.Contains("mat_wood_hollow_coral")) &&
                (idSet.Contains("mat_wood_root_gear") || idSet.Contains("mat_wood_symbiotic_fungi")))
            {
                int wallHp = Mathf.Clamp(Mathf.RoundToInt((150 + sumG * 2.0f) * qualityMultiplier), 150, 480);
                int thorns = Mathf.Clamp(Mathf.RoundToInt((10 + sumE * 0.18f + sumG * 0.16f) * qualityMultiplier), 10, 42);
                int regen = Mathf.Clamp(Mathf.RoundToInt((sumG / 25.0f + 1) * qualityMultiplier), 1, 8);

                return new CraftedProduct
                {
                    id = "furn_coral_wall",
                    productName = $"{qualityTier}·增生珊瑚管排墙",
                    category = "增生防线",
                    isAberrantScrap = false,
                    qualityTier = qualityTier,
                    nodeCount = count,
                    primaryElement = "Wood",
                    totalP = sumP, totalE = sumE, totalG = sumG, totalW = sumW,
                    hasMajorResonance = hasMajorResonance,
                    flavorDescription = "粉红致密的钙化珊瑚排管，在水汽中能以肉眼可见的速度不断增生自愈。" + resonanceTag,
                    tacticalEffect = $"【活体掩体】掩体耐久 {wallHp} 点；触须荆棘反伤 {thorns}；每秒自愈恢复 +{regen} 点耐久。",
                    themeColor = themeCol
                };
            }

            // 3. 气泡微压悬浮软床 (气系核心：储气泡囊 + 呼吸浮囊)
            if (idSet.Contains("mat_gas_bubble_sac") && idSet.Contains("mat_gas_float_bladder"))
            {
                float fieldRadius = Mathf.Clamp(2.5f + sumP / 45.0f * qualityMultiplier, 2.5f, 5.5f);
                int speedBoost = Mathf.Clamp(Mathf.RoundToInt((12 + sumP / 14.0f) * qualityMultiplier), 12, 35);
                int dura = Mathf.Clamp(Mathf.RoundToInt((90 + sumG * 1.2f) * qualityMultiplier), 90, 300);

                return new CraftedProduct
                {
                    id = "furn_pneumatic_bed",
                    productName = $"{qualityTier}·气泡微压悬浮软床",
                    category = "气压设施",
                    isAberrantScrap = false,
                    qualityTier = qualityTier,
                    nodeCount = count,
                    primaryElement = "Gas",
                    totalP = sumP, totalE = sumE, totalG = sumG, totalW = sumW,
                    hasMajorResonance = hasMajorResonance,
                    flavorDescription = "微弱浮空呼吸的透明气垫床，宛如活着的海绵水母。" + resonanceTag,
                    tacticalEffect = $"【反重力斥力圈】范围 {fieldRadius:F1}m；圈内友方移速提升 +{speedBoost}%；气囊承压韧度 {dura} 点。",
                    themeColor = themeCol
                };
            }

            // 4. 脉动造水循环塔 (水系核心：造水心室 + 海绵滤芯/虹吸管)
            if (idSet.Contains("mat_water_ventricle") && (idSet.Contains("mat_water_sponge_filter") || idSet.Contains("mat_water_siphon_tube")))
            {
                float sprayRadius = Mathf.Clamp(2.5f + sumW / 40.0f * qualityMultiplier, 2.5f, 6.0f);
                int slowPercent = Mathf.Clamp(Mathf.RoundToInt((25 + sumW / 9.0f) * qualityMultiplier), 25, 60);

                return new CraftedProduct
                {
                    id = "furn_water_tower",
                    productName = $"{qualityTier}·脉动造水循环塔",
                    category = "潮润设施",
                    isAberrantScrap = false,
                    qualityTier = qualityTier,
                    nodeCount = count,
                    primaryElement = "Water",
                    totalP = sumP, totalE = sumE, totalG = sumG, totalW = sumW,
                    hasMajorResonance = hasMajorResonance,
                    flavorDescription = "心室有节律地泵出清冽活水，通过虹吸管网形成闭环活体喷泉。" + resonanceTag,
                    tacticalEffect = $"【潮润浸染】喷淋半径 {sprayRadius:F1}m；地面水渍使经过敌人减速 {slowPercent}% 并导电。",
                    themeColor = themeCol
                };
            }

            // 5. 小型生体驯化培养箱 (水/电AI核心：破损逻辑指令片 + 压电晶片/复眼/菌群)
            if (idSet.Contains("mat_water_logic_chip") && (idSet.Contains("mat_elec_piezo_chip") || idSet.Contains("mat_water_patrol_lens") || idSet.Contains("mat_wood_symbiotic_fungi")))
            {
                float lureRange = Mathf.Clamp(3.0f + (sumW + sumE) / 50.0f * qualityMultiplier, 3.0f, 7.5f);
                int pacifyChance = Mathf.Clamp(Mathf.RoundToInt((25 + sumG / 7.0f) * qualityMultiplier), 25, 65);

                return new CraftedProduct
                {
                    id = "furn_bio_incubator",
                    productName = $"{qualityTier}·小型生体驯化培养箱",
                    category = "智能设施",
                    isAberrantScrap = false,
                    qualityTier = qualityTier,
                    nodeCount = count,
                    primaryElement = "Water",
                    totalP = sumP, totalE = sumE, totalG = sumG, totalW = sumW,
                    hasMajorResonance = hasMajorResonance,
                    flavorDescription = "内置水族认知逻辑回路的微型培养舱，散发淡蓝荧光。" + resonanceTag,
                    tacticalEffect = $"【生体共鸣诱驯】安抚诱引半径 {lureRange:F1}m；使进入范围的野生怪物驯化概率 {pacifyChance}%。",
                    themeColor = themeCol
                };
            }

            // 6. 高压自愈喷水炮 (角质喷头 + 排气阀管)
            if (idSet.Contains("mat_water_keratin_nozzle") && idSet.Contains("mat_gas_valve_pipe"))
            {
                float shotRange = Mathf.Clamp(4.5f + sumP / 38.0f * qualityMultiplier, 4.5f, 8.5f);
                int blastImpact = Mathf.Clamp(Mathf.RoundToInt((18 + (sumP + sumW) * 0.22f) * qualityMultiplier), 18, 65);

                return new CraftedProduct
                {
                    id = "furn_hydro_cannon",
                    productName = $"{qualityTier}·高压自愈喷水炮",
                    category = "水气武器",
                    isAberrantScrap = false,
                    qualityTier = qualityTier,
                    nodeCount = count,
                    primaryElement = "Water",
                    totalP = sumP, totalE = sumE, totalG = sumG, totalW = sumW,
                    hasMajorResonance = hasMajorResonance,
                    flavorDescription = "以生体角质为炮管的高压喷流防卫炮，受触碰时喷出击退水刃。" + resonanceTag,
                    tacticalEffect = $"【高压水刃】射程 {shotRange:F1}m；冲击推力 {blastImpact}；强力击退触碰的敌对生物。",
                    themeColor = themeCol
                };
            }

            // =========================================================================
            // B. 无废品涌现规则：混乱连接衍生趣味生体废料 (数值收敛平衡，防止数值通胀)
            // =========================================================================

            // 杂乱连线时的生体排异反应惩罚 (四元素冲突耗散 18%)
            float entropyPenalty = 0.82f;
            int balancedP = Mathf.RoundToInt(sumP * entropyPenalty);
            int balancedE = Mathf.RoundToInt(sumE * entropyPenalty);
            int balancedG = Mathf.RoundToInt(sumG * entropyPenalty);
            int balancedW = Mathf.RoundToInt(sumW * entropyPenalty);

            // 废料1: 抽搐的痉挛金属团 (电+木 / 弹力击退)
            if (balancedE > 20 && balancedG > 20)
            {
                float knockbackPower = Mathf.Clamp(2.5f + balancedP / 38.0f * qualityMultiplier, 2.5f, 6.0f);
                float stunSec = Mathf.Clamp(1.2f + balancedE / 75.0f * qualityMultiplier, 1.2f, 3.0f);

                return new CraftedProduct
                {
                    id = "scrap_spasm_metal",
                    productName = $"{qualityTier}·抽搐的痉挛金属团",
                    category = "活性杂交废料",
                    isAberrantScrap = true,
                    qualityTier = qualityTier,
                    nodeCount = count,
                    primaryElement = "Electric",
                    totalP = balancedP, totalE = balancedE, totalG = balancedG, totalW = balancedW,
                    hasMajorResonance = hasMajorResonance,
                    flavorDescription = "电肌与金属残件在强行熔铸后形成的畸变瘤块，通体剧烈痉挛颤动。" + resonanceTag,
                    tacticalEffect = $"【弹力奇特废料】摆放在地，怪物踢到时如超强弹簧般反向击退 {knockbackPower:F1}m，附带麻痹 {stunSec:F1} 秒！",
                    themeColor = new Color(0.98f, 0.45f, 0.85f)
                };
            }

            // 废料2: 漏电的烂泥珊瑚块 (电+水 / 麻痹陷阱)
            if (balancedE > 20 && balancedW > 20)
            {
                float trapRadius = Mathf.Clamp(2.0f + balancedW / 45.0f * qualityMultiplier, 2.0f, 4.5f);
                int shockDmg = Mathf.Clamp(Mathf.RoundToInt((12 + balancedE * 0.28f) * qualityMultiplier), 12, 52);

                return new CraftedProduct
                {
                    id = "scrap_mud_coral",
                    productName = $"{qualityTier}·漏电的烂泥珊瑚块",
                    category = "活性杂交废料",
                    isAberrantScrap = true,
                    qualityTier = qualityTier,
                    nodeCount = count,
                    primaryElement = "Water",
                    totalP = balancedP, totalE = balancedE, totalG = balancedG, totalW = balancedW,
                    hasMajorResonance = hasMajorResonance,
                    flavorDescription = "带有电解液的潮湿珊瑚烂泥，散发出焦糊的海洋咸腥味。" + resonanceTag,
                    tacticalEffect = $"【导电陷阱】在半径 {trapRadius:F1}m 内铺开带电泥潭，踩入者持续减速并遭受 {shockDmg} 点麻痹电击！",
                    themeColor = new Color(0.85f, 0.55f, 0.95f)
                };
            }

            // 废料3: 膨胀的气囊疙瘩 (气+水 / 自爆破甲)
            if (balancedP > 20 && balancedW > 20)
            {
                int blastDmg = Mathf.Clamp(Mathf.RoundToInt((25 + balancedP * 0.36f) * qualityMultiplier), 25, 78);
                float blastRadius = Mathf.Clamp(2.0f + balancedP / 55.0f * qualityMultiplier, 2.0f, 4.5f);

                return new CraftedProduct
                {
                    id = "scrap_gas_cyst",
                    productName = $"{qualityTier}·膨胀的气囊疙瘩",
                    category = "活性杂交废料",
                    isAberrantScrap = true,
                    qualityTier = qualityTier,
                    nodeCount = count,
                    primaryElement = "Gas",
                    totalP = balancedP, totalE = balancedE, totalG = balancedG, totalW = balancedW,
                    hasMajorResonance = hasMajorResonance,
                    flavorDescription = "因气压与水压冲突而鼓起的生体肿瘤，表面薄如蝉翼，极度不稳定。" + resonanceTag,
                    tacticalEffect = $"【受触自爆】受撞击立即发生连锁泄压爆炸，对半径 {blastRadius:F1}m 内造成 {blastDmg} 点溅射伤害并留下减速黏液！",
                    themeColor = new Color(0.95f, 0.40f, 0.55f)
                };
            }

            // 废料4: 嘶鸣的过载排气瓣 (气+电 / 诱敌尖啸)
            if (balancedP > 20 && balancedE > 20)
            {
                float tauntRadius = Mathf.Clamp(3.5f + (balancedP + balancedE) / 48.0f * qualityMultiplier, 3.5f, 8.0f);
                int duration = Mathf.Clamp(Mathf.RoundToInt((4.0f + balancedP / 25.0f) * qualityMultiplier), 4, 10);

                return new CraftedProduct
                {
                    id = "scrap_screaming_valve",
                    productName = $"{qualityTier}·嘶鸣的过载排气瓣",
                    category = "活性杂交废料",
                    isAberrantScrap = true,
                    qualityTier = qualityTier,
                    nodeCount = count,
                    primaryElement = "Gas",
                    totalP = balancedP, totalE = balancedE, totalG = balancedG, totalW = balancedW,
                    hasMajorResonance = hasMajorResonance,
                    flavorDescription = "金属瓣膜因电弧灼烧变形，排气时发出刺耳的高频尖叫。" + resonanceTag,
                    tacticalEffect = $"【诱敌尖啸】间歇喷射蒸汽发出尖啸，强制吸引半径 {tauntRadius:F1}m 内怪物注意力，持续 {duration} 秒！",
                    themeColor = new Color(0.95f, 0.65f, 0.25f)
                };
            }

            // 废料5: 活性共生杂交残渣 (通用保底活跃组织)
            int obstacleHp = Mathf.Clamp(Mathf.RoundToInt((90 + balancedG * 1.25f) * qualityMultiplier), 90, 320);
            int slowFactor = Mathf.Clamp(Mathf.RoundToInt((25 + balancedG / 11.0f) * qualityMultiplier), 25, 55);

            return new CraftedProduct
            {
                id = "scrap_symbiotic_sludge",
                productName = $"{qualityTier}·活性共生杂交残渣",
                category = "活性杂交废料",
                isAberrantScrap = true,
                qualityTier = qualityTier,
                nodeCount = count,
                primaryElement = dominantElem,
                totalP = balancedP, totalE = balancedE, totalG = balancedG, totalW = balancedW,
                hasMajorResonance = hasMajorResonance,
                flavorDescription = "多种生体组织强行缠绕的蠕动残渣，生命力异常顽强。" + resonanceTag,
                tacticalEffect = $"【活体路障】摆放作为自愈障碍物，拥有 {obstacleHp} 点耐久度，阻挡敌人并使其移速降低 {slowFactor}%。",
                themeColor = new Color(0.7f, 0.85f, 0.5f)
            };
        }
    }
}
