using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 基础材料实体类：
/// 包含部位适配标签（如【块】、【棒/棍】、【板】）、物理特质（硬度、韧性、重量）与元素属性（火/冰/雷/毒等）。
/// </summary>
[Serializable]
public class MaterialItem
{
    [Header("Basic Info")]
    public string id = "mat_default";
    public string materialName = "普通材料";
    public string description = "";

    [Header("Part Adaptability (适配部位标签，如：块、棒、板)")]
    public List<string> adaptableParts = new List<string>();

    [Header("Physical Attributes (物理三维特质)")]
    [Tooltip("硬度 (1~100): 主导武器基础伤害、家具抗碎")]
    public float hardness = 30f;
    [Tooltip("韧性 (1~100): 主导耐久上限、攻击减震回弹")]
    public float toughness = 30f;
    [Tooltip("重量 (kg): 主导攻击间隔、家具滑动速度与挥动耗体")]
    public float weight = 2.0f;

    [Header("Elemental Attributes (元素属性)")]
    [Tooltip("元素类型: None / fire / ice / thunder / poison")]
    public string element = "None";
    [Tooltip("元素强度/点数")]
    public float elementPotency = 0f;

    [Header("Visual")]
    public Color visualColor = Color.white;

    /// <summary>
    /// 判断是否适配某个指定部位标签（兼容“棒”与“棍”）
    /// </summary>
    public bool HasPartTag(string tag)
    {
        if (adaptableParts == null) return false;
        foreach (var p in adaptableParts)
        {
            if (string.Equals(p, tag, StringComparison.OrdinalIgnoreCase))
                return true;
            // 兼容性判定：棍与棒视为同一部位
            if ((tag == "棒" || tag == "棍") && (p == "棒" || p == "棍"))
                return true;
        }
        return false;
    }

    /// <summary>
    /// 预设材料库：提供典型怪物掉落器官与物理材料
    /// </summary>
    public static List<MaterialItem> CreateDefaultMaterialLibrary()
    {
        return new List<MaterialItem>
        {
            // === 块 (Block) 部位材料 ===
            new MaterialItem
            {
                id = "mat_fire_core",
                materialName = "熔火炽晶",
                description = "炽炎猎犬胸腔中剥取的炽热结晶，硬度拔群。",
                adaptableParts = new List<string> { "块" },
                hardness = 85f,
                toughness = 30f,
                weight = 5.2f,
                element = "fire",
                elementPotency = 45f,
                visualColor = new Color(0.95f, 0.35f, 0.1f)
            },
            new MaterialItem
            {
                id = "mat_frost_fang",
                materialName = "极地尖牙",
                description = "寒霜魔兽的冰晶獠牙，锋锐致冷，但韧性略脆。",
                adaptableParts = new List<string> { "块" },
                hardness = 75f,
                toughness = 22f,
                weight = 3.6f,
                element = "ice",
                elementPotency = 35f,
                visualColor = new Color(0.45f, 0.85f, 1f)
            },
            new MaterialItem
            {
                id = "mat_thunder_horn",
                materialName = "奔雷晶角",
                description = "电弧缠绕的雷兽破魔角，轻巧且极具破防力。",
                adaptableParts = new List<string> { "块" },
                hardness = 68f,
                toughness = 45f,
                weight = 2.8f,
                element = "thunder",
                elementPotency = 40f,
                visualColor = new Color(1f, 0.9f, 0.2f)
            },
            new MaterialItem
            {
                id = "mat_dark_iron",
                materialName = "深渊黑铁块",
                description = "沉重厚实的冷钢生铁，硬度与破甲极佳，无元素属性。",
                adaptableParts = new List<string> { "块" },
                hardness = 70f,
                toughness = 40f,
                weight = 5.8f,
                element = "None",
                elementPotency = 0f,
                visualColor = new Color(0.3f, 0.35f, 0.4f)
            },

            // === 棒 (Stick/Rod) 部位材料 ===
            new MaterialItem
            {
                id = "mat_frost_spine",
                materialName = "极地柔韧脊骨",
                description = "霜翼飞兽的修长骨节，韧性极高，大幅优化攻击后摇与攻速。",
                adaptableParts = new List<string> { "棒" },
                hardness = 32f,
                toughness = 88f,
                weight = 1.4f,
                element = "ice",
                elementPotency = 30f,
                visualColor = new Color(0.6f, 0.9f, 0.98f)
            },
            new MaterialItem
            {
                id = "mat_flame_bone",
                materialName = "焦黑炎骨",
                description = "浸染烈焰能量的硬兽骨，硬韧兼顾，握持温热。",
                adaptableParts = new List<string> { "棒" },
                hardness = 48f,
                toughness = 65f,
                weight = 2.2f,
                element = "fire",
                elementPotency = 25f,
                visualColor = new Color(0.7f, 0.25f, 0.15f)
            },
            new MaterialItem
            {
                id = "mat_oak_rod",
                materialName = "质感橡木棒",
                description = "老质坚实橡木打磨而成的握杆，手感均衡温润。",
                adaptableParts = new List<string> { "棒", "板" },
                hardness = 35f,
                toughness = 50f,
                weight = 2.0f,
                element = "None",
                elementPotency = 0f,
                visualColor = new Color(0.65f, 0.45f, 0.25f)
            },
            new MaterialItem
            {
                id = "mat_thunder_tendon",
                materialName = "雷光韧筋棒",
                description = "带有天然高弹回缩特性的雷兽筋杆，能极大缩短攻击间隔。",
                adaptableParts = new List<string> { "棒" },
                hardness = 28f,
                toughness = 92f,
                weight = 1.2f,
                element = "thunder",
                elementPotency = 35f,
                visualColor = new Color(0.9f, 0.85f, 0.3f)
            },

            // === 板 (Board) 部位材料 ===
            new MaterialItem
            {
                id = "mat_earth_carapace",
                materialName = "玄岩厚重背甲板",
                description = "重甲石魔的平整坚壳，极其沉重，防震防撞耐久高。",
                adaptableParts = new List<string> { "板" },
                hardness = 78f,
                toughness = 60f,
                weight = 16.0f,
                element = "None",
                elementPotency = 0f,
                visualColor = new Color(0.4f, 0.45f, 0.5f)
            },
            new MaterialItem
            {
                id = "mat_poplar_board",
                materialName = "轻杨木板",
                description = "质地轻盈的木板，滑动阻力极低，适合做滑得快的家具。",
                adaptableParts = new List<string> { "板" },
                hardness = 18f,
                toughness = 30f,
                weight = 3.5f,
                element = "None",
                elementPotency = 0f,
                visualColor = new Color(0.85f, 0.75f, 0.6f)
            }
        };
    }
}
