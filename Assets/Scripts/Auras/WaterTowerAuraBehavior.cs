using UnityEngine;

/// <summary>
/// 4. 脉动造水循环塔 (furn_water_tower)
/// - 分类：潮润设施 | 主导元素：Water | 主题色：潮润青蓝 (0.22, 0.74, 0.97)
/// - 机制：
///   * 持续造水浸染：向地面脉动喷洒活水，踩入敌人移速降低 slowPercent%
///   * 【元素协同·感电暴击】：若水渍地表与【电鳗电机】电网重叠，转为带电水潭，
///     敌人受到电击伤害提升 200%（造成3倍伤害），且破防/瘫痪蓄积加倍！
/// </summary>
public class WaterTowerAuraBehavior : BiomechanicalAuraBase
{
    [Header("Water Spray Stats")]
    public float sprayRadius = 3.8f;
    public int slowPercent = 45;

    [Header("Synergy State")]
    public bool isElectrifiedPuddle = false;
    private float pulseTimer = 0f;
    private float damageTickTimer = 0f;

    protected override void Awake()
    {
        base.Awake();
        CreateRangeRing(sprayRadius, new Color(0.22f, 0.74f, 0.97f, 0.45f), "Water_Spray_Puddle");
    }

    void Update()
    {
        if (parentFurniture != null && parentFurniture.currentState != FurnitureObject.FurnitureState.Placed)
        {
            if (rangeRing != null && rangeRing.activeSelf) rangeRing.SetActive(false);
            return;
        }

        if (rangeRing != null && !rangeRing.activeSelf) rangeRing.SetActive(true);
        UpdateRingPulse(2.8f, 0.05f);

        // 实时检测是否处于电鳗电机供电网中（触发水电协同）
        bool nowPowered = EelGeneratorAuraBehavior.IsPositionPowered(transform.position);
        if (nowPowered != isElectrifiedPuddle)
        {
            isElectrifiedPuddle = nowPowered;
            if (rangeRingRenderer != null)
            {
                // 水电协同：水潭呈现高光青黄电芒
                rangeRingRenderer.color = isElectrifiedPuddle 
                    ? new Color(0.35f, 0.95f, 1f, 0.75f) 
                    : new Color(0.22f, 0.74f, 0.97f, 0.45f);
            }
        }

        // 脉动喷水周期
        pulseTimer += Time.deltaTime;
        if (pulseTimer >= 2.0f)
        {
            pulseTimer = 0f;
            TriggerPulseSpray();
        }

        // 持续对水洼内的敌人进行减速与感电暴击判定
        damageTickTimer += Time.deltaTime;
        if (damageTickTimer >= 0.8f)
        {
            damageTickTimer = 0f;
            ApplyPuddleEffects();
        }
    }

    private void TriggerPulseSpray()
    {
        if (DamageTextManager.Instance != null && isElectrifiedPuddle)
        {
            DamageTextManager.Instance.ShowText(transform.position + Vector3.up * 0.7f, "⚡ 水电协同·脉动带电水潭!", new Color(0.3f, 0.9f, 1f), 0.09f);
        }
    }

    private void ApplyPuddleEffects()
    {
        var enemies = EnemyController.AllEnemies;
        for (int i = 0; i < enemies.Count; i++)
        {
            var e = enemies[i];
            if (e != null && e.IsAlive && !e.isPacified)
            {
                float d = Vector2.Distance(transform.position, e.transform.position);
                if (d <= sprayRadius)
                {
                    // 1. 基础水渍减速
                    e.ApplySlow(slowPercent, 1.2f);

                    // 2. 水电协同【感电暴击】：电伤 +200%（3倍）且破防翻倍
                    if (isElectrifiedPuddle)
                    {
                        float baseShock = 22f;
                        float critShock = baseShock * 3.0f; // +200% 提升
                        e.TakeDamage(critShock, 40f, transform.position); // 双倍破防
                        e.ApplyParalysis(0.8f);

                        if (DamageTextManager.Instance != null)
                        {
                            DamageTextManager.Instance.ShowText(e.transform.position + Vector3.up * 0.6f, $"⚡ 感电暴击 -{critShock:0}!", new Color(1f, 0.85f, 0.15f), 0.12f);
                        }
                    }
                }
            }
        }
    }
}
