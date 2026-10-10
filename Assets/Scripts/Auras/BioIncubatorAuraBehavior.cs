using UnityEngine;

/// <summary>
/// 5. 小型生体驯化培养箱 (furn_bio_incubator)
/// - 分类：智能设施 | 主导元素：Water | 主题色：深邃天蓝 (0.20, 0.65, 0.92)
/// - 机制：
///   * 心智诱引：持续释放脉冲共鸣波，吸引附近 lureRange 内怪物向培养箱聚集（聚怪效果）
///   * 阵营逆转/瘫痪：每 3 秒对范围内的非 Boss 怪物进行概率判定 (pacifyChance%)：
///     成功则使其停止攻击玩家，转为反戈攻击同类怪物，持续 8 秒！
/// </summary>
public class BioIncubatorAuraBehavior : BiomechanicalAuraBase
{
    [Header("Incubator Stats")]
    public float lureRange = 5.2f;
    public int pacifyChance = 45;

    private float lureTimer = 0f;
    private float pacifyCheckTimer = 0f;

    protected override void Awake()
    {
        base.Awake();
        CreateRangeRing(lureRange, new Color(0.20f, 0.65f, 0.92f, 0.40f), "Mind_Resonance_Ring");
    }

    void Update()
    {
        if (parentFurniture != null && parentFurniture.currentState != FurnitureObject.FurnitureState.Placed)
        {
            if (rangeRing != null && rangeRing.activeSelf) rangeRing.SetActive(false);
            return;
        }

        if (rangeRing != null && !rangeRing.activeSelf) rangeRing.SetActive(true);
        UpdateRingPulse(2.4f, 0.07f);

        // 1. 心智诱引脉冲（温和聚怪微牵引）
        lureTimer += Time.deltaTime;
        if (lureTimer >= 1.2f)
        {
            lureTimer = 0f;
            TriggerLurePulse();
        }

        // 2. 每 3 秒判定一次心智反戈
        pacifyCheckTimer += Time.deltaTime;
        if (pacifyCheckTimer >= 3.0f)
        {
            pacifyCheckTimer = 0f;
            TriggerPacifyCheck();
        }
    }

    private void TriggerLurePulse()
    {
        var enemies = EnemyController.AllEnemies;
        for (int i = 0; i < enemies.Count; i++)
        {
            var e = enemies[i];
            if (e != null && e.IsAlive && !e.isPacified)
            {
                float d = Vector2.Distance(transform.position, e.transform.position);
                if (d <= lureRange && d > 1.0f)
                {
                    // 温和牵引力
                    Vector3 pullDir = (transform.position - e.transform.position).normalized;
                    e.transform.position += pullDir * 0.12f;
                }
            }
        }
    }

    private void TriggerPacifyCheck()
    {
        var enemies = EnemyController.AllEnemies;
        for (int i = 0; i < enemies.Count; i++)
        {
            var e = enemies[i];
            if (e != null && e.IsAlive && !e.isPacified)
            {
                float d = Vector2.Distance(transform.position, e.transform.position);
                if (d <= lureRange)
                {
                    int roll = Random.Range(1, 101);
                    if (roll <= pacifyChance)
                    {
                        // 寻找另一个可以攻击的敌怪
                        EnemyController targetOther = enemies.Find(other => other != null && other != e && other.IsAlive);
                        e.ApplyPacify(8.0f, targetOther);

                        if (DamageTextManager.Instance != null)
                        {
                            DamageTextManager.Instance.ShowText(e.transform.position + Vector3.up * 0.8f, "❤️ 心智驯化反戈 (8s)!", new Color(0.2f, 0.8f, 1f), 0.12f);
                        }
                    }
                }
            }
        }
    }
}
