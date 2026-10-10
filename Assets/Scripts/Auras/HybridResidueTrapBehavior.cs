using UnityEngine;

/// <summary>
/// 废料 4: 活性共生杂交残渣 (scrap_symbiotic_sludge / scrap_hybrid_residue)
/// - 机制：【恶臭黏油减速块】常驻阻滞地块
///   敌人踩踏强制减速 50%，被打碎或死亡后释放生体疗愈水雾，为近旁友方玩家恢复生命值！
/// </summary>
public class HybridResidueTrapBehavior : BiomechanicalAuraBase
{
    [Header("Residue Stats")]
    public float slowFactor = 0.5f;
    public float healAmount = 35f;

    protected override void Awake()
    {
        base.Awake();
        CreateRangeRing(1.6f, new Color(0.7f, 0.85f, 0.5f, 0.45f), "Residue_Sludge_Ring");

        if (parentFurniture != null)
        {
            parentFurniture.onFurnitureDestroyed += OnResidueDestroyed;
        }
    }

    void OnDestroy()
    {
        if (parentFurniture != null)
        {
            parentFurniture.onFurnitureDestroyed -= OnResidueDestroyed;
        }
    }

    private void OnResidueDestroyed()
    {
        // 破裂释放生体疗愈水雾
        var player = FindObjectOfType<PlayerController>();
        if (player != null)
        {
            float d = Vector2.Distance(transform.position, player.transform.position);
            if (d <= 4.0f)
            {
                player.Heal(healAmount);
                if (DamageTextManager.Instance != null)
                {
                    DamageTextManager.Instance.ShowText(player.transform.position + Vector3.up * 0.9f, $"💚 生体疗愈水雾 +{healAmount:0} HP!", new Color(0.2f, 1f, 0.5f), 0.12f);
                }
            }
        }
    }

    void Update()
    {
        if (parentFurniture != null && parentFurniture.currentState != FurnitureObject.FurnitureState.Placed)
        {
            if (rangeRing != null && rangeRing.activeSelf) rangeRing.SetActive(false);
            return;
        }

        if (rangeRing != null && !rangeRing.activeSelf) rangeRing.SetActive(true);
        UpdateRingPulse(2.2f, 0.04f);

        // 持续对踩入的怪兽施加 50% 减速
        var enemies = EnemyController.AllEnemies;
        for (int i = 0; i < enemies.Count; i++)
        {
            var e = enemies[i];
            if (e != null && e.IsAlive)
            {
                float d = Vector2.Distance(transform.position, e.transform.position);
                if (d <= 1.5f)
                {
                    e.ApplySlow(50, 0.6f);
                }
            }
        }
    }
}
