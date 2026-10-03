using System;
using UnityEngine;

/// <summary>
/// 武器战斗属性定义（基于 COMBAT_CRAFTING_INTEGRATION_SPEC 规范）：
/// 包含基础伤害、耐久度、攻击间隔、削韧破防力、耐力消耗及元素克制属性。
/// </summary>
[Serializable]
public class WeaponData
{
    [Header("Weapon Identity")]
    public string instanceId = "wpn_default";
    public string weaponName = "铁制单手剑";
    public string category = "weapon";

    [Header("Combat Stats")]
    [Tooltip("单次斩击伤害")]
    public float damage = 25f;
    [Tooltip("当前耐久度")]
    public float currentDurability = 360f;
    [Tooltip("最大耐久度")]
    public float maxDurability = 360f;
    [Tooltip("攻击间隔节奏 (秒)")]
    public float attackInterval = 0.75f;
    [Tooltip("削韧破防力 (Guard Break Power)")]
    public float guardBreakPower = 30f;
    [Tooltip("单次挥舞耐力消耗")]
    public float staminaCost = 18f;

    [Header("Elemental Stats")]
    [Tooltip("主要元素 (None / fire / ice / thunder / poison)")]
    public string primaryElement = "None";
    [Tooltip("元素潜能/倍率")]
    public float elementPotency = 0f;

    [Header("Visuals")]
    public Sprite weaponSprite;
    public Sprite slashSprite;

    /// <summary>
    /// 创建默认初始武器：铁制佩剑
    /// </summary>
    public static WeaponData CreateDefault()
    {
        return new WeaponData
        {
            instanceId = "wpn_default_sword",
            weaponName = "铁制佩剑",
            damage = 25f,
            attackInterval = 0.75f,
            guardBreakPower = 30f,
            currentDurability = 300f,
            maxDurability = 300f,
            staminaCost = 18f,
            primaryElement = "None"
        };
    }

    /// <summary>
    /// 创建规范指定的武器：炽炎双手长剑 (Spec Section II.2: Damage 78, Durability 360, Interval 1.08, GuardBreak 65, Stamina 22)
    /// </summary>
    public static WeaponData CreateFireGreatsword()
    {
        return new WeaponData
        {
            instanceId = "wpn_inst_sword_1001",
            weaponName = "炽炎双手长剑",
            damage = 78f,
            attackInterval = 1.08f,
            guardBreakPower = 65f,
            currentDurability = 360f,
            maxDurability = 360f,
            staminaCost = 22f,
            primaryElement = "fire",
            elementPotency = 45f
        };
    }
}
