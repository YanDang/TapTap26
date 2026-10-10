using UnityEngine;

/// <summary>
/// 3. 气泡微压悬浮软床 (furn_pneumatic_bed)
/// - 分类：气压设施 | 主导元素：Gas | 主题色：荧光淡紫 (0.75, 0.52, 0.99)
/// - 机制：
///   * 反重力力场：玩家步入力场时，移动速度瞬间提升 +speedBoost%，翻滚闪避精力消耗降低 25%
///   * 轻微排斥：敌人进入边缘时受到柔性微压阻力，移动速度被衰减 30%
/// </summary>
public class PneumaticBedAuraBehavior : BiomechanicalAuraBase
{
    [Header("Pneumatic Stats")]
    public float fieldRadius = 3.6f;
    public int speedBoostPercent = 25;

    private PlayerController player;
    private bool isPlayerInside = false;

    protected override void Awake()
    {
        base.Awake();
        CreateRangeRing(fieldRadius, new Color(0.75f, 0.52f, 0.99f, 0.40f), "Anti_Gravity_Field");
        player = FindObjectOfType<PlayerController>();
    }

    void OnDisable()
    {
        ResetPlayerBuff();
    }

    void OnDestroy()
    {
        ResetPlayerBuff();
    }

    private void ResetPlayerBuff()
    {
        if (player != null && isPlayerInside)
        {
            player.moveSpeedMultiplier = 1f;
            player.staminaCostMultiplier = 1f;
            isPlayerInside = false;
        }
    }

    void Update()
    {
        if (parentFurniture != null && parentFurniture.currentState != FurnitureObject.FurnitureState.Placed)
        {
            if (rangeRing != null && rangeRing.activeSelf) rangeRing.SetActive(false);
            ResetPlayerBuff();
            return;
        }

        if (rangeRing != null && !rangeRing.activeSelf) rangeRing.SetActive(true);
        UpdateRingPulse(3.2f, 0.06f);

        if (player == null) player = FindObjectOfType<PlayerController>();

        // 玩家反重力增益检测
        if (player != null)
        {
            float distToPlayer = Vector2.Distance(transform.position, player.transform.position);
            if (distToPlayer <= fieldRadius)
            {
                if (!isPlayerInside)
                {
                    isPlayerInside = true;
                    player.moveSpeedMultiplier = 1f + (speedBoostPercent / 100f);
                    player.staminaCostMultiplier = 0.75f; // 精力消耗降低 25%

                    if (DamageTextManager.Instance != null)
                    {
                        DamageTextManager.Instance.ShowText(player.transform.position + Vector3.up * 0.8f, $"💨 反重力气垫: 移速+{speedBoostPercent}% 闪避精力-25%!", new Color(0.85f, 0.65f, 1f), 0.10f);
                    }
                }
            }
            else if (isPlayerInside)
            {
                ResetPlayerBuff();
            }
        }

        // 敌人柔性阻滞检测
        var enemies = EnemyController.AllEnemies;
        for (int i = 0; i < enemies.Count; i++)
        {
            var e = enemies[i];
            if (e != null && e.IsAlive)
            {
                float d = Vector2.Distance(transform.position, e.transform.position);
                if (d <= fieldRadius)
                {
                    e.ApplySlow(30, 0.5f);
                }
            }
        }
    }
}
