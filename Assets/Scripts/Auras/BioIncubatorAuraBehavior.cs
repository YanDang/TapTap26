using System.Collections.Generic;
using UnityEngine;
using GameJam;

/// <summary>
/// 5. 小型生体驯化培养箱 (furn_bio_incubator) - 对齐 gamejam-integration：
/// - 机制与特效：
///   * 淡蓝荧光呼吸光环与慢呼吸舒缩动效 (Resources/UI/ui_ring)
///   * 4.5m 范围内的怪物慢慢被安抚（积累安抚进度约 2.5 ~ 3 秒）；
///   * 触发【安抚】状态：5 秒内不攻击玩家、走到培养箱旁发呆，身上呈现淡蓝荧光；
///   * 安抚成功绽放淡蓝星火 (VFX_ParrySpark) 与轻灵音效；
///   * 挨打立刻苏醒；苏醒后赋予 4 秒免疫保护期 (immuneSeconds)，杜绝无限循环控怪！
/// </summary>
public class BioIncubatorAuraBehavior : BiomechanicalAuraBase
{
    [Header("Incubator Stats")]
    public float lureRange = 4.5f;
    public float calmSeconds = 5.0f;
    public float exposureNeeded = 2.5f;
    public float immuneSeconds = 4.0f;

    private readonly Dictionary<EnemyController, float> exposure = new Dictionary<EnemyController, float>();
    private Vector3 originalVisualScale = Vector3.one;
    public static readonly Color Glow = new Color(0.45f, 0.85f, 1f);

    protected override void Awake()
    {
        base.Awake();
        CreateRangeRing(lureRange, new Color(Glow.r, Glow.g, Glow.b, 0.35f), "Mind_Resonance_Ring");

        if (parentFurniture != null && parentFurniture.furnitureRenderer != null)
        {
            originalVisualScale = parentFurniture.furnitureRenderer.transform.localScale;
        }
    }

    void Update()
    {
        bool isPlaced = (parentFurniture != null && parentFurniture.currentState == FurnitureObject.FurnitureState.Placed);

        if (!isPlaced)
        {
            if (rangeRing != null && rangeRing.activeSelf) rangeRing.SetActive(false);
            exposure.Clear();
            return;
        }

        if (rangeRing != null && !rangeRing.activeSelf) rangeRing.SetActive(true);

        float t = Time.time;

        // 慢呼吸舒缩动效 (来自 gamejam-integration)
        if (parentFurniture != null && parentFurniture.furnitureRenderer != null)
        {
            float breath = Mathf.Sin(t * 1.4f) * 0.035f;
            parentFurniture.furnitureRenderer.transform.localScale = new Vector3(
                originalVisualScale.x * (1f - breath * 0.3f),
                originalVisualScale.y * (1f + breath),
                originalVisualScale.z
            );
        }

        // 荧光光圈呼吸脉冲
        if (rangeRingRenderer != null)
        {
            float alpha = 0.22f + 0.12f * Mathf.Sin(t * 2.2f);
            rangeRingRenderer.color = new Color(Glow.r, Glow.g, Glow.b, alpha);
        }

        UpdateRingPulse(2.2f, 0.05f);

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

                    // 安抚成功绽放淡蓝星火粒子与音效 (来自 gamejam-integration)
                    Vfx.Spawn("VFX_ParrySpark", e.transform.position + Vector3.up * 1.2f, 0f, 1.1f, false, null, Glow);
                    Sfx.Play("sfx_parry", 0.35f);

                    if (DamageTextManager.Instance != null)
                    {
                        DamageTextManager.Instance.ShowText(e.transform.position + Vector3.up * 0.8f, $"🕊️ 安抚 ({calmSeconds:0.#}s)", new Color(0.45f, 0.85f, 1f), 0.12f);
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
