using UnityEngine;
using GameJam;

/// <summary>
/// 2. 增生珊瑚管排墙 (furn_coral_wall)
/// - 分类：增生防线 | 主导元素：Wood | 主题色：翠绿增生 (0.29, 0.85, 0.44)
/// - 机制：
///   * A* 寻路硬阻挡：阻断寻路，强行改变敌人行进路线
///   * 荆棘反噬：怪近战攻击珊瑚墙，即刻受到固定 thorns 点破防物理伤害
///   * 活体自愈：按配置间隔恢复耐久，潮湿时加快恢复
/// </summary>
public class CoralWallAuraBehavior : BiomechanicalAuraBase
{
    [Header("Coral Stats")]
    public float thornsDamage = 16f;
    public float regenRate = 1f;
    public float regenInterval = 4f;
    public float moistRegenInterval = 2f;

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

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (parentFurniture != null)
        {
            parentFurniture.onHitTaken -= OnFurnitureHit;
        }
    }

    private void OnFurnitureHit(float damage, EnemyController attacker)
    {
        // 荆棘反噬 (来自 gamejam-integration)
        if (attacker != null && attacker.IsAlive && !attacker.isPacified)
        {
            attacker.TakeDamage(thornsDamage, 20f, transform.position);
            Vfx.Spawn("VFX_ParrySpark", attacker.transform.position + Vector3.up * 0.7f, Random.Range(0f, 360f), 0.9f, false, null, new Color(0.29f, 0.85f, 0.44f));
            Sfx.Play("sfx_parry", 0.6f);

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

        if (IsCombatPaused)
        {
            return;
        }

        // 对齐 gamejam-integration：珊瑚管排墙一直慢慢自愈，在水汽里自愈速度翻倍
        if (parentFurniture != null && parentFurniture.currentDurability < parentFurniture.maxDurability)
        {
            bool isMoist = WaterTowerAuraBehavior.IsPositionMoist(transform.position);
            float interval = Mathf.Max(0.01f, isMoist ? moistRegenInterval : regenInterval);
            regenTimer += Time.deltaTime;
            if (regenTimer >= interval)
            {
                regenTimer -= interval;
                parentFurniture.currentDurability = Mathf.Min(parentFurniture.maxDurability, parentFurniture.currentDurability + regenRate);

                if (DamageTextManager.Instance != null)
                {
                    string label = (isMoist ? "💧 潮湿增生 +" : "🌿 珊瑚自愈 +") + regenRate.ToString("0.#");
                    DamageTextManager.Instance.ShowText(transform.position + Vector3.up * 0.5f, label, new Color(0.35f, 1f, 0.5f), 0.09f);
                }
            }
        }
    }
}
