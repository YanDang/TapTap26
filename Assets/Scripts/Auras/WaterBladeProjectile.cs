using UnityEngine;

/// <summary>
/// 高压水刃飞弹：高速弹道冲击，造成水系破防与直线击退
/// </summary>
public class WaterBladeProjectile : MonoBehaviour
{
    public float speed = 9.5f;
    public float damage = 35f;
    public float guardBreak = 40f;
    public float knockbackPower = 5.0f;
    public Vector3 direction;

    private float lifetime = 1.2f;

    void Start()
    {
        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        transform.position += direction * (speed * Time.deltaTime);

        // 碰撞检测
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, 0.45f);
        foreach (var col in hits)
        {
            var enemy = col.GetComponent<EnemyController>() ?? col.GetComponentInParent<EnemyController>();
            if (enemy != null && enemy.IsAlive)
            {
                enemy.TakeDamage(damage, guardBreak, transform.position);
                enemy.ApplyKnockback(direction, knockbackPower * 0.1f, 0.3f);

                if (DamageTextManager.Instance != null)
                {
                    DamageTextManager.Instance.ShowText(enemy.transform.position + Vector3.up * 0.6f, "🌊 高压水刃击退!", new Color(0.15f, 0.85f, 0.95f), 0.11f);
                }

                Destroy(gameObject);
                return;
            }
        }
    }
}
