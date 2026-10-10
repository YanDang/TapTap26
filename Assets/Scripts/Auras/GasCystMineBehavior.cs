using System.Collections;
using UnityEngine;

/// <summary>
/// 废料 3: 膨胀的气囊疙瘩 (scrap_gas_cyst)
/// - 机制：【烈性生物雷】受击 / 接近延时自爆
///   怪物靠近 1.2m 内或受到任何攻击时剧烈抽搐，1 秒倒计时后自爆，产生半径 2.0 ~ 4.5m 的高额破甲冲击爆发伤害！
/// </summary>
public class GasCystMineBehavior : BiomechanicalAuraBase
{
    [Header("Mine Stats")]
    public float triggerDistance = 1.2f;
    public float blastRadius = 3.2f;
    public float blastDamage = 48f;

    private bool isArmed = false;

    protected override void Awake()
    {
        base.Awake();
        CreateRangeRing(blastRadius, new Color(0.95f, 0.40f, 0.55f, 0.35f), "Blast_Radius_Ring");

        if (parentFurniture != null)
        {
            parentFurniture.onHitTaken += OnMineHit;
        }
    }

    void OnDestroy()
    {
        if (parentFurniture != null)
        {
            parentFurniture.onHitTaken -= OnMineHit;
        }
    }

    private void OnMineHit(float dmg, EnemyController attacker)
    {
        if (!isArmed)
        {
            StartCoroutine(CountdownAndExplode());
        }
    }

    void Update()
    {
        if (isArmed) return;

        // 接近检测
        var enemies = EnemyController.AllEnemies;
        for (int i = 0; i < enemies.Count; i++)
        {
            var e = enemies[i];
            if (e != null && e.IsAlive)
            {
                if (Vector2.Distance(transform.position, e.transform.position) <= triggerDistance)
                {
                    StartCoroutine(CountdownAndExplode());
                    break;
                }
            }
        }
    }

    private IEnumerator CountdownAndExplode()
    {
        isArmed = true;

        if (DamageTextManager.Instance != null)
        {
            DamageTextManager.Instance.ShowText(transform.position + Vector3.up * 0.7f, "⚠️ 气囊急剧膨胀! (1.0s)", new Color(1f, 0.2f, 0.2f), 0.12f);
        }

        // 1秒剧烈抽搐与红光警告
        float countdown = 1.0f;
        Vector3 anchorPos = transform.position;
        while (countdown > 0f)
        {
            countdown -= Time.deltaTime;
            float shake = Mathf.Sin(Time.time * 40f) * 0.08f;
            if (parentFurniture != null && parentFurniture.furnitureRenderer != null)
            {
                parentFurniture.furnitureRenderer.color = Color.Lerp(Color.red, Color.white, Mathf.PingPong(Time.time * 8f, 1f));
            }
            transform.position = anchorPos + new Vector3(shake, 0f, 0f);
            yield return null;
        }
        transform.position = anchorPos;

        // 自爆伤害结算
        if (DamageTextManager.Instance != null)
        {
            DamageTextManager.Instance.ShowText(transform.position + Vector3.up * 0.8f, $"💥 烈性生物雷自爆 -{blastDamage:0}!", new Color(1f, 0.3f, 0.1f), 0.14f);
        }

        Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, blastRadius);
        foreach (var col in colliders)
        {
            var e = col.GetComponent<EnemyController>() ?? col.GetComponentInParent<EnemyController>();
            if (e != null && e.IsAlive)
            {
                e.TakeDamage(blastDamage, 75f, transform.position);
                Vector3 blastDir = (e.transform.position - transform.position).normalized;
                e.ApplyKnockback(blastDir, 0.6f, 0.35f);
            }
        }

        if (parentFurniture != null)
        {
            Destroy(parentFurniture.gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
