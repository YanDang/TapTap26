using UnityEngine;

/// <summary>
/// 2. 增生珊瑚管排墙 (furn_coral_wall)
/// - 分类：增生防线 | 主导元素：Wood | 主题色：翠绿增生 (0.29, 0.85, 0.44)
/// - 机制：
///   * A* 寻路硬阻挡：阻断寻路，强行改变敌人行进路线
///   * 荆棘反噬：怪近战攻击珊瑚墙，即刻受到固定 thorns 点破防物理伤害
///   * 活体自愈：脱离受击 3 秒后，每秒自动恢复 regen 点耐久
/// </summary>
public class CoralWallAuraBehavior : BiomechanicalAuraBase
{
    [Header("Coral Stats")]
    public float thornsDamage = 16f;
    public float regenRate = 3f;

    private float outOfCombatTimer = 0f;
    private float regenTimer = 0f;

    protected override void Awake()
    {
        base.Awake();
        CreateRangeRing(1.8f, new Color(0.29f, 0.85f, 0.44f, 0.45f), "Coral_Wall_Ring");

        if (parentFurniture != null)
        {
            parentFurniture.onHitTaken += OnFurnitureHit;
        }
    }

    void OnDestroy()
    {
        if (parentFurniture != null)
        {
            parentFurniture.onHitTaken -= OnFurnitureHit;
        }
    }

    private void OnFurnitureHit(float damage, EnemyController attacker)
    {
        outOfCombatTimer = 3.0f; // 重置脱战计时器

        // 荆棘反噬
        if (attacker != null && attacker.IsAlive && !attacker.isPacified)
        {
            attacker.TakeDamage(thornsDamage, 20f, transform.position);
            if (DamageTextManager.Instance != null)
            {
                DamageTextManager.Instance.ShowText(attacker.transform.position + Vector3.up * 0.7f, $"🌵 荆棘反噬 -{thornsDamage:0}!", new Color(0.29f, 0.85f, 0.44f), 0.11f);
            }
        }
    }

    void Update()
    {
        if (parentFurniture != null && parentFurniture.currentState != FurnitureObject.FurnitureState.Placed)
        {
            if (rangeRing != null && rangeRing.activeSelf) rangeRing.SetActive(false);
            return;
        }

        if (rangeRing != null && !rangeRing.activeSelf) rangeRing.SetActive(true);
        UpdateRingPulse(2.0f, 0.04f);

        if (outOfCombatTimer > 0f)
        {
            outOfCombatTimer -= Time.deltaTime;
        }
        else if (parentFurniture != null && parentFurniture.currentDurability < parentFurniture.maxDurability)
        {
            // 活体自愈
            regenTimer += Time.deltaTime;
            if (regenTimer >= 1.0f)
            {
                regenTimer = 0f;
                parentFurniture.currentDurability = Mathf.Min(parentFurniture.maxDurability, parentFurniture.currentDurability + regenRate);

                if (DamageTextManager.Instance != null)
                {
                    DamageTextManager.Instance.ShowText(transform.position + Vector3.up * 0.5f, $"+{regenRate:0} 珊瑚自愈", new Color(0.35f, 1f, 0.5f), 0.09f);
                }
            }
        }
    }
}
