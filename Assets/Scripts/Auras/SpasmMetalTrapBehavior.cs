using UnityEngine;

/// <summary>
/// 废料 1: 抽搐的痉挛金属团 (scrap_spasm_metal)
/// - 机制：【超级弹力跳板】踩踏触发 (怪物或踢击碰撞)
///   如同超强压缩弹簧，将踩中的怪物直接弹飞 2.5 ~ 6.0m，撞墙眩晕并附加 1.2 ~ 3.0s 麻痹！随后自身解体！
/// </summary>
public class SpasmMetalTrapBehavior : BiomechanicalAuraBase
{
    [Header("Spring Trap Stats")]
    public float knockbackForce = 5.2f;
    public float stunDuration = 2.0f;

    private bool isTriggered = false;

    protected override void Awake()
    {
        base.Awake();
        CreateRangeRing(1.0f, new Color(0.98f, 0.45f, 0.85f, 0.5f), "Spring_Ring");
    }

    private Vector3 anchorPos;
    private bool hasAnchor = false;

    void Update()
    {
        if (isTriggered) return;

        // 持续微弱痉挛抖动特效：仅在放置锚定状态下围绕当前地块锚点微颤，绝不可覆写 localPosition 为原点
        if (parentFurniture != null && parentFurniture.currentState == FurnitureObject.FurnitureState.Placed)
        {
            if (!hasAnchor)
            {
                anchorPos = transform.position;
                hasAnchor = true;
            }
            float shake = Mathf.Sin(Time.time * 24f) * 0.025f;
            transform.position = new Vector3(anchorPos.x + shake, anchorPos.y, anchorPos.z);
        }
        else if (hasAnchor)
        {
            transform.position = anchorPos;
            hasAnchor = false;
        }

        // 碰撞检测：怪兽踩踏
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, 0.85f);
        foreach (var col in hits)
        {
            var enemy = col.GetComponent<EnemyController>() ?? col.GetComponentInParent<EnemyController>();
            if (enemy != null && enemy.IsAlive && !enemy.isPacified)
            {
                TriggerSpringTrap(enemy);
                break;
            }
        }
    }

    void OnDisable()
    {
        if (hasAnchor)
        {
            transform.position = anchorPos;
            hasAnchor = false;
        }
    }

    private void TriggerSpringTrap(EnemyController enemy)
    {
        if (hasAnchor)
        {
            transform.position = anchorPos;
            hasAnchor = false;
        }

        isTriggered = true;

        Vector3 flingDir = (enemy.transform.position - transform.position).normalized;
        if (flingDir.sqrMagnitude < 0.01f) flingDir = Vector3.up;

        enemy.TakeDamage(30f, 60f, transform.position);
        enemy.ApplyKnockback(flingDir, knockbackForce * 0.15f, 0.45f);
        enemy.ApplyParalysis(stunDuration);

        if (DamageTextManager.Instance != null)
        {
            DamageTextManager.Instance.ShowText(enemy.transform.position + Vector3.up * 0.8f, "🌀 超级弹力飞射! (眩晕麻痹)", new Color(1f, 0.4f, 0.9f), 0.13f);
        }

        if (parentFurniture != null)
        {
            parentFurniture.TriggerShatterBurst();
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
