using UnityEngine;

namespace BiomechanicalCrafting
{
    public sealed class CraftedProductStats
    {
        public float durability;
        public float radius;
        public int damage;
        public int percent;
        public float duration;
        public float force;
        public float kickDamage;
        public float kickGuardBreak;
        public float regenAmount = 1f;
        public float regenInterval = 4f;
        public float moistRegenInterval = 2f;
        public float exposureSeconds = 2.5f;
        public float healAmount = 35f;

        // 采集门动态推演属性（绑定一笔画投入材料数与元素总和：投入材料越多，怪物越多，材料也越多）
        public int minGatherCount => damage > 0 ? damage : 3;
        public int maxGatherCount => force > 0 ? Mathf.RoundToInt(force) : 5;
        public int extraMonsters => percent;
        public int monsterReduction => -percent;

        public static float QualityMultiplier(int nodeCount)
        {
            return nodeCount <= 3 ? 1f : nodeCount == 4 ? 1.15f : 1.28f;
        }

        public static CraftedProductStats Calculate(CraftedProduct product)
        {
            float quality = QualityMultiplier(product.nodeCount);
            int pressure = product.totalP, electricity = product.totalE, growth = product.totalG, water = product.totalW;
            var stats = new CraftedProductStats();
            stats.durability = Mathf.Clamp(Mathf.RoundToInt((120f + growth * 1.5f) * quality), 120, 420);
            stats.kickDamage = (60f + pressure * 0.8f) * quality;
            stats.kickGuardBreak = (80f + pressure * 0.5f) * quality;
            switch (product.id)
            {
                case "furn_eel_generator":
                    stats.radius = Mathf.Clamp(2f + electricity / 55f * quality, 2f, 5f);
                    stats.damage = Mathf.Clamp(Mathf.RoundToInt((18 + electricity * 0.35f + pressure * 0.12f) * quality), 18, 75);
                    break;
                case "furn_coral_wall":
                    stats.durability = Mathf.Clamp(Mathf.RoundToInt((150 + growth * 2f) * quality), 150, 480);
                    stats.damage = Mathf.Clamp(Mathf.RoundToInt((10 + electricity * 0.18f + growth * 0.16f) * quality), 10, 42);
                    stats.radius = 1.8f;
                    break;
                case "furn_pneumatic_bed":
                    stats.durability = Mathf.Clamp(Mathf.RoundToInt((90 + growth * 1.2f) * quality), 90, 300);
                    stats.radius = Mathf.Clamp(2.5f + pressure / 45f * quality, 2.5f, 5.5f);
                    stats.percent = Mathf.Clamp(Mathf.RoundToInt((12 + pressure / 14f) * quality), 12, 35);
                    break;
                case "furn_water_tower":
                    stats.radius = Mathf.Clamp(2.5f + water / 40f * quality, 2.5f, 6f);
                    stats.percent = Mathf.Clamp(Mathf.RoundToInt((25 + water / 9f) * quality), 25, 60);
                    break;
                case "furn_bio_incubator":
                    stats.radius = Mathf.Clamp(3f + (water + electricity) / 50f * quality, 3f, 7.5f);
                    stats.duration = Mathf.Clamp((4f + growth / 25f) * quality, 4f, 8f);
                    break;
                case "furn_hydro_cannon":
                    stats.damage = Mathf.RoundToInt(32f * quality);
                    stats.radius = Mathf.Clamp(4.5f + pressure / 38f * quality, 4.5f, 8.5f);
                    stats.force = Mathf.Clamp(Mathf.RoundToInt((18 + (pressure + water) * 0.22f) * quality), 18, 65);
                    break;
                case "scrap_spasm_metal":
                    stats.force = Mathf.Clamp(4.5f + pressure / 30f * quality, 4.5f, 7.5f);
                    stats.duration = Mathf.Clamp(1.2f + electricity / 75f * quality, 1.2f, 3f);
                    stats.damage = Mathf.RoundToInt(30f * quality);
                    break;
                case "scrap_mud_coral":
                    stats.radius = Mathf.Clamp(2f + water / 45f * quality, 2f, 4.5f);
                    stats.damage = Mathf.Clamp(Mathf.RoundToInt((12 + electricity * 0.28f) * quality), 12, 52);
                    break;
                case "scrap_gas_cyst":
                    stats.radius = Mathf.Clamp(2f + pressure / 55f * quality, 2f, 4.5f);
                    stats.damage = Mathf.Clamp(Mathf.RoundToInt((25 + pressure * 0.36f) * quality), 25, 78);
                    break;
                case "scrap_screaming_valve":
                    stats.radius = Mathf.Clamp(3.5f + (pressure + electricity) / 48f * quality, 3.5f, 8f);
                    stats.duration = Mathf.Clamp(Mathf.RoundToInt((4f + pressure / 25f) * quality), 4, 10);
                    break;
                case "furn_gather_door":
                    // 给的材料越多，出现的怪物越多，温室材料萌发也越多！
                    // 初始基准（nodeCount <= 2）：0额外守卫，3~5材料
                    int extraMaterials = Mathf.Max(0, product.nodeCount - 2);
                    int extraMonsters = extraMaterials;
                    int bonusYield = extraMaterials * 3;
                    if (extraMaterials > 0)
                    {
                        bonusYield += Mathf.RoundToInt((growth + water) / 50f);
                        if (pressure + electricity > 120) extraMonsters += 1;
                    }
                    int minG = extraMaterials == 0 ? 3 : Mathf.Clamp(3 + bonusYield, 3, 13);
                    int maxG = extraMaterials == 0 ? 5 : Mathf.Clamp(5 + bonusYield + extraMaterials, 5, 16);

                    stats.damage = minG;                             // minGatherCount
                    stats.force = maxG;                              // maxGatherCount
                    stats.percent = Mathf.Clamp(extraMonsters, 0, 4); // extraMonsters
                    break;
                case "scrap_symbiotic_sludge":
                case "scrap_hybrid_residue":
                    stats.durability = Mathf.Clamp(Mathf.RoundToInt((90 + growth * 1.25f) * quality), 90, 320);
                    stats.percent = Mathf.Clamp(Mathf.RoundToInt((25 + growth / 11f) * quality), 25, 55);
                    stats.radius = 1.5f;
                    break;
            }
            return stats;
        }

        public static string Describe(CraftedProduct product)
        {
            var stats = Calculate(product);
            string effect;
            switch (product.id)
            {
                case "furn_eel_generator": effect = $"【脉冲供电网】供电半径 4.5m；麻痹范围 {stats.radius:F1}m；电弧伤害 {stats.damage}。"; break;
                case "furn_coral_wall": effect = $"【活体掩体】荆棘反伤 {stats.damage}；每 {stats.regenInterval:0.#} 秒恢复 +{stats.regenAmount:0.#}，潮湿时每 {stats.moistRegenInterval:0.#} 秒恢复 +{stats.regenAmount:0.#}。"; break;
                case "furn_pneumatic_bed": effect = $"【气压助推】范围 {stats.radius:F1}m；移速提升 {stats.percent}%。"; break;
                case "furn_water_tower": effect = $"【潮润浸染】喷淋半径 {stats.radius:F1}m；减速 {stats.percent}% 并导电。"; break;
                case "furn_bio_incubator": effect = $"【生体共鸣诱驯】范围 {stats.radius:F1}m；累计接触 {stats.exposureSeconds:0.#} 秒后安抚 {stats.duration:0.#} 秒。"; break;
                case "furn_hydro_cannon": effect = $"【高压水刃】射程 {stats.radius:F1}m；水刃伤害 {stats.damage}；冲击推力 {stats.force:0}。"; break;
                case "furn_gather_door":
                    string extraInfo = stats.extraMonsters > 0 ? $"守卫涌现 +{stats.extraMonsters} 只；" : "标准守卫；";
                    return $"【空间裂隙温室】投入材料 {product.nodeCount} 份；{extraInfo}温室萌发 {stats.minGatherCount}~{stats.maxGatherCount} 份材料。门不可拆除或摧毁。";
                case "scrap_spasm_metal": effect = $"【弹簧陷阱】伤害 {stats.damage}；弹射力度 {stats.force:F1}；麻痹 {stats.duration:F1} 秒。"; break;
                case "scrap_mud_coral": effect = $"【导电陷阱】半径 {stats.radius:F1}m；电击伤害 {stats.damage}，减速 40%。"; break;
                case "scrap_gas_cyst": effect = $"【受触自爆】半径 {stats.radius:F1}m；爆炸伤害 {stats.damage}。"; break;
                case "scrap_screaming_valve": effect = $"【诱敌尖啸】半径 {stats.radius:F1}m；尖啸 {stats.duration:0.#} 秒，间歇 5 秒。"; break;
                default: effect = $"【活体路障】减速 {stats.percent}%；破裂时为 4m 内玩家恢复 {stats.healAmount:0} 生命。"; break;
            }
            return effect + $" 耐久 {stats.durability:0}；踢击伤害 {stats.kickDamage:0.#}。";
        }
    }
}
