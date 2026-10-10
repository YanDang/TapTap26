using UnityEngine;

/// <summary>
/// 废料 2: 漏电的烂泥珊瑚块 (scrap_mud_coral)
/// - 机制：【带电烂泥陷阱】范围常驻陷阱
///   半径 2.0 ~ 4.5m 铺开潮湿带电泥潭，踩入者持续减速 40%，每秒遭受 12 ~ 52 点电击！
/// </summary>
public class MudCoralTrapBehavior : BiomechanicalAuraBase
{
    [Header("Mud Trap Stats")]
    public float trapRadius = 3.2f;
    public int shockDmg = 20;

    private float tickTimer = 0f;

    protected override void Awake()
    {
        base.Awake();
        CreateRangeRing(trapRadius, new Color(0.85f, 0.55f, 0.95f, 0.45f), "Electric_Mud_Puddle");
    }

    void Update()
    {
        if (parentFurniture != null && parentFurniture.currentState != FurnitureObject.FurnitureState.Placed)
        {
            if (rangeRing != null && rangeRing.activeSelf) rangeRing.SetActive(false);
            return;
        }

        if (rangeRing != null && !rangeRing.activeSelf) rangeRing.SetActive(true);
        UpdateRingPulse(3.0f, 0.05f);

        tickTimer += Time.deltaTime;
        if (tickTimer >= 1.0f)
        {
            tickTimer = 0f;
            ApplyMudShock();
        }
    }

    private void ApplyMudShock()
    {
        var enemies = EnemyController.AllEnemies;
        for (int i = 0; i < enemies.Count; i++)
        {
            var e = enemies[i];
            if (e != null && e.IsAlive)
            {
                float d = Vector2.Distance(transform.position, e.transform.position);
                if (d <= trapRadius)
                {
                    e.ApplySlow(40, 1.2f);
                    e.TakeDamage(shockDmg, 20f, transform.position);

                    if (DamageTextManager.Instance != null)
                    {
                        DamageTextManager.Instance.ShowText(e.transform.position + Vector3.up * 0.6f, $"⚡ 漏电烂泥 -{shockDmg}!", new Color(0.85f, 0.5f, 0.95f), 0.09f);
                    }
                }
            }
        }
    }
}
