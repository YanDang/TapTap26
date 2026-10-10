using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 1. 电鳗电机发电机组 (furn_eel_generator)
/// - 分类：电网设施 | 主导元素：Electric | 主题色：暖黄电弧 (0.98, 0.82, 0.18)
/// - 机制：
///   * 常驻供电：以自身为圆心半径 4.5m 形成电网供电区，赋能附近造水塔与喷水炮
///   * 静电反制：靠近半径内的敌怪，每 1.5 秒受到一次电弧跳跃伤害，附加 1.0 秒麻痹
///   * 水电协同：为重叠的水渍赋予持续导电与感电暴击特性！
/// </summary>
public class EelGeneratorAuraBehavior : BiomechanicalAuraBase
{
    [Header("Generator Stats")]
    public float paralyzeRadius = 3.2f;
    public int shockDmg = 28;
    public float powerGridRadius = 4.5f;
    public float interval = 1.5f;

    private float timer = 0f;
    public static readonly List<EelGeneratorAuraBehavior> ActiveGenerators = new List<EelGeneratorAuraBehavior>();

    protected override void Awake()
    {
        base.Awake();
        CreateRangeRing(paralyzeRadius, new Color(0.98f, 0.82f, 0.18f, 0.45f), "Electric_Aura_Ring");
    }

    void OnEnable()
    {
        if (!ActiveGenerators.Contains(this))
            ActiveGenerators.Add(this);
    }

    void OnDisable()
    {
        ActiveGenerators.Remove(this);
    }

    void Update()
    {
        if (parentFurniture != null && parentFurniture.currentState != FurnitureObject.FurnitureState.Placed)
        {
            if (rangeRing != null && rangeRing.activeSelf) rangeRing.SetActive(false);
            return;
        }

        if (rangeRing != null && !rangeRing.activeSelf) rangeRing.SetActive(true);
        UpdateRingPulse(4.2f, 0.08f);

        timer += Time.deltaTime;
        if (timer >= interval)
        {
            timer = 0f;
            TriggerElectricPulse();
        }
    }

    private void TriggerElectricPulse()
    {
        // 查找半径内的所有活体敌人
        var enemies = EnemyController.AllEnemies;
        for (int i = 0; i < enemies.Count; i++)
        {
            var e = enemies[i];
            if (e != null && e.IsAlive && !e.isPacified)
            {
                float d = Vector2.Distance(transform.position, e.transform.position);
                if (d <= paralyzeRadius)
                {
                    e.TakeDamage(shockDmg, 25f, transform.position);
                    e.ApplyParalysis(1.0f);

                    if (DamageTextManager.Instance != null)
                    {
                        DamageTextManager.Instance.ShowText(e.transform.position + Vector3.up * 0.7f, $"⚡ 电弧跳跃 -{shockDmg}!", new Color(1f, 0.88f, 0.2f), 0.11f);
                    }
                }
            }
        }
    }

    /// <summary>
    /// 检测某世界坐标是否处于任意电鳗电机的常驻电网供电范围内 (4.5m)
    /// </summary>
    public static bool IsPositionPowered(Vector3 pos)
    {
        for (int i = 0; i < ActiveGenerators.Count; i++)
        {
            var gen = ActiveGenerators[i];
            if (gen != null && gen.parentFurniture != null && gen.parentFurniture.currentState == FurnitureObject.FurnitureState.Placed)
            {
                if (Vector2.Distance(pos, gen.transform.position) <= gen.powerGridRadius)
                    return true;
            }
        }
        return false;
    }
}
