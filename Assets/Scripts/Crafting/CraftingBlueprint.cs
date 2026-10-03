using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 合成计算推演结果
/// </summary>
[Serializable]
public class CraftingResult
{
    public bool isValid = false;
    public string statusMessage = "";

    // 产物通用属性
    public string productName = "";
    public string category = "weapon"; // "weapon" 或 "furniture"

    // 元素属性计算结果（取结合之后的最高元素值）
    public string primaryElement = "None";
    public float elementPotency = 0f;
    public Dictionary<string, float> elementBreakdown = new Dictionary<string, float>();

    // 武器数值推演结果
    public float damage = 0f;
    public float maxDurability = 0f;
    public float attackInterval = 0f;
    public float guardBreakPower = 0f;
    public float staminaCost = 0f;
    public float totalWeight = 0f;
    public Color visualColor = Color.white;

    // 家具附加属性（若为家具）
    public float slideSpeed = 0f;
    public float heldDamage = 0f;
    public float heldInterval = 0f;
    public float heldGuardBreak = 0f;
    public float moveSpeedPenalty = 0f;

    // 生成的武器数据实例（可直接装备给 PlayerController）
    public WeaponData generatedWeaponData;
}

/// <summary>
/// 蓝图配方类：
/// 负责校验输入材料是否满足部位限制（如剑至少需要【块】和【棒】），
/// 并按物理特质与元素最高值规则计算产物各项属性。
/// </summary>
[Serializable]
public class CraftingBlueprint
{
    public string id = "sword";
    public string blueprintName = "双手长剑";
    public string category = "weapon"; // "weapon" 或 "furniture"
    public string description = "标准利刃，至少需要一个【块】(刃部)和一个【棒】(握把)。";

    // 限制配方：必须包含的部位标签
    public List<string> requiredTags = new List<string> { "块", "棒" };

    public int minMaterials = 2;
    public int maxMaterials = 3;

    /// <summary>
    /// 校验输入材料是否满足蓝图配方条件
    /// </summary>
    public bool Validate(List<MaterialItem> materials, out string message)
    {
        if (materials == null || materials.Count < minMaterials)
        {
            message = $"至少需要放入 {minMaterials} 个材料（当前: {materials?.Count ?? 0} 个）";
            return false;
        }

        if (materials.Count > maxMaterials)
        {
            message = $"最多只能够选择 {maxMaterials} 个材料（当前: {materials.Count} 个）";
            return false;
        }

        // 逐一检查蓝图要求的每个必要部位标签
        foreach (var requiredTag in requiredTags)
        {
            bool hasTag = false;
            foreach (var mat in materials)
            {
                if (mat != null && mat.HasPartTag(requiredTag))
                {
                    hasTag = true;
                    break;
                }
            }

            if (!hasTag)
            {
                message = $"配方不满足：缺少具有【{requiredTag}】适配特性的材料！";
                return false;
            }
        }

        message = $"配方条件满足！已包含所需部位: {string.Join(" + ", requiredTags)}";
        return true;
    }

    /// <summary>
    /// 计算推演合成结果
    /// </summary>
    public CraftingResult Evaluate(List<MaterialItem> materials)
    {
        CraftingResult result = new CraftingResult();
        result.category = this.category;

        if (!Validate(materials, out string msg))
        {
            result.isValid = false;
            result.statusMessage = msg;
            return result;
        }

        result.isValid = true;
        result.statusMessage = msg;

        // ====================================================================
        // 1. 元素属性计算：各元素累加后，最终产物的元素属性取结合之后的最高值！
        // ====================================================================
        Dictionary<string, float> elementSums = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        foreach (var mat in materials)
        {
            if (mat == null) continue;
            string elem = string.IsNullOrEmpty(mat.element) ? "None" : mat.element;
            if (elem != "None" && mat.elementPotency > 0)
            {
                if (!elementSums.ContainsKey(elem))
                    elementSums[elem] = 0f;
                elementSums[elem] += mat.elementPotency;
            }
        }

        result.elementBreakdown = elementSums;
        string topElement = "None";
        float maxPotency = 0f;

        foreach (var kvp in elementSums)
        {
            if (kvp.Value > maxPotency)
            {
                maxPotency = kvp.Value;
                topElement = kvp.Key;
            }
        }

        result.primaryElement = topElement;
        result.elementPotency = maxPotency;

        // ====================================================================
        // 2. 部位与物理属性推导 (硬度、韧性、重量)
        // ====================================================================
        float totalWeight = 0f;
        MaterialItem primaryBlock = null;
        MaterialItem primaryStick = null;
        MaterialItem primaryBoard = null;
        List<MaterialItem> extraMaterials = new List<MaterialItem>();

        foreach (var mat in materials)
        {
            totalWeight += mat.weight;

            if (mat.HasPartTag("块") && (primaryBlock == null || mat.hardness > primaryBlock.hardness))
            {
                if (primaryBlock != null) extraMaterials.Add(primaryBlock);
                primaryBlock = mat;
            }
            else if (mat.HasPartTag("棒") && (primaryStick == null || mat.toughness > primaryStick.toughness))
            {
                if (primaryStick != null) extraMaterials.Add(primaryStick);
                primaryStick = mat;
            }
            else if (mat.HasPartTag("板") && (primaryBoard == null || mat.hardness > primaryBoard.hardness))
            {
                if (primaryBoard != null) extraMaterials.Add(primaryBoard);
                primaryBoard = mat;
            }
            else
            {
                extraMaterials.Add(mat);
            }
        }

        float extraHardness = 0f;
        float extraToughness = 0f;
        foreach (var ext in extraMaterials)
        {
            extraHardness += ext.hardness;
            extraToughness += ext.toughness;
        }

        result.totalWeight = totalWeight;

        // 混合视觉颜色
        Color blendedColor = Color.white;
        if (materials.Count > 0)
        {
            blendedColor = materials[0].visualColor;
            for (int i = 1; i < materials.Count; i++)
            {
                blendedColor = Color.Lerp(blendedColor, materials[i].visualColor, 0.5f);
            }
        }
        result.visualColor = blendedColor;

        // ====================================================================
        // 3. 武器属性换算 (若为武器蓝图)
        // ====================================================================
        if (category == "weapon")
        {
            float blockH = primaryBlock != null ? primaryBlock.hardness : 30f;
            float stickH = primaryStick != null ? primaryStick.hardness : 20f;
            float blockT = primaryBlock != null ? primaryBlock.toughness : 25f;
            float stickT = primaryStick != null ? primaryStick.toughness : 50f;

            // 伤害公式：刃部硬度主导 (85%) + 握柄硬度 (15%) + 额外辅料硬度加成 (20%)
            result.damage = Mathf.Round(blockH * 0.85f + stickH * 0.15f + extraHardness * 0.20f);

            // 耐久上限：刃部与握柄韧性综合决定 + 基础耐久
            result.maxDurability = Mathf.Round((blockT * 0.65f + stickT * 0.35f + extraToughness * 0.30f) * 5.0f + 30f);

            // 攻击间隔 (秒)：基础 0.95s + 重量延迟 - 握把回弹
            float interval = 0.95f + (totalWeight * 0.04f) - (stickT * 0.005f);
            result.attackInterval = Mathf.Clamp((float)Math.Round(interval, 2), 0.50f, 2.00f);

            // 削韧破防力：基础伤害的 80%
            result.guardBreakPower = Mathf.Round(result.damage * 0.8f);

            // 挥舞耐力消耗：基础 15 + 重量
            result.staminaCost = Mathf.Round(15f + totalWeight * 1.2f);

            // 动态命名
            string elemPrefix = GetElementPrefix(result.primaryElement);
            string blockPrefix = primaryBlock != null ? primaryBlock.materialName.Substring(0, Mathf.Min(2, primaryBlock.materialName.Length)) : "";
            string stickPrefix = primaryStick != null ? primaryStick.materialName.Substring(0, Mathf.Min(2, primaryStick.materialName.Length)) : "";
            result.productName = $"{elemPrefix}{blockPrefix}{stickPrefix}{blueprintName}";

            // 封装为真正的 WeaponData 对象供战斗直接使用
            result.generatedWeaponData = new WeaponData
            {
                instanceId = "wpn_crafted_" + Guid.NewGuid().ToString().Substring(0, 6),
                weaponName = result.productName,
                category = "weapon",
                damage = result.damage,
                currentDurability = result.maxDurability,
                maxDurability = result.maxDurability,
                attackInterval = result.attackInterval,
                guardBreakPower = result.guardBreakPower,
                staminaCost = result.staminaCost,
                primaryElement = result.primaryElement,
                elementPotency = result.elementPotency
            };
        }
        // ====================================================================
        // 4. 家具属性换算 (若为家具蓝图)
        // ====================================================================
        else
        {
            float boardH = primaryBoard != null ? primaryBoard.hardness : 40f;
            float blockH = primaryBlock != null ? primaryBlock.hardness : 30f;
            float boardT = primaryBoard != null ? primaryBoard.toughness : 40f;

            result.maxDurability = Mathf.Round((boardT * 0.6f + blockH * 0.4f + extraToughness * 0.3f) * 8f + 100f);
            result.slideSpeed = Mathf.Clamp((float)Math.Round(380f - totalWeight * 8.5f, 1), 80f, 420f);

            // 手持家具作为武器的极值
            result.heldDamage = Mathf.Round(15f + totalWeight * 3.5f + blockH * 0.8f);
            result.heldInterval = Mathf.Clamp((float)Math.Round(0.85f + totalWeight * 0.04f, 2), 1.0f, 2.5f);
            result.heldGuardBreak = Mathf.Round(result.heldDamage * 1.5f);
            result.moveSpeedPenalty = Mathf.Clamp((float)Math.Round(totalWeight * 0.012f, 2), 0.05f, 0.45f);

            string elemPrefix = GetElementPrefix(result.primaryElement);
            result.productName = $"{elemPrefix}{blueprintName}";
        }

        return result;
    }

    private string GetElementPrefix(string elem)
    {
        switch (elem.ToLower())
        {
            case "fire": return "【烈焰】";
            case "ice": return "【寒霜】";
            case "thunder": return "【奔雷】";
            case "poison": return "【剧毒】";
            default: return "";
        }
    }

    /// <summary>
    /// 预设蓝图库
    /// </summary>
    public static List<CraftingBlueprint> CreateDefaultBlueprints()
    {
        return new List<CraftingBlueprint>
        {
            new CraftingBlueprint
            {
                id = "sword",
                blueprintName = "双手长剑",
                category = "weapon",
                description = "标准斩击武器。至少需要一个【块】(刃锋)和一个【棒】(握柄)。",
                requiredTags = new List<string> { "块", "棒" }
            },
            new CraftingBlueprint
            {
                id = "warhammer",
                blueprintName = "重型战锤",
                category = "weapon",
                description = "钝击巨兵。至少需要一个【块】(重锤头)和一个【棒】(力臂杠杆)。",
                requiredTags = new List<string> { "块", "棒" }
            },
            new CraftingBlueprint
            {
                id = "table",
                blueprintName = "实木长餐桌",
                category = "furniture",
                description = "可放置阻挡，也可被踢出冲撞或举起砸怪！至少需要一个【板】和一个【棒】。",
                requiredTags = new List<string> { "板", "棒" }
            }
        };
    }
}
