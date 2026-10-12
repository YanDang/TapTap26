using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 5. 小型生体驯化培养箱 (furn_bio_incubator) - 对齐 gamejam-integration：
/// - 机制：
///   * 4.5m 范围内的怪物慢慢被安抚（停留约 2.5 ~ 3 秒）；
///   * 触发【安抚】状态：5 秒内不攻击玩家、走到培养箱旁发呆（头上显示“安抚”）；
///   * 挨打立刻苏醒，苏醒后 4 秒内免疫安抚。
/// </summary>
public class BioIncubatorAuraBehavior : BiomechanicalAuraBase
{
    [Header("Incubator Stats")]
    public float lureRange = 4.5f;
    public float calmSeconds = 5.0f;
    public float exposureNeeded = 2.5f;

    private readonly Dictionary<EnemyController, float> exposure = new Dictionary<EnemyController, float>();

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

        if (IsCombatPaused) return;

        var enemies = EnemyController.AllEnemies;
        for (int i = 0; i < enemies.Count; i++)
        {
            var e = enemies[i];
            if (e == null || !e.IsAlive) continue;

            if (!e.CanBePacified)
            {
                exposure.Remove(e);
                continue;
            }

            float d = Vector2.Distance(transform.position, e.transform.position);
            if (d <= lureRange)
            {
                if (!exposure.ContainsKey(e)) exposure[e] = 0f;
                exposure[e] += Time.deltaTime;

                if (exposure[e] >= exposureNeeded)
                {
                    exposure.Remove(e);
                    e.Pacify(calmSeconds, parentFurniture);

                    if (DamageTextManager.Instance != null)
                    {
                        DamageTextManager.Instance.ShowText(e.transform.position + Vector3.up * 0.8f, "🕊️ 安抚 (5s)", new Color(0.35f, 0.85f, 1f), 0.12f);
                    }
                }
            }
            else if (exposure.ContainsKey(e))
            {
                exposure[e] = Mathf.Max(0f, exposure[e] - Time.deltaTime);
                if (exposure[e] <= 0f) exposure.Remove(e);
            }
        }
    }
}
